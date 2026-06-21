#!/usr/bin/env bash
set -Eeuo pipefail

SERVICE_NAME="${SERVICE_NAME:-zadana-api}"
TARGET_ENV="${TARGET_ENV:-/home/zadna0/config/zadana-api.env}"
SMTP_HOST="smtp-relay.brevo.com"
# cPanel SMTP Restrictions can redirect outbound 25/465/587 to local Exim.
# Brevo's submission port 2525 avoids that redirect while still using STARTTLS.
SMTP_PORT="2525"
FROM_EMAIL="${FROM_EMAIL:-no-reply@zadna0.com}"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [[ ! -f "${TARGET_ENV}" ]]; then
  echo "Environment file not found: ${TARGET_ENV}" >&2
  exit 1
fi

read -rp "Enter Brevo SMTP login: " SMTP_USERNAME
read -rsp "Enter the NEW Brevo SMTP key: " SMTP_PASSWORD
echo

if [[ -z "${SMTP_USERNAME}" || -z "${SMTP_PASSWORD}" ]]; then
  echo "SMTP login and key are required." >&2
  exit 1
fi

if [[ "${SMTP_USERNAME}" == *$'\n'* || "${SMTP_USERNAME}" == *$'\r'* ||
      "${SMTP_PASSWORD}" == *$'\n'* || "${SMTP_PASSWORD}" == *$'\r'* ]]; then
  echo "SMTP values cannot contain newlines." >&2
  exit 1
fi

quote_env_value() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '"%s"' "${value}"
}

echo "Checking STARTTLS connection to ${SMTP_HOST}:${SMTP_PORT}..."
if command -v openssl >/dev/null 2>&1; then
  if ! timeout 20 openssl s_client \
      -starttls smtp \
      -connect "${SMTP_HOST}:${SMTP_PORT}" \
      -servername "${SMTP_HOST}" \
      -brief </dev/null >/dev/null 2>&1; then
    echo "STARTTLS connection to Brevo failed; configuration was not changed." >&2
    unset SMTP_PASSWORD
    exit 1
  fi
fi

backup="${TARGET_ENV}.bak.brevo.$(date +%Y%m%d%H%M%S)"
owner="$(stat -c '%U:%G' "${TARGET_ENV}")"
cp --preserve=mode,ownership,timestamps "${TARGET_ENV}" "${backup}"

sed -i \
  -e '/^ZADANA_Email__FromEmail=/d' \
  -e '/^ZADANA_Email__Smtp__/d' \
  "${TARGET_ENV}"

{
  printf '\n# Brevo SMTP relay\n'
  printf 'ZADANA_Email__FromEmail=%s\n' "$(quote_env_value "${FROM_EMAIL}")"
  printf 'ZADANA_Email__Smtp__Host=%s\n' "$(quote_env_value "${SMTP_HOST}")"
  printf 'ZADANA_Email__Smtp__Port=%s\n' "$(quote_env_value "${SMTP_PORT}")"
  printf 'ZADANA_Email__Smtp__Security=%s\n' "$(quote_env_value "StartTls")"
  printf 'ZADANA_Email__Smtp__Username=%s\n' "$(quote_env_value "${SMTP_USERNAME}")"
  printf 'ZADANA_Email__Smtp__Password=%s\n' "$(quote_env_value "${SMTP_PASSWORD}")"
  printf 'ZADANA_Email__Smtp__TimeoutSeconds=%s\n' "$(quote_env_value "30")"
  printf 'ZADANA_Email__Smtp__RequireAuthentication=%s\n' "$(quote_env_value "true")"
} >> "${TARGET_ENV}"

unset SMTP_PASSWORD
chown "${owner}" "${TARGET_ENV}"
chmod 600 "${TARGET_ENV}"

systemctl restart "${SERVICE_NAME}"
sleep 8

if ! systemctl is-active --quiet "${SERVICE_NAME}"; then
  echo "API failed after Brevo configuration. Restoring ${backup}." >&2
  cp --preserve=mode,ownership,timestamps "${backup}" "${TARGET_ENV}"
  systemctl restart "${SERVICE_NAME}"
  systemctl status "${SERVICE_NAME}" --no-pager -l || true
  exit 1
fi

systemctl status "${SERVICE_NAME}" --no-pager -l
curl --fail --silent --show-error "http://127.0.0.1:5000/health"
printf '\nBrevo SMTP settings installed. Backup: %s\n' "${backup}"

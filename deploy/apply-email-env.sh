#!/usr/bin/env bash
set -Eeuo pipefail

SERVICE_NAME="${SERVICE_NAME:-zadana-api}"
TARGET_ENV="${TARGET_ENV:-/home/zadna0/config/zadana-api.env}"
SMTP_HOST="${SMTP_HOST:-mail.zadna0.com}"
SMTP_PORT="${SMTP_PORT:-465}"
SMTP_USER="${SMTP_USER:-no-reply@zadna0.com}"
FROM_NAME="${FROM_NAME:-Zadna}"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run as root: sudo bash $0" >&2
  exit 1
fi

if [[ ! -f "${TARGET_ENV}" ]]; then
  echo "Environment file not found: ${TARGET_ENV}" >&2
  exit 1
fi

read -rsp "Enter password for ${SMTP_USER}: " SMTP_PASSWORD
echo

if [[ -z "${SMTP_PASSWORD}" || "${SMTP_PASSWORD}" == *$'\n'* || "${SMTP_PASSWORD}" == *$'\r'* ]]; then
  echo "SMTP password is empty or contains a newline." >&2
  exit 1
fi

quote_env_value() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  printf '"%s"' "${value}"
}

echo "Checking TLS connection to ${SMTP_HOST}:${SMTP_PORT}..."
if command -v openssl >/dev/null 2>&1; then
  if ! timeout 15 openssl s_client \
      -connect "${SMTP_HOST}:${SMTP_PORT}" \
      -servername "${SMTP_HOST}" \
      -brief </dev/null >/dev/null 2>&1; then
    echo "TLS connection to SMTP server failed; environment file was not changed." >&2
    unset SMTP_PASSWORD
    exit 1
  fi
else
  echo "openssl is unavailable; skipping the TLS connectivity pre-check."
fi

backup="${TARGET_ENV}.bak.$(date +%Y%m%d%H%M%S)"
owner="$(stat -c '%U:%G' "${TARGET_ENV}")"
cp --preserve=mode,ownership,timestamps "${TARGET_ENV}" "${backup}"

sed -i \
  -e '/^ZADANA_Email__FromEmail=/d' \
  -e '/^ZADANA_Email__FromName=/d' \
  -e '/^ZADANA_Email__SupportEmail=/d' \
  -e '/^ZADANA_Email__HelloEmail=/d' \
  -e '/^ZADANA_Email__InfoEmail=/d' \
  -e '/^ZADANA_Email__ContactEmail=/d' \
  -e '/^ZADANA_Email__Smtp__/d' \
  "${TARGET_ENV}"

{
  printf '\n# SMTP email\n'
  printf 'ZADANA_Email__FromEmail=%s\n' "$(quote_env_value "${SMTP_USER}")"
  printf 'ZADANA_Email__FromName=%s\n' "$(quote_env_value "${FROM_NAME}")"
  printf 'ZADANA_Email__SupportEmail=%s\n' "$(quote_env_value "support@zadna0.com")"
  printf 'ZADANA_Email__HelloEmail=%s\n' "$(quote_env_value "hello@zadna0.com")"
  printf 'ZADANA_Email__InfoEmail=%s\n' "$(quote_env_value "info@zadna0.com")"
  printf 'ZADANA_Email__ContactEmail=%s\n' "$(quote_env_value "contact@zadna0.com")"
  printf 'ZADANA_Email__Smtp__Host=%s\n' "$(quote_env_value "${SMTP_HOST}")"
  printf 'ZADANA_Email__Smtp__Port=%s\n' "$(quote_env_value "${SMTP_PORT}")"
  printf 'ZADANA_Email__Smtp__Security=%s\n' "$(quote_env_value "SslOnConnect")"
  printf 'ZADANA_Email__Smtp__Username=%s\n' "$(quote_env_value "${SMTP_USER}")"
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
  echo "Service failed after SMTP configuration. Restoring ${backup}." >&2
  cp --preserve=mode,ownership,timestamps "${backup}" "${TARGET_ENV}"
  systemctl restart "${SERVICE_NAME}"
  systemctl status "${SERVICE_NAME}" --no-pager -l || true
  exit 1
fi

systemctl status "${SERVICE_NAME}" --no-pager -l
curl --fail --silent --show-error "http://127.0.0.1:5000/health"
printf '\nSMTP settings installed. Backup: %s\n' "${backup}"

#!/usr/bin/env bash
set -Eeuo pipefail

SERVICE_NAME="${SERVICE_NAME:-zadana-api}"
TARGET_ENV="${TARGET_ENV:-/home/zadna0/config/zadana-api.env}"
SOURCE_ENV="${1:-$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/secrets.env}"

if [[ "${EUID}" -ne 0 ]]; then
  echo "Run this script as root: sudo bash $0" >&2
  exit 1
fi

if [[ ! -f "${TARGET_ENV}" ]]; then
  echo "Target environment file does not exist: ${TARGET_ENV}" >&2
  exit 1
fi

if [[ ! -f "${SOURCE_ENV}" ]]; then
  echo "OneSignal secrets file does not exist: ${SOURCE_ENV}" >&2
  exit 1
fi

backup="${TARGET_ENV}.bak.$(date +%Y%m%d%H%M%S)"
owner="$(stat -c '%U:%G' "${TARGET_ENV}")"

systemctl stop "${SERVICE_NAME}"
cp --preserve=mode,ownership,timestamps "${TARGET_ENV}" "${backup}"

sed -i '/^ZADANA_OneSignal__/d' "${TARGET_ENV}"
{
  printf '\n# OneSignal applications\n'
  cat "${SOURCE_ENV}"
  printf '\n'
} >> "${TARGET_ENV}"

chown "${owner}" "${TARGET_ENV}"
chmod 600 "${TARGET_ENV}"

systemctl reset-failed "${SERVICE_NAME}"
systemctl start "${SERVICE_NAME}"
sleep 8

systemctl status "${SERVICE_NAME}" --no-pager -l
curl --fail --silent --show-error "http://127.0.0.1:5000/health"
printf '\nOneSignal settings installed. Backup: %s\n' "${backup}"

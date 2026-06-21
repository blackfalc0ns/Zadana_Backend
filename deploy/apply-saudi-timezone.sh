#!/usr/bin/env bash
set -euo pipefail

TARGET_ENV="${TARGET_ENV:-/home/zadna0/config/zadana-api.env}"
SERVICE_NAME="${SERVICE_NAME:-zadana-api}"
BACKUP="${TARGET_ENV}.bak.timezone.$(date +%Y%m%d%H%M%S)"

if [[ ! -f "$TARGET_ENV" ]]; then
  echo "Environment file not found: $TARGET_ENV" >&2
  exit 1
fi

cp --preserve=mode,ownership,timestamps "$TARGET_ENV" "$BACKUP"
sed -i '/^TZ=/d' "$TARGET_ENV"
printf '\n# Saudi Arabia timezone\nTZ=Asia/Riyadh\n' >> "$TARGET_ENV"

timedatectl set-timezone Asia/Riyadh
systemctl restart "$SERVICE_NAME"

echo "Saudi timezone installed. Backup: $BACKUP"
timedatectl show --property=Timezone --value
systemctl is-active "$SERVICE_NAME"

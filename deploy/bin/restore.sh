#!/usr/bin/env bash
# Restores an encrypted backup into the production database (ADR-0022 §4, docs/isletim.md §6). Stops the API,
# decrypts with the offline private key, pg_restore as nirengi_owner in one transaction, migrate, renews the sync
# epoch (devices then send what the backup lacks), starts the API and waits for Healthy.
# Usage: restore.sh <nirengi-....dump.age> [--yes]     private key: $RESTORE_KEY (default /root/restore.key)
set -euo pipefail
# shellcheck source=deploy/bin/lib.sh
. "$(dirname "$(readlink -f "$0")")/lib.sh"

file=${1:?usage: restore.sh <file.dump.age> [--yes]}
key=${RESTORE_KEY:-/root/restore.key}
[[ -f $file ]] || die "no such file: $file"
[[ -f $key ]] || die "private key missing: $key (copy it for the restore, shred it afterwards)"
if [[ -f $file.sha256 ]]; then
  (cd "$(dirname "$file")" && sha256sum -c --quiet "$(basename "$file").sha256") || die "checksum mismatch: $file"
else
  log "no checksum file next to $file; continuing"
fi

if [[ ${2:-} != --yes ]]; then
  read -r -p "This replaces the live database with $(basename "$file"). Type RESTORE to go on: " answer
  [[ $answer == RESTORE ]] || die "cancelled"
fi

log "stopping api"
compose stop api
log "restoring $(basename "$file")"
age -d -i "$key" "$file" \
  | compose exec -T postgres pg_restore -U postgres -d nirengi --clean --if-exists --no-owner --role=nirengi_owner --single-transaction
log "migrating"
compose run --rm migrate
log "renewing the sync epoch"
compose exec -T postgres psql -U postgres -d nirengi -v ON_ERROR_STOP=1 -c "UPDATE platform.sync_epoch SET epoch = gen_random_uuid()"
compose up -d api
wait_ready 180 || die "API not Healthy after the restore; see: docker compose logs api"
printf '%s restore %s ok\n' "$(date -u +%FT%TZ)" "$(basename "$file")" >> "$NIRENGI_DIR/deploy-history"
log "restored; now shred the private key: shred -u $key"

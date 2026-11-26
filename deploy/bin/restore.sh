#!/usr/bin/env bash
# Restores an encrypted backup into the production database (ADR-0022 §4, docs/isletim.md §6).
#   1. safety backup of the current database (--tag pre-restore), 2. API stopped,
#   3. ONE transaction: drop the module schemas (objects newer than the backup included), load the dump as
#      nirengi_owner, renew the sync epoch (devices then send what the backup lacks),
#   4. migrate, 5. API started, Healthy awaited.
# A failure before step 3 commits leaves the data unchanged and restarts the API.
# Usage: restore.sh <nirengi-....dump.age> [--yes]     private key: $RESTORE_KEY (default /root/restore.key)
set -euo pipefail
here=$(dirname "$(readlink -f "$0")")
# shellcheck source=deploy/bin/lib.sh
. "$here/lib.sh"

file=${1:?usage: restore.sh <file.dump.age> [--yes]}
key=${RESTORE_KEY:-/root/restore.key}
[[ -f $file ]] || die "no such file: $file"
[[ -f $key ]] || die "private key missing: $key (copy it for the restore, shred it afterwards)"
[[ $(stat -c %a "$key") == 400 ]] || log "warning: $key should be mode 0400"
if [[ -f $file.sha256 ]]; then
  (cd "$(dirname "$file")" && sha256sum -c --quiet "$(basename "$file").sha256") || die "checksum mismatch: $file"
else
  log "no checksum file next to $file; continuing"
fi
age -d -i "$key" -o /dev/null "$file" || die "the key does not decrypt $file"

if [[ ${2:-} != --yes ]]; then
  read -r -p "This replaces the live database with $(basename "$file"). Type RESTORE to go on: " answer
  [[ $answer == RESTORE ]] || die "cancelled"
fi

stage=safety-backup
# shellcheck disable=SC2317,SC2329 # EXIT trap
finish() {
  local status=$?
  ((status == 0)) && return
  case $stage in
    safety-backup | restore)
      log "restore failed at '$stage'; the data is unchanged, starting the API again"
      compose up -d api || true
      ;;
    migrate)
      log "restore committed but migrate failed: do NOT start the API before 'docker compose run --rm migrate' succeeds"
      ;;
    *) log "restore failed at '$stage'" ;;
  esac
  log "shred the private key when done: shred -u $key"
}
trap finish EXIT

log "safety backup of the current database"
"$here/backup.sh" --tag pre-restore >/dev/null

stage=restore
log "stopping api"
compose stop api
schemas=$(compose exec -T postgres psql -U postgres -d nirengi -tA -c \
  "SELECT string_agg(quote_ident(nspname), ', ') FROM pg_namespace WHERE nspowner = 'nirengi_owner'::regrole")
log "restoring $(basename "$file") (replacing: ${schemas:-nothing})"
{
  echo "SET client_min_messages = warning;"
  echo "SET ROLE nirengi_owner;"
  [[ -z $schemas ]] || echo "DROP SCHEMA IF EXISTS $schemas CASCADE;"
  age -d -i "$key" "$file" | compose exec -T postgres pg_restore --no-owner -f -
  echo "RESET ROLE;"
  echo "UPDATE platform.sync_epoch SET epoch = gen_random_uuid();"
} | compose exec -T postgres psql -U postgres -d nirengi -q --single-transaction -v ON_ERROR_STOP=1 >/dev/null

stage=migrate
log "migrating"
compose run --rm migrate

stage=start
compose up -d api
wait_ready 180 || die "API not Healthy after the restore; see: docker compose logs api"
printf '%s restore %s ok\n' "$(date -u +%FT%TZ)" "$(basename "$file")" >>"$NIRENGI_DIR/deploy-history"
log "restored; now shred the private key: shred -u $key"

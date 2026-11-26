#!/usr/bin/env bash
# Encrypted logical backup (ADR-0022 §4): pg_dump -Fc → checked with pg_restore --list → age (public key only on
# the server) → $BACKUP_DIR/nirengi-<UTC>-<tag>.dump.age + .sha256 + .counts (row counts for verify-restore.sh).
# Keeps 7 days of daily/manual backups and the last 3 pre-deploy ones. Usage: backup.sh [--tag daily|pre-deploy|manual]
set -euo pipefail
# shellcheck source=deploy/bin/lib.sh
. "$(dirname "$(readlink -f "$0")")/lib.sh"

tag=daily
if [[ ${1:-} == --tag ]]; then
  tag=${2:?--tag needs a value}
fi
[[ $tag =~ ^[a-z][a-z-]{0,19}$ ]] || die "bad tag: $tag"
recipient=${AGE_RECIPIENT_FILE:-$NIRENGI_DIR/backup.pub}
[[ -s $recipient ]] || die "age public key missing: $recipient"

hc_ping "${HC_BACKUP_URL:-}" start
trap 'hc_ping "${HC_BACKUP_URL:-}" fail' ERR

install -d -m 0750 "$BACKUP_DIR"
name="nirengi-$(date -u +%Y%m%dT%H%M%SZ)-$tag"
partial="$BACKUP_DIR/.$name.partial"

# The archive is listed while it is encrypted: an unreadable dump fails the run before anything is kept.
compose exec -T postgres pg_dump -U postgres -d nirengi -Fc \
  | tee >(age -R "$recipient" -o "$partial") \
  | compose exec -T postgres pg_restore --list >/dev/null
wait $! # the age process substitution
[[ -s $partial ]] || die "empty backup"

# Row counts of the core tables at dump time (table names and numbers only).
compose exec -T postgres psql -U postgres -d nirengi -tA -F ' ' -c "
  SELECT 'identity.users', count(*) FROM identity.users
  UNION ALL SELECT 'curriculum.topics', count(*) FROM curriculum.topics
  UNION ALL SELECT 'tracking.topic_tests', count(*) FROM tracking.topic_tests
  UNION ALL SELECT 'tracking.mock_exams', count(*) FROM tracking.mock_exams" > "$BACKUP_DIR/.$name.counts"

mv "$partial" "$BACKUP_DIR/$name.dump.age"
mv "$BACKUP_DIR/.$name.counts" "$BACKUP_DIR/$name.counts"
(cd "$BACKUP_DIR" && sha256sum "$name.dump.age" > "$name.dump.age.sha256")
chmod 0640 "$BACKUP_DIR/$name".*

# Retention (copy 1): 7 days of daily/manual, the 3 newest pre-deploy.
prune() { for f in "$@"; do rm -f "$f" "${f%.dump.age}.counts" "$f.sha256"; done; }
mapfile -t old < <(find "$BACKUP_DIR" -maxdepth 1 -name 'nirengi-*.dump.age' ! -name '*-pre-deploy.dump.age' -mtime +7)
prune "${old[@]}"
mapfile -t predeploy < <(find "$BACKUP_DIR" -maxdepth 1 -name 'nirengi-*-pre-deploy.dump.age' | sort -r | tail -n +4)
prune "${predeploy[@]}"
find "$BACKUP_DIR" -maxdepth 1 -name '.nirengi-*' -mmin +60 -delete

log "backup $name.dump.age ($(du -h "$BACKUP_DIR/$name.dump.age" | cut -f1))"
trap - ERR
hc_ping "${HC_BACKUP_URL:-}"
echo "$BACKUP_DIR/$name.dump.age"

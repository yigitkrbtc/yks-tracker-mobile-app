#!/usr/bin/env bash
# Encrypted logical backup (ADR-0022 §4): pg_dump -Fc → checked with pg_restore --list → age (public key only on
# the server) → $BACKUP_DIR/nirengi-<UTC>-<tag>.dump.age + .sha256 + .counts. The dump and the counts read one
# exported snapshot, so verify-restore.sh can compare them exactly. Copy 1 keeps 7 days of daily/manual backups
# and the last 3 pre-deploy/pre-restore ones (none older than 35 days). Usage: backup.sh [--tag <tag>]
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

name="nirengi-$(date -u +%Y%m%dT%H%M%SZ)-$tag"
partial="$BACKUP_DIR/.$name.partial"
fifo="$BACKUP_DIR/.$name.fifo"
run="/tmp/nirengi-backup-$$"
holder=""
# shellcheck disable=SC2317,SC2329 # EXIT trap
finish() {
  local status=$?
  [[ -z $holder ]] || { compose exec -T postgres touch "$run.done" 2>/dev/null || true; wait "$holder" 2>/dev/null || true; }
  compose exec -T postgres rm -f "$run.snapshot" "$run.counts" "$run.ready" "$run.done" 2>/dev/null || true
  rm -f "$fifo" "$partial" "$BACKUP_DIR/.$name.counts"
  ((status == 0)) || hc_ping "${HC_BACKUP_URL:-}" fail
}
trap finish EXIT

# The Healthchecks "backup" check watches the daily timer: other backups (pre-deploy, manual) only report failures,
# so frequent deploys cannot hide a broken timer.
if [[ $tag == daily ]]; then hc_ping "${HC_BACKUP_URL:-}" start; fi
# Not while a restore replaces the database or a deploy migrates it.
take_lock 1800
install -d -m 0750 "$BACKUP_DIR" # keeps a setgid bit set for copy 2's reader (docs/isletim.md §2)
compose up -d --wait --no-recreate postgres >/dev/null 2>&1 || true # after a reboot the timer may beat compose

# A REPEATABLE READ session exports its snapshot and counts rows in it, then holds it open while pg_dump reads
# the same snapshot (pg_dump --snapshot).
compose exec -T postgres psql -U postgres -d nirengi -qAt -F ' ' -v ON_ERROR_STOP=1 <<SQL >/dev/null &
BEGIN ISOLATION LEVEL REPEATABLE READ;
\o $run.snapshot
SELECT pg_export_snapshot();
\o $run.counts
$COUNTS_SQL;
\o
\! touch $run.ready; while [ ! -e $run.done ]; do sleep 0.2; done
COMMIT;
SQL
holder=$!
for _ in $(seq 1 150); do
  compose exec -T postgres test -e "$run.ready" 2>/dev/null && break
  kill -0 "$holder" 2>/dev/null || die "could not open the snapshot session"
  sleep 0.2
done
snapshot=$(compose exec -T postgres cat "$run.snapshot")
[[ $snapshot =~ ^[0-9A-F-]+$ ]] || die "no exported snapshot"
compose exec -T postgres cat "$run.counts" >"$BACKUP_DIR/.$name.counts"

# The archive is listed while it is encrypted: an unreadable dump fails the run before anything is kept. The
# lister reads a copy through a FIFO and then drains it (pg_restore --list stops after the table of contents; a
# closed reader would kill tee with SIGPIPE on dumps larger than the pipe buffers).
mkfifo -m 0600 "$fifo"
compose exec -T postgres sh -c 'pg_restore --list >/dev/null && exec cat >/dev/null' <"$fifo" &
lister=$!
compose exec -T postgres pg_dump -U postgres -d nirengi -Fc --snapshot="$snapshot" | tee "$fifo" | age -R "$recipient" -o "$partial"
wait "$lister" || die "pg_restore --list could not read the dump"
[[ -s $partial ]] || die "empty backup"
compose exec -T postgres touch "$run.done"
wait "$holder" || die "the snapshot session failed"
holder=""

mv "$partial" "$BACKUP_DIR/$name.dump.age"
mv "$BACKUP_DIR/.$name.counts" "$BACKUP_DIR/$name.counts"
(cd "$BACKUP_DIR" && sha256sum "$name.dump.age" >"$name.dump.age.sha256")
chmod 0640 "$BACKUP_DIR/$name".*

# Retention (copy 1): 7 days of daily/manual; the 3 newest of each pre-* kind (deploy, restore: a run of deploys
# cannot push out the last safety copy taken before a restore); nothing older than 35 days.
prune() { for f in "$@"; do rm -f "$f" "${f%.dump.age}.counts" "$f.sha256"; done; }
mapfile -t old < <(find "$BACKUP_DIR" -maxdepth 1 -name 'nirengi-*.dump.age' ! -name '*-pre-*.dump.age' -mtime +6)
prune "${old[@]}"
for kind in pre-deploy pre-restore; do
  mapfile -t pre < <(find "$BACKUP_DIR" -maxdepth 1 -name "nirengi-*-$kind.dump.age" -printf '%f\n' | sort -r | tail -n +4)
  prune "${pre[@]/#/$BACKUP_DIR/}"
done
mapfile -t ancient < <(find "$BACKUP_DIR" -maxdepth 1 -name 'nirengi-*.dump.age' -mtime +34)
prune "${ancient[@]}"
find "$BACKUP_DIR" -maxdepth 1 -name '.nirengi-*' -mmin +60 -delete

log "backup $name.dump.age ($(du -h "$BACKUP_DIR/$name.dump.age" | cut -f1))"
if [[ $tag == daily ]]; then hc_ping "${HC_BACKUP_URL:-}"; fi
echo "$BACKUP_DIR/$name.dump.age"

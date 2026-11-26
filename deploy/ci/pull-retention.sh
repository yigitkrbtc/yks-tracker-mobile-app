#!/usr/bin/env bash
# backup-pull.sh (copy 2) without a server (F2 card 11): retention by arrival time, companions removed with their
# archive, a planted back-dated name cannot push real weekly backups out, a bad checksum is quarantined.
set -euo pipefail
repo=$(cd "$(dirname "$0")/../.." && pwd)
t=$(mktemp -d)
trap 'rm -rf "$t"' EXIT
mkdir -p "$t/src" "$t/dst"
now=$(date -u -d 2026-11-26T06:00:00Z +%s)
pull() { PULL_ENV_FILE=/dev/null PULL_SOURCE=$t/src PULL_DEST=$t/dst PULL_NOW=$now "$repo/deploy/bin/backup-pull.sh"; }
backup() { # backup <file name> <arrival: days before now>
  echo "$1" >"$t/dst/$1"
  (cd "$t/dst" && sha256sum "$1" >"$1.sha256")
  echo "counts" >"$t/dst/${1%.dump.age}.counts"
  touch -d "@$((now - $2 * 86400 - 3600))" "$t/dst/$1" "$t/dst/$1.sha256" "$t/dst/${1%.dump.age}.counts"
}
# What earlier pulls left here: one backup per day for 50 days.
for d in $(seq 0 50); do
  backup "nirengi-$(date -u -d "@$((now - d * 86400))" +%Y%m%d)T033000Z-daily.dump.age" "$d"
done
# Today's pull brings a planted file named into the past (newest-of-week by name, not by arrival).
echo planted >"$t/src/nirengi-20261020T235959Z-daily.dump.age"
(cd "$t/src" && sha256sum nirengi-20261020T235959Z-daily.dump.age >nirengi-20261020T235959Z-daily.dump.age.sha256)
pull

kept=$(find "$t/dst" -maxdepth 1 -name '*.dump.age' -printf '%f\n' | cut -c9-16 | sort | tr '\n' ' ')
expected="20261020 20261025 20261101 20261108 20261115 20261118 20261119 20261120 20261121 20261122 20261123 20261124 20261125 20261126 "
[[ $kept == "$expected" ]] || { echo "::error::kept: $kept; expected: $expected"; exit 1; }
orphans=$(find "$t/dst" -maxdepth 1 \( -name '*.sha256' -o -name '*.counts' \) | while read -r f; do
  base=${f%.sha256}; base=${base%.counts}; [[ -e $base || -e $base.dump.age ]] || echo "$f"; done)
[[ -z $orphans ]] || { echo "::error::companions left without their archive: $orphans"; exit 1; }

# A file that does not match its checksum is quarantined and the run fails (Healthchecks alarms).
echo tampered >"$t/src/nirengi-20261126T120000Z-manual.dump.age"
echo "0000  nirengi-20261126T120000Z-manual.dump.age" >"$t/src/nirengi-20261126T120000Z-manual.dump.age.sha256"
if pull 2>/dev/null; then
  echo "::error::a checksum mismatch did not fail the pull"
  exit 1
fi
[[ -e $t/dst/quarantine/nirengi-20261126T120000Z-manual.dump.age ]] || { echo "::error::mismatch not quarantined"; exit 1; }
echo "copy 2 retention ok: $kept"

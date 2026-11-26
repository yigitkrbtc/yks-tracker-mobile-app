#!/usr/bin/env bash
# backup-pull.sh against a local "VPS" directory holding 51 daily backups (F2 card 11).
set -euo pipefail
repo=$(cd "$(dirname "$0")/../.." && pwd)
t=$(mktemp -d)
trap 'rm -rf "$t"' EXIT
mkdir -p "$t/src" "$t/dst"
now=$(date -u -d 2026-11-26T06:00:00Z +%s)
for d in $(seq 0 50); do
  stamp=$(date -u -d "@$((now - d * 86400))" +%Y%m%dT033000Z)
  f=nirengi-$stamp-daily.dump.age
  echo "$d" > "$t/src/$f"
  (cd "$t/src" && sha256sum "$f" > "$f.sha256")
done
PULL_SOURCE=$t/src PULL_DEST=$t/dst PULL_NOW=$now "$repo/deploy/bin/backup-pull.sh"
kept=$(find "$t/dst" -name '*.dump.age' -printf '%f\n' | cut -c9-16 | sort | tr '\n' ' ')
expected="20261025 20261101 20261108 20261115 20261118 20261119 20261120 20261121 20261122 20261123 20261124 20261125 20261126 "
[[ $kept == "$expected" ]] || { echo "::error::kept: $kept; expected: $expected"; exit 1; }

# A corrupted pull fails the run.
echo tampered >> "$t/src/nirengi-20261126T033000Z-daily.dump.age"
rm "$t/dst/nirengi-20261126T033000Z-daily.dump.age"
if PULL_SOURCE=$t/src PULL_DEST=$t/dst PULL_NOW=$now "$repo/deploy/bin/backup-pull.sh" 2>/dev/null; then
  echo "::error::a checksum mismatch did not fail the pull"
  exit 1
fi
echo "retention ok: $kept"

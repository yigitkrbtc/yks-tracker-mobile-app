#!/usr/bin/env bash
# Disk usage alarm (ADR-0022 §5): the root file system or Docker's data root above $DISK_THRESHOLD % (default 80)
# pings Healthchecks /fail. Run every 15 minutes by nirengi-disk.timer.
set -euo pipefail
# shellcheck source=deploy/bin/lib.sh
. "$(dirname "$(readlink -f "$0")")/lib.sh"

threshold=${DISK_THRESHOLD:-80}
docker_root=$(docker info --format '{{.DockerRootDir}}' 2>/dev/null || echo /var/lib/docker)
over=""
for path in / "$docker_root"; do
  [[ -e $path ]] || continue
  used=$(df --output=pcent "$path" | tail -1 | tr -dc 0-9)
  if ((used > threshold)); then
    over+="$path=$used% "
  fi
done
if [[ -n $over ]]; then
  log "disk above $threshold%: $over"
  hc_ping "${HC_DISK_URL:-}" fail
  exit 1
fi
hc_ping "${HC_DISK_URL:-}"

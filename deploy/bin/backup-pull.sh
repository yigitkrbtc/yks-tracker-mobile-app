#!/usr/bin/env bash
# Runs on copy 2, a storage server at another Turkish provider (ADR-0022 §4). Pulls new backups from the VPS over
# an rrsync -ro key (the VPS cannot delete them), checks their SHA-256, and keeps 7 days + one per week up to 35
# days. Configuration in /etc/nirengi-pull.env (PULL_SOURCE, PULL_DEST, PULL_SSH_KEY, HC_PULL_URL).
# PULL_SOURCE is "user@vps:" (rrsync roots it at the backup directory) or a local directory (tests).
set -euo pipefail
env_file=${PULL_ENV_FILE:-/etc/nirengi-pull.env}
# shellcheck disable=SC1090
[[ -f $env_file ]] && . "$env_file"
source=${PULL_SOURCE:?PULL_SOURCE}
dest=${PULL_DEST:-/srv/nirengi-backups}
now=${PULL_NOW:-$(date -u +%s)}
ping() { [[ -z ${HC_PULL_URL:-} ]] || curl -fsS -m 10 --retry 3 -o /dev/null "$HC_PULL_URL${1:+/$1}" 2>/dev/null || true; }
problems=0

ping start
trap 'ping fail' ERR
install -d -m 0750 "$dest" "$dest/quarantine"
rsh=()
if [[ -n ${PULL_SSH_KEY:-} ]]; then
  # The VPS host key must already be in known_hosts (docs/isletim.md §2 step 11).
  rsh=(-e "ssh -i $(printf '%q' "$PULL_SSH_KEY") -o BatchMode=yes -o StrictHostKeyChecking=yes")
fi
# No -t: a pulled file's time is its arrival here. Retention below goes by arrival, so files a compromised VPS
# names into the past cannot push real backups out.
rsync -rl --ignore-existing --include='nirengi-*' --exclude='*' "${rsh[@]}" "${source%/}/" "$dest/"

# A file that fails its checksum is quarantined (kept for inspection, outside retention) and the run reports it.
for sum in "$dest"/nirengi-*.dump.age.sha256; do
  [[ -e $sum ]] || continue
  if ! (cd "$dest" && sha256sum -c --quiet "$(basename "$sum")" 2>/dev/null); then
    echo "checksum mismatch, quarantined: $(basename "${sum%.sha256}")" >&2
    mv -f "${sum%.sha256}" "$sum" "$dest/quarantine/" 2>/dev/null || true
    problems=1
  fi
done

# Retention by arrival time: everything from the last 7 days; older than that, the newest per ISO week up to
# 35 days; nothing beyond (deleted accounts leave the backups within ~35 days).
declare -A newest_of_week=()
while read -r mtime f; do
  age_days=$(((now - ${mtime%.*}) / 86400))
  week=$(date -u -d "@${mtime%.*}" +%G%V)
  keep=false
  if ((age_days <= 7)); then
    keep=true
  elif ((age_days <= 35)) && [[ -z ${newest_of_week[$week]:-} ]]; then
    keep=true
    newest_of_week[$week]=$f
  fi
  if [[ $keep == false ]]; then
    rm -f "$dest/$f" "$dest/$f.sha256" "$dest/${f%.dump.age}.counts"
  fi
done < <(find "$dest" -maxdepth 1 -name 'nirengi-*.dump.age' -printf '%T@ %f\n' | sort -rn)

trap - ERR
if ((problems)); then
  ping fail
  exit 1
fi
ping

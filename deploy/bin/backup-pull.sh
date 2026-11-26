#!/usr/bin/env bash
# Runs on copy 2, a storage server at another Turkish provider (ADR-0022 §4). Pulls new backups from the VPS over
# an rrsync -ro key (the VPS cannot delete them), checks their SHA-256, and keeps 7 days + one per week up to 35
# days. Configuration in /etc/nirengi-pull.env (PULL_SOURCE, PULL_DEST, PULL_SSH_KEY, HC_PULL_URL).
# PULL_SOURCE is "user@vps:" (rrsync roots it at the backup directory) or a local directory (tests).
set -euo pipefail
# shellcheck disable=SC1091
[[ -f /etc/nirengi-pull.env ]] && . /etc/nirengi-pull.env
source=${PULL_SOURCE:?PULL_SOURCE}
dest=${PULL_DEST:-/srv/nirengi-backups}
now=${PULL_NOW:-$(date -u +%s)}
ping() { [[ -z ${HC_PULL_URL:-} ]] || curl -fsS -m 10 --retry 3 -o /dev/null "$HC_PULL_URL${1:+/$1}" || true; }

ping start
trap 'ping fail' ERR
install -d -m 0750 "$dest"
ssh_opts=()
[[ -n ${PULL_SSH_KEY:-} ]] && ssh_opts=(-e "ssh -i $PULL_SSH_KEY -o BatchMode=yes -o StrictHostKeyChecking=yes")
rsync -a --ignore-existing --include='nirengi-*' --exclude='*' "${ssh_opts[@]}" "${source%/}/" "$dest/"

for sum in "$dest"/nirengi-*.dump.age.sha256; do
  [[ -e $sum ]] || continue
  (cd "$dest" && sha256sum -c --quiet "$(basename "$sum")") || { echo "checksum mismatch: $sum" >&2; false; }
done

# Retention by the timestamp in the name: everything from the last 7 days; older than that, the newest per ISO week
# up to 35 days; nothing beyond (deleted accounts leave the backups within ~35 days).
declare -A newest_of_week=()
mapfile -t files < <(find "$dest" -maxdepth 1 -name 'nirengi-*.dump.age' -printf '%f\n' | sort -r)
for f in "${files[@]}"; do
  stamp=${f#nirengi-}
  stamp=${stamp:0:8}
  age_days=$(((now - $(date -u -d "$stamp" +%s)) / 86400))
  week=$(date -u -d "$stamp" +%G%V)
  keep=false
  if ((age_days <= 7)); then
    keep=true
  elif ((age_days <= 35)) && [[ -z ${newest_of_week[$week]:-} ]]; then
    keep=true
  fi
  if ((age_days > 7)) && [[ -z ${newest_of_week[$week]:-} ]]; then
    newest_of_week[$week]=$f
  fi
  if [[ $keep == false ]]; then
    rm -f "$dest/$f" "$dest/$f.sha256" "$dest/${f%.dump.age}.counts"
  fi
done

trap - ERR
ping

#!/usr/bin/env bash
# Shared by the server scripts (ADR-0022 §4). Sourced, not run. Paths are overridable for CI (deploy/ci/smoke.sh).
NIRENGI_DIR=${NIRENGI_DIR:-/opt/nirengi}
BACKUP_DIR=${BACKUP_DIR:-/var/backups/nirengi}

# .env carries the Healthchecks.io ping URLs (no personal data in pings).
if [[ -f $NIRENGI_DIR/.env ]]; then
  # shellcheck disable=SC1091
  set -a && . "$NIRENGI_DIR/.env" && set +a
fi

compose() { docker compose --project-directory "$NIRENGI_DIR" -f "$NIRENGI_DIR/compose.prod.yml" "$@"; }

log() { printf '%s %s\n' "$(date -u +%FT%TZ)" "$*" >&2; }

die() {
  log "ERROR: $*"
  exit 1
}

# hc_ping <url-or-empty> [start|fail|<exit code>]: best effort, never fails the caller.
hc_ping() {
  local url=$1 suffix=${2:-}
  [[ -n $url ]] || return 0
  curl -fsS -m 10 --retry 3 -o /dev/null "$url${suffix:+/$suffix}" || log "healthcheck ping failed: $url${suffix:+/$suffix}"
}

# Waits until the API's /health/ready answers Healthy (max $1 seconds). Asked from the caddy container (the API
# image has no curl); Host: localhost because AllowedHosts admits only the public name and localhost.
wait_ready() {
  local limit=${1:-120} body=""
  for ((i = 0; i < limit; i += 2)); do
    body=$(compose exec -T caddy wget -qO- -T 3 --header 'Host: localhost' http://api:8080/health/ready 2>/dev/null || true)
    [[ $body == Healthy ]] && return 0
    sleep 2
  done
  log "/health/ready answered '${body:-nothing}'"
  return 1
}

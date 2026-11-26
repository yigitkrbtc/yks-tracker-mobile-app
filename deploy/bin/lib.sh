#!/usr/bin/env bash
# Shared by the server scripts (ADR-0022 §4). Sourced, not run. Paths are overridable for CI (deploy/ci/smoke.sh).
NIRENGI_DIR=${NIRENGI_DIR:-/opt/nirengi}
BACKUP_DIR=${BACKUP_DIR:-/var/backups/nirengi}

# Only the Healthchecks.io ping URLs are read from .env (compose syntax, not shell: values may contain spaces).
if [[ -f $NIRENGI_DIR/.env ]]; then
  while IFS='=' read -r key value; do
    [[ $key =~ ^HC_[A-Z_]+$ ]] && export "$key=$value"
  done <"$NIRENGI_DIR/.env"
fi

compose() { docker compose --project-directory "$NIRENGI_DIR" -f "$NIRENGI_DIR/compose.prod.yml" "$@"; }

log() { printf '%s %s\n' "$(date -u +%FT%TZ)" "$*" >&2; }

die() {
  log "ERROR: $*"
  exit 1
}

# hc_ping <url-or-empty> [start|fail]: best effort, never fails the caller. The URL is a credential: not logged.
hc_ping() {
  local url=$1 suffix=${2:-}
  [[ -n $url ]] || return 0
  curl -fsS -m 10 --retry 3 -o /dev/null "$url${suffix:+/$suffix}" 2>/dev/null || log "healthcheck ping failed${suffix:+ ($suffix)}"
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

# Core row counts (names and numbers only). Reads one transaction's snapshot when given its psql session.
# shellcheck disable=SC2034 # used by the scripts that source this file
COUNTS_SQL="SELECT 'identity.users', count(*) FROM identity.users
  UNION ALL SELECT 'curriculum.topics', count(*) FROM curriculum.topics
  UNION ALL SELECT 'tracking.topic_tests', count(*) FROM tracking.topic_tests
  UNION ALL SELECT 'tracking.mock_exams', count(*) FROM tracking.mock_exams"

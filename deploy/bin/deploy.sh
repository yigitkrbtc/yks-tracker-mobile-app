#!/usr/bin/env bash
# Deploys API version X.Y.Z (ADR-0022 §2), by hand (sudo deploy.sh X.Y.Z) or from CI through ci-entry.
#   1. image pulled, its digest recorded   2. pre-deploy backup   3. migrate with the new image
#   4. .env NIRENGI_VERSION switched, api recreated, the new image checked to be the one running
#   5. /health/ready Healthy (or Degraded: started, an outbox alarm is open) within $DEPLOY_READY_TIMEOUT s (120)
#   6. otherwise the previous version is started again (migrations stay: expand/contract), exit non-zero
#   7. a line in deploy-history: time version digest result (running before)
# Re-running the current version recreates the API (picks up changed secrets). One deploy or restore at a time.
# Runs to the end when the SSH session or the CI job goes away (HUP/PIPE ignored; output also in deploy.log).
set -euo pipefail
here=$(dirname "$(readlink -f "$0")")
# shellcheck source=deploy/bin/lib.sh
. "$here/lib.sh"

version=${1:?usage: deploy.sh X.Y.Z}
[[ $version =~ $VERSION_PATTERN ]] || die "version must look like 1.2.3, got '$version'"
env_file=$NIRENGI_DIR/.env
history=$NIRENGI_DIR/deploy-history
timeout=${DEPLOY_READY_TIMEOUT:-120}

trap '' HUP PIPE
exec > >(tee -a "$NIRENGI_DIR/deploy.log" 2>/dev/null || true) 2>&1
take_lock 0

current=$(sed -n 's/^NIRENGI_VERSION=//p' "$env_file" | tail -1)
[[ $current =~ $VERSION_PATTERN ]] || current=""
owner=$(sed -n 's/^NIRENGI_IMAGE_OWNER=//p' "$env_file" | tail -1)
[[ -n $owner ]] || die "NIRENGI_IMAGE_OWNER missing in $env_file"
image=ghcr.io/$owner/nirengi-api:$version
digest="?"

record() { printf '%s %s %s %s (was %s)\n' "$(date -u +%FT%TZ)" "$version" "$digest" "$1" "${current:-none}" >>"$history"; }
set_version() {
  sed -i "s/^NIRENGI_VERSION=.*/NIRENGI_VERSION=$1/" "$env_file"
  grep -qx "NIRENGI_VERSION=$1" "$env_file" || die "could not write NIRENGI_VERSION=$1 to $env_file (expected a plain NIRENGI_VERSION= line)"
}
running_image() { docker inspect -f '{{.Config.Image}}' "$(compose ps -q api 2>/dev/null)" 2>/dev/null || true; }
# start <version>: .env, recreate, and the container must really run that version.
start() {
  set_version "$1"
  compose up -d --no-deps --force-recreate api || return 1
  [[ $(running_image) == "ghcr.io/$owner/nirengi-api:$1" ]] || { log "api does not run $1"; return 1; }
}

log "deploying $version (running: ${current:-none})"
# The readiness check asks through Caddy: without it a healthy version would look broken and be rolled back.
compose exec -T caddy true 2>/dev/null || die "caddy is not running; start it first: docker compose up -d caddy"

if ! docker pull -q "$image" >/dev/null; then
  # CI only: images built on the runner. The marker is a root-owned file, never on a server.
  if [[ ! -e $NIRENGI_DIR/.allow-local-images ]] || ! docker image inspect "$image" >/dev/null 2>&1; then
    record pull-failed
    die "cannot pull $image"
  fi
fi
digest=$(docker image inspect --format '{{if .RepoDigests}}{{index .RepoDigests 0}}{{else}}{{.Id}}{{end}}' "$image")

"$here/backup.sh" --tag pre-deploy >/dev/null || { record backup-failed; die "pre-deploy backup failed; nothing changed"; }

if ! NIRENGI_VERSION=$version compose run --rm migrate; then
  record migrate-failed
  die "migrate failed; the running version (${current:-none}) is untouched (migrations are expand-only: any that did apply are safe)"
fi

if start "$version" && wait_ready "$timeout"; then
  if [[ $READY_STATE == Healthy ]]; then record ok; else record ok-degraded; fi
  log "deployed $version ($READY_STATE)"
  exit 0
fi

log "$version did not start; going back to ${current:-nothing}"
if [[ -n $current && $current != "$version" ]]; then
  if start "$current" && wait_ready "${DEPLOY_ROLLBACK_TIMEOUT:-180}"; then
    record rolled-back
  else
    record rollback-failed
    log "the previous version is not running either: see docker compose logs api"
  fi
else
  record failed
  log "no other version to go back to"
fi
exit 1

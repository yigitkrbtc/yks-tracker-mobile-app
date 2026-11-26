#!/usr/bin/env bash
# Deploys API version X.Y.Z (ADR-0022 §2), by hand (sudo deploy.sh X.Y.Z) or from CI through ci-entry.
#   1. image pulled, its digest recorded   2. pre-deploy backup   3. migrate with the new image
#   4. .env NIRENGI_VERSION switched, api recreated   5. /health/ready = Healthy within $DEPLOY_READY_TIMEOUT s (120)
#   6. otherwise the previous version is started again (migrations stay: expand/contract) and the exit is non-zero
#   7. a line in deploy-history: time version digest result
# Re-running the current version restarts the API (picks up changed secrets).
set -euo pipefail
here=$(dirname "$(readlink -f "$0")")
# shellcheck source=deploy/bin/lib.sh
. "$here/lib.sh"

version=${1:?usage: deploy.sh X.Y.Z}
[[ $version =~ ^[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,4}$ ]] || die "version must look like 1.2.3, got '$version'"
env_file=$NIRENGI_DIR/.env
history=$NIRENGI_DIR/deploy-history
timeout=${DEPLOY_READY_TIMEOUT:-120}

# One deploy (or restore) at a time.
exec 9>"${DEPLOY_LOCK:-/run/lock/nirengi-deploy}"
flock -n 9 || die "another deploy is running"

current=$(sed -n 's/^NIRENGI_VERSION=//p' "$env_file" | tail -1)
owner=$(sed -n 's/^NIRENGI_IMAGE_OWNER=//p' "$env_file" | tail -1)
[[ -n $owner ]] || die "NIRENGI_IMAGE_OWNER missing in $env_file"
image=ghcr.io/$owner/nirengi-api:$version

record() { printf '%s %s %s %s\n' "$(date -u +%FT%TZ)" "$version" "${digest:-?}" "$1" >>"$history"; }
set_version() { sed -i "s/^NIRENGI_VERSION=.*/NIRENGI_VERSION=$1/" "$env_file"; }

log "deploying $version (running: ${current:-none})"
if ! docker pull -q "$image" >/dev/null; then
  # CI tests use images that exist only locally.
  if [[ ${NIRENGI_ALLOW_LOCAL_IMAGE:-} != 1 ]] || ! docker image inspect "$image" >/dev/null 2>&1; then
    record pull-failed
    die "cannot pull $image"
  fi
fi
digest=$(docker image inspect --format '{{if .RepoDigests}}{{index .RepoDigests 0}}{{else}}{{.Id}}{{end}}' "$image")

"$here/backup.sh" --tag pre-deploy >/dev/null || { record backup-failed; die "pre-deploy backup failed; nothing changed"; }

if ! NIRENGI_VERSION=$version compose run --rm migrate; then
  record migrate-failed
  die "migrate failed; the running version ($current) is untouched"
fi

set_version "$version"
compose up -d --no-deps --force-recreate api
if wait_ready "$timeout"; then
  record ok
  log "deployed $version"
  exit 0
fi

log "$version did not become Healthy; going back to $current"
if [[ -n $current && $current != "$version" ]]; then
  set_version "$current"
  compose up -d --no-deps --force-recreate api
  if wait_ready "$timeout"; then
    record rolled-back
  else
    record rollback-unhealthy
    log "the previous version is not Healthy either: see docker compose logs api"
  fi
else
  record failed
fi
exit 1

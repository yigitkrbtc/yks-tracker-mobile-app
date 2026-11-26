#!/usr/bin/env bash
# Static rules for deploy/compose.prod.yml (ADR-0022 §1, F2 card 11). Needs docker compose and jq.
# Usage: deploy/ci/check-compose.sh <directory holding compose.prod.yml and .env>
set -euo pipefail
dir=${1:?directory}
config=$(docker compose --project-directory "$dir" -f "$dir/compose.prod.yml" --profile tools config --format json)
fail=0

# Docker publishes ports past ufw: only Caddy may have any.
offenders=$(jq -r '.services | to_entries[] | select(.key != "caddy" and ((.value.ports // []) | length) > 0) | .key' <<<"$config")
if [[ -n $offenders ]]; then
  echo "::error::Only caddy may publish ports; found ports on: $offenders"
  fail=1
fi

# The database network has no route out; postgres sits on it alone with the API and migrate.
if [[ $(jq -r '.networks.db.internal // false' <<<"$config") != true ]]; then
  echo "::error::The db network must be internal"
  fail=1
fi
if [[ $(jq -r '.services.postgres.networks | keys | join(",")' <<<"$config") != db ]]; then
  echo "::error::postgres must be on the db network only"
  fail=1
fi

# Every image is pinned to a version (no latest, no bare name).
unpinned=$(jq -r '.services[] | .image | select(test(":[^/]+$") | not) // empty' <<<"$config")
latest=$(jq -r '.services[] | .image | select(endswith(":latest")) // empty' <<<"$config")
if [[ -n $unpinned$latest ]]; then
  echo "::error::Images must be pinned: $unpinned $latest"
  fail=1
fi

# Secrets come from files, never from the environment.
leaky=$(jq -r '.services[] | (.environment // {}) | keys[] | select(test("Password|SigningKey|HashKey|Certificate|ConnectionStrings"; "i"))' <<<"$config")
if [[ -n $leaky ]]; then
  echo "::error::Secret-looking settings in environment (use secrets/): $leaky"
  fail=1
fi

exit $fail

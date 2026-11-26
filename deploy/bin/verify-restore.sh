#!/usr/bin/env bash
# Restore drill (ADR-0022 §4, docs/isletim.md §6): on the administrator's machine, never on the server. Restores a
# backup into a throwaway PostgreSQL 17 next to the given API image and checks that
#   - the backup is at the image's schema version (migrate finds nothing to apply),
#   - core row counts equal the counts recorded at dump time and the curriculum is not empty,
#   - the API opens against it and /health/ready answers Healthy.
# On success it pings $HC_DRILL_URL and appends a line to $DRILL_HISTORY (default ~/nirengi-drill-history).
# The image must be the version production ran when the backup was taken (deploy-history): an older image would also
# find nothing to migrate. The restored data stays in memory (tmpfs) and is gone when the drill ends.
# Usage: verify-restore.sh <file.dump.age> --identity <age private key> --image ghcr.io/<owner>/nirengi-api:<X.Y.Z>
set -euo pipefail
file=${1:?usage: verify-restore.sh <file.dump.age> --identity <key> --image <image>}
shift
identity="" image=""
while (($#)); do
  case $1 in
    --identity) identity=$2; shift 2 ;;
    --image) image=$2; shift 2 ;;
    *) echo "unknown option $1" >&2; exit 2 ;;
  esac
done
[[ -f $file && -f $identity && -n $image ]] || { echo "need a backup file, --identity and --image" >&2; exit 2; }
here=$(dirname "$(readlink -f "$0")")
# shellcheck source=deploy/bin/lib.sh
. "$here/lib.sh"
sql=$(readlink -f "${ROLES_SQL:-$here/../sql/00-roles.sql}")
[[ -f $sql ]] || sql=/opt/nirengi/sql/00-roles.sql
counts=${file%.dump.age}.counts
step() { printf '== %s\n' "$*"; }
fail() { echo "DRILL FAILED: $*" >&2; exit 1; }

if [[ -f $file.sha256 ]]; then
  (cd "$(dirname "$file")" && sha256sum -c --quiet "$(basename "$file").sha256") || fail "checksum mismatch"
fi

run=nirengi-drill-$$
work=$(mktemp -d)
# shellcheck disable=SC2317,SC2329 # called by the EXIT trap
cleanup() {
  docker rm -f -v "$run-db" "$run-api" >/dev/null 2>&1 || true # the restored data lives in tmpfs: nothing stays behind
  docker network rm "$run" >/dev/null 2>&1 || true
  rm -rf "$work"
}
trap cleanup EXIT

rand() { openssl rand -hex 32; }
owner_pw=$(rand) app_pw=$(rand)
docker network create "$run" >/dev/null
docker run -d --name "$run-db" --network "$run" --network-alias postgres \
  --tmpfs /var/lib/postgresql/data:rw,size=4g \
  -e POSTGRES_DB=nirengi -e POSTGRES_PASSWORD="$(rand)" -v "$sql:/sql/00-roles.sql:ro" postgres:17.11-alpine >/dev/null
for _ in $(seq 1 60); do
  docker exec "$run-db" pg_isready -h 127.0.0.1 -U postgres -d nirengi >/dev/null 2>&1 && break
  sleep 1
done
psql() { docker exec -i "$run-db" psql -U postgres -d nirengi -v ON_ERROR_STOP=1 "$@"; }

step "roles"
docker exec -i -e NIRENGI_OWNER_PASSWORD="$owner_pw" -e NIRENGI_APP_PASSWORD="$app_pw" "$run-db" \
  psql -q -U postgres -d nirengi -f /sql/00-roles.sql
step "restore $(basename "$file")"
age -d -i "$identity" "$file" \
  | docker exec -i "$run-db" pg_restore -U postgres -d nirengi --no-owner --role=nirengi_owner --single-transaction

history() {
  local schema
  for schema in $(psql -tA -c "SELECT table_schema FROM information_schema.tables WHERE table_name = '__ef_migrations_history' ORDER BY 1"); do
    printf '%s=%s ' "$schema" "$(psql -tA -c "SELECT count(*) FROM \"$schema\".__ef_migrations_history")"
  done
}
before=$(history)
step "migrate with $image"
docker run --rm --network "$run" -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Database="Host=postgres;Database=nirengi;Username=nirengi_owner;Password=$owner_pw" "$image" --migrate >/dev/null
after=$(history)
[[ $before == "$after" ]] || fail "the backup is not at the image's schema version (migrations before: $before; after: $after)"
echo "migration history per schema: $after"

step "row counts"
actual=$(psql -tA -F ' ' -c "$COUNTS_SQL")
echo "$actual"
if [[ -f $counts ]]; then
  [[ $actual == "$(cat "$counts")" ]] || fail "row counts differ from the ones recorded at dump time ($counts)"
else
  echo "no $counts next to the backup; counts not compared"
fi
[[ $(awk '$1 == "curriculum.topics" {print $2}' <<<"$actual") -gt 0 ]] || fail "curriculum is empty"

step "API against the restored database"
dp_pw=$(rand)
openssl req -x509 -newkey rsa:3072 -sha256 -days 2 -nodes -subj /CN=nirengi-drill -keyout "$work/dp.key" -out "$work/dp.crt" 2>/dev/null
dp_cert=$(openssl pkcs12 -export -inkey "$work/dp.key" -in "$work/dp.crt" -passout pass:"$dp_pw" | base64 | tr -d '\n')
docker run -d --name "$run-api" --network "$run" -p 127.0.0.1::8080 \
  -e ASPNETCORE_ENVIRONMENT=Production -e AllowedHosts=localhost -e ForwardedHeaders__KnownProxies__0=127.0.0.1 \
  -e ConnectionStrings__Database="Host=postgres;Database=nirengi;Username=nirengi_app;Password=$app_pw" \
  -e Jwt__SigningKey="$(openssl rand -base64 64 | tr -d '\n')" -e Identity__Security__HashKey="$(openssl rand -base64 64 | tr -d '\n')" \
  -e DataProtection__Certificate="$dp_cert" -e DataProtection__CertificatePassword="$dp_pw" \
  -e Smtp__Host=smtp.invalid -e Smtp__FromAddress=drill@example.invalid "$image" >/dev/null
port=$(docker port "$run-api" 8080/tcp | head -1 | sed 's/.*://')
body=""
for _ in $(seq 1 60); do
  body=$(curl -s -m 3 "http://localhost:$port/health/ready" || true)
  [[ $body == Healthy ]] && break
  sleep 2
done
[[ $body == Healthy ]] || { docker logs --tail 50 "$run-api" >&2; fail "/health/ready answered '${body:-nothing}'"; }
echo "health/ready: Healthy"

printf '%s drill %s %s ok\n' "$(date -u +%FT%TZ)" "$(basename "$file")" "$image" >> "${DRILL_HISTORY:-$HOME/nirengi-drill-history}"
[[ -z ${HC_DRILL_URL:-} ]] || curl -fsS -m 10 --retry 3 -o /dev/null "$HC_DRILL_URL" || echo "drill ping failed" >&2
echo "DRILL OK"

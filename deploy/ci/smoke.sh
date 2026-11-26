#!/usr/bin/env bash
# Brings the production compose up with throwaway secrets and checks the whole path (F2 card 11):
# roles SQL → migrate as nirengi_owner → API as nirengi_app (seed is DML only) → Caddy TLS → /health/ready = Healthy.
# Usage: deploy/ci/smoke.sh <image owner> <version>   (image ghcr.io/<owner>/nirengi-api:<version> must exist locally)
set -euo pipefail
owner=${1:?owner} version=${2:?version}
work=$(mktemp -d)
# shellcheck disable=SC2317,SC2329 # called by the EXIT trap
cleanup() {
  cd "$work" || return
  docker compose -f compose.prod.yml --profile tools logs --no-color 2>&1 | tail -200 || true
  docker compose -f compose.prod.yml --profile tools down -v >/dev/null 2>&1 || true
}
trap cleanup EXIT

repo=$(cd "$(dirname "$0")/../.." && pwd)
cp "$repo/deploy/compose.prod.yml" "$repo/deploy/Caddyfile" "$work/"
cp -r "$repo/deploy/sql" "$work/sql"
cd "$work"

cat > .env <<ENV
NIRENGI_VERSION=$version
NIRENGI_IMAGE_OWNER=$owner
NIRENGI_API_HOST=localhost
ACME_EMAIL=ci@example.com
NIRENGI_ADMIN_EMAIL=admin@example.com
ENV

mkdir -p secrets
rand() { openssl rand -hex 32; }
owner_pw=$(rand) app_pw=$(rand)
printf '%s' "$owner_pw" > secrets/owner_password
printf '%s' "$app_pw" > secrets/app_password
printf '%s' "$(rand)" > secrets/postgres_password
printf 'Host=postgres;Port=5432;Database=nirengi;Username=nirengi_owner;Password=%s' "$owner_pw" > secrets/owner_db_connection
printf 'Host=postgres;Port=5432;Database=nirengi;Username=nirengi_app;Password=%s' "$app_pw" > secrets/app_db_connection
openssl rand -base64 64 | tr -d '\n' > secrets/Jwt__SigningKey
openssl rand -base64 64 | tr -d '\n' > secrets/Identity__Security__HashKey
dp_pw=$(rand)
printf '%s' "$dp_pw" > secrets/DataProtection__CertificatePassword
openssl req -x509 -newkey rsa:3072 -sha256 -days 30 -nodes -subj /CN=nirengi-ci -keyout dp.key -out dp.crt 2>/dev/null
openssl pkcs12 -export -inkey dp.key -in dp.crt -passout pass:"$dp_pw" | base64 -w0 > secrets/DataProtection__Certificate
rm dp.key dp.crt
: > secrets/Smtp__Password
# Same ownership as on the server (docs/isletim.md §7): api/migrate read as APP_UID 1654, postgres as 70.
sudo chown 1654 secrets/app_db_connection secrets/owner_db_connection secrets/Jwt__SigningKey \
  secrets/Identity__Security__HashKey secrets/DataProtection__Certificate secrets/DataProtection__CertificatePassword secrets/Smtp__Password
sudo chown 70 secrets/postgres_password
sudo chmod 0400 secrets/*

"$repo/deploy/ci/check-compose.sh" "$work"

docker compose -f compose.prod.yml up -d --wait postgres
docker compose -f compose.prod.yml exec -T postgres psql -U postgres -d nirengi \
  -v owner_password="$owner_pw" -v app_password="$app_pw" -f /sql/00-roles.sql
# Idempotent: a second run changes nothing and does not fail.
docker compose -f compose.prod.yml exec -T postgres psql -U postgres -d nirengi \
  -v owner_password="$owner_pw" -v app_password="$app_pw" -f /sql/00-roles.sql

docker compose -f compose.prod.yml run --rm migrate
docker compose -f compose.prod.yml up -d caddy api

for _ in $(seq 1 60); do
  body=$(curl -sk --max-time 3 https://localhost/health/ready || true)
  if [[ $body == Healthy ]]; then
    echo "health/ready: Healthy"
    # HTTP redirects to HTTPS; a host other than the configured one is not served.
    [[ $(curl -s -o /dev/null -w '%{http_code}' http://localhost/health/ready) == 308 ]]
    # The API refuses DDL: nirengi_app cannot create a table.
    if docker compose -f compose.prod.yml exec -T -e PGPASSWORD="$app_pw" postgres psql -U nirengi_app -h localhost -d nirengi \
      -c 'CREATE TABLE platform.ci_probe (x int)' 2>/dev/null; then
      echo "::error::nirengi_app was able to run DDL"
      exit 1
    fi
    exit 0
  fi
  sleep 2
done
echo "::error::/health/ready did not answer Healthy (last: ${body:-none})"
exit 1

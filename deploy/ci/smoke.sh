#!/usr/bin/env bash
# Brings the production compose up with throwaway secrets and checks the whole path (F2 card 11):
# roles SQL → migrate as nirengi_owner → API as nirengi_app (seed is DML only) → Caddy TLS → /health/ready = Healthy,
# the image's own HEALTHCHECK, and the app role's privileges.
# Usage: deploy/ci/smoke.sh <image owner> <version>   (image ghcr.io/<owner>/nirengi-api:<version> must exist locally)
set -euo pipefail
owner=${1:?owner} version=${2:?version}
host=api.nirengi.test
work=$(mktemp -d)
compose() { docker compose -f compose.prod.yml "$@"; }
fail() { echo "::error::$*"; exit 1; }
# shellcheck disable=SC2317,SC2329 # called by the EXIT trap
cleanup() {
  cd "$work" || return
  compose --profile tools ps -a || true
  compose --profile tools logs --no-color 2>&1 | tail -200 || true
  compose --profile tools down -v >/dev/null 2>&1 || true
}
trap cleanup EXIT

repo=$(cd "$(dirname "$0")/../.." && pwd)
cp "$repo/deploy/compose.prod.yml" "$work/"
cp -r "$repo/deploy/sql" "$work/sql"
# A made-up host cannot get an ACME certificate: the smoke uses Caddy's internal CA, nothing else differs.
sed "s|^{\$NIRENGI_API_HOST} {|&\n\ttls internal|" "$repo/deploy/Caddyfile" > "$work/Caddyfile"
grep -q 'tls internal' "$work/Caddyfile" || fail "could not add tls internal to the Caddyfile copy"
cd "$work"

cat > .env <<ENV
NIRENGI_VERSION=$version
NIRENGI_IMAGE_OWNER=$owner
NIRENGI_API_HOST=$host
ACME_EMAIL=ci@example.com
NIRENGI_ADMIN_EMAIL=admin@example.com
SMTP_HOST=smtp.invalid
SMTP_FROM_ADDRESS=no-reply@example.com
ENV

# Same layout and ownership as on the server (docs/isletim.md §7).
mkdir -p secrets/api secrets/migrate secrets/postgres
rand() { openssl rand -hex 32; }
owner_pw=$(rand) app_pw=$(rand) dp_pw=$(rand)
printf '%s' "$(rand)" > secrets/postgres/postgres_password
printf 'Host=postgres;Port=5432;Database=nirengi;Username=nirengi_owner;Password=%s' "$owner_pw" > secrets/migrate/ConnectionStrings__Database
printf 'Host=postgres;Port=5432;Database=nirengi;Username=nirengi_app;Password=%s' "$app_pw" > secrets/api/ConnectionStrings__Database
openssl rand -base64 64 | tr -d '\n' > secrets/api/Jwt__SigningKey
openssl rand -base64 64 | tr -d '\n' > secrets/api/Identity__Security__HashKey
printf '%s' "$dp_pw" > secrets/api/DataProtection__CertificatePassword
openssl req -x509 -newkey rsa:3072 -sha256 -days 30 -nodes -subj /CN=nirengi-ci -keyout dp.key -out dp.crt 2>/dev/null
openssl pkcs12 -export -inkey dp.key -in dp.crt -passout pass:"$dp_pw" | base64 -w0 > secrets/api/DataProtection__Certificate
rm dp.key dp.crt
: > secrets/api/Smtp__Password
sudo chmod 0400 secrets/api/* secrets/migrate/* secrets/postgres/*
sudo chown -R 1654 secrets/api secrets/migrate
sudo chown -R 70 secrets/postgres
sudo chmod 0500 secrets/api secrets/migrate secrets/postgres

"$repo/deploy/ci/check-compose.sh" "$work"

compose up -d --wait postgres
roles() {
  NIRENGI_OWNER_PASSWORD=$owner_pw NIRENGI_APP_PASSWORD=$app_pw \
    compose exec -T -e NIRENGI_OWNER_PASSWORD -e NIRENGI_APP_PASSWORD postgres psql -U postgres -d nirengi -f /sql/00-roles.sql
}
roles
roles # idempotent: a second run changes nothing and does not fail
if compose exec -T postgres psql -U postgres -d nirengi -f /sql/00-roles.sql >/dev/null 2>&1; then
  fail "00-roles.sql ran without passwords"
fi

compose run --rm migrate
compose up -d caddy api

body=""
for _ in $(seq 1 90); do
  [[ $(docker inspect -f '{{.State.Status}}' "$(compose ps -q api)") == running ]] || fail "api container stopped"
  body=$(curl -sk --max-time 3 --resolve "$host:443:127.0.0.1" "https://$host/health/ready" || true)
  [[ $body == Healthy ]] && break
  sleep 2
done
[[ $body == Healthy ]] || fail "/health/ready through Caddy did not answer Healthy (last: ${body:-none})"
echo "health/ready through Caddy: Healthy"

code=$(curl -s -o /dev/null -w '%{http_code}' --resolve "$host:80:127.0.0.1" "http://$host/health/ready")
[[ $code == 308 ]] || fail "HTTP should redirect to HTTPS with 308, got $code"

# The image's HEALTHCHECK calls localhost:8080 inside the container (AllowedHosts must admit it).
health=""
for _ in $(seq 1 30); do
  health=$(docker inspect -f '{{.State.Health.Status}}' "$(compose ps -q api)")
  [[ $health == healthy ]] && break
  sleep 2
done
[[ $health == healthy ]] || fail "api container HEALTHCHECK is $health"

# Signed in as nirengi_app, it may write rows but not create anything.
privileges=$(compose exec -T -e PGPASSWORD="$app_pw" postgres psql -h 127.0.0.1 -U nirengi_app -d nirengi -tA -c \
  "SELECT current_user, has_database_privilege('nirengi','CREATE'), has_schema_privilege('platform','CREATE'), has_table_privilege('platform.sync_epoch','UPDATE')")
[[ $privileges == "nirengi_app|f|f|t" ]] || fail "nirengi_app privileges are '$privileges', expected nirengi_app|f|f|t"
echo "nirengi_app: DML only"

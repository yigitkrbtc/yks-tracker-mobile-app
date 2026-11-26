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

# --- Card 8 scripts against the running stack (as on the server, with paths moved into $work). ---
export NIRENGI_DIR=$work BACKUP_DIR=$work/backups DISK_THRESHOLD=99
age-keygen -o "$work/age.key" 2>/dev/null
age-keygen -y "$work/age.key" > "$work/backup.pub"

# A few MB in an owner schema: the dump outgrows pipe buffers (the lister must drain its input).
compose exec -T postgres psql -U postgres -d nirengi -qc \
  "SET ROLE nirengi_owner; CREATE TABLE platform.ci_ballast AS SELECT md5(random()::text) AS x FROM generate_series(1, 300000)"
backup=$("$repo/deploy/bin/backup.sh" --tag manual)
[[ -s $backup && -s $backup.sha256 && -s ${backup%.dump.age}.counts ]] || fail "backup.sh left no archive, checksum or counts"
(cd "$BACKUP_DIR" && sha256sum -c --quiet "$(basename "$backup").sha256") || fail "backup checksum"
age -d -i "$work/age.key" -o "$work/plain.dump" "$backup" || fail "backup does not decrypt with the key"
[[ $(head -c 5 "$work/plain.dump") == PGDMP ]] || fail "backup is not a pg_dump custom archive"
rm -f "$work/plain.dump"
echo "backup: age-encrypted pg_dump archive"
grep -q '^curriculum.topics [1-9]' "${backup%.dump.age}.counts" || fail "counts file lacks the curriculum"

# After the backup: an object the backup does not have (a newer migration) and lost rows.
compose exec -T postgres psql -U postgres -d nirengi -qc \
  "SET ROLE nirengi_owner; CREATE TABLE platform.ci_newer_than_backup (x int); DELETE FROM platform.ci_ballast WHERE x < '8'"
epoch_before=$(compose exec -T postgres psql -U postgres -d nirengi -tA -c 'SELECT epoch FROM platform.sync_epoch')
RESTORE_KEY=$work/age.key "$repo/deploy/bin/restore.sh" "$backup" --yes
epoch_after=$(compose exec -T postgres psql -U postgres -d nirengi -tA -c 'SELECT epoch FROM platform.sync_epoch')
[[ -n $epoch_after && $epoch_after != "$epoch_before" ]] || fail "restore.sh did not renew the sync epoch"
state=$(compose exec -T postgres psql -U postgres -d nirengi -tA -c \
  "SELECT to_regclass('platform.ci_newer_than_backup') IS NULL, (SELECT count(*) FROM platform.ci_ballast)")
[[ $state == "t|300000" ]] || fail "restore did not bring back the backup's state ($state; want: newer table gone, 300000 rows)"
ls "$BACKUP_DIR"/*-pre-restore.dump.age >/dev/null || fail "restore.sh took no safety backup"
privileges=$(compose exec -T postgres psql -h 127.0.0.1 -U nirengi_app -d nirengi -tA -c \
  "SELECT has_schema_privilege('platform','CREATE'), has_table_privilege('platform.sync_epoch','UPDATE')")
[[ $privileges == "f|t" ]] || fail "after restore nirengi_app privileges are '$privileges'"
body=$(curl -sk --max-time 5 --resolve "$host:443:127.0.0.1" "https://$host/health/ready" || true)
[[ $body == Healthy ]] || fail "not Healthy after restore ($body)"
echo "restore: backup state back, newer objects gone, new sync epoch, safety backup, DML-only app role, Healthy"

"$repo/deploy/bin/verify-restore.sh" "$backup" --identity "$work/age.key" --image "ghcr.io/$owner/nirengi-api:$version"
"$repo/deploy/bin/diskcheck.sh"
if DISK_THRESHOLD=0 "$repo/deploy/bin/diskcheck.sh" 2>/dev/null; then fail "diskcheck.sh did not alarm above the threshold"; fi
echo "diskcheck: ok, alarms above the threshold"

# --- Card 9: deploy.sh and ci-entry against the running stack. ---
export NIRENGI_ALLOW_LOCAL_IMAGE=1 DEPLOY_LOCK=$work/deploy.lock DEPLOY_READY_TIMEOUT=90
next=$(awk -F. '{print $1"."$2"."$3+1}' <<<"$version")
docker tag "ghcr.io/$owner/nirengi-api:$version" "ghcr.io/$owner/nirengi-api:$next"
"$repo/deploy/bin/deploy.sh" "$next" || fail "deploy.sh $next failed"
grep -qx "NIRENGI_VERSION=$next" .env || fail ".env does not name $next after the deploy"
[[ $(docker inspect -f '{{.Config.Image}}' "$(compose ps -q api)") == "ghcr.io/$owner/nirengi-api:$next" ]] || fail "api does not run $next"
[[ $(tail -1 deploy-history) =~ \ $next\ .+\ ok$ ]] || fail "deploy-history: $(tail -1 deploy-history)"
ls "$BACKUP_DIR"/*-pre-deploy.dump.age >/dev/null || fail "no pre-deploy backup"
echo "deploy: $next running, pre-deploy backup, history ok"

# A version that migrates but never becomes Healthy: deploy.sh must fail and go back to the running one.
broken=$(awk -F. '{print $1"."$2"."$3+1}' <<<"$next")
# shellcheck disable=SC2016 # $1 belongs to the image's shell, not this one
printf 'FROM ghcr.io/%s/nirengi-api:%s\nENTRYPOINT ["sh", "-c", "if [ \\"$1\\" = --migrate ]; then exec dotnet Nirengi.Host.dll --migrate; fi; sleep 3600", "--"]\n' \
  "$owner" "$next" | docker build -q -t "ghcr.io/$owner/nirengi-api:$broken" - >/dev/null
if DEPLOY_READY_TIMEOUT=20 "$repo/deploy/bin/deploy.sh" "$broken"; then fail "deploy.sh $broken reported success"; fi
grep -qx "NIRENGI_VERSION=$next" .env || fail ".env was not put back to $next"
[[ $(tail -1 deploy-history) =~ \ $broken\ .+\ rolled-back$ ]] || fail "deploy-history: $(tail -1 deploy-history)"
body=$(curl -sk --max-time 5 --resolve "$host:443:127.0.0.1" "https://$host/health/ready" || true)
[[ $body == Healthy ]] || fail "not Healthy after the rollback ($body)"
echo "deploy: unhealthy $broken rolled back to $next"

# The CI key's forced command accepts "deploy X.Y.Z" and "status" only.
for request in "deploy $next; touch /tmp/pwned" "deploy latest" "sh" "deploy $next extra" ""; do
  if SSH_ORIGINAL_COMMAND=$request "$repo/deploy/bin/ci-entry" >/dev/null 2>&1; then fail "ci-entry accepted '$request'"; fi
done
SSH_ORIGINAL_COMMAND=status "$repo/deploy/bin/ci-entry" | grep -q "$next" || fail "ci-entry status"
SSH_ORIGINAL_COMMAND="deploy $next" CI_ENTRY_SUDO=env "$repo/deploy/bin/ci-entry" >/dev/null || fail "ci-entry deploy $next"
echo "ci-entry: only 'deploy X.Y.Z' and 'status'"

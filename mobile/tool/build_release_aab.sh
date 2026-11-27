#!/usr/bin/env bash
# Builds and checks the signed release AAB (ADR-0022 §7). Used by both jobs of .github/workflows/mobile-release.yml,
# so the dry run exercises the release path. Run from mobile/. Secrets arrive in this process's environment only.
#   NIRENGI_KEYSTORE_FILE, NIRENGI_KEYSTORE_PASSWORD, NIRENGI_KEY_ALIAS, NIRENGI_KEY_PASSWORD (read by Gradle too)
#   VERSION, VERSION_CODE, API_URL, SENTRY_DSN (may be empty)
# Prints the AAB path on success.
set -euo pipefail
: "${NIRENGI_KEYSTORE_FILE:?}" "${NIRENGI_KEYSTORE_PASSWORD:?}" "${NIRENGI_KEY_ALIAS:?}" "${NIRENGI_KEY_PASSWORD:?}"
: "${VERSION:?}" "${VERSION_CODE:?}" "${API_URL:?}"
[[ $API_URL == https://* ]] || { echo "::error::API_URL must be https://" >&2; exit 1; }
export NIRENGI_KEYSTORE_FILE NIRENGI_KEYSTORE_PASSWORD NIRENGI_KEY_ALIAS NIRENGI_KEY_PASSWORD

fingerprint() { awk '/SHA256:/ {print $2; exit}'; }

# A wrong password, alias or a damaged base64 paste fails here, before a five-minute build.
listing=$(keytool -list -v -keystore "$NIRENGI_KEYSTORE_FILE" -storepass:env NIRENGI_KEYSTORE_PASSWORD \
  -alias "$NIRENGI_KEY_ALIAS" 2>&1) || true
expected=$(fingerprint <<<"$listing")
[[ -n $expected ]] || { echo "::error::Cannot read alias '$NIRENGI_KEY_ALIAS' from the keystore (password, alias or base64?)" >&2; exit 1; }

# No obfuscation: stack traces stay readable without symbol uploads (ADR-0022 §7).
flutter build appbundle --release \
  --build-name="$VERSION" --build-number="$VERSION_CODE" \
  --dart-define=API_BASE_URL="$API_URL" \
  --dart-define=SENTRY_DSN="${SENTRY_DSN:-}" \
  --dart-define=SENTRY_ENVIRONMENT=production >&2

aab=build/app/outputs/bundle/release/app-release.aab
# jarsigner exits 0 for an unsigned archive too: require its "verified" line.
verify=$(jarsigner -verify "$aab" 2>&1) || true
grep -q '^jar verified\.' <<<"$verify" || { echo "::error::The AAB is not signed (or its digests do not match): $verify" >&2; exit 1; }
certs=$(keytool -printcert -jarfile "$aab" 2>&1) || true
actual=$(fingerprint <<<"$certs")
[[ -n $actual ]] || { echo "::error::No signer certificate in the AAB." >&2; exit 1; }
[[ $actual == "$expected" ]] || { echo "::error::Signed with another key: $actual (upload key: $expected)" >&2; exit 1; }
echo "signed with the upload key: SHA-256 $actual" >&2
echo "$aab"

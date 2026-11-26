#!/usr/bin/env bash
# Release version of the mobile app (ADR-0022 §7). Prints "<X.Y.Z> <versionCode>".
#   release_version.sh <pubspec.yaml> [mobile-vX.Y.Z]
# With a tag, the tag must name pubspec's version (the build number after "+" is ignored: CI derives it).
# versionCode = X*1000000 + Y*1000 + Z (Y, Z < 1000): it follows the version, so the same version can never be
# uploaded twice and a fix needs a new PATCH.
set -euo pipefail
pubspec=${1:?usage: release_version.sh <pubspec.yaml> [mobile-vX.Y.Z]}
tag=${2:-}
pattern='^(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})\.(0|[1-9][0-9]{0,2})$'

name=$(sed -n 's/^version:[[:space:]]*\([^+[:space:]]*\).*/\1/p' "$pubspec" | head -1)
[[ $name =~ $pattern ]] || { echo "pubspec version '$name' is not X.Y.Z with parts below 1000" >&2; exit 1; }
if [[ -n $tag ]]; then
  [[ $tag == "mobile-v$name" ]] || { echo "tag '$tag' does not match pubspec version $name (expected mobile-v$name)" >&2; exit 1; }
fi
IFS=. read -r major minor patch <<<"$name"
code=$((major * 1000000 + minor * 1000 + patch))
((code >= 1)) || { echo "versionCode must be at least 1 (0.0.0 is not a release)" >&2; exit 1; }
echo "$name $code"

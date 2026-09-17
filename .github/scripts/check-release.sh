#!/usr/bin/env bash
# The release gate (root BOOT.md, ## Delivery, Release; CI/CD and build audit, F11):
# - a tag `v<version>` must equal the packed version (skipped when no ref is given, e.g. workflow_dispatch);
# - CHANGELOG.md must carry a non-empty `## [<version>]` section, extracted into a notes file for the
#   GitHub release (F15).
#
# Usage: check-release.sh <version> <changelog-file> <notes-output-file> [<ref-name>]
set -euo pipefail

version=$1
changelog=$2
notes_out=$3
ref_name=${4:-}

if [[ -n "$ref_name" ]]; then
  expected="v${version}"
  if [[ "$ref_name" != "$expected" ]]; then
    echo "::error::tag '${ref_name}' does not match the package version '${expected}'" >&2
    exit 1
  fi
fi

if [[ ! -f "$changelog" ]]; then
  echo "::error::changelog not found: ${changelog}" >&2
  exit 1
fi

awk -v ver="$version" '
  BEGIN { found = 0 }
  /^## \[/ {
    if (found) { exit }
    if ($0 ~ ("^## \\[" ver "\\]")) { found = 1; next }
    next
  }
  found { print }
' "$changelog" > "$notes_out"

# Drop leading/trailing blank lines without touching the notes themselves.
sed -i -e '/./,$!d' -e ':a' -e '/^\n*$/{$d;N;ba' -e '}' "$notes_out"

if [[ ! -s "$notes_out" ]]; then
  echo "::error::CHANGELOG.md has no non-empty '## [${version}]' section" >&2
  exit 1
fi

echo "release notes for ${version} extracted to ${notes_out}:"
cat "$notes_out"

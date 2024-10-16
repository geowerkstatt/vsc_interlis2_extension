#!/bin/bash
# This script gets all changelog entries from CHANGELOG.md since last release.

set -e

tempDir="$(mktemp -d)"
tempFile=$tempDir/gh_release_notes.md

# Get changelog entries since last release
cat CHANGELOG.md | \
  grep -Pazo '(?s)(?<=\#{3} vNext\n{2}).*?(?=\n\#{3} \d+|$)' \
  > $tempFile

sed -i 's/\x0//g' $tempFile

cat $tempFile

# Cleanup temporary files
trap 'rm -rf -- "$tempDir"' EXIT

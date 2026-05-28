#!/usr/bin/env bash
set -euo pipefail

SOURCE_MANIFEST="${SOURCE_MANIFEST:-VendingMachine/VendingMachine.json}"
MANIFEST_PATH="${1:-VendingMachine/bin/Release/VendingMachine.json}"
ASSEMBLY_VERSION=$(jq -r '.AssemblyVersion' "$SOURCE_MANIFEST")
DALAMUD_API=$(jq -r '.DalamudApiLevel' "$SOURCE_MANIFEST")

if [[ -z "$ASSEMBLY_VERSION" || "$ASSEMBLY_VERSION" == "null" ]]; then
  echo "AssemblyVersion not found in $SOURCE_MANIFEST" >&2
  exit 1
fi

if [[ -z "$DALAMUD_API" || "$DALAMUD_API" == "null" ]]; then
  echo "DalamudApiLevel not found in $SOURCE_MANIFEST" >&2
  exit 1
fi

jq --arg av "$ASSEMBLY_VERSION" --arg api "$DALAMUD_API" \
  '.AssemblyVersion = $av | .DalamudApiLevel = ($api | tonumber)' \
  "$MANIFEST_PATH" > "${MANIFEST_PATH}.tmp"
mv "${MANIFEST_PATH}.tmp" "$MANIFEST_PATH"

echo "Synced $MANIFEST_PATH: AssemblyVersion=$ASSEMBLY_VERSION DalamudApiLevel=$DALAMUD_API"

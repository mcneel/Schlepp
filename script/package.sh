#!/usr/bin/env bash
#
# Build every runtime variant and wrap them into a single Yak package.
#
# The package holds one subfolder per target framework. Rhino picks the folder
# which matches the runtime it is currently hosting, falling through from .NET
# Core to .NET Framework but never the other way around, so a package carrying
# both loads in either flavour of Rhino.
#
#   dist/
#     manifest.yml
#     net48/Schlepp.rhp
#     net8.0/Schlepp.rhp
#
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dist="$root/dist"
configuration="${1:-Release}"

find_yak() {
  local candidates=(
    "/Applications/RhinoBETA.app/Contents/Resources/bin/yak"
    "/Applications/Rhino 9.app/Contents/Resources/bin/yak"
    "/Applications/Rhino 8.app/Contents/Resources/bin/yak"
    "/c/Program Files/Rhino 9 Beta/System/Yak.exe"
    "/c/Program Files/Rhino 9 WIP/System/Yak.exe"
    "/c/Program Files/Rhino 8/System/Yak.exe"
  )
  for candidate in "${candidates[@]}"; do
    [[ -x "$candidate" ]] && { printf '%s' "$candidate"; return 0; }
  done
  command -v yak 2>/dev/null && return 0
  return 1
}

rm -rf "$dist"
mkdir -p "$dist"

for framework in net48 net8.0; do
  echo "==> building $framework ($configuration)"
  dotnet build "$root/Schlepp/Schlepp.csproj" \
    --configuration "$configuration" \
    --framework "$framework" \
    --output "$dist/$framework"
done

# Yak refuses anything it does not recognise, and the build drops reference
# assemblies and dependency manifests beside the plugin. Keep only our own.
for framework in net48 net8.0; do
  find "$dist/$framework" -type f ! -name 'Schlepp.rhp' ! -name 'Schlepp.pdb' -delete
done

cp "$root/manifest.yml" "$dist/manifest.yml"

yak="$(find_yak)" || {
  echo "Could not find the yak executable. Staged the package contents in $dist and stopped." >&2
  exit 1
}

echo "==> packaging with $yak"
(cd "$dist" && "$yak" build --platform any)

echo
echo "Done. Package written to:"
ls -1 "$dist"/*.yak
echo
echo "Push it with:  \"$yak\" push dist/<file>.yak"
echo "Test first against the test server:  \"$yak\" push --source https://test.yak.rhino3d.com dist/<file>.yak"

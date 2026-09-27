#!/usr/bin/env bash
# Builds all three mods against the installed game's assemblies and copies them into BepInEx/plugins.
# Needs the game with BepInEx installed. Set GAME=/path/to/SignalSimulator if it isn't in the default Steam folder.
# Usage: ./build.sh [path-to-csc.exe]   (defaults to Roslyn in tools/roslyn, downloaded from NuGet on first run)
set -euo pipefail

HERE="$(cd "$(dirname "$0")" && pwd)"
GAME="${GAME:-/c/Program Files (x86)/Steam/steamapps/common/SignalSimulator}"
CSC="${1:-${CSC:-$HERE/tools/roslyn/csc.exe}}"

if [ ! -f "$CSC" ] && [ "$CSC" = "$HERE/tools/roslyn/csc.exe" ]; then
  echo "downloading Roslyn compiler (Microsoft.Net.Compilers.Toolset 4.11.0) ..."
  tmp="$(mktemp -d)"
  curl -sSL -o "$tmp/roslyn.zip" "https://www.nuget.org/api/v2/package/Microsoft.Net.Compilers.Toolset/4.11.0"
  unzip -q "$tmp/roslyn.zip" "tasks/net472/*" -d "$tmp"
  mkdir -p "$HERE/tools"
  mv "$tmp/tasks/net472" "$HERE/tools/roslyn"
  rm -rf "$tmp"
fi
M="$GAME/SignalSimulator_Data/Managed"
B="$GAME/BepInEx/core"
OUT="$HERE/bin"
mkdir -p "$OUT"

REFS=(
  -nostdlib -noconfig
  "-r:$M/mscorlib.dll" "-r:$M/System.dll" "-r:$M/System.Core.dll" "-r:$M/netstandard.dll"
  "-r:$M/UnityEngine.dll" "-r:$M/UnityEngine.CoreModule.dll" "-r:$M/UnityEngine.PhysicsModule.dll"
  "-r:$M/UnityEngine.InputLegacyModule.dll" "-r:$M/UnityEngine.UI.dll" "-r:$M/UnityEngine.UIModule.dll"
  "-r:$M/UnityEngine.TextRenderingModule.dll" "-r:$M/UnityEngine.VehiclesModule.dll" "-r:$M/UnityEngine.ParticleSystemModule.dll" "-r:$M/Assembly-CSharp.dll"
  "-r:$M/UnityEngine.TerrainModule.dll" "-r:$M/UnityEngine.IMGUIModule.dll" "-r:$M/Unity.Postprocessing.Runtime.dll"
  "-r:$B/BepInEx.dll" "-r:$B/0Harmony.dll"
)

build() {
  local name="$1"; shift
  "$CSC" -nologo -target:library -langversion:latest -optimize+ -deterministic -warn:4 \
    "${REFS[@]}" -out:"$OUT/$name.dll" "$@"
  echo "built $OUT/$name.dll"
}

build SignalSim.FluidMovement "$HERE/FluidMovement/"*.cs "$HERE/Shared/"*.cs
build SignalSim.RandomSecretSignals "$HERE/RandomSecretSignals/"*.cs "$HERE/Shared/"*.cs
build SignalSim.PerformanceTweaks "$HERE/PerformanceTweaks/"*.cs

# Install the same way the release zips lay it out: one folder per mod.
P="$GAME/BepInEx/plugins"
for mod in FluidMovement RandomSecretSignals PerformanceTweaks; do
  mkdir -p "$P/$mod"
  cp "$OUT/SignalSim.$mod.dll" "$P/$mod/"
  rm -f "$P/SignalSim.$mod.dll"   # older loose-file layout
done
echo "installed to $P"

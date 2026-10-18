#!/bin/bash
set -e

# Suppress LD_PRELOAD warnings for 32-bit vs 64-bit mismatch on Bazzite/Fedora
export LD_PRELOAD=""

BASE_DIR="$(cd "$(dirname "$0")" && pwd)"
WORKSPACE="$BASE_DIR/workshop-fog"
PROJECT="src/SlayTheSpireMapFog.Godot/SlayTheSpireMapFog.Godot.csproj"
UPLOADER_DIR_ROOT="$BASE_DIR/workshop/uploader"

echo "=============================================="
echo " Fog of War Map: Steam Workshop Publish       "
echo "=============================================="
cd "$BASE_DIR"

echo "=== 1. Building and Publishing Fog Mod ==="
rm -rf "$WORKSPACE/content"
LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet publish "$PROJECT" -c Release -o "$WORKSPACE/content/"
# Game ships GodotSharp; never upload our copy
rm -f "$WORKSPACE/content/GodotSharp.dll"
# deps.json is read by the game as a mod manifest and logs a bogus error; the mod does not need it
rm -f "$WORKSPACE/content"/*.deps.json

echo "=== 2. Locating Mod Uploader (built by upload_to_workshop.sh) ==="
if [ ! -d "$UPLOADER_DIR_ROOT" ]; then
    git clone https://github.com/megacrit/sts2-mod-uploader.git "$UPLOADER_DIR_ROOT"
fi
UPLOADER_DLL=$(find "$UPLOADER_DIR_ROOT/bin" -name "ModUploader.dll" 2>/dev/null | head -n 1)
if [ -z "$UPLOADER_DLL" ]; then
    LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet build "$UPLOADER_DIR_ROOT/ModUploader.sln" -c Release
    UPLOADER_DLL=$(find "$UPLOADER_DIR_ROOT/bin" -name "ModUploader.dll" | head -n 1)
fi
UPLOADER_DIR=$(dirname "$UPLOADER_DLL")
cp "$UPLOADER_DIR_ROOT/steam/libsteam_api.so" "$UPLOADER_DIR/"
mkdir -p "$UPLOADER_DIR/runtimes/linux-x64/lib/netstandard2.1/"
cp "$UPLOADER_DIR_ROOT/steam/libsteam_api.so" "$UPLOADER_DIR/runtimes/linux-x64/lib/netstandard2.1/"
cp "$UPLOADER_DIR_ROOT/steam/steam_appid.txt" "$BASE_DIR/"

echo "=== 3. Safety check: visibility must be private ==="
if ! grep -q '"visibility": *"private"' "$WORKSPACE/workshop.json"; then
    echo "[-] workshop.json visibility is not private. Aborting."
    exit 1
fi

echo "=== 4. Uploading (Steam must be running and logged in) ==="
LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet "$UPLOADER_DLL" upload -w "$WORKSPACE"
echo "[+] Done. Workshop item id is in $WORKSPACE/mod_id.txt"

#!/bin/bash
set -e

# Suppress LD_PRELOAD warnings for 32-bit vs 64-bit mismatch on Bazzite/Fedora
export LD_PRELOAD=""

BASE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
WORKSPACE="$BASE_DIR/workshop-vote"
PROJECT="src/SlayTheSpireTestSubjectVote.Godot/SlayTheSpireTestSubjectVote.Godot.csproj"
UPLOADER_DIR_ROOT="$BASE_DIR/workshop/uploader"

# Usage: upload_vote_to_workshop.sh [--allow-public "<mod name>"]
# A private item uploads without flags. A public item uploads only when --allow-public is
# followed by the mod's exact title from workshop.json, so a public release is always deliberate.
ALLOW_PUBLIC_NAME=""
while [ $# -gt 0 ]; do
    case "$1" in
        --allow-public)
            if [ $# -lt 2 ] || [ -z "$2" ]; then
                echo "[-] --allow-public needs the mod name as its next argument, e.g. --allow-public \"<mod name>\"."
                exit 1
            fi
            ALLOW_PUBLIC_NAME="$2"
            shift 2
            ;;
        *)
            echo "[-] Unknown argument: $1"
            echo "Usage: $0 [--allow-public \"<mod name>\"]"
            exit 1
            ;;
    esac
done

echo "=============================================="
echo " Test Subject Vote: Steam Workshop Publish   "
echo "=============================================="
cd "$BASE_DIR"

echo "=== 0. Safety check: visibility ==="
VISIBILITY=$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1])).get("visibility",""))' "$WORKSPACE/workshop.json")
MOD_NAME=$(python3 -I -c 'import json,sys; print(json.load(open(sys.argv[1])).get("title",""))' "$WORKSPACE/workshop.json")
case "$VISIBILITY" in
    private)
        echo "[+] Visibility is private."
        ;;
    public)
        if [ "$ALLOW_PUBLIC_NAME" != "$MOD_NAME" ]; then
            echo "[-] workshop.json visibility is public. Aborting."
            echo "    To publish publicly, pass the exact mod name: --allow-public \"$MOD_NAME\""
            exit 1
        fi
        echo "[!] Publishing PUBLICLY as '$MOD_NAME' (confirmed with --allow-public)."
        ;;
    *)
        echo "[-] workshop.json visibility is '$VISIBILITY'; only private, or public with --allow-public, are supported. Aborting."
        exit 1
        ;;
esac

echo "=== 1. Building and Publishing Test Subject Vote ==="
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

echo "=== 3. Uploading (Steam must be running and logged in) ==="
LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet "$UPLOADER_DLL" upload -w "$WORKSPACE"
echo "[+] Done. Workshop item id is in $WORKSPACE/mod_id.txt"

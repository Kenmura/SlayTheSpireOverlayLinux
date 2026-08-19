#!/bin/bash
set -e

# Suppress LD_PRELOAD warnings for 32-bit vs 64-bit mismatch on Bazzite/Fedora
export LD_PRELOAD=""

# Base directory
BASE_DIR="/var/home/nickmarc/repos/SlayTheSpireOverlayLinux"

echo "=============================================="
echo " Slay the Spire 2 Mod: Steam Workshop Publish "
echo "=============================================="

# Ensure we are in the correct directory
cd "$BASE_DIR"

echo "=== 1. Building and Publishing Mod Assemblies ==="
LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet publish src/SlayTheSpireOverlay.Godot/SlayTheSpireOverlay.Godot.csproj -c Release -o "$BASE_DIR/workshop/content/"

echo "=== 2. Clonging official megacrit/sts2-mod-uploader ==="
if [ ! -d "$BASE_DIR/workshop/uploader" ]; then
    git clone https://github.com/megacrit/sts2-mod-uploader.git "$BASE_DIR/workshop/uploader"
else
    echo "Uploader repo already cloned."
fi

echo "=== 3. Building Mod Uploader Tool ==="
LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet build "$BASE_DIR/workshop/uploader/ModUploader.sln" -c Release

# Find compiled DLL in the bin directory
UPLOADER_DLL=$(find "$BASE_DIR/workshop/uploader/bin" -name "ModUploader.dll" | head -n 1)

if [ -z "$UPLOADER_DLL" ]; then
    echo "[-] Error: Could not find compiled ModUploader.dll."
    exit 1
fi

echo "[+] Found ModUploader at: $UPLOADER_DLL"

echo "=== 4. Starting Mod Upload ==="
echo "[*] IMPORTANT: Steam must be running on your system and you must be logged in."
echo "[*] Uploading workspace: $BASE_DIR/workshop"
echo "----------------------------------------------"

LD_PRELOAD="" distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet "$UPLOADER_DLL" upload -w "$BASE_DIR/workshop"

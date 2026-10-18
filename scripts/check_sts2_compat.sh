#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

STS2_DLL="${1:-}"

if [ -z "$STS2_DLL" ]; then
    CANDIDATES=(
        "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64/sts2.dll"
        "$HOME/.steam/steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64/sts2.dll"
        "$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/sts2.dll"
    )
    for c in "${CANDIDATES[@]}"; do
        if [ -f "$c" ]; then
            STS2_DLL="$c"
            break
        fi
    done
fi

if [ -z "$STS2_DLL" ] || [ ! -f "$STS2_DLL" ]; then
    echo "Error: sts2.dll not found." >&2
    echo "Usage: $0 [path-to-sts2.dll] [optional-mod-dlls...]" >&2
    exit 1
fi

shift || true

MOD_DLLS=()
if [ "$#" -gt 0 ]; then
    MOD_DLLS=("$@")
else
    # Automatically locate built mod DLLs if present
    OVERLAY_CANDIDATES=(
        "$REPO_ROOT/src/SlayTheSpireOverlay.Godot/bin/Release/net9.0/SlayTheSpireOverlay.Godot.dll"
        "$REPO_ROOT/src/SlayTheSpireOverlay.Godot/bin/Debug/net9.0/SlayTheSpireOverlay.Godot.dll"
        "$REPO_ROOT/workshop/content/SlayTheSpireOverlay.Godot.dll"
    )
    for c in "${OVERLAY_CANDIDATES[@]}"; do
        if [ -f "$c" ]; then
            MOD_DLLS+=("$c")
            break
        fi
    done

    MAPFOG_CANDIDATES=(
        "$REPO_ROOT/src/SlayTheSpireMapFog.Godot/bin/Release/net9.0/SlayTheSpireMapFog.Godot.dll"
        "$REPO_ROOT/src/SlayTheSpireMapFog.Godot/bin/Debug/net9.0/SlayTheSpireMapFog.Godot.dll"
        "$REPO_ROOT/workshop-fog/content/SlayTheSpireMapFog.Godot.dll"
    )
    for c in "${MAPFOG_CANDIDATES[@]}"; do
        if [ -f "$c" ]; then
            MOD_DLLS+=("$c")
            break
        fi
    done
fi

DOTNET_CMD=(dotnet)
if ! command -v dotnet >/dev/null 2>&1; then
    if command -v distrobox >/dev/null 2>&1; then
        DOTNET_CMD=(distrobox enter dotnet-dev -- dotnet)
    else
        echo "Error: Neither 'dotnet' nor 'distrobox' command found." >&2
        exit 1
    fi
fi

"${DOTNET_CMD[@]}" run --project "$REPO_ROOT/tools/Sts2CompatChecker" -- "$STS2_DLL" "${MOD_DLLS[@]}"

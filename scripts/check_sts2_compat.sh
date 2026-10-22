#!/usr/bin/env bash
# Verifies every game type/member the mods use exists in the installed sts2.dll.
# Override the game folder with STS2_DATA_DIR=/path/to/data_sts2_windows_x86_64
set -euo pipefail
export LD_PRELOAD=""
BASE_DIR="$(cd "$(dirname "$0")/.." && pwd)"
DATA_DIR="${STS2_DATA_DIR:-$HOME/.local/share/Steam/steamapps/common/Slay the Spire 2/data_sts2_windows_x86_64}"

dn() { distrobox enter dotnet-dev -- env LD_PRELOAD="" dotnet "$@"; }

dn build "$BASE_DIR/src/SlayTheSpireOverlay.Godot" -c Release -p:Sts2DataDir="$DATA_DIR" --nologo -v q
dn build "$BASE_DIR/src/SlayTheSpireMapFog.Godot" -c Release -p:Sts2DataDir="$DATA_DIR" --nologo -v q
dn build "$BASE_DIR/src/SlayTheSpireTestSubjectVote.Godot" -c Release -p:Sts2DataDir="$DATA_DIR" --nologo -v q

dn run --project "$BASE_DIR/tools/Sts2CompatCheck" -c Release -- \
    "$DATA_DIR/sts2.dll" \
    "$BASE_DIR/tools/Sts2CompatCheck/patch-targets.txt" \
    "$BASE_DIR/src/SlayTheSpireOverlay.Godot/bin/Release/net9.0/SlayTheSpireOverlay.Godot.dll" \
    "$BASE_DIR/src/SlayTheSpireMapFog.Godot/bin/Release/net9.0/SlayTheSpireMapFog.Godot.dll" \
    "$BASE_DIR/src/SlayTheSpireTestSubjectVote.Godot/bin/Release/net9.0/SlayTheSpireTestSubjectVote.Godot.dll"

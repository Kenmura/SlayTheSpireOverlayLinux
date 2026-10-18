#!/usr/bin/env bash
# Same duplicate-code check CI runs. Threshold/baseline is in .jscpd.json; only ever lower it.
set -euo pipefail
cd "$(dirname "$0")/.."
npx --yes jscpd@4.0.5

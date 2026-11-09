#!/usr/bin/env bash
#
# Generates src/Sedna.UI.Catalogue/Data/class-states.json: for every class the shipped
# stylesheet declares, its family, its variants, and the attribute selectors,
# pseudo-classes and classes written on the same compound as it. The MCP server's
# describe_class answers with it.
#
# Derived from the stylesheet, never typed: build/class-states.py reads the served copy,
# with the same two extraction rules as build/css-inventory.sh — comments out first, and
# the @layer names never read as classes. Its header says what each field holds.
#
# Usage:  build/class-states.sh          # regenerate
#         build/class-states.sh --check  # fail if out of date
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CSS="$ROOT/src/Sedna.UI/wwwroot/css/Sedna.UI.css"
OUT="$ROOT/src/Sedna.UI.Catalogue/Data/class-states.json"

mkdir -p "$(dirname "$OUT")"
TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT

python3 -I "$ROOT/build/class-states.py" "$CSS" > "$TMP"

if [[ "${1:-}" == "--check" ]]; then
    if cmp -s "$TMP" "$OUT"; then
        echo "ok      class states are up to date"
        exit 0
    fi
    echo "::error::$OUT does not match the stylesheet. Run build/class-states.sh"
    diff "$OUT" "$TMP" | head -30 || true
    exit 1
fi

cp "$TMP" "$OUT"
echo "wrote   $(basename "$OUT")"

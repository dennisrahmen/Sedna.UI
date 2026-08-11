#!/usr/bin/env bash
#
# Generates wwwroot/js/Sedna.UI.js from every file in js-parts/.
#
# Mirrors build/bundle-css.sh: the parts are DISCOVERED, never listed anywhere, so
# adding a file to js-parts/ puts it in the next build and there is no manifest to
# forget. Load order is the byte-ordinal filename order, which is why every part
# carries a numeric prefix:
#
#     00  core — the global, config, the shared internals, configure()
#     1x  settings
#     2x  behaviour attached to the document (hover hints, later: menus, tabs, …)
#     3x  the Markdown editor
#     4x  small interop helpers
#     5x  notifications and the audio ping
#
# Each part is a self-contained IIFE that extends window.sednaUi, so a part is a
# valid script on its own — take 00-core.js plus that part to use one feature
# outside NuGet. Order still matters: 00-core.js creates the global and the shared
# internals every other part reads.
#
# Why one file ships rather than N script tags:
#   * N tags is N requests, each blocking on the previous only for ORDER, and the
#     library must expose one global before any app code runs. One file removes the
#     ordering question entirely.
#   * wwwroot/js/Sedna.UI.js is a pinned path that consuming apps hard-code
#     (ShippedAssetsTests asserts it). js-parts/ sits outside wwwroot, so the parts
#     are not static web assets and there is exactly one script path to reference.
#
# Usage:  build/bundle-js.sh          # regenerate
#         build/bundle-js.sh --check  # fail if the bundle is out of date
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PARTS="$ROOT/src/Sedna.UI/js-parts"
OUT="$ROOT/src/Sedna.UI/wwwroot/js/Sedna.UI.js"

if [[ ! -d "$PARTS" ]]; then
    echo "::error::No js-parts directory at $PARTS"
    exit 1
fi

mapfile -t FILES < <(LC_ALL=C find "$PARTS" -maxdepth 1 -name '*.js' -type f | LC_ALL=C sort)

if [[ ${#FILES[@]} -eq 0 ]]; then
    echo "::error::No parts found in $PARTS"
    exit 1
fi

for f in "${FILES[@]}"; do
    base="$(basename "$f")"
    if [[ ! "$base" =~ ^[0-9][0-9]- ]]; then
        echo "::error::$base has no NN- prefix, so its load order is undefined."
        echo "Rename it, e.g. 60-$base — see src/Sedna.UI/js-parts/CLAUDE.md"
        exit 1
    fi
    # Automatic semicolon insertion turns a missing terminator into a call
    # expression joining two parts. Every part ends with an IIFE, so require it.
    if [[ ! "$(tail -c 40 "$f" | tr -d '[:space:]')" =~ \)\;$ ]]; then
        echo "::error::$base does not end with a terminated IIFE — '})(window.sednaUi);'"
        echo "Without the semicolon, concatenation can splice it into the next part."
        exit 1
    fi
done

TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT

RULE="────────────────────────────────────────────────"

{
    echo "/* ═══════════════════════════════════════════════════════════════════════════"
    echo "   GENERATED FILE — DO NOT EDIT."
    echo ""
    echo "   Built by build/bundle-js.sh from src/Sedna.UI/js-parts/. Edit the part"
    echo "   that owns the behaviour and re-run that script; a guard test fails the build"
    echo "   if this file and the parts disagree. Adding a part needs no change here —"
    echo "   the directory is the source of truth."
    echo ""
    echo "   Contents, in load order:"
    for f in "${FILES[@]}"; do
        printf '     %s\n' "$(basename "$f")"
    done
    echo "   ═══════════════════════════════════════════════════════════════════════════ */"
    echo ""
} >> "$TMP"

for f in "${FILES[@]}"; do
    printf '/* ── %s %s */\n' "$(basename "$f")" "$RULE" >> "$TMP"
    cat "$f" >> "$TMP"
    printf '\n' >> "$TMP"
done

if [[ "${1:-}" == "--check" ]]; then
    if cmp -s "$TMP" "$OUT"; then
        echo "ok      bundle is up to date (${#FILES[@]} parts)"
        exit 0
    fi
    echo "::error::$OUT does not match js-parts/. Run build/bundle-js.sh"
    diff "$OUT" "$TMP" | head -40 || true
    exit 1
fi

cp "$TMP" "$OUT"
echo "wrote   $(basename "$OUT") from ${#FILES[@]} parts ($(wc -l < "$OUT") lines)"

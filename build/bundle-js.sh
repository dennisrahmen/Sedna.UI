#!/usr/bin/env bash
#
# Generates the shipped scripts from their parts:
#
#   src/Sedna.UI/js-parts/      → wwwroot/js/Sedna.UI.js         the classic script every page loads
#   src/Sedna.UI/graph-parts/   → wwwroot/js/Sedna.UI.graph.js   the ES module a graph imports on demand
#   src/Sedna.UI/editor-parts/  → wwwroot/js/Sedna.UI.editor.js  the ES module an editor imports on demand
#
# Mirrors build/bundle-css.sh: the parts are DISCOVERED, never listed anywhere, so
# adding a file to either directory puts it in the next build and there is no manifest
# to forget. Load order is the byte-ordinal filename order, which is why every part
# carries a numeric prefix:
#
#   js-parts/
#     00  core — the global, config, the shared internals, configure()
#     1x  settings
#     2x  behaviour attached to the document (hover hints, menus, tabs, …)
#     3x  the Markdown editor
#     4x  interop helpers and presenters, the surface loader, and the graph's and the
#         editor's front doors
#     5x  notifications and the audio ping
#
#   graph-parts/, editor-parts/  — see the CLAUDE.md in each
#     00  the imports, and nothing else
#     1x–8x  the surface, one concern a file
#     99  the exports, and nothing else
#
# Each js-part is a self-contained IIFE that extends window.sednaUi, so a part is a
# valid script on its own — take 00-core.js plus that part to use one feature
# outside NuGet. Order still matters: 00-core.js creates the global and the shared
# internals every other part reads.
#
# A surface's parts are one ES module cut into files: they share its top-level scope,
# so only 00 may import and only 99 may export, which this script checks.
#
# Why one file ships rather than N script tags:
#   * N tags is N requests, each blocking on the previous only for ORDER, and the
#     library must expose one global before any app code runs. One file removes the
#     ordering question entirely.
#   * wwwroot/js/Sedna.UI.js is a pinned path that consuming apps hard-code
#     (ShippedAssetsTests asserts it). js-parts/ sits outside wwwroot, so the parts
#     are not static web assets and there is exactly one script path to reference.
#     A surface's module is not referenced by any app at all: Sedna.UI.js imports it,
#     relative to itself, the first time a page shows that surface.
#
# Usage:  build/bundle-js.sh          # regenerate all three
#         build/bundle-js.sh --check  # fail if any is out of date
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHECK=0
[[ "${1:-}" == "--check" ]] && CHECK=1
RULE="────────────────────────────────────────────────"
failed=0

# bundle <parts dir> <output> <script|module>
bundle() {
    local parts="$1" out="$2" kind="$3"
    local rel="${parts#"$ROOT"/}"

    if [[ ! -d "$parts" ]]; then
        echo "::error::No parts directory at $parts"
        exit 1
    fi

    local files
    mapfile -t files < <(LC_ALL=C find "$parts" -maxdepth 1 -name '*.js' -type f | LC_ALL=C sort)

    if [[ ${#files[@]} -eq 0 ]]; then
        echo "::error::No parts found in $parts"
        exit 1
    fi

    local f base
    for f in "${files[@]}"; do
        base="$(basename "$f")"
        if [[ ! "$base" =~ ^[0-9][0-9]- ]]; then
            echo "::error::$base has no NN- prefix, so its load order is undefined."
            echo "Rename it, e.g. 60-$base — see $rel/CLAUDE.md"
            exit 1
        fi
        if [[ "$kind" == "script" ]]; then
            # Automatic semicolon insertion turns a missing terminator into a call
            # expression joining two parts. Every part ends with an IIFE, so require it.
            if [[ ! "$(tail -c 40 "$f" | tr -d '[:space:]')" =~ \)\;$ ]]; then
                echo "::error::$base does not end with a terminated IIFE — '})(window.sednaUi);'"
                echo "Without the semicolon, concatenation can splice it into the next part."
                exit 1
            fi
        else
            # One module cut into files: a second import list or export list in the
            # middle would still parse, and would hide which part owns the surface.
            if [[ ! "$base" =~ ^00- ]] && grep -Eq '^\s*import\s' "$f"; then
                echo "::error::$base imports. Only the 00- part of $rel may, so the module's dependencies are in one place."
                exit 1
            fi
            if [[ ! "$base" =~ ^99- ]] && grep -Eq '^\s*export\s' "$f"; then
                echo "::error::$base exports. Only the 99- part of $rel may, so the module's surface is in one place."
                exit 1
            fi
        fi
    done

    local tmp
    tmp="$(mktemp)"

    {
        echo "/* ═══════════════════════════════════════════════════════════════════════════"
        echo "   GENERATED FILE — DO NOT EDIT."
        echo ""
        echo "   Built by build/bundle-js.sh from $rel/. Edit the part"
        echo "   that owns the behaviour and re-run that script; a guard test fails the build"
        echo "   if this file and the parts disagree. Adding a part needs no change here —"
        echo "   the directory is the source of truth."
        echo ""
        echo "   Contents, in load order:"
        for f in "${files[@]}"; do
            printf '     %s\n' "$(basename "$f")"
        done
        echo "   ═══════════════════════════════════════════════════════════════════════════ */"
        echo ""
    } >> "$tmp"

    for f in "${files[@]}"; do
        printf '/* ── %s %s */\n' "$(basename "$f")" "$RULE" >> "$tmp"
        cat "$f" >> "$tmp"
        printf '\n' >> "$tmp"
    done

    if [[ "$CHECK" -eq 1 ]]; then
        if cmp -s "$tmp" "$out"; then
            echo "ok      $(basename "$out") is up to date (${#files[@]} parts)"
        else
            echo "::error::$out does not match $rel/. Run build/bundle-js.sh"
            diff "$out" "$tmp" | head -40 || true
            failed=1
        fi
        rm -f "$tmp"
        return
    fi

    cp "$tmp" "$out"
    rm -f "$tmp"
    echo "wrote   $(basename "$out") from ${#files[@]} parts ($(wc -l < "$out") lines)"
}

bundle "$ROOT/src/Sedna.UI/js-parts"    "$ROOT/src/Sedna.UI/wwwroot/js/Sedna.UI.js"       script
bundle "$ROOT/src/Sedna.UI/graph-parts" "$ROOT/src/Sedna.UI/wwwroot/js/Sedna.UI.graph.js" module
bundle "$ROOT/src/Sedna.UI/editor-parts" "$ROOT/src/Sedna.UI/wwwroot/js/Sedna.UI.editor.js" module

exit "$failed"

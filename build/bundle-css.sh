#!/usr/bin/env bash
#
# Generates wwwroot/css/Sedna.UI.css from every file in css-parts/.
#
# The parts are DISCOVERED, never listed anywhere. Add a file to css-parts/ and it
# is in the next build; there is no manifest to forget to update, which is the one
# failure mode a hand-kept index guarantees. Cascade order is the filename order,
# byte-ordinal, which is why every part carries a numeric prefix.
#
# The prefix also decides the part's CASCADE LAYER, so the layer assignment cannot
# drift away from the ordering convention and no part has to declare its own:
#
#     00-04  @layer sedna.tokens      tokens and the theme remap blocks
#     05-09  @layer sedna.base        bare element styles
#     10-29  @layer sedna.frame       the tier-1 frame
#     30-79  @layer sedna.paint       tier-2 content classes, then RTL/forced-colors/print
#     80-89  @layer sedna.utilities   single-purpose classes
#     90-99  @layer sedna.overrides   density — last, it tightens what came before
#
# Each part is wrapped in its layer WITHOUT being re-indented, so its text still
# appears verbatim in the bundle and the drift guard keeps working.
#
# The generated file opens with a contents block listing the parts in order, and
# each part is introduced by a `── from <file> ──` marker, so the single shipped
# stylesheet still reads as one section per component.
#
# Why the parts are inlined instead of shipped and @imported at runtime:
#   * A browser cannot discover an @import until the parent sheet has downloaded
#     and parsed, so shipping a manifest costs an extra round trip before ANY
#     style applies, then N render-blocking requests instead of one.
#   * The .NET SDK will not do it for us: its only CSS bundling is scoped
#     .razor.css, which rewrites selectors to add a b-{hash} attribute and would
#     scope tier-2 classes to markup the library renders rather than markup the
#     app writes.
#   * css-parts/ sits outside wwwroot, so the parts are not static web assets and
#     an app has exactly one stylesheet path to link. To reuse one part on its
#     own, take 01-tokens.css plus that part from the repo.
#
# Usage:  build/bundle-css.sh          # regenerate
#         build/bundle-css.sh --check  # fail if the bundle is out of date
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PARTS="$ROOT/src/Sedna.UI/css-parts"
OUT="$ROOT/src/Sedna.UI/wwwroot/css/Sedna.UI.css"

if [[ ! -d "$PARTS" ]]; then
    echo "::error::No css-parts directory at $PARTS"
    exit 1
fi

# LC_ALL=C so the order is byte-ordinal on every machine and locale, and matches
# StringComparer.Ordinal in the guard test.
mapfile -t FILES < <(LC_ALL=C find "$PARTS" -maxdepth 1 -name '*.css' -type f | LC_ALL=C sort)

if [[ ${#FILES[@]} -eq 0 ]]; then
    echo "::error::No parts found in $PARTS"
    exit 1
fi

for f in "${FILES[@]}"; do
    base="$(basename "$f")"
    if [[ ! "$base" =~ ^[0-9][0-9]- ]]; then
        echo "::error::$base has no NN- prefix, so its place in the cascade is undefined."
        echo "Rename it, e.g. 45-$base — see src/Sedna.UI/css-parts/CLAUDE.md"
        exit 1
    fi
done

TMP="$(mktemp)"
trap 'rm -f "$TMP"' EXIT

# The layer a part belongs to, derived from its NN- prefix.
layer_for() {
    local n=${1%%-*}
    n=$((10#$n))
    if   (( n <= 4 ));  then echo "sedna.tokens"
    elif (( n <= 9 ));  then echo "sedna.base"
    elif (( n <= 29 )); then echo "sedna.frame"
    elif (( n <= 79 )); then echo "sedna.paint"
    elif (( n <= 89 )); then echo "sedna.utilities"
    else                     echo "sedna.overrides"
    fi
}

{
    echo "/* ═══════════════════════════════════════════════════════════════════════════"
    echo "   GENERATED FILE — DO NOT EDIT."
    echo ""
    echo "   Built by build/bundle-css.sh from src/Sedna.UI/css-parts/. Edit the part"
    echo "   that owns the rule and re-run that script; a guard test fails the build if"
    echo "   this file and the parts disagree. Adding a part needs no change here — the"
    echo "   directory is the source of truth."
    echo ""
    echo "   Contents, in cascade order:"
    for f in "${FILES[@]}"; do
        printf '     %s\n' "$(basename "$f")"
    done
    echo "   ═══════════════════════════════════════════════════════════════════════════ */"
    echo ""
    echo "/* Layer order, declared once and up front so it does not depend on which layer"
    echo "   happens to appear first below."
    echo ""
    echo "   The consequence that matters to a consuming app: EVERY rule in this file is"
    echo "   layered, and an unlayered rule beats a layered one whatever its specificity."
    echo "   So your own stylesheet — which is unlayered unless you say otherwise — always"
    echo "   wins, and you no longer have to out-specify anything to override it."
    echo ""
    echo "   Two things to know:"
    echo "     * !important inverts layer order, which is why this library uses none. An"
    echo "       !important inside a layer becomes HARDER for you to override, not easier."
    echo "     * Because your unlayered :root wins outright, a token you set at bare :root"
    echo "       now also beats this library's [data-theme=\"light\"] value for it. Set both"
    echo "       blocks, as the rebrand recipe in the catalogue shows. */"
    echo "@layer sedna.tokens, sedna.base, sedna.frame, sedna.paint, sedna.utilities, sedna.overrides;"
    echo ""
} >> "$TMP"

# A fixed rule, never a sliced one. The box-drawing dash is three bytes, and bash
# substring expansion cuts by bytes in some locales — which splits a character in
# half and emits a replacement glyph into the shipped file.
RULE="────────────────────────────────────────────────"

for f in "${FILES[@]}"; do
    base="$(basename "$f")"
    layer="$(layer_for "$base")"
    # One marker per part, naming its layer, so the single shipped stylesheet still
    # reads as a sequence of named sections and it is obvious where each one sits.
    printf '/* ── %s → @layer %s %s */\n' "$base" "$layer" "$RULE" >> "$TMP"
    # The part is NOT re-indented: its text has to appear verbatim in the bundle for
    # The_shipped_stylesheet_matches_its_parts to keep working.
    printf '@layer %s {\n' "$layer" >> "$TMP"
    cat "$f" >> "$TMP"
    printf '}\n\n' >> "$TMP"
done

if [[ "${1:-}" == "--check" ]]; then
    if cmp -s "$TMP" "$OUT"; then
        echo "ok      bundle is up to date (${#FILES[@]} parts)"
        exit 0
    fi
    echo "::error::$OUT does not match css-parts/. Run build/bundle-css.sh"
    diff "$OUT" "$TMP" | head -40 || true
    exit 1
fi

cp "$TMP" "$OUT"
echo "wrote   $(basename "$OUT") from ${#FILES[@]} parts ($(wc -l < "$OUT") lines)"

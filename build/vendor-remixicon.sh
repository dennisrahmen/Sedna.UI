#!/usr/bin/env bash
#
# Vendors the Remix Icon webfont into the package.
#
# Run this to add or update the icon font. The output is committed, so a build
# never needs network access and consuming apps get the icons from the package
# rather than a CDN.
#
#   build/vendor-remixicon.sh            # the pinned version below
#   build/vendor-remixicon.sh 4.9.1      # a specific version
#
# What it produces in src/Sedna.UI/wwwroot/lib/remixicon/:
#   remixicon.css      upstream CSS, @font-face reduced to woff2 only
#   remixicon.woff2    the font
#   LICENSE            the Remix Icon License, as required when redistributing
#
# Only woff2 is shipped. Upstream also references eot, woff, ttf and svg for
# IE and iOS 4; requests for those would 404, and every browser that runs
# Blazor Server supports woff2. Shipping all formats would add ~1 MB to the
# package for files no supported browser fetches.
#
# Licensing: the icons stay under the Remix Icon License v1.0, not this
# repository's Apache-2.0. Section 9 of that licence permits integration into
# an Apache-2.0 project provided the icons remain under their own licence and
# the Section 3 restrictions carry through. Section 3.1 permits "design systems
# or UI kits where Icons are a minor component". See THIRD-PARTY-NOTICES.md.
#
set -euo pipefail

VERSION="${1:-4.9.1}"
DEST="src/Sedna.UI/wwwroot/lib/remixicon"
BASE="https://cdn.jsdelivr.net/npm/remixicon@${VERSION}"

if [[ ! -d src/Sedna.UI ]]; then
    echo "::error::Run this from the repository root." >&2
    exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

echo "Vendoring Remix Icon ${VERSION}"

curl -fsSL "${BASE}/fonts/remixicon.css"   -o "$tmp/remixicon.css"
curl -fsSL "${BASE}/fonts/remixicon.woff2" -o "$tmp/remixicon.woff2"
curl -fsSL "${BASE}/License"               -o "$tmp/LICENSE"

# Sanity-check what we downloaded before overwriting anything committed.
grep -q "Remix Icon v${VERSION}" "$tmp/remixicon.css" \
    || { echo "::error::CSS header does not say v${VERSION}. Aborting." >&2; exit 1; }
grep -q "Remix Icon License" "$tmp/LICENSE" \
    || { echo "::error::Downloaded LICENSE is not the Remix Icon License. Aborting." >&2; exit 1; }
[[ -s "$tmp/remixicon.woff2" ]] \
    || { echo "::error::Empty font file. Aborting." >&2; exit 1; }

# Replace the multi-format src list with woff2 only. The cache-busting query
# string goes too — the file is versioned by the package it ships in.
# The upstream copyright header above the @font-face block is left intact, as
# Section 5 of the licence requires.
python - "$tmp/remixicon.css" <<'PY'
import re, sys

path = sys.argv[1]
css = open(path, encoding="utf-8").read()

font_face = re.search(r"@font-face\s*\{.*?\}", css, re.S)
if not font_face:
    sys.exit("Could not find the @font-face block.")

replacement = (
    '@font-face {\n'
    '  font-family: "remixicon";\n'
    '  src: url("remixicon.woff2") format("woff2");\n'
    '  font-display: swap;\n'
    '}'
)
css = css[:font_face.start()] + replacement + css[font_face.end():]

# Record the trim next to the upstream header so the change is not a mystery.
css = css.replace(
    "*/\n@font-face",
    "*\n* Vendored into Sedna.UI by build/vendor-remixicon.sh.\n"
    "* Unmodified except the @font-face src list, reduced to woff2 only.\n*/\n@font-face",
    1,
)

open(path, "w", encoding="utf-8", newline="\n").write(css)
PY

# Assert the whole file now references exactly one asset, the woff2. Checking
# for leftover "remixicon.woff" by substring would false-match "remixicon.woff2".
urls="$(grep -o 'url([^)]*)' "$tmp/remixicon.css" | sort -u)"
if [[ "$urls" != 'url("remixicon.woff2")' ]]; then
    echo "::error::Expected exactly one font reference after the trim, found:" >&2
    printf '%s\n' "$urls" >&2
    exit 1
fi

mkdir -p "$DEST"
cp "$tmp/remixicon.css" "$tmp/remixicon.woff2" "$tmp/LICENSE" "$DEST/"

echo
echo "Wrote to $DEST:"
ls -la "$DEST" | tail -n +2
echo
echo "Icon classes available: $(grep -c '^\.ri-' "$DEST/remixicon.css")"
echo
echo "Remember: THIRD-PARTY-NOTICES.md and the docs state the version. Update them if it changed."

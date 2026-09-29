#!/usr/bin/env bash
#
# Vendors the rich-text editor's engine — Quill — into the package.
#
# Run this to add or update it. The output is committed, so a build never needs network
# access and a consuming app gets the engine from the package, never from a CDN:
#
#   build/vendor-quill.sh            # the pinned version below
#   build/vendor-quill.sh --check    # fail if the committed files differ from a fresh vendor
#
# What it produces in src/Sedna.UI/wwwroot/lib/quill/:
#
#   quill.js                       Quill's own browser build, which bundles the packages below
#   licenses/<package>.txt         every package's own licence, as its licence requires
#   VENDORED.txt                   name, version and integrity of each package, one per line
#
# quill.js is an ES module the editor imports by relative path. How the upstream build is
# brought to that shape, how the tarballs are verified and how licences are checked is
# build/vendor-npm.py's, shared with every surface's engine.
#
# Licensing: Quill, parchment and quill-delta are BSD-3-Clause — quill-delta's package.json
# says MIT, its licence file says BSD-3-Clause, and the file is what ships — fast-diff is
# Apache-2.0, and the rest MIT.
# THIRD-PARTY-NOTICES.md lists them and a test holds that list against VENDORED.txt. The
# bundled packages are pinned at the versions Quill's own release resolved, for their
# licences: their code is inside quill.js, as upstream built it.
#
set -euo pipefail

DEST="src/Sedna.UI/wwwroot/lib/quill"

if [[ ! -d src/Sedna.UI ]]; then
    echo "::error::Run this from the repository root." >&2
    exit 1
fi

MODE="write"
if [[ "${1:-}" == "--check" ]]; then
    MODE="check"
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

cat > "$tmp/manifest.json" <<'JSON'
[
  {
    "name": "quill",
    "version": "2.0.3",
    "integrity": "sha512-xEYQBqfYx/sfb33VJiKnSJp8ehloavImQ2A6564GAbqG55PGw1dAWUn1MUbQB62t0azawUS2CZZhWCjO8gRvTw==",
    "licence": "BSD-3-Clause",
    "licenceFile": "LICENSE",
    "files": [ { "path": "dist/quill.js", "target": "quill.js", "format": "umd" } ]
  },
  {
    "name": "parchment",
    "version": "3.0.0",
    "integrity": "sha512-HUrJFQ/StvgmXRcQ1ftY6VEZUq3jA2t9ncFN4F84J/vN0/FPpQF+8FKXb3l6fLces6q0uOHj6NJn+2xvZnxO6A==",
    "licence": "BSD-3-Clause",
    "licenceFile": "LICENSE"
  },
  {
    "name": "quill-delta",
    "version": "5.1.0",
    "integrity": "sha512-X74oCeRI4/p0ucjb5Ma8adTXd9Scumz367kkMK5V/IatcX6A0vlgLgKbzXWy5nZmCGeNJm2oQX0d2Eqj+ZIlCA==",
    "licence": "BSD-3-Clause",
    "licenceFile": "LICENSE"
  },
  {
    "name": "eventemitter3",
    "version": "5.0.1",
    "integrity": "sha512-GWkBvjiSZK87ELrYOSESUYeVIc9mvLLf/nXalMOS5dYrgZq9o5OVkbZAVM06CVxYsCwH9BDZFPlQTlPA1j4ahA==",
    "licence": "MIT",
    "licenceFile": "LICENSE"
  },
  {
    "name": "lodash-es",
    "version": "4.17.21",
    "integrity": "sha512-mKnC+QJ9pWVzv+C4/U3rRsHapFfHvQFoFB92e52xeyGMcX6/OlIl78je1u8vePzYZSkkogMPJ2yjxxsb89cxyw==",
    "licence": "MIT",
    "licenceFile": "LICENSE"
  },
  {
    "name": "fast-diff",
    "version": "1.3.0",
    "integrity": "sha512-VxPP4NqbUjj6MaAOafWeUn2cXWLcCtljklUtZf0Ind4XQ+QPtmA0b18zZy0jIQx+ExRVCR/ZQpBmik5lXshNsw==",
    "licence": "Apache-2.0",
    "licenceFile": "LICENSE"
  },
  {
    "name": "lodash.clonedeep",
    "version": "4.5.0",
    "integrity": "sha512-H5ZhCF25riFd9uB5UCkVKo61m3S/xZk1x4wA6yp/L3RFP6Z/eHH1ymQcGLo7J3GMPfm0V/7m1tryHuGVxpqEBQ==",
    "licence": "MIT",
    "licenceFile": "LICENSE"
  },
  {
    "name": "lodash.isequal",
    "version": "4.5.0",
    "integrity": "sha512-pDo3lu8Jhfjqls6GkMgpahsF9kCyayhgykjyLMNFTKWrpVdAQtYyB4muAMWozBB4ig/dtWAmsMxLEI8wuz+DYQ==",
    "licence": "MIT",
    "licenceFile": "LICENSE"
  }
]
JSON

out="$tmp/out"
mkdir -p "$out"
python "$(dirname "${BASH_SOURCE[0]}")/vendor-npm.py" "$tmp/manifest.json" "$out" "build/vendor-quill.sh"

if [[ "$MODE" == "check" ]]; then
    if diff -r "$out" "$DEST" >/dev/null; then
        echo "ok      the vendored editor engine matches the pinned packages"
        exit 0
    fi
    echo "::error::$DEST differs from a fresh vendor. Run build/vendor-quill.sh"
    diff -rq "$out" "$DEST" | head -20 || true
    exit 1
fi

rm -rf "$DEST"
mkdir -p "$DEST"
cp -r "$out"/. "$DEST"/

echo
echo "Wrote to $DEST:"
ls -la "$DEST" | tail -n +2
echo
echo "Remember: THIRD-PARTY-NOTICES.md lists these packages and versions. Update it if one changed."

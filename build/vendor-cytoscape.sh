#!/usr/bin/env bash
#
# Vendors the graph engine — cytoscape.js and the plugins the graph loads on demand —
# into the package.
#
# Run this to add or update them. The output is committed, so a build never needs
# network access and a consuming app gets the engine from the package, never from a
# CDN:
#
#   build/vendor-cytoscape.sh            # the pinned versions below
#   build/vendor-cytoscape.sh --check    # fail if the committed files differ from a fresh vendor
#
# What it produces in src/Sedna.UI/wwwroot/lib/cytoscape/:
#
#   cytoscape.js                   the engine, upstream's minified ES module
#   cytoscape-dagre.js             hierarchical layout (dagre and graphlib are bundled inside it upstream)
#   cytoscape-fcose.js             the fast compound spring layout, and what it stands on:
#   cose-base.js, layout-base.js
#   cytoscape-edgehandles.js       drawing an edge by dragging, and its two helpers:
#   lodash-memoize.js, lodash-throttle.js
#   cytoscape-expand-collapse.js   collapsible groups
#   cytoscape-bubblesets.js        soft outlines around a group, and what it stands on:
#   bubblesets.js, cytoscape-layers.js
#   licenses/<package>.txt         every package's own licence, as its licence requires
#   VENDORED.txt                   name, version and integrity of each package, one per line
#
# Every file is an ES module the graph imports by relative path. How each upstream format is
# brought to that shape, how the tarballs are verified and how licences are checked is
# build/vendor-npm.py's, shared with every surface's engine.
#
# Licensing: every package is MIT. THIRD-PARTY-NOTICES.md lists them and a test holds
# that list against VENDORED.txt. cytoscape-svg is deliberately absent — it is GPL-3.0,
# which an Apache-2.0 package cannot carry; the graph's SVG export is Sedna.UI's own.
#
set -euo pipefail

DEST="src/Sedna.UI/wwwroot/lib/cytoscape"

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

# name, version, pinned integrity, licence and its file in the tarball, files to produce. A
# file is its path in the tarball, the name written, its format, and the bare imports it
# makes, each pointed at the file beside it. A package with no files is carried for its
# licence alone: it is bundled inside another. build/vendor-npm.py does the rest.
cat > "$tmp/manifest.json" <<'JSON'
[
  {
    "name": "cytoscape",
    "version": "3.34.3",
    "integrity": "sha512-yfYGhRcGAntq6YBD583j4n0Eg3jIxvWmZtz/5uz9UYkeIStSlMxuUja+ec5j3iBD8nv1rwaOAYMW09tBdkSeaQ==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "dist/cytoscape.esm.min.mjs",
        "target": "cytoscape.js",
        "format": "esm"
      }
    ]
  },
  {
    "name": "cytoscape-dagre",
    "version": "4.0.1",
    "integrity": "sha512-hyNdh8Vp1nwzBcjHnM4c71G81bFhPmQYLXD2HM9AZIvl+8TrMzWEOPW45hF/XOBQiEguPcDQzi6VrXR1XBcXOA==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "dist/cytoscape-dagre.min.mjs",
        "target": "cytoscape-dagre.js",
        "format": "esm"
      }
    ]
  },
  {
    "name": "@dagrejs/dagre",
    "version": "3.0.0",
    "integrity": "sha512-ZzhnTy1rfuoew9Ez3EIw4L2znPGnYYhfn8vc9c4oB8iw6QAsszbiU0vRhlxWPFnmmNSFAkrYeF1PhM5m4lAN0Q==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": []
  },
  {
    "name": "@dagrejs/graphlib",
    "version": "4.0.1",
    "integrity": "sha512-IvcV6FduIIAmLwnH+yun+QtV36SC7mERqa86aClNqmMN09WhmPPYU8ckHrZBozErf+UvHPWOTJYaGYiIcs0DgA==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": []
  },
  {
    "name": "layout-base",
    "version": "2.0.1",
    "integrity": "sha512-dp3s92+uNI1hWIpPGH3jK2kxE2lMjdXdr+DH8ynZHpd6PUlH6x6cbuXnoMmiNumznqaNO31xu9e79F0uuZ0JFg==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "layout-base.js",
        "target": "layout-base.js",
        "format": "umd"
      }
    ]
  },
  {
    "name": "cose-base",
    "version": "2.2.0",
    "integrity": "sha512-AzlgcsCbUMymkADOJtQm3wO9S3ltPfYOFD5033keQn9NJzIbtnZj+UdBJe7DYml/8TdbtHJW3j58SOnKhWY/5g==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "cose-base.js",
        "target": "cose-base.js",
        "format": "umd",
        "deps": {
          "layout-base": "./layout-base.js"
        }
      }
    ]
  },
  {
    "name": "cytoscape-fcose",
    "version": "2.2.0",
    "integrity": "sha512-ki1/VuRIHFCzxWNrsshHYPs6L7TvLu3DL+TyIGEsRcvVERmxokbf5Gdk7mFxZnTdiGtnA4cfSmjZJMviqSuZrQ==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "cytoscape-fcose.js",
        "target": "cytoscape-fcose.js",
        "format": "umd",
        "deps": {
          "cose-base": "./cose-base.js"
        }
      }
    ]
  },
  {
    "name": "lodash.memoize",
    "version": "4.1.2",
    "integrity": "sha512-t7j+NzmgnQzTAYXcsHYLgimltOV1MXHtlOWf6GjL9Kj8GK5FInw5JotxvbOs+IvV1/Dzo04/fCGfLVs7aXb4Ag==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "index.js",
        "target": "lodash-memoize.js",
        "format": "cjs"
      }
    ]
  },
  {
    "name": "lodash.throttle",
    "version": "4.1.1",
    "integrity": "sha512-wIkUCfVKpVsWo3JSZlc+8MB5it+2AN5W8J7YVMST30UrvcQNZ1Okbj+rbVniijTWE6FGYy4XJq/rHkas8qJMLQ==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "index.js",
        "target": "lodash-throttle.js",
        "format": "cjs"
      }
    ]
  },
  {
    "name": "cytoscape-edgehandles",
    "version": "4.0.1",
    "integrity": "sha512-uSYshkqRZ4luCxK295bEVTg46q4ZW+fwJhcIzMrtfNR7zeAnJ38Z48kUGeu5ibtXkgLbcZAg0YE4ED2dRuaePg==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "cytoscape-edgehandles.js",
        "target": "cytoscape-edgehandles.js",
        "format": "umd",
        "deps": {
          "lodash.memoize": "./lodash-memoize.js",
          "lodash.throttle": "./lodash-throttle.js"
        }
      }
    ]
  },
  {
    "name": "cytoscape-expand-collapse",
    "version": "4.1.1",
    "integrity": "sha512-MI4/GsA6Rf6RRzNR1aCitBLSnxiIKLxvZyCzF+oti/zn/ui1jmf769VcEFAEbjjsAtwteGsTmczI+niCMWJNvA==",
    "licence": "MIT",
    "licenceFile": "LICENSE.md",
    "files": [
      {
        "path": "cytoscape-expand-collapse.js",
        "target": "cytoscape-expand-collapse.js",
        "format": "umd"
      }
    ]
  },
  {
    "name": "bubblesets-js",
    "version": "3.0.1",
    "integrity": "sha512-EKPfysvIU5+u5RLW3mOr94wxzA3nKzqMBX0F95L95BPBDZPVgLBUnT0kJNz4UK/TXbGs8G7yEgl5MvibRBCQoQ==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "build/index.js",
        "target": "bubblesets.js",
        "format": "esm"
      }
    ]
  },
  {
    "name": "cytoscape-layers",
    "version": "3.1.0",
    "integrity": "sha512-HhldyRPURRn4axeDsCiKdKqiZN8bcoWjclLXl3kWekDd7Wy4QHo5cntm4W4kxL/R0u8AA9VAfmNB1P7nJ9scew==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "build/index.js",
        "target": "cytoscape-layers.js",
        "format": "esm"
      }
    ]
  },
  {
    "name": "cytoscape-bubblesets",
    "version": "4.1.0",
    "integrity": "sha512-5ms7vbsYXWI8HSZXvEEgT1eyIQbdkMXwGCF9ZEEl3awkpcO8CG3/DAeqa5udP+6lwb5GwFf5EC5E7iat7o8CJQ==",
    "licence": "MIT",
    "licenceFile": "LICENSE",
    "files": [
      {
        "path": "build/index.js",
        "target": "cytoscape-bubblesets.js",
        "format": "esm",
        "deps": {
          "bubblesets-js": "./bubblesets.js",
          "cytoscape-layers": "./cytoscape-layers.js",
          "lodash.throttle": "./lodash-throttle.js"
        }
      }
    ]
  }
]
JSON

out="$tmp/out"
mkdir -p "$out"
python "$(dirname "${BASH_SOURCE[0]}")/vendor-npm.py" "$tmp/manifest.json" "$out" "build/vendor-cytoscape.sh"

if [[ "$MODE" == "check" ]]; then
    if diff -r "$out" "$DEST" >/dev/null; then
        echo "ok      the vendored graph engine matches the pinned packages"
        exit 0
    fi
    echo "::error::$DEST differs from a fresh vendor. Run build/vendor-cytoscape.sh"
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

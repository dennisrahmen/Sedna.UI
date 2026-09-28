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
# Every file is an ES module the graph imports by relative path, so nothing here needs
# an import map, a bundler or a global. Upstream ships three formats and each is
# brought to that one shape:
#
#   * an ES module is copied, its bare imports ("lodash.throttle") pointed at the file
#     beside it, and its source-map comment dropped (the maps do not ship);
#   * a UMD or CommonJS build is wrapped: a module-scoped `module`, `exports` and
#     `require` are declared, the original text runs unchanged inside them, and
#     `module.exports` is the default export.
#
# Nothing is minified, re-formatted or otherwise edited: what ships is upstream's text
# with a provenance header above it, and its line endings as LF. Each tarball's sha512 is pinned below and checked
# before anything is written, so a registry that served different bytes for the same
# version would stop this script rather than change the package.
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

python - "$tmp" <<'PY'
import base64, hashlib, io, json, os, re, sys, tarfile, urllib.request

out = sys.argv[1]

# name, version, pinned integrity, licence file in the tarball, files to produce.
# A file is (path in the tarball, name written, format, {bare import: file beside it}).
# A package with no files is carried for its licence alone: it is bundled inside another.
PACKAGES = [
    ("cytoscape", "3.34.3",
     "sha512-yfYGhRcGAntq6YBD583j4n0Eg3jIxvWmZtz/5uz9UYkeIStSlMxuUja+ec5j3iBD8nv1rwaOAYMW09tBdkSeaQ==",
     "LICENSE", [("dist/cytoscape.esm.min.mjs", "cytoscape.js", "esm", {})]),
    ("cytoscape-dagre", "4.0.1",
     "sha512-hyNdh8Vp1nwzBcjHnM4c71G81bFhPmQYLXD2HM9AZIvl+8TrMzWEOPW45hF/XOBQiEguPcDQzi6VrXR1XBcXOA==",
     "LICENSE", [("dist/cytoscape-dagre.min.mjs", "cytoscape-dagre.js", "esm", {})]),
    ("@dagrejs/dagre", "3.0.0",
     "sha512-ZzhnTy1rfuoew9Ez3EIw4L2znPGnYYhfn8vc9c4oB8iw6QAsszbiU0vRhlxWPFnmmNSFAkrYeF1PhM5m4lAN0Q==",
     "LICENSE", []),
    ("@dagrejs/graphlib", "4.0.1",
     "sha512-IvcV6FduIIAmLwnH+yun+QtV36SC7mERqa86aClNqmMN09WhmPPYU8ckHrZBozErf+UvHPWOTJYaGYiIcs0DgA==",
     "LICENSE", []),
    ("layout-base", "2.0.1",
     "sha512-dp3s92+uNI1hWIpPGH3jK2kxE2lMjdXdr+DH8ynZHpd6PUlH6x6cbuXnoMmiNumznqaNO31xu9e79F0uuZ0JFg==",
     "LICENSE", [("layout-base.js", "layout-base.js", "umd", {})]),
    ("cose-base", "2.2.0",
     "sha512-AzlgcsCbUMymkADOJtQm3wO9S3ltPfYOFD5033keQn9NJzIbtnZj+UdBJe7DYml/8TdbtHJW3j58SOnKhWY/5g==",
     "LICENSE", [("cose-base.js", "cose-base.js", "umd", {"layout-base": "./layout-base.js"})]),
    ("cytoscape-fcose", "2.2.0",
     "sha512-ki1/VuRIHFCzxWNrsshHYPs6L7TvLu3DL+TyIGEsRcvVERmxokbf5Gdk7mFxZnTdiGtnA4cfSmjZJMviqSuZrQ==",
     "LICENSE", [("cytoscape-fcose.js", "cytoscape-fcose.js", "umd", {"cose-base": "./cose-base.js"})]),
    ("lodash.memoize", "4.1.2",
     "sha512-t7j+NzmgnQzTAYXcsHYLgimltOV1MXHtlOWf6GjL9Kj8GK5FInw5JotxvbOs+IvV1/Dzo04/fCGfLVs7aXb4Ag==",
     "LICENSE", [("index.js", "lodash-memoize.js", "cjs", {})]),
    ("lodash.throttle", "4.1.1",
     "sha512-wIkUCfVKpVsWo3JSZlc+8MB5it+2AN5W8J7YVMST30UrvcQNZ1Okbj+rbVniijTWE6FGYy4XJq/rHkas8qJMLQ==",
     "LICENSE", [("index.js", "lodash-throttle.js", "cjs", {})]),
    ("cytoscape-edgehandles", "4.0.1",
     "sha512-uSYshkqRZ4luCxK295bEVTg46q4ZW+fwJhcIzMrtfNR7zeAnJ38Z48kUGeu5ibtXkgLbcZAg0YE4ED2dRuaePg==",
     "LICENSE", [("cytoscape-edgehandles.js", "cytoscape-edgehandles.js", "umd",
                  {"lodash.memoize": "./lodash-memoize.js", "lodash.throttle": "./lodash-throttle.js"})]),
    ("cytoscape-expand-collapse", "4.1.1",
     "sha512-MI4/GsA6Rf6RRzNR1aCitBLSnxiIKLxvZyCzF+oti/zn/ui1jmf769VcEFAEbjjsAtwteGsTmczI+niCMWJNvA==",
     "LICENSE.md", [("cytoscape-expand-collapse.js", "cytoscape-expand-collapse.js", "umd", {})]),
    ("bubblesets-js", "3.0.1",
     "sha512-EKPfysvIU5+u5RLW3mOr94wxzA3nKzqMBX0F95L95BPBDZPVgLBUnT0kJNz4UK/TXbGs8G7yEgl5MvibRBCQoQ==",
     "LICENSE", [("build/index.js", "bubblesets.js", "esm", {})]),
    ("cytoscape-layers", "3.1.0",
     "sha512-HhldyRPURRn4axeDsCiKdKqiZN8bcoWjclLXl3kWekDd7Wy4QHo5cntm4W4kxL/R0u8AA9VAfmNB1P7nJ9scew==",
     "LICENSE", [("build/index.js", "cytoscape-layers.js", "esm", {})]),
    ("cytoscape-bubblesets", "4.1.0",
     "sha512-5ms7vbsYXWI8HSZXvEEgT1eyIQbdkMXwGCF9ZEEl3awkpcO8CG3/DAeqa5udP+6lwb5GwFf5EC5E7iat7o8CJQ==",
     "LICENSE", [("build/index.js", "cytoscape-bubblesets.js", "esm",
                  {"bubblesets-js": "./bubblesets.js", "cytoscape-layers": "./cytoscape-layers.js",
                   "lodash.throttle": "./lodash-throttle.js"})]),
]

SOURCE_MAP = re.compile(r"^\s*//# sourceMappingURL=.*$", re.M)
BARE_FROM = re.compile(r"""(\bfrom\s*|\bimport\s*\(?\s*)(['"])([^'"./][^'"]*)\2""")


def tarball_url(name, version):
    base = name.split("/")[-1]
    return f"https://registry.npmjs.org/{name}/-/{base}-{version}.tgz"


def fetch(name, version, integrity):
    url = tarball_url(name, version)
    with urllib.request.urlopen(url) as response:
        data = response.read()
    algo, expected = integrity.split("-", 1)
    actual = base64.b64encode(hashlib.new(algo, data).digest()).decode()
    if actual != expected:
        sys.exit(f"::error::{name}@{version}: the registry served bytes whose {algo} is {actual}, "
                 f"not the pinned {expected}. Nothing was written.")
    return url, tarfile.open(fileobj=io.BytesIO(data), mode="r:gz")


def read(tar, path):
    # npm tarballs put everything under one top-level folder, almost always "package/".
    for member in tar.getmembers():
        if member.isfile() and member.name.split("/", 1)[-1] == path:
            # LF throughout: .gitattributes checks every .js and .txt out as LF, so a file
            # written with upstream's CRLF would never match a --check against a checkout.
            return tar.extractfile(member).read().decode("utf-8").replace("\r\n", "\n")
    sys.exit(f"::error::{path} is not in the tarball.")


def header(name, version, url, fmt, deps):
    how = {
        "esm": "Upstream's ES module" + (", its bare imports pointed at the files beside it" if deps else "")
               + ", with its source-map comment removed.",
        "umd": "Upstream's UMD build, unchanged, run inside a module-scoped module/exports/require so it is an ES module.",
        "cjs": "Upstream's CommonJS file, unchanged, run inside a module-scoped module/exports/require so it is an ES module.",
    }[fmt]
    licence = "licenses/" + name.lstrip("@").replace("/", "-") + ".txt"
    return (f"/* {name} {version} — vendored into Sedna.UI by build/vendor-cytoscape.sh.\n"
            f"   Source: {url} (sha512 pinned and verified).\n"
            f"   {how}\n"
            f"   Licence: MIT — see {licence}. */\n")


def esm(text, deps, name):
    text = SOURCE_MAP.sub("", text).rstrip() + "\n"

    def point(m):
        spec = m.group(3)
        if spec not in deps:
            sys.exit(f"::error::{name} imports '{spec}', which nothing here provides.")
        return m.group(1) + m.group(2) + deps[spec] + m.group(2)

    return BARE_FROM.sub(point, text)


def wrapped(text, deps):
    text = SOURCE_MAP.sub("", text).rstrip()
    lines = [f"import __dep{i} from '{path}';" for i, path in enumerate(deps.values())]
    cases = " ".join(f"case '{spec}': return __dep{i};" for i, spec in enumerate(deps))
    lines += [
        "const module = { exports: {} };",
        "const exports = module.exports;",
        "const require = (name) => { switch (name) { " + cases
        + (" " if cases else "") + "default: throw new Error('Not vendored: ' + name); } };",
        "(function () {",
        text,
        "}).call(globalThis);",
        "export default module.exports;",
    ]
    return "\n".join(lines) + "\n"


os.makedirs(os.path.join(out, "licenses"), exist_ok=True)
manifest = []
for name, version, integrity, licence_path, files in PACKAGES:
    url, tar = fetch(name, version, integrity)
    licence = read(tar, licence_path)
    if "MIT" not in licence and "Permission is hereby granted, free of charge" not in licence:
        sys.exit(f"::error::{name}@{version}: {licence_path} is not an MIT licence. Stop and review.")
    slug = name.lstrip("@").replace("/", "-")
    with open(os.path.join(out, "licenses", slug + ".txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(licence.rstrip() + "\n")
    written = []
    for path, target, fmt, deps in files:
        body = read(tar, path)
        body = esm(body, deps, name) if fmt == "esm" else wrapped(body, deps)
        leftover = [m.group(3) for m in BARE_FROM.finditer(body) if not m.group(3).startswith("./")]
        if leftover:
            sys.exit(f"::error::{target} still imports {leftover} by bare name.")
        with open(os.path.join(out, target), "w", encoding="utf-8", newline="\n") as f:
            f.write(header(name, version, url, fmt, deps) + body)
        written.append(target)
    manifest.append(f"{name} {version} {integrity} {' '.join(written) if written else '(licence only: bundled in another file)'}")
    print(f"  {name}@{version}  {', '.join(written) or 'licence only'}")

with open(os.path.join(out, "VENDORED.txt"), "w", encoding="utf-8", newline="\n") as f:
    f.write("# Written by build/vendor-cytoscape.sh. name version integrity files\n")
    f.write("\n".join(manifest) + "\n")
PY

if [[ "$MODE" == "check" ]]; then
    if diff -r "$tmp" "$DEST" >/dev/null; then
        echo "ok      the vendored graph engine matches the pinned packages"
        exit 0
    fi
    echo "::error::$DEST differs from a fresh vendor. Run build/vendor-cytoscape.sh"
    diff -rq "$tmp" "$DEST" | head -20 || true
    exit 1
fi

rm -rf "$DEST"
mkdir -p "$DEST"
cp -r "$tmp"/. "$DEST"/

echo
echo "Wrote to $DEST:"
ls -la "$DEST" | tail -n +2
echo
echo "Remember: THIRD-PARTY-NOTICES.md lists these packages and versions. Update it if one changed."

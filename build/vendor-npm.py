"""Vendors npm packages into the package as ES modules, for a tier 3 surface's engine.

Called by the build/vendor-*.sh scripts, each of which pins its packages and passes them in:

    python build/vendor-npm.py <manifest.json> <out dir> <calling script>

The manifest is a JSON list of packages:

    { "name": "cytoscape", "version": "3.34.3", "integrity": "sha512-…",
      "licence": "MIT", "licenceFile": "LICENSE",
      "files": [ { "path": "dist/cytoscape.esm.min.mjs", "target": "cytoscape.js",
                   "format": "esm", "deps": { "bare-name": "./file-beside-it.js" } } ] }

A package with no files is carried for its licence alone: it is bundled inside another.

Every file written is an ES module a surface imports by relative path, so nothing needs an
import map, a bundler or a global. Upstream ships three formats and each is brought to that
one shape:

  * an ES module is copied, its bare imports pointed at the file beside it, and its
    source-map comment dropped (the maps do not ship);
  * a UMD or CommonJS build is wrapped: a module-scoped `module`, `exports` and `require`
    are declared, the original text runs unchanged inside them, and `module.exports` is the
    default export.

Nothing is minified, re-formatted or otherwise edited: what ships is upstream's text with a
provenance header above it, and its line endings as LF. Each tarball's sha512 is checked
before anything is written, so a registry that served different bytes for the same version
stops the script rather than changing the package.

A licence is accepted only if it is the permissive licence the manifest names, and its text
says so; anything copyleft stops the script. The surface's CLAUDE.md rule is "MIT or as
permissive", and these are the three that meet it here.
"""

import base64, hashlib, io, json, os, re, sys, tarfile, urllib.request

LICENCES = {
    "MIT": ["Permission is hereby granted, free of charge"],
    "BSD-3-Clause": ["Redistribution and use in source and binary forms", "Neither the name"],
    "Apache-2.0": ["Apache License", "Version 2.0"],
}
COPYLEFT = re.compile(r"GNU (Lesser |Affero )?General Public License|Mozilla Public License|Eclipse Public License", re.I)

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


def header(script, name, version, url, fmt, deps, licence):
    how = {
        "esm": "Upstream's ES module" + (", its bare imports pointed at the files beside it" if deps else "")
               + ", with its source-map comment removed.",
        "umd": "Upstream's UMD build, unchanged, run inside a module-scoped module/exports/require so it is an ES module.",
        "cjs": "Upstream's CommonJS file, unchanged, run inside a module-scoped module/exports/require so it is an ES module.",
    }[fmt]
    path = "licenses/" + name.lstrip("@").replace("/", "-") + ".txt"
    return (f"/* {name} {version} — vendored into Sedna.UI by {script}.\n"
            f"   Source: {url} (sha512 pinned and verified).\n"
            f"   {how}\n"
            f"   Licence: {licence} — see {path}. */\n")


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


def check_licence(name, version, licence_id, text):
    if COPYLEFT.search(text):
        sys.exit(f"::error::{name}@{version}: its licence is copyleft. Stop and review.")
    signs = LICENCES.get(licence_id)
    if signs is None:
        sys.exit(f"::error::{name}@{version}: {licence_id} is not a licence this script accepts. Stop and review.")
    if not all(s in text for s in signs):
        sys.exit(f"::error::{name}@{version}: its licence file is not the {licence_id} text. Stop and review.")


def main(manifest_path, out, script):
    packages = json.load(open(manifest_path, encoding="utf-8"))
    os.makedirs(os.path.join(out, "licenses"), exist_ok=True)
    manifest = []
    for p in packages:
        name, version, integrity = p["name"], p["version"], p["integrity"]
        url, tar = fetch(name, version, integrity)
        licence = read(tar, p["licenceFile"])
        check_licence(name, version, p["licence"], licence)
        slug = name.lstrip("@").replace("/", "-")
        with open(os.path.join(out, "licenses", slug + ".txt"), "w", encoding="utf-8", newline="\n") as f:
            f.write(licence.rstrip() + "\n")
        written = []
        for file in p.get("files", []):
            deps = file.get("deps", {})
            body = read(tar, file["path"])
            body = esm(body, deps, name) if file["format"] == "esm" else wrapped(body, deps)
            leftover = [m.group(3) for m in BARE_FROM.finditer(body) if not m.group(3).startswith("./")]
            if leftover:
                sys.exit(f"::error::{file['target']} still imports {leftover} by bare name.")
            with open(os.path.join(out, file["target"]), "w", encoding="utf-8", newline="\n") as f:
                f.write(header(script, name, version, url, file["format"], deps, p["licence"]) + body)
            written.append(file["target"])
        manifest.append(f"{name} {version} {integrity} {' '.join(written) if written else '(licence only: bundled in another file)'}")
        print(f"  {name}@{version}  {', '.join(written) or 'licence only'}")

    with open(os.path.join(out, "VENDORED.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write(f"# Written by {script}. name version integrity files\n")
        f.write("\n".join(manifest) + "\n")


if __name__ == "__main__":
    main(*sys.argv[1:4])

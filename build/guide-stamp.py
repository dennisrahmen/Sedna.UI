#!/usr/bin/env python3
"""Stamp the design guide's entries with the sources that define each page.

    python3 -I build/guide-stamp.py <repo root> --check
    python3 -I build/guide-stamp.py <repo root> --all
    python3 -I build/guide-stamp.py <repo root> /table [/stat …]
    python3 -I build/guide-stamp.py <repo root> --sources /table

Called through build/guide-stamp.sh, which says what each mode does. This file is the one
implementation of "what defines a page" for the guide; GuideStampTests runs --check and
nothing else computes a stamp.

A page's sources are, in this order:

  * its own .razor under Components/Pages/, found by its @page directive;
  * every example it renders — a CatExample's TExample, a CatSnippet's Name, a Companion —
    and every file under Examples/<the page's file name>/;
  * for each class the page's own source names as `.name` and the stylesheet declares, the
    css part that introduces it — the first in cascade order to name it — and the part that
    names it most (`build/css-inventory.sh --tally`; a tie goes to the earlier part). The classes a page names are the ones it teaches; the classes its examples
    merely use — a button in a table's toolbar — are taught by their own pages, and counting
    them would put every part under every page;
  * every script part under src/Sedna.UI/*-parts/ whose code, comments removed by
    build/strip-comments.py, names one of those classes as a selector or one of the data-*
    attributes the page's source names.

Each source is hashed with its line endings normalised to LF and any byte-order mark
dropped, so a checkout on any OS stamps the same. The stamp is the first 16 hex digits of
the SHA-256 of every "path hash" line, sorted. docs/design-guide/stamps/<group>.json keeps
each source's own hash beside it, so a stale stamp can name what changed.
"""
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys

GUIDE = "docs/design-guide"
PAGES = "src/Sedna.UI.Catalogue/Components/Pages"
EXAMPLES = "src/Sedna.UI.Catalogue/Examples"
CSS_PARTS = "src/Sedna.UI/css-parts"
LIBRARY = "src/Sedna.UI"

PAGE_DIRECTIVE = re.compile(r'^@page\s+"([^"]+)"', re.M)
EXAMPLE_TAG = re.compile(r"<Cat(?:Example|Snippet)\s+([^>]*?)/?>", re.S)
ATTRIBUTE = re.compile(r'(\w+)="([^"]*)"')
# The same shape CatalogueIndex reads a class named in prose with.
NAMED_CLASS = re.compile(r"(?<![\w-])\.(-?[a-zA-Z][a-zA-Z0-9-]*)")
DATA_ATTRIBUTE = re.compile(r"(?<![\w-])data-[a-z][a-z0-9-]*")
STAMP = re.compile(r'"stamp"\s*:\s*"[^"]*"')


def read(root, path):
    with open(os.path.join(root, path), "rb") as f:
        data = f.read()
    text = data.decode("utf-8-sig")
    return text.replace("\r\n", "\n").replace("\r", "\n")


def digest(text):
    return hashlib.sha256(text.encode("utf-8")).hexdigest()


def rel(root, path):
    return os.path.relpath(path, root).replace(os.sep, "/")


def files_under(root, directory, suffix=""):
    out = []
    for base, _, names in os.walk(os.path.join(root, directory)):
        for name in names:
            if name.endswith(suffix):
                out.append(rel(root, os.path.join(base, name)))
    return sorted(out)


class Sources:
    """Everything the computation reads, read once."""

    def __init__(self, root):
        self.root = root
        self.pages = {}
        for path in files_under(root, PAGES, ".razor"):
            for route in PAGE_DIRECTIVE.findall(read(root, path)):
                self.pages[route] = path

        tally = {}
        self.parts = files_under(root, CSS_PARTS, ".css")
        for part in self.parts:
            out = subprocess.run(
                ["bash", os.path.join(root, "build", "css-inventory.sh"), "--tally",
                 os.path.join(root, part), "classes"],
                check=True, capture_output=True, text=True).stdout
            for line in out.splitlines():
                count, name = line.split()
                tally.setdefault(name, []).append((int(count), part))
        # The part that introduces a class — the first in cascade order to name it — and the
        # part that names it most, on a tie the earlier, so the answer does not depend on the
        # order a directory lists in. The first is where it is defined; the second is where
        # most of its rules are, which for a class a later part dresses (a tone, a state) is
        # not the same file.
        self.home = {name: {min(part for _, part in seen),
                            sorted(seen, key=lambda s: (-s[0], s[1]))[0][1]}
                     for name, seen in tally.items()}

        strip = load_strip_js(root)
        self.scripts = {}
        for entry in sorted(os.listdir(os.path.join(root, LIBRARY))):
            if entry.endswith("-parts") and entry != "css-parts":
                for path in files_under(root, f"{LIBRARY}/{entry}", ".js"):
                    self.scripts[path] = strip(read(root, path))

    def of(self, route):
        """The sorted source paths that define one page."""
        page = self.pages.get(route)
        if page is None:
            raise SystemExit(f"::error::No page under {PAGES} has @page \"{route}\".")
        text = read(self.root, page)
        found = {page}

        stem = os.path.splitext(os.path.basename(page))[0]
        found.update(files_under(self.root, f"{EXAMPLES}/{stem}"))
        for match in EXAMPLE_TAG.finditer(text):
            attrs = dict(ATTRIBUTE.findall(match.group(1)))
            if "TExample" in attrs and attrs["TExample"].startswith("Examples."):
                folder, name = attrs["TExample"].split(".")[1:3]
                found.add(f"{EXAMPLES}/{folder}/{name}.razor")
            for key in ("Name", "Companion"):
                if key in attrs:
                    found.add(f"{EXAMPLES}/{attrs[key]}")

        classes = {c for c in NAMED_CLASS.findall(text) if c in self.home}
        for c in classes:
            found.update(self.home[c])

        alternatives = [r"(?<![\w-])" + re.escape(a) + r"(?![\w-])" for a in sorted(set(DATA_ATTRIBUTE.findall(text)))]
        alternatives += [r"(?<![\w$\])])\." + re.escape(c) + r"(?![\w-])" for c in sorted(classes)]
        if alternatives:
            named = re.compile("|".join(alternatives))
            found.update(path for path, code in self.scripts.items() if named.search(code))

        missing = sorted(p for p in found if not os.path.isfile(os.path.join(self.root, p)))
        if missing:
            raise SystemExit(f"::error::{route} renders examples that do not exist: {', '.join(missing)}")
        return sorted(found)

    def hashes(self, route):
        return {path: digest(read(self.root, path))[:16] for path in self.of(route)}


def stamp_of(hashes):
    lines = "".join(f"{path} {h}\n" for path, h in sorted(hashes.items()))
    return digest(lines)[:16]


def load_strip_js(root):
    # Imported by path, and without writing a __pycache__ into build/.
    sys.dont_write_bytecode = True
    spec = importlib.util.spec_from_file_location("strip_comments", os.path.join(root, "build", "strip-comments.py"))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module.strip_js


def entries(text, path):
    """Each top-level entry of a components file, with the span of its text."""
    decoder = json.JSONDecoder()
    at = text.index("[") + 1
    out = []
    while True:
        while at < len(text) and text[at] in " \t\n,":
            at += 1
        if at >= len(text) or text[at] == "]":
            return out
        try:
            value, end = decoder.raw_decode(text, at)
        except json.JSONDecodeError as e:
            raise SystemExit(f"::error::{path} is not valid JSON: {e}")
        out.append((value, at, end))
        at = end


def component_files(root):
    directory = os.path.join(root, GUIDE, "components")
    return sorted(rel(root, os.path.join(directory, n)) for n in os.listdir(directory) if n.endswith(".json"))


def sidecar_path(component_file):
    return component_file.replace("/components/", "/stamps/")


def load_sidecar(root, path):
    full = os.path.join(root, path)
    if not os.path.isfile(full):
        return {}
    with open(full, encoding="utf-8") as f:
        return json.load(f)


def write_sidecar(root, path, data):
    full = os.path.join(root, path)
    os.makedirs(os.path.dirname(full), exist_ok=True)
    with open(full, "w", encoding="utf-8", newline="\n") as f:
        f.write("{\n  \"$comment\": \"GENERATED by build/guide-stamp.sh: the sources each entry was last reviewed against. Do not edit.\",\n")
        routes = [r for r in sorted(data) if r != "$comment"]
        for i, route in enumerate(routes):
            f.write(f"  {json.dumps(route)}: {{\n")
            items = sorted(data[route].items())
            for j, (source, h) in enumerate(items):
                f.write(f"    {json.dumps(source)}: {json.dumps(h)}{',' if j < len(items) - 1 else ''}\n")
            f.write("  }" + ("," if i < len(routes) - 1 else "") + "\n")
        f.write("}\n")


def check(root, sources):
    stale = 0
    checked = 0
    for path in component_files(root):
        text = read(root, path)
        recorded = load_sidecar(root, sidecar_path(path))
        for entry, _, _ in entries(text, path):
            route = entry.get("page", "")
            checked += 1
            current = sources.hashes(route)
            if entry.get("stamp") == stamp_of(current) and recorded.get(route) == current:
                continue
            stale += 1
            before = recorded.get(route, {})
            print(f"::error::The design guide's entry for {route} in {path} has not been reviewed "
                  "against the page as it is now.")
            if not entry.get("stamp"):
                print("  It has never been stamped.")
            elif not before:
                print(f"  {sidecar_path(path)} has no record of its sources; every one is listed.")
            for source in sorted(set(current) | set(before)):
                if source not in before and before:
                    print(f"  added    {source}")
                elif source not in current:
                    print(f"  removed  {source}")
                elif not before:
                    print(f"  source   {source}")
                elif before[source] != current[source]:
                    print(f"  changed  {source}")
            print("  Read the page and its examples, correct useFor, combineWith, notFor and goals where "
                  "they no longer hold, then run:")
            print(f"    build/guide-stamp.sh {route}")
    if stale:
        print(f"{stale} of {checked} design guide entries need reviewing.")
        return 1
    print(f"ok      every design guide entry is stamped against its sources ({checked} checked)")
    return 0


def restamp(root, sources, routes):
    """Rewrites the stamp of the given routes, or of every entry when routes is None."""
    wanted = None if routes is None else set(routes)
    done = set()
    for path in component_files(root):
        text = read(root, path)
        sidecar = sidecar_path(path)
        recorded = load_sidecar(root, sidecar)
        recorded.pop("$comment", None)
        spans = entries(text, path)
        known = {e.get("page") for e, _, _ in spans}
        for route in list(recorded):
            if route not in known:
                del recorded[route]
        # Back to front, so an edit cannot move a span not yet edited.
        for entry, start, end in reversed(spans):
            route = entry.get("page", "")
            if wanted is not None and route not in wanted:
                continue
            hashes = sources.hashes(route)
            block = text[start:end]
            stamps = list(STAMP.finditer(block))
            if not stamps:
                raise SystemExit(f"::error::The entry for {route} in {path} has no \"stamp\" key. "
                                 "End the entry with \"stamp\": \"\" and run this again.")
            last = stamps[-1]
            block = block[:last.start()] + f'"stamp": "{stamp_of(hashes)}"' + block[last.end():]
            text = text[:start] + block + text[end:]
            recorded[route] = hashes
            done.add(route)
            print(f"stamped {route}  ({len(hashes)} sources)")
        with open(os.path.join(root, path), "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        write_sidecar(root, sidecar, recorded)
    if wanted is not None and wanted - done:
        raise SystemExit("::error::No entry in docs/design-guide/components/ for: "
                         + ", ".join(sorted(wanted - done)))
    return 0


def main(argv):
    if len(argv) < 3:
        raise SystemExit(__doc__)
    root, args = argv[1], argv[2:]
    sources = Sources(root)
    if args == ["--check"]:
        return check(root, sources)
    if args == ["--all"]:
        return restamp(root, sources, None)
    if args[0] == "--sources":
        for route in args[1:]:
            for path, h in sorted(sources.hashes(route).items()):
                print(f"{route}  {h}  {path}")
        return 0
    if all(a.startswith("/") for a in args):
        return restamp(root, sources, args)
    raise SystemExit(__doc__)


if __name__ == "__main__":
    sys.exit(main(sys.argv))

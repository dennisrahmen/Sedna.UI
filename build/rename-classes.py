#!/usr/bin/env python3
"""Apply the class renames between two Sedna.UI releases to an app's own files.

    python3 -I rename-classes.py --from 0.27.0 [--to 0.28.0] [--dry-run] [--maps DIR] PATH...

Reads every rename map (docs/renames/<version>.json) whose release is newer than --from and
not newer than --to (default: the newest map), in release order, and rewrites each old class
in the .razor, .cshtml, .html, .htm and .css files under each PATH. A directory is walked,
skipping bin/, obj/, node_modules/ and wwwroot/lib/.

  * A class is replaced whole, never as a prefix: btn-warn does not touch btn-warn-solid or
    my-btn-warn.
  * One old class can become two. In markup they are written as two tokens (class="btn btn--go
    btn--solid"); in a stylesheet as one compound selector (.btn--go.btn--solid), because both
    sit on the same element. A token already on the element is not written twice.
  * In a .css file only a selector (.old) is rewritten; in markup a whole token anywhere, and a
    selector inside a <style> block.

--dry-run writes nothing and prints a unified diff of what would change. Without it the files
are rewritten in place. Either way the last line says how many classes changed in how many files.

The maps are found beside this script: tooling/renames/ in the NuGet package, docs/renames/ in
the repository. --maps names another directory. Classes a release removed without a rename are
not in a map and are left alone; the build's SEDNAUI002 warning names them.
"""
import argparse
import difflib
import json
import os
import re
import sys

EXTENSIONS = (".razor", ".cshtml", ".html", ".htm", ".css")
SKIP_DIRS = {"bin", "obj", "node_modules", ".git"}
VERSION = re.compile(r"^\d+\.\d+\.\d+$")
CLASS_ATTR = re.compile(r"""(\bclass\s*=\s*)(["'])([^"'@]*)\2""")


def version_key(text):
    return tuple(int(p) for p in text.split("."))


def default_maps():
    here = os.path.dirname(os.path.abspath(__file__))
    for candidate in (os.path.join(here, "renames"), os.path.join(here, "..", "docs", "renames")):
        if os.path.isdir(candidate):
            return os.path.normpath(candidate)
    return None


def load_maps(directory, start, end):
    maps = []
    for name in sorted(os.listdir(directory)):
        if not name.endswith(".json"):
            continue
        with open(os.path.join(directory, name), encoding="utf-8") as f:
            doc = json.load(f)
        release = doc["release"]
        if version_key(start) < version_key(release) <= version_key(end):
            maps.append((release, {r["old"]: list(r["new"]) for r in doc["renames"]}))
    return sorted(maps, key=lambda m: version_key(m[0]))


def compile_map(renames):
    names = "|".join(re.escape(n) for n in sorted(renames, key=len, reverse=True))
    return re.compile(r"(?<![\w-])(\.?)(" + names + r")(?![\w-])")


def dedupe(line):
    def one(m):
        seen, tokens = set(), []
        for token in m.group(3).split():
            if token not in seen:
                seen.add(token)
                tokens.append(token)
        return m.group(1) + m.group(2) + " ".join(tokens) + m.group(2)
    return CLASS_ATTR.sub(one, line)


def rewrite(text, is_css, compiled):
    count = 0
    out = []
    for line in text.splitlines(keepends=True):
        original = line
        for pattern, renames in compiled:
            def replace(m):
                nonlocal count
                dot, old = m.group(1), m.group(2)
                if is_css and not dot:
                    return m.group(0)
                count += 1
                new = renames[old]
                return "." + ".".join(new) if dot else " ".join(new)
            line = pattern.sub(replace, line)
        if line != original and not is_css:
            line = dedupe(line)
        out.append(line)
    return "".join(out), count


def files_under(paths):
    for path in paths:
        if os.path.isfile(path):
            yield path
            continue
        for root, dirs, names in os.walk(path):
            dirs[:] = sorted(d for d in dirs if d not in SKIP_DIRS
                             and not (d == "lib" and os.path.basename(root) == "wwwroot"))
            for name in sorted(names):
                if name.lower().endswith(EXTENSIONS):
                    yield os.path.join(root, name)


def main(argv):
    parser = argparse.ArgumentParser(
        description="Apply Sedna.UI's class renames between two releases to an app's files.")
    parser.add_argument("--from", dest="start", required=True, help="the version the app has now, e.g. 0.27.0")
    parser.add_argument("--to", dest="end", help="the version the app moves to (default: the newest map)")
    parser.add_argument("--maps", help="the directory holding the <version>.json rename maps")
    parser.add_argument("--dry-run", action="store_true", help="print a diff and write nothing")
    parser.add_argument("paths", nargs="+", help="files or directories to rewrite")
    args = parser.parse_args(argv)

    maps_dir = args.maps or default_maps()
    if not maps_dir or not os.path.isdir(maps_dir):
        parser.error("no rename maps found; pass --maps <directory>")
    available = sorted((n[:-5] for n in os.listdir(maps_dir) if n.endswith(".json")), key=version_key)
    end = args.end or (available[-1] if available else args.start)
    for label, value in (("--from", args.start), ("--to", end)):
        if not VERSION.match(value):
            parser.error(f"{label} must be a version such as 0.27.0, not {value!r}")
    if version_key(end) < version_key(args.start):
        parser.error(f"--to {end} is older than --from {args.start}")

    maps = load_maps(maps_dir, args.start, end)
    if not maps:
        print(f"No release between {args.start} and {end} renamed a class. Nothing to do.")
        return 0
    compiled = [(compile_map(renames), renames) for _, renames in maps]

    total, changed = 0, 0
    for path in files_under(args.paths):
        with open(path, encoding="utf-8", newline="") as f:
            text = f.read()
        new_text, count = rewrite(text, path.lower().endswith(".css"), compiled)
        if new_text == text:
            continue
        total += count
        changed += 1
        if args.dry_run:
            sys.stdout.writelines(difflib.unified_diff(
                text.splitlines(keepends=True), new_text.splitlines(keepends=True),
                fromfile=path, tofile=path))
        else:
            with open(path, "w", encoding="utf-8", newline="") as f:
                f.write(new_text)

    releases = ", ".join(r for r, _ in maps)
    verb = "would change" if args.dry_run else "changed"
    print(f"{total} class{'es' if total != 1 else ''} {verb} in {changed} file{'s' if changed != 1 else ''} "
          f"(renames of {releases}).")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

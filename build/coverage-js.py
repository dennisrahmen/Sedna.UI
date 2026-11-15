#!/usr/bin/env python3
"""Turns V8 coverage of the served scripts into a Cobertura file over the script's parts.

    python3 -I build/coverage-js.py --out artifacts/coverage/js.cobertura.xml artifacts/coverage/js

Input: the JSON files src/Sedna.UI.Tests/TestSupport/JsCoverage.cs writes (one per test
process; they are merged): per served script, the character offsets V8 reports as executed.

Output: Cobertura, one class per file of js-parts/ (and of each surface's <name>-parts/),
so build/coverage-gate.py reads it like any other report.

The mapping is exact and checked, never guessed. build/bundle-js.sh writes the readable bundle
as the parts' own lines under a one-line marker per part, and build/strip-comments.py makes
the served file from it by dropping whole lines and trimming trailing whitespace. This script
replays that second step while keeping each surviving line's number, and fails unless the replay
equals the served file byte for byte and every mapped line equals its line in the part. If
strip-comments.py changes, this fails instead of mapping wrongly.

A line is executable unless it holds only brackets, commas and semicolons; it is covered when
the first character that is not whitespace or a closing bracket was executed. V8 reports
blocks, not lines, so a line that is part executed counts as covered.
"""

import argparse
import bisect
import hashlib
import json
import os
import re
import sys
import xml.sax.saxutils as xml

PUNCTUATION_ONLY = re.compile(r"^[\s{}()\[\],;]*$")
IIFE_CLOSE = re.compile(r"^\}\)\(window\.sednaUi\);?$")
MARKER = re.compile(r"^/\* ── (\S+\.js) ─+ \*/$")
HEADER = re.compile(r"^/\*! Sedna\.UI — generated, comments stripped\.")


class MappingError(Exception):
    pass


def strip_js_with_lines(text):
    """strip-comments.py's strip_js, returning (kept lines, their 1-based source line numbers)."""
    kept, numbers, in_block = [], [], False
    for number, line in enumerate(text.split("\n"), 1):
        s = line.strip()
        if in_block:
            if "*/" in s:
                in_block = False
            continue
        if s.startswith("/*") and not s.startswith("/*!"):
            if s.endswith("*/") and s.find("*/", 2) == len(s) - 2:
                continue
            if "*/" not in s:
                in_block = True
                continue
        if s.startswith("//"):
            continue
        if s:
            kept.append(line.rstrip())
            numbers.append(number)
    return kept, numbers


def parts_directory(script_name):
    """Sedna.UI.js -> js-parts; Sedna.UI.graph.js -> graph-parts."""
    stem = script_name[len("Sedna.UI."):-len(".js")] if script_name != "Sedna.UI.js" else "js"
    return stem + "-parts"


def map_script(repo, name, covered_runs, recorded_hash):
    base = os.path.join(repo, "src", "Sedna.UI")
    served_path = os.path.join(base, "wwwroot", "js", name)
    readable_path = os.path.join(base, "readable", name)
    with open(served_path, "rb") as handle:
        raw = handle.read()
    if hashlib.sha256(raw).hexdigest() != recorded_hash:
        raise MappingError("%s changed since it was measured; collect again" % served_path)
    served = raw.decode("utf-8")
    if "\r" in served:
        raise MappingError("%s has carriage returns; offsets cannot be mapped" % served_path)
    with open(readable_path, encoding="utf-8") as handle:
        readable = handle.read()

    served_lines = served.split("\n")
    if not HEADER.match(served_lines[0]):
        raise MappingError("%s does not start with the generated header" % served_path)
    kept, numbers = strip_js_with_lines(readable)
    if served_lines[1:] != kept + [""]:
        raise MappingError("%s is not what build/strip-comments.py makes of %s; run build/bundle-js.sh"
                           % (served_path, readable_path))

    readable_lines = readable.split("\n")
    markers = [(i + 1, MARKER.match(l).group(1)) for i, l in enumerate(readable_lines) if MARKER.match(l)]
    if not markers:
        raise MappingError("%s names no parts" % readable_path)
    parts_dir = parts_directory(name)
    part_cache = {}

    def part_of(readable_line):
        index = bisect.bisect_right([m[0] for m in markers], readable_line) - 1
        if index < 0:
            return None
        return markers[index]

    starts = [r[0] for r in covered_runs]
    ends = [r[1] for r in covered_runs]

    def executed(offset):
        i = bisect.bisect_right(starts, offset) - 1
        return i >= 0 and offset < ends[i]

    result = {}  # (part path, part line) -> hits
    offset = len(served_lines[0].encode("utf-16-le")) // 2 + 1
    for text, source_number in zip(kept, numbers):
        line_offset = offset
        offset += len(text.encode("utf-16-le")) // 2 + 1
        marker = part_of(source_number)
        if marker is None or PUNCTUATION_ONLY.match(text) or IIFE_CLOSE.match(text):
            continue
        marker_line, part_name = marker
        part_rel = "src/Sedna.UI/%s/%s" % (parts_dir, part_name)
        if part_rel not in part_cache:
            with open(os.path.join(repo, part_rel), encoding="utf-8") as handle:
                part_cache[part_rel] = handle.read().split("\n")
        part_line = source_number - marker_line
        part_lines = part_cache[part_rel]
        if part_line < 1 or part_line > len(part_lines) or part_lines[part_line - 1].rstrip() != text:
            raise MappingError("%s:%d does not match line %d of the served script" % (part_rel, part_line, source_number))
        probe = len(text) - len(text.lstrip(" \t})]"))
        probe_offset = line_offset + len(text[:probe].encode("utf-16-le")) // 2
        result[(part_rel, part_line)] = 1 if executed(probe_offset) else 0
    return result


def merge_inputs(paths):
    scripts = {}
    files = []
    for argument in paths:
        if os.path.isdir(argument):
            for folder, _, names in os.walk(argument):
                files.extend(os.path.join(folder, n) for n in sorted(names) if n.endswith(".json"))
        elif os.path.isfile(argument):
            files.append(argument)
        else:
            raise MappingError("not found: " + argument)
    if not files:
        raise MappingError("no JavaScript coverage file was given or found: nothing was measured")
    for path in sorted(set(files)):
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
        for script, entry in data["scripts"].items():
            current = scripts.setdefault(script, {"sha256": entry["sha256"], "runs": []})
            if current["sha256"] != entry["sha256"]:
                raise MappingError("%s was measured against two different builds of %s" % (path, script))
            current["runs"].extend(entry["covered"])
    for entry in scripts.values():
        runs, merged = sorted(entry["runs"]), []
        for start, end in runs:
            if merged and start <= merged[-1][1]:
                merged[-1][1] = max(merged[-1][1], end)
            else:
                merged.append([start, end])
        entry["runs"] = merged
    return scripts


def write_cobertura(path, lines):
    by_file = {}
    for (part, number), hits in lines.items():
        by_file.setdefault(part, {})[number] = hits
    covered = sum(1 for h in lines.values() if h)
    out = ['<?xml version="1.0" encoding="utf-8"?>',
           '<coverage line-rate="%.4f" lines-covered="%d" lines-valid="%d" version="1.9"><sources/><packages>'
           '<package name="Sedna.UI.js"><classes>' % (covered / len(lines) if lines else 0, covered, len(lines))]
    for part in sorted(by_file):
        rows = "".join('<line number="%d" hits="%d" branch="False"/>' % (n, by_file[part][n]) for n in sorted(by_file[part]))
        out.append('<class name="%s" filename=%s><methods/><lines>%s</lines></class>'
                   % (xml.escape(os.path.basename(part)), xml.quoteattr(part), rows))
    out.append("</classes></package></packages></coverage>")
    with open(path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(out) + "\n")


def main(argv):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("inputs", nargs="+", help="JSON files written by JsCoverage.cs, or directories of them")
    parser.add_argument("--out", required=True, help="the Cobertura file to write")
    parser.add_argument("--repo", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    args = parser.parse_args(argv)
    repo = os.path.abspath(args.repo)
    try:
        scripts = merge_inputs(args.inputs)
        lines = {}
        # Every bundle there is, so a script no test ever loaded counts as uncovered rather than
        # as absent.
        readable = os.path.join(repo, "src", "Sedna.UI", "readable")
        for name in sorted(n for n in os.listdir(readable) if n.endswith(".js")):
            entry = scripts.get("/js/" + name)
            if entry is None:
                with open(os.path.join(repo, "src", "Sedna.UI", "wwwroot", "js", name), "rb") as handle:
                    entry = {"runs": [], "sha256": hashlib.sha256(handle.read()).hexdigest()}
            lines.update(map_script(repo, name, entry["runs"], entry["sha256"]))
        if not lines:
            raise MappingError("no executable line was mapped: nothing was measured")
    except (MappingError, OSError, ValueError, KeyError) as error:
        print("ERROR: %s" % error, file=sys.stderr)
        return 2
    write_cobertura(args.out, lines)
    covered = sum(1 for h in lines.values() if h)
    print("JavaScript: %d of %d executable lines covered (%.2f%%) across %d script(s)"
          % (covered, len(lines), 100.0 * covered / len(lines), len(scripts)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

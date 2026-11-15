#!/usr/bin/env python3
"""The coverage gate: what a change covers, what the whole library covers, what remains.

    python3 build/coverage-gate.py --base origin/main report.cobertura.xml [more.xml | directory] ...

Reads one or more Cobertura reports, merges them (a line is covered when any report covers
it) and applies three checks, all configured by build/coverage-policy.txt:

  1. the changed-lines gate: of the executable lines `git diff <base>...<head>` adds or
     changes in scope, at least `changed_lines_min` percent are covered. The failure
     lists each uncovered changed line as `path:line`;
  2. the whole-code floor: total line coverage in scope is at least `whole_floor` percent;
  3. the remainder report: the uncovered executable lines per file, written as Markdown
     with --markdown, so the uncovered part is reviewed and not only bounded.

Exit status: 0 passed, 1 a threshold failed, 2 the harness is not trustworthy (no report,
nothing measured in scope, a report that matches no file of this repository, a malformed
policy, an exclusion without a reason). A harness that measured nothing is never a pass.

Standard library only; runs on python 3.8 and later.
"""

import argparse
import fnmatch
import os
import posixpath
import re
import subprocess
import sys
import xml.etree.ElementTree as ET

PASSED, FAILED, ERROR = 0, 1, 2


class HarnessError(Exception):
    """The measurement itself cannot be trusted; reported with exit status 2."""


# ── policy ──────────────────────────────────────────────────────────────────────────────

class Policy:
    def __init__(self):
        self.changed_lines_min = None
        self.whole_floor = None
        self.scope = []  # (directory, [extensions])
        self.excludes = []  # (glob, reason)


def read_policy(path):
    if not os.path.isfile(path):
        raise HarnessError("policy file not found: " + path)
    policy = Policy()
    with open(path, encoding="utf-8") as handle:
        for number, raw in enumerate(handle, 1):
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            key, sep, value = line.partition("=")
            key, value = key.strip(), value.strip()
            where = "%s:%d" % (path, number)
            if not sep:
                raise HarnessError("%s: expected `key = value`" % where)
            if key in ("changed_lines_min", "whole_floor"):
                try:
                    number_value = float(value)
                except ValueError:
                    raise HarnessError("%s: %s is not a number" % (where, key))
                if not 0 <= number_value <= 100:
                    raise HarnessError("%s: %s must be between 0 and 100" % (where, key))
                setattr(policy, key, number_value)
            elif key == "scope":
                words = value.split()
                if len(words) < 2 or not words[0].endswith("/") or not all(w.startswith(".") for w in words[1:]):
                    raise HarnessError("%s: a scope is `scope = <directory>/ <.ext> [<.ext> ...]`" % where)
                policy.scope.append((words[0], words[1:]))
            elif key == "exclude":
                glob, bar, reason = value.partition("|")
                glob, reason = glob.strip(), reason.strip()
                if not bar or not reason:
                    raise HarnessError("%s: an exclusion is `exclude = <path or glob> | <reason>` "
                                       "and the reason is required" % where)
                policy.excludes.append((glob, reason))
            else:
                raise HarnessError("%s: unknown key `%s`" % (where, key))
    for key in ("changed_lines_min", "whole_floor"):
        if getattr(policy, key) is None:
            raise HarnessError("%s: `%s` is not set" % (path, key))
    if not policy.scope:
        raise HarnessError("%s: no `scope` is set" % path)
    return policy


# ── repository ──────────────────────────────────────────────────────────────────────────

def git(root, *args):
    result = subprocess.run(["git", "-C", root, "-c", "core.quotepath=off"] + list(args),
                            stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                            universal_newlines=True, encoding="utf-8")
    if result.returncode != 0:
        raise HarnessError("git %s failed: %s" % (" ".join(args), result.stderr.strip()))
    return result.stdout


def tracked_files(root):
    return set(p for p in git(root, "ls-files", "-z").split("\0") if p)


def in_scope(path, policy, excluded):
    extension = os.path.splitext(path)[1]
    return (any(path.startswith(prefix) and extension in extensions for prefix, extensions in policy.scope)
            and path not in excluded)


def excluded_files(policy, tracked):
    """Files named by an exclusion. A glob that matches nothing is stale and is an error."""
    excluded = {}
    for glob, reason in policy.excludes:
        matched = [p for p in sorted(tracked) if fnmatch.fnmatchcase(p, glob)]
        if not matched:
            raise HarnessError("exclusion `%s` matches no file in the repository; remove it" % glob)
        for path in matched:
            excluded.setdefault(path, reason)
    return excluded


ATTRIBUTE = re.compile(r"ExcludeFromCodeCoverage")
JUSTIFIED = re.compile(r'ExcludeFromCodeCoverage(?:Attribute)?\s*\(\s*Justification\s*=\s*"[^"\s][^"]*"')


def unjustified_attributes(root, paths):
    """`[ExcludeFromCodeCoverage]` is the in-code exclusion; its reason is required."""
    found = []
    for path in sorted(paths):
        if not path.endswith(".cs"):
            continue
        try:
            with open(os.path.join(root, path), encoding="utf-8") as handle:
                for number, text in enumerate(handle, 1):
                    code = text.strip()
                    if code.startswith("//") or code.startswith("using ") or not ATTRIBUTE.search(code):
                        continue
                    if not JUSTIFIED.search(code):
                        found.append("%s:%d" % (path, number))
        except (OSError, UnicodeDecodeError):
            continue
    return found


# ── cobertura ───────────────────────────────────────────────────────────────────────────

def collect_reports(arguments):
    files = []
    for argument in arguments:
        if os.path.isdir(argument):
            for folder, _, names in os.walk(argument):
                files.extend(os.path.join(folder, n) for n in sorted(names) if n.endswith(".xml"))
        elif os.path.isfile(argument):
            files.append(argument)
        else:
            raise HarnessError("report not found: " + argument)
    if not files:
        raise HarnessError("no Cobertura report was given or found: nothing was measured")
    return sorted(set(files))


def resolve(filename, sources, tracked):
    """Map a Cobertura path to a repository-relative one, or None.

    Absolute paths from another machine or a container, relative paths under <sources>, and
    deterministic-build roots such as /_/ all end in the repository path, so the longest
    trailing run of path components that is a tracked file wins.
    """
    candidates = [filename] + [posixpath.join(s.replace("\\", "/"), filename)
                               for s in sources if not os.path.isabs(filename)]
    for candidate in candidates:
        parts = [p for p in posixpath.normpath(candidate.replace("\\", "/")).split("/") if p not in ("", ".")]
        for start in range(len(parts) - 1):
            suffix = "/".join(parts[start:])
            if suffix in tracked:
                return suffix
    return None


def read_report(path, tracked):
    """Return ({repo path: {line: hits}}, [filenames that match no repository file])."""
    lines, unmatched = {}, []
    try:
        sources = []
        for _, element in ET.iterparse(path, events=("end",)):
            tag = element.tag
            if tag == "source":
                if element.text and element.text.strip():
                    sources.append(element.text.strip())
                element.clear()
            elif tag == "class":
                filename = element.get("filename")
                if filename:
                    repo_path = resolve(filename, sources, tracked)
                    if repo_path is None:
                        unmatched.append(filename)
                    else:
                        table = lines.setdefault(repo_path, {})
                        for line in element.iter("line"):
                            number, hits = int(line.get("number")), int(line.get("hits"))
                            if hits > table.get(number, -1):
                                table[number] = hits
                element.clear()
    except (ET.ParseError, ValueError, TypeError) as error:
        raise HarnessError("%s is not a Cobertura report: %s" % (path, error))
    return lines, unmatched


def merge(reports):
    merged = {}
    for lines in reports:
        for path, table in lines.items():
            target = merged.setdefault(path, {})
            for number, hits in table.items():
                if hits > target.get(number, -1):
                    target[number] = hits
    return merged


# ── the diff ────────────────────────────────────────────────────────────────────────────

HUNK = re.compile(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@")


def changed_lines(root, base, head):
    """{path: set of added or changed line numbers} for base...head, no context lines."""
    text = git(root, "diff", "--unified=0", "--no-color", "--no-ext-diff", "-M",
               "%s...%s" % (base, head), "--")
    result, current = {}, None
    for line in text.splitlines():
        if line.startswith("+++ "):
            name = line[4:].split("\t")[0]
            current = name[2:] if name.startswith("b/") else None
        elif line.startswith("@@") and current is not None:
            match = HUNK.match(line)
            if match:
                start = int(match.group(1))
                count = 1 if match.group(2) is None else int(match.group(2))
                result.setdefault(current, set()).update(range(start, start + count))
    return result


# ── reporting ───────────────────────────────────────────────────────────────────────────

def ranges(numbers):
    out, run = [], []
    for number in sorted(numbers):
        if run and number == run[-1] + 1:
            run.append(number)
        else:
            if run:
                out.append(run)
            run = [number]
    if run:
        out.append(run)
    return ", ".join(str(r[0]) if len(r) == 1 else "%d-%d" % (r[0], r[-1]) for r in out)


def percent(covered, total):
    return 100.0 * covered / total if total else 0.0


def write_markdown(path, scoped, totals, changed_result, policy, excluded, absent, markup_only):
    covered, executable = totals
    rows = []
    for name, table in scoped.items():
        uncovered = sorted(n for n, hits in table.items() if hits == 0)
        if uncovered:
            rows.append((name, len(table), len(table) - len(uncovered), uncovered))
    rows.sort(key=lambda r: (-len(r[3]), r[0]))
    out = ["## Coverage", ""]
    if changed_result is None:
        out.append("Changed lines: not checked.")
    else:
        count, hit, uncovered_lines = changed_result
        if count == 0:
            out.append("Changed lines: no executable line in scope changed.")
        else:
            out.append("Changed lines: %d of %d covered (%.1f%%, required %g%%)."
                       % (hit, count, percent(hit, count), policy.changed_lines_min))
            for item in uncovered_lines:
                out.append("- `%s`" % item)
    out += ["", "Whole code: %d of %d executable lines covered (%.1f%%, floor %g%%)."
            % (covered, executable, percent(covered, executable), policy.whole_floor), "",
            "### Uncovered lines per file", ""]
    if rows:
        out += ["| File | Executable | Uncovered | Covered | Uncovered lines |", "|---|--:|--:|--:|---|"]
        for name, count, hit, uncovered in rows:
            out.append("| `%s` | %d | %d | %.1f%% | %s |" % (name, count, len(uncovered), percent(hit, count), ranges(uncovered)))
    else:
        out.append("Every executable line in scope is covered.")
    if absent or markup_only:
        out += ["", "### In scope, in no report", "",
                "Files no report mentions: the assembly was never loaded, or the file has no executable line. "
                "They are not in the figures above.", ""]
        out += ["- `%s`" % a for a in absent]
        if markup_only:
            out.append("- %d file(s) that are not `.cs`, nearly all markup with no executable line" % markup_only)
    if excluded:
        out += ["", "### Excluded files", ""]
        out += ["- `%s`: %s" % (p, r) for p, r in sorted(excluded.items())]
    with open(path, "w", encoding="utf-8") as handle:
        handle.write("\n".join(out) + "\n")


# ── main ────────────────────────────────────────────────────────────────────────────────

def run(args):
    root = os.path.abspath(args.repo)
    policy = read_policy(args.policy if os.path.isabs(args.policy) else os.path.join(root, args.policy))
    if args.changed_min is not None:
        policy.changed_lines_min = args.changed_min
    if args.floor is not None:
        policy.whole_floor = args.floor

    tracked = tracked_files(root)
    excluded = excluded_files(policy, tracked)
    scope_files = set(p for p in tracked if in_scope(p, policy, excluded))

    bad = unjustified_attributes(root, scope_files)
    if bad:
        raise HarnessError("[ExcludeFromCodeCoverage] without a Justification:\n  " + "\n  ".join(bad))

    reports = []
    for report in collect_reports(args.reports):
        lines, unmatched = read_report(report, tracked)
        if not lines:
            raise HarnessError("%s matches no file of this repository%s" % (
                report, " (e.g. %s)" % unmatched[0] if unmatched else ": it holds no classes"))
        if not any(p in scope_files for p in lines):
            raise HarnessError("%s measured no file in scope (%s)" % (report, ", ".join(d for d, _ in policy.scope)))
        reports.append(lines)
    merged = merge(reports)

    scoped = {}
    for path, table in merged.items():
        if path in scope_files and table:
            scoped[path] = table
    executable = sum(len(t) for t in scoped.values())
    covered = sum(1 for t in scoped.values() for h in t.values() if h > 0)
    if executable == 0:
        raise HarnessError("no executable line in scope was measured")
    # A markup-only .razor file has no executable line and is in no report by nature, so only
    # the files that should have one are listed.
    not_reported = sorted(p for p in scope_files if p not in scoped)
    absent = [p for p in not_reported if p.endswith(".cs")]
    markup_only = len(not_reported) - len(absent)

    failures = []
    print("Whole code: %d of %d executable lines covered (%.2f%%); floor %g%%."
          % (covered, executable, percent(covered, executable), policy.whole_floor))
    if covered * 100 < policy.whole_floor * executable:
        failures.append("whole-code coverage %.2f%% is below the floor of %g%%"
                        % (percent(covered, executable), policy.whole_floor))

    changed_result = None
    if not args.skip_changed:
        diff = changed_lines(root, args.base, args.head)
        count, hit, uncovered_lines = 0, 0, []
        for path in sorted(diff):
            table = scoped.get(path)
            if table is None:
                continue
            for number in sorted(diff[path]):
                if number in table:
                    count += 1
                    if table[number] > 0:
                        hit += 1
                    else:
                        uncovered_lines.append("%s:%d" % (path, number))
        changed_result = (count, hit, uncovered_lines)
        if count == 0:
            print("Changed lines: no executable line in scope changed; nothing to cover.")
        else:
            print("Changed lines: %d executable, %d covered (%.1f%%); required %g%%."
                  % (count, hit, percent(hit, count), policy.changed_lines_min))
            if hit * 100 < policy.changed_lines_min * count:
                failures.append("%d of %d changed executable lines are covered (%.1f%%), "
                                "%g%% is required" % (hit, count, percent(hit, count), policy.changed_lines_min))
            if uncovered_lines:
                print("Uncovered changed lines:")
                for item in uncovered_lines:
                    print("  " + item)
    else:
        print("Changed lines: not checked (--skip-changed).")

    if absent:
        print("%d in-scope .cs file(s) appear in no report; see the Markdown report." % len(absent))
    if args.markdown:
        write_markdown(args.markdown, scoped, (covered, executable), changed_result, policy, excluded, absent, markup_only)
    for failure in failures:
        print("FAILED: " + failure, file=sys.stderr)
    return FAILED if failures else PASSED


def main(argv):
    parser = argparse.ArgumentParser(description="Coverage gate; see the module docstring.")
    parser.add_argument("reports", nargs="+", help="Cobertura files, or directories searched for *.xml")
    parser.add_argument("--base", default="origin/main", help="base ref; the diff is base...head")
    parser.add_argument("--head", default="HEAD")
    parser.add_argument("--skip-changed", action="store_true", help="check the floor only (no pull request)")
    parser.add_argument("--policy", default="build/coverage-policy.txt")
    parser.add_argument("--repo", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
    parser.add_argument("--markdown", help="write the Markdown report to this path")
    parser.add_argument("--changed-min", type=float, help="override changed_lines_min (tests only)")
    parser.add_argument("--floor", type=float, help="override whole_floor (tests only)")
    args = parser.parse_args(argv)
    try:
        return run(args)
    except HarnessError as error:
        print("ERROR: %s" % error, file=sys.stderr)
        return ERROR


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))

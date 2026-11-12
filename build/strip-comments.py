#!/usr/bin/env python3
"""Strip comments from a generated stylesheet or script, for the copy that is served.

    python3 -I build/strip-comments.py css < readable.css > served.css
    python3 -I build/strip-comments.py js  < readable.js  > served.js

The readable copy keeps every comment and ships inside the package under readable/;
the served copy is what a browser downloads, and most of the readable one is prose.

CSS: every /* … */ outside a string goes, except /*! … */, which is kept for a notice.
Lines left empty are dropped, so the file keeps its shape and its line breaks.

JS: only what is certainly a comment goes — a line whose text starts with //, and a
/* … */ that starts a line and ends one. A comment after code on the same line stays.
That is deliberately less than a tokenizer would take: a regular expression literal
cannot be told from a division without parsing, and the browser tests run against the
served file, so anything this got wrong would fail them. Empty lines are dropped.
"""
import sys


def strip_css(text: str) -> str:
    out = []
    i, n = 0, len(text)
    while i < n:
        c = text[i]
        if c in "\"'":
            j = i + 1
            while j < n and text[j] != c:
                j += 2 if text[j] == "\\" else 1
            out.append(text[i:j + 1])
            i = j + 1
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            if end < 0:
                raise SystemExit("strip-comments: an unterminated comment in the stylesheet")
            if text.startswith("/*!", i):
                out.append(text[i:end + 2])
            i = end + 2
        else:
            out.append(c)
            i += 1
    lines = "".join(out).split("\n")
    return "\n".join(l.rstrip() for l in lines if l.strip()) + "\n"


def strip_js(text: str) -> str:
    out = []
    in_block = False
    for line in text.split("\n"):
        s = line.strip()
        if in_block:
            if "*/" in s:
                if not s.endswith("*/"):
                    raise SystemExit("strip-comments: code after a whole-line comment closes: " + s)
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
            out.append(line.rstrip())
    if in_block:
        raise SystemExit("strip-comments: an unterminated comment in the script")
    return "\n".join(out) + "\n"


def main() -> None:
    if len(sys.argv) != 2 or sys.argv[1] not in ("css", "js"):
        raise SystemExit("usage: strip-comments.py css|js < in > out")
    text = sys.stdin.read()
    sys.stdout.write(strip_css(text) if sys.argv[1] == "css" else strip_js(text))


# Guarded, so build/guide-stamp.py can import strip_js rather than write a second one.
if __name__ == "__main__":
    main()

#!/usr/bin/env python3
"""Derive each class's variants and states from the shipped stylesheet.

    python3 -I build/class-states.py <Sedna.UI.css> > class-states.json

Called by build/class-states.sh, which owns the paths and --check. The answer is read out
of the selectors, never typed: for every class the sheet declares,

  family         the block it belongs to: the name before its first "--"
  variants       the modifier classes of its family, block--modifier
  attributes     the attribute selectors written on the same compound as the class —
                 [aria-expanded="true"], [data-state="failed"], [open] — and :not() of one
  pseudoClasses  the state pseudo-classes written on the same compound — :hover,
                 :focus-visible, :checked, :disabled — and :not() of one
  combinedWith   the other classes written on the same compound: .btn--go.btn--solid

"The same compound" is what makes it a state of this class rather than of an ancestor:
`.tree-item[aria-expanded="true"] > .tree-toggle` is a state of .tree-item, and
`:root[data-density="compact"] .btn` is no state of .btn at all.

The extraction follows build/css-inventory.sh's two rules: comments are stripped first, and
the @layer preludes are never read as selectors, because their dotted names look like
classes. A class is any name after a dot in a selector, so this lists exactly the classes
css-inventory.sh does.
"""
import json
import re
import sys

CLASS = re.compile(r"\.(-?[A-Za-z_][A-Za-z0-9_-]*)")

# Pseudo-classes that say what state an element is in. Structural ones (:first-child,
# :nth-child) and selector logic (:is, :where, :has) are not states.
STATES = {
    "hover", "focus", "focus-visible", "focus-within", "active", "visited", "link", "target",
    "checked", "indeterminate", "default", "disabled", "enabled", "required", "optional",
    "invalid", "valid", "user-invalid", "user-valid", "in-range", "out-of-range",
    "read-only", "read-write", "placeholder-shown", "autofill", "empty",
    "open", "closed", "popover-open", "modal", "fullscreen", "playing", "paused", "defined",
}


def strip_comments(css):
    return re.sub(r"/\*.*?\*/", " ", css, flags=re.S)


def rules(css):
    """Every style rule's selector, at any depth, skipping at-rule preludes."""
    stack = []
    start = 0
    for i, c in enumerate(css):
        if c == "{":
            stack.append(css[start:i].strip())
            start = i + 1
        elif c == "}":
            if stack:
                prelude = stack.pop()
                if prelude and not prelude.startswith("@"):
                    yield prelude
            start = i + 1
        elif c == ";":
            start = i + 1


def split_top(text, separators):
    """Splits outside (), [] and quotes; returns (piece, separator-before) pairs."""
    parts, depth, quote, cur, sep = [], 0, None, "", None
    i = 0
    while i < len(text):
        c = text[i]
        if quote:
            cur += c
            if c == quote:
                quote = None
        elif c in "\"'":
            quote = c
            cur += c
        elif c in "([":
            depth += 1
            cur += c
        elif c in ")]":
            depth -= 1
            cur += c
        elif depth == 0 and c in separators:
            if cur.strip():
                parts.append((cur.strip(), sep))
            cur, sep = "", c
        else:
            cur += c
        i += 1
    if cur.strip():
        parts.append((cur.strip(), sep))
    return parts


def compounds(selector):
    # Combinators: whitespace, >, +, ~ — each splits one compound from the next.
    return [p for p, _ in split_top(selector, " \t\n>+~")]


def scan(compound):
    """(classes, attributes, pseudo-classes) written at the top level of one compound."""
    classes, attributes, pseudo = [], [], []
    i, n = 0, len(compound)
    while i < n:
        c = compound[i]
        if c == ".":
            m = CLASS.match(compound, i)
            if m:
                classes.append(m.group(1))
                i = m.end()
                continue
        elif c == "[":
            j, quote = i + 1, None
            while j < n and (quote or compound[j] != "]"):
                if compound[j] in "\"'":
                    quote = None if quote == compound[j] else (quote or compound[j])
                j += 1
            attributes.append(normalise(compound[i:j + 1]))
            i = j + 1
            continue
        elif c == ":":
            if compound.startswith("::", i):
                m = re.match(r"::[\w-]+(\([^)]*\))?", compound[i:])
                i += len(m.group(0)) if m else 2
                continue
            m = re.match(r":([\w-]+)", compound[i:])
            if m:
                name = m.group(1)
                end = i + len(m.group(0))
                arg = None
                if end < n and compound[end] == "(":
                    depth, j = 0, end
                    while j < n:
                        depth += compound[j] == "("
                        depth -= compound[j] == ")"
                        if depth == 0:
                            break
                        j += 1
                    arg = compound[end + 1:j].strip()
                    end = j + 1
                if arg is None and name in STATES:
                    pseudo.append(":" + name)
                elif name == "not" and arg is not None:
                    inner = arg.strip()
                    if re.fullmatch(r"\[[^\]]+\]", inner):
                        attributes.append(":not(" + normalise(inner) + ")")
                    elif re.fullmatch(r":([\w-]+)", inner) and inner[1:] in STATES:
                        pseudo.append(":not(" + inner + ")")
                i = end
                continue
        i += 1
    return classes, attributes, pseudo


def normalise(attribute):
    """[data-state=failed] and [data-state='failed'] are both [data-state="failed"]."""
    m = re.fullmatch(r"\[\s*([\w-]+)\s*([~|^$*]?=)\s*(['\"]?)(.*?)\3\s*(\s[is])?\s*\]", attribute)
    if not m:
        return re.sub(r"\s+", "", attribute)
    flag = (" " + m.group(5).strip()) if m.group(5) else ""
    return f'[{m.group(1)}{m.group(2)}"{m.group(4)}"{flag}]'


def main(path):
    with open(path, encoding="utf-8") as f:
        css = strip_comments(f.read())
    # The @layer ordering statement: its dotted names are no classes. A block opener is an
    # at-rule prelude, which rules() never reads as a selector.
    css = re.sub(r"@layer[^{;]*;", " ", css)

    found = {}
    for prelude in rules(css):
        for selector, _ in split_top(prelude, ","):
            for compound in compounds(selector):
                classes, attributes, pseudo = scan(compound)
                for name in classes:
                    entry = found.setdefault(name, {"attributes": set(), "pseudoClasses": set(), "combinedWith": set()})
                    entry["attributes"].update(attributes)
                    entry["pseudoClasses"].update(pseudo)
                    entry["combinedWith"].update(c for c in classes if c != name)
            # A class inside :is() / :not() / :has() is still a class the sheet names.
            for name in CLASS.findall(selector):
                found.setdefault(name, {"attributes": set(), "pseudoClasses": set(), "combinedWith": set()})

    out = {}
    for name in sorted(found):
        family = name.split("--")[0] if "--" in name[1:] else name
        entry = {"family": family}
        if "--" not in name[1:]:
            variants = sorted(c for c in found if c.startswith(name + "--"))
            if variants:
                entry["variants"] = variants
        for key in ("attributes", "pseudoClasses", "combinedWith"):
            if found[name][key]:
                entry[key] = sorted(found[name][key])
        out[name] = entry

    json.dump({
        "$comment": "GENERATED by build/class-states.sh from the shipped stylesheet. Do not edit.",
        "classes": out,
    }, sys.stdout, indent=2, ensure_ascii=False)
    sys.stdout.write("\n")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit("usage: class-states.py <Sedna.UI.css>")
    main(sys.argv[1])

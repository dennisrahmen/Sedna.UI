#!/usr/bin/env python3
"""Write the token export in the Design Tokens Community Group format (2025.10).

    python3 -I build/export-tokens-dtcg.py <Sedna.UI.tokens.json> <out-dir>

Called by build/export-tokens.sh, which writes the blocks export first and reads this
script's output for --check. The input is the blocks export, never the stylesheet: one
parse of the CSS, and two shapes of its result.

FILES. One file per block of the blocks export, because a mode in DTCG is a set of files
loaded in order, which is what Style Dictionary's `source` list and Tokens Studio's
multi-file token sets both take:

    Sedna.UI.tokens.dtcg.json                       the :root block: the dark default
    Sedna.UI.tokens.dtcg.<condition>.json           one per other block, e.g.
                                                    …dtcg.variant-light.json,
                                                    …dtcg.variant-light.cvd-1.json

A condition is the block's media query and attribute selectors, as slugs. The base file's
$extensions["com.sedna-ui"].modes lists every other file with its selector, its media and
`load`: the files that go before it, in order. A combination no single block covers
(light, colour-blind and high contrast at once) loads every file whose condition it meets,
in the order `modes` lists them.

VALUES. Each value is the 2025.10 form of its type: a colour is an sRGB object with its
hex, a dimension and a duration an object with a unit, a font family an array, a shadow
a shadow object or an array of them. A value that is exactly var(--x) is an alias,
"{group.name}". A value CSS computes — color-mix(), calc() of lengths — is resolved for
that file's condition and carries the CSS it came from in $extensions["com.sedna-ui"].css,
and it is written again in every later file where it resolves differently. A value no
DTCG type can hold — a system colour, env(), a keyword, a percentage — is not a token;
it is listed in the file's $extensions["com.sedna-ui"].cssOnly, so nothing is dropped
without a trace.

NAMES. A token's group is the first word of its name, and its name is the custom property
without its dashes: --brand-tint is brand.brand-tint, referenced as {brand.brand-tint}.
The full name is kept as the leaf because --brand is a token and a group of the same name
would have to hold it, which 2025.10 does with $root and today's tools do not read.
"""
import json
import os
import re
import sys

EXT = "com.sedna-ui"
BASE_NAME = "Sedna.UI.tokens.dtcg"

VAR = re.compile(r"^var\(\s*(--[\w-]+)\s*\)$")
HEX = re.compile(r"^#([0-9a-fA-F]{3,8})$")
NUMBER = re.compile(r"^-?(\d+\.?\d*|\.\d+)$")
DIMENSION = re.compile(r"^(-?(?:\d+\.?\d*|\.\d+))(px|rem)$")
DURATION = re.compile(r"^(-?(?:\d+\.?\d*|\.\d+))(ms|s)$")
ATTRIBUTE = re.compile(r'\[([\w-]+)="([^"]*)"\]')


def split_top(text, sep=","):
    """Splits on `sep` outside parentheses."""
    parts, depth, start = [], 0, 0
    for i, c in enumerate(text):
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
        elif c == sep and depth == 0:
            parts.append(text[start:i].strip())
            start = i + 1
    parts.append(text[start:].strip())
    return parts


def split_space(text):
    parts, depth, cur = [], 0, ""
    for c in text:
        if c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
        if c.isspace() and depth == 0:
            if cur:
                parts.append(cur)
            cur = ""
        else:
            cur += c
    if cur:
        parts.append(cur)
    return parts


def num(text):
    value = float(text)
    return int(value) if value == int(value) else round(value, 4)


# ── Colours ───────────────────────────────────────────────────────────────────


def parse_color(expr, ctx, depth=0):
    """(r, g, b, a) in 0–1, or None when the expression is no colour DTCG can hold."""
    if depth > 40:
        return None
    expr = expr.strip()
    m = VAR.match(expr)
    if m:
        target = ctx.get(m.group(1))
        return None if target is None else parse_color(target, ctx, depth + 1)
    if expr == "transparent":
        return (0.0, 0.0, 0.0, 0.0)
    m = HEX.match(expr)
    if m:
        h = m.group(1)
        if len(h) in (3, 4):
            h = "".join(c * 2 for c in h)
        if len(h) not in (6, 8):
            return None
        r, g, b = (int(h[i:i + 2], 16) / 255 for i in (0, 2, 4))
        a = int(h[6:8], 16) / 255 if len(h) == 8 else 1.0
        return (r, g, b, a)
    m = re.match(r"^rgba?\((.*)\)$", expr)
    if m:
        body = m.group(1).replace("/", ",")
        parts = [p for p in re.split(r"[\s,]+", body.strip()) if p]
        if len(parts) not in (3, 4):
            return None
        try:
            rgb = [float(p[:-1]) / 100 if p.endswith("%") else float(p) / 255 for p in parts[:3]]
            a = 1.0
            if len(parts) == 4:
                a = float(parts[3][:-1]) / 100 if parts[3].endswith("%") else float(parts[3])
        except ValueError:
            return None
        return (rgb[0], rgb[1], rgb[2], a)
    m = re.match(r"^color-mix\((.*)\)$", expr)
    if m:
        return mix(m.group(1), ctx, depth)
    return None


def mix(body, ctx, depth):
    """CSS Color 5 color-mix() in sRGB: premultiplied, percentages normalised."""
    args = split_top(body)
    if len(args) != 3 or args[0].replace(" ", "") != "insrgb":
        return None
    colours, weights = [], []
    for arg in args[1:]:
        after = re.match(r"^(.*\S)\s+(\d+\.?\d*)%$", arg)
        before = re.match(r"^(\d+\.?\d*)%\s+(.*)$", arg)
        if after:
            colour, weight = after.group(1), float(after.group(2)) / 100
        elif before:
            colour, weight = before.group(2), float(before.group(1)) / 100
        else:
            colour, weight = arg, None
        c = parse_color(colour, ctx, depth + 1)
        if c is None:
            return None
        colours.append(c)
        weights.append(weight)
    p1, p2 = weights
    if p1 is None and p2 is None:
        p1 = p2 = 0.5
    elif p1 is None:
        p1 = 1 - p2
    elif p2 is None:
        p2 = 1 - p1
    total = p1 + p2
    if total <= 0:
        return None
    multiplier = min(total, 1.0)
    p1, p2 = p1 / total, p2 / total
    (r1, g1, b1, a1), (r2, g2, b2, a2) = colours
    a = a1 * p1 + a2 * p2
    if a <= 0:
        return (0.0, 0.0, 0.0, 0.0)
    r = (r1 * a1 * p1 + r2 * a2 * p2) / a
    g = (g1 * a1 * p1 + g2 * a2 * p2) / a
    b = (b1 * a1 * p1 + b2 * a2 * p2) / a
    return (r, g, b, a * multiplier)


def color_value(c):
    r, g, b, a = (min(max(x, 0.0), 1.0) for x in c)
    value = {
        "colorSpace": "srgb",
        "components": [round(r, 4), round(g, 4), round(b, 4)],
    }
    if round(a, 4) != 1:
        value["alpha"] = round(a, 4)
    value["hex"] = "#" + "".join(f"{round(x * 255):02x}" for x in (r, g, b))
    return value


# ── Lengths ───────────────────────────────────────────────────────────────────


def parse_dimension(expr, ctx, depth=0):
    """(value, unit), or None."""
    if depth > 40:
        return None
    expr = expr.strip()
    m = VAR.match(expr)
    if m:
        target = ctx.get(m.group(1))
        return None if target is None else parse_dimension(target, ctx, depth + 1)
    m = DIMENSION.match(expr)
    if m:
        return (float(m.group(1)), m.group(2))
    if expr == "0":
        return (0.0, "px")
    m = re.match(r"^calc\((.*)\)$", expr)
    if m:
        terms = re.split(r"\s+([+-])\s+", m.group(1).strip())
        total, unit, sign = 0.0, None, 1
        for i, term in enumerate(terms):
            if i % 2 == 1:
                sign = 1 if term == "+" else -1
                continue
            d = parse_dimension(term, ctx, depth + 1)
            if d is None or (unit is not None and d[1] != unit and d[0] != 0):
                return None
            unit = unit or d[1]
            total += sign * d[0]
        return (total, unit or "px")
    return None


def dimension_value(d):
    return {"value": num(repr(d[0])), "unit": d[1]}


# ── Tokens ────────────────────────────────────────────────────────────────────


def ref(name):
    """The DTCG reference to a custom property, e.g. --brand-tint -> {brand.brand-tint}."""
    leaf = name[2:]
    return "{" + leaf.split("-")[0] + "." + leaf + "}"


class Context:
    """The tokens in force for one file's condition, and what each converts to."""

    def __init__(self, raw):
        self.raw = raw
        self.memo = {}

    def convert(self, name, depth=0):
        """A DTCG token object, or None when no type holds the value here."""
        if name in self.memo:
            return self.memo[name]
        self.memo[name] = None
        value = self.raw.get(name)
        token = None if value is None or depth > 40 else self._convert(name, value, depth)
        self.memo[name] = token
        return token

    def _convert(self, name, value, depth):
        m = VAR.match(value)
        if m:
            target = self.convert(m.group(1), depth + 1)
            if target is None:
                return None
            return {"$type": target["$type"], "$value": ref(m.group(1))}

        derived = "var(" in value or value.startswith(("color-mix(", "calc("))

        colour = parse_color(value, self.raw)
        if colour is not None:
            return self._token("color", color_value(colour), value, derived)

        if name.startswith("--font-") and ("," in value or '"' in value):
            return self._token("fontFamily", [f.strip().strip('"\'') for f in value.split(",")], value, False)

        m = DURATION.match(value)
        if m:
            return self._token("duration", {"value": num(m.group(1)), "unit": m.group(2)}, value, False)

        if NUMBER.match(value):
            return self._token("number", num(value), value, False)

        m = re.match(r"^cubic-bezier\((.*)\)$", value)
        if m:
            return self._token("cubicBezier", [num(p) for p in split_top(m.group(1))], value, False)

        d = parse_dimension(value, self.raw)
        if d is not None:
            return self._token("dimension", dimension_value(d), value, derived)

        shadow = self._shadow(value)
        if shadow is not None:
            return self._token("shadow", shadow, value, False)
        return None

    def _token(self, kind, dtcg, css, derived):
        token = {"$type": kind, "$value": dtcg}
        if derived:
            token["$extensions"] = {EXT: {"css": css}}
        return token

    def _shadow(self, value):
        layers = []
        for layer in split_top(value):
            parts = split_space(layer)
            inset = "inset" in parts
            parts = [p for p in parts if p != "inset"]
            lengths = [p for p in parts if DIMENSION.match(p) or p == "0"]
            colours = [p for p in parts if p not in lengths]
            if len(colours) != 1 or not 2 <= len(lengths) <= 4:
                return None
            m = VAR.match(colours[0])
            if m:
                target = self.convert(m.group(1))
                if target is None or target["$type"] != "color":
                    return None
                colour = ref(m.group(1))
            else:
                c = parse_color(colours[0], self.raw)
                if c is None:
                    return None
                colour = color_value(c)
            lengths += ["0"] * (4 - len(lengths))
            dims = [dimension_value(parse_dimension(p, {})) for p in lengths]
            shadow = {"color": colour, "offsetX": dims[0], "offsetY": dims[1], "blur": dims[2], "spread": dims[3]}
            if inset:
                shadow["inset"] = True
            layers.append(shadow)
        return layers[0] if len(layers) == 1 else layers


# ── Blocks to files ──────────────────────────────────────────────────────────


def condition(block):
    attrs = frozenset(ATTRIBUTE.findall(block["selector"]))
    media = block["media"] or ""
    return attrs, media


def slug(block):
    attrs, media = condition(block)
    parts = []
    if media:
        parts.append(re.sub(r"[^a-z0-9]+", "-", media.lower()).strip("-"))
    for name, value in ATTRIBUTE.findall(block["selector"]):
        parts.append((name[5:] if name.startswith("data-") else name) + "-" + value)
    return ".".join(parts)


def applies_within(a, b):
    """Block a is in force wherever block b is."""
    (attrs_a, media_a), (attrs_b, media_b) = condition(a), condition(b)
    return attrs_a <= attrs_b and (media_a == "" or media_a == media_b)


def order_key(item):
    index, block = item
    return (len(condition(block)[0]), index)


def group(tokens):
    """{name: token} to nested groups, in the order the names first appear."""
    out = {}
    for name, token in tokens.items():
        out.setdefault(name.split("-")[0], {})[name] = token
    return out


def main(source, out_dir):
    with open(source, encoding="utf-8") as f:
        blocks = json.load(f)["blocks"]

    base_index = next(i for i, b in enumerate(blocks) if b["media"] is None and b["selector"] == ":root")
    ordered = sorted(enumerate(blocks), key=order_key)
    files = {}
    for i, b in enumerate(blocks):
        files[i] = BASE_NAME + ".json" if i == base_index else f"{BASE_NAME}.{slug(b)}.json"
    if len(set(files.values())) != len(files):
        sys.exit("::error::Two token blocks give the same DTCG file name.")

    written = {}       # index -> {name: token} actually emitted
    documents = {}
    for index, block in ordered:
        load = [i for i, b in ordered if i != index and applies_within(b, block)]
        raw = {}
        for i in load + [index]:
            raw.update(blocks[i]["tokens"])
        ctx = Context(raw)

        # What the files before this one leave each name as.
        before = {}
        for i in load:
            before.update(written[i])

        tokens, css_only = {}, {}
        for name in raw:
            key = name[2:]
            declared_here = name in block["tokens"]
            token = ctx.convert(name)
            if declared_here:
                if token is None:
                    css_only[key] = block["tokens"][name]
                else:
                    tokens[key] = token
            elif token is not None and EXT in token.get("$extensions", {}):
                # Computed from something this block changed: write it again.
                if before.get(key, {}).get("$value") != token["$value"]:
                    tokens[key] = token
            elif token is None and key in before and EXT in before[key].get("$extensions", {}):
                # Computed from something this block made inexpressible.
                css_only[key] = raw[name]
        written[index] = tokens

        doc = {}
        if index == base_index:
            doc["$description"] = ("Sedna.UI design tokens, generated by build/export-tokens.sh from the "
                                   "stylesheet. This file is the default (dark) :root; the files under "
                                   "$extensions modes add each theme, colour-vision, contrast and density "
                                   "block on top of it.")
        else:
            where = block["selector"] + (f" inside @media {block['media']}" if block["media"] else "")
            doc["$description"] = f"Sedna.UI design tokens for {where}. Load after the files in load."
        meta = {}
        if index != base_index:
            meta["selector"] = block["selector"]
            meta["media"] = block["media"]
            meta["load"] = [files[i] for i in load]
        meta["cssOnly"] = {k: v for k, v in sorted(css_only.items())}
        doc["$extensions"] = {EXT: meta}
        doc.update(group({k: v for k, v in sorted(tokens.items())}))
        documents[index] = doc

    documents[base_index]["$extensions"][EXT]["modes"] = [
        {
            "file": files[i],
            "selector": blocks[i]["selector"],
            "media": blocks[i]["media"],
            "load": documents[i]["$extensions"][EXT]["load"],
        }
        for i, _ in ordered if i != base_index
    ]
    # Keep the manifest first, after the description.
    base = documents[base_index]
    meta = base["$extensions"][EXT]
    base["$extensions"][EXT] = {"modes": meta["modes"], "cssOnly": meta["cssOnly"]}

    os.makedirs(out_dir, exist_ok=True)
    for index, doc in documents.items():
        with open(os.path.join(out_dir, files[index]), "w", encoding="utf-8", newline="\n") as f:
            json.dump(doc, f, indent=2, ensure_ascii=False)
            f.write("\n")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        sys.exit("usage: export-tokens-dtcg.py <Sedna.UI.tokens.json> <out-dir>")
    main(sys.argv[1], sys.argv[2])

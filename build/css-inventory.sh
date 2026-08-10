#!/usr/bin/env bash
#
# Lists what a stylesheet declares — the class names in its selectors, or the custom
# properties it declares. One name per line, sorted, no duplicates.
#
# This exists because every count in this repo was hand-typed at some point and every
# one of them went wrong: the landing page advertised 317 CSS classes against an actual
# 311, and two structure listings claimed ~30 and ~35 CSS parts against an actual 57.
# Anything that needs a number now derives it from here.
#
# Two callers: build/class-history.sh derives which release first shipped each name, and
# build/release-inventory.sh diffs two revisions for the release notes. Both use this
# one implementation, because the extraction is where the mistakes live:
#
#   * A CLASS is not simply a dot followed by a name. `@layer sedna.tokens, sedna.paint, …`
#     uses DOTTED names, so a naive scan reports six phantom classes — .tokens, .base,
#     .frame, .paint, .utilities, .overrides. The @layer preludes are removed first.
#     Constraining the dot instead (requiring a non-identifier before it) looks like it
#     works and silently drops `dialog.palette`, because a tag-qualified selector has an
#     identifier right before the dot. Strip the prelude, not the dot.
#
#   * A TOKEN is a DECLARATION, so `--name:` has to open one. Without that anchor,
#     `.chip--active:hover` reads as a token called `--active`, and so do --required,
#     --start and --focusable from the other modifier classes. That inflated the token
#     count by four.
#
# Comments are stripped first, so prose mentioning a class or a token is never counted.
#
# Usage:  build/css-inventory.sh <file.css> classes
#         build/css-inventory.sh <file.css> tokens
#         build/css-inventory.sh --count <file.css> classes
#
set -euo pipefail

COUNT_ONLY=0
if [[ "${1:-}" == "--count" ]]; then
    COUNT_ONLY=1
    shift
fi

FILE="${1:-}"
WHAT="${2:-}"

if [[ -z "$FILE" || -z "$WHAT" ]]; then
    echo "usage: $(basename "$0") [--count] <file.css> classes|tokens" >&2
    exit 2
fi
if [[ ! -f "$FILE" ]]; then
    echo "::error::No such stylesheet: $FILE" >&2
    exit 1
fi
if [[ "$WHAT" != "classes" && "$WHAT" != "tokens" ]]; then
    echo "::error::Second argument must be 'classes' or 'tokens', not '$WHAT'." >&2
    exit 2
fi

# Strips /* … */ including comments spanning lines, then removes the @layer preludes
# (both the ordering statement and each block's opener) so their dotted names cannot be
# read as class selectors.
strip() {
    awk '
        inc { if ($0 ~ /\*\//) { sub(/^.*\*\//, "", $0); inc = 0 } else next }
        { while (match($0, /\/\*.*\*\//)) sub(/\/\*.*\*\//, "", $0)
          if ($0 ~ /\/\*/) { sub(/\/\*.*$/, "", $0); inc = 1 } }
        { gsub(/@layer[^{;]*[;{]/, " ") ; print }
    ' "$1"
}

case "$WHAT" in
    classes)
        # A leading "-" is legal in a class name, hence the optional one.
        NAMES="$(strip "$FILE" | grep -oE '\.-?[A-Za-z_][A-Za-z0-9_-]*' | sed 's/^\.//' | sort -u)"
        ;;
    tokens)
        # ERE has no lookbehind, so the character before "--" is captured and dropped.
        # A declaration opens the line or follows "{" or ";".
        NAMES="$(strip "$FILE" \
            | grep -oE '(^|[{;])[[:space:]]*--[A-Za-z0-9_-]+[[:space:]]*:' \
            | grep -oE '\-\-[A-Za-z0-9_-]+' \
            | sort -u)"
        ;;
esac

if [[ -z "$NAMES" ]]; then
    echo "::error::Found no $WHAT in $FILE. The extraction is broken, or the file is not a stylesheet." >&2
    exit 1
fi

if [[ "$COUNT_ONLY" == "1" ]]; then
    printf '%s\n' "$NAMES" | wc -l | tr -d ' '
else
    printf '%s\n' "$NAMES"
fi

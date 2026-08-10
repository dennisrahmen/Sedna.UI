#!/usr/bin/env bash
#
# Lists the classes and tokens a release adds and removes, by diffing the shipped
# stylesheet against an earlier revision.
#
# docs/releasing.md requires the notes to name every class the release adds, because a
# consuming app that already styles one of those names sees its appearance change on
# upgrade with no error — the list is what lets it grep first. Deriving that list by
# reading the diff is how it goes wrong: the first attempt at the 0.2.0 notes was
# transcribed from a roadmap and was wrong twice, once counting the dotted names in the
# @layer prelude as classes and once counting `.chip--active:hover` as a token.
#
# The removals matter more than the additions and are printed first: a removed class is
# the breaking part of the release.
#
# Usage:  build/release-inventory.sh                # against the latest tag
#         build/release-inventory.sh v0.1.0         # against a specific ref
#         build/release-inventory.sh v0.1.0 --notes # ready to paste into the notes
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# Two paths, deliberately: the working tree's, and the one this ref used.
# build/css-path.sh owns the history.
CSS="$ROOT/$("$ROOT/build/css-path.sh" HEAD)"
INVENTORY="$ROOT/build/css-inventory.sh"

REF="${1:-}"
NOTES=0
for arg in "$@"; do [[ "$arg" == "--notes" ]] && NOTES=1; done
[[ "$REF" == "--notes" ]] && REF=""

if [[ -z "$REF" ]]; then
    REF="$(git -C "$ROOT" describe --tags --abbrev=0 2>/dev/null || true)"
    [[ -n "$REF" ]] || { echo "::error::No tags yet — pass a ref explicitly."; exit 1; }
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

REF_CSS="$("$ROOT/build/css-path.sh" "$REF")" || exit 1
if ! git -C "$ROOT" show "$REF:$REF_CSS" > "$TMP/old.css" 2>/dev/null; then
    echo "::error::Cannot read $REF_CSS at $REF. Is the ref right?"
    exit 1
fi

inv() { bash "$INVENTORY" "$1" "$2"; }

for what in classes tokens; do
    inv "$TMP/old.css" "$what" > "$TMP/old.$what"
    inv "$CSS"         "$what" > "$TMP/new.$what"
    comm -13 "$TMP/old.$what" "$TMP/new.$what" > "$TMP/added.$what"
    comm -23 "$TMP/old.$what" "$TMP/new.$what" > "$TMP/removed.$what"
done

# ".name" for classes, "--name" for tokens — as they appear in a stylesheet, so the
# list can be pasted into a grep.
decorate() { [[ "$1" == "classes" ]] && sed 's/^/./' || cat; }

# Four per line, so a long list stays readable in release notes.
columns() { awk '{ printf "%-30s", $0; if (NR % 4 == 0) printf "\n" } END { if (NR % 4) printf "\n" }' \
            | sed 's/[[:space:]]*$//'; }

count() { wc -l < "$1" | tr -d ' '; }

if [[ "$NOTES" == "1" ]]; then
    if [[ -s "$TMP/removed.classes" || -s "$TMP/removed.tokens" ]]; then
        echo "## Breaking"
        echo
        echo "**Grep your stylesheets for these. They are gone:**"
        echo
        echo '```'
        cat "$TMP/removed.classes" | decorate classes | columns
        [[ -s "$TMP/removed.tokens" ]] && cat "$TMP/removed.tokens" | columns
        echo '```'
        echo
    fi
    echo "## The $(count "$TMP/added.classes") class names this release adds"
    echo
    echo "A class your app already styles changes appearance on upgrade with no error, so grep"
    echo "before bumping:"
    echo
    echo '```'
    cat "$TMP/added.classes" | decorate classes | columns
    echo '```'
    echo
    echo "## The $(count "$TMP/added.tokens") tokens this release adds"
    echo
    echo '```'
    cat "$TMP/added.tokens" | columns
    echo '```'
    exit 0
fi

echo "Stylesheet inventory: $REF -> working tree"
echo

for what in classes tokens; do
    printf '%s: %s at %s, %s now  (+%s / -%s)\n' \
        "$what" "$(count "$TMP/old.$what")" "$REF" "$(count "$TMP/new.$what")" \
        "$(count "$TMP/added.$what")" "$(count "$TMP/removed.$what")"
done

for what in classes tokens; do
    if [[ -s "$TMP/removed.$what" ]]; then
        echo
        echo "REMOVED $what — this is the breaking part of the release:"
        cat "$TMP/removed.$what" | decorate "$what" | columns | sed 's/^/  /'
    fi
done

for what in classes tokens; do
    echo
    echo "Added $what:"
    if [[ -s "$TMP/added.$what" ]]; then
        cat "$TMP/added.$what" | decorate "$what" | columns | sed 's/^/  /'
    else
        echo "  (none)"
    fi
done

echo
echo "Paste-ready: build/release-inventory.sh $REF --notes"

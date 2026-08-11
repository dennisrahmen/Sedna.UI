#!/usr/bin/env bash
# Which release first shipped each CSS class and each token.
#
# The hosted catalogue is built from `main` and can be ahead of any released
# version. An agent that copies markup for a class its app's pinned version does
# not have gets a page that renders unstyled, with no error anywhere — so every
# class and token the MCP server returns carries the release it first appeared in.
#
#     build/class-history.sh            regenerate the data file
#     build/class-history.sh --check    fail if it is out of date (CI-friendly)
#
# `null` means "in the working tree, in no release yet". That is the signal the
# server actually needs, and it is correct without regenerating after a tag —
# only *complete* after one. Cutting a release is therefore followed by running
# this and committing; see docs/releasing.md.
#
# Deliberately NOT written under src/Sedna.UI/wwwroot/, where it would ship
# inside the package. It is the catalogue app's data.
#
# Sits on build/css-inventory.sh, which is the one implementation of "what does
# this stylesheet declare". Both of its extractions are subtle — read its header
# before writing a third one.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
inventory="$root/build/css-inventory.sh"
# The stylesheet's path is not constant across history — build/css-path.sh owns the
# list. Reading a tag at the working tree's path silently attributed nothing.
sheet="$("$root/build/css-path.sh" HEAD)"
out="$root/src/Sedna.UI.Catalogue/Data/class-history.json"

check=0
[[ "${1:-}" == "--check" ]] && check=1

# Version order, not tag order: v0.10.0 sorts after v0.9.0 only under -V.
mapfile -t tags < <(git -C "$root" tag -l 'v*' | sort -V)

# Every release in the output comes from a tag, so a checkout without tags cannot
# produce the file at all. Named here rather than left to fail on an empty array,
# because the usual cause is a CI checkout that fetched none — `actions/checkout`
# is shallow by default and takes `fetch-depth: 0` to bring them.
if [[ ${#tags[@]} -eq 0 ]]; then
    echo "::error::No v* tags in this checkout, so no release can be attributed." >&2
    echo "        Fetch them: git fetch --tags   (in CI: actions/checkout with fetch-depth: 0)" >&2
    exit 1
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

emit_first_seen() {
    local kind="$1" name tag version tag_sheet resolved=0
    # An associative array, and the loops read from process substitution rather
    # than a pipe: a `while read` on the right of a pipe runs in a subshell, and
    # every assignment made there is discarded when it exits.
    local -A first=()
    local -A current=()

    # Only what the stylesheet declares TODAY. A class that shipped in 0.1.0 and
    # was removed since would otherwise be reported as available, which is worse
    # than not reporting it at all.
    while IFS= read -r name; do
        [[ -n "$name" ]] && current["$name"]=1
    done < <("$inventory" "$root/$sheet" "$kind")

    # The inventory runs inside process substitution, where a non-zero exit is
    # not seen by `set -e` and an unreadable script is simply no output. The
    # stylesheet always declares both kinds, so an empty result means the
    # inventory failed to run — say so, instead of emitting an almost-empty file
    # and letting --check report it as merely out of date.
    if [[ ${#current[@]} -eq 0 ]]; then
        echo "::error::$inventory produced no $kind. It did not run." >&2
        echo "        Check it is executable (git ls-files -s build/) and try: bash $inventory $sheet $kind" >&2
        exit 1
    fi

    for tag in "${tags[@]}"; do
        version="${tag#v}"
        # A tag from before the stylesheet existed has nothing to read; a tag that
        # has it under an older name must be read at THAT name.
        tag_sheet="$("$root/build/css-path.sh" "$tag" 2>/dev/null || true)"
        [[ -n "$tag_sheet" ]] || continue
        git -C "$root" show "$tag:$tag_sheet" >"$tmp/sheet.css" 2>/dev/null || continue
        resolved=$((resolved + 1))

        while IFS= read -r name; do
            [[ -z "$name" || -z "${current[$name]:-}" ]] && continue
            [[ -n "${first[$name]:-}" ]] && continue
            first["$name"]="$version"
        done < <("$inventory" "$tmp/sheet.css" "$kind")
    done

    # Every release in the output comes from a tag. Resolving none means the path
    # lookup is broken, not that nothing shipped — and the all-null file that would
    # be written is self-consistent, so --check could never report it.
    if [[ $resolved -eq 0 ]]; then
        echo "::error::No tag yielded a stylesheet, so no release can be attributed." >&2
        echo "        Check build/css-path.sh against: $(git -C "$root" tag -l 'v*' | tr '\n' ' ')" >&2
        exit 1
    fi

    # Anything in the working tree that no tag had is unreleased: the empty
    # second field, which becomes JSON null.
    for name in "${!current[@]}"; do
        printf '%s\t%s\n' "$name" "${first[$name]:-}"
    done | sort
}

json_map() {
    local first=1
    while IFS=$'\t' read -r name version; do
        [[ -z "$name" ]] && continue
        [[ $first -eq 0 ]] && printf ',\n'
        first=0
        if [[ -z "$version" ]]; then
            printf '    "%s": null' "$name"
        else
            printf '    "%s": "%s"' "$name" "$version"
        fi
    done
    printf '\n'
}

latest="${tags[-1]#v}"

{
    printf '{\n'
    printf '  "$comment": "GENERATED by build/class-history.sh — do not edit. null = in the working tree, in no release.",\n'
    printf '  "latestRelease": "%s",\n' "$latest"
    printf '  "classes": {\n'
    emit_first_seen classes | json_map
    printf '  },\n'
    printf '  "tokens": {\n'
    emit_first_seen tokens | json_map
    printf '  }\n'
    printf '}\n'
} >"$tmp/class-history.json"

if [[ $check -eq 1 ]]; then
    if [[ ! -f "$out" ]] || ! cmp -s "$tmp/class-history.json" "$out"; then
        echo "::error::class-history.json is out of date. Run build/class-history.sh."
        diff -u "$out" "$tmp/class-history.json" 2>/dev/null | head -40 || true
        exit 1
    fi
    echo "ok      class history is up to date (latest release $latest)"
    exit 0
fi

mkdir -p "$(dirname "$out")"
cp "$tmp/class-history.json" "$out"
echo "wrote   class-history.json (latest release $latest)"

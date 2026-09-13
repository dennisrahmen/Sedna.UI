#!/usr/bin/env bash
# Which release first shipped each CSS class, token, public C# member and example.
#
# The hosted catalogue is built from `main` and can be ahead of any released
# version. An agent that copies markup for a class its app's pinned version does
# not have gets a page that renders unstyled, with no error anywhere — so every
# class and token the MCP server returns carries the release it first appeared in.
#
#     build/class-history.sh              regenerate the data file
#     build/class-history.sh --check      fail if it is out of date (CI-friendly)
#
# `null` means "in the working tree, in no release yet". That is the signal the
# server actually needs.
#
# NOTHING IS DONE BEFORE A RELEASE. The tag is the only thing that dates an entry:
# once it exists, a plain regeneration turns every `null` the tag covers into its
# version and moves `latestRelease` onto it. So the first pull request after a
# release regenerates the file, and --check is what says so. Between the tag and
# that merge the committed file is behind — every entry the release shipped still
# reads null — and the hosted catalogue closes that gap itself: release.yml runs
# this at the tag and attaches the result to the GitHub release, and the site reads
# the latest release's copy at runtime and takes a version from it wherever its own
# embedded copy says null (ReleasedHistory, in the catalogue). See docs/releasing.md.
#
# Deliberately NOT written under src/Sedna.UI/wwwroot/, where it would ship
# inside the package. It is the catalogue app's data.
#
# Sits on build/css-inventory.sh, which is the one implementation of "what does
# this stylesheet declare". Both of its extractions are subtle — read its header
# before writing a third one. build/api-inventory.sh is the same thing for the
# public C# surface.
#
# FOUR MAPS, because a class floor is not the only floor an example has:
#
#   classes, tokens   the release that first declared each one.
#   csharp            the release that first declared each public C# type and
#                     member. An example demonstrating ISednaUi has no classes in
#                     it at all, so without this it had no floor to report.
#   examples          the release each example has looked EXACTLY like since —
#                     the earliest tag in the unbroken run of byte-identical
#                     copies ending at the working tree. Not "when the file first
#                     appeared": a file rewritten in the newest release to show a
#                     new API would otherwise claim the date of its first line.
#
# The server takes the newest of whichever floors apply to a result. Before this,
# an example with no classes fell back to `latestRelease` — which reported every
# JavaScript and C# snippet in the catalogue as brand new, and warned an agent
# off a capability its pinned version already had.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
inventory="$root/build/css-inventory.sh"
# The stylesheet's path is not constant across history — build/css-path.sh owns the
# list. Reading a tag at the working tree's path silently attributed nothing.
sheet="$("$root/build/css-path.sh" HEAD)"
api="$root/build/api-inventory.sh"
# Both have been at these paths for every tag that exists. If either moves, the
# emitters below fail loudly rather than attributing nothing — the failure
# css-path.sh was written for.
library="src/Sedna.UI"
examples="src/Sedna.UI.Catalogue/Examples"
out="$root/src/Sedna.UI.Catalogue/Data/class-history.json"

check=0
case "${1:-}" in
    "") ;;
    --check) check=1 ;;
    *)
        echo "::error::Unknown argument '$1'. Usage: class-history.sh [--check]" >&2
        exit 1
        ;;
esac

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

# The public C# surface, first release each name appeared in. Same shape as
# emit_first_seen, but the inventory reads a DIRECTORY rather than one file, so each
# tag's sources are unpacked into a scratch tree first. `git archive` is used rather
# than a checkout: it touches no working tree and no index.
emit_csharp_first_seen() {
    local name tag version resolved=0
    local -A first=()
    local -A current=()

    while IFS= read -r name; do
        [[ -n "$name" ]] && current["$name"]=1
    done < <("$api" "$root/$library")

    if [[ ${#current[@]} -eq 0 ]]; then
        echo "::error::$api produced nothing for $library. It did not run." >&2
        echo "        Try: bash $api $root/$library" >&2
        exit 1
    fi

    for tag in "${tags[@]}"; do
        version="${tag#v}"
        rm -rf "$tmp/api" && mkdir -p "$tmp/api"
        git -C "$root" archive "$tag" "$library" 2>/dev/null | tar -x -C "$tmp/api" 2>/dev/null || continue
        [[ -d "$tmp/api/$library" ]] || continue
        resolved=$((resolved + 1))

        while IFS= read -r name; do
            [[ -z "$name" || -z "${current[$name]:-}" ]] && continue
            [[ -n "${first[$name]:-}" ]] && continue
            first["$name"]="$version"
        done < <("$api" "$tmp/api/$library")
    done

    if [[ $resolved -eq 0 ]]; then
        echo "::error::No tag yielded $library, so no release can be attributed." >&2
        echo "        The library has moved; point the library path at its new home." >&2
        exit 1
    fi

    for name in "${!current[@]}"; do
        printf '%s\t%s\n' "$name" "${first[$name]:-}"
    done | sort
}

# Which release each example has looked EXACTLY like since. Walking the tags oldest
# first, a tag whose copy differs from the working tree's RESETS the run — so the
# answer is the start of the unbroken identical run reaching HEAD, and an example
# edited since the last release is correctly unreleased.
#
# "First appeared" would be wrong here, and quietly so: an example rewritten to
# demonstrate a new API keeps the path it has always had.
#
# Compared by blob id, never by content. `git ls-tree` lists every file in a tag
# with its hash in one call and `git hash-object --stdin-paths` hashes the whole
# working tree in another, so the walk is one process per tag. Showing each tag's
# copy of each file and piping it through cmp was one process per file per tag —
# thousands, and minutes on Windows.
emit_example_first_seen() {
    local path id tag version blob meta resolved=0
    local -A since=()
    local -A ids=()
    local -A wt=()
    local paths=()
    while IFS= read -r path; do
        [[ -n "$path" ]] || continue
        # …/Examples/Badge/Semantic.razor -> Badge/Semantic, which is the id the
        # server builds from the embedded resource name.
        id="${path#"$examples"/}"
        id="${id%.*}"
        ids["$id"]="$path"
        paths+=("$path")
    done < <(git -C "$root" ls-files "$examples")
    if [[ ${#ids[@]} -eq 0 ]]; then
        echo "::error::No tracked files under $examples. The examples have moved." >&2
        exit 1
    fi

    # `git ls-files` sees TRACKED files only, and that is deliberate: an id's `since`
    # is derived by reading the same path out of every tag, which an untracked file has
    # no history in.
    #
    # But it makes running this before `git add` quietly wrong, and --check then agrees
    # with the file it just failed to notice. That is the failure this repo keeps
    # finding: one implementation agreeing with itself. So say so out loud, and fail in
    # --check so CI is not the thing that catches it.
    local untracked
    untracked=$(git -C "$root" ls-files --others --exclude-standard "$examples")
    if [[ -n "$untracked" ]]; then
        {
            echo "::error::These example files are not tracked, so they are invisible here"
            echo "           and would be missing from class-history.json. git add them first:"
            echo "$untracked" | sed 's/^/             /'
        } >&2
        exit 1
    fi

    # The working tree's blob ids, filtered as git would store them, so a CRLF
    # checkout hashes to the same id the tag holds.
    while IFS=$'\t' read -r blob path; do
        wt["$path"]="$blob"
    done < <(paste <(printf '%s\n' "${paths[@]}" | git -C "$root" hash-object --stdin-paths) \
                   <(printf '%s\n' "${paths[@]}"))

    for tag in "${tags[@]}"; do
        version="${tag#v}"
        git -C "$root" cat-file -e "$tag:$examples" 2>/dev/null || continue
        resolved=$((resolved + 1))
        local -A at=()
        while IFS=$'\t' read -r meta path; do
            at["$path"]="${meta##* }"
        done < <(git -C "$root" ls-tree -r "$tag" -- "$examples")
        for id in "${!ids[@]}"; do
            path="${ids[$id]}"
            if [[ -n "${at[$path]:-}" && "${at[$path]}" == "${wt[$path]:-}" ]]; then
                [[ -n "${since[$id]:-}" ]] || since["$id"]="$version"
            else
                # Absent, or changed since: nothing before this tag can be the floor.
                unset "since[$id]"
            fi
        done
        unset at
    done
    if [[ $resolved -eq 0 ]]; then
        echo "::error::No tag yielded $examples, so no release can be attributed." >&2
        exit 1
    fi
    for id in "${!ids[@]}"; do
        printf '%s\t%s\n' "$id" "${since[$id]:-}"
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
    printf '  },\n'
    printf '  "csharp": {\n'
    emit_csharp_first_seen | json_map
    printf '  },\n'
    printf '  "examples": {\n'
    emit_example_first_seen | json_map
    printf '  }\n'
    printf '}\n'
} >"$tmp/class-history.json"

if [[ $check -eq 1 ]]; then
    if [[ -f "$out" ]] && cmp -s "$tmp/class-history.json" "$out"; then
        echo "ok      class history is up to date (latest release $latest)"
        exit 0
    fi
    echo "::error::class-history.json is out of date. Run build/class-history.sh and commit the result —"
    echo "        after a release, the first pull request is the one that does."
    diff -u "$out" "$tmp/class-history.json" 2>/dev/null | head -40 || true
    exit 1
fi

mkdir -p "$(dirname "$out")"
cp "$tmp/class-history.json" "$out"
echo "wrote   class-history.json (latest release $latest)"

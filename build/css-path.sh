#!/usr/bin/env bash
# Where did the shipped stylesheet live at a given git ref?
#
#     build/css-path.sh HEAD        -> src/Sedna.UI/wwwroot/css/Sedna.UI.css
#     build/css-path.sh v0.2.0      -> the same, at that tag
#
# One implementation, because two scripts read the stylesheet out of a tag and both
# once used the WORKING TREE's path to do it. That silently stops resolving the
# moment the file is renamed — and in class-history.sh the failure was swallowed by
# `|| continue`, attributing every class and token to no release at all.
#
# Sits beside build/css-inventory.sh, which is the one implementation of "what does
# this stylesheet declare". This is the one implementation of "where is it".
#
# Renaming the stylesheet or its directory again means ADDING A LINE to PATHS below,
# newest first. Never editing one: an old tag still needs its old path.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Newest first. The pre-rename path was here until the history was squashed to a
# single commit and v0.1.0 removed; with no ref older than the rename left in the
# repository, an entry for it could never match. The list keeps its shape because
# the NEXT rename needs it — add a line, never edit one, because an old tag still
# has to be read at the path it used.
PATHS=(
    "src/Sedna.UI/wwwroot/css/Sedna.UI.css"
)

REF="${1:-}"
[[ -n "$REF" ]] || { echo "usage: build/css-path.sh <ref>" >&2; exit 2; }

for path in "${PATHS[@]}"; do
    if git -C "$ROOT" cat-file -e "$REF:$path" 2>/dev/null; then
        echo "$path"
        exit 0
    fi
done

echo "::error::No known stylesheet path exists at $REF. Tried: ${PATHS[*]}" >&2
echo "        A ref older than the stylesheet is expected; a NEW path is not — add it to PATHS." >&2
exit 1

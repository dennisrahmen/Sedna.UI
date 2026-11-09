#!/usr/bin/env bash
#
# Generates the two files an app's own tooling reads, from the sources they describe:
#
#   tooling/stylelint/sedna-ui.stylelintrc.json   from the shipped stylesheet, through
#                                                 build/css-inventory.sh — never a typed list
#   skills/sedna-ui/SKILL.md                      from docs/CLAUDE.consuming-app.md
#
# Both are committed and packed into the .nupkg under tooling/, so the copy an app reads
# describes the version it installed. build/app-tooling.py says what each one holds.
#
# Usage:  build/app-tooling.sh          # regenerate
#         build/app-tooling.sh --check  # fail if either is out of date
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CSS="$ROOT/src/Sedna.UI/wwwroot/css/Sedna.UI.css"
DOC="$ROOT/docs/CLAUDE.consuming-app.md"
STYLELINT="$ROOT/tooling/stylelint/sedna-ui.stylelintrc.json"
SKILL="$ROOT/skills/sedna-ui/SKILL.md"

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

bash "$ROOT/build/css-inventory.sh" "$CSS" classes > "$TMP/classes.txt"
bash "$ROOT/build/css-inventory.sh" "$CSS" tokens > "$TMP/tokens.txt"
python3 -I "$ROOT/build/app-tooling.py" stylelint "$TMP/classes.txt" "$TMP/tokens.txt" > "$TMP/stylelint.json"
python3 -I "$ROOT/build/app-tooling.py" skill "$DOC" > "$TMP/SKILL.md"

if [[ "${1:-}" == "--check" ]]; then
    failed=0
    for pair in "stylelint.json:$STYLELINT" "SKILL.md:$SKILL"; do
        generated="$TMP/${pair%%:*}"
        committed="${pair#*:}"
        if cmp -s "$generated" "$committed"; then
            echo "ok      ${committed#"$ROOT"/} is up to date"
        else
            echo "::error::${committed#"$ROOT"/} does not match its source. Run build/app-tooling.sh"
            diff "$committed" "$generated" | head -20 || true
            failed=1
        fi
    done
    exit "$failed"
fi

mkdir -p "$(dirname "$STYLELINT")" "$(dirname "$SKILL")"
cp "$TMP/stylelint.json" "$STYLELINT"
cp "$TMP/SKILL.md" "$SKILL"
echo "wrote   ${STYLELINT#"$ROOT"/} and ${SKILL#"$ROOT"/}"

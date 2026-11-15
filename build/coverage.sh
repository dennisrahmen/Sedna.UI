#!/usr/bin/env bash
#
# Runs the test projects with coverage collection and leaves one Cobertura file per project in
# a known directory. Optionally applies the gate to what it collected.
#
#   build/coverage.sh                          both projects -> artifacts/coverage/
#   build/coverage.sh --project ui             one project: ui (Sedna.UI.Tests) or catalogue
#   build/coverage.sh --out DIR --name shard-1 DIR/shard-1-<project>.cobertura.xml
#   build/coverage.sh --no-build               reuse the built test projects
#   build/coverage.sh --gate [--base REF] [--markdown FILE]
#                                              then run build/coverage-gate.py on DIR
#   build/coverage.sh --gate-only ...          skip the tests; gate the files already in DIR
#   build/coverage.sh -- --filter "Foo"        everything after -- goes to `dotnet test`
#
# JavaScript: where the test base calls JsCoverage (docs/coverage.md), the Sedna.UI.Tests run in
# Chromium also writes V8 coverage, converted here to <label>-js.cobertura.xml over js-parts/.
# SEDNA_UI_BROWSER_TESTS=0 and SEDNA_UI_BROWSER=webkit collect none.
#
# The collector is the one Microsoft.NET.Test.Sdk already brings, so no package is added.
# SEDNA_UI_BROWSER_TESTS=0 is honoured as it is by the tests; coverage from several runs is
# merged by the gate, not here: give each run its own --name (or its own --out) and pass
# every file, or the directory holding them, to build/coverage-gate.py.
#
# Runs on bash 3.2.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT="$ROOT/artifacts/coverage"
NAME=""
PROJECTS=""
BUILD=1
GATE=0
RUN_TESTS=1
BASE=""
MARKDOWN=""
EXTRA=()

while [ $# -gt 0 ]; do
    case "$1" in
        --out) OUT="$2"; shift 2 ;;
        --name) NAME="$2"; shift 2 ;;
        --project) PROJECTS="$PROJECTS $2"; shift 2 ;;
        --no-build) BUILD=0; shift ;;
        --gate) GATE=1; shift ;;
        --gate-only) GATE=1; RUN_TESTS=0; shift ;;
        --base) BASE="$2"; shift 2 ;;
        --markdown) MARKDOWN="$2"; shift 2 ;;
        --) shift; EXTRA=("$@"); break ;;
        -h|--help) sed -n '2,23p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "coverage.sh: unknown argument: $1" >&2; exit 2 ;;
    esac
done

[ -n "$PROJECTS" ] || PROJECTS="ui catalogue"
mkdir -p "$OUT"
OUT="$(cd "$OUT" && pwd)"

project_dir() {
    case "$1" in
        ui) echo "Sedna.UI.Tests" ;;
        catalogue) echo "Sedna.UI.Catalogue.Tests" ;;
        *) echo "coverage.sh: unknown project: $1 (ui or catalogue)" >&2; exit 2 ;;
    esac
}

if [ "$RUN_TESTS" = 1 ]; then
    for p in $PROJECTS; do
        dir="$(project_dir "$p")"
        label="$p"
        [ -z "$NAME" ] || label="$NAME-$p"
        work="$(mktemp -d "${TMPDIR:-/tmp}/sedna-cov.XXXXXX")"
        jsdir=""
        if [ "$p" = ui ] && [ "${SEDNA_UI_BROWSER_TESTS:-1}" != 0 ] && [ "${SEDNA_UI_BROWSER:-chromium}" = chromium ]; then
            jsdir="$OUT/$label-js"
            rm -rf "$jsdir" "$OUT/$label-js.cobertura.xml"
        fi
        args=(test "$ROOT/src/$dir" --settings "$ROOT/build/coverage.runsettings"
              --collect "Code Coverage;Format=cobertura" --results-directory "$work")
        [ "$BUILD" = 1 ] || args+=(--no-build)
        # An empty array is an unbound variable to bash 3.2 under `set -u`.
        if [ ${#EXTRA[@]} -gt 0 ]; then args+=("${EXTRA[@]}"); fi
        echo "== $dir (coverage -> $OUT/$label.cobertura.xml)"
        status=0
        SEDNA_UI_JS_COVERAGE_DIR="$jsdir" dotnet "${args[@]}" || status=$?
        if [ -n "$jsdir" ]; then
            if ls "$jsdir"/*.json >/dev/null 2>&1; then
                python3 -I "$ROOT/build/coverage-js.py" --repo "$ROOT" --out "$OUT/$label-js.cobertura.xml" "$jsdir"
            else
                echo "coverage.sh: no JavaScript coverage was written (the test base does not call JsCoverage)" >&2
            fi
            rm -rf "$jsdir"
        fi
        found="$(find "$work" -name '*.cobertura.xml' | head -n 1)"
        if [ -z "$found" ]; then
            echo "coverage.sh: the collector wrote no Cobertura file for $dir" >&2
            rm -rf "$work"
            exit 2
        fi
        mv -f "$found" "$OUT/$label.cobertura.xml"
        rm -rf "$work"
        # Failing tests still leave their coverage; the run fails after every project ran.
        if [ "$status" != 0 ]; then FAILED_TESTS=1; fi
    done
fi

if [ "$GATE" = 1 ]; then
    gate=(python3 -I "$ROOT/build/coverage-gate.py" --repo "$ROOT")
    if [ -n "$BASE" ]; then
        gate+=(--base "$BASE")
    elif git -C "$ROOT" rev-parse --verify --quiet origin/main >/dev/null; then
        gate+=(--base origin/main)
    else
        gate+=(--base main)
    fi
    if [ -n "$MARKDOWN" ]; then gate+=(--markdown "$MARKDOWN"); fi
    gate+=("$OUT")
    "${gate[@]}"
fi

if [ "${FAILED_TESTS:-0}" = 1 ]; then
    echo "coverage.sh: some tests failed" >&2
    exit 1
fi

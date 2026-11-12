#!/usr/bin/env bash
#
# Stamps each design-guide entry with the sources that define its page, so that changing a
# component — its page, its examples, the css and script parts behind the classes the page
# teaches — fails a test until a person has read the entry again.
#
#   build/guide-stamp.sh --check          fail, naming each page and what changed, if any
#                                         entry's stamp no longer matches its sources
#   build/guide-stamp.sh /table [/stat]   after reviewing those entries: stamp them
#   build/guide-stamp.sh --all            stamp every entry (only after reviewing every one)
#   build/guide-stamp.sh --sources /table list the sources a page is stamped against
#
# The entries are docs/design-guide/components/<group>.json; the sources each was stamped
# against are kept in docs/design-guide/stamps/<group>.json, which this writes. What counts
# as a source, and why, is in build/guide-stamp.py. GuideStampTests runs --check.
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [[ $# -eq 0 ]]; then
    sed -n '2,16p' "$0" | sed 's/^# \{0,1\}//'
    exit 2
fi

exec python3 -I "$ROOT/build/guide-stamp.py" "$ROOT" "$@"

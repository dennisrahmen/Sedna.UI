#!/usr/bin/env bash
#
# Verifies that a built .nupkg contains the CSS, the JS, the icons and the tokens
# — and that it contains no catalogue.
#
# This exists because static web assets have historically been dropped from
# packages under some build configurations, and the failure is silent: the
# package restores, the app builds, and every stylesheet link 404s at runtime.
# So we do not assume — we unpack and assert.
#
# Usage:  build/verify-package.sh artifacts/Sedna.UI.*.nupkg
#
set -euo pipefail

PACKAGE="${1:?usage: verify-package.sh <path-to-nupkg>}"

# Only $1 is read, so a glob matching several packages would silently verify one
# of them — most likely a stale build left in artifacts/ — and report success for
# a package nobody is shipping. Refuse instead.
if [[ $# -gt 1 ]]; then
    echo "::error::Expected exactly one package, got $#: $*"
    echo "Clear artifacts/ so the glob matches only the build under test."
    exit 1
fi

if [[ ! -f "$PACKAGE" ]]; then
    echo "::error::Package not found: $PACKAGE"
    exit 1
fi

echo "Verifying $(basename "$PACKAGE")"
echo

CONTENTS="$(unzip -Z1 "$PACKAGE")"

# Static web assets are packed under staticwebassets/ and served to a consuming
# app from _content/Sedna.UI/ at runtime.
REQUIRED=(
    "lib/net10.0/Sedna.UI.dll"
    # GenerateDocumentationFile is on. Without this the C# surface ships
    # with no IntelliSense, which nothing else would notice.
    "lib/net10.0/Sedna.UI.xml"
    "staticwebassets/css/Sedna.UI.css"
    "staticwebassets/js/Sedna.UI.js"
    "staticwebassets/js/Sedna.UI.boot.js"
    # The token export. A design tool reads this path out of the restored package,
    # so it is as much a shipped contract as the stylesheet.
    "staticwebassets/tokens/Sedna.UI.tokens.json"
    "staticwebassets/lib/remixicon/remixicon.css"
    "staticwebassets/lib/remixicon/remixicon.woff2"
    "staticwebassets/lib/remixicon/LICENSE"
    "README.md"
    "LICENSE"
    "THIRD-PARTY-NOTICES.md"
    "icon.png"
)

failed=0
for entry in "${REQUIRED[@]}"; do
    if grep -Fxq "$entry" <<<"$CONTENTS"; then
        printf '  ok      %s\n' "$entry"
    else
        printf '  MISSING %s\n' "$entry"
        failed=1
    fi
done

echo

# The stylesheet must be the real one, not an empty placeholder, and must still
# carry the token block the whole contract rests on.
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
unzip -q -o "$PACKAGE" 'staticwebassets/css/Sedna.UI.css' -d "$tmp"
css="$tmp/staticwebassets/css/Sedna.UI.css"

if [[ ! -s "$css" ]]; then
    echo "::error::The packed stylesheet is empty."
    failed=1
elif ! grep -q -- '--brand:' "$css"; then
    echo "::error::The packed stylesheet has no --brand token — the token layer did not ship."
    failed=1
else
    echo "  ok      packed stylesheet carries the token layer ($(wc -c <"$css" | tr -d ' ') bytes)"
fi

# "The catalogue ships in the package" used to be a test-enforced rule. Its inverse
# needs the same enforcement, or a stray wwwroot/catalogue/ quietly puts it back —
# most plausibly by someone restoring a deleted file.
if grep -q '^staticwebassets/catalogue/' <<<"$CONTENTS"; then
    echo "::error::The package contains catalogue files. The catalogue is a hosted app now,"
    echo "         src/Sedna.UI.Catalogue — see CLAUDE.md."
    failed=1
else
    echo "  ok      no catalogue in the package"
fi

# And the app itself must never be packed with the library.
if grep -qi 'Sedna\.UI\.Catalogue' <<<"$CONTENTS"; then
    echo "::error::The catalogue application leaked into the package."
    failed=1
else
    echo "  ok      the catalogue application is not in the package"
fi

# One package, one dependency. With a second project in the repository a stray
# ProjectReference would become a NuGet dependency in every consuming app — and the
# catalogue app deliberately takes a third-party package the library may not.
unzip -q -o "$PACKAGE" '*.nuspec' -d "$tmp"
deps=$(grep -ohE '<dependency id="[^"]+"' "$tmp"/*.nuspec 2>/dev/null \
       | sed 's/.*id="//; s/"//' | sort -u | tr '\n' ' ' | sed 's/ $//')
if [[ "$deps" == "Microsoft.AspNetCore.Components.Web" ]]; then
    echo "  ok      exactly one package dependency ($deps)"
else
    echo "::error::Unexpected package dependencies: ${deps:-<none>}"
    failed=1
fi

# Blazor CSS isolation: the library ships no components, so no scoped CSS exists.
# If one ever appears its bundle must be packed too, or those styles vanish.
if grep -q '^staticwebassets/Sedna\.UI\.bundle\.scp\.css$' <<<"$CONTENTS"; then
    echo "  ok      scoped-CSS bundle packed"
elif grep -Erq '\.razor\.css$' <<<"$(find src/Sedna.UI -name '*.razor.css' 2>/dev/null || true)"; then
    echo "::error::Scoped CSS files exist in the project but no .bundle.scp.css was packed."
    failed=1
else
    echo "  ok      no scoped CSS in use, none expected"
fi

echo
if [[ "$failed" -ne 0 ]]; then
    echo "Package verification FAILED."
    exit 1
fi
echo "Package verification passed."

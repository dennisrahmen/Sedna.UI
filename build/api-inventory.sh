#!/usr/bin/env bash
#
# Lists the library's PUBLIC C# surface — one `Type` or `Type.Member` per line, sorted,
# no duplicates.
#
# This is the C# half of what build/css-inventory.sh does for the stylesheet, and it
# exists for the same failure. The catalogue's `since` answers "which release first
# shipped this", and until now it could only answer that for classes and tokens. An
# example demonstrating `ISednaUi.RegisterCommandsAsync` has no classes in it at all, so
# it had no floor to report and fell back to the newest release — which told an agent on
# the previous version that a capability it already had was unavailable.
#
# Caller: build/class-history.sh, which runs this over each tag's sources.
#
# WHY A PARSE AND NOT REFLECTION. Reflection over the built assembly is exact, and it is
# what CatalogueDataTests checks this against on every run — but getting it for an OLD
# TAG means restoring and building that tag, once per tag, on every CI run, for ever. So
# the history is parsed from source and the parse is pinned to reflection at HEAD: if the
# two ever disagree about the current surface, the test says so and this script is wrong.
#
# The three things that make the parse subtle, each of which produced a wrong answer
# first:
#
#   * INTERFACE AND ENUM MEMBERS CARRY NO `public` KEYWORD. `Task ToastAsync(…)` inside
#     `public interface ISednaUi` is public; so is `Info,` inside `public enum ToastKind`.
#     Filtering on the keyword drops the single most useful type in the library.
#
#   * A MEMBER IS THE LAST IDENTIFIER BEFORE ITS OPENER, not the first identifier on the
#     line. `public IReadOnlyList<SednaTheme> Themes { get; set; }` has four identifiers
#     before the brace and only the last one is the member. Taking the first reports the
#     return type of every method in the library.
#
#   * A CONSTRUCTOR LOOKS EXACTLY LIKE A METHOD NAMED AFTER ITS TYPE. `public SednaUi(…)`
#     is not a member called `SednaUi`; the type already carries that name, and emitting
#     both makes `SednaUi.SednaUi` a thing an agent can ask about.
#
# Depth is tracked with a brace count so a member is only taken one level inside its own
# type — otherwise a lambda body, an object initialiser or a nested type's members are
# all attributed to whatever type is open.
#
# `.razor` files are deliberately NOT scanned. A component's public surface is its
# parameters, and a parameter is a `[Parameter] public` property — which the Razor SDK
# requires be declared in a partial class, i.e. in a `.cs` file this already reads. A
# component whose whole class is in `@code` would be missed, and the reflection check
# names it.
#
# Usage:  build/api-inventory.sh <directory-of-.cs-files>
#
set -euo pipefail

DIR="${1:-}"
if [[ -z "$DIR" ]]; then
    echo "usage: $(basename "$0") <directory>" >&2
    exit 2
fi
if [[ ! -d "$DIR" ]]; then
    echo "::error::No such directory: $DIR" >&2
    exit 1
fi

# `obj/` and `bin/` hold generated sources — AssemblyInfo, global usings, the Razor
# SDK's own output — none of which is the library's surface.
find "$DIR" -type f -name '*.cs' -not -path '*/obj/*' -not -path '*/bin/*' -print0 |
    sort -z |
    xargs -0 cat |
    # Comments first: a `public` in prose, and an XML doc's <c>public int Foo</c>, are
    # not declarations. Block comments span lines, so this runs over the whole stream.
    perl -0777 -pe 's{/\*.*?\*/}{}gs; s{//[^\n]*}{}g' |
    awk '
        function emit(name) { if (name != "") print name }

        {
            line = $0
            # `record struct` and `record class` are one kind spelled with two words;
            # matching the first of them makes the TYPE NAME come out as "struct".
            gsub(/record[ 	]+struct/, "struct", line)
            gsub(/record[ 	]+class/, "class", line)

            # ── the type this line may open ──────────────────────────────────
            # Matched before the depth is updated, so the type is recorded at the
            # depth its brace is about to open at.
            if (match(line, /(^|[^A-Za-z0-9_])(class|interface|record|struct|enum)[ \t]+[A-Za-z_][A-Za-z0-9_]*/)) {
                decl = substr(line, RSTART, RLENGTH)
                kind = decl
                sub(/^[^A-Za-z]*/, "", kind)
                sub(/[ \t].*$/, "", kind)
                name = decl
                sub(/^.*[ \t]/, "", name)
                # Only a type the outside world can see. A nested type inherits its
                # own accessibility keyword, so the test is on this line alone.
                if (line ~ /(^|[^A-Za-z0-9_])public([^A-Za-z0-9_]|$)/) {
                    typeName = name
                    typeKind = kind
                    typeDepth = depth
                    typeOpen = 0
                    emit(typeName)
                    # A POSITIONAL RECORD declares its members in its own header, and
                    # there is no body for the member branch below to walk. Each
                    # parameter is a public property, so each one is emitted here.
                    header = line
                    sub(/^[^(]*/, "", header)
                    if (header ~ /^\(/) {
                        sub(/^\(/, "", header)
                        sub(/\).*$/, "", header)
                        gsub(/<[^<>]*>/, " ", header)
                        n = split(header, params, ",")
                        for (i = 1; i <= n; i++) {
                            p = params[i]
                            sub(/=.*$/, "", p)
                            if (match(p, /[A-Za-z_][A-Za-z0-9_]*[ 	]*$/))
                                emit(typeName "." substr(p, RSTART, RLENGTH))
                        }
                    }
                } else {
                    # A non-public type still opens a scope; forgetting it attributes
                    # its members to the enclosing public type.
                    typeName = ""
                    typeKind = ""
                    typeDepth = depth
                    typeOpen = 0
                }
            }
            else if (typeName != "" && depth == typeDepth + 1 && parens == 0) {
                member = ""

                if (typeKind == "enum") {
                    # `Info,` / `Danger,` / `Warn = 2,`
                    if (match(line, /^[ \t]*[A-Za-z_][A-Za-z0-9_]*/)) {
                        member = substr(line, RSTART, RLENGTH)
                        gsub(/[ \t]/, "", member)
                    }
                }
                # An interface member has no accessibility keyword; a class member
                # must carry `public`.
                # An indexer is reported by reflection under the name the compiler
                # gives it, which is not the name in the source.
                else if (line ~ /this[ 	]*\[/ &&
                         (typeKind == "interface" || line ~ /(^|[^A-Za-z0-9_])public([^A-Za-z0-9_]|$)/)) {
                    member = "Item"
                }
                else if (typeKind == "interface" || line ~ /(^|[^A-Za-z0-9_])public([^A-Za-z0-9_]|$)/) {
                    head = line
                    # Everything up to whatever opens the member: a parameter list, a
                    # property accessor block, an initialiser, or a bare `;`.
                    sub(/[({=;].*$/, "", head)
                    # Generic parameters are part of the name, not of it.
                    gsub(/<[^<>]*>/, " ", head)
                    if (match(head, /[A-Za-z_][A-Za-z0-9_]*[ \t]*$/)) {
                        member = substr(head, RSTART, RLENGTH)
                        gsub(/[ \t]/, "", member)
                    }
                }

                # `public SednaUi(…)` is the constructor, not a member of that name.
                if (member != "" && member != typeName && member !~ /^(get|set|init|value|new|return|if|else|for|foreach|while|using|namespace|partial|readonly|static|const|required|virtual|override|abstract|sealed|async|extern|unsafe|event|delegate|operator|implicit|explicit|this|base|public|private|protected|internal|void|var)$/)
                    emit(typeName "." member)
            }

            # ── depth, last ──────────────────────────────────────────────────
            opens = gsub(/\{/, "{", line)
            closes = gsub(/\}/, "}", line)
            depth += opens - closes
            # A PARAMETER LIST WRAPS. `string? title = null,` on its own line sits at
            # exactly the depth a member sits at, so without this every parameter of
            # every multi-line signature is reported as a member of the type.
            parens += gsub(/\(/, "(", line) - gsub(/\)/, ")", line)
            # The body may open on a LATER line than the declaration — Allman braces
            # are the house style here. Closing the type on `depth <= typeDepth`
            # before it ever opened drops every member in the file.
            if (typeName != "" && depth > typeDepth) typeOpen = 1
            if (typeName != "" && typeOpen && depth <= typeDepth) { typeName = ""; typeKind = ""; typeOpen = 0 }
        }
    ' |
    sort -u

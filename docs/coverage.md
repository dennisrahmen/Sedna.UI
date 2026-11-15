# Coverage

Line coverage of the library's code, and the three checks that hold it. The thresholds and the
list of exclusions are in one file, `build/coverage-policy.txt`.

## What is measured

| Code | How | Report |
|---|---|---|
| C# in `src/Sedna.UI/` and `.cs` / `.razor` in `src/Sedna.UI.Catalogue/` | the collector `Microsoft.NET.Test.Sdk` brings, over both test projects | Cobertura |
| The shipped script: `js-parts/` and each surface's `<name>-parts/` | V8 block coverage from Chromium, mapped back to the parts | Cobertura, written by `build/coverage-js.py` |

CSS is not line-covered. Its coverage is the structural tests: every class appears in an example, and
every example is scanned.

Only tracked files under a `scope` of the policy count, so generated code under `obj/`, the test
projects and packages the tests load are never in a figure. The vendored engines under `wwwroot/lib/`
and the generated bundles under `wwwroot/js/` are not measured; their sources are.

## The three checks

`build/coverage-gate.py` merges every report it is given (a line is covered when any report covers
it) and applies:

1. **Changed lines.** Of the executable lines the pull request adds or changes in scope (`git diff
   <base>...HEAD`), at least `changed_lines_min` percent are covered. The failure lists each
   uncovered changed line as `path:line`. A change with no executable line in scope passes and says so.
2. **Whole-code floor.** Total line coverage in scope is at least `whole_floor` percent.
3. **Remainder report.** With `--markdown <file>` the gate writes the uncovered lines of every file,
   most first, the totals, the exclusions with their reasons and the files no report mentions. CI
   appends it to the job summary. The remainder is read in review; it is not only bounded.

A harness that measured nothing is an error, never a pass: no report, a report that matches no file
of the repository, a report with no executable line in scope, or nothing measured under a directory
the policy `require`s.

## The numbers do not move

Neither number is a target, and neither is raised because the suite exceeds it. A change that adds
tests does not move them. `changed_lines_min` is how a change comes with its tests; `whole_floor` is a
tripwire set once a little under the measured value, for tests being deleted or a harness measuring
less. A pull request that changes either says why, and the reviewer decides.

Cover the change you made. Do not write a test to reach a percentage: a test names a behaviour and
asserts it.

## Excluding code

An exclusion is in the code or in the policy, with a reason, and nowhere else.

- **A type or member:** `[ExcludeFromCodeCoverage(Justification = "…")]`. The collector honours it. The
  gate fails on one without a justification.
- **A file:** `exclude = <path or glob> | <reason>` in `build/coverage-policy.txt`. The reason is
  required, and a pattern that matches no tracked file fails the gate.

A valid reason says what the code is and why no test of this repository can run it: a member only a
host process calls at start-up, a branch only reachable with an operating-system failure, wiring with
nothing to assert. "Hard to test", "not worth testing" and "covered elsewhere" are not reasons. Do not
add a path pattern to a script, a runsettings file or a workflow.

## Running it

```bash
build/coverage.sh --gate --base origin/main --markdown artifacts/coverage.md
```

Both test projects run with collection; Cobertura files land in `artifacts/coverage/`, one per
project (`ui.cobertura.xml`, `catalogue.cobertura.xml`) and one for the script when Chromium ran
(`ui-js.cobertura.xml`). Useful variants:

```bash
build/coverage.sh --project ui --no-build          # one project, reuse the build
build/coverage.sh --out artifacts/coverage --name shard-2 -- --filter "FullyQualifiedName~Menu"
build/coverage.sh --gate-only --partial            # gate what is already there (see below)
python3 build/coverage-gate.py --base origin/main --skip-changed artifacts/coverage   # floor only
```

`SEDNA_UI_BROWSER_TESTS=0` skips the browser tests, and with them the script's coverage and the
catalogue's browser-driven code. A run like that is partial: pass `--partial` to the gate, which then
warns about a `require`d directory with nothing measured instead of failing. Its floor figure is not
comparable with a full run.

To merge runs, give each its own `--name` (or `--out`) and pass every file, or the directory holding
them, to the gate.

## Reading a failure

```
Changed lines: 6 executable, 3 covered (50.0%); required 80%.
Uncovered changed lines:
  src/Sedna.UI/State/SednaPager.cs:41
  …
```

Each listed line is a line you added or changed that no test executed. Write the test that executes
it, or, when no test can, exclude it as above with its reason. Paths are repository-relative, so they
click through in the job summary.

- `whole-code coverage … is below the floor` — tests were removed or stopped running. Find which.
- `ERROR: … measured no file in scope`, `matches no file of this repository`, `nothing was measured` —
  the harness is broken, not the code: a collector that did not run, a report from another checkout,
  a shard's artifact that did not arrive.
- `[ExcludeFromCodeCoverage] without a Justification` — add the reason.

## The script's coverage

Chromium's DevTools Profiler reports which character ranges of the served `Sedna.UI*.js` ran.
`TestSupport/JsCoverage.cs` collects it from every page the browser tests open and writes one JSON
file per test process; `build/coverage-js.py` merges them and maps each range to a line of a part.

The mapping is exact and checked: the served file is `build/strip-comments.py`'s output of the
readable bundle, which is the parts' own lines under one marker per part. The converter replays the
stripping with line numbers and fails unless the result equals the served file and every line equals
its line in the part. A script no test loaded counts as uncovered. A line is executable unless it
holds only brackets, commas and semicolons, and is covered when the first character that is not
whitespace or a closing bracket ran; V8 reports blocks, not lines.

Chromium only: WebKit offers no Profiler. The script's coverage is Chromium's, and CI collects it once.
`Sedna.UI.boot.js` is served as written and is not measured.

## CI

The jobs that run the tests write their Cobertura files and upload them as artifacts; one job
downloads all of them and runs the gate. The commands and the artifact names are in the workflow.

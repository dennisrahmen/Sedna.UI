# Sedna.UI Rebrand Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename `DR.Simple_UI` to `Sedna.UI` across every surface the library owns — package, namespace, C# types, JavaScript global, CSS utility and cascade-layer namespaces, storage prefix, shipped asset paths, build scripts, guards, docs, CI and deploy config — without changing a single design-token value.

**Architecture:** Four stages on branch `rebrand/sedna-ui`, merged as one PR so `main` is never half-renamed. Stage 3 (brand assets) is blocked on artwork and is the last task here, marked BLOCKED. Substitutions are applied with `perl -pi` using an ordered, case-sensitive rule list; directory moves use `git mv` so history follows. Generated artefacts are never hand-edited — the generators are edited and re-run.

**Tech Stack:** .NET 10, Blazor, xUnit + bUnit + Playwright, bash build scripts, Docker, Railway, GitHub Actions.

**Spec:** `docs/superpowers/specs/2026-08-04-sedna-ui-rebrand-design.md`

## Global Constraints

- `net10.0`, `LangVersion latest`, `Nullable enable`, `ImplicitUsings enable`, `TreatWarningsAsErrors true`. A warning fails the build.
- **No token value changes.** The 159 design tokens keep their current values. The default theme must render byte-identically. Token *names* stay unprefixed.
- **Never edit a generated file.** `wwwroot/css/Sedna.UI.css`, `wwwroot/js/Sedna.UI.js`, `wwwroot/tokens/Sedna.UI.tokens.json` and `Data/class-history.json` are generated. Edit the source and re-run the generator.
- **No `!important`.** A test enforces it.
- **No third-party package reference in the library.** `Microsoft.AspNetCore.Components.Web` is the only permitted dependency.
- **No hard-coded colours** outside a `:root` token block.
- **Do not state a count of anything in prose** in repository documentation. Where a number matters it is calculated.
- **`release.yml` must keep its filename.** The nuget.org trusted-publishing policy matches on it.
- **Brand filenames (`dr-simple-ui-*`) are NOT renamed before Task 10.** Tasks 1–9 must leave every `dr-simple-ui` string untouched, or `BrandAssetTests` and `dotnet pack` fail.
- Case-sensitive throughout: `DrSimpleUi` (C# type) and `drSimpleUi` (JS global) are different identifiers renamed in different tasks.

## Substitution rules

Applied with `perl -pi -e` so lookahead is available. Case-sensitive. The order within a task matters where noted.

**Every sweep must exclude `docs/superpowers/*`.** The spec and this plan are the
only files that legitimately contain "old → new" tables, and a blind sweep turns
each row into "new → new", destroying the record of what changed. Task 1's first
run did exactly that and the documents had to be restored from git.

| # | Pattern (perl) | Replacement | Task |
|---|---|---|---|
| R1 | `DR\.Simple_UI` | `Sedna.UI` | 1 |
| R1b | `DR\\\.Simple_UI` (a literal backslash before each dot) | `Sedna\.UI` | 1 |
| R2 | `DR_UI_BROWSER_TESTS` | `SEDNA_UI_BROWSER_TESTS` | 8 |
| R3 | `dr\.simple_ui` | `sedna.ui` | 8 |
| R4 | `DrSimpleUi` | `SednaUi` | 2 |
| R5 | `drsimpleui-catalogue` | `sedna-ui-catalogue` | 8 |
| R6 | `drsimpleui://` | `sednaui://` | 6 |
| R7 | `drSimpleUi` | `sednaUi` | 6 |
| R8 | `drui\.` | `sedna.` | 6 |
| R9 | `dr\.(tokens\|base\|frame\|paint\|utilities\|overrides)` | `sedna.$1` | 5 |
| R10 | `dr-ui\.test` | `sedna-ui.test` | 6 |
| R11 | `\bdr-(?!simple-ui)` | `sedna-` | 5 |
| R12 | `dr-simple-ui` | `sedna-ui` | **10 (BLOCKED)** |

R1 covers `DR.Simple_UI.Tests`, `DR.Simple_UI.Catalogue` and `DR.Simple_UI.Catalogue.Tests` because the suffixes survive.

**R1b exists because R1 cannot match an escaped form.** `build/verify-package.sh`
greps the package listing with `'DR\.Simple_UI\.Catalogue'` and
`'^staticwebassets/DR\.Simple_UI\.bundle\.scp\.css$'` — regex literals in which
each dot is backslash-escaped. R1's pattern expects a bare dot, so it leaves them
untouched, and the guard then searches for an assembly name that can no longer
exist: **the "catalogue leaked into the package" check goes silently inert while
still printing `ok`.** These two lines are the only escaped survivors in the
repository, confirmed with `grep -nI 'DR\\\.Simple'`. R4 covers `IDrSimpleUi` → `ISednaUi` and `DrSimpleUiServiceCollectionExtensions`. R7 covers `drSimpleUiCatalogue`. R5 must precede any `drsimpleui` rule. R10 must precede R11. R11's negative lookahead is what protects the brand filenames.

**What R11 renames**, beyond the 36 utility class selectors:

- **5 `@keyframes` names** — `dr-modal-in`, `dr-progress`, `dr-pulse`, `dr-skeleton`, `dr-spin`. A keyframes name that stops matching its `animation:` reference **silently disables the animation**; there is no error. Both sides are in `css-parts/`, so one sweep keeps them in step.
- **JS-generated DOM ids and ARIA targets** — `dr-toasts`, `dr-palette-list`, `dr-palette-<n>`, `dr-search-panel`, `dr-search-list`, `dr-search-item-<n>`, `dr-rail-tip`, `dr-md-view-<n>`, `dr-step`. These are written by `js-parts/` and read by `css-parts/`, the Playwright script tests and the catalogue's accessibility tests. A mismatch breaks `aria-controls` linkage, which `Catalogue.Tests/Browser/AccessibilityTests` catches.

## Standing gate

Referred to below as **the gate**. Run from the repository root.

```bash
dotnet build Sedna.UI.slnx -c Debug
SEDNA_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Debug --no-build
```

Before Task 8 renames the environment variable, the gate uses `DR_UI_BROWSER_TESTS=1` instead. Without the variable the browser tests skip while reporting as passed, which is exactly the false green this rename cannot afford.

## Note on TDD in a rename

The existing guard tests **are** the specification. For Tasks 1, 2, 3, 5, 6 and 8 the honest cycle is: make the change, run the gate, watch a red suite go green — the tests were written before the change and they fail without it. Where genuinely new behaviour is added (Task 4's `build/css-path.sh`, Task 7's regression guard) a real red-green-commit cycle applies and is written out.

---

## File Structure

**Moved (via `git mv`, history preserved):**

| From | To |
|---|---|
| `DR.Simple_UI.slnx` | `Sedna.UI.slnx` |
| `src/DR.Simple_UI/` | `src/Sedna.UI/` |
| `src/DR.Simple_UI/DR.Simple_UI.csproj` | `src/Sedna.UI/Sedna.UI.csproj` |
| `src/DR.Simple_UI.Tests/` | `src/Sedna.UI.Tests/` |
| `src/DR.Simple_UI.Catalogue/` | `src/Sedna.UI.Catalogue/` |
| `src/DR.Simple_UI.Catalogue.Tests/` | `src/Sedna.UI.Catalogue.Tests/` |
| `src/Sedna.UI/Interop/DrSimpleUi.cs` | `src/Sedna.UI/Interop/SednaUi.cs` |
| `src/Sedna.UI/Interop/IDrSimpleUi.cs` | `src/Sedna.UI/Interop/ISednaUi.cs` |
| `src/Sedna.UI/Interop/DrSimpleUiOptions.cs` | `src/Sedna.UI/Interop/SednaUiOptions.cs` |
| `src/Sedna.UI/Interop/DrSimpleUiSettings.cs` | `src/Sedna.UI/Interop/SednaUiSettings.cs` |
| `src/Sedna.UI/DependencyInjection/DrSimpleUiServiceCollectionExtensions.cs` | `.../SednaUiServiceCollectionExtensions.cs` |
| `src/Sedna.UI/wwwroot/css/DR.Simple_UI.css` | `src/Sedna.UI/wwwroot/css/Sedna.UI.css` |
| `src/Sedna.UI/wwwroot/js/DR.Simple_UI.js` | `src/Sedna.UI/wwwroot/js/Sedna.UI.js` |
| `src/Sedna.UI/wwwroot/js/DR.Simple_UI.boot.js` | `src/Sedna.UI/wwwroot/js/Sedna.UI.boot.js` |
| `src/Sedna.UI/wwwroot/tokens/DR.Simple_UI.tokens.json` | `src/Sedna.UI/wwwroot/tokens/Sedna.UI.tokens.json` |

**Created:**

| File | Responsibility |
|---|---|
| `build/css-path.sh` | The single implementation of "where did the stylesheet live at this git ref". Consumed by `class-history.sh` and `release-inventory.sh`. |
| `src/Sedna.UI.Tests/Packaging/BrandNamingTests.cs` | The old-brand regression guard: no `dr-`, `DR.Simple_UI`, `drSimpleUi`, `drui.` or `@layer dr.` in the shipped assets or the catalogue's sources. |
| `docs/migrating-from-dr-simple-ui.md` | The consumer migration guide. Published by the MCP server — see Task 8. |

---

### Task 1: Move the projects and rename the namespace

**Files:**
- Move: the six project/solution paths in the File Structure table
- Modify: all four `.csproj`, `Sedna.UI.slnx`, every `.cs` and `.razor` with a `DR.Simple_UI` namespace or `using`, `src/Sedna.UI.Tests/TestSupport/Assets.cs`, `src/Sedna.UI.Catalogue.Tests/TestSupport/CatalogueAssets.cs`, `.editorconfig`, `build/*.sh` path variables, `railway.json`, `src/Sedna.UI.Catalogue/Dockerfile`, `.github/workflows/*.yml`
- Test: the whole existing suite is the test

**Interfaces:**
- Consumes: nothing — this is the first task.
- Produces: root namespace `Sedna.UI`; sub-namespaces `Sedna.UI.Interop`, `Sedna.UI.Navigation`, `Sedna.UI.Tests.TestSupport`, `Sedna.UI.Catalogue.Mcp`, `Sedna.UI.Catalogue.Navigation`, `Sedna.UI.Catalogue.Tests.TestSupport`. `Assets.RepoRoot` now finds the root by `Sedna.UI.slnx`. Every later task depends on these paths.

- [ ] **Step 1: Delete stale build output so a stale assembly cannot satisfy a reference that should fail**

```bash
cd "C:/Users/rahmen/source/Sedna.UI"
rm -rf artifacts .vs
find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
git status --short   # expect: only the spec/plan additions, nothing deleted
```

- [ ] **Step 2: Move the solution file and the four project directories**

```bash
git mv DR.Simple_UI.slnx Sedna.UI.slnx
git mv src/DR.Simple_UI.Catalogue.Tests src/Sedna.UI.Catalogue.Tests
git mv src/DR.Simple_UI.Catalogue      src/Sedna.UI.Catalogue
git mv src/DR.Simple_UI.Tests          src/Sedna.UI.Tests
git mv src/DR.Simple_UI                src/Sedna.UI
git mv src/Sedna.UI/DR.Simple_UI.csproj                             src/Sedna.UI/Sedna.UI.csproj
git mv src/Sedna.UI.Tests/DR.Simple_UI.Tests.csproj                 src/Sedna.UI.Tests/Sedna.UI.Tests.csproj
git mv src/Sedna.UI.Catalogue/DR.Simple_UI.Catalogue.csproj         src/Sedna.UI.Catalogue/Sedna.UI.Catalogue.csproj
git mv src/Sedna.UI.Catalogue.Tests/DR.Simple_UI.Catalogue.Tests.csproj src/Sedna.UI.Catalogue.Tests/Sedna.UI.Catalogue.Tests.csproj
```

Order matters: the two `.Catalogue*` directories move before `src/DR.Simple_UI`, because moving the shorter path first would leave the longer ones dangling.

- [ ] **Step 3: Apply R1 and R1b to every tracked text file, excluding the brand assets and this plan's own documents**

```bash
git ls-files -z -- ':!:assets/brand/*' ':!:docs/superpowers/*' \
  | xargs -0 grep -lIZ 'DR\.Simple_UI' \
  | xargs -0 perl -pi \
      -e 's/DR\\\.Simple_UI/Sedna\\.UI/g;' \
      -e 's/DR\.Simple_UI/Sedna.UI/g;'
```

`-I` skips binary files. `assets/brand/*` is excluded because its `.svg` files carry the old name in metadata and belong to Task 10. **`docs/superpowers/*` is excluded because the spec and this plan are the only files whose "old → new" tables must keep the old name** — a blind sweep rewrites every row to "new → new". R1b runs first, so the escaped forms in `build/verify-package.sh` become `Sedna\.UI` and stay valid grep patterns rather than being missed.

- [ ] **Step 4: Verify no `DR.Simple_UI` survives, in either spelling, outside the brand directory and this plan's documents**

```bash
git ls-files -z | xargs -0 grep -nI -e 'DR\.Simple_UI' -e 'DR\\\.Simple_UI' \
  | grep -v '^assets/brand/' | grep -v '^docs/superpowers/'
```

Expected: no output.

- [ ] **Step 4a: Confirm the two package guards are still live patterns, not inert ones**

```bash
grep -n 'Sedna' build/verify-package.sh | grep 'grep -'
```

Expected: `grep -qi 'Sedna\.UI\.Catalogue'` and `grep -q '^staticwebassets/Sedna\.UI\.bundle\.scp\.css$'`. If either still names `DR`, the guard prints `ok` while checking for something that cannot exist.

- [ ] **Step 5: Confirm the four csproj identity properties**

`src/Sedna.UI/Sedna.UI.csproj` must now read `<RootNamespace>Sedna.UI</RootNamespace>`, `<AssemblyName>Sedna.UI</AssemblyName>`, `<PackageId>Sedna.UI</PackageId>`, `<Title>Sedna.UI</Title>`. `src/Sedna.UI.Catalogue/Sedna.UI.Catalogue.csproj` must read `<RootNamespace>Sedna.UI.Catalogue</RootNamespace>` and `<AssemblyName>Sedna.UI.Catalogue</AssemblyName>`, and `<InternalsVisibleTo Include="Sedna.UI.Catalogue.Tests" />`.

`RootNamespace` and the project name must stay equal: the catalogue finds its embedded example sources by `RootNamespace` + folder path, so a split produces **blank code blocks rather than a build error**.

```bash
grep -n 'RootNamespace\|AssemblyName\|PackageId\|<Title>\|InternalsVisibleTo' src/Sedna.UI/Sedna.UI.csproj src/Sedna.UI.Catalogue/Sedna.UI.Catalogue.csproj
```

- [ ] **Step 6: Confirm the `.editorconfig` path globs moved**

A glob that stops matching raises no error, so this is checked by eye.

```bash
grep -n '^\[src/' .editorconfig
```

Expected: `[src/Sedna.UI.Tests/**.cs]` and `[src/Sedna.UI.Catalogue{,.Tests}/**.cs]`.

- [ ] **Step 7: Move the four generated assets — this task cannot be green without it**

R1 rewrote the asset-path *literals* in `Assets.cs`, `ShippedPathTests`, `verify-package.sh`, `App.razor`, `getting-started.md` and the three generators. The files on disk still carry the old names, so the literals now point at nothing. The first execution of this plan separated these moves into Task 3 and Task 1's gate failed with 224 tests red for exactly this reason: **the rename of a path and the move of the file it names are one atomic change, not two.**

```bash
git mv src/Sedna.UI/wwwroot/css/DR.Simple_UI.css            src/Sedna.UI/wwwroot/css/Sedna.UI.css
git mv src/Sedna.UI/wwwroot/js/DR.Simple_UI.js              src/Sedna.UI/wwwroot/js/Sedna.UI.js
git mv src/Sedna.UI/wwwroot/js/DR.Simple_UI.boot.js         src/Sedna.UI/wwwroot/js/Sedna.UI.boot.js
git mv src/Sedna.UI/wwwroot/tokens/DR.Simple_UI.tokens.json src/Sedna.UI/wwwroot/tokens/Sedna.UI.tokens.json
ls src/Sedna.UI/wwwroot/css src/Sedna.UI/wwwroot/js src/Sedna.UI/wwwroot/tokens
```

Expected: no filename under `wwwroot` contains `DR`.

- [ ] **Step 8: Regenerate the three generated artefacts**

Their header comments name their generator and source directory, both of which moved.

```bash
bash build/bundle-css.sh
bash build/bundle-js.sh
bash build/export-tokens.sh
git status --short src/Sedna.UI/wwwroot
```

Expected: the three generated files show as modified. `Sedna.UI.boot.js` is hand-maintained and must NOT appear — no generator writes it.

- [ ] **Step 9: Run the gate**

```bash
dotnet build Sedna.UI.slnx -c Debug
DR_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Debug --no-build
```

Expected: PASS. Two failure modes worth naming: every test failing with "Could not find Sedna.UI.slnx" means R1 missed `Assets.cs`; a cluster of missing-file assertions plus 30-second Playwright navigation timeouts means Step 7's moves are incomplete.

- [ ] **Step 10: Pack and verify the package**

```bash
dotnet pack src/Sedna.UI/Sedna.UI.csproj -c Release -o artifacts
bash build/verify-package.sh artifacts/Sedna.UI.0.1.0.nupkg
```

Expected: every asserted path present, no catalogue, exactly one dependency. The packed icon is still `dr-simple-ui-icon-128.png` renamed to `icon.png` — correct until Task 10.

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "Move the projects and rename the namespace to Sedna.UI"
```

---

### Task 2: Rename the C# public types

**Files:**
- Move: the five Interop/DependencyInjection files in the File Structure table
- Modify: every `.cs` referencing them, `docs/getting-started.md` (the `@using` and API names only — the domain and prose are Task 8)
- Test: `src/Sedna.UI.Tests/Utilities/InteropTests.cs`, `RegistrationTests.cs`

**Interfaces:**
- Consumes: Task 1's `Sedna.UI` namespaces.
- Produces: `Sedna.UI.Interop.SednaUi`, `Sedna.UI.Interop.ISednaUi`, `Sedna.UI.Interop.SednaUiOptions`, `Sedna.UI.Interop.SednaUiSettings`, and `Sedna.UI.DependencyInjection.SednaUiServiceCollectionExtensions.AddSednaUi(this IServiceCollection, Action<SednaUiOptions>?)`. `SednaUiOptions.StoragePrefix` still defaults to `"drui."` after this task — Task 6 changes it.

- [ ] **Step 1: Move the five files**

```bash
git mv src/Sedna.UI/Interop/DrSimpleUi.cs         src/Sedna.UI/Interop/SednaUi.cs
git mv src/Sedna.UI/Interop/IDrSimpleUi.cs        src/Sedna.UI/Interop/ISednaUi.cs
git mv src/Sedna.UI/Interop/DrSimpleUiOptions.cs  src/Sedna.UI/Interop/SednaUiOptions.cs
git mv src/Sedna.UI/Interop/DrSimpleUiSettings.cs src/Sedna.UI/Interop/SednaUiSettings.cs
git mv src/Sedna.UI/DependencyInjection/DrSimpleUiServiceCollectionExtensions.cs \
       src/Sedna.UI/DependencyInjection/SednaUiServiceCollectionExtensions.cs
```

- [ ] **Step 2: Apply R4**

`DrSimpleUi` → `SednaUi`, which also turns `IDrSimpleUi` into `ISednaUi`, `AddDrSimpleUi` into `AddSednaUi`, and `DrSimpleUiServiceCollectionExtensions` into `SednaUiServiceCollectionExtensions`.

```bash
git ls-files -z | xargs -0 grep -lIZ 'DrSimpleUi' \
  | xargs -0 perl -pi -e 's/DrSimpleUi/SednaUi/g'
```

This rule is case-sensitive and does **not** touch the lowercase JS global `drSimpleUi`.

- [ ] **Step 3: Verify**

```bash
git ls-files -z | xargs -0 grep -nI 'DrSimpleUi' | grep -v '^docs/superpowers/'
```

Expected: no output.

- [ ] **Step 4: Run the gate**

```bash
dotnet build Sedna.UI.slnx -c Debug
DR_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Debug --no-build
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Rename the public C# surface to SednaUi"
```

---

### Task 3: Verify the asset-path coupling — moves ABSORBED INTO TASK 1

**This task's `git mv`s moved into Task 1 Steps 7–10.** The first execution of
this plan separated them and Task 1's gate failed with 224 tests red: R1 rewrites
the asset-path *literals*, so the files must move in the same commit or every
path-based assertion points at nothing. Renaming a path and moving the file it
names is one atomic change.

Task 3 is kept as a numbered slot rather than renumbered, because
`scripts/task-brief` extracts by task number and renumbering would silently
misalign every later brief.

**Files:**
- Verify only: `src/Sedna.UI.Catalogue/Components/App.razor`, `docs/getting-started.md`, `build/verify-package.sh`
- Test: `src/Sedna.UI.Catalogue.Tests/HostPageTests.cs`

**Interfaces:**
- Consumes: Task 1's moved assets and swept literals.
- Produces: nothing new. This is a checkpoint.

- [ ] **Step 1: Confirm the host page and the documented block still agree**

`HostPageTests` asserts `docs/getting-started.md`'s block is a substring of
`App.razor` with comments stripped, so the two must carry identical asset paths.
Neither file is generated, so a mismatch here is a real drift.

```bash
grep -n '_content/Sedna.UI' src/Sedna.UI.Catalogue/Components/App.razor docs/getting-started.md
```

Expected, in both and in this order: `js/Sedna.UI.boot.js`,
`lib/remixicon/remixicon.css`, `css/Sedna.UI.css`, `js/Sedna.UI.js`.

- [ ] **Step 2: Confirm no generated artefact disagrees with its parts**

```bash
bash build/bundle-css.sh && bash build/bundle-js.sh && bash build/export-tokens.sh
git status --porcelain src/Sedna.UI/wwwroot
```

Expected: empty. Task 1 already regenerated them, so a diff here means a
generator is non-deterministic or Task 1's commit missed a file.

- [ ] **Step 3: Confirm both package guards are live patterns**

```bash
bash build/verify-package.sh artifacts/Sedna.UI.0.1.0.nupkg
grep -n 'Sedna' build/verify-package.sh | grep 'grep -'
```

Expected: the script green, and both greps naming `Sedna\.UI\.Catalogue` and
`^staticwebassets/Sedna\.UI\.bundle\.scp\.css$`. A pattern still naming `DR`
prints `ok` while checking for something that cannot exist.

- [ ] **Step 4: Nothing to commit**

If Steps 1–3 are clean this task produces no commit. Say so rather than
manufacturing an empty one.

### Task 4: Add `build/css-path.sh` and fix the two historical-path readers

**Files:**
- Create: `build/css-path.sh`
- Modify: `build/class-history.sh`, `build/release-inventory.sh`
- Test: `src/Sedna.UI.Catalogue.Tests/CatalogueDataTests.cs` (create)

**Interfaces:**
- Consumes: Task 3's `src/Sedna.UI/wwwroot/css/Sedna.UI.css`.
- Produces: `build/css-path.sh <ref>` writing one repo-relative path to stdout, exit 0; exit 1 and a message on stderr when no known path exists at `<ref>`. `class-history.sh` and `release-inventory.sh` both call it.

- [ ] **Step 1: Reproduce the defect**

```bash
bash build/class-history.sh
python -c "
import json;d=json.load(open('src/Sedna.UI.Catalogue/Data/class-history.json'))
n=sum(1 for v in d['classes'].values() if v is None)
print('classes:',len(d['classes']),'null:',n,'card =>',d['classes'].get('card'))"
```

Expected **failure**: `null` equals the class count and `card => None`. `.card` shipped in 0.1.0, so a `None` here is the silent defect. If `card` already reports `0.1.0`, stop — the premise is wrong and the task needs rethinking.

- [ ] **Step 2: Write the failing regression test**

Create `src/Sedna.UI.Catalogue.Tests/CatalogueDataTests.cs`:

`CatalogueAssets` lives in `Sedna.UI.Catalogue.Tests.TestSupport`, a *child* of this file's namespace, and C# does not search child namespaces — the `using` is required, and `Usings.cs` holds only `global using Xunit;`.

```csharp
using System.Text.Json;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The generated class history actually attributes releases.
/// </summary>
/// <remarks>
/// A fully-nulled class-history.json is internally self-consistent, so
/// build/class-history.sh --check cannot detect one. The generator reads the
/// stylesheet out of each git tag, and it once did so at the working tree's path —
/// which stops resolving the moment the file is renamed, silently attributing
/// nothing. This asserts a class that demonstrably shipped still says so.
/// </remarks>
public class CatalogueDataTests
{
    private static JsonDocument History() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(CatalogueAssets.AppDir, "Data", "class-history.json")));

    [Fact]
    public void Classes_that_shipped_in_a_release_carry_that_release()
    {
        using var doc = History();
        var classes = doc.RootElement.GetProperty("classes");

        // .card is in the published 0.1.0 stylesheet and has never been renamed.
        Assert.Equal("0.1.0", classes.GetProperty("card").GetString());

        var attributed = classes.EnumerateObject().Count(p => p.Value.ValueKind != JsonValueKind.Null);
        Assert.True(attributed > 0,
            "Every class is null. build/class-history.sh read no tag — check build/css-path.sh.");
    }

    [Fact]
    public void Tokens_that_shipped_in_a_release_carry_that_release()
    {
        using var doc = History();
        Assert.Equal("0.1.0", doc.RootElement.GetProperty("tokens").GetProperty("--brand").GetString());
    }
}
```

- [ ] **Step 3: Run it and watch it fail**

```bash
dotnet test src/Sedna.UI.Catalogue.Tests -c Debug --filter CatalogueDataTests
```

Expected: FAIL — `Assert.Equal() Failure: Expected "0.1.0", Actual: null`.

- [ ] **Step 4: Create `build/css-path.sh`**

```bash
#!/usr/bin/env bash
# Where did the shipped stylesheet live at a given git ref?
#
#     build/css-path.sh v0.1.0      -> src/DR.Simple_UI/wwwroot/css/DR.Simple_UI.css
#     build/css-path.sh HEAD        -> src/Sedna.UI/wwwroot/css/Sedna.UI.css
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

PATHS=(
    "src/Sedna.UI/wwwroot/css/Sedna.UI.css"
    "src/DR.Simple_UI/wwwroot/css/DR.Simple_UI.css"
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
```

- [ ] **Step 5: Make it executable and check it in as executable**

git on Windows does not record the bit by default, and `set -e` cannot see a failure inside process substitution — a missing bit is therefore silent.

```bash
chmod +x build/css-path.sh
git update-index --add --chmod=+x build/css-path.sh
git ls-files -s build/css-path.sh    # expect mode 100755
bash build/css-path.sh v0.1.0        # expect src/DR.Simple_UI/wwwroot/css/DR.Simple_UI.css
bash build/css-path.sh HEAD          # expect src/Sedna.UI/wwwroot/css/Sedna.UI.css
```

- [ ] **Step 6: Rewire `class-history.sh`**

Replace the fixed `sheet=` assignment with the working-tree lookup, and resolve each tag's own path inside the loop. In `build/class-history.sh`, change:

```bash
sheet="src/DR.Simple_UI/wwwroot/css/DR.Simple_UI.css"
```

to:

```bash
# The stylesheet's path is not constant across history — build/css-path.sh owns the
# list. Reading a tag at the working tree's path silently attributed nothing.
sheet="$("$root/build/css-path.sh" HEAD)"
```

and inside `emit_first_seen`, change:

```bash
        # A tag from before the stylesheet existed has nothing to read.
        git -C "$root" show "$tag:$sheet" >"$tmp/sheet.css" 2>/dev/null || continue
```

to:

```bash
        # A tag from before the stylesheet existed has nothing to read; a tag that
        # has it under an older name must be read at THAT name.
        tag_sheet="$("$root/build/css-path.sh" "$tag" 2>/dev/null || true)"
        [[ -n "$tag_sheet" ]] || continue
        git -C "$root" show "$tag:$tag_sheet" >"$tmp/sheet.css" 2>/dev/null || continue
        resolved=$((resolved + 1))
```

Declare `local tag_sheet` and `local resolved=0` beside the existing locals, and after the tag loop add the loud guard:

```bash
    # Every release in the output comes from a tag. Resolving none means the path
    # lookup is broken, not that nothing shipped — and the all-null file that would
    # be written is self-consistent, so --check could never report it.
    if [[ $resolved -eq 0 ]]; then
        echo "::error::No tag yielded a stylesheet, so no release can be attributed." >&2
        echo "        Check build/css-path.sh against: $(git -C "$root" tag -l 'v*' | tr '\n' ' ')" >&2
        exit 1
    fi
```

- [ ] **Step 7: Rewire `release-inventory.sh`**

In `build/release-inventory.sh`, change:

```bash
REL_CSS="src/DR.Simple_UI/wwwroot/css/DR.Simple_UI.css"
CSS="$ROOT/$REL_CSS"
```

to:

```bash
# Two paths, deliberately: the working tree's, and the one this ref used.
# build/css-path.sh owns the history.
CSS="$ROOT/$("$ROOT/build/css-path.sh" HEAD)"
```

and replace the `git show` guard with a lookup against `$REF`:

```bash
REF_CSS="$("$ROOT/build/css-path.sh" "$REF")" || exit 1
if ! git -C "$ROOT" show "$REF:$REF_CSS" > "$TMP/old.css" 2>/dev/null; then
    echo "::error::Cannot read $REF_CSS at $REF. Is the ref right?"
    exit 1
fi
```

`REF` is resolved before this point in the existing script, so the lookup has a value.

- [ ] **Step 8: Regenerate and watch the test pass**

```bash
bash build/class-history.sh
bash build/class-history.sh --check
dotnet test src/Sedna.UI.Catalogue.Tests -c Debug --filter CatalogueDataTests
```

Expected: the generator reports `wrote class-history.json (latest release 0.1.0)`, `--check` reports ok, tests PASS. The 36 `dr-*` utilities still appear under their old names and resolve to `0.1.0` — Task 5 renames them, after which they correctly become `null`.

- [ ] **Step 9: Confirm `release-inventory.sh` runs**

```bash
bash build/release-inventory.sh v0.1.0
```

Expected: added/removed class and token lists, exit 0.

- [ ] **Step 10: Run the gate and commit**

```bash
dotnet build Sedna.UI.slnx -c Debug
DR_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Debug --no-build
git add -A
git commit -m "Read the stylesheet at the path each tag used"
```

---

### Task 5: Rename the CSS utility namespace and the cascade layers

**Files:**
- Modify: 16 files in `src/Sedna.UI/css-parts/`, `src/Sedna.UI/css-parts/CLAUDE.md`, `build/bundle-css.sh` (`layer_for()`), `build/css-inventory.sh`, `src/Sedna.UI/js-parts/*` (the class and id strings they write), `src/Sedna.UI/wwwroot/js/Sedna.UI.boot.js`, `src/Sedna.UI.Catalogue/**` examples and pages, `src/Sedna.UI.Catalogue/wwwroot/catalogue.js`, `CLAUDE.md`, `docs/architecture.md`
- Test: `Css/CascadeLayerTests.cs`, `Catalogue.Tests/Mcp/McpToolTests.cs`, `Catalogue.Tests/CoverageTests.cs`, the Playwright script and browser tests

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces: `@layer sedna.tokens, sedna.base, sedna.frame, sedna.paint, sedna.utilities, sedna.overrides`; 36 `.sedna-*` utility classes; `@keyframes sedna-modal-in|sedna-progress|sedna-pulse|sedna-skeleton|sedna-spin`; DOM ids `sedna-toasts`, `sedna-palette-list`, `sedna-palette-<n>`, `sedna-search-panel`, `sedna-search-list`, `sedna-search-item-<n>`, `sedna-rail-tip`, `sedna-md-view-<n>`, `sedna-step`.

- [ ] **Step 1: Record the before-state so the sweep can be checked, not trusted**

```bash
bash build/css-inventory.sh src/Sedna.UI/wwwroot/css/Sedna.UI.css classes | sort > /tmp/before-classes
grep -c 'dr-' src/Sedna.UI/wwwroot/css/Sedna.UI.css
```

- [ ] **Step 2: Apply R9 then R11 across the library and the catalogue, never the brand**

R10 is Task 6's, and `dr-ui.test` lives only in `src/Sedna.UI.Tests/TestSupport/ScriptTestBase.cs`; R11's `\b` boundary would rewrite it to `sedna-ui.test` here, which is the intended end state anyway, so allowing it is correct and Task 6's R10 then finds nothing. Note it rather than fight it.

```bash
git ls-files -z -- ':!:assets/brand/*' ':!:docs/superpowers/*' \
  | xargs -0 grep -lIZ -e 'dr\.\(tokens\|base\|frame\|paint\|utilities\|overrides\)' -e 'dr-' \
  | xargs -0 perl -pi \
      -e 's/\bdr\.(tokens|base|frame|paint|utilities|overrides)\b/sedna.$1/g;' \
      -e 's/\bdr-(?!simple-ui)/sedna-/g;'
```

- [ ] **Step 3: Confirm the brand strings survived untouched**

```bash
git ls-files -z | xargs -0 grep -nI 'dr-simple-ui' | wc -l
```

Expected: a non-zero count, unchanged from before the sweep. If it is 0, the lookahead failed and Task 10's premise is broken — revert and fix the rule.

- [ ] **Step 4: Confirm no `dr-` or `dr.`-layer token remains**

```bash
git ls-files -z -- ':!:assets/brand/*' ':!:docs/superpowers/*' \
  | xargs -0 grep -nI -e '\bdr-\(\?\!simple\)' -e '@layer dr\.' -e '\bdr\.tokens\b' \
  | grep -v 'dr-simple-ui'
```

Expected: no output.

- [ ] **Step 5: Confirm `layer_for()` emits the new names**

```bash
grep -n 'sedna\.' build/bundle-css.sh
```

Expected: the six `echo "sedna.<layer>"` branches and the six-line comment table.

- [ ] **Step 6: Regenerate the stylesheet and the script, and diff the class inventory**

```bash
bash build/bundle-css.sh
bash build/bundle-js.sh
bash build/css-inventory.sh src/Sedna.UI/wwwroot/css/Sedna.UI.css classes | sort > /tmp/after-classes
diff <(sed 's/^dr-/sedna-/' /tmp/before-classes | sort) /tmp/after-classes
```

Expected: no differences. This proves the sweep renamed exactly the `dr-` classes and touched no other selector.

- [ ] **Step 7: Confirm every keyframes name still matches an `animation` reference**

A mismatch disables the animation silently, so it is checked explicitly.

```bash
for k in $(grep -roh '@keyframes[[:space:]]*sedna-[a-z-]*' src/Sedna.UI/css-parts/ | awk '{print $2}' | sort -u); do
    n=$(grep -c "$k" src/Sedna.UI/wwwroot/css/Sedna.UI.css)
    echo "$k referenced $n time(s)"
    [ "$n" -ge 2 ] || echo "  ^^ DEFINED BUT NEVER USED — check the animation: property"
done
```

Expected: five names, each appearing at least twice (its definition plus at least one `animation:` reference).

- [ ] **Step 8: Regenerate the class history**

The 36 renamed utilities must now report `null`, because `.sedna-row` has never shipped.

```bash
bash build/class-history.sh
python -c "
import json;d=json.load(open('src/Sedna.UI.Catalogue/Data/class-history.json'))
c=d['classes']
print('card =>',c.get('card'))
print('sedna-row =>',c.get('sedna-row'))
print('dr-row present?',('dr-row' in c))"
```

Expected: `card => 0.1.0`, `sedna-row => None`, `dr-row present? False`.

- [ ] **Step 9: Run the gate**

```bash
dotnet build Sedna.UI.slnx -c Debug
DR_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Debug --no-build
```

Expected: PASS. `CoverageTests` failing with an undocumented `sedna-*` class means an example still carries the old name; `AccessibilityTests` failing means an `aria-controls` id and its target diverged.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "Rename the CSS utility and cascade-layer namespaces to sedna"
```

---

### Task 6: Rename the JavaScript global and the storage prefix

**Files:**
- Modify: 17 files in `src/Sedna.UI/js-parts/`, `js-parts/CLAUDE.md`, `src/Sedna.UI/wwwroot/js/Sedna.UI.boot.js`, `src/Sedna.UI/Interop/SednaUi.cs`, `src/Sedna.UI/Interop/SednaUiOptions.cs`, `src/Sedna.UI.Catalogue/wwwroot/catalogue.js`, `src/Sedna.UI.Catalogue/Mcp/CatalogueResources.cs`, `src/Sedna.UI.Tests/TestSupport/ScriptTestBase.cs`, `CLAUDE.md`, `docs/architecture.md`, `docs/getting-started.md`
- Test: `Packaging/ScriptContractTests.cs`, `Script/SurfaceTests.cs`, `Script/BootThemeTests.cs`, `Browser/ThemeToggleTests.cs`

**Interfaces:**
- Consumes: Tasks 1–5.
- Produces: `window.sednaUi`, `window.sednaUiCatalogue`, `localStorage` keys `sedna.theme`/`sedna.cvd`/`sedna.density`/`sedna.dir`/`sedna.lang`, cookie `sedna.lang`, `SednaUiOptions.StoragePrefix` defaulting to `"sedna."`, boot-script `data-prefix` defaulting to `"sedna."`, MCP resource URIs `sednaui://stylesheet|tokens|version|docs/{name}`.

`Sedna.UI.boot.js` is generated by no script — it is hand-maintained and standalone, so it is edited directly.

- [ ] **Step 1: Apply R6, R7, R8 and R10**

```bash
git ls-files -z -- ':!:assets/brand/*' ':!:docs/superpowers/*' \
  | xargs -0 grep -lIZ -e 'drSimpleUi' -e 'drui\.' -e 'drsimpleui' -e 'dr-ui\.test' \
  | xargs -0 perl -pi \
      -e 's{drsimpleui://}{sednaui://}g;' \
      -e 's/drSimpleUi/sednaUi/g;' \
      -e 's/drui\./sedna./g;' \
      -e 's/dr-ui\.test/sedna-ui.test/g;'
```

- [ ] **Step 2: Verify**

```bash
git ls-files -z -- ':!:docs/superpowers/*' \
  | xargs -0 grep -nI -e 'drSimpleUi' -e 'drui\.' -e 'drsimpleui://' -e 'dr-ui\.test'
```

Expected: no output.

**The pattern is `drsimpleui://`, not bare `drsimpleui`.** A bare `drsimpleui` also
matches the docker tag `drsimpleui-catalogue` in `.github/workflows/ci.yml` and
`src/Sedna.UI.Catalogue/Dockerfile`, which rule R5 assigns to **Task 8**. The first
execution of this task hand-fixed that tag before catching the ownership in the spec
and reverting it. Leave both files untouched here.

- [ ] **Step 3: Confirm the two storage-prefix defaults still agree**

`ScriptContractTests` asserts it, but the failure message is clearer read directly.

```bash
grep -n "storagePrefix" src/Sedna.UI/js-parts/00-core.js
grep -n "prefix = " src/Sedna.UI/wwwroot/js/Sedna.UI.boot.js
grep -n "StoragePrefix" src/Sedna.UI/Interop/SednaUiOptions.cs
```

Expected: `storagePrefix: 'sedna.'`, `|| 'sedna.'`, and `= "sedna.";`.

- [ ] **Step 4: Rename the guard test whose name states the old identifier**

In `src/Sedna.UI.Tests/Packaging/ScriptContractTests.cs`, rename the method:

```csharp
    [Fact]
    public void The_javascript_global_is_sednaUi()
```

R7 already rewrote its body's `window.sednaUi` assertion; only the method name carries the old identifier, because it was written `drSimpleUi` and R7 rewrote it to `sednaUi` — confirm the name reads `The_javascript_global_is_sednaUi` and fix it by hand if not.

- [ ] **Step 5: Regenerate the script**

```bash
bash build/bundle-js.sh
grep -n 'window.sednaUi' src/Sedna.UI/wwwroot/js/Sedna.UI.js
```

- [ ] **Step 6: Run the gate**

```bash
dotnet build Sedna.UI.slnx -c Debug
DR_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Debug --no-build
```

Expected: PASS. A `BootThemeTests` failure means the boot script and the main script disagree on the prefix, so a stored theme is not found on reload.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Rename the JavaScript global to sednaUi and the storage prefix to sedna."
```

---

### Task 7: Guard against the old brand returning

**Files:**
- Create: `src/Sedna.UI.Tests/Packaging/BrandNamingTests.cs`
- Create: `src/Sedna.UI.Catalogue.Tests/BrandNamingTests.cs`
- Test: both of the above

**Interfaces:**
- Consumes: Tasks 1–6 complete, so nothing carries an old identifier. Uses `Assets.CssPath`/`JsPath`/`BootJsPath`/`TokensPath` from Task 3 and `CatalogueAssets.ContentFiles()`.
- Produces: nothing consumed by later tasks.

**Two files, one per project, deliberately.** CLAUDE.md requires that
`dotnet test src/Sedna.UI.Tests` passes *with the catalogue project deleted*, so a
library test may not enumerate catalogue directories. The catalogue half also gets
`CatalogueAssets.ContentFiles()` for free, which already excludes
`wwwroot/catalogue.css` on purpose — a class named in the docs' own chrome is not a
use of it.

The forbidden `dr-` is matched with a **word boundary** (`\bdr-`), not
`Contains("dr-")`. Without the boundary a token like `--dialog-backdrop-blur` would
be a false positive, and the guard would be unfixable without weakening it.

- [ ] **Step 1: Write the library-side guard**

Create `src/Sedna.UI.Tests/Packaging/BrandNamingTests.cs`:

```csharp
using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// No trace of the pre-Sedna brand survives in anything that ships.
/// </summary>
/// <remarks>
/// The rename moved a class name, a keyframes name, a DOM id, a cascade layer and a
/// storage prefix. Every one of those fails SILENTLY when only one side of a pair
/// moves: an unmatched keyframes name disables an animation, a stale utility class
/// renders unstyled, a stale storage prefix loses a stored setting. This is the cheap
/// check that stops one returning through a copy-pasted part.
///
/// Deliberately not repository-wide: docs/migrating-from-dr-simple-ui.md exists in
/// order to name the old brand.
/// </remarks>
public class BrandNamingTests
{
    /// <summary>
    /// The old brand's markers. <c>dr-</c> carries a word boundary so
    /// <c>--dialog-backdrop</c> and friends are not false positives.
    /// </summary>
    internal static readonly Regex OldBrand = new(
        @"\bdr-|DR\.Simple_UI|DrSimpleUi|drSimpleUi|drui\.|@layer\s+dr\.|\bdr\.(tokens|base|frame|paint|utilities|overrides)\b",
        RegexOptions.Compiled);

    public static IEnumerable<object[]> ShippedAssets() =>
    [
        [Assets.CssPath], [Assets.JsPath], [Assets.BootJsPath], [Assets.TokensPath]
    ];

    [Theory]
    [MemberData(nameof(ShippedAssets))]
    public void No_shipped_asset_carries_the_old_brand(string path)
    {
        var found = OldBrand.Matches(File.ReadAllText(path))
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(found.Count == 0,
            $"{Path.GetFileName(path)} still carries: {string.Join(", ", found)}");
    }
}
```

- [ ] **Step 2: Write the catalogue-side guard**

Create `src/Sedna.UI.Catalogue.Tests/BrandNamingTests.cs`:

```csharp
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// No catalogue example, page or registry entry carries the pre-Sedna brand.
/// </summary>
/// <remarks>
/// An example using a class the stylesheet no longer defines renders UNSTYLED, with
/// no error anywhere. CoverageTests catches the same mistake from the other
/// direction — a sedna-* class no example mentions fails as undocumented — but only
/// while no other example happens to use it.
/// </remarks>
public class BrandNamingTests
{
    [Fact]
    public void No_catalogue_content_file_carries_the_old_brand()
    {
        var offenders = new List<string>();

        foreach (var file in CatalogueAssets.ContentFiles())
        {
            var found = Sedna.UI.Tests.BrandNamingTests.OldBrand
                .Matches(File.ReadAllText(file))
                .Select(m => m.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (found.Count > 0)
                offenders.Add(
                    $"{Path.GetRelativePath(Assets.RepoRoot, file)}: {string.Join(", ", found)}");
        }

        Assert.True(offenders.Count == 0,
            "The old brand survives in: " + string.Join("; ", offenders));
    }
}
```

`OldBrand` is `internal` and the catalogue tests are not an `InternalsVisibleTo`
target of the library's test project. If the reference does not resolve, copy the
regex literal into the catalogue file with a comment naming the other copy — two
copies of one regex is the lesser evil against making a test project's internals
public.

- [ ] **Step 3: Run both — they must pass, because Tasks 5 and 6 removed the names**

```bash
dotnet test src/Sedna.UI.Tests           -c Debug --filter BrandNamingTests
dotnet test src/Sedna.UI.Catalogue.Tests -c Debug --filter BrandNamingTests
```

Expected: PASS. A failure here is a genuine leftover — read the named file rather than weakening the regex.

- [ ] **Step 4: Confirm the library suite still passes with the catalogue absent**

The whole reason this is two files. Task 8 has not renamed the environment variable yet.

```bash
DR_UI_BROWSER_TESTS=1 dotnet test src/Sedna.UI.Tests -c Debug
```

Expected: PASS.

- [ ] **Step 5: Prove each guard can actually fail**

A guard that passes without ever being able to fail is worse than none. Also confirms the word boundary works — the second injection must NOT trip it.

**Inject into a css-part and regenerate — never append to the generated bundle.** The Global Constraint holds even for a throwaway check, and going through the generator is also the more honest test: it proves the guard catches a violation arriving the way a real one would.

```bash
printf '\n/* dr-row */\n' >> src/Sedna.UI/css-parts/80-utilities.css
bash build/bundle-css.sh
dotnet test src/Sedna.UI.Tests -c Debug --filter BrandNamingTests
```

Expected: FAIL, naming `Sedna.UI.css` and `dr-`.

```bash
git checkout -- src/Sedna.UI/css-parts/80-utilities.css
printf '\n/* --dialog-backdrop-blur is not a violation */\n' >> src/Sedna.UI/css-parts/80-utilities.css
bash build/bundle-css.sh
dotnet test src/Sedna.UI.Tests -c Debug --filter BrandNamingTests
```

Expected: PASS. If this fails, the boundary is wrong and the regex needs fixing rather than the file.

- [ ] **Step 6: Prove the catalogue guard can fail**

```bash
bash build/bundle-css.sh
printf '\n<!-- dr-row -->\n' >> src/Sedna.UI.Catalogue/Examples/Utility/FlexRow.razor
dotnet test src/Sedna.UI.Catalogue.Tests -c Debug --filter BrandNamingTests
```

Expected: FAIL, naming that example. If `Examples/Utility/FlexRow.razor` does not exist, use any file returned by `bash -c 'ls src/Sedna.UI.Catalogue/Examples/*/*.razor | head -1'`.

- [ ] **Step 7: Restore everything and re-confirm green**

```bash
git checkout -- src/Sedna.UI/css-parts src/Sedna.UI.Catalogue/Examples
bash build/bundle-css.sh
git status --porcelain src/Sedna.UI src/Sedna.UI.Catalogue   # expect empty
dotnet test src/Sedna.UI.Tests           -c Debug --filter BrandNamingTests
dotnet test src/Sedna.UI.Catalogue.Tests -c Debug --filter BrandNamingTests
```

Expected: empty status — the regenerated bundle must be byte-identical to the committed one — and both tests PASS.

- [ ] **Step 8: Commit**

```bash
git add src/Sedna.UI.Tests/Packaging/BrandNamingTests.cs src/Sedna.UI.Catalogue.Tests/BrandNamingTests.cs
git commit -m "Guard against the pre-Sedna brand returning"
```

---

### Task 8: Docs, CI, infrastructure and the domain

**Files:**
- Modify: `README.md`, `CLAUDE.md`, `CONTRIBUTING.md`, `THIRD-PARTY-NOTICES.md`, `.gitattributes`, `docs/architecture.md`, `docs/getting-started.md`, `docs/releasing.md`, `docs/development.md`, `docs/accessibility.md`, `docs/CLAUDE.consuming-app.md`, `src/Sedna.UI/css-parts/CLAUDE.md`, `src/Sedna.UI/js-parts/CLAUDE.md`, `src/Sedna.UI.Catalogue/CLAUDE.md`, `.github/workflows/ci.yml`, `.github/workflows/codeql.yml`, `.github/workflows/release.yml`, `src/Sedna.UI.Catalogue/Dockerfile`, `railway.json`, `src/Sedna.UI/Sedna.UI.csproj`, `src/Sedna.UI.Catalogue/Navigation/CatalogueLinks.cs`, `src/Sedna.UI.Catalogue/Examples/Mcp/ClaudeCode.txt`, `src/Sedna.UI.Catalogue/Examples/Mcp/Config.txt`, `src/Sedna.UI/css-parts/30-cards.css`, `src/Sedna.UI.Catalogue.Tests/ExampleSourceTests.cs`, `src/Sedna.UI.Catalogue/Components/Layout/CatalogueTopbar.razor`
- Create: `docs/migrating-from-dr-simple-ui.md`
- Test: `Packaging/DocumentationTests.cs`, `Catalogue.Tests/ExampleSourceTests.cs`, `Catalogue.Tests/Mcp/McpToolTests.cs`

**Interfaces:**
- Consumes: Tasks 1–7.
- Produces: `https://www.sedna-ui.com/` as the canonical URL; `SEDNA_UI_BROWSER_TESTS` as the browser-test switch. The gate's environment variable changes from this task onward.

- [ ] **Step 1: Apply R2, R3 and R5, and swap the domain**

```bash
git ls-files -z -- ':!:assets/brand/*' ':!:docs/superpowers/*' \
  | xargs -0 grep -lIZ -e 'DR_UI_BROWSER_TESTS' -e 'dr\.simple_ui' -e 'drsimpleui-catalogue' -e 'simpleui\.dennisrahmen\.dev' \
  | xargs -0 perl -pi \
      -e 's/DR_UI_BROWSER_TESTS/SEDNA_UI_BROWSER_TESTS/g;' \
      -e 's/dr\.simple_ui/sedna.ui/g;' \
      -e 's/drsimpleui-catalogue/sedna-ui-catalogue/g;' \
      -e 's{simpleui\.dennisrahmen\.dev}{www.sedna-ui.com}g;'
```

- [ ] **Step 1a: Rename the MCP server name users register**

`Examples/Mcp/ClaudeCode.txt` and `Examples/Mcp/Config.txt` name the server
`dr-simple-ui`. That is **not** a brand filename — it is the identifier a user types
into their MCP client — so it belongs here, beside the URL Step 1 just changed, not
in the brand task. R11's `(?!simple-ui)` lookahead shielded it in Task 5, and R12
in Task 10 would eventually rename it by accident; but Task 10 is blocked on
artwork, and leaving it would ship an example telling users to register a server
called `dr-simple-ui` pointing at the new domain.

```bash
perl -pi -e 's/\bdr-simple-ui\b/sedna-ui/g' \
  src/Sedna.UI.Catalogue/Examples/Mcp/ClaudeCode.txt \
  src/Sedna.UI.Catalogue/Examples/Mcp/Config.txt
cat src/Sedna.UI.Catalogue/Examples/Mcp/ClaudeCode.txt src/Sedna.UI.Catalogue/Examples/Mcp/Config.txt
```

Expected: `claude mcp add --transport http sedna-ui https://www.sedna-ui.com/mcp`, and
the JSON key `"sedna-ui"`. Both files are embedded and printed by the catalogue, so
`ExampleSourceTests` covers them.

Scope this to those two files by name. A broader `dr-simple-ui` sweep would hit the
brand filenames, which must not move until Task 10.

- [ ] **Step 1b: Delete the exception list this rename makes dead**

Task 7's catalogue guard carries a `PendingRenameFiles` array excluding exactly these
two files, because the guard had to be green before this rename existed. Once Step 1a
lands the array excludes nothing, and a dead allowlist is how a real violation hides
later.

Delete the `PendingRenameFiles` field and the `if (PendingRenameFiles.Any(…)) continue;`
guard clause from `src/Sedna.UI.Catalogue.Tests/BrandNamingTests.cs`, along with its
`<remarks>` block. Then confirm the guard still passes on its own merits:

```bash
dotnet test src/Sedna.UI.Catalogue.Tests -c Debug --filter BrandNamingTests
```

Expected: PASS with nothing excluded. If it fails, Step 1a missed an occurrence —
fix the occurrence, do not restore the array.

- [ ] **Step 2: Point the GitHub URLs at the renamed repository**

```bash
git ls-files -z -- ':!:docs/superpowers/*' \
  | xargs -0 grep -lIZ 'dennisrahmen/DR\.Simple_UI' \
  | xargs -0 perl -pi -e 's{dennisrahmen/DR\.Simple_UI}{dennisrahmen/Sedna.UI}g'
```

This covers `RepositoryUrl`, the seven README badge URLs, the README hero's `raw.githubusercontent.com` URL and the `CONTRIBUTING.md` issue link.

- [ ] **Step 3: Recolour the README badges from the old brand blue to Sedna Red**

> **STOP. `2563eb` is a live token value.** It is the value of `--brand` and
> friends in `css-parts/01-tokens.css`, `02-theme-light.css` and
> `03-theme-colour-blind.css`, and therefore in the generated stylesheet and the
> generated tokens JSON. A repo-wide `s/2563eb/FF6B4A/` would change the default
> theme and violate the Global Constraint that no token value changes.
> **This command must never lose its `README.md` argument.**

```bash
grep -c '2563eb' README.md          # expect 6 — six of the seven badges carry a colour
perl -pi -e 's/2563eb/FF6B4A/g' README.md
grep -c 'FF6B4A' README.md          # expect 6
git diff --stat                      # expect README.md and nothing else
```

The CI badge has no colour parameter, which is why the count is 6 and not 7.

`src/Sedna.UI.Catalogue/wwwroot/catalogue.css` also carries one `2563eb`, in the
docs site's own chrome. Leave it: recolouring the catalogue is a visual change
nobody asked for, and the instruction was to record the palette, not apply it.
Note it as a deferred minor.

- [ ] **Step 4: Remove the stale sentence from the package description**

In `src/Sedna.UI/Sedna.UI.csproj`, the `<Description>` still ends with "Ships a copy-pasteable HTML catalogue inside the package." That stopped being true when the catalogue became a hosted app. Delete that sentence, leaving the rest of the description unchanged.

- [ ] **Step 5: Update the trusted-publishing comment in `release.yml`**

The comment block documents the policy that must exist on nuget.org. Change the `Repository` line to `Sedna.UI`:

```yaml
      #     Repository Owner  dennisrahmen
      #     Repository        Sedna.UI
      #     Workflow File     release.yml      <- THIS FILE'S NAME
      #     Environment       (empty)
```

Do not rename the file.

- [ ] **Step 6: Confirm `railway.json` watch patterns moved**

A stale pattern silently stops triggering redeploys.

```bash
grep -n 'src/' railway.json
```

Expected: `src/Sedna.UI.Catalogue/Dockerfile`, `src/Sedna.UI/**`, `src/Sedna.UI.Catalogue/**`.

- [ ] **Step 7: Write the migration guide**

Create `docs/migrating-from-dr-simple-ui.md`. It is embedded by `..\..\docs\*.md` and served by the **public, unauthenticated** MCP server, so write it for a consuming developer or agent, not as an internal note.

Generate the class list first — do not type it:

```bash
bash build/release-inventory.sh v0.1.0 --notes
```

Write the document to this structure, in this order:

```markdown
# Migrating from DR.Simple_UI

`DR.Simple_UI` is now `Sedna.UI`. The package ID, the namespace, the asset paths,
the JavaScript global, the CSS utility prefix, the cascade layers and the
`localStorage` prefix all change. There are no aliases and no compatibility
shims: a shim is a second code path nobody tests, and it outlives the migration
it was written for.

## 1. The package

<the `dotnet remove` / `dotnet add` pair>

## 2. The host page

<the old five asset lines, then the new five — copied from docs/getting-started.md>

## 3. Identifiers

<table: package, namespace, SednaUi, ISednaUi, SednaUiOptions, SednaUiSettings,
AddSednaUi, window.sednaUi, the six cascade layers>

## 4. CSS classes

<the dr- -> sedna- table from release-inventory.sh. Only the prefixed utilities
moved; semantic names such as .card and .badge-go are unchanged.>

## 5. Stored settings are lost — read this

<the `drui.` -> `sedna.` change, what each key held, and that re-reading the old
keys is deliberately not implemented>

## 6. Cascade layers

<only relevant to an app that addresses a layer by name>
```

Two rules bind this file specifically:

- **It is published.** Written for a consuming developer or agent, not as an internal note.
- **Do not state a count of the classes in prose.** The table is the count. `.gitattributes` and the repo's own convention exist because typed counts go stale in silence.

- [ ] **Step 8: Verify no old-brand string survives outside its two legitimate homes**

```bash
git ls-files -z | xargs -0 grep -lI -e 'DR\.Simple_UI' -e 'simpleui\.dennisrahmen\.dev' -e 'DR_UI_BROWSER_TESTS' \
  | grep -v '^assets/brand/' | grep -v '^docs/superpowers/' | grep -v '^docs/migrating-from-dr-simple-ui.md$'
```

Expected: no output.

- [ ] **Step 9: Run the gate with the new variable name**

```bash
dotnet build Sedna.UI.slnx -c Debug
SEDNA_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Debug --no-build
```

Expected: PASS. If the browser tests report as skipped, the workflows and `BrowserTestBase` disagree on the variable name.

- [ ] **Step 10: Commit**

```bash
git add -A
git commit -m "Point the docs, CI and deploy config at Sedna.UI and www.sedna-ui.com"
```

---

### Task 9: Full verification

**Files:** none modified — this task only runs things.

**Interfaces:**
- Consumes: Tasks 1–8.
- Produces: a green branch ready for Task 10 and the PR.

- [ ] **Step 1: Clean build from scratch, Release configuration, as CI does**

```bash
rm -rf artifacts
find src -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +
dotnet restore Sedna.UI.slnx
dotnet build Sedna.UI.slnx -c Release --no-restore
```

- [ ] **Step 2: Install the browser and run the full suite in Release**

```bash
pwsh src/Sedna.UI.Tests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
SEDNA_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Release --no-build --verbosity normal
```

Expected: PASS, with the browser tests asserting rather than skipping.

- [ ] **Step 3: Confirm the library's suite still passes with the catalogue absent**

CLAUDE.md states this is the honest statement of the split.

```bash
SEDNA_UI_BROWSER_TESTS=1 dotnet test src/Sedna.UI.Tests -c Release --no-build
```

- [ ] **Step 4: Pack and verify the package**

```bash
dotnet pack src/Sedna.UI/Sedna.UI.csproj -c Release --no-build -o artifacts
bash build/verify-package.sh artifacts/Sedna.UI.0.1.0.nupkg
unzip -l artifacts/Sedna.UI.0.1.0.nupkg | grep -E 'staticwebassets|lib/net10.0'
```

Expected: `verify-package.sh` green; the listing shows `staticwebassets/css/Sedna.UI.css`, `staticwebassets/js/Sedna.UI.js`, `staticwebassets/js/Sedna.UI.boot.js`, `staticwebassets/tokens/Sedna.UI.tokens.json`, `lib/net10.0/Sedna.UI.dll`.

- [ ] **Step 5: Confirm both generated-artefact checks and the class history**

```bash
bash build/bundle-css.sh && bash build/bundle-js.sh && bash build/export-tokens.sh
git diff --stat src/Sedna.UI/wwwroot        # expect no diff
bash build/class-history.sh --check
bash build/release-inventory.sh v0.1.0 --notes
```

Expected: no diff, `--check` ok, and a notes-ready added/removed listing for the release.

- [ ] **Step 6: Build the container image, as CI does**

```bash
docker build -f src/Sedna.UI.Catalogue/Dockerfile -t sedna-ui-catalogue:local .
```

Expected: success. The build context is the repository root.

- [ ] **Step 7: Run the catalogue and look at it**

Use the Browser pane, not a shell. `preview_start` with `.claude/launch.json`, then walk the pages, watching the console. Every utility class changed, and a stale one renders unstyled with no error. Check specifically: a page using the grid utilities, the command palette (`sedna-palette-*` ids), the search panel, a toast, a hover hint, and the collapsed-rail flyout.

- [ ] **Step 8: Commit anything the regeneration touched**

```bash
git status --short
git add -A && git commit -m "Regenerate after the rename" || echo "nothing to commit"
```

---

### Task 10: Brand assets — BLOCKED

**Blocked on:** a ≥1024 square transparent icon PNG, a white-wordmark lockup ≥1200px wide, `sedna-ui-icon.svg`, and vector lockups. Fallbacks are recorded in the spec at §6.3 and the choice between "Claude authors the SVG" and "drop the vectors" is Dennis's.

Do not start this task by guessing. If the artwork has not arrived, stop after Task 9 and report.

**Files (when unblocked):**
- Move/replace: every file in `assets/brand/`
- Modify: `src/Sedna.UI/Sedna.UI.csproj` (`PackageIcon` `None Include`), `README.md` (hero filename), `src/Sedna.UI.Tests/Packaging/BrandAssetTests.cs`, `assets/brand/README.md`
- Copy: `assets/brand/favicon.ico` → `src/Sedna.UI.Catalogue/wwwroot/favicon.ico`; `assets/brand/sedna-ui-icon-64.png` → `src/Sedna.UI.Catalogue/wwwroot/logo.png`, both byte-identical

- [ ] **Step 1: Apply R12 to the remaining references**

```bash
git ls-files -z -- ':!:docs/superpowers/*' \
  | xargs -0 grep -lIZ 'dr-simple-ui' \
  | xargs -0 perl -pi -e 's/dr-simple-ui/sedna-ui/g'
```

- [ ] **Step 2: Produce the raster set from the supplied masters**

Pillow 12.3.0 is available; there is no ImageMagick and no Inkscape on this machine. `getbbox()` is useless on these files — a faint wide glow spans the canvas — so the content box is found at an alpha threshold.

Write this to the scratchpad, not the repository. It is a one-shot derivation, not a generator worth keeping.

```python
"""Derive the Sedna raster brand set from the supplied masters."""
from pathlib import Path
from PIL import Image

SRC = Path.home() / "Downloads" / "SednaUI-brand-assets"
OUT = Path("assets/brand")
ICE_WHITE = (248, 250, 252)          # #F8FAFC


def trimmed(path, thresh=8):
    """The image cropped to its content, ignoring the faint glow."""
    im = Image.open(path).convert("RGBA")
    mask = im.getchannel("A").point(lambda v: 255 if v >= thresh else 0)
    box = mask.getbbox()
    if box is None:
        raise SystemExit(f"{path} is fully transparent at threshold {thresh}")
    return im.crop(box)


def squared(im):
    """Centre the content on a transparent square canvas."""
    side = max(im.size)
    out = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    out.paste(im, ((side - im.width) // 2, (side - im.height) // 2))
    return out


# ── icon ladder ───────────────────────────────────────────────────────────────
# Use the >=1024 master Dennis supplied if it is there; otherwise the 610px one,
# and drop the 1024 rung rather than upscale into it.
master = SRC / "sednaui_icon_1024.png"
icon = squared(trimmed(master if master.exists() else SRC / "sednaui_logo.png"))
sizes = [1024, 512, 256, 128, 64, 48, 32, 16]
if icon.width < 1024:
    print(f"icon master is {icon.width}px — dropping the 1024 rung")
    sizes.remove(1024)

for n in sizes:
    icon.resize((n, n), Image.LANCZOS).save(OUT / f"sedna-ui-icon-{n}.png")

# ── favicon ───────────────────────────────────────────────────────────────────
icon.resize((256, 256), Image.LANCZOS).save(
    OUT / "favicon.ico", sizes=[(16, 16), (32, 32), (48, 48)])

# ── lockups ───────────────────────────────────────────────────────────────────
light = trimmed(SRC / "sednaui_logotext.png")
light.save(OUT / "sedna-ui-logo-light.png")

white = SRC / "sednaui_logotext_white.png"
if white.exists():
    trimmed(white).save(OUT / "sedna-ui-logo-dark.png")
else:
    # Recolour the navy wordmark to Ice White, alpha preserved, mark untouched.
    # The mark ends and the text begins at the widest fully-transparent column
    # gap, measured at 547..588 in the supplied file.
    print("no white lockup supplied — recolouring the navy wordmark")
    dark = light.copy()
    px = dark.load()
    gap = None
    for x in range(dark.width):
        if all(px[x, y][3] <= 16 for y in range(dark.height)):
            gap = x
        elif gap is not None and x > dark.width // 4:
            break
    split = gap or dark.width // 3
    for x in range(split, dark.width):
        for y in range(dark.height):
            r, g, b, a = px[x, y]
            if a:
                px[x, y] = (*ICE_WHITE, a)
    dark.save(OUT / "sedna-ui-logo-dark.png")

# ── social preview, 1280x640 ──────────────────────────────────────────────────
bg = Image.open(SRC / "sednaui_background_dark.png").convert("RGBA")
scale = max(1280 / bg.width, 640 / bg.height)
bg = bg.resize((round(bg.width * scale), round(bg.height * scale)), Image.LANCZOS)
hero = Image.new("RGBA", (1280, 640))
hero.paste(bg, ((1280 - bg.width) // 2, (640 - bg.height) // 2))

lock = Image.open(OUT / "sedna-ui-logo-dark.png")
w = round(1280 * 0.62)
lock = lock.resize((w, round(lock.height * w / lock.width)), Image.LANCZOS)
hero.alpha_composite(lock, ((1280 - lock.width) // 2, (640 - lock.height) // 2))
hero.convert("RGB").save(OUT / "sedna-ui-social-preview.png")

# ── the catalogue's two byte-identical copies ─────────────────────────────────
cat = Path("src/Sedna.UI.Catalogue/wwwroot")
(cat / "favicon.ico").write_bytes((OUT / "favicon.ico").read_bytes())
(cat / "logo.png").write_bytes((OUT / "sedna-ui-icon-64.png").read_bytes())
print("done")
```

Then remove the superseded files and record the two supplied backgrounds:

```bash
git rm assets/brand/dr-simple-ui-*.png assets/brand/dr-simple-ui-*.svg
cp ~/Downloads/SednaUI-brand-assets/sednaui_background_light.png assets/brand/sedna-ui-background-light.png
cp ~/Downloads/SednaUI-brand-assets/sednaui_background_dark.png  assets/brand/sedna-ui-background-dark.png
git add assets/brand
```

The `.svg` files are removed here only if Dennis chose the raster-only fallback. If he supplied vectors, `git mv` each into its `sedna-ui-*` name instead. If he asked Claude to author `sedna-ui-icon.svg`, that is its own step before this one.

- [ ] **Step 2a: Look at the small sizes before believing them**

A downscaled detailed illustration turns to mud. Open `sedna-ui-icon-16.png` and `-32.png` and check the orange sphere still reads. If it does not, say so rather than shipping it — that is the argument for a simplified small-size icon.

- [ ] **Step 3: Confirm the byte-identical copies and the package icon size**

```bash
cmp assets/brand/favicon.ico src/Sedna.UI.Catalogue/wwwroot/favicon.ico
cmp assets/brand/sedna-ui-icon-64.png src/Sedna.UI.Catalogue/wwwroot/logo.png
python -c "from PIL import Image; im=Image.open('assets/brand/sedna-ui-icon-128.png'); print(im.size); assert max(im.size)<=128"
```

- [ ] **Step 4: Record the palette in `assets/brand/README.md`**

Replace the claim that the brand colours come from the package's default design tokens. State the six Sedna colours, and state explicitly that the library's default tokens are unchanged and that applying the palette to the default theme is a separate later change.

- [ ] **Step 5: Run the gate, pack, and verify**

```bash
dotnet build Sedna.UI.slnx -c Debug
SEDNA_UI_BROWSER_TESTS=1 dotnet test Sedna.UI.slnx -c Debug --no-build
dotnet pack src/Sedna.UI/Sedna.UI.csproj -c Release -o artifacts
bash build/verify-package.sh artifacts/Sedna.UI.0.1.0.nupkg
unzip -p artifacts/Sedna.UI.0.1.0.nupkg icon.png | wc -c   # expect non-zero
```

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Replace the brand assets and record the Sedna palette"
```

---

## After the plan

These are not implementation tasks and are gated on Dennis, in this order. The README hero URL resolves only once the repository is renamed **and** the branch is on `main`, so the tag comes last.

1. Open the PR from `rebrand/sedna-ui`, review, merge to `main`.
2. ~~Rename the GitHub repository and update the local `origin` URL.~~ **DONE
   2026-08-04, ahead of the merge.** `dennisrahmen/Sedna.UI`; description is now
   "One design-token contract. Semantic CSS. Consistent Blazor apps."; homepage
   `https://www.sedna-ui.com`; `origin` repointed and verified reaching `main`.
   GitHub redirects the old path, and the branch was local-only at the time, so
   nothing published depended on the old name.

   The ordering originally written here — rename *after* the merge — was wrong.
   Both the rename and the merge must precede the **tag**, because the nuget.org
   README hero loads `raw.githubusercontent.com/dennisrahmen/Sedna.UI/main/…`.
   Their order relative to each other does not matter, and renaming first makes the
   branch's seven badge URLs resolve instead of 404.

   **Still open on the repository, and Dennis's call:** GitHub Pages is enabled
   (`has_pages: true`) and still serving the old, old-branded site. Disabling it
   takes a live site down, so it is a decision rather than a step. The new homepage
   404s until the Hetzner CNAME for `www.sedna-ui.com` resolves.
3. **Dennis:** move the nuget.org trusted-publishing policy to repository `Sedna.UI`. Skipping this fails `release.yml` at *Exchange OIDC token*, after a full green build.
4. Draft the release notes: level Major shipping as `0.2.0`, every added and removed class name from `build/release-inventory.sh`, the identifier table, and the stored-state loss. Confirm with Dennis.
5. `git tag -a v0.2.0 -F notes.md && git push origin v0.2.0`, with `notes.md` written outside the repository.
6. After the tag: re-run `bash build/class-history.sh` and commit, so the `sedna-*` classes move from `null` to `0.2.0`.
7. **Dennis:** deprecate `DR.Simple_UI` 0.1.0 on nuget.org pointing at `Sedna.UI`; DNS for `www.sedna-ui.com` — the records Railway lists for the domain (one CNAME as of the 2026-08-04 setup), remembering that Hetzner DNS appends the zone to an unqualified CNAME value so the target needs its trailing dot; upload the social preview in GitHub repository settings.

# Rebrand `DR.Simple_UI` → `Sedna.UI`

**Date:** 2026-08-04
**Status:** approved, not yet implemented
**Release it ships in:** `0.2.0`

Rename the library, its namespace, every prefix it owns, its brand assets, its
hosted domain and its repository. The default theme's token *values* do not
change: the new palette is recorded as brand documentation only.

All measurements in this document were taken against `main` at `241c891` on
2026-08-04 and are snapshots, not live claims.

---

## 1. Scope

In scope: package ID, assembly, namespace, public C# type names, the JavaScript
global, the CSS utility namespace, the cascade layer namespace, the
`localStorage` prefix, shipped asset filenames and paths, brand assets, build
scripts, guard tests, the catalogue application and its MCP server, docs, CI,
the container build, the Railway deploy config, the hosted domain, and the
GitHub repository name.

Out of scope, deliberately:

- **Token values.** The 159 design tokens keep their current values, so the
  default theme renders byte-identically. Applying the Sedna palette to the
  default theme is a separate, later change.
- **Token names.** Tokens are unprefixed today (`--brand`, `--bg`, `--fg`) and
  stay unprefixed.
- **Semantic class names.** `.card`, `.badge-go`, `.btn-warn` carry no brand and
  do not move. Only the `dr-` utility namespace does.
- **Remix Icon.** The vendored `ri-` prefix is upstream's and is untouched.
- **Consuming applications.** `AI_Console` / Athene consumes `DR.Simple_UI`
  0.1.0 from a different repository. It is not migrated here.

## 2. Decisions

| Decision | Choice | Decided by |
|---|---|---|
| Package / namespace | `Sedna.UI` | Dennis |
| Prefix word | `Sedna` | Dennis |
| CSS utility namespace | `sedna-` | Dennis |
| JavaScript global | `window.sednaUi` | Dennis |
| Version of the first Sedna release | `0.2.0` | Dennis |
| GitHub repo rename | Claude, via the GitHub API, confirmed immediately before the call | Dennis |
| Canonical URL | `https://www.sedna-ui.com/` (`www` subdomain) | Dennis |
| Palette | recorded as brand documentation; no theme change yet | Dennis |
| Sequencing | four staged commits on one branch, merged as one PR | Dennis |
| Old package | not aliased, not shimmed — clean break, documented | CLAUDE.md |

`Sedna.UI` was confirmed free on nuget.org: the flat-container index returns 404
and an exact-ID search returns `totalHits: 0`.

## 3. The identifier contract

### 3.1 Project and package

| Old | New |
|---|---|
| `DR.Simple_UI` | `Sedna.UI` |
| `DR.Simple_UI.slnx` | `Sedna.UI.slnx` |
| `src/DR.Simple_UI/` | `src/Sedna.UI/` |
| `src/DR.Simple_UI.Tests/` | `src/Sedna.UI.Tests/` |
| `src/DR.Simple_UI.Catalogue/` | `src/Sedna.UI.Catalogue/` |
| `src/DR.Simple_UI.Catalogue.Tests/` | `src/Sedna.UI.Catalogue.Tests/` |

`PackageId`, `AssemblyName`, `RootNamespace` and `Title` all become `Sedna.UI`.
Directory moves use `git mv` so history follows.

The namespace keeps its dot, so the sub-namespaces map straight across:
`DR.Simple_UI.Interop` → `Sedna.UI.Interop`, `DR.Simple_UI.Navigation` →
`Sedna.UI.Navigation`, `DR.Simple_UI.Tests.TestSupport` →
`Sedna.UI.Tests.TestSupport`, and so on.

### 3.2 Shipped asset paths

Consuming apps hard-code these, which is why `Packaging/ShippedPathTests` pins
them.

| Old | New |
|---|---|
| `_content/DR.Simple_UI/css/DR.Simple_UI.css` | `_content/Sedna.UI/css/Sedna.UI.css` |
| `_content/DR.Simple_UI/js/DR.Simple_UI.js` | `_content/Sedna.UI/js/Sedna.UI.js` |
| `_content/DR.Simple_UI/js/DR.Simple_UI.boot.js` | `_content/Sedna.UI/js/Sedna.UI.boot.js` |
| `_content/DR.Simple_UI/tokens/DR.Simple_UI.tokens.json` | `_content/Sedna.UI/tokens/Sedna.UI.tokens.json` |
| `_content/DR.Simple_UI/lib/remixicon/…` | `_content/Sedna.UI/lib/remixicon/…` |

The `"name"` field `export-tokens.sh` writes into the tokens JSON becomes
`Sedna.UI`.

### 3.3 C# public surface

| Old | New |
|---|---|
| `DrSimpleUi` | `SednaUi` |
| `IDrSimpleUi` | `ISednaUi` |
| `DrSimpleUiOptions` | `SednaUiOptions` |
| `DrSimpleUiSettings` | `SednaUiSettings` |
| `AddDrSimpleUi()` | `AddSednaUi()` |
| `DrSimpleUiServiceCollectionExtensions` | `SednaUiServiceCollectionExtensions` |

File names follow the type names. `ActiveLink`, `PaletteCommand`, `SearchItem`
and `ToastKind` are unchanged.

### 3.4 JavaScript

| Old | New |
|---|---|
| `window.drSimpleUi` | `window.sednaUi` |
| `window.drSimpleUiCatalogue` | `window.sednaUiCatalogue` |

The catalogue global belongs to the catalogue application, not the package.

### 3.5 CSS utility namespace

36 classes, measured with `build/css-inventory.sh`:

```
dr-baseline dr-between dr-busy dr-center dr-col dr-disabled
dr-divider-bottom dr-divider-top dr-end dr-fill dr-gap-0 dr-gap-1
dr-gap-2 dr-gap-3 dr-invisible dr-mb-0 dr-mb-1 dr-mb-2 dr-mb-3
dr-mt-0 dr-mt-1 dr-mt-2 dr-mt-3 dr-no-print dr-print-only dr-push
dr-row dr-row-wrap dr-scroll dr-scroll-x dr-shrink-0 dr-start
dr-tip dr-tip--visible dr-w-full dr-wrap
```

Each becomes `sedna-<rest>`, preserving the modifier convention
(`dr-tip--visible` → `sedna-tip--visible`).

### 3.6 Cascade layer namespace

```css
@layer dr.tokens, dr.base, dr.frame, dr.paint, dr.utilities, dr.overrides;
   →
@layer sedna.tokens, sedna.base, sedna.frame, sedna.paint, sedna.utilities, sedna.overrides;
```

The layer for a part is derived from its `NN-` prefix by `layer_for()` in
`build/bundle-css.sh`; that function holds the only copy of the mapping and is
the only place the names need editing. The layer *order* and the
prefix-to-layer boundaries do not change, so no part moves between layers.

An app can address a layer by name (`@layer dr.overrides { … }`), so this is a
breaking rename like the others and goes in the release notes.

### 3.7 Storage, config and miscellaneous

| Old | New |
|---|---|
| `localStorage` prefix `drui.` | `sedna.` |
| language cookie `drui.lang` | `sedna.lang` |
| `SednaUiOptions.StoragePrefix` default | `sedna.` |
| `data-prefix` default on the boot script | `sedna.` |
| env var `DR_UI_BROWSER_TESTS` | `SEDNA_UI_BROWSER_TESTS` |
| MCP resource URIs `drsimpleui://…` | `sednaui://…` |
| test origin `https://dr-ui.test` | `https://sedna-ui.test` |
| docker tag `drsimpleui-catalogue` | `sedna-ui-catalogue` |
| brand files `dr-simple-ui-*` | `sedna-ui-*` |

The boot script's `data-prefix` and the main script's `storagePrefix` must stay
equal — `ScriptContractTests` asserts it.

## 4. Two defects the rename exposes

`build/class-history.sh` and `build/release-inventory.sh` both read the
stylesheet **out of a git tag** using the same path variable they use for the
working tree:

```bash
sheet="src/DR.Simple_UI/wwwroot/css/DR.Simple_UI.css"
git -C "$root" show "$tag:$sheet" >"$tmp/sheet.css" 2>/dev/null || continue
```

After the rename that path does not exist at `v0.1.0`.

- **`class-history.sh` fails silently.** `|| continue` swallows the error, every
  tag is skipped, and every class and token in `class-history.json` becomes
  `null`. The catalogue would report the entire library as unreleased and the
  MCP server's `since` would be wrong everywhere, with nothing red anywhere.
- **`release-inventory.sh` fails loudly** (`exit 1` with a message), but it is
  the script that derives the release-notes class lists, so it stops working
  exactly when `v0.2.0` needs it.

### 4.1 Fix: `build/css-path.sh`

One new script, the single implementation of "where did the stylesheet live at
this ref", mirroring how `build/css-inventory.sh` is the single implementation
of "what does this stylesheet declare".

```
build/css-path.sh <ref>     echo the stylesheet's path at <ref>
```

It holds a newest-first list of historical paths and echoes the first that
`git cat-file -e` resolves at that ref:

```
src/Sedna.UI/wwwroot/css/Sedna.UI.css
src/DR.Simple_UI/wwwroot/css/DR.Simple_UI.css
```

A future rename adds one line. Both callers use it.

`class-history.sh` additionally gains a guard: if no tag resolved to any path,
it errors instead of writing a fully-nulled file. A fully-nulled file is
internally self-consistent, so `--check` alone cannot detect this — which is
why the guard is needed and why §10 asserts a positive attribution rather than
only running `--check`.

Expected correct output after the fix: unprefixed classes and all tokens keep
`"0.1.0"`; the 36 renamed `sedna-*` utilities report `null` until `v0.2.0` is
tagged, because the name `.sedna-row` genuinely has never shipped.

## 5. Guard tests

`TestSupport/Assets.FindRepoRoot()` walks upward looking for
`DR.Simple_UI.slnx`. Renaming the solution file breaks every test in both
projects until that one line changes. This is a single hinge that fails loudly
and immediately, which is the desired behaviour; it is listed so it is not
mistaken for a broken rename.

Tests that change:

| File | What is pinned |
|---|---|
| `TestSupport/Assets.cs` | solution filename, project dir, four asset paths |
| `TestSupport/CatalogueAssets.cs` | catalogue project dir, `using` of the library tests' namespace |
| `Packaging/ShippedPathTests` | four inline asset paths, the catalogue-absence assertion |
| `Packaging/PackageConfigTests` | `PackageId`, `AssemblyName`, the packable-project list |
| `Packaging/DocumentationTests` | host-page load order, matched by asset filename |
| `Packaging/GeneratedArtefactTests` | tokens path; bundle-vs-parts agreement |
| `Packaging/ScriptContractTests` | `window.drSimpleUi`, **and the test's own name** `The_javascript_global_is_drSimpleUi` |
| `Packaging/BrandAssetTests` | three required brand files; two byte-identical catalogue copies |
| `Css/CascadeLayerTests` | the `dr.*` layer names |
| `Mcp/McpToolTests` | a `dr.*` layer name in expected output |
| `build/verify-package.sh` | eight asserted package paths, the catalogue-absence check |

### 5.1 One new guard

`ScriptContractTests` already asserts the shipped script and stylesheet carry no
application-specific naming. Add the symmetric assertion: no `\.dr-`,
`DR.Simple_UI`, `drSimpleUi`, `drui.` or `@layer dr.` appears in the shipped
stylesheet, the shipped script, the boot script or the tokens JSON — extended
over `src/Sedna.UI.Catalogue/Examples/` and `Components/`, because a stale
`.dr-row` in an example renders unstyled with no error.

Scoped to those paths only. `docs/` must stay free to name the old brand, since
the migration document exists to do exactly that.

Not added now: a reverse-coverage guard asserting every class *used* in an
example exists in the stylesheet. It is the more general form of the same check,
but examples legitimately show app-owned markup, so it would need an ownership
rule of its own. `CoverageTests` already catches the important direction — a
`sedna-*` class no example mentions fails as undocumented.

## 6. Brand assets and the palette

### 6.1 File set

`assets/brand/`, all `dr-simple-ui-*` renamed to `sedna-ui-*`:

```
sedna-ui-icon.svg                       pinned by BrandAssetTests
sedna-ui-icon-{512,256,128,64,48,32,16}.png
sedna-ui-icon-1024.png                  only with a ≥1024 master
sedna-ui-logo-light.{svg,png}
sedna-ui-logo-dark.{svg,png}
sedna-ui-social-preview.{svg,png}       1280×640
sedna-ui-background-{light,dark}.png    new
favicon.ico                             16/32/48
```

### 6.2 Source material and what it can yield

Supplied in `~/Downloads/SednaUI-brand-assets/`, measured 2026-08-04:

| File | Canvas | Opaque content | Notes |
|---|---|---|---|
| `sednaui_logo.png` | 1536×1024 | 610×606, aspect 1.007 | squircle icon; hard alpha, no partial-alpha glow |
| `sednaui_logotext.png` | 1536×1024 | 946×219, aspect 4.32 | horizontal lockup, navy `#0A1C53` |
| `sednaui_background_light.png` | 1536×1024 | opaque | usable as-is |
| `sednaui_background_dark.png` | 1536×1024 | opaque | usable as-is |
| `sednaui_brand_guide.png` | 1491×1055 | opaque | reference sheet, not shipped |

Derivable without new exports: icons 512 and below, `favicon.ico`, the light
lockup, a dark lockup produced by recolouring the navy text pixels to Ice White
with alpha preserved and the mark untouched, and a composed 1280×640 social
preview.

Not derivable: a 1024 icon (the source is 610px, so it would be a fabricated
upscale), any SVG, and a small-size-legible icon.

### 6.3 Requested from Dennis, with fallbacks

Requested: a ≥1024 square transparent icon; a white-wordmark lockup ≥1200px
wide; `sedna-ui-icon.svg`; vector lockups.

Fallbacks if a request cannot be met, so stage 3 is never blocked outright:

| Missing | Fallback |
|---|---|
| ≥1024 icon | ladder tops out at 512; `assets/brand/README.md` says so |
| white lockup | recolour the navy lockup's text to `#F8FAFC`, alpha preserved |
| SVG | either Claude authors `sedna-ui-icon.svg` as a vector reduction of the mark, or the vectors are dropped and `BrandAssetTests`' required list plus `assets/brand/README.md` are amended. Dennis chooses; not decided in advance |

### 6.4 Three wirings that fail quietly

1. `Sedna.UI.csproj` packs `sedna-ui-icon-128.png` as `icon.png` via
   `PackageIcon`. nuget.org requires 128×128 or smaller.
2. The README hero doubles as the nuget.org readme, so it must be an absolute
   `raw.githubusercontent.com` URL — which contains the repository name and
   becomes
   `https://raw.githubusercontent.com/dennisrahmen/Sedna.UI/main/assets/brand/sedna-ui-social-preview.png`.
   `DocumentationTests` asserts the hero URL is absolute.
3. `src/Sedna.UI.Catalogue/wwwroot/favicon.ico` and `logo.png` are byte-identical
   copies of `favicon.ico` and `sedna-ui-icon-64.png`, compared byte-for-byte by
   `BrandAssetTests`.

Also: `CatalogueTopbar.razor` prints the wordmark text, which becomes `Sedna.UI`.

### 6.5 The palette, recorded only

`assets/brand/README.md` currently states that the brand colours come from the
package's default design tokens and lists `#111827`, `#1F2937`, `#2563EB`,
`#60A5FA`, `#F3F4F6`. That statement becomes false and is rewritten to record
the brand palette as its own thing, with an explicit note that the library's
default tokens are unchanged and that applying the palette to the default theme
is a separate later change.

| Name | Hex |
|---|---|
| Sedna Red | `#FF6B4A` |
| Orbit Blue | `#59C3FF` |
| Deep Space | `#0F172A` |
| Navy Slate | `#1E293B` |
| Ice White | `#F8FAFC` |
| Dust Gray | `#94A3B8` |

The README badge accent moves from the old brand blue `2563eb` to Sedna Red
`#FF6B4A`. This is brand chrome in a Markdown file and touches no library token.

## 7. Docs, CI and infrastructure

- `README.md` — hero URL and alt text, seven badge URLs (each embeds the repo
  name or the package ID), the `dotnet add package` line, the title. The
  existing tagline "One design-token contract. Semantic CSS. Consistent Blazor
  apps." is kept; it states what the library does.
- `CLAUDE.md` ×4 — repository root, `src/Sedna.UI/css-parts/`,
  `src/Sedna.UI/js-parts/`, `src/Sedna.UI.Catalogue/`.
- `docs/` — `architecture.md`, `getting-started.md`, `releasing.md`,
  `development.md`, `accessibility.md`, `CLAUDE.consuming-app.md`.
- `CONTRIBUTING.md`, `THIRD-PARTY-NOTICES.md`, and the `.gitattributes` comment
  naming the tokens file.
- **`.editorconfig`** — path-scoped sections `[src/DR.Simple_UI.Tests/**.cs]` and
  `[src/DR.Simple_UI.Catalogue{,.Tests}/**.cs]`. A glob that stops matching
  raises no error at all; analysis rules just silently change.
- `.github/workflows/ci.yml` — solution path, `playwright.ps1` path, the browser
  env var, the pack path, the `verify-package.sh` artefact glob, the docker tag.
- `.github/workflows/codeql.yml` — solution path.
- `.github/workflows/release.yml` — all of the above, the `dotnet add package`
  line in the generated notes, and the trusted-publishing comment block, which
  must state repository `Sedna.UI`. **The workflow filename does not change**,
  because the nuget.org policy matches on it.
- `src/Sedna.UI.Catalogue/Dockerfile` — every copied path and the example build
  command in its header.
- `railway.json` — `dockerfilePath` and `watchPatterns`. A stale watch pattern
  silently stops redeploying on library changes.
- `www.sedna-ui.com` replaces `simpleui.dennisrahmen.dev` in `PackageProjectUrl`,
  `README.md`, `CLAUDE.md`, four `docs/*.md`, the catalogue's `CLAUDE.md`,
  `Navigation/CatalogueLinks.cs`, `Examples/Mcp/ClaudeCode.txt`,
  `Examples/Mcp/Config.txt`, the comment in `css-parts/30-cards.css`, and
  `Catalogue.Tests/ExampleSourceTests.cs`.
- The `Description` in the csproj still claims the package "Ships a
  copy-pasteable HTML catalogue inside the package", which stopped being true
  when the catalogue became a hosted app. That sentence is removed while the
  metadata is being edited.

### 7.1 New: `docs/migrating-from-dr-simple-ui.md`

The one document where the old names legitimately appear. Contents:

- the identifier table from §3;
- the 36-class `dr-` → `sedna-` list, generated by `build/release-inventory.sh`;
- the cascade layer rename, for any app that addresses a layer by name;
- **the storage-prefix change loses stored user state.** `drui.theme` → 
  `sedna.theme` means every user silently reverts to default theme, density,
  colour-vision and language settings on upgrade. This is stated explicitly
  rather than left to be discovered.

No aliases and no compatibility shims, per CLAUDE.md: a shim is a second code
path nobody tests and it outlives the migration it was written for.

Note the side effect: the catalogue csproj embeds `..\..\docs\*.md`, so any new
top-level file in `docs/` is automatically served by the **public,
unauthenticated** MCP server as a `docs` resource and listed in `Docs.Names`.
That is the desired outcome for a migration guide — an agent should be able to
read it — but it means the document is published, and it should be written for
that audience. The glob is single-level, so this spec, at
`docs/superpowers/specs/`, is not embedded and not served.

## 8. Stays with Dennis

| Item | Consequence if missed |
|---|---|
| nuget.org trusted publishing → repository `Sedna.UI` | `release.yml` fails at *Exchange OIDC token*, after build and tests have passed |
| Deprecate `DR.Simple_UI` 0.1.0 with a pointer to `Sedna.UI` | old package looks current; it cannot be unpublished, only deprecated |
| DNS for `www.sedna-ui.com` → Railway: the records Railway lists for the domain (one CNAME, as of the 2026-08-04 setup). Hetzner DNS hosts the zone and appends it to an unqualified CNAME value, so the target needs its trailing dot | catalogue and `/mcp` unreachable at the new domain |
| GitHub social-preview image upload, repository description and website field | dashboard-only for the image; Claude can set description and homepage via the API on request |
| `AI_Console` / Athene migration | separate repository; still pinned to `DR.Simple_UI` 0.1.0 |

## 9. Staging

Branch `rebrand/sedna-ui` off `main` (clean at `241c891`). Four commits, merged
as one PR so `main` is never half-renamed.

Substitutions are applied with a throwaway script kept in the session scratchpad
and **not committed** — it has no value after the rename and would rot. Ordered
longest-match-first so no substitution corrupts another.

**Stage 1 — project and identifiers.** `git mv` the four project directories and
the solution file; csproj metadata; namespace and `using` directives; the six C#
type renames; shipped asset filenames; `Assets.cs` and `CatalogueAssets.cs`;
`build/bundle-css.sh`, `bundle-js.sh`, `export-tokens.sh`, `verify-package.sh`;
add `build/css-path.sh` and rewire `class-history.sh` and
`release-inventory.sh`; regenerate all three generated artefacts.

Stage 1 deliberately leaves **brand filenames alone**. The csproj's
`<None Include="…assets\brand\dr-simple-ui-icon-128.png" PackagePath="icon.png">`,
the README hero URL and the three filename strings inside `BrandAssetTests` keep
their old names until stage 3, because renaming the csproj reference before the
file exists under its new name would fail the pack and `BrandAssetTests`. Only
the *path prefix* to the moved projects changes in stage 1; "shipped asset
filenames" above means the stylesheet, the two scripts and the tokens JSON.

**Stage 2 — CSS and JS prefixes.** The 36 utility classes in `css-parts/`; the
cascade layer names in `layer_for()` and every part; `window.sednaUi` and the
`sedna.` storage prefix in `js-parts/`; the catalogue's `Examples/`, pages and
`catalogue.js`; regenerate the bundles.

**Stage 3 — brand.** Blocked on Dennis's uploads; the §6.3 fallbacks unblock it
if a request cannot be met. New imagery, renamed files, catalogue byte-copies,
`BrandAssetTests`, `assets/brand/README.md` with the palette, README badge
accent.

**Stage 4 — docs, CI, infrastructure, domain.** Everything in §7, including the
new migration document.

Stage 3 is independent of 2 and 4 and may land in any order among them; stage 1
must be first because every later stage's paths depend on it.

## 10. Verification

Every stage: `dotnet build`, then `dotnet test` over both test projects with
`SEDNA_UI_BROWSER_TESTS=1` so the Playwright tests assert instead of skipping.

Four checks specific to this change, because a rename's failure mode is silence:

1. **Generated artefacts.** Re-run `bundle-css.sh`, `bundle-js.sh` and
   `export-tokens.sh`; `GeneratedArtefactTests` proves each bundle agrees with
   its parts. The bundles are never hand-edited.
2. **Class history, positively.** `class-history.sh --check` is not sufficient —
   a fully-nulled file is self-consistent. Assert that unprefixed classes and
   tokens still resolve to `"0.1.0"` against the real `v0.1.0` tag. That is what
   proves `css-path.sh` works.
3. **`release-inventory.sh v0.1.0`** runs clean. It is both a regression check on
   the same trap and the source of the release-notes class lists.
4. **`dotnet pack` then `build/verify-package.sh`** on the real `.nupkg`: eight
   asserted paths, no `staticwebassets/catalogue/`, no catalogue assembly, and
   exactly one dependency.

Then the catalogue in a browser, page by page, watching the console. Every
utility class changed, and a stale `.dr-row` in one example renders unstyled
with no error. `CoverageTests` catches most of that from the other direction and
the §5.1 guard closes the rest; the browser pass is the backstop.

Stale build output (`bin/`, `obj/`, `artifacts/`, `.vs/`) under the old project
names is deleted before the first build, so a stale assembly cannot satisfy a
reference that should fail.

## 11. Release ordering

The README hero URL embeds the repository name and resolves only once the repo
is renamed **and** the branch is merged to `main`. Therefore, in order:

1. Merge the PR to `main`.
2. Rename the GitHub repository to `Sedna.UI` (Claude, confirming immediately
   before the API call). Update the local `origin` URL so `git remote -v` does
   not lie.
3. Dennis updates the nuget.org trusted-publishing policy to repository
   `Sedna.UI`.
4. Draft the release notes: the level, every added class name, every removed
   class name, the identifier table, and the stored-state loss. Confirm with
   Dennis.
5. `git tag -a v0.2.0 -F notes.md && git push origin v0.2.0`, with `notes.md`
   written outside the repository.
6. After the tag: re-run `build/class-history.sh` and commit, so the `sedna-*`
   classes move from `null` to `"0.2.0"`.

Classification: **Major** by every rule in CLAUDE.md §"Version rules" — renamed
classes, renamed asset paths, renamed JS global, renamed public types. Because
the major version is 0, it ships as the next minor, `0.2.0`, with the breaks
listed at the top of the notes.

A published nuget.org version cannot be replaced, reused or withdrawn. Step 5
does not happen without Dennis confirming the version and the notes.

## 12. Risks

| Risk | Mitigation |
|---|---|
| `class-history.json` silently nulls, making every `since` wrong | §4.1 fix plus the positive assertion in §10.2 |
| A stale `dr-` class in a catalogue example renders unstyled, no error | §5.1 guard over `Examples/` and `Components/`; `CoverageTests`; browser pass |
| `.editorconfig` globs stop matching, changing analysis rules silently | listed in §7 and edited in stage 4 |
| `railway.json` watch patterns go stale, deploys stop triggering | listed in §7 and edited in stage 4 |
| nuget.org policy not updated → release fails after a full green build | §8, and step 3 precedes step 5 in §11 |
| README hero 404s on the nuget.org package page | §11 ordering: merge and rename before tagging |
| Users lose stored theme/density/language | unavoidable given the prefix rename; documented in §7.1 rather than shimmed |
| A substitution corrupts an unrelated string | longest-match-first ordering; staged commits keep each diff reviewable; `git mv` preserves history |

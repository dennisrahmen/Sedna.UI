# Sedna.UI — conventions

Shared UI layer for Blazor apps. A UI fix is made here once, not re-copied into each app.

Stack: **.NET 10 / Blazor / server-side Razor**. `net10.0`, `LangVersion latest`, `Nullable enable`,
`ImplicitUsings enable`, `TreatWarningsAsErrors true`, xUnit.

## Two tiers

**Tier 1 — the frame.** Shell, sidebar and nav, header, user widget, toasts, modal shell.
Pixel-identical in every app, never restyled per project.

**Tier 2 — the paint.** Tables, forms, cards, badges, buttons, panels, alerts. Pages write plain HTML
and apply the classes.

**Both tiers are CSS classes. There are no components.** Do not add a `<DataTable>`, a `<Card>` or an
`<AppShell>` — the frame is markup on the catalogue's Shell & nav page, copied like everything else.
Adding UI means adding classes and a catalogue page.

The package is the stylesheet, the script, the icons, the token export, and a small C# surface for the
things markup cannot express: `ActiveLink` (which link is the current page), `ISednaUi` (typed
access to the browser API) and `AddSednaUi()`. Most of this library is CSS.

## The stylesheet and the script are generated

Edit `src/Sedna.UI/css-parts/*.css` or `js-parts/*.js`, then run `build/bundle-css.sh` /
`build/bundle-js.sh`. Never edit `wwwroot/css/Sedna.UI.css` or `wwwroot/js/Sedna.UI.js` — both
are generated, and a test fails if a bundle and its parts disagree.

Parts are **discovered**, not listed: the generator reads the directory, so adding a file is the whole
job and nothing can be left out. Order is the byte-ordinal filename order, which is why every part
carries an `NN-` prefix and the build fails without one. Conventions for writing a part are in a
`CLAUDE.md` inside each parts directory.

One file of each ships. The parts sit outside `wwwroot` so they are not static web assets: an app has
exactly one stylesheet path and one script path. Do not add a runtime loader for the parts — JS-injected
CSS leaves content unstyled until scripts run, and `@import` serialises the requests.

`Sedna.UI.boot.js` stays standalone. It runs in `<head>` before first paint; bundling it into the
main script would defeat its purpose.

## No hard-coded colours

Every colour, tint and shadow in `wwwroot/css/Sedna.UI.css` resolves through a token declared in one
of the `:root` blocks. No hex, no `rgb()`, no colour keyword anywhere else in the file.

The guards in `src/Sedna.UI.Tests/Css/` also enforce that:

- every `var(--x)` used is declared;
- token blocks declare only custom properties;
- the light and colour-blind blocks only remap tokens and never override a selector, which is what keeps
  CSS load order irrelevant;
- `font-family` rides `var(--font-sans)` / `var(--font-mono)`;
- no app-specific naming (`athene`, `zbx`, `gsearch`, `guide-`, …) appears.

Adding a token is a minor version. Renaming or removing one is major.

## Out of scope

App-specific business UI stays in the app that owns it: approval panels, SLA badges, tour overlays, demo
choosers, first-run guides, claim overlays, ServiceNow journal styling, page-specific grids.

Permanently out of scope:

- MudBlazor, Syncfusion, Radzen, Tailwind.
- Wrapping tables, forms or page content in components.
- Wrapping the frame in components. They existed on `main` between `0.1.0` and this release and were
  removed before shipping; do not bring them back.
- **The package** loading anything from a remote URL at runtime. Everything it needs ships inside it,
  so no host outage can affect a customer site. The catalogue application is a web server, and that
  rule is about the package.
- **Any third-party package reference in the library.** `Microsoft.AspNetCore.Components.Web` is the
  only dependency and is unavoidable — `NavigationManager`, `NavLinkMatch` and `IJSRuntime` live there.
  A third-party package is the same exposure moved to build time: a supply-chain risk, a licence to
  audit, and a transitive version conflict in every consuming app. A test fails on one, and
  `build/verify-package.sh` asserts the packed dependency list is exactly that one name.
- A second icon set. Remix Icon is bundled and is the only one.

## The catalogue

`src/Sedna.UI.Catalogue/` — a Blazor Web App with interactive server rendering, deployed to
<https://www.sedna-ui.com/>. One page per class family, with copy-pasteable markup. Add
catalogue pages as classes are added; a class with no page is a class nobody can find.

Two rules, both test-enforced:

1. **Single source of CSS.** The app serves the library's own static web assets from
   `_content/Sedna.UI/…`, never a copy. A test compares the bytes the running app returns against
   the file in the repo.
2. **The catalogue does not ship in the package.** `build/verify-package.sh` fails on any
   `staticwebassets/catalogue/` entry and on the app's assembly appearing in the `.nupkg`.

The site is built from `main` and can be ahead of any released version. Every class, token and example
carries `since` — the release it first shipped in, or `"unreleased"` — so an agent can check before
copying, and the strip under the header says the same thing to a human. Keep the notice and the `since`
data working.

**Write each example once, as a file under `Examples/`.** A live example is a `.razor` file the page
renders *and* prints, from the same embedded bytes, so a demo and its snippet cannot drift. A code-only
snippet is a `.html`, `.css` or `.txt` file rendered by `CatSnippet`, and must never be named `.razor` —
the Razor SDK's own glob would sweep it into a component. Do not hand-write a `<pre>` beside a demo.

**An example `.razor` file is plain HTML with no Razor syntax at all.** Then the bytes on disk are the
bytes compiled, rendered and printed, so nothing is escaped and the snippet pastes into a `.razor` page
and an `.html` file alike. `Examples/Interop/` is the one exception, for demonstrating the C# surface,
and a test asserts from both directions that it is used for nothing else.

Adding a page: create `Components/Pages/<Name>.razor` with a `@page` route, then add it to
`CataloguePages`. Tests fail on an orphaned page and on a registry entry with no page.

`catalogue.css` is the docs' own chrome and may only style `.cat-*` / `.ex-*`. **The app's own
JavaScript reads and never writes to the DOM** — Blazor owns the document, and anything it mutated
would be reverted on the next render, silently and only sometimes.

### The MCP server

`/mcp` on the same app: Streamable HTTP, **public, unauthenticated and read-only**. Six tools —
`search`, `get_example`, `describe_class`, `get_page`, `get_tokens`, `get_integration_guide` — plus
four resources. **There must never be a seventh tool that writes.** A client honouring the read-only
hint calls these without prompting, which is only safe while that stays true.

Rate limited on `/mcp` alone, never globally: a global limiter would also count Blazor's SignalR
upgrades, so one person browsing would trip a limit sized for MCP calls. A concurrency limiter is the
actual control; the per-caller token bucket is fairness, because behind a proxy whose addresses we do
not control per-IP limiting is not a security measure. `docs/architecture.md` says so out loud.

## Structure

```
src/
  Sedna.UI/
    css-parts/                        the stylesheet, authored as one short file per component
    js-parts/                         the script, authored as one short file per behaviour
    wwwroot/css/Sedna.UI.css      GENERATED by build/bundle-css.sh — do not edit
    wwwroot/js/Sedna.UI.js        GENERATED by build/bundle-js.sh — do not edit
    wwwroot/js/Sedna.UI.boot.js   pre-paint theme, loaded in <head>; standalone
    wwwroot/tokens/…tokens.json       GENERATED by build/export-tokens.sh
    Navigation/ActiveLink.cs          which link is the current page
    Interop/                          ISednaUi — typed access to the browser API
  Sedna.UI.Tests/                 xUnit + bUnit + Playwright, over the shipped assets
  Sedna.UI.Catalogue/             the hosted catalogue and the MCP server
    Components/Pages/                 one .razor page per class family
    Examples/                         one file per example — rendered AND printed
    Mcp/                              the six tools, the ranker, the version envelope
    Navigation/                       the page registry
    Dockerfile                        build context is the REPOSITORY ROOT
  Sedna.UI.Catalogue.Tests/       WebApplicationFactory + Kestrel + Playwright
assets/brand/                         icon, logo, favicon, social preview
build/verify-package.sh               unpacks the .nupkg and asserts its contents
docs/                                 long-form documentation
railway.json                          deploy configuration, reviewable in a pull request
```

All four projects live under `src/`. Tests sit beside the project they test, and
`dotnet test src/Sedna.UI.Tests` passes with the catalogue project deleted — which is the honest
statement of the split, and what stops the coupling growing back.

**One package ships from this repo.** The catalogue is a second project but not a second package
(`IsPackable=false`, asserted by `PackageConfigTests` and again by `build/verify-package.sh`). A second
*package* must not be added: it is a second version to keep in step, a second trusted-publishing policy,
and a second copy of the host page to keep in step with `docs/getting-started.md`. Getting started is a
documented block to copy, and `HostPageTests` keeps that block correct by executing it — the
catalogue's own host page **is** that block.

**Do not state a count of anything in prose.** File counts, class counts and token counts go stale in
silence, and all three figures on the catalogue landing page were wrong at some point. Where a number
matters it is **calculated**, never typed:

- `build/css-inventory.sh` is the one implementation of "what does this stylesheet declare". Both
  extractions in it are subtle — see the header before writing a third one.
- `build/release-inventory.sh` derives the class and token lists a release adds, for the notes.
- `build/class-history.sh` derives which release first shipped each class and token, which is what the
  MCP server's `since` reports. `--check` in CI; regenerate and commit after cutting a tag.

The landing page's figures are computed at runtime by the browser's own CSS parser
(`sednaUiCatalogue.readInventory`), and a test compares them against a .NET regex over the same
file. **That duplication is the point:** every one of those figures was wrong while a single
implementation agreed with itself — and the CSSOM implementation immediately found a fourth, because
the browser re-serialises the icon font's `:before` as `::before`, so a regex copied from the .NET side
reported zero icons.

Three directories carry their own `CLAUDE.md`, next to the files an agent will edit: `css-parts/`,
`js-parts/` and `src/Sedna.UI.Catalogue/`. Read the local one before adding a file there — it holds
the rules that only apply inside it, including which number prefix to choose and which names are
already taken.

Keep `README.md` short — hero, badges, the two-tier summary, versioning, licence, links into `docs/`.
Detail belongs in `docs/`.

Documentation is written as documentation: state what to do and what the rules are. Do not narrate design
rationale or explain why a choice was made unless the reason changes what a reader should do.

### README.md is also the nuget.org readme

Two constraints, both of which look correct on GitHub and fail on the package page:

- **No raw HTML.** nuget.org renders a subset of Markdown and ignores HTML.
- **Images and links must be absolute.** Relative paths do not resolve on nuget.org, and only
  allow-listed image hosts render — use `https://raw.githubusercontent.com/…`. A test asserts the hero
  image URL is absolute.

## Brand assets

`assets/brand/` holds the icon (SVG, PNG 16→1024), horizontal logo (light and dark), 1280×640 social
preview, and a multi-resolution favicon, in the library's default tokens (`#111827`, `#1F2937`, `#2563EB`,
`#60A5FA`, `#F3F4F6`).

Three are wired into things that fail quietly if renamed, and a test pins them:

- `dr-simple-ui-icon-128.png` → packed as `icon.png`, the NuGet package icon. nuget.org requires a raster
  image of 128×128 or smaller.
- `dr-simple-ui-social-preview.png` → the README hero.
- `src/Sedna.UI.Catalogue/wwwroot/favicon.ico` and `logo.png` — the catalogue application's own
  copies. They no longer travel inside the package, but the drift hazard is identical: update the icon
  in `assets/brand/` and the site keeps serving the old one.

**Outstanding: the brand rename.** The rename to `sedna-ui-*` names is pending artwork — a ≥1024px
square transparent icon, a white-wordmark lockup, and a true vector `sedna-ui-icon.svg`. Until that
artwork lands, these files intentionally keep their `dr-simple-ui-*` names. Renaming the files alone
breaks three other things, which must move **in the same commit** as the files: `Sedna.UI.csproj`'s
`PackageIcon` `None Include`, `README.md`'s hero image URL, and `BrandAssetTests`.

The Sedna brand palette — Sedna Red `#FF6B4A`, Orbit Blue `#59C3FF`, Deep Space `#0F172A`, Navy Slate
`#1E293B`, Ice White `#F8FAFC`, Dust Gray `#94A3B8` — is recorded here only. The library's default
token values above are deliberately unchanged, so the default theme renders exactly as it did before
the rename. `README.md`'s badges already use `#FF6B4A`, but that is brand chrome, not a token.

## Icons

Remix Icon is bundled at `wwwroot/lib/remixicon/` and is the only icon set. Add or update it with
`build/vendor-remixicon.sh`, which pins a version, trims the `@font-face` `src` to woff2, preserves the
upstream copyright header, and copies the licence. The output is committed, so the build needs no network.

After changing the version, update `THIRD-PARTY-NOTICES.md` and the version stated in the docs. A test
compares the notice against the version in the vendored CSS header and fails on drift.

The icons stay under the **Remix Icon License v1.0**, not this repo's Apache-2.0. Section 9 of that
licence permits the combination; Sections 2.3 and 3.1 permit bundling them in a UI kit where they are a
minor component. Two restrictions bind this repo directly: the icons may not be redistributed as a
standalone icon pack, and none of them may be used as a logo or app icon — which is why the brand assets
in `assets/brand/` are bespoke rather than built from an icon.

## Naming

- CSS classes: semantic, lowercase-kebab, no app or vendor prefix. Library-owned utilities that need a
  namespace use `sedna-` (`.sedna-scroll`, `.sedna-tip`); everything else is plain (`.card`, `.btn-go`).
- **A plain name is a claim on the shared namespace.** An app that already styles that class silently
  gets the library's rules merged with its own on upgrade — no error, just a changed appearance. Before
  adding a plain generic name (`.list`, `.menu`, `.row`, `.tag`, `.pager`), check it against the apps
  known to consume this library, and prefer a name none of them uses when the meanings differ. Every
  release lists the class names it adds, so a consuming app can grep its own CSS before bumping.
- Modifiers: `--` suffix on the block (`.nav-status-card--ok`, `.tab--active`).
- Semantic families across buttons, badges and alerts: `go` (sends outward), `warn` (control changes),
  `danger`, `info`, `secret`, plus `cyan` / `orange` / `teal` as categorical hues with no meaning.
- Assets are named after the package. `Packaging/ShippedPathTests` pins the paths; consuming apps hard-code them.
- The JS global is `sednaUi`.

## JavaScript

`Sedna.UI.js` holds generic UI behaviour only: hover hints, theme settings, clipboard, notifications,
toasts, confirm dialogs, delegated menus, tabs, the command palette and the header search, and the
Markdown editor. App-specific interop stays in the app's own script. The member table is in
`docs/architecture.md`.

- `palette` and `search` share one matcher, `ui._.score`. Do not write a second one.
- `search`'s index is client-side and registered up front. Searching a database is the app's own job —
  it renders `.search-panel` with the library's classes and leaves `data-search` off the input.

- The hover-hint engine skips elements inside `.sidebar`; the collapsed rail has a CSS flyout, and both
  firing produces a double tooltip.
- An app suppresses hints by setting `sednaUi.tips.gate = el => …`. The library has no knowledge of
  what is suppressing them.
- Settings are stored under the `sedna.` prefix, which apps do not need to configure — `localStorage` is
  origin-scoped, so apps on separate domains cannot collide. The prefix exists to namespace against other
  code on the same origin, for apps sharing one origin under different paths, and for the language
  cookie, which is not origin-scoped. If an app does override it, `data-prefix` and `storagePrefix` must
  match; a test asserts the two defaults agree.

## Z-order

topbar 60 < user widget 200 < collapsed-rail flyout 400 < drawer scrim 480 < drawer panel 490 < modal
backdrop 500 < spotlight 510 < popover and dropdown 550 < toast 600 < hover hints and reconnect banner
1000. Use one of these values for a new overlay; 0 and 1 are for local stacking inside a component and
are not part of the scale. Every rung is in use. A test fails on any value not on the scale, so a new
layer is added to the table in `docs/architecture.md` first.

550 carries the dropdown panels: `.menu`, `.search-panel`, `.popover` and the user widget's own. A real
`.popover` is in the top layer and ignores the scale entirely; the rung is its fallback.

**The browser floor is Chromium — current Chrome and Edge.** CSS anchor positioning is therefore
available and two things use it: `.popover`, and the collapsed rail's hover flyout, which has no other
option because `.nav-scroll` scrolls and a scroll container clips both axes. `.menu` stays on
`position: relative` and must: use anchor positioning where the alternative is measuring in JavaScript,
not as a default.

The drawer sits **below** the modal backdrop on purpose, so a modal opened from inside a drawer still
covers it.

`.topbar` and `.user-widget` create stacking contexts, so a panel nested in either is ordered within it
and cannot be lifted above the modal backdrop by z-index alone. The top layer (`popover`,
`dialog.showModal()`) ignores z-index altogether and orders by promotion.

## Cascade layers

The shipped stylesheet is entirely inside `@layer sedna.tokens, sedna.base, sedna.frame, sedna.paint,
sedna.utilities, sedna.overrides`. A part's layer is derived from its `NN-` prefix by the generator, so it
cannot drift from the source order and no part declares its own — the table is in
`css-parts/CLAUDE.md`.

**A consuming app's stylesheet is unlayered, so it beats every rule here whatever the specificity.**
Never try to out-specify an app; if an app has to be overridden, that is a design problem.

Two guards hold the model up: one fails if any rule escapes a layer (it would outrank the whole library
*and* be unreachable from the app), and one fails if a layer is used without being in the ordering
statement, since an undeclared layer sorts after every declared one.

Moving a part between layers, or reordering the layers, is **major** — see `docs/releasing.md`.

## No !important

The library uses none, and a test enforces it. Inside a cascade layer an `!important`
declaration becomes *harder* for an app to override rather than easier, because layer order inverts for
important declarations — so `!important` here would defeat the override model. Raise specificity instead
(see `.nav-link .nav-link-ext`).

## Releasing

The git tag is the version — `v1.2.3` publishes `1.2.3`. No file in the repo records it.

**There is no `CHANGELOG.md` and one must not be added.** Release notes come from the annotated tag
message; the GitHub Releases page is the changelog. A test asserts no changelog file exists.

One package ships, from `release.yml` on a `v*` tag. Its nuget.org trusted-publishing policy matches on
the workflow **file name**, so renaming that file breaks publishing until the policy is updated.

### Before 1.0.0, breaking changes ship in a minor bump

The major version is 0, which is SemVer's way of saying the design is still being got right. A change
classified **Major** below goes out as the next **minor** (`0.2.0` → `0.3.0`), with the breaks listed
at the top of the notes.

Two things follow, and they matter more than the numbering:

- **Do not soften a breaking change with a fallback.** A compatibility shim is a second code path
  nobody tests, and it outlives the migration it was written for. Change it properly and say so in the
  notes.
- **Do not ship the same idea twice under two names** because renaming the first would break someone.
  `.user-menu-*` was deleted rather than left beside `.menu-*` for exactly this reason.

Classify the change anyway — the release notes have to state it. From 1.0.0 the levels mean what they
say.

### When asked to release

A published nuget.org version cannot be replaced, reused or withdrawn. Do not tag without confirming the
version first.

1. `git describe --tags --abbrev=0` for the last released version.
2. Read `git log <last-tag>..HEAD` **and the diff**. A commit subject can hide a contract change.
3. Classify against the rules below, taking the highest applicable level.
4. State the proposed version, its level, and the reason, then wait for confirmation — e.g.
   "0.2.0 (minor): adds `--brand-glow` and `.badge-teal`, nothing renamed or removed."
5. Draft the release notes. **List every CSS class the release adds, and every one it removes.** The
   additions let a consuming app grep its own stylesheets for a collision before bumping — a class the
   app already styles changes its appearance silently otherwise. The removals are the breaking part of
   the release, and `build/release-inventory.sh` prints them first for that reason; list them just as
   plainly. Confirm the notes too, then tag:

   ```bash
   git tag -a v0.2.0 -F notes.md
   git push origin v0.2.0
   ```

   Write `notes.md` outside the repo. The first line becomes the release title suffix; the rest becomes
   the body.

`release.yml` builds, tests, packs, verifies the package contents, publishes to nuget.org, and creates the
GitHub release.

### Version rules

Judged by what a consuming app sees, not by the size of the diff.

**Major** — an app breaks or changes appearance without editing anything:

- renaming or removing a token, class or modifier
- changing a tier-1 component's markup, parameters or emitted classes
- renaming a shipped asset path or the JS global
- changing an existing rule's values enough to move layout or colour
- a change that makes an existing app override stop working

**Minor** — additive and backwards compatible:

- a new token, class, variant, component or catalogue page
- a new optional parameter or JS function

**Patch** — no contract change:

- correcting a wrong value
- docs, tests, CI, comments

When a change is arguable, use the higher level and say so. Note the implied level while making an
ordinary change, so step 3 does not become archaeology.

### Trusted publishing

The release job exchanges its GitHub OIDC token (`permissions: id-token: write`) for a single-use NuGet
key valid one hour. No long-lived API key exists in this repo.

It requires a nuget.org policy matching owner `dennisrahmen`, repository `Sedna.UI` and workflow file
`release.yml`. The policy matches on the file **name** — renaming the workflow breaks publishing until the
policy is updated. The only secret is `NUGET_USER`, the nuget.org profile name.

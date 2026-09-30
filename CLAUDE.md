# Sedna.UI — conventions

Shared UI layer for Blazor apps. A UI fix is made here once, not re-copied into each app.

Stack: **.NET 10 / Blazor / server-side Razor**. `net10.0`, `LangVersion latest`, `Nullable enable`,
`ImplicitUsings enable`, `TreatWarningsAsErrors true`, xUnit.

## Three tiers

Every piece of UI belongs to exactly one tier, and one question decides which: **who writes the markup
on screen?**

**Tier 1 — the frame.** Shell, sidebar and nav, header, user widget, modal shell. The app writes it,
copied from the catalogue's Shell & nav page. Pixel-identical in every app, never restyled per project.

**Tier 2 — the paint.** Tables, forms, cards, badges, buttons, panels, alerts. The app writes plain HTML
and applies the classes.

**Tier 3 — the surfaces.** What the library draws or drives itself, because there is nothing for the
app to write: a toast, the hover-hint bubble, a graph's canvas, the document inside a rich-text editor.
The app writes everything around a surface — its toolbar, legend, filters, menu, panel, empty state — as
tier 2 markup, and hands the surface its data.

**Tiers 1 and 2 are CSS classes, and nothing else.** Do not add a `<DataTable>`, a `<Card>` or an
`<AppShell>` — the frame is markup on the catalogue's Shell & nav page, copied like everything else.
Adding UI means adding classes and a catalogue page.

**Tier 3 has a contract instead of exceptions.** The surfaces, how each reaches a page, and the rules
every one meets are in `docs/surfaces.md` — the registry the tests read. A surface is admitted by the
owner and by passing that contract; nothing in this file changes when one is added. See **Tier 3 —
surfaces** below.

## Markup belongs to the app

**The library never stands in for markup the app would otherwise write.** A component hides the CSS,
the HTML and the JS behind a tag, and an agent cannot read or edit what it cannot see — so the markup
stops being greppable, copyable and reviewable. That is the whole objection, and it decides every case
below.

**The test: after this ships, does the app still author the markup it renders?**

Three kinds of code pass it, and they are the only kinds that may exist beside the stylesheet — apart
from a tier 3 surface, which answers to its own contract:

- **Infrastructure components** emit no UI. `SednaBrandStyle` writes a `<style>` of brand tokens into
  `<head>`; `SednaStateArt` writes the state illustrations' `<symbol>` sprite into `<body>`. Removing
  one leaves a reader able to see every line their page renders — they would only write plumbing by
  hand.
- **Presenters** show, hide and await markup the app wrote. `ISednaUi.ShowModalAsync(id)` opens the
  app's `<dialog>` — a modal, a drawer or a sheet — and completes with its `returnValue` when it
  closes. `ISednaOverlays.ShowAsync<TComponent, TResult>` renders an app component through
  `SednaOverlayHost`, opens the `<dialog>` that component wrote, and completes with the result it
  closed with. **A presenter never adds an element the app did not write.** Presenters are for overlays
  opened by app logic; a popover or a menu opened by its trigger's own attribute needs none.
- **State helpers** compute what markup cannot express. `ActiveLink` answers which link is the current
  page, as `aria-current`; `SednaSort` renders `aria-sort`, `SednaPager` picks the page numbers to show,
  `SednaTabs` renders a managed tablist's attributes. Pure functions, no interop, nothing held.

**Tier 3 is where the library draws, and nowhere else.** A surface draws only what has no author —
the app's data or the app's one line — and the rule above still holds around it: every element and
every word beside a surface is the app's. A confirmation dialog, the command palette and the header
search's results were all once drawn by the script and were moved to app markup, because each had
something to author; that is the test a proposed surface fails or passes.

**A list the script fills comes from the app's `<template>`.** Where the script renders rows from data —
the palette's commands, the header search's results — the app writes the container and a `<template>`
per row shape, and the script clones it and fills `data-*` slots. Every element and every word on
screen is then the app's, in the app's language.

## The interop boundary

**The script never goes looking for .NET.** Parts change the DOM and dispatch events that Blazor's
bindings pick up. A part may do two more things, and only these:

- **Complete a call the app is awaiting.** `ShowModalAsync` completing when the dialog closes is one
  call finishing, not the library calling in.
- **Invoke a reference the app handed it and owns.** `ISednaSettings` passes a `DotNetObjectReference`
  to `watchSettings`, and creates and disposes it; the script invokes that one object and nothing else.
  The script never creates, finds or keeps a reference of its own.

**The test: who owns and disposes the reference?** If it is the app, the call is allowed. If the script
would have to hold one to make a feature work, the feature is designed wrong.

**A function does not cross the boundary; a handle does.** Where a JavaScript member takes or returns a
function, the C# member takes or returns data naming the same thing — an id, a boolean — and the script
keeps the function in a table keyed by it, as `watchSettings` does.

**A call that waits on the reader passes a `CancellationToken`.** Blazor applies a one-minute timeout
to every interop call that does not, so a dialog left open for a minute would throw into the app.

The package is the stylesheet, the script, the icons, the token export, the engines of the tier 3
surfaces that ship one, and the C# surface above: `ActiveLink`, `SednaSort`, `SednaPager`, `SednaTabs`, `ISednaUi`, `ISednaSettings`,
`ISednaOverlays`, `ISednaGraphs`, `ISednaEditors` and `AddSednaUi()`. Most of this library is CSS.

## The stylesheet and the script are generated

Edit `src/Sedna.UI/css-parts/*.css`, `js-parts/*.js`, `graph-parts/*.js` or `editor-parts/*.js`, then run
`build/bundle-css.sh` / `build/bundle-js.sh`. Never edit `wwwroot/css/Sedna.UI.css`,
`wwwroot/js/Sedna.UI.js`, `wwwroot/js/Sedna.UI.graph.js` or `wwwroot/js/Sedna.UI.editor.js` — all are
generated, and a test fails if a bundle and its parts disagree.

Parts are **discovered**, not listed: the generator reads the directory, so adding a file is the whole
job and nothing can be left out. Order is the byte-ordinal filename order, which is why every part
carries an `NN-` prefix and the build fails without one. Conventions for writing a part are in a
`CLAUDE.md` inside each parts directory.

One file of each ships. The parts sit outside `wwwroot` so they are not static web assets: an app has
exactly one stylesheet path and one script path. Do not add a runtime loader for the parts — JS-injected
CSS leaves content unstyled until scripts run, and `@import` serialises the requests.

**A shipped surface's module is the one kind of script an app never references.** `Sedna.UI.graph.js`
and `Sedna.UI.editor.js` are ES modules that `Sedna.UI.js` imports, relative to itself, the first time a
page shows a `[data-graph]` or a `[data-editor]` — so a page without one never downloads it or the engine
it imports, and the host page stays exactly as `docs/getting-started.md` writes it. Each is generated
from its `<name>-parts/` like the main script, and one loader, `js-parts/42-surfaces.js`, finds, holds,
starts and disposes them all. Nothing but a tier 3 surface is loaded this way: a behaviour every page may
need belongs in `Sedna.UI.js`.

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
- nothing in the file names a real ticket, host, address or company — see **No real names** below.

Adding a token is a minor version. Renaming or removing one is major.

## Out of scope

App-specific business UI stays in the app that owns it: its own workflow panels, its own overlays and
guided tours, styling for a particular integration's output, page-specific grids. If a class knows
what the app is *for*, it belongs to the app.

**A tier 3 surface is the one way the library draws for the app or dresses another engine's output** —
the graph ships cytoscape.js, the rich-text editor Quill. The registry in
`docs/surfaces.md` names them, so the rule above does not remove them, and "one engine per job" keeps
a second from being added beside one.

Permanently out of scope:

- MudBlazor, Syncfusion, Radzen, Tailwind.
- Wrapping tables, forms or page content in components.
- Wrapping the frame in components. The frame is markup, not a component tree; do not introduce one.

  This bans components that **hide markup**. It does not ban components outright — see **Markup
  belongs to the app** above for the three kinds that are allowed.
- Library-drawn UI outside tier 3: a confirmation, a prompt, an alert box built by the script. The app
  writes it as a modal and a presenter shows it.
- **The package** loading anything from a remote URL at runtime. Everything it needs ships inside it,
  so no host outage can affect a customer site. The catalogue application is a web server, and that
  rule is about the package.
- **Any third-party package reference in the library.** `Microsoft.AspNetCore.Components.Web` is the
  only dependency and is unavoidable — `NavigationManager`, `NavLinkMatch` and `IJSRuntime` live there.
  A third-party package is the same exposure moved to build time: a supply-chain risk, a licence to
  audit, and a transitive version conflict in every consuming app. A test fails on one, and
  `build/verify-package.sh` asserts the packed dependency list is exactly that one name. Vendored
  browser code is not a package reference and is held to its own rules instead: Remix Icon under
  **Icons**, a surface's engine under **Tier 3 — surfaces** — pinned, checksummed, licence-checked,
  committed, and loaded by nothing but the library's own code.
- A second engine for a job a surface already does, and any vendored code under a copyleft licence.
  cytoscape-svg is the plugin this rules out today: it is GPL-3.0, and the graph's SVG export is
  Sedna.UI's own instead.
- A second icon set. Remix Icon is bundled and is the only one. The state illustrations in
  `StateArt/Sedna.UI.states.svg` are not one: an icon set is a vocabulary a page draws from,
  and that file is thirteen fixed pictures of thirteen fixed states, embedded in the assembly and
  written into the page by `SednaStateArt` — never a static web asset, because WebKit renders an
  external `<use>` without the page's colours. Adding a fourteenth state is a minor version;
  adding a drawing an app picks between is the thing this bans.

## No real names

This repository is public and the catalogue is a public website. **Nothing in either may name a real
organisation, customer, employer, internal application, internal host, internal ticket or real
person.** Not in an example, not in page prose, not in a class name, not in a test fixture, not in a
comment, not in a commit message, and not in a pull request.

The reason is that the boundary is invisible from inside. A hostname or a product name lifted from a
real system reads as ordinary demo content to whoever wrote it and as a disclosure to everybody else,
and it is being published the moment it lands on `main` — a later edit removes it from the working
tree and not from the history or from anyone's clone. So the rule is that it never gets committed,
rather than that it gets cleaned up.

What to write instead:

- **People** — the demo identity is Alex Fischer, `alex.fischer@example.com`. Other fictional names
  already in the examples may be reused; do not invent one that could belong to a colleague.
- **Domains and e-mail** — `example.com`, `example.org`, `example.net`, `example.gov` (RFC 2606).
- **Addresses** — the documentation ranges, `192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24`
  (RFC 5737). Never a private or link-local address: a real-looking internal address in a
  copy-pasteable field reads as a real system's.
- **Hosts and systems** — a generic role plus a number, `orders-console-01`, `src-db-14`,
  `build-runner-04`. No site code, no company prefix.
- **Companies** — `Northwind Retail`, the well-known fictional sample, or nothing at all.
- **Third-party technology is not covered by this.** Naming Remix Icon, Railway, nuget.org or
  Chromium is describing a dependency, and the notices file has to.
- **An attribution is not covered by this either.** A photographer credited beside their own
  photograph is the opposite of a disclosure — it is the credit the licence asks for, and leaving it
  off to satisfy this rule would be the actual wrong. Credit them where the work is; do not use a
  real person's name for anything else.

**The tests match shapes, never names, and there is deliberately no deny-list.** There used to be
one, and a literal list of forbidden names is itself a published list of those names — the exact
disclosure it exists to prevent. So the example guard and the two in
`ScriptContractTests` match the *form* of a real thing: an
ITSM record number, a German public body or company legal form, an internal DNS label, a private
address range. Three real authorities were in the examples and each was recognisable by the word in
front of the place, so that word is what is matched.

The script and stylesheet are scanned **with their comments intact**. A markup snippet in a comment
is copied verbatim into the shipped file, so it is as published as a line of code — which is how a
first name used as demo content reached every installing app while a comment-stripping guard
watched.

**A name that fits no shape is caught by review and nothing else**, here and in page prose, doc
comments and documentation. That is the trade: no test can hold the list without publishing it.

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
copying. Keep the `since` data working.

**Write each example once, as a file under `Examples/`.** A live example is a `.razor` file the page
renders *and* prints, from the same embedded bytes, so a demo and its snippet cannot drift. A code-only
snippet is a `.html`, `.css` or `.txt` file rendered by `CatSnippet`, and must never be named `.razor` —
the Razor SDK's own glob would sweep it into a component. Do not hand-write a `<pre>` beside a demo.

**An example `.razor` file is plain HTML with no Razor syntax at all.** Then the bytes on disk are the
bytes compiled, rendered and printed, so nothing is escaped and the snippet pastes into a `.razor` page
and an `.html` file alike. `Examples/Interop/` is the exception — the C# surface — and a test asserts
from both directions that it is used for nothing else.

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
    graph-parts/                      the graph module, authored as one file per concern
    editor-parts/                     the rich-text editor module, the same way
    wwwroot/css/Sedna.UI.css      GENERATED by build/bundle-css.sh — do not edit
    wwwroot/js/Sedna.UI.js        GENERATED by build/bundle-js.sh — do not edit
    wwwroot/js/Sedna.UI.graph.js  GENERATED by build/bundle-js.sh — imported by Sedna.UI.js on demand
    wwwroot/js/Sedna.UI.editor.js GENERATED by build/bundle-js.sh — imported by Sedna.UI.js on demand
    wwwroot/js/Sedna.UI.boot.js   pre-paint theme, loaded in <head>; standalone
    wwwroot/lib/cytoscape/            VENDORED by build/vendor-cytoscape.sh — the graph engine and its plugins
    wwwroot/lib/quill/                VENDORED by build/vendor-quill.sh — the rich-text editor's engine
    StateArt/…states.svg              the thirteen state illustrations, one <symbol> each; embedded, not shipped as a file
    Components/                       SednaBrandStyle, SednaStateArt (infrastructure) and SednaOverlayHost (presenter)
    wwwroot/tokens/…tokens.json       GENERATED by build/export-tokens.sh
    Navigation/ActiveLink.cs          which link is the current page
    State/                            SednaSort, SednaPager, SednaTabs — the state helpers
    Interop/                          ISednaUi, ISednaSettings, ISednaOverlays, ISednaGraphs and ISednaEditors — typed access to the browser API
  Sedna.UI.Tests/                 xUnit + bUnit + Playwright, over the shipped assets
  Sedna.UI.Catalogue/             the hosted catalogue and the MCP server
    Components/Pages/                 one .razor page per class family
    Examples/                         one file per example — rendered AND printed
    Mcp/                              the six tools, the ranker, the version envelope
    Navigation/                       the page registry
    Dockerfile                        build context is the REPOSITORY ROOT
  Sedna.UI.Catalogue.Tests/       WebApplicationFactory + Kestrel + Playwright
assets/brand/                         icon, logo, favicon, social preview
build/api-inventory.sh                lists the public C# surface — the C# half of css-inventory.sh
build/verify-package.sh               unpacks the .nupkg and asserts its contents
docs/                                 long-form documentation
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
silence, and every figure the catalogue landing page once showed was wrong at some point. Where a number
matters it is **calculated**, never typed:

- `build/css-inventory.sh` is the one implementation of "what does this stylesheet declare". Both
  extractions in it are subtle — see the header before writing a third one.
- `build/release-inventory.sh` derives the class and token lists a release adds, for the notes.
- `build/api-inventory.sh` is the same implementation for the public C# surface, and is the one place
  that answers "what does this library export".
- `build/class-history.sh` derives which release first shipped each class, token, public C# member and
  example, which is what the MCP server's `since` reports. `--check` in CI. **Nothing is done before
  a release**: the tag is what dates an entry, the first PR after it regenerates the file, and until
  that merge the hosted site reads the release's own copy — `release.yml` attaches the history
  computed at the tag to every GitHub release, and the catalogue fetches it at runtime
  (`ReleasedHistory`) to fill in what its embedded copy still calls unreleased.

These directories carry their own `CLAUDE.md`, next to the files an agent will edit: `graph-parts/`, `editor-parts/`, `css-parts/`,
`js-parts/` and `src/Sedna.UI.Catalogue/`. Read the local one before adding a file there — it holds
the rules that only apply inside it, including which number prefix to choose and which names are
already taken.

Keep `README.md` short — hero, badges, the three-tier summary, versioning, licence, links into `docs/`.
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

`assets/brand/` holds the master mark **"Eccentric"** in the Sedna palette. All artwork sits on a
120-unit grid and the SVGs are the source of truth. The full manual is `docs/BRANDING.md`;
`assets/brand/README.md` says which file to reach for.

**The mark is drawn three times, not scaled three ways.** The orbit is a hairline that fills in and
reads as noise when shrunk, so stroke thickens and detail drops as size falls: full (stroke 2.5, 96px+),
compact (5.5, 32–64px), micro (8, 16px, satellite dropped). **The PNG ladders already switch tier by
size** — `sedna-ui-icon-16.png` is the micro drawing. Pick by size and the right one comes out.

**Two ladders, because a transparent mark is invisible on the wrong surface.** `sedna-ui-icon-*` is the
navy mark for light backgrounds; `sedna-ui-icon-on-dark-*` is the Ice White mark for dark. Where the
background is unknown — a NuGet listing, a browser tab, an IDE package pane — use `sedna-ui-tile-*`,
which carries its own Deep Space field.

Three are wired into things that fail quietly if renamed, and a test pins each:

- `sedna-ui-tile-128.png` → packed as `icon.png`, the NuGet package icon. nuget.org requires a raster
  image of 128×128 or smaller. The tile rather than the bare mark, for the reason above.
- `sedna-ui-social-preview.png` → the README hero, which is also the nuget.org readme.
- `src/Sedna.UI.Catalogue/wwwroot/favicon.ico` and `logo.png` — the catalogue's own copies, of
  `favicon.ico` and `sedna-ui-tile-64.png`. They no longer travel inside the package, but the drift
  hazard is identical: update the source and the site keeps serving the old one.

**The SVG trap.** `sedna-ui-wordmark*.svg` and `sedna-ui-logo-*.svg` set live `<text>` in **Outfit**. On
a machine without it they fall back to a generic sans, which the brand rules forbid outright — and
nothing errors, the wordmark is simply wrong. Mark and tile SVGs are pure geometry and safe anywhere;
for anything web-facing use the **PNG** wordmarks and lockups, where the type is already rasterised.
`assets/brand/font/` holds the variable font so the SVGs open correctly. It is a brand asset only: the
UI uses the system sans and mono stacks and never loads it, it is not in `wwwroot`, not a `@font-face`,
and not in the package. Licence in `THIRD-PARTY-NOTICES.md`.

The brand palette — Sedna Red `#FF6B4A`, Orbit Blue `#59C3FF`, Sedna Navy `#17346E`, Deep Space
`#0F172A`, Navy Slate `#1E293B`, Ice White `#F8FAFC`, Dust Gray `#94A3B8`. The focal body is a gradient
`#FD7636` → `#F34238` that flattens to Sedna Red. Sedna Navy is not one of the original six swatches;
it was sampled from the mark because the palette had no mid-dark blue.

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

## Tier 3 — surfaces

`docs/surfaces.md` is the registry and the contract; read it before touching a surface or proposing
one. What it means while editing:

- **A surface without a row does not exist.** `SurfaceRegistryTests` reads the table and checks every
  file a row names; `SurfacePageTests` checks the catalogue pages and their tier 3 badge. A script part
  that puts an element a reader sees on the page, and is not a surface's, fails the registry test.
- **The frame around a surface is tier 2.** A tooltip, a menu, a side panel, a legend, an
  announcement: app markup the script fills with `textContent` and places. No `innerHTML` in a
  surface's script, and a test says so.
- **Colour through tokens, resolved in the browser**, never a literal handed to an engine — the same
  test reads every surface's script for one.
- **Vendored engines** live under `wwwroot/lib/<engine>/`, written by a `build/vendor-*.sh` script
  through the shared `build/vendor-npm.py`:
  each package pinned to a version and to the sha512 of its registry tarball, checked before anything
  is written, its licence copied beside it, and upstream's text shipped unchanged under a provenance
  header — an ES module as it is, a UMD or CommonJS build wrapped in a module scope. A `VENDORED.txt`
  lists what is there, the output is committed so the build needs no network, and `--check` compares
  the committed files with a fresh vendor. After changing a version, update `THIRD-PARTY-NOTICES.md`; a
  test holds it against `VENDORED.txt`. Every package must be MIT, BSD-3-Clause or Apache-2.0, and its
  licence file must say so — the script stops on anything else, including a copyleft licence.
- **Only the library imports an engine**, by relative path from its own module. An app reaches one
  through a handle, documented as unversioned — the graph's `cy`, the editor's `quill` — and never by
  importing a file under `lib/`.
- **One loader.** `js-parts/42-surfaces.js` finds a shipped surface's elements, holds them on a
  prerendered Blazor page, starts each as it comes near the viewport and disposes each whose element has
  gone. A new shipped surface registers with it; it does not write a second one.

### The graph

`[data-graph]` markup draws records and links on a canvas; the reference is `docs/graph.md`. The engine
is [cytoscape.js](https://js.cytoscape.org/), vendored with its plugins by `build/vendor-cytoscape.sh`.

- **A record's tone is a series name** — the colour of the `.series-*` class of the same name — read
  through a probe element, so themes, the colour-vision setting and forced colours reach the canvas.
- **The keyboard is not optional.** The graph is one tab stop with arrow-key navigation between records
  and announcements in the app's words; a change that leaves a record unreachable without a pointer is a
  regression.
- The minimap and the SVG export are Sedna.UI's own; the other plugins are upstream's, loaded on demand.

### The rich-text editor

`[data-editor]` markup edits a document the app posts as HTML; the reference is `docs/editor.md`. The
engine is [Quill](https://quilljs.com/) 2, vendored with the licences of what its build bundles by
`build/vendor-quill.sh`.

- **The textarea is the field.** The editor loads from the app's `textarea[data-editor-value]` and writes
  every change back with the textarea's own events, so a form posts it, Blazor binds it, and it works
  without the script. Never add a value the textarea does not carry.
- **The toolbar and the link form are the app's.** Quill's toolbar, themes and stylesheet are not used.
- **The formats the toolbar offers are the formats the document can hold**; what is pasted is cut down
  to them.

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
toasts, the dialog presenter, delegated menus, tabs, combo fields, the command palette and the header
search, drag and drop, the Markdown editor, and the graph's front door, which imports the graph module
when a page first shows a graph. Drag and drop moves no node: it writes attributes and dispatches
`sedna-drop`, and the app moves the item; a link drawn on a graph is `sedna-graph-connect` the same way.
`wwwroot/Sedna.UI.lib.module.js` is the Blazor initializer that gives those events their data in C#,
and tells the graph when Blazor has started. App-specific interop stays in the app's own script. The member table is
in `docs/architecture.md`, and the rules for crossing into .NET are **The interop boundary** above.

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
1000. Use one of these values for a new overlay; 0, 1, 2 and 3 are for local stacking inside a
component and are not part of the scale — a sticky table uses 1, 2 and 3, and `docs/architecture.md`
says which is which and why the pinned column cannot sit at 0. Every rung is in use. A test fails on any value not on the scale, so a new
layer is added to the table in `docs/architecture.md` first.

550 carries the dropdown panels: `.menu`, `.search-panel`, `.popover`, `.form-combo-panel` and the user widget's own. A real
`.popover` is in the top layer and ignores the scale entirely; the rung is its fallback.

**The browser floor is Chromium — current Chrome and Edge.** CSS anchor positioning is therefore
available, and these use it: `.popover`; the collapsed rail's hover flyout, which has no other
option because `.nav-scroll` scrolls and a scroll container clips both axes; `.form-combo-panel`; and
`.menu`, for the same reason — an absolutely positioned panel is laid out inside the nearest scroll container and counts
towards its scrollable overflow, so a menu opened in a toolbar or a scrolling table both grew that
container a scrollbar and got clipped at its edge. Use anchor positioning where the alternative is
measuring in JavaScript, not as a default.

**It does not survive a scroll, and that is why `.menu` and `.popover` close on one.** Chromium
computes an anchored `position: fixed` panel's offset when the panel becomes visible and never
recomputes it while the anchor scrolls, so the gap grows by exactly the scroll distance and stays
wrong — measured at 4px → 154px after a 150px scroll, overshooting to −146px on the way back.
`22-anchored.js` closes an open panel when a scroll moves its trigger, which is what a platform menu
does anyway; nothing there measures or writes a coordinate, so the CSS is still the whole positioning
model. The collapsed rail's flyout has the same fault and no fix, being a hover state with nothing to
close — the pointer has to stay on the item for it to exist, which is what makes it the mild case.
`AnchoredPanelTests` pins all of it.

The drawer sits **below** the modal backdrop on purpose, so a modal opened from inside a drawer still
covers it.

A spotlight over an open modal `<dialog>` leaves the scale entirely: `sednaUi.spotlight` raises the
hole and the bubble into the top layer after the dialog and moves the bubble into it, because
everything outside an open modal dialog is inert and inertness follows the DOM, not the paint order.

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
   plainly. Confirm the notes too.
6. Once the version and the notes are confirmed, tag `main` — no release PR, nothing to stamp:

   ```bash
   git tag -a v0.2.0 -F notes.md --cleanup=verbatim
   git push origin v0.2.0
   ```

   `--cleanup=verbatim` is required: git's default cleanup deletes every line starting with `#`, so
   every Markdown heading would vanish from the tag message and the release body. Write `notes.md`
   outside the repo. The first line becomes the release title suffix; the rest becomes
   the body.

7. The first pull request after the release regenerates `class-history.json` — `--check` fails until it
   does. The hosted site is right in the meantime: `release.yml` attaches the history computed at the
   tag to the release, and the catalogue reads it.

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

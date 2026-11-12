# Consuming-app `CLAUDE.md` block

Copy the section below into the `CLAUDE.md` of any app that uses `Sedna.UI`, so an AI agent working
in that app follows the library's rules instead of inventing its own UI.

Replace:

- `<VERSION>` — the pinned package version, e.g. `0.2.0`
- `<APP-PREFIX>` — a short CSS prefix for this app, e.g. `myapp`

---

## Shared UI — Sedna.UI

This project uses `Sedna.UI` version `<VERSION>` for all shared UI.

### Copy markup from the catalogue

The catalogue is a hosted site with an MCP server. Add it to this project's MCP configuration:

```json
{ "type": "http", "url": "https://www.sedna-ui.com/mcp" }
```

Start from what the user of this app has to be able to do, not from a component: `search` for it in
plain words — "let the user upload a file", "confirm a destructive action" — and a hit of kind
`goal` names the page to start from. `get_guide` with the goal's id says which parts go around it,
how they combine and the usual mistake; with a page route it says what that part is for, what it is
combined with and what it is not for; with `"principles"` it covers composition, spacing, colour,
actions, feedback, density, motion and words. The same guide is at
<https://www.sedna-ui.com/design-guide>.

Then fetch the exact markup — `search` → `get_example`. Use `describe_class` to choose between
variants. **Do not write shared-UI markup from memory**, and do not invent class names: a class
that is not in the stylesheet does nothing at all, silently.

The site is built from the library's `main` branch and can be ahead of the version pinned here. Every
class, token and example it returns carries `since`: the release it first shipped in, or the literal
`"unreleased"`. **Pass `installedVersion: "<VERSION>"` to the tools, and do not copy anything whose
`since` is newer than that or is `"unreleased"`.**

Without an MCP client, browse <https://www.sedna-ui.com/> and read the strip under the header.

### Content UI is a CSS class, not a component

Tables, forms, cards, badges, buttons and panels are CSS classes. Write plain HTML in the `.razor` file
and apply them. Do not wrap page content in a component, and do not add MudBlazor, Syncfusion, Radzen or
Tailwind.

The layout frame — shell, sidebar, header, bottom bar, user widget, drawer — is CSS classes too. There
is no `<AppShell>`. Each part is a component of its own: write the parts this app needs and combine
them as it likes, starting from a combination on the catalogue's Shell and nav page. `ActiveLink` from
the package supplies the one thing that markup cannot, which is knowing which nav link is the current
page. Keep the library's class names on the parts rather than copying their rules into this app: a
copied rule stops receiving library fixes, and it will silently miss later additions such as the
responsive drawer and the skip link.

### A label beside anything else is an element

In a row — a button, a badge, a chip, a menu item, a nav link, an alert, a list row — write the words
beside an icon, a dot, a count, a checkbox or a badge as an element: a `<span>` with no class, or with
only utility classes.

```html
<button class="btn"><i class="ri-check-line"></i> <span>Approve</span></button>
<label class="form-check"><input type="checkbox" /> <span>Notify me</span></label>
```

The library centres what is drawn, not the boxes: an icon on the middle of its glyph, a word on the
middle of its capitals. A word that is the row's own text node is set where its font puts it, up to a
pixel off, because no rule can reach it. Text on its own — `<button class="btn">Save</button>` — needs
nothing: the library centres it.

### When a component needs something only this app needs

Every component is exact in every state it supports and combines with any other. What this app alone
needs is reached in this order:

1. **A token** — the component's own value, in `wwwroot/css/brand.css` or inline
   (`style="--modal-width: 1200px"`).
2. **A variant** — a modifier class the library ships.
3. **A class of this app's own**, prefixed with this app's name (`.<APP-PREFIX>-…`), on the element.
   It may set any property; the library never declares that name, so it survives upgrades.

Never write a rule for a class name the library owns. If a token or variant is missing, use step 3 now
and request the token or variant upstream — see *Requesting a library change*.

### Toasts, and the one word the library writes

`ISednaUi.ToastAsync` returns a `SednaToast`. For work that ends later, show the running state with
`timeoutMs: 0` and `ReplaceAsync` the outcome into it. Set `SednaUiOptions.ToastDismissLabel` in
`AddSednaUi` when the app is not in English — the toast's close button is the only text the library
writes. `ToastPile` and `ToastLimit` set the stack's shape for every toast. Nothing the reader needs or
must act on lives only in a toast: it goes inline, in an `.alert`, or in the notification inbox.
`SetTipsEnabledAsync(false)` switches hover hints off from a setting.

### State the markup cannot express

Three helpers render attributes from the app's own state — never a component:

- **A sorted table:** `SednaSort` — `aria-sort="@_sort.AriaSort("due")"` on each sortable header,
  `_sort = _sort.Toggle("due")` on its `.th-sort` button.
- **A pager:** `foreach (var slot in SednaPager.Window(_page, _pages))`, rendering `.page-gap` for a gap.
- **Tabs selected from C#:** `data-tabs="managed"` on the tablist, `@attributes="SednaTabs.Tab(key, _tab)"`
  on each tab and `SednaTabs.Panel(key, _tab)` on each panel. Never plain `data-tabs` with C# selection:
  two things would write `aria-selected`.

### Tables, filters and pagers

- **Filters on a table:** a `.toolbar` above it by default; a `.tr-filter` row for filtering column
  by column, each control matching its column's type; a `.th-filter` popover where the table is too
  narrow. Mark a set filter with `.col-filtered`, repeat it as a removable `.chip--active`, and show the
  count of rows left.
- **Resizable columns:** `.table--fixed` inside a `.sedna-scroll-x`, with each width as the heading's
  `--col-width` kept from `sedna-resize`. The table fills its box and scrolls once the columns outgrow it;
  never set a `width` on the table or its cells.
- **A pager** goes straight after the card or list it pages, and keeps its own distance from it. A list
  of known length shows the page numbers from `SednaPager.Window` — never only the current page between
  Previous and Next.

### The header search

`.search` in the topbar is a library class. An app that already ships its own search chrome under its
own prefix should move onto these classes rather than keep a parallel copy.

There are two ways to use it, and the choice is about where the results come from:

- **A fixed, known set** — the app's pages, reports, settings screens. Register it once with
  `ISednaUi.RegisterSearchAsync`, write the results panel — `[data-search-panel]` with its
  `<template>`s, copied from the Topbar page — once at the end of the layout, and point the input at
  it with `data-search="<panel id>"`. The ranking and the keyboard come from the library; every word in
  the panel is the app's.
- **A database.** Leave `data-search` off and render `.search-panel` yourself. The debounce length,
  cancelling a superseded keystroke and the busy state are decisions about your backend, and the
  library will not guess them.

### The command palette is app markup too

`ISednaUi.RegisterCommandsAsync` supplies the commands; the `<dialog class="palette" data-palette>`
with its input, footer and `<template>`s is written once in the layout, copied from the Command palette
page. Without it, Ctrl-K stays the browser's.

A `data-copy` button carries its own feedback: `data-copied-hide` on what shows at rest,
`data-copied-show="ok"` on what replaces it. The script only sets `data-copied` on the button.

### Dialogs are the platform's, not a div

**Pick the route by where the dialog's markup lives.**

- **A few lines of a page's own markup** — a confirmation, a one-field prompt, reading and writing that
  page's fields: a `<dialog class="modal" id="…">` in the page, awaited with `ISednaUi.ShowModalAsync(id)`.
- **A `.razor` file of its own** — its own parameters, state, loading or validation, or opened from more
  than one page: `ISednaOverlays.ShowAsync<TComponent, TResult>`, with one `<SednaOverlayHost />` in the
  layout. Never render such a component inline in a page and open it through a `@ref`: it renders on
  every visit, its state outlives each opening, and each page repeats the wiring the host already does.

Put an app's own modal in a `<dialog class="modal">` with an `id`, and open it with
`ISednaUi.ShowModalAsync(id)`. That is the whole reason the member exists: the top layer, a focus trap,
Escape-to-close and inert content behind come from the browser, and a `.modal-backdrop` div behind an
`@if` has none of the four. The same call opens a `<dialog class="drawer">` or a `.sheet`. Neither this
nor anything below needs `IJSRuntime`.

`ShowModalAsync` completes when the dialog closes, with its `returnValue`, so **a confirmation is a
`.modal--sm` you write**: a `<form method="dialog">` whose buttons carry a `value`, and one comparison on
the result — `null` is Escape. The library draws no confirmation, so the wording and the language are
the app's. Leave `@onsubmit` off that form: a Blazor submit handler cancels the submit, and closing the
dialog is its default action.

A modal, drawer or sheet that is **its own component** and hands back a typed result goes through
`ISednaOverlays.ShowAsync<TComponent, TResult>(parameters)`. Place `<SednaOverlayHost />` once in the
layout. The component takes `[CascadingParameter] SednaOverlay Overlay`, puts `id="@Overlay.Id"` on its
`<dialog>`, renders that dialog on its first render, and closes with `Overlay.CloseAsync(result)` or
`Overlay.CancelAsync()`.

A dialog holding unsaved work **asks before it closes**: put `data-close-guard @oncancel="Dismiss"` on
the `<dialog>`, point its close buttons at the same `Dismiss`, and have it ask and then call
`Overlay.CancelAsync()`. Escape then never closes the dialog by itself. Do not write
`@oncancel:preventDefault` — Razor renders it as a literal attribute and the render throws.

A form holding unsaved work **asks before the page is left**: put `data-close-guard` on the `<form>` and
render `data-dirty="@_dirty"` from the app's own comparison with what was saved. The browser then asks on
a reload, a closed tab or a link out of the app. A link inside the app never unloads the page, so add
`<NavigationLock OnBeforeInternalNavigation="ConfirmLeave" />` and, while dirty, open the app's own
dialog with `ShowModalAsync(id, context.CancellationToken)` and call `context.PreventNavigation()`
unless the reader chose to leave. Leave `ConfirmExternalNavigation` off. A long form's actions are a
`.form-actions--sticky` row, or a `.form-actions--save-bar` that shows only under `data-dirty`.

A session that ends on its own warns **two minutes** before the end, in an app-written
`<dialog class="modal" role="alertdialog">` holding a `<time data-countdown>`; `@onsedna-countdown-end`
closes it and signs out.

A tour step is `ISednaUi.FollowSpotlightAsync(hole, target, options)`; dispose the `SednaSpotlight` it
returns when the step ends.

The panel caps itself at the viewport and the body is what scrolls, so a tall form needs no
`max-height` of its own. A scrolling body holding nothing focusable does need `tabindex="0"` with a
`role` and a name, so a keyboard reader can scroll it.

### Vertical rhythm between page sections is the frame's, not yours

Every direct child of `.page` after the first gets `--page-gap` (`--space-7`) above it, so a card
followed by a card, or a toolbar followed by the list it controls, needs no margin of its own. Do not
write `style="margin-top:1rem"` for a seam between page sections. Headings keep their own larger
rhythm and `.lede` stays tight under its heading; `--page-gap: 0` on `.page` turns the whole thing off,
and `.sedna-mt-0` flushes one child.

Inside a `.card-body` the rhythm is yours, because its content is: a `.kv` stack is tight on purpose
and a form is not. Reach for `.sedna-col` with a gap step rather than a margin per child. Sub-sections
of one body — details, then attachments, then history — are `.card-section`, which draws the hairline
between them and spaces them with the card's own gutter; a second `.card-body` ends the first body's
layout at the seam instead.

### A number in a `style` attribute is usually a knob you have not found

Where a library class has one value an app reasonably wants to change, that value is a custom property
on the element, not a class to override. `--toolbar-input-width`, `--search-max`, `--modal-width`, `--modal-max-height`, `--drawer-width`,
`--scroll-max`, `--skeleton-height`, `--page-gap`, `--sidebar-width`, `--nav-indent`,
`--prose-measure`, `--deck-height`, `--form-combo-panel-width`,
`--col-control-min` / `--col-control-max`, `--col-prose-min` / `--col-prose-max`. Set one inline —
`style="--search-max: 340px"` — rather than declaring `max-width` for a library class, which the
section below is about. If the number you need has no knob, that is a request for the library.

The same goes for a border, a corner, a position or a padding written inline beside a library class.
A panel that draws its own frame — `.code-block`, `.diff`, `.accordion`, `.pdf-frame`,
`.media-player`, `.cal-agenda`, `.cal-month`, `.cal-week`, `.cal-timeline` — takes its `--flush`
modifier inside a card, never `style="border: 0; border-radius: 0"`. A `.guard` positions the element
it covers itself, so its card needs no `position: relative`; `.mochi-host--room` keeps clear the room a
perch reaches into, so the page around Mochi needs no padding for it; and a `.palette` that is not a
`<dialog>` draws the same panel, so it needs no card around it.

### Check the classes this app uses

`SednaContract.Classes` holds every class the installed package declares, with the release that
first shipped it, and `SednaContract.Tokens` the same for tokens. A test can hold this app's markup
to it, so a mistyped class or one newer than the pinned version fails a test instead of rendering
unstyled:

```csharp
Assert.True(SednaContract.Classes.ContainsKey("card-head"), ".card-head is not a Sedna.UI class.");
```

`SednaContract.LatestRelease` is the newest release the history knows, so a test can say which
version a class map was checked against.

### Words

Buttons are verbs and saving is **Save**; an empty state's headline says what to do; an error says
what went wrong and how to fix it; status is never colour alone; sentence case; no "please"; numbers
as digits; dates that cannot be read two ways. The full table is under **Writing the words** on
<https://www.sedna-ui.com/concepts#words>.

### Do not style a class name the library owns

Every class in the catalogue belongs to the library. If this app defines a rule for one of those names,
the two sets of declarations merge on upgrade and the appearance changes with no error and no diff in
this repo.

Before bumping the pinned version, read the class names the release adds and grep this app's stylesheets
for each one. Where a name collides, delete the local rule and use the library's, or rename the local one
into this app's own prefix.

If this app runs Stylelint, extend the package's `tooling/stylelint/sedna-ui.stylelintrc.json`, copied
beside this app's own config: it reports a selector naming a library class and a colour literal outside
`brand.css`.

### Graphs

Records and the links between them are a `[data-graph]` element — copy it from the catalogue's Graph
pages, and read `docs/graph.md` (the MCP resource `sednaui://docs/graph`) for every attribute. The
library ships the engine and loads it itself; the host page needs nothing added.

- **Do not add a graph or chart library**, and do not import anything under `_content/Sedna.UI/lib/`.
  The engine is reachable, unversioned, as the graph handle's `cy` — for what the markup cannot say.
- **Colour a record with `data-tone`** (`1`–`6`, `go`, `warn`, `danger`, `info`, `muted`), never a hex:
  the canvas follows the theme only through tones. A legend swatch is `.graph-swatch` with the matching
  `.series-*` class.
- **The toolbar, legend, filters, tooltip, menu and side panel are your markup** with `data-graph-*`
  attributes; the script fills and places them. Write the `.graph-data` items as sentences — they are
  the graph's text for a screen reader.
- **From C#, use `ISednaGraphs`** by the element's id in `OnAfterRenderAsync`, and the
  `@onsedna-graph-*` events. Records that arrive by call want `data-graph-deferred`; a large graph wants
  `data-graph-src` or `SetDataAsync` rather than a Razor-rendered list.
- **A link the reader draws is yours to add** (`@onsedna-graph-connect`), as a drop is.
- **A button on a link is yours to act on** (`@onsedna-graph-edge`, with the button's `value`), and so is a
  placeholder record opened (`@onsedna-graph-open`, `Placeholder` true): change your data and render.

### Workflows

A run through a process — an automation, an agent's work, a release — is an `ol.workflow` copied from
the catalogue's Workflow pages. It is markup and classes; no script draws it.

- **State goes on the step.** `data-state` on the `li.workflow-step` — `next`, `running`, `waiting`,
  `done`, `failed`, `skipped`, or none for a step not reached — never on the node and never on a wire:
  every wire takes the state of the step it leads into. A decision's branches take `data-state="taken"`
  or `"skipped"`.
- **Keep the latest state of each step and bind it.** Each event the automation sends updates a
  dictionary of step to state, and the markup binds `data-state="@S("step-id")"`; a step nobody has
  mentioned renders no attribute. The catalogue's "Watching a run" page is the pattern.
- **Say the state in words** — the meta line or a `.visually-hidden` span. The tick and the light are
  paint, and a screen reader hears the list.
- **Write the structure, not positions.** A fork holds branches, a branch holds a path, a loop holds the
  path it repeats; the connectors are drawn from that. A flow too large to write by hand — generated from
  the automation's own definition — is a graph with `data-state` on its records instead.
- **A step the reader adds** is a `<button class="workflow-node workflow-node--add">` in a step of its
  own with no state, handled by your `@onclick`: it is dashed and unfilled, and so is the wire into it.
- **A diagram that scrolls takes `tabindex="0"`**, so the keyboard can reach it, and `data-follow` keeps
  the step at work in view in one wider than its container.
- **On a phone or a tablet held upright** the diagram runs down: `--down`, or `--auto` to run across on
  a wide screen and down below 640px. Same markup either way.
- **Showing rather than reading** — a wall screen, a tablet — is `--xl` or `--lg`, `--fill` and
  `--focus` (only the step at work opens), with a `data-fullscreen` button. Other colours are
  `--workflow-trail` and `--workflow-flow`/`-line`/`-tint`/`-glow`, set from tokens; never restyle the
  classes.
- **An agent's step** carries `.workflow-node--agent` on its node, whatever its state, and wears the
  agent's colours while it is at work; `.workflow-node--glow` is a plain step with a glow. The wires
  never take the agent's colours.
- **Too much movement** is `--flow-off` (a still, lit line). `--workflow-flow-speed` and
  `--workflow-flow-packets`, set on the diagram, make it slower or faster and sparser or denser; `1` is
  the default for both.

### Rich-text editors

- **The value is a `textarea[data-editor-value]`** inside `[data-editor]`. Bind it with `@bind` like any
  textarea, post it with a form, validate it on the server — the editor writes clean HTML to it, but that
  is not a sanitiser.
- **The toolbar is your markup**: buttons with `data-editor-format` or `data-editor-action`. The formats
  the toolbar offers are the only ones the document accepts, pasted text included.
- **Do not install another editor package, and do not load Quill yourself.** The library ships Quill and
  loads it on demand; see `docs/editor.md`.
- **From C#, `ISednaEditors`** — only for what a binding cannot do, such as typing at the caret.

### Icons

Remix Icon is bundled in the package. Link `_content/Sedna.UI/lib/remixicon/remixicon.css` and use
`ri-*` classes on an `<i>`. Do not add a second icon set and do not load icons from a CDN.

### CSS variables

The only file in this project that may contain `:root { --… }` is `wwwroot/css/brand.css`, and it may
only **redefine tokens the library already declares** — normally `--brand*`, `--accent` and
`--sidebar-active`.

**Never declare a new `--` name that the library does not define.** A future library version may
introduce that exact name with a different meaning, and this app would silently break on upgrade.

If a value you need is genuinely missing from the library:

1. Prefer requesting it upstream — see *Requesting a library change* below. A token added to the library
   benefits every app and survives upgrades.
2. If you need it before that lands, use an **app-scoped** variable with this app's own prefix
   (`--<APP-PREFIX>-…`) in an app stylesheet, not a bare name in the shared namespace. Prefixed names
   cannot collide with a future library token.

Do not write a rule for a library class name to work around a missing value. Overriding `.btn` or
`.card` in this app means the next library upgrade fights you, which is the drift the shared library
exists to prevent. Put an app-prefixed class on the element instead.

### Safe area — do not write `env()` yourself

The host page carries `viewport-fit=cover`, so the viewport reaches the physical edges of the display
and `env(safe-area-inset-*)` reports real values on a notched phone. The library's frame already holds
itself clear: the shell pads the top and both sides, and everything pinned to the bottom edge — a
sheet, the toast stack, the FAB, the reconnect bar — pads itself.

For **this app's own** element on a viewport edge (a sticky action bar, a footer, a bottom nav, a
cookie banner), use `.sedna-safe-bottom`, `.sedna-safe-top` or `.sedna-safe-inline`, or read
`--safe-block-start`, `--safe-block-end`, `--safe-inline-start` and `--safe-inline-end`.

Two reasons not to reach for `env()` directly: the inline pair is mirrored for `dir="rtl"`, which
`env()` cannot do, and a token can be set to a value in a test where `env()` reports `0px` in every
browser a test can drive.

Put the class on the element that carries the background, not a wrapper — the utilities add
**padding**, so the element's own colour fills the strip instead of leaving a transparent band.

### Overriding is now trivially easy, which is the reason not to

The library's stylesheet is entirely inside cascade layers. **This app's CSS is unlayered, so any rule
here beats any library rule, whatever the specificity.** A longer selector is never needed to win.

That makes discipline the only thing left protecting the shared design:

- **Redefine tokens, not rules.** A token override travels with the theme, the colour-blind palette and
  every future version. A rule override is a private fork of the design that nothing will tell you has
  gone stale.
- **Two things the layering means for this app's own CSS:** a token set at bare `:root` here also beats
  the library's `[data-variant="light"]` value for it, so set both blocks; and any unconditional rule
  here beats a library rule at any specificity. The case to check is a copied
  `.table th, .table td { padding: … }` or `.card-body { padding: … }`, which stops compact density
  from tightening this app's tables and cards. **Delete copied library rules** — including a
  `:root[data-density="compact"] .card-body` rule written before cards compacted on their own.
- **Never use `!important` against the library.** Layer order inverts for important declarations, so it
  is not needed and makes the result harder to reason about, not easier.
- **To change this app's background, register a theme — do not override `--bg`.**
  `SednaRamp.Surface("#18181b")` generates the whole base ramp from the colour the canvas should be,
  which moves the canvas, the chrome, the cards, the borders and all three elevation levels together
  and in both variants. Overriding `--bg` alone moves one of those and leaves the rest blue.
- **Nested surfaces are automatic.** A `.card` inside a `.card` takes `--surface-raised-2` and one
  inside that takes `-3`; do not paint a nested card by hand. The named surfaces —
  `--surface-app`, `--surface-chrome`, `--surface-content` — are what to read when this app paints a
  surface of its own, so it lands on the same colours as the frame.

### Requesting a library change

`Sedna.UI` is a separate, versioned package. This app cannot release it.

- Open an issue or pull request at <https://github.com/dennisrahmen/Sedna.UI>.
- Describe the value or variant needed and where it is used, not just the CSS you would have written.
- Adding a token or variant is a minor release; renaming or removing one is major. A release can be
  major with nothing renamed or removed — changing an existing rule enough to move layout or colour, or
  making an override in this app stop working, both count. Read the notes, do not assume minor is safe.

Once a version containing the change is published, bump the reference here.

### Load order

```html
<script src="_content/Sedna.UI/js/Sedna.UI.boot.js"></script>
<link rel="stylesheet" href="_content/Sedna.UI/lib/remixicon/remixicon.css" />
<link rel="stylesheet" href="_content/Sedna.UI/css/Sedna.UI.css" />
<link rel="stylesheet" href="css/brand.css" />
```

At the end of `<body>`:

```html
<script src="_content/Sedna.UI/js/Sedna.UI.js"></script>
```

`brand.css` must load after the library stylesheet.

Settings are stored under the default `sedna.` prefix. Do not set `data-prefix` / `storagePrefix` unless
this app shares an origin with another app; if you do set one, both values must be identical.

If this app registers a theme of its own as `SednaUiOptions.Default`, put that name on the boot script
as `data-theme-default` — render it from the options, do not retype it. It is what `<html data-theme>`
carries on a first visit, and the built-in `sedna` there selects the wrong palette.

### Upgrading

The version is pinned and does not float. Before bumping it:

1. Read the release notes at <https://github.com/dennisrahmen/Sedna.UI/releases>.
2. Grep this app's stylesheets for every class name the release adds — see above.
3. Check the pages this app renders, in each theme and variant (`data-theme`, `data-variant`,
   `data-cvd`, `data-density`).

After bumping it, build. The package's class check warns `SEDNAUI001` for each use of a class a release
renamed, with the classes that replace it, and `SEDNAUI002` for one it removed. Apply the renames with the
package's script — `python3 -I ~/.nuget/packages/sedna.ui/<VERSION>/tooling/rename-classes.py --from
<the previous version> --dry-run .` prints the diff, and without `--dry-run` it writes it. Replace a
removed class by the release notes. Do not silence either warning to finish an upgrade.

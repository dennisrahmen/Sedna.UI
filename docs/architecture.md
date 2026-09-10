# Architecture

## Two tiers

**Tier 1 — the frame.** Shell, sidebar and nav, header, user widget. Layout chrome that is
pixel-identical in every app and is not restyled per project. Shipped as CSS classes.

**Tier 2 — the paint.** Tables, forms, cards, badges, buttons, alerts. Shipped as semantic CSS classes.
Pages write plain HTML and apply the classes.

Content UI is always a class, never a component. There is no `<DataTable>` and there will not be one.

Both tiers are CSS classes. The package ships one component, `SednaBrandStyle` — a service that
emits infrastructure CSS, not markup a page depends on. See [Branding](#branding).

## The frame

The frame is CSS classes: `.layout > .content > .page`, `aside.sidebar` with `.brand`, `nav.nav >
.nav-scroll` and `.nav-tools`, `header.topbar` with its `.topbar-spacer`, `.user-widget`. The markup is
on the catalogue's *Shell and nav* page, and the catalogue application writes exactly that markup by hand
— so a regression in the frame shows up on the documentation site first, and `FrameMarkupTests` fails
if the app names a class the stylesheet does not define or one the page does not show.

`.page` is the only scroll container; do not wrap it in another. `.collapsed` on `.sidebar` gives the
56px rail and changes nothing else, because the rail is pure CSS.

### The C# surface

Three things the package ships that markup cannot express:

| Member | Purpose |
|---|---|
| `ActiveLink.IsActive` / `CssClass` / `AriaCurrent` | Which navigation link is the current page |
| `ISednaUi` | Typed access to `sednaUi` — toasts, confirmations, clipboard, settings, palette, search, the Markdown editor |
| `AddSednaUi()` | Registers the above, scoped to the circuit |

`ActiveLink` drops the query string and the fragment, treats a trailing slash as insignificant, and
requires a prefix match to end on a path segment, so `/queue` does not light up on `/queue-archive`. The
link to the app root needs `NavLinkMatch.All`. The helpers are pure and do not subscribe to
`LocationChanged`: a page re-rendered by navigation picks up the new state for free, and navigation
markup that survives navigation subscribes itself — **in the component that renders the links**, not in
the layout around it. Blazor only hands new parameters to a child whose parameters differ, so a
subscription one level too high re-renders the layout and leaves the links reading the previous address.

`ISednaUi` is `IJSRuntime` calls, so none of it can run during prerendering. Two members of the
JavaScript surface have no wrapper because neither can cross the boundary: `toast()` returns a remover
function, and `tips.gate` is a predicate an app assigns.

The only package dependency is `Microsoft.AspNetCore.Components.Web`, which is where
`NavigationManager`, `NavLinkMatch` and `IJSRuntime` live. It is a `PackageReference` rather than a
`FrameworkReference` because the shared framework is not available to a Blazor WebAssembly consumer. A
test fails on any third-party package reference, and `build/verify-package.sh` asserts the packed
dependency list is exactly that one name.

## Responsive frame

`.layout--responsive` on `.layout` turns the sidebar into the icon rail below 900px and trims the user
widget's text at 900px and 560px. **Opt-in**: applying it automatically would change how every released
app looks on a narrow screen with no app edit.

It is also the fix for the reverse hazard. An app that sets `.sidebar { width: 260px }` unconditionally
would beat a bare `@media .sidebar { width: 56px }` rule on source order; `.layout--responsive .sidebar`
is `(0,2,0)` and outranks it.

Two rules hold for any layout media query added later:

- **Geometry only**, or a value that comes entirely from a token. A colour set inside a breakpoint is
  invisible to both a rebrand and a theme remap, and reappears at one window size.
  `Layout_media_queries_only_change_geometry` enforces it.
- The responsive rail **duplicates** `12-frame-collapsed-rail.css`, because CSS cannot alias a selector
  — there is no way to say "also apply the rail when this media query matches".
  `The_responsive_frame_mirrors_the_collapsed_rail` fails when the two drift.

## Cascade layers

The shipped stylesheet is entirely inside cascade layers, declared up front:

```css
@layer sedna.tokens, sedna.base, sedna.frame, sedna.paint, sedna.utilities, sedna.overrides;
```

**Your stylesheet is unlayered, so it beats all of them, whatever the specificity.** That is the point:
overriding the library never needs a longer selector than the library's, and there is nothing to
out-specify.

| Layer | Parts | Holds |
|---|---|---|
| `sedna.tokens` | `00`–`04` | tokens and the theme remap blocks |
| `sedna.base` | `05`–`09` | bare element styles |
| `sedna.frame` | `10`–`29` | tier 1 |
| `sedna.paint` | `30`–`79` | tier 2, then RTL, forced colours, print |
| `sedna.utilities` | `80`–`89` | single-purpose classes |
| `sedna.overrides` | `90`–`99` | density, reduced motion |

Each part's layer comes from its numeric prefix, so it cannot drift away from the source order. Two
tests hold the model up: one fails if any rule escapes a layer — an unlayered library rule would
outrank the whole library *and* be unreachable from your stylesheet — and one fails if a layer is used
without being in the ordering statement, since an undeclared layer sorts after every declared one.

### Three things it means for your own stylesheet

**A token you set at bare `:root` beats the library's `[data-variant="light"]` value for it.** Set both
blocks, as the rebrand recipe shows.

**An unconditional rule of yours beats a library rule at any specificity.** The case to check is
compact density: an app carrying its own `.table th, .table td { padding: 8px 12px }` wins over
`:root[data-density="compact"] .table td`, so compact density stops tightening its tables. Delete the
copied rule.

**`!important` is actively harmful here, not merely unnecessary.** Layer order *inverts* for important
declarations, so an `!important` inside `sedna.paint` is harder for you to override than an ordinary
declaration. The library uses none, and a test enforces it.

## Focus rings

Every focus ring in the library is a `box-shadow` in a brand ring token, never an `outline`.
`71-forced-colors.css` restates each one as an `outline`, because `box-shadow` is not painted in
forced colours.

Two rules in `06-base-elements.css` decide when a ring appears at all:

- `:focus:not(:focus-visible)` drops the outline for pointer focus and for focus restored on reload.
- `[tabindex="-1"]:not(a, area, button, input, select, textarea, summary, [contenteditable], [role])`
  drops it for `:focus-visible` too. `tabindex="-1"` takes an element out of the tab order, so nothing
  carrying it is reachable by Tab and nothing carrying it needs a ring to say where the keyboard is.

The second rule exists for `FocusOnNavigate`, the Blazor Web App template's own component: it puts
`tabindex="-1"` on the page's `<h1>` and focuses it after every route change, and Chromium's
`:focus-visible` heuristic reads that as keyboard focus, so the heading arrives wearing the UA outline.
Keep `FocusOnNavigate` — it is what announces a client-side navigation to a screen reader.

A **roving-tabindex** widget is the exception the exclusions carve out: a `.tab`, a menu item or a
listbox option sits at `tabindex="-1"` and *is* reached, by arrow key. Every one of those is a control,
so an interactive tag or any ARIA role keeps its ring.

A **skip-link target** — `<main id="main" tabindex="-1">` — loses its ring, deliberately: it is a
viewport-sized box that says nothing, `<main>` is not keyboard-operable, and the skip link itself keeps
its ring while it is the focused control. To put it back, write `main:focus-visible { outline: … }` in
your own stylesheet; it is unlayered and beats the library.

## The token contract

- Tokens are declared in the library's `:root`, plus the light and colour-blind blocks.
- Every class references tokens. The library CSS contains no colour literals outside the token
  declarations.
- An app redefines tokens in its own stylesheet, loaded after the library. Normally the `--brand*` family,
  `--accent` and `--sidebar-active`.
- Some tokens are derived from others rather than restated. `--brand-tint`, `--brand-ring`,
  `--brand-ring-soft`, `--brand-ring-check` and `--brand-glow` are `color-mix()` of `--brand`, so
  redefining `--brand` carries all five. An app may still pin any of them to change the alpha.
  A derived token serialises through `getComputedStyle` as `color(srgb …)` rather than `rgba(…)`; the
  painted result is unchanged, but a tool that compares computed-style *strings* will report a difference
  where there is none.
- Apps must not declare new token names. Request the token instead — see
  [CONTRIBUTING.md](../CONTRIBUTING.md) — and use an app-prefixed variable (`--myapp-…`) in the meantime.
  A bare new name risks colliding with a future library token.

Theme differences are expressed only as token values, so the light and colour-blind blocks contain no
selector overrides and CSS load order does not affect them.

The full token list is on the [Tokens](https://www.sedna-ui.com/tokens) catalogue
page, which reads its values from the live stylesheet.

Every token also ships as JSON, for a design tool that needs the values without parsing CSS:

| Where | Path |
|---|---|
| In a running app | `_content/Sedna.UI/tokens/Sedna.UI.tokens.json` |
| In the repo, and in the restored package | `wwwroot/tokens/Sedna.UI.tokens.json` |
| Hosted | <https://www.sedna-ui.com/_content/Sedna.UI/tokens/Sedna.UI.tokens.json> |

`blocks` is an **ordered** array of `{ media, selector, tokens }` — merge them in order, applying a block
when its media condition matches and its selector matches the document root. It is not a map keyed by
theme: `:root` appears three times across the ten blocks (base, forced-colors, density), so a map would
silently lose two of them, and the media condition is part of the contract.

Generated by `build/export-tokens.sh` from the same parts as the stylesheet;
`The_token_export_matches_the_stylesheet` fails on drift, and `verify-package.sh` asserts the path ships.

### One control height

`--control-height-sm`, `--control-height` and `--control-height-lg` (28, 36 and 44px) are the height of
everything that can sit in a row with another control: `.btn`, `.btn-icon` (which is also its width),
`.form-input`, `.form-select`, `.form-value-display`, `.input-group`, `.stepper`, `.search-input` and
`.chip`. Each has an `-sm` and an `-lg` variant taking the matching token. A badge does not: it is a
label rather than a target, so it has its own type scale in `31-badges.css`.

One token per tier, not a number per control. `ControlRowTests` measures the whole set in a browser at
all three tiers, because whether a `min-height` *binds* is a question about padding and line boxes that
no source scan can answer.

Two consequences for a rule that sets a control's height:

- Keep the control's own natural height **under** the token, or the token stops deciding anything and
  the control silently grows past it.
- If a wrapper draws the border — `.input-group` does — the wrapper takes the height and the children
  give theirs up, or the wrapper ends up two border-pixels taller than a bare control.

## Theming

`data-theme`, `data-variant`, `data-cvd="1"`, `data-density="compact"` and `dir` are set on `<html>`.

- `Sedna.UI.boot.js` applies them from `localStorage` before first paint.
- `sednaUi.settings.save('variant', 'light')` (or `'theme'`, `'cvd'`, `'density'`) updates them at
  runtime.
- **`ISednaSettings` is the C# view of the same state** — `Current`, setters, and a `Changed` event.
  `StartAsync()` in `OnAfterRenderAsync(firstRender)` reads the browser and subscribes; the service
  never writes the attributes itself, so `sednaUi.settings.apply()` stays the only writer. The case
  it exists for: a stored preference of `"system"` follows the OS while the page is open, and an app
  that only called `LoadSettingsAsync` once never hears about it.

The **base** — canvas, chrome, cards, borders, muted text — is the `slate` ramp, so a theme changes
it like any other ramp. `SednaRamp.Surface(anchor)` generates all fourteen surface steps from the
colour the canvas should be, because `FromAnchor`'s curve is fit to brand hues and puts step 900 at
more than half lightness. `SednaTheme.Graphite` is the shipped example. The named surface roles
(`--surface-app`, `--surface-chrome`, `--surface-content`) and the elevation ladder
(`--surface-raised-1/-2/-3`, applied automatically to a card inside a card) resolve through that
ramp, so they follow a themed base with no rule changed.

`boot.js` also writes two **cookies**, both opt-in and neither read by the library — they exist so a
server-rendered app can get right on its first render what only the browser knows.
`data-lang-cookie="true"` writes the stored language to `<prefix>lang`; `data-tz-cookie="<name>"`
writes `Intl.DateTimeFormat().resolvedOptions().timeZone` to the cookie named, and only when it
differs from the one already there. Both are written before first paint. The first request of a session
carries neither, so an app needs a configured fallback and must **not** reload to obtain one: the next
navigation already carries it.

`dir` and `lang` are the two that are written **only from a stored choice**. Both are attributes the host
page declares about itself, so with nothing stored they are left exactly as the document wrote them — the
library never infers a document's direction or language from the browser's. Never derive `lang` from
`navigator.language`: it relabels an English page as German for anybody visiting with a German browser.

**`data-theme` and `data-variant` are two orthogonal attributes**, both always present, never absent:

- **`data-theme`** — *which* theme, by name; `sedna` with nothing stored.
- **`data-variant`** — `dark` or `light` — *which variant* of it, what a reader toggles.

Consuming apps select on `:root[data-variant="light"]` to brand the light palette, so that selector has
to match whenever the light palette is in use. The two used to be one attribute (`data-theme` took
`dark`/`light` directly), which is why there was nowhere to put a second theme; splitting them is a
breaking change, permitted pre-1.0 (see [releasing](releasing.md)).

`prefers-color-scheme` resolves into `data-variant` inside `boot.js`, never as a `@media` block —
a media block would make the light palette reachable without the attribute, and every consuming app's
light-theme branding would silently stop applying. `sedna.variant` in `localStorage` can hold a stored
`"system"`, which is a real, user-selectable third mode rather than a value collapsed to `dark`/`light`
on read: with it stored, the variant follows a live `matchMedia('(prefers-color-scheme: light)')`
listener, and a stored `dark`/`light` still wins outright over both the listener and the boot script
tag's own `data-variant-default`.

The colour-blind palette (`data-cvd="1"`) remaps only the `go` family to blue, so go and danger read as
blue against red. Amber and the brand colour are unchanged.

`data-density="compact"` tightens `.table` padding. Apps tighten their own page-specific components.

## Branding

A theme is data, not a stylesheet. `SednaTheme` names a palette for each of the two variants —
`Dark` and `Light` — and both are required constructor arguments, so a one-variant theme cannot
be constructed. `SednaTheme.Sedna` is the built-in, built from the literal values in
`00-palette.css`; `SednaThemeTests` asserts the two never drift apart. For Sedna itself the two
variants are the same palette object, because tier 1 is not remapped by variant (see
[Theming](#theming) above) — a theme whose brand needs different anchors per variant supplies
two different palettes instead.

A `SednaPalette` holds the twelve ramps `00-palette.css` declares — `Slate`, `Coral`, `Orbit`,
`Navy` and the eight support hues — each a `SednaRamp`. Build a `SednaRamp` from an
already-designed set of steps, taken verbatim, or generate one from a single anchor colour:

```csharp
var ramp = SednaRamp.FromAnchor("#2f6fed", anchorStep: 500);
```

`FromAnchor` follows `docs/BRANDING.md` §2.1 — a lightness curve shared across every generated
ramp, and a chroma bell that peaks at the anchor step. It is implemented over a direct sRGB ⇄
OKLCH conversion, not a package: the library takes no third-party dependency (see
[the token contract](#the-token-contract) above).

Register themes and emit the palette CSS:

```csharp
// Program.cs
builder.Services.AddSednaUi(o =>
{
    o.Themes  = [SednaTheme.Sedna, myTheme];
    o.Default = "sedna";
});
```

```razor
<head>
    <SednaBrandStyle />
</head>
```

`SednaBrandStyle` reads the registered themes from DI and renders one `<style>`: `:root { … }`
for `Default`'s palette, then `[data-theme="<name>"] { … }` for every other registered theme —
palette tokens only, never the semantic tier, which already ships in the stylesheet and already
points at the palette. It renders as part of the server-rendered document rather than being
injected by JavaScript, so no page flashes the wrong colours before the theme applies.
`SednaUiBrand.ToCss(SednaUiOptions)` is the static method behind it, for an app that wants to
emit the CSS itself — per-request multi-tenant branding, for instance.

## Semantic families

Used consistently across buttons, badges and alerts:

| Family | Meaning |
|---|---|
| `go` | Sends something outward — approve, apply, send |
| `warn` | Changes who is in control — take over |
| `danger` | Destructive or failed |
| `info` | Informational |
| `secret` | Sensitive values |
| `cyan`, `orange`, `teal` | Categorical only, no meaning |

A panel's primary action is a filled button in its semantic colour.

## Z-order

| Layer | z-index | What sits there |
|---|---|---|
| Local stacking inside a component | 0, 1, 2, 3 | **not the overlay scale** — see the note below |
| Topbar | 60 | `.topbar`, `.fab` |
| User widget | 200 | `.user-widget` |
| Collapsed-rail flyout | 400 | `.sidebar.collapsed [data-tip]:hover::after` |
| Drawer scrim | 480 | `.drawer-scrim` |
| Drawer panel | 490 | `.drawer` |
| Modal backdrop | 500 | `.modal-backdrop` |
| Spotlight | 510 | `.spotlight-hole`, `.spotlight-tip` |
| Popover, dropdown menu | 550 | `.menu`, `.search-panel`, `.popover`, the user widget's own panel |
| Toast | 600 | `.toast-stack` |
| Hover hints, reconnect banner | 1000 | `.sedna-tip`, `#components-reconnect-modal`, `.skip-link` |

A new overlay uses one of these values. `Every_z_index_comes_from_the_documented_scale` fails on any
other, so adding a layer means adding it to this table first.

**The local values are a scale of their own, and a sticky table uses three of them.** They order
cells inside one table and never anything else: `1` is a pinned column's body cells, above the static
cells they slide across; `2` is `.table--sticky`'s header row, above those; `3` is the corner cell of a
pinned column, which is sticky on both axes and has to beat the other header cells as well — it is
first in DOM order, so at an equal z-index every one of them paints over it. That is the whole reason
the band is three wide rather than two.

**The pinned column starts at `1` and not at `0`, and the difference is not cosmetic.** `0` is not
above `auto`: a positioned child of an ordinary cell — a `.segmented-option`, a `.switch`, a
`.menu-anchor` — paints in the same step of the stacking order as a `z-index: 0` stacking context, and
the tie is broken by tree order, so every such control in a column to the right of the pinned one slid
over the top of it as the table scrolled. It read as the pinned cell being transparent. `0` stays on
the scale for a component that wants a floor of its own.

Two things the flat list does not say:

- **`.topbar` (60) and `.user-widget` (200) create stacking contexts.** A panel nested inside either is
  ordered *within* that context, so its z-index is local and cannot lift it above a modal backdrop. A
  dropdown that must escape belongs in the top layer instead.
- **The top layer ignores z-index entirely.** An element promoted by `popover` or `dialog.showModal()`
  paints above every non-top-layer element regardless of this scale, and among top-layer elements the
  order is promotion order, not z-index. Once a family moves to the top layer, its row here describes
  the fallback path only. `.popover` and the command palette are both already there.
- **The collapsed rail's flyout is `position: fixed`**, not absolute, because `.nav-scroll` scrolls and
  would otherwise clip it. It is still on rung 400: fixed positioning escapes an ancestor's `overflow`,
  not the z-order.
- **`.menu-anchor > .menu` is fixed and anchor-positioned, for the same reason.** An absolutely
  positioned panel is laid out inside the nearest scroll container and counts towards its scrollable
  overflow, so a menu opened from a toolbar, from a `.sedna-scroll-x` around a table or from any app
  container with an `overflow` of its own both grew that container a scrollbar and was clipped at its
  edge. It stays on rung 550, and `position-visibility: anchors-visible` hides it when its trigger
  scrolls out — a fixed panel does not travel with its anchor's scroller. `.user-widget > .menu` keeps
  its own `position: absolute` anchoring: the widget is not a scroll container.

## JavaScript

`Sedna.UI.js` exposes the global `sednaUi`:

| Member | Purpose |
|---|---|
| `configure(options)` | Storage prefix, notification icon, language cookie |
| `settings` | `load()`, `save(key, value)`, `apply()`, `onChange(fn)` → unsubscribe. Keys: `theme`, `variant`, `cvd`, `density`, `dir`, `lang` |
| `tips` | Hover-hint engine. Set `tips.gate = el => bool` to suppress hints conditionally |
| `toast(message, options)` | Creates and reuses its own `.toast-stack[data-sedna-toasts]`, and leaves any stack the app wrote alone. Returns its own remover; `timeout: 0` stays until dismissed |
| `confirm(options)` | A `<dialog>.showModal()` confirmation. Returns a promise; `danger: true` reddens confirm and focuses cancel |
| `modal` | The platform dialog for an app's own markup: `show(id)` → `showModal()`, `close(id, value)` → `close(value)`. An id that is not a `<dialog>`, or one already open, warns in the console and does nothing — an exception crossing the interop boundary from a Blazor handler tears down the circuit |
| `menu` | Delegated dropdowns. `closeAll()`, for after a navigation |
| — | `22-anchored.js` adds no member. It closes an open `.menu` or `.popover` when a scroll moves its trigger, because an anchored `position: fixed` panel's offset is computed at reveal and never recomputed while the anchor scrolls — see the anchor-positioning note above |
| `tabs` | Delegated tabs with the arrow/Home/End keyboard contract. `select(tabOrPanelId)` |
| `select` | `refresh(root?)` → how many it fixed. Fills in the `<selectedcontent>` clone a customizable `<select>` should have made and Blazor's render prevents, leaving the closed box blank. Runs on load and after any render that adds nodes; an app calls it only for a select it moved into place some other way |
| `palette` | Command palette, opened by Ctrl/⌘-K once commands exist: `register(list)`, `open()`, `close()`, `rank(query)` |
| `search` | Header search behind a `data-search` input: `register(items)`, `rank(query)`, `close()` |
| `dropzone` | Delegated drag-and-drop for a `data-dropzone` zone: maintains `.dropzone--over`, hands a dropped file to the zone's own `input[type=file]` as a `change` event. `reset()` clears the highlight |
| `output` | Follow-tail for a `data-follow` output pane: sticks to the newest line, releases when the reader scrolls up, re-attaches when they scroll back down. `follow(pane)`, `isFollowing(pane)` |
| `codeBlock` | `toggle(block, expanded?)` — expands or collapses a `.code-block--clamped`. Delegated from `[data-code-expand]` |
| `spotlight` | Tour geometry and the input model, not the sequence. `at(hole, target, { pad, include })` positions `.spotlight-hole` over an element, a list of them or a selector, and returns the rectangle — `null` when nothing visible is left; `tipAt(tip, rect, { placement, gap, margin, boundary })` places the bubble on any of the four sides, flips it when the side does not fit, clamps it into the viewport — or into `boundary` — and reports the side used; `follow(hole, target, opts)` keeps both attached across scroll, resize and re-render, returning `{ update, stop, side }`; `lock(opts)` / `unlock()` make the rest of the page inert and gate hover hints. The steps, the copy and the order stay the app's |
| `md` | Markdown editor: `init(root?)` wires every `.md-editor` in `root` (the document by default) and is idempotent per editor; `apply(textarea, cmd)`, `render(src)` |
| `copyText`, `openTab`, `viewportWidth`, `scrollPageTop` | Interop helpers. `scrollPageTop` resets `.page`, which is the only scroll container the frame has and therefore the one navigation leaves where it was |
| `getItem`, `setItem` | `localStorage` access |
| `watchSettings`, `unwatchSettings` | The Blazor bridge for `settings.onChange`: takes a `DotNetObjectReference` and returns an id to unwatch with. `ISednaSettings` is the C# side; nothing else should call these |
| `requestNotify`, `notify`, `ping` | Desktop notifications and an audio ping |

`ui._` also exists and is **private** — shared closure state the parts need. It may change in a patch.

Several behaviours are delegated from `document`, so content rendered after load is covered without
re-wiring: hover hints, `data-menu-toggle`, `data-tabs`, `data-search`, `data-dropzone`,
`data-sheet`, and `data-copy` / `data-copy-target`. The last two have no member on the global — the
attribute is the whole API.

`data-sheet` on a `<dialog class="sheet">` opts into dragging it down to dismiss: the sheet follows
the pointer from the handle or the header, and closes past a quarter of its height or on a flick.
It is `<dialog>`-only, because the `div` form's open state is a class the app owns. The handle stays
unfocusable, so the drag is deliberately not a keyboard path — the close button is what makes a
sheet dismissible.
Elements inside `.sidebar` are skipped by the hover hints — the collapsed rail has a CSS flyout instead.

`palette` and `search` rank with the same matcher, `ui._.score`. Two copies would drift, and the drift
would be found by someone seeing one query ordered two ways on one page.

`search`'s index lives in the browser, which is what makes it suitable for a fixed, known set — an
app's pages, reports and settings screens — and unsuitable for a database. An app that needs a query
per keystroke owns that itself: the debounce length, cancelling a superseded keystroke and the busy
state are all decisions about *its* backend. It renders `.search-panel` with the same classes and
leaves `data-search` off, and this file stays out of the way.

`sednaUi.md.render()` escapes HTML before re-introducing a fixed set of Markdown constructs, and
restricts link hrefs to `http:`, `https:`, `mailto:` and root-relative paths. Sanitise untrusted input
server-side as well.

## Icons

[Remix Icon](https://remixicon.com) 4.9.1 is bundled at `_content/Sedna.UI/lib/remixicon/`. It is the
only icon set; the library styles `i` elements (`.btn i`, `.nav-link i`) and the icon classes come from
this font.

Only `woff2` is shipped. Upstream also carries eot, woff, ttf and svg for IE and iOS 4, which no browser
running Blazor Server needs.

The icons remain under the Remix Icon License v1.0, not Apache-2.0. See
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

## State illustrations

`_content/Sedna.UI/img/Sedna.UI.states.svg` holds thirteen `<symbol>` drawings, one per state:
`nothing-yet`, `no-results`, `filtered-out`, `no-access`, `failed`, `waiting`, `not-found`,
`server-error`, `session-expired`, `maintenance`, `all-done`, `first-run`, `offline`. A page
references one by id:

```html
<svg class="state-art state-art--lg" aria-hidden="true">
    <use href="_content/Sedna.UI/img/Sedna.UI.states.svg#no-access" />
</svg>
```

Three sizes — `--sm` 56px, the bare class 96px, `--lg` 152px. There is no smaller step: below 56px the
3-unit stroke fills in.

This is not a second icon set. An icon set is a vocabulary a page draws from; this is a fixed drawing
per fixed state, and adding one an app chooses between is what the rule in `CLAUDE.md` bans.

The file writes no colour. Line work is `currentColor`, which `.state-art` points at `--muted`; the one
accent per drawing is `var(--state-accent)`, which the containing block re-points — `.empty-state--failed`
moves it into the danger ramp. Inherited custom properties cross into a `<use>` shadow tree, which is
what lets one file follow four themes.

`StateSpriteTests` guards it: well-formed XML, no `--` in a comment (which breaks the whole file, not one
drawing), no literal colour, one shared `viewBox`, and the id list in `42-state-art.css` matching the
symbols. `ShippedPathTests` and `build/verify-package.sh` pin the path.

## The MCP server

`/mcp` on the catalogue application: Streamable HTTP, stateless, **public, unauthenticated and
read-only**. Six tools, plus four resources — `sednaui://stylesheet`, `sednaui://tokens`,
`sednaui://version` and `sednaui://docs/{name}`. There must never be a seventh tool that writes: a
client honouring the read-only hint calls these without prompting.

| Tool | Returns |
| --- | --- |
| `search` | References only, never markup, across examples, classes, tokens and pages. |
| `get_example` | The bytes the site renders for an example, valid in a `.razor` page and an `.html` file alike. |
| `describe_class` | What the shipped stylesheet declares for a class, its layer, its modifiers, and the examples using it. |
| `get_page` | Every example on one catalogue page, or the list of pages. |
| `get_tokens` | The token export, as an ordered array of blocks. |
| `get_integration_guide` | This repository's own documentation, verbatim: `host-page`, `branding`, `javascript`, `rules`. |

Nothing in the index is hand-listed. Examples come from the same embedded resources the pages render,
classes from the stylesheet the app serves, docs from `docs/`.

### No output schema

A tool's payload is the JSON text of its response, read from `content[0].text`. **No tool declares an
`outputSchema`**, and a test fails on any schema a tool publishes that uses a boolean subschema.

The tools return anonymous objects, which the SDK cannot describe, so enabling structured content made
every one of them advertise the placeholder `{"type":"object","properties":{"result":true}}`. That is
legal JSON Schema and the Zod validator in the MCP TypeScript SDK rejects it, so a client validating the
tool list dropped all six tools — while the server stayed connected and its instructions loaded. The
whole surface vanished with no error anywhere.

### The version envelope

Every response carries `meta`: the branch, the commit and build time, the latest release, and — when
the caller passed one — its `installedVersion` and a `warning` naming everything that version does not
have. Every class, token and example also carries `since`, which is `build/class-history.sh` data and
never a hand-kept list. `since` is the literal `"unreleased"` rather than a missing key, so
"in no release yet" cannot be read as "not reported".

An example's `since` is the newest release among everything its content needs: the CSS classes it is
about, and the public C# members it writes. A live example applies every class it is about; a code-only
snippet also names classes in prose, so those count too when the stylesheet declares them — a JS snippet
whose API is what puts the class on the page would otherwise be dated by the buttons in its markup.

An example with neither — a configuration snippet, a JavaScript one touching no class — falls back to
the release it has looked *exactly* like since, which `class-history.sh` derives by comparing each tag's
copy of the file byte for byte. It is deliberately not "when the file first appeared": an example
rewritten to demonstrate a new API keeps the path it has always had.

That fallback used to be `latestRelease`, and it was wrong in the one direction that matters. Every
JavaScript and C# snippet in the catalogue reported itself as first shipping in whatever the newest
release happened to be, so an agent on the previous version — following the documented rule, do not copy
anything whose `since` is newer than your pinned version — concluded the command palette was unavailable
while it was fully available from C#.

The C# history has two implementations on purpose. `build/api-inventory.sh` parses source, because
reflecting over an old tag's surface would mean building that tag once per CI run for ever; the
catalogue reflects over the assembly it actually references, which is exact but only for HEAD. Neither
alone is trustworthy, so `McpApiHistoryTests` holds the two against each other — the same reason the
landing page's figures are measured twice. There is still no version history for the *script's* own
surface; a JavaScript snippet is dated by its classes and its file, and that is the gap.

`meta.commit` is baked at image build time from `-p:SourceRevisionId`, and falls back to `SOURCE_COMMIT`
or `RAILWAY_GIT_COMMIT_SHA` in the environment. Never a runtime `git` call — `.git` is excluded from the
Docker context. Both build arguments are declared in the Dockerfile, because a builder only passes a
variable it already holds to an `ARG` the Dockerfile names. An empty value counts as absent at every
step: a Railway variable set to `${{RAILWAY_GIT_COMMIT_SHA}}` renders empty where the platform has no
git variable to resolve, and an empty commit reads as a field nobody populated rather than one that
could not be. `"unknown"` is what it says when there is genuinely nothing to report.

### Errors name the limit or the values

**Every rejection is an `McpException`**, whose message the SDK propagates to the caller. Any other
exception type arrives as the bare `An error occurred invoking 'x'.`, which tells a model nothing and
leaves it retrying the same call. So a rejection states the limit it broke or lists the values the
argument accepts: an unknown `kind`, an unknown guide section, an unknown doc name, too many ids, and an
`installedVersion` that is not a version are all errors rather than empty results.

An `installedVersion` this cannot parse is rejected rather than ignored, because it would otherwise
compare as `0.0.0` and produce a confident warning built from nonsense.

### Rate limiting

On `/mcp` alone, never globally: a global limiter would also count Blazor's SignalR upgrades and every
static asset, so one person browsing the site would trip a limit sized for MCP calls.

A **concurrency limiter** is the actual control — not partitioned, so there is nothing to spoof your way
around. The per-caller **token bucket** is fairness only: behind a proxy whose address range we do not
control, per-IP limiting is not a security measure. A bucket rather than a fixed window because agent
traffic is bursty, and a fixed window punishes exactly that. Kestrel's request-body limit closes the
"POST a gigabyte at it" hole, and the stateless transport means a flood of `initialize` calls cannot
grow the heap.

## Decisions with a measurement behind them

Recorded so they are not re-opened from intuition.

**No minification.** Measured on the shipped assets: minifying the CSS and JS saves **4,360 brotli
bytes**, about **2% of first load**. The .NET SDK already serves the stylesheet gzipped at 11,289
bytes. The cost would be a build step, a second artefact to keep in step with the parts, and a
stylesheet nobody can read in DevTools or in the restored package. Not worth 2%.

**No pixel baselines for visual regression.** Screenshot comparison is the obvious tool and the wrong
one here: baselines rendered on Windows do not match the Linux CI runner (font rasterisation and
scrollbar metrics differ), so the suite either fails constantly or gets a tolerance wide enough to
miss real changes. The regressions this library actually suffers are **cascade** regressions — a class
that loses a property to a more specific rule and silently does nothing. Those are found by reading
`getComputedStyle`, never by looking, which is why the browser tests assert computed values.

**CSS anchor positioning, deliberately.** The floor is Chromium — current Chrome and Edge — so
`anchor-name`, `anchor-scope`, `position-area` and `align-self: anchor-center` are all available and
three things depend on them: the collapsed rail's hover flyout, `.popover` and `.menu`.

The rail is the one that could not be done any other way. It scrolls, and **a scroll container clips
both axes** — there is no combination of `overflow` values that scrolls vertically and lets a child out
sideways, so a flyout inside `.nav-scroll` is either clipped or the rail cannot scroll. `position: fixed`
takes the viewport as its containing block and escapes the clip; anchor positioning is then what tells
it where to go without measuring anything in JavaScript.

`.menu` came to need it for the same reason. An absolutely positioned panel is laid out inside the
nearest scroll container and counts towards its scrollable overflow, so a menu opened in a toolbar or a
scrolling table both grew that container a scrollbar and got clipped at its edge. `.menu-anchor` still
carries `position: relative`, which is what the panel's own `--start` variant and the fallback rung
resolve against. Use anchor positioning where the alternative is a measurement, not as a default.

**It does not survive a scroll.** Chromium computes an anchored `position: fixed` panel's offset when
the panel becomes visible and does not recompute it while the anchor scrolls: the panel stays pinned to
the viewport, the trigger travels out from under it, and the gap grows by exactly the scroll distance.
Measured on `/menu` — 4px under the trigger at rest, 154px after a 150px scroll, and −146px after
scrolling back, with the panel's own viewport `top` never changing. Hiding and re-showing it puts it
right, which is what identifies the cause as a snapshot at reveal rather than a positioning error.

So `22-anchored.js` closes an open `.menu` or `.popover` when a scroll moves its trigger. A dropdown
whose trigger has scrolled away is stale rather than misplaced, and closing it is what a platform menu
does — which is why this is the fix rather than repositioning in JavaScript. The handler measures
nothing and writes no coordinate; it reads `[data-menu-toggle][aria-expanded="true"]` and
`.popover:popover-open`, ignores a scroll that came from inside the panel, and ignores one that did not
move the trigger at all. It listens in the **capture** phase, because `scroll` does not bubble and the
frame's own `.page` is a scroll container.

The rail's flyout has the same fault and no fix: it is a CSS hover state with no open/closed to toggle.
It is also the mildest case — the pointer has to stay on the rail item for the flyout to exist, and
leaving closes it. `AnchoredPanelTests` pins the menu and popover behaviour, including the two cases
that must NOT close.

**Scroll-driven animations are avoided** for a sharper reason: they fail *incorrectly*. A browser that
drops `animation-timeline` leaves the rest of the `animation` shorthand running, so the animation plays
on a timer instead of not at all.

## Out of scope

- App-specific business UI — approval panels, SLA badges, tour overlays, page-specific grids.
- MudBlazor, Syncfusion, Radzen, Tailwind.
- Wrapping tables, forms or page content in components.
- Loading anything from a remote URL at runtime. Everything the package needs, including the icon font,
  ships inside it.

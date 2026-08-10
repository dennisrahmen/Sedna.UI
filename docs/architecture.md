# Architecture

## Two tiers

**Tier 1 — the frame.** Shell, sidebar and nav, header, user widget. Layout chrome that is
pixel-identical in every app and is not restyled per project. Shipped as CSS classes.

**Tier 2 — the paint.** Tables, forms, cards, badges, buttons, alerts. Shipped as semantic CSS classes.
Pages write plain HTML and apply the classes.

Content UI is always a class, never a component. There is no `<DataTable>` and there will not be one.

Both tiers are CSS classes. The package ships no components at all.

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

**A token you set at bare `:root` beats the library's `[data-theme="light"]` value for it.** Set both
blocks, as the rebrand recipe shows.

**An unconditional rule of yours beats a library rule at any specificity.** The case to check is
compact density: an app carrying its own `.table th, .table td { padding: 8px 12px }` wins over
`:root[data-density="compact"] .table td`, so compact density stops tightening its tables. Delete the
copied rule.

**`!important` is actively harmful here, not merely unnecessary.** Layer order *inverts* for important
declarations, so an `!important` inside `sedna.paint` is harder for you to override than an ordinary
declaration. The library uses none, and a test enforces it.

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

`data-theme="light"`, `data-cvd="1"`, `data-density="compact"` and `dir` are set on `<html>`.

- `Sedna.UI.boot.js` applies them from `localStorage` before first paint.
- `sednaUi.settings.save('theme', 'light')` updates them at runtime.

`dir` and `lang` are the two that are written **only from a stored choice**. Both are attributes the host
page declares about itself, so with nothing stored they are left exactly as the document wrote them — the
library never infers a document's direction or language from the browser's. Never derive `lang` from
`navigator.language`: it relabels an English page as German for anybody visiting with a German browser.

`data-theme` is **always** present, set to `light` or `dark`, never absent. Consuming apps select on
`:root[data-theme="light"]` to brand the light palette, so that selector has to match whenever the light
palette is in use. Any future support for the operating system's preference must therefore resolve
`prefers-color-scheme` into this attribute in `boot.js` — expressing it as a `@media` block instead would
make the light palette reachable without the attribute, and every consuming app's light-theme branding
would silently stop applying.

The colour-blind palette (`data-cvd="1"`) remaps only the `go` family to blue, so go and danger read as
blue against red. Amber and the brand colour are unchanged.

`data-density="compact"` tightens `.table` padding. Apps tighten their own page-specific components.

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
| Local stacking inside a component | 0, 1 | **not the overlay scale** — a sticky table header above its own rows |
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

## JavaScript

`Sedna.UI.js` exposes the global `sednaUi`:

| Member | Purpose |
|---|---|
| `configure(options)` | Storage prefix, notification icon, language cookie |
| `settings` | `load()`, `save(key, value)`, `apply()`. Keys: `theme`, `cvd`, `density`, `dir`, `lang` |
| `tips` | Hover-hint engine. Set `tips.gate = el => bool` to suppress hints conditionally |
| `toast(message, options)` | Creates and reuses its own `.toast-stack[data-sedna-toasts]`, and leaves any stack the app wrote alone. Returns its own remover; `timeout: 0` stays until dismissed |
| `confirm(options)` | A `<dialog>.showModal()` confirmation. Returns a promise; `danger: true` reddens confirm and focuses cancel |
| `menu` | Delegated dropdowns. `closeAll()`, for after a navigation |
| `tabs` | Delegated tabs with the arrow/Home/End keyboard contract. `select(tabOrPanelId)` |
| `palette` | Command palette, opened by Ctrl/⌘-K once commands exist: `register(list)`, `open()`, `close()`, `rank(query)` |
| `search` | Header search behind a `data-search` input: `register(items)`, `rank(query)`, `close()` |
| `dropzone` | Delegated drag-and-drop for a `data-dropzone` zone: maintains `.dropzone--over`, hands a dropped file to the zone's own `input[type=file]` as a `change` event. `reset()` clears the highlight |
| `output` | Follow-tail for a `data-follow` output pane: sticks to the newest line, releases when the reader scrolls up, re-attaches when they scroll back down. `follow(pane)`, `isFollowing(pane)` |
| `codeBlock` | `toggle(block, expanded?)` — expands or collapses a `.code-block--clamped`. Delegated from `[data-code-expand]` |
| `spotlight` | `at(hole, target, { pad })` positions `.spotlight-hole` over an element and returns the rectangle; `tipAt(tip, rect, gap)` places the bubble, flipping above when there is no room below. The steps stay the app's |
| `md` | Markdown editor: `init(root?)` wires every `.md-editor` in `root` (the document by default) and is idempotent per editor; `apply(textarea, cmd)`, `render(src)` |
| `copyText`, `openTab`, `viewportWidth`, `scrollPageTop` | Interop helpers. `scrollPageTop` resets `.page`, which is the only scroll container the frame has and therefore the one navigation leaves where it was |
| `getItem`, `setItem` | `localStorage` access |
| `requestNotify`, `notify`, `ping` | Desktop notifications and an audio ping |

`ui._` also exists and is **private** — shared closure state the parts need. It may change in a patch.

Five behaviours are delegated from `document`, so content rendered after load is covered without
re-wiring: hover hints, `data-menu-toggle`, `data-tabs`, `data-search`, and `data-copy` /
`data-copy-target`. The last has no member on the global — the attribute is the whole API.
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
two things depend on them: the collapsed rail's hover flyout and `.popover`.

The rail is the one that could not be done any other way. It scrolls, and **a scroll container clips
both axes** — there is no combination of `overflow` values that scrolls vertically and lets a child out
sideways, so a flyout inside `.nav-scroll` is either clipped or the rail cannot scroll. `position: fixed`
takes the viewport as its containing block and escapes the clip; anchor positioning is then what tells
it where to go without measuring anything in JavaScript.

`.menu` is **not** anchored this way and should stay that way: `.menu-anchor` uses `position: relative`,
needs no measurement, and works in any engine. Use anchor positioning where the alternative is a
measurement, not as a default.

**Scroll-driven animations are avoided** for a sharper reason: they fail *incorrectly*. A browser that
drops `animation-timeline` leaves the rest of the `animation` shorthand running, so the animation plays
on a timer instead of not at all.

## Out of scope

- App-specific business UI — approval panels, SLA badges, tour overlays, page-specific grids.
- MudBlazor, Syncfusion, Radzen, Tailwind.
- Wrapping tables, forms or page content in components.
- Loading anything from a remote URL at runtime. Everything the package needs, including the icon font,
  ships inside it.

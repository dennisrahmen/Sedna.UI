# Getting started

## Install

```bash
dotnet add package Sedna.UI
```

Pin the version. Do not use a floating version range.

Upgrading from `DR.Simple_UI`? See [Migrating from DR.Simple_UI](migrating-to-sedna-ui.md) instead
of this page.

## Host page

Copy this block into `App.razor` (or `_Host.cshtml`). Your override file must come **after** the
library stylesheet, and the `<base>` must come before all of them.

```html
<head>
    <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />

    <base href="/" />

    <script src="_content/Sedna.UI/js/Sedna.UI.boot.js"></script>

    <link rel="stylesheet" href="_content/Sedna.UI/lib/remixicon/remixicon.css" />
    <link rel="stylesheet" href="_content/Sedna.UI/css/Sedna.UI.css" />
    <link rel="stylesheet" href="css/brand.css" />
</head>
<body>
    <!-- … -->
    <script src="_content/Sedna.UI/js/Sedna.UI.js"></script>
</body>
```

`Sedna.UI.boot.js` applies the stored theme name and variant before first paint. Load it in
`<head>`.

### `<base href="/">`

Required, and it must come before the asset lines — a `<base>` applies only to URLs after it.

The five asset paths are relative. Without a `<base>`, a browser resolves them against the current
path instead of the root, so a **direct hit on a sub-route** — a pasted link, a bookmark, a refresh —
asks for them under that route and gets a 404 for each. The page renders unstyled and never becomes
interactive, no error is logged, and it only happens on the direct hit: the same page reached by
clicking a link inside the app looks correct, because the router never re-resolved the assets.

An app served from a sub-path uses that path instead, with both slashes — `<base href="/admin/" />`.

A bare fragment resolves against the base too: `href="#main"` on `/orders` means `/#main`, the start
page, and Blazor's router goes there. `Sedna.UI.js` handles a click on a link written as a bare
fragment whose target is on the page: it scrolls to the target and focuses it, without navigating.
Write a same-page jump — the skip link, a validation summary's links — as a bare fragment and keep
the script loaded. The address does not change, so a link meant to be copied carries the page's path
(`orders#totals`) and is the router's to handle; that jump scrolls but does not move focus.

### `viewport-fit=cover`

Required. Without it a browser lays the page out inside the display's safe area and
`env(safe-area-inset-*)` reports `0px` on every device, so the library's safe-area handling never
runs and a phone's home indicator crosses the bottom of a sheet, a toast and the reconnect bar.

With it, the viewport reaches the physical edges of the display and the library holds the content
clear: the shell pads the top and the two sides, and each element pinned to the bottom edge pads
itself so its own surface fills the strip.

**Migrating an app that already has the old meta tag**, in order:

1. Add `viewport-fit=cover` to the viewport meta. Nothing changes on a desktop, a tablet without a
   notch, or any Android device without a gesture bar — all four insets stay `0px` there.
2. Find every element of your own that is `position: fixed` or `sticky` against a viewport edge — a
   bottom action bar, a footer, a cookie banner, a bottom nav — and add `.sedna-safe-bottom`,
   `.sedna-safe-top` or `.sedna-safe-inline`. Put the class on the element that carries the
   background, not on a wrapper: the classes add **padding**, so the element's own colour fills the
   inset instead of leaving a transparent band.
3. Anything of your own that uses `100vh` against the bottom edge wants `100dvh`.

The four tokens are readable directly where a utility does not fit — `--safe-block-start`,
`--safe-block-end`, `--safe-inline-start` and `--safe-inline-end`. Use these rather than `env()`:
the inline pair is mirrored for `dir="rtl"`, which `env()` cannot do on its own.

### The status bar

Blazor Server injects its own reconnect UI — unstyled, with inline styles — unless the host page
supplies one. Add both blocks from the catalogue's
[Status bar](https://www.sedna-ui.com/status-bar) page inside `<body>`, before the component that
carries the render mode:

- **`#components-reconnect-modal`**, holding one `.status-bar` row per state. Supply
  `.status-bar--reconnecting`, `.status-bar--failed` and `.status-bar--expired`; `.status-bar--paused`
  is optional and falls back to the reconnecting row. Blazor puts its state classes on the id and the
  stylesheet shows one row at a time — omit a required row and that state renders as an empty strip.
- **`#blazor-error-ui`**, the unhandled-error bar: `.status-bar--error` on a strip *inside* the id.
  Blazor reveals the id with an inline `display: block`, which beats every rule, so a bar on the id
  itself lays out as inline text.

In both, the bar is a child of the id, never the id itself.

No configuration is required. `sednaUi.configure()` is only needed for the options below.

### `configure()` options

| Option | Default | Purpose |
|---|---|---|
| `notifyIcon` | `null` | Icon for desktop notifications. |
| `langCookie` | `false` | Mirror the language into a cookie for server-side prerendering. |
| `storagePrefix` | `sedna.` | See below. Rarely needed. |
| `themeDefault` | `sedna` | Which theme name applies with nothing stored. See [The default theme](#the-default-theme). |

`ISednaUi.ConfigureAsync()` pushes these from `SednaUiOptions`, so an app registering the services
does not call `configure()` by hand.

### Storage keys

Settings are stored under `sedna.theme`, `sedna.variant`, `sedna.cvd`, `sedna.density`, `sedna.dir`
and `sedna.lang`. `localStorage` is scoped per origin, so apps on different domains never share state
and the default prefix is fine.

`sedna.theme` and `sedna.variant` are two orthogonal choices, both always applied:

- **`sedna.theme`** — *which* theme, by name; the default theme with nothing stored.
- **`sedna.variant`** — `dark`, `light`, or `system` to follow `prefers-color-scheme` live. `<html>`
  always carries the *resolved* `data-variant="dark"` or `data-variant="light"`; `system` only ever
  appears in the stored preference, for a settings UI that wants to show it as selected.

`data-variant-default` on the boot script tag controls what a visitor who has never chosen sees —
`dark` (the default), `light`, or `system`:

```html
<script src="_content/Sedna.UI/js/Sedna.UI.boot.js" data-variant-default="system"></script>
```

A stored choice always wins over this default; it only ever governs a first-time visitor.

### The default theme

An app that registers a theme of its own as `Default` sets the same name on the boot script, and
renders it from the options rather than retyping it:

```razor
@inject SednaUiOptions SednaOptions

<script src="_content/Sedna.UI/js/Sedna.UI.boot.js"
        data-theme-default="@SednaOptions.Default"></script>
```

`SednaUiBrand.ToCss` emits `Default`'s palette at bare `:root` and every other registered theme at
`[data-theme="<name>"]`, so the name the browser stamps decides which palette a first visit gets.
Left at `sedna` while the app's default is its own theme, a visitor with nothing stored is stamped
`data-theme="sedna"` — which selects the built-in palette if Sedna is also registered, and otherwise
names a theme no block answers to.

`ISednaUi.ConfigureAsync()` pushes the same value to `Sedna.UI.js`, so the two agree once the circuit
is up. An app calling neither keeps today's behaviour: `sedna` in both scripts.

### The browser's time zone

`data-tz-cookie` names a cookie, and the boot script writes the browser's IANA zone to it before first
paint — `path=/`, `SameSite=Lax`, a year's `max-age`, and only when the value differs from the cookie
already there:

```html
<script src="_content/Sedna.UI/js/Sedna.UI.boot.js" data-tz-cookie="tz"></script>
```

For an app that stores instants as UTC and renders them on the reader's clock. Only the browser knows
the zone, and in Blazor Server with prerendering off `App.razor` is the last component with an
`HttpContext` to read a cookie from — so without this an app ships an inline script of its own.

The library does nothing else with it: no reload and no event. **The first request of a session
carries no cookie yet**, so configure a fallback zone for that one render and let it stand. Do not
reload the page to get the cookie; the next navigation already carries it.

From a circuit, ask for the zone directly:

```csharp
var zone = await Ui.GetTimeZoneAsync();          // "Europe/Lisbon", or null
if (zone is not null && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out var tz)) _zone = tz;
```

Call it from `OnAfterRenderAsync(firstRender: true)` — it is interop, so it cannot run while
prerendering. The cookie is what the first server render reads; this is what the first session reads,
and it is an IANA id rather than an offset, which is only true until that zone's next transition.

`sedna.dir` and `sedna.lang` are the two that are only applied **once stored**. Both are attributes the
host page declares about itself, so with nothing stored `<html dir>` and `<html lang>` are left exactly
as written — the library never infers a document's direction or language from the browser's.

Override it only when **two apps share one origin** — for example `example.com/app-a` and
`example.com/app-b` behind one reverse proxy, which is a single origin and therefore one `localStorage`.
It also matters for `langCookie`, since cookies are not origin-scoped the way `localStorage` is.

If you override it, set the same value in both places or the theme is not found on reload:

```html
<script src="_content/Sedna.UI/js/Sedna.UI.boot.js" data-prefix="app-a."></script>
<script>sednaUi.configure({ storagePrefix: 'app-a.' });</script>
```

## Branding

Create `wwwroot/css/brand.css` and redefine the brand tokens:

```css
:root {
    --brand:          #d62828;
    --brand-hover:    #b81f1f;
    --brand-active:   #8f1818;
    --brand-soft:     #ff7a70;
    --brand-text:     #ff9b93;
    --accent:         #ff7a70;
    --sidebar-active: #d62828;
}

:root[data-variant="light"] {
    --brand-soft: #d62828;
    --brand-text: #b81f1f;
    --accent:     #b81f1f;
}
```

`--brand-tint`, `--brand-ring`, `--brand-ring-soft`, `--brand-ring-check` and `--brand-glow` are mixed
from `--brand` and follow it automatically, in both themes — set them only to override the alpha the
library chose. The five values above are hues, not opacities, which is why they are still stated.

Redefine only tokens the library already declares. **Never declare a new `--` name the library does not
define** — a future version may introduce that name with a different meaning and your app breaks on
upgrade.

If a value is missing, [request it](../CONTRIBUTING.md). Until it ships, use an app-prefixed variable
(`--myapp-…`) in your own stylesheet rather than a bare name in the shared namespace. Do not override
library classes to work around it. See [architecture](architecture.md#the-token-contract).

The full token list is on the [Tokens](https://www.sedna-ui.com/tokens) catalogue
page.

### The branding service

The recipe above overrides a handful of semantic tokens by hand. For a rebrand that also wants
its own palette — or an app that registers more than one selectable theme — register themes
instead:

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

`SednaBrandStyle` reads the registered themes from DI and writes the palette CSS into `<head>` —
`:root { … }` for `Default`, `[data-theme="<name>"] { … }` for every other registered theme.
`SednaTheme.Sedna` is the built-in; a new theme supplies a `SednaPalette` for each of `Dark` and
`Light` — both are required, so a theme cannot omit one. A ramp is either supplied whole, step by
step, or generated from a single anchor colour:

```csharp
var ramp = SednaRamp.FromAnchor("#2f6fed", anchorStep: 500);
```

The anchor gives the ramp its hue and chroma; every step's lightness, including the anchor's own,
comes from the shared curve — so `ramp[500]` is **not** `#2f6fed`. When a brand colour is mandated
exactly, keep it and let the rest generate around it:

```csharp
var ramp = SednaRamp.FromAnchor("#D62828", anchorStep: 600, exactAnchor: true);
```

See [architecture](architecture.md#branding) for what `FromAnchor` follows and what
`SednaUiBrand.ToCss` emits.

The catalogue's own `App.razor` registers `SednaTheme.Sedna`, `.Forest`, `.Cobalt` and `.Graphite`
exactly this way, so the [Branding](https://www.sedna-ui.com/branding) page's live switch has
several palettes to move between.

To change the **background** rather than the brand, generate the base ramp from the colour the
canvas should be:

```csharp
var palette = new SednaPalette(slate: SednaRamp.Surface("#18181b"), coral: sedna.Coral, …);
```

That moves the canvas, the chrome, the cards and all three elevation levels together — see
[branding §4.5](BRANDING.md#45-changing-the-base). `SednaRamp.FromAnchor` is for brand hues and is
the wrong tool here: its curve puts step 900 at more than half lightness.

## Icons

[Remix Icon](https://remixicon.com) 4.9.1 is bundled in the package — no CDN and nothing extra to
install. The landing page counts what the stylesheet actually declares; a figure typed here would go
stale in silence. Add the stylesheet:

```html
<link rel="stylesheet" href="_content/Sedna.UI/lib/remixicon/remixicon.css" />
```

Then use the classes on an `<i>`:

```html
<button class="btn btn-go"><i class="ri-check-line"></i> Approve</button>
<button class="btn" aria-label="Refresh"><i class="ri-refresh-line"></i></button>
```

Browse the full set at <https://remixicon.com>. Every catalogue example uses these class names.

The icons are licensed under the **Remix Icon License v1.0**, not this project's Apache-2.0. Displaying
them in an application requires nothing of you; see
[THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md) for the restrictions that do carry through — chiefly
that you may not redistribute them as a standalone icon pack, or use one as a logo.

## The frame

The shell, sidebar, header and user widget are CSS classes, like everything else. There is no
`<AppShell>` and there will not be one — copy the markup from the catalogue's
[Shell and nav](https://www.sedna-ui.com/frame) page.

One thing markup cannot express is which navigation link is the current page. The package supplies it:

```razor
@using Sedna.UI

<a class="@Nav.CssClass("queue")" aria-current="@Nav.AriaCurrent("queue")" href="queue">
    <i class="ri-inbox-line"></i><span>Queue</span>
</a>
```

`CssClass` appends `active`; `AriaCurrent` returns `"page"` or null, which Blazor omits — the class
colours the item, `aria-current` is what is announced. Matching drops the query string and the fragment,
ignores a trailing slash, and requires a prefix match to end on a path segment, so `/queue` does not
light up on `/queue-archive`. **The link to the app root needs `NavLinkMatch.All`**, or it is active
everywhere.

These are pure functions and do not subscribe to `LocationChanged`. A page re-rendered by navigation
picks up the new state for free. A sidebar that survives navigation has to subscribe and call
`StateHasChanged`.

**Subscribe in the component that renders the links, not in the layout around it.** When a parent
re-renders, Blazor only hands new parameters to a child component whose parameters actually differ — so
a sidebar whose parameters are unchanged is skipped and goes on rendering the previous address. A
subscription one level too high looks right and does nothing: the active link then updates on the next
unrelated click rather than on navigation.

```csharp
// CatalogueSidebar.razor.cs — the component that reads the address subscribes to it.
protected override void OnInitialized() => Nav.LocationChanged += OnLocationChanged;
public void Dispose() => Nav.LocationChanged -= OnLocationChanged;
private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => StateHasChanged();
```

## The C# surface

```csharp
// Program.cs
builder.Services.AddSednaUi();
```

That registers `ISednaUi`, a typed wrapper over the browser API — `ToastAsync`, `ConfirmAsync`,
`CopyTextAsync`, `SaveSettingAsync`, the command palette, and the rest of `sednaUi`.

Every member is a JavaScript call, so **none of them can run during prerendering**. Call them from an
event handler, or from `OnAfterRenderAsync(firstRender: true)`. They deliberately do not swallow the
exception prerendering raises: a call that silently did nothing would be far harder to find.

Two parts of the JavaScript surface have no C# equivalent, because neither can cross the boundary.
`toast()` returns a function that removes that toast early, and `tips.gate` is a predicate you assign to
suppress hover hints. Both stay JavaScript.

## Writing pages

Copy markup from the catalogue at <https://www.sedna-ui.com/> rather than writing it from
scratch. Every page carries copy-pasteable HTML for its class family.

The site is built from `main` and can be ahead of the version you have installed. Each class, token and
example says which release first shipped it, so check that before copying something new.

## AI agents

The catalogue has an MCP server. Add one URL:

```json
{ "type": "http", "url": "https://www.sedna-ui.com/mcp" }
```

| Tool | What it answers |
|---|---|
| `search` | "What is there for a sortable table with status badges?" Returns references, never markup. |
| `get_example` | The exact markup for an example, byte-for-byte what the site renders. |
| `describe_class` | What a class does: its rules from the shipped stylesheet, its layer, its modifiers. |
| `get_page` | Everything on one page, or the list of pages. |
| `get_tokens` | The design tokens, for writing `brand.css`. |
| `get_integration_guide` | This document, the branding recipe, the JavaScript surface, or the rules. |

Pass `installedVersion` and the response names anything your version does not have.

Then copy the block in [`CLAUDE.consuming-app.md`](CLAUDE.consuming-app.md) into your app's `CLAUDE.md`.

# Getting started

## Install

```bash
dotnet add package Sedna.UI
```

Pin the version. Do not use a floating version range.

Upgrading from `DR.Simple_UI`? See [Migrating from DR.Simple_UI](migrating-to-sedna-ui.md) instead
of this page.

## Host page

Add three stylesheets and two scripts to `App.razor` (or `_Host.cshtml`). Your override file must come
**after** the library stylesheet.

```html
<head>
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />

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

`Sedna.UI.boot.js` applies the stored theme before first paint. Load it in `<head>`.

### The reconnect banner

Blazor Server injects its own reconnect UI — unstyled, with inline styles — unless the host page
supplies one. Add the block from the catalogue's
[Shell and nav](https://www.sedna-ui.com/frame) page inside `<body>`, before the component that
carries the render mode.

Supply `.reconnect-attempting`, `.reconnect-failed` and `.reconnect-rejected`; `.reconnect-paused` is
optional and falls back to the attempting row. Blazor puts the state classes on
`#components-reconnect-modal` itself and the stylesheet shows one row at a time — omit a required row
and that state renders as an empty bar.

No configuration is required. `sednaUi.configure()` is only needed for the options below.

### `configure()` options

| Option | Default | Purpose |
|---|---|---|
| `notifyIcon` | `null` | Icon for desktop notifications. |
| `langCookie` | `false` | Mirror the language into a cookie for server-side prerendering. |
| `storagePrefix` | `sedna.` | See below. Rarely needed. |

### Storage keys

Settings are stored under `sedna.theme`, `sedna.cvd`, `sedna.density`, `sedna.dir` and `sedna.lang`.
`localStorage` is scoped per origin, so apps on different domains never share state and the default
prefix is fine.

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
    --brand:          #e41f16;
    --brand-hover:    #c8170f;
    --brand-active:   #a81209;
    --brand-soft:     #ff6f66;
    --brand-text:     #ff8f88;
    --accent:         #ff6f66;
    --sidebar-active: #e41f16;
}

:root[data-theme="light"] {
    --brand-soft: #e41f16;
    --brand-text: #c8170f;
    --accent:     #c8170f;
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

## Icons

[Remix Icon](https://remixicon.com) 4.9.1 is bundled in the package — 3,245 icons, no CDN and nothing
extra to install. Add the stylesheet:

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

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

Search it, then fetch the exact markup — `search` → `get_example`. Use `describe_class` to choose
between variants. **Do not write shared-UI markup from memory**, and do not invent class names: a class
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

The layout frame — shell, sidebar, header, user widget — is CSS classes too. There is no `<AppShell>`:
copy the markup from the catalogue's Shell and nav page. `ActiveLink` from the package supplies the one
thing that markup cannot, which is knowing which nav link is the current page. Do not restyle the frame
here and do not fork it: a copied frame stops receiving library fixes, and it will silently miss later
additions such as the responsive drawer and the skip link.

### The header search

`.search` in the topbar is a library class. An app that already ships its own search chrome under its
own prefix should move onto these classes rather than keep a parallel copy.

There are two ways to use it, and the choice is about where the results come from:

- **A fixed, known set** — the app's pages, reports, settings screens. Register it once with
  `ISednaUi.RegisterSearchAsync`, add `data-search` to the input, and the panel, the ranking and the
  keyboard come from the library.
- **A database.** Leave `data-search` off and render `.search-panel` yourself. The debounce length,
  cancelling a superseded keystroke and the busy state are decisions about your backend, and the
  library will not guess them.

### Do not style a class name the library owns

Every class in the catalogue belongs to the library. If this app defines a rule for one of those names,
the two sets of declarations merge on upgrade and the appearance changes with no error and no diff in
this repo.

Before bumping the pinned version, read the class names the release adds and grep this app's stylesheets
for each one. Where a name collides, delete the local rule and use the library's, or rename the local one
into this app's own prefix.

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

Do not restyle a library class to work around a missing value. Overriding `.btn` or `.card` in this app
means the next library upgrade fights you, which is the drift the shared library exists to prevent.

### Overriding is now trivially easy, which is the reason not to

The library's stylesheet is entirely inside cascade layers. **This app's CSS is unlayered, so any rule
here beats any library rule, whatever the specificity.** A longer selector is never needed to win.

That makes discipline the only thing left protecting the shared design:

- **Redefine tokens, not rules.** A token override travels with the theme, the colour-blind palette and
  every future version. A rule override is a private fork of the design that nothing will tell you has
  gone stale.
- **Two things the layering means for this app's own CSS:** a token set at bare `:root` here also beats
  the library's `[data-theme="light"]` value for it, so set both blocks; and any unconditional rule here
  beats a library rule at any specificity. The case to check is a copied
  `.table th, .table td { padding: … }`, which stops compact density from tightening this app's tables.
  **Delete copied library rules.**
- **Never use `!important` against the library.** Layer order inverts for important declarations, so it
  is not needed and makes the result harder to reason about, not easier.

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

### Upgrading

The version is pinned and does not float. Before bumping it:

1. Read the release notes at <https://github.com/dennisrahmen/Sedna.UI/releases>.
2. Grep this app's stylesheets for every class name the release adds — see above.
3. Check the pages this app renders, in each theme (`data-theme`, `data-cvd`, `data-density`).

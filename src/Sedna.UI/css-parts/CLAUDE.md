# `css-parts/` — how the stylesheet is authored

This directory **is** the stylesheet. `wwwroot/css/Sedna.UI.css` is generated from it and must
never be edited by hand.

```bash
build/bundle-css.sh            # regenerate the shipped stylesheet
build/bundle-css.sh --check    # fail if it is out of date (CI-friendly)
```

`The_shipped_stylesheet_matches_its_parts` fails the build if the two disagree, so a forgotten
regeneration cannot ship.

## Adding a part

1. Create `NN-name.css` here. **The `NN-` prefix is the cascade order** — the generator discovers
   every `*.css` in this directory and concatenates them in byte-ordinal filename order. There is no
   manifest to update, and the build fails on a file without a prefix.
2. Run `build/bundle-css.sh`.
3. Add or extend a catalogue page under `src/Sedna.UI.Catalogue/Components/Pages/`, with its
   example markup as a file under `Examples/`. A class with no catalogue page is a class nobody can
   find, and a test fails on one.
4. `dotnet test`.

### Choosing the number

The prefix decides two things at once: the source order, and the **cascade layer**. The layer is
derived from the number by the generator, so it cannot drift away from the ordering convention and no
part declares its own.

| Range | Layer | What lives there |
|---|---|---|
| `00`–`04` | `sedna.tokens` | tokens and the theme remap blocks |
| `05`–`09` | `sedna.base` | bare element styles — `html`, `body`, `a`, `code`, headings |
| `10`–`29` | `sedna.frame` | the tier-1 frame — shell, sidebar, topbar, user widget, layouts |
| `30`–`79` | `sedna.paint` | tier-2 content classes, then RTL, forced colours and print |
| `80`–`89` | `sedna.utilities` | single-purpose classes |
| `90`–`99` | `sedna.overrides` | density and reduced motion — the last word |

Pick a free number inside the right range. **Renumbering an existing part can change its layer as well
as its order**, which is a behaviour change twice over — check the affected pages.

Two consequences to keep in mind while writing a rule:

- **A later layer beats an earlier one regardless of specificity.** That is why a utility at (0,1,0)
  wins against `.table td` at (0,1,1) despite the lower specificity.
- **An app's own stylesheet is unlayered, so it beats everything here.** A library rule can no longer
  out-specify an app. Do not try: if an app has to be overridden, that is a design problem, not a
  specificity one.

## Rules that apply to every part

- **No hard-coded colours.** Every colour, tint and shadow resolves through a token declared in
  `01-tokens.css` and remapped in `02-theme-light.css` / `03-theme-colour-blind.css`. `transparent`
  and `currentColor` are the only literals allowed; system colour keywords (`Canvas`, `CanvasText`, …)
  are allowed **only** as a `:root` remap inside `@media (forced-colors: active)`.
- **No `!important`.** An app must always be able to win an override. Raise specificity instead — see
  `.nav-link .nav-link-ext` in `11-frame-sidebar.css`.
- **A token block is `:root` plus attribute filters and nothing else.** Do not put a non-custom-property
  declaration in one; `color-scheme` belongs on `html`, not `:root`, for exactly this reason. Do not
  use CSS nesting inside a `:root` block — the token parser is brace-naive and a test fails loudly if
  you do.
- **Appearance-conditional blocks only remap tokens.** A `prefers-color-scheme`, `prefers-contrast` or
  `forced-colors` block may contain `:root` token remaps and nothing else. Layout media queries
  (`min-width` / `max-width` / `print`) may move real selectors — that is what they are for.
- **`z-index` comes from the documented scale** in `docs/architecture.md`. A test fails on any other
  value.
- **Nothing is loaded or inlined.** No `url(`, no `data:` URI. Need a glyph? Use the bundled Remix Icon
  font on a pseudo-element. Need a mark? Draw it in CSS.
- **Tier 2 is classes, never components.** If you are reaching for a wrapper component, stop and read
  `CLAUDE.md` in the repo root.

## Naming

Semantic, lowercase-kebab, no app or vendor prefix. Library-owned utilities that need a namespace use
`sedna-`; everything else is plain. **A plain name is a claim on the shared namespace** — before adding a
generic one (`.list`, `.menu`, `.row`, `.tag`), check it against the apps known to consume this
library, because an app that already styles that class silently gets both rule sets merged on upgrade.

## Why one file ships

The parts are inlined rather than `@import`ed at runtime because a browser cannot discover an
`@import` until the parent sheet has parsed — that costs a round trip before any style applies, then
one render-blocking request per part. And the parts live **outside `wwwroot`** on purpose: they are not
static web assets, so a consuming app has exactly one stylesheet path to link and cannot load a part
and the bundle at once. To reuse a part on its own outside NuGet, take `01-tokens.css` plus that part
from the repo.

The .NET SDK cannot bundle this for us. Its only CSS bundling is scoped `.razor.css`, which rewrites
selectors to add a `b-{hash}` attribute — that would scope tier-2 classes to markup the library
renders, when the whole point is that the app writes the markup.

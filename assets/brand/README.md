# Sedna.UI brand assets

Master mark **"Eccentric"**. All artwork sits on a 120-unit grid; the SVGs are the source of truth.

The full brand and design manual is [`docs/BRANDING.md`](../../docs/BRANDING.md).

## Which file to reach for

| Need | File |
|---|---|
| Scalable icon | `sedna-ui-icon.svg` |
| Icon, light background | `sedna-ui-icon-<n>.png` — navy mark |
| Icon, dark background | `sedna-ui-icon-on-dark-<n>.png` — Ice White mark |
| App tile / any background | `sedna-ui-tile-<n>.png` — carries its own field |
| NuGet package icon | `sedna-ui-tile-128.png` |
| Horizontal logo | `sedna-ui-logo-light.png` / `sedna-ui-logo-dark.png` |
| Wordmark alone | `sedna-ui-wordmark.png` / `-white.png` |
| GitHub social preview | `sedna-ui-social-preview.png` — 1280×640 |
| Favicon | `favicon.ico` — 16/32/48, from the tile |

## Three detail tiers, and why

The mark is drawn three times, not scaled three ways. The orbit is a hairline that fills in and reads
as noise when it is shrunk, so the stroke thickens and detail drops as the size falls:

| Tier | Stroke | Use at | Satellite |
|---|---|---|---|
| `sedna-ui-icon.svg` | 2.5 | 96px and above | yes |
| `sedna-ui-icon-compact.svg` | 5.5 | 32–64px | yes, larger |
| `sedna-ui-icon-micro.svg` | 8 | 16px | **dropped** |

**The PNG ladders already switch tier for you** — `sedna-ui-icon-16.png` is the micro form,
`-32` to `-64` are compact, `-96` and up are full. Pick by size and the right drawing comes out.

## Two ladders, because a transparent mark is invisible on the wrong surface

`sedna-ui-icon-*` is the navy mark, for light backgrounds. `sedna-ui-icon-on-dark-*` is the Ice White
mark, for dark ones. Where the background is unknown — a NuGet listing, a browser tab, an IDE package
pane — use the **tile**, which brings its own Deep Space field and reads on anything. That is why the
package icon and the favicon are both derived from it.

## The SVG trap: live text needs the font

`sedna-ui-wordmark*.svg` and `sedna-ui-logo-*.svg` contain a `<text>` element set in **Outfit**. On a
machine without Outfit they fall back to a generic sans, and the brand rules forbid exactly that:

> Never rebuild the wordmark in another face.

Nothing errors — the wordmark is simply wrong. So:

- **Mark and tile SVGs** are pure geometry. Safe anywhere.
- **Wordmark and lockup SVGs** are design sources. For anything web-facing use the **PNG** versions,
  where the type is already rasterised.

`font/` holds the variable font so those SVGs open correctly. It is a brand asset only — the library's
UI uses the system sans and mono stacks and never loads it. See
[`THIRD-PARTY-NOTICES.md`](../../THIRD-PARTY-NOTICES.md) for the licence.

## Colour

The brand palette. These are the fixed points; the interface ramps are derived from them in
`docs/BRANDING.md` §3.

| Name | Hex | Role |
|---|---|---|
| Sedna Red | `#FF6B4A` | brand signal — the focal body |
| Orbit Blue | `#59C3FF` | accent, links, the satellite |
| Sedna Navy | `#17346E` | the orbit and the *S* form |
| Deep Space | `#0F172A` | dark canvas, the tile's field |
| Navy Slate | `#1E293B` | dark elevated surface |
| Ice White | `#F8FAFC` | light canvas, the mark on dark fields |
| Dust Gray | `#94A3B8` | muted text on dark |

The focal body is a gradient, `#FD7636` → `#F34238`; it flattens to Sedna Red.

## Rules

- Clear space on all four sides equals the radius of the focal body.
- Horizontal lockup minimum width 96px. Standalone icon minimum 20px — below that, the micro tier.
- Never rotate, recolour or add effects. Never rebuild the wordmark in another face.

## Wired into things that fail quietly

Three of these are referenced by name somewhere that breaks without an error, and a test pins each:

- `sedna-ui-tile-128.png` → packed as `icon.png`, the NuGet package icon. nuget.org requires 128×128
  or smaller.
- `sedna-ui-social-preview.png` → the README hero, which is also the nuget.org readme.
- `favicon.ico` and `sedna-ui-tile-64.png` → copied into the catalogue's own `wwwroot`. Update the
  source here and the site keeps serving the old one unless the copy moves too.

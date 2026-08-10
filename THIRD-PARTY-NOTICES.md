# Third-party notices

`Sedna.UI` is licensed under [Apache-2.0](LICENSE). It redistributes the following third-party
components, which remain under their own licences.

Only one of them ships inside the package. The distinction matters for anyone auditing what a
`dotnet add package` actually pulls in:

| Component | In the `.nupkg` | Where |
|---|---|---|
| Remix Icon | **yes** | `_content/Sedna.UI/lib/remixicon/` |
| `ModelContextProtocol.AspNetCore` | no — the catalogue application only | published in its container image |
| Outfit | no — a design asset, never loaded as a UI face | `assets/brand/font/` |

## Remix Icon

This project uses icons from Remix Icon (<https://remixicon.com>), licensed under the Remix Icon
License v1.0.

| | |
|---|---|
| Version | 4.9.1 |
| Copyright | Copyright (c) 2017–2026 Remix Design |
| Licence | Remix Icon License v1.0 |
| Licence text | [`src/Sedna.UI/wwwroot/lib/remixicon/LICENSE`](src/Sedna.UI/wwwroot/lib/remixicon/LICENSE) |
| Upstream | <https://github.com/Remix-Design/RemixIcon> |

Shipped in the package at `_content/Sedna.UI/lib/remixicon/` as `remixicon.css` and
`remixicon.woff2`. Vendored by [`build/vendor-remixicon.sh`](build/vendor-remixicon.sh), unmodified
except that the `@font-face` `src` list is reduced to `woff2`.

### What this means if you use Sedna.UI

The icons are **not** covered by this project's Apache-2.0 licence. They stay under the Remix Icon
License, and its restrictions carry through to you. In normal use — icons in the UI of an application
you build — nothing is required of you. Section 5 of that licence requires no copyright notice for
individual icon use within a compiled app or website.

The restrictions that do carry through, in short:

- Do not sell or distribute the icons as a standalone product, icon pack or icon font.
- Do not use them to build a competing icon library.
- Do not use an icon as a logo, trademark, app icon or brand identifier.
- Brand icons (GitHub, etc.) may be used only to represent or link to that brand, and grant no
  trademark rights.

Read [the licence](src/Sedna.UI/wwwroot/lib/remixicon/LICENSE) if you are doing anything beyond
displaying icons in a UI.

### Why redistribution here is permitted

Section 2.3 permits including the icons in a larger product where they are functional or decorative
components and are not its primary value. Section 3.1 gives "design systems or UI kits where Icons are
a minor component" as a permitted example. Section 9 permits integration into projects under permissive
licences including Apache-2.0, provided the icons remain governed by their own licence and the Section 3
restrictions continue to apply — which is what this notice records.

Attribution is optional under Section 2.4. It is given here anyway, in the form Remix Icon suggests.

### Note on upstream metadata

The `package.json` of `remixicon@4.9.1` on npm reports `Apache-2.0`. That is stale — the `License` file
shipped inside the same package is the Remix Icon License v1.0, dated January 2026. The licence file that
ships with the artifact governs, and it is the one vendored here.

## Catalogue application dependencies

These are **not redistributed in the NuGet package**. They are dependencies of the hosted catalogue
application (`src/Sedna.UI.Catalogue`), whose container image is published.

| Package | Licence | Source |
|---|---|---|
| `ModelContextProtocol.AspNetCore` | Apache-2.0 | <https://github.com/modelcontextprotocol/csharp-sdk> |

The library itself takes exactly one dependency, `Microsoft.AspNetCore.Components.Web`, and
`build/verify-package.sh` fails if the packed dependency list is anything else.

## Outfit

The brand's wordmark typeface (<https://github.com/Outfitio/Outfit-Fonts>), licensed under the
SIL Open Font License 1.1.

| | |
|---|---|
| Version | 1.100 |
| Copyright | Copyright 2021 The Outfit Project Authors |
| Licence | SIL Open Font License 1.1 (OFL-1.1) |
| Licence text | [`assets/brand/font/OFL.txt`](assets/brand/font/OFL.txt) |
| Upstream | <https://github.com/Outfitio/Outfit-Fonts> |

Vendored unmodified as the variable font, in both formats upstream publishes:

```
assets/brand/font/Outfit[wght].ttf     from fonts/variable/Outfit[wght].ttf
assets/brand/font/Outfit[wght].woff2   from fonts/variable/Outfit[wght].woff2
assets/brand/font/OFL.txt              from OFL.txt
```

The version above is the font's own `name` table entry, not a figure typed from the release page.

### What this is for, and what it is not for

**It is a brand asset, not a UI face.** The wordmark and lockup SVGs set live text in Outfit, so the
font has to be installed for those files to render as designed. Nothing in the library loads it: the
UI uses the system sans and mono stacks, and `BRANDING.md` §7.3 says so explicitly — *"The wordmark's
geometric sans is a brand asset for the mark only and is never loaded as a UI face."*

It is therefore **not** in `wwwroot`, **not** a `@font-face`, and **not** in the package. Adding it to
any of those would make every consuming app download a font it never renders.

### The trap this exists to defuse

`sedna-ui-wordmark*.svg` and `sedna-ui-logo-*.svg` contain a `<text>` element with
`font-family="Outfit, sans-serif"`. On a machine without Outfit they silently fall back to a generic
sans — which the brand rules forbid outright: *"Never rebuild the wordmark in another face."* Nothing
errors; the wordmark is simply wrong.

So for anything web-facing, use the **PNG** wordmarks and lockups, where the type is already
rasterised in Outfit. The SVGs are the design source, and this font is what makes them open
correctly.

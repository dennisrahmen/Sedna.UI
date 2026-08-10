# Sedna.UI brand assets

## Recommended files
- `dr-simple-ui-icon.svg` — scalable project/package icon.
- `dr-simple-ui-icon-128.png` — suitable as a NuGet package icon.
- `dr-simple-ui-logo-dark.svg` — horizontal logo on a dark background.
- `dr-simple-ui-logo-light.svg` — horizontal logo on a light background.
- `dr-simple-ui-social-preview.png` — 1280×640 GitHub social preview.
- `favicon.ico` — multi-resolution favicon.

These files keep their `dr-simple-ui-*` names for now, on purpose, pending new artwork — see
`CLAUDE.md`'s "Brand assets" section for what else has to move in the same commit once they are
renamed.

## Concept
The mark combines the library's two-tier model:
- a stable application frame (window, top bar, sidebar);
- reusable semantic content modules inside it.

## Brand palette
Sedna Red `#FF6B4A`, Orbit Blue `#59C3FF`, Deep Space `#0F172A`, Navy Slate `#1E293B`, Ice White
`#F8FAFC`, Dust Gray `#94A3B8`.

This is the brand palette, not the package's design tokens. The library's default token values
(`#111827`, `#1F2937`, `#2563EB`, `#60A5FA`, `#F3F4F6`) are deliberately unchanged, so the default
theme renders exactly as it did before the rename. Applying this palette to the default theme is a
separate, later change.

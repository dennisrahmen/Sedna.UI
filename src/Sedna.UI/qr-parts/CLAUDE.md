# `qr-parts/` — how the QR code module is authored

This directory **is** `wwwroot/js/Sedna.UI.qr.js`, the ES module the QR code's front door
(`js-parts/48-qr.js`) imports the first time a page shows a `[data-qr]`. The module is generated from
it and must never be edited by hand.

```bash
build/bundle-js.sh            # regenerates Sedna.UI.js and every surface module
build/bundle-js.sh --check    # fail if any is out of date (CI runs this)
```

`The_qr_module_matches_its_parts` fails the build if the two disagree. The reference for what the
surface does — the markup, the levels, the sizes, the colours — is `docs/qr.md`; keep it in step.

## One module, cut into files

The parts are concatenated into **one** module and share its top-level scope. The encoder is Sedna.UI's
own and imports nothing, so there is no `00-` part; **only `99-exports.js` exports** — `attach` and
`sweep`, for the front door.

A part is plain top-level declarations: no IIFE, no `window.sednaUi`.

## Choosing the number

| Range | What lives there |
|---|---|
| `1x` | the standard's tables, and what is computed from a version |
| `2x` | Reed–Solomon arithmetic |
| `3x` | text to codewords: the version, the bit stream, the blocks |
| `4x` | the module matrix: function patterns, format and version information, placement |
| `5x` | masking, the penalty rules, and `encode` |
| `60` | a code on the page: the SVG, watching its attributes, disposing |
| `99` | the exports |

## Rules

- **ISO/IEC 18004, byte mode, as written.** Text is UTF-8 in byte mode with no ECI; the level is the
  app's and never raised; the smallest version that holds the text is used; the mask is the one with
  the lowest penalty, scored with its format information drawn. `QrTests` decodes what is drawn and
  compares it with matrices from an independent encoder — a change here is checked against both.
- **Every colour through the stylesheet.** The SVG carries geometry only; `.qr` paints it with
  `--qr-ink` and `--qr-paper`. No literal colour, not even as a fallback. `SurfaceRegistryTests` reads
  every part for one.
- **The SVG is the only thing drawn**, and no word of text: the accessible name is the app's
  `aria-label`, and the human-readable text beside a code is the app's markup.

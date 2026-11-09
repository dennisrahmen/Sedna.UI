# `signature-parts/` — how the signature pad module is authored

This directory **is** `wwwroot/js/Sedna.UI.signature.js`, the ES module the signature pad's front door
(`js-parts/47-signature.js`) imports the first time a page shows a `[data-signature]`. The module is
generated from it and must never be edited by hand.

```bash
build/bundle-js.sh            # regenerates Sedna.UI.js and every surface module
build/bundle-js.sh --check    # fail if any is out of date (CI runs this)
```

`The_signature_module_matches_its_parts` fails the build if the two disagree. The reference for what
the surface does — the markup, the value, the states, the alternative for a reader without a pointer —
is `docs/signature.md`; keep it in step.

## One module, cut into files

The parts are concatenated into **one** module and share its top-level scope. The pad has no engine and
imports nothing, so there is no `00-` part; **only `99-exports.js` exports** — `attach` and `sweep`, for
the front door.

A part is plain top-level declarations: no IIFE, no `window.sednaUi`.

## Choosing the number

| Range | What lives there |
|---|---|
| `1x` | the value — SVG path data, written and read back |
| `2x` | the ink — read through a probe |
| `3x` | painting the strokes |
| `50` | a pad: the pointer, the field, the buttons, watching, disposing |
| `99` | the exports |

## Rules

- **The field is the value.** The pad loads from the app's `[data-signature-value]` and writes every
  change back with the field's own `input` and `change` events. Never keep a value the field does not
  carry, and never write pixels into it: the value is geometry, so it is the same on every screen and
  in every theme.
- **Every colour through the tokens.** `--signature-ink`, read through a probe inside the pad, so the
  pad's own tone, the variant and every setting reach the canvas. Never a literal, not even as a
  fallback. `SurfaceRegistryTests` reads every part for one.
- **The canvas is the only thing drawn**, and no word of text: the pad's name is the app's
  `aria-label`, and the hint, the buttons and the typed alternative are the app's markup.

# `chart-parts/` — how the chart module is authored

This directory **is** `wwwroot/js/Sedna.UI.chart.js`, the ES module the chart's front door
(`js-parts/43-chart.js`) hands to the shared surface loader (`js-parts/42-surfaces.js`), which imports it
the first time a page shows a `[data-chart]`. The module is generated from it and must never be edited by
hand.

```bash
build/bundle-js.sh            # regenerates Sedna.UI.js and every surface module
build/bundle-js.sh --check    # fail if any is out of date (CI runs this)
```

`The_chart_module_matches_its_parts` fails the build if the two disagree. The reference for what the
surface does — the markup, the types, the attributes, the keyboard, the event, the handle — is
`docs/charts.md`; keep it in step with any change to the surface.

## One module, cut into files

The parts are concatenated into **one** module and share its top-level scope, so `41-marks.js` calls a
helper from `30-svg.js` directly. The chart has no engine and imports nothing, so there is no `00-` part;
**only `99-exports.js` exports** — `attach` and `sweep`, for the loader.

A part is plain top-level declarations: no IIFE, no `window.sednaUi`. Everything a chart holds lives on
its own object, `c`, made in `60-chart.js`; the only shared state is the set of live charts, the one
ResizeObserver, the text measurer and the warnings already given.

## Choosing the number

| Range | What lives there |
|---|---|
| `1x` | the model — the app's table, its JSON or a call, read into one shape |
| `2x` | numbers and scales — the format, nice ticks, a linear scale |
| `3x` | drawing in SVG — elements, labels, marker shapes, measuring text |
| `4x` | the drawing — `40` the axes, `41` the marks of every type |
| `5x` | what the reader works — the active point, the tooltip, the keyboard |
| `60` | a chart: starting, watching, the handle, disposing |
| `99` | the exports |

## Rules

- **Modern JavaScript, because this is a module.** `const`, arrow functions, `?.`, `??` — the floor is
  current Chromium and current Safari on iOS.
- **Colour is a class, never a value.** A mark carries the `.series-*` class of its tone and the
  stylesheet paints it `currentColor`, so every theme, colour vision, contrast setting, forced colours and
  an app's brand reach it, and a change of any of them repaints it with no script. No literal colour, no
  style attribute with a colour in it, no colour the app hands over — a tone is a series name, and
  `toneOf` refuses anything else. `SurfaceRegistryTests` reads every part for a literal.
- **A second channel beside the colour.** Every series has its own marker shape, in `SHAPES` order, so
  two series whose colours are close are still two series — `SeriesSeparationTests` holds the order and
  the legend swatches that name it.
- **The SVG is the only thing drawn.** The tooltip, the legend, the announcements, the empty state and
  the wait are the app's elements: fill `[data-chart-field]` slots with `textContent`, clone the app's
  `<template>`, toggle `hidden`, set a transform. Never `innerHTML`, and never an element the reader
  sees outside the svg that the app did not write.
- **No word of text.** A tick is a number in the page's language, through `Intl.NumberFormat` and the
  app's `data-chart-format`; a label is the app's own. A warning in the console is the one place this
  module writes English.
- **A timer, never an animation frame, for anything that must happen.** A tab that is not painted gets
  no frames, and a redraw queued on one would never run.
- **Motion is the stylesheet's.** The first drawing fades in by CSS, which `95-reduced-motion.css`
  switches off; nothing in this module animates.
- **Name nothing real** — see the root `CLAUDE.md`. The module ships to every app that installs the
  package, comments included, and `The_scripts_name_nothing_real` scans it.

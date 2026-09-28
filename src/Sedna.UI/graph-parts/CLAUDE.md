# `graph-parts/` — how the graph module is authored

This directory **is** `wwwroot/js/Sedna.UI.graph.js`, the ES module the graph's front door
(`js-parts/43-graph.js`) imports the first time a page shows a `[data-graph]`. The module is generated
from it and must never be edited by hand.

```bash
build/bundle-js.sh            # regenerates Sedna.UI.js and Sedna.UI.graph.js
build/bundle-js.sh --check    # fail if either is out of date (CI runs this)
```

`The_graph_module_matches_its_parts` fails the build if the two disagree. The reference for what the
module does — the markup, the attributes, the controls, the events, the handle — is `docs/graph.md`;
keep it in step with any change to the surface.

## One module, cut into files

The parts are concatenated into **one** module and share its top-level scope, so a function in
`40-view.js` calls one in `22-style.js` directly. Two consequences, both checked by the build:

- **Only `00-imports.js` imports** — the engine, by relative path. A plugin is not imported at the top:
  `plugin(name)` in `30-layouts.js` imports it the first time a graph needs it.
- **Only `99-exports.js` exports** — `attach`, `sweep` and `normalise`, for the front door. A script
  reaches a graph through `sednaUi.graph.get()`, never through an import of this file.

A part is plain top-level declarations: no IIFE, no `window.sednaUi` extension, no state outside a
graph. Everything a graph holds lives on its own object, `g`, made in `70-graph.js`, so two graphs on
one page never share anything but the caches in `pluginLoads` and `warned`.

## Choosing the number

| Range | What lives there |
|---|---|
| `00` | the imports |
| `1x` | the model — reading markup, JSON and calls into one shape |
| `2x` | colour, icons and the engine's stylesheet |
| `3x` | layouts |
| `4x` | the view — fitting, zoom, names, lighting, filters |
| `5x` | what the reader works — minimap, tooltip, keyboard, menu, side panel, controls |
| `6x` | export and the plugins |
| `70` | a graph: starting, events, new data, the handle |
| `99` | the exports |

## Rules

- **Modern JavaScript, because this is a module.** `const`, arrow functions, `?.`, `async` — the floor is
  current Chromium. This is the one place that differs from `js-parts/`, which is a classic script.
- **Every colour through the tokens.** `colours.token('--x')` or `palette(colours)`, and a record's tone
  through `tokenOfTone`. Never a literal colour — except as the last-resort fallback a probe returns on a
  page with no stylesheet at all — and never a colour an app handed over: `toneOf` refuses one.
- **The canvas is the only thing drawn.** The tooltip, the menu, the side panel, the stats line and the
  announcements are the app's elements: fill `[data-graph-field]` slots with `textContent`, clone the
  app's `<template>`, toggle `hidden`, set a transform. Never `innerHTML`, never an element the reader
  sees that the app did not write. A probe the reader never sees — the colour and icon probes — is fine.
- **No word of text.** Announcements, the stats line and the empty states are templates the app wrote;
  a warning in the console is the one place this module writes English.
- **A timer, never an animation frame, for anything that must happen.** A tab that is not being painted
  gets no frames: a layout or a repaint queued on one would never run. Animation is decoration — skip it
  under `prefers-reduced-motion` and when `document.visibilityState` is not `visible`.
- **Every layout run is deterministic.** Order by degree then id; seed anything random (`seeded()`).
  The drawing a reader learned yesterday must be the one that comes back.
- **Restyle only what changed.** A class set on an element is a restyle of it, which over a large
  graph is the most expensive thing this can do. Batch (`cy.batch`) and diff (`light`, `declutter`).
- **An exception crossing into Blazor tears down the circuit.** `invoke` answers null and warns for
  anything it cannot do; a plugin that fails to load is reported and the graph keeps drawing.
- **Name nothing real** — see the root `CLAUDE.md`. The module ships to every app that installs the
  package, comments included, and `The_scripts_name_nothing_real` scans it.

## Updating the engine

`build/vendor-cytoscape.sh` owns `wwwroot/lib/cytoscape/`. Change a pin there, run it, run the graph
tests, and update `THIRD-PARTY-NOTICES.md`. The engine's API is reached through `g.cy` everywhere in
this module; a cytoscape release that renames something breaks here, and nowhere in an app.

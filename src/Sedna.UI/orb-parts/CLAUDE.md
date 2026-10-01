# `orb-parts/` — how the orb module is authored

This directory **is** `wwwroot/js/Sedna.UI.orb.js`, the ES module the orb's front door
(`js-parts/45-orb.js`) imports the first time a page shows a `[data-orb]`. The module is generated from
it and must never be edited by hand.

```bash
build/bundle-js.sh            # regenerates Sedna.UI.js and every surface module
build/bundle-js.sh --check    # fail if any is out of date (CI runs this)
```

`The_orb_module_matches_its_parts` fails the build if the two disagree. The reference for what the
surface does — the markup, the states, the sizes, the tones — is `docs/orb.md`; keep it in step.

## One module, cut into files

The parts are concatenated into **one** module and share its top-level scope, so a mode in
`31-activities.js` calls a helper from `10-geometry.js` directly. The orb has no engine and imports
nothing, so there is no `00-` part; **only `99-exports.js` exports** — `attach` and `sweep`, for the
front door.

A part is plain top-level declarations: no IIFE, no `window.sednaUi`. Everything an orb holds lives on
its own object, made in `50-orb.js`; the only shared state is the set of live orbs and the one clock that
draws them.

## Choosing the number

| Range | What lives there |
|---|---|
| `1x` | geometry — projection, orbits, outlines |
| `2x` | colour — reading the three roles through the element |
| `3x` | the drawings — `30` the states, `31` the activities |
| `4x` | the renderer — one scene, three drawings by size |
| `50` | an orb: starting, watching, the clock, disposing |
| `99` | the exports |

## Rules

- **Every colour through the tokens.** The three `--orb-*` roles, read through a probe inside the
  element so the element's own tone, the variant and every setting reach the canvas. Never a literal,
  not even as a fallback. `SurfaceRegistryTests` reads every part for one.
- **The canvas is the only thing drawn**, and no word of text: the accessible name is the app's
  `aria-label`.
- **A drawing for every state at every size.** A mode describes orbits and bodies; the renderer decides
  how to draw them — solid lines below 28px, dots above. A new mode is checked at 16px and at 96px.
- **Motion is decoration.** One still frame is drawn the moment an orb starts, so a hidden tab or a
  reader who asked for no motion still sees the state. The clock runs only while an orb is on screen,
  the tab is visible and motion is allowed.

# Orb

What an agent is doing, drawn. A tier 3 surface — see [surfaces.md](surfaces.md) — delivered **drawn**:
the library's own module draws it, with no engine behind it.

## Markup

```html
<span class="orb orb--lg" data-orb="thinking" role="img" aria-label="Thinking"></span>
```

The app writes the span and its name; the library adds a canvas inside it and draws. Change
`data-orb` and the drawing changes; nothing else is needed.

| Attribute | Values |
|---|---|
| `data-orb` | A state — `idle`, `thinking`, `searching`, `working`, `listening`, `speaking`, `waiting`, `error`, `done` — or an activity — `solving`, `writing`, `connecting`, `planning`, `shaping`, `reading`, `remembering`, `syncing`, `analyzing` |
| `data-orb-tone` | `brand`, `mono`, `go`, `warn`, `danger`. Optional; `waiting`, `error` and `done` pick their own family without it |
| `data-orb-eager` | Start at once rather than when the orb comes within a screen of the viewport |
| `aria-label` | Required. The orb draws no words, so its name is the app's: a state the reader can act on is worth saying |

| Class | Size |
|---|---|
| `.orb--xs` | 16px |
| `.orb--sm` | 20px |
| `.orb` | 32px |
| `.orb--md` | 48px |
| `.orb--lg` | 64px |
| `.orb--xl` | 96px |
| `.orb--hero` | 220px |
| `.orb--inline` | the text's size, centred on its x-height |

`--orb-size` sets any other size.

## Three drawings by size

The renderer draws the same scene three ways, as the brand mark is drawn three ways rather than scaled:
below 28px the orbits are solid lines, because dots that small read as noise; from 28px they are dots;
from 72px finer and denser. Every state and activity has a drawing at every size.

## Voice

`listening` and `speaking` follow `--level`, 0 to 1, from the orb or any element above it — the same
number the app writes for `.voice-bars`, `.btn-mic`, `.agent-avatar` and Mochi. Without one they move
gently on their own.

## Colour

Three roles, `--orb-body`, `--orb-dot` and `--orb-trail`, read through the element in the browser, so
the variant, colour vision, contrast, forced colours and the app's brand all reach the canvas, and the
orb repaints when any of them changes. They default to `--agent-from`, `--agent-to` and
`--agent-trail`; the tones re-point them.

## Lifecycle

`js-parts/45-orb.js` registers `[data-orb]` with the surface loader, which imports `Sedna.UI.orb.js`
relative to the main script the first time a page shows one, starts each orb as it comes within a
screen of the viewport, and disposes one whose element has gone. On a prerendered Blazor page orbs wait
for the interactive render, as every surface does.

An orb draws one still frame the moment it starts. After that a single clock draws every orb on screen
while the tab is visible; under `prefers-reduced-motion` the still frame is all there is, and a change
of state draws a new one.

## Without the script

The span is a still focal body inside one orbit — something is there, and it is an agent.

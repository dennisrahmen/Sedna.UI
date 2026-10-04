# Graph

Records and the links between them, drawn on a canvas: a dependency map, an org chart, a network,
a knowledge graph, a flow, a state machine. The catalogue's **Graph** pages show every part of this
running; this file is the reference.

The canvas is drawn by [cytoscape.js](https://js.cytoscape.org/), which ships inside the package
(see [Engine and plugins](#engine-and-plugins)). Everything a reader sees *around* the canvas — the
toolbar, the legend, the filters, the tooltip, the side panel, the menu, the empty state — is the
app's own markup with the library's classes, and the script only fills and places it.

## A graph

```html
<div class="graph" data-graph id="deps" aria-label="Service dependencies">
    <ul class="graph-data" data-graph-data>
        <li data-node="api" data-tone="2" data-icon="ri-server-line">Orders API</li>
        <li data-node="db" data-tone="4" data-shape="square">Orders database</li>
        <li data-edge data-source="api" data-target="db" data-label="reads">Orders API reads the orders database</li>
    </ul>
    <div class="graph-canvas"></div>
    <div class="graph-wait skeleton skeleton-block"></div>
</div>
```

Nothing goes in the host page: `Sedna.UI.js` finds every `[data-graph]`, and imports the engine the
first time one comes within a screen of the viewport. A page without a graph never downloads it.

- **`.graph-canvas` is required.** The engine draws into it.
- **The list is the data, and the text alternative.** `.graph-data` is visually hidden and read by a
  screen reader, so each item's text is a sentence ("Orders API reads the orders database") and
  `data-label` is the short form the canvas writes ("reads"). An item's text is its label when it
  has no `data-label`. Put `hidden` on the list when the page already says the same thing another
  way.
- **A render that changes the list changes the drawing.** What stayed keeps its place, what changed
  is restyled in place, and what arrived is placed beside what it links to.
- **`data-graph-deferred`** — the records arrive by call ([script](#script), [C#](#c)); the wait stays
  up until they do, instead of an empty state flashing first.
- **`data-graph-src="/api/graph"`** — fetched as JSON `{ nodes, edges }`, same-origin with cookies. The
  records never cross a Blazor circuit.
- **`data-graph-eager`** — start at once instead of when scrolled near.

`data-graph-state` on the element says where it is — `loading`, `ready`, `empty`, `filtered` or
`error` — and the stylesheet shows the app's `.graph-wait` while loading, `.graph-empty` when there
are no records, `.graph-empty--filtered` when the filters hide every one and `.graph-empty--error`
when the data could not be read.

`data-graph-colouring` on the element names the colouring on show — `tone`, or the `<name>` of the
`data-tone-<name>` a control or a call switched to — so an app shows the legend for each colouring
with CSS alone: `[data-graph-colouring="status"] .legend-status`.

### Records

| Attribute | JSON / C# | What it does |
|---|---|---|
| `data-node="id"` | `id` | Required, unique among records and links. |
| text, `data-label` | `label` | The name on the canvas. |
| `data-kind` | `kind` | A category — what a filter, a legend and a tooltip slot name. |
| `data-tone` | `tone` | `1`–`6`, `go`, `warn`, `danger`, `info`, `muted`, `brand`, `accent` — the colour of the `.series-*` class of the same name. Default `1`, the brand. |
| `data-tone-<name>` | `tones` | Another colouring, which `data-graph-colour="<name>"` switches to. |
| `data-shape` | `shape` | `circle`, `square`, `rounded`, `diamond`, `hexagon`, `octagon`, `pentagon`, `triangle`, `tag`, `star`, `barrel`, `rhomboid`, `vee`. |
| `data-icon` | `icon` | A Remix Icon class drawn inside the record. |
| `data-group` | `group` | Its island in the `islands` layout, and its outline with `data-graph-hulls`. The record whose id is a group's name is that group's heart. |
| `data-cluster` | `cluster` | Islands of one cluster are packed side by side. |
| `data-parent` | `parent` | The record it is drawn inside — a compound group. |
| `data-collapsed` | `collapsed` | Starts a group folded, with `data-graph-collapse`. |
| `data-weight` | `weight` | Its size. Default: how many links it has. |
| `data-muted` | `muted` | Drawn quieter; `data-graph-show="muted"` hides it. |
| `data-root` | `root` | What the graph is about: ringed, always named, where the keyboard starts. |
| `data-hub` | `hub` | Named in bold, first when names compete for room. |
| `data-href` | `href` | Where opening it goes. |
| `data-meta` | `meta` | A second line: the default tooltip, under the name in a box, `{meta}`. |
| `data-tags` | `tags` | Space-separated; a filter matches any one. |
| `data-x`, `data-y` | `x`, `y` | A position, for the `preset` layout. |
| `data-display` | `display` | `dot` or `box`, whatever the graph draws the rest as. |
| `data-state` | `state` | Where a run has got to: `next`, `running`, `waiting`, `done`, `failed`, `skipped`. None is a step not reached yet. See [A run](#a-run). |
| any other `data-*` | `fields` | The app's own field — a filter, a tooltip slot and an announcement can name it. |

### Links

| Attribute | JSON / C# | What it does |
|---|---|---|
| `data-edge="id"` | `id` | Optional; one is made from the ends. |
| `data-source`, `data-target` | `source`, `target` | Required: record ids. |
| text, `data-label` | `label` | Written on the link when it is lit, or always with `data-graph-edge-labels="always"`. |
| `data-kind` | `kind` | What an `edge.kind` filter names. |
| `data-tone` | `tone` | As a record's. Default: the quiet border colour. |
| `data-line` | `line` | `solid`, `dashed`, `dotted`. |
| `data-weight` | `weight` | `light`, `normal`, `heavy`, or a number (1 is normal): how thick it is drawn, and how hard it holds its ends together in a layout — in `dagre`, a `light` link gives way to the links the hierarchy is made of. |
| `data-arrow` | `arrow` | `none`, `target`, `source`, `both`. Default: `data-graph-arrows`. |
| `data-muted` | `muted` | Drawn quieter. |
| `data-state` | `state` | As a record's. Default: `skipped` out of a skipped record, else its target record's state. |

A link whose end is not a record, a duplicate id and a cycle of parents are dropped with a console
warning rather than failing the graph.

### The graph's options

On the `[data-graph]` element:

| Attribute | Values | |
|---|---|---|
| `data-graph-layout` | `force` (default), `islands`, `rings`, `concentric`, `tree`, `dagre`, `fcose`, `grid`, `circle`, `preset` | See [Layouts](#layouts). |
| `data-graph-direction` | `TB` (default), `LR`, `BT`, `RL` | For `dagre` and `tree`. |
| `data-graph-spacing` | `compact`, `normal`, `loose` | |
| `data-graph-nodes` | `dot` (default), `box` | A map of many, or a diagram of a few dozen. |
| `data-graph-labels` | `auto` (default), `all`, `none` | `auto` names what there is room for. |
| `data-graph-edge-labels` | `hover` (default for dots), `always` (default for boxes), `none` | |
| `data-graph-curve` | `bezier` (default), `straight`, `taxi`, `round-taxi` | `taxi` draws right angles — an org chart. |
| `data-graph-arrows` | `none` (default), `target`, `source`, `both` | |
| `data-graph-colour` | `tone` (default), or a `data-tone-<name>` | |
| `data-graph-open` | `dbltap` (default), `tap`, `none` | What opens a record. |
| `data-graph-wheel` | `focus` (default), `always`, `none` | `focus`: the wheel zooms once the canvas has been pressed, so a graph in a scrolling page does not swallow the scroll. |
| `data-graph-hover` | `light` (default), `none` | Light a record's neighbourhood under the pointer. |
| `data-graph-select` | `single` (default), `none` | |
| `data-graph-drag` | `false` | Records cannot be dragged. |
| `data-graph-focus`, `data-graph-focus-depth` | an id, 1–6 | Start on one record's neighbourhood. |
| `data-graph-connect`, `data-graph-collapse`, `data-graph-hulls` | — | Start with a plugin on; see [Plugins](#plugins). |
| `data-graph-managed` | — | Keep a link the reader drew, for a page with no app behind it. |
| `data-graph-renderer` | `canvas` (default), `webgl` | WebGL is faster for many thousands of elements on a real GPU, and is still experimental in the engine: choose it for a very large graph and check it on the hardware the app runs on. |

## The frame

```html
<div class="graph-frame" data-graph-frame>
    <div class="toolbar">…controls…</div>
    <div class="sedna-split">
        <div class="graph" data-graph id="deps" …>
            …
            <div class="graph-tools" role="toolbar" aria-label="View">
                <button class="btn btn-ghost btn-sm btn-icon" type="button" data-graph-action="fit"
                        aria-label="Fit" data-tip="Fit"><i class="ri-focus-3-line"></i></button>
            </div>
            <canvas class="graph-minimap" data-graph-minimap aria-hidden="true"></canvas>
        </div>
        <aside class="card card--fill sedna-split-aside graph-detail" data-graph-detail hidden>…</aside>
    </div>
</div>
```

| Class | |
|---|---|
| `.graph` | The frame around the canvas: `--graph-height` (26rem), `--sm`, `--lg`, `--fill` (its container's height), `--viewport` (what the viewport has left). `--graph-height` set on the `.graph-frame` sizes the graph and its side panel together. |
| `.graph-canvas` | What the engine draws into. |
| `.graph-data` | The records as markup, visually hidden. |
| `.graph-tools` | The toolbar on the canvas: top right; `--start`, `--bottom`, `--stack`; `.graph-tools-sep` between groups. A toggle's two icons are `.graph-when-off` and `.graph-when-on`. |
| `.graph-minimap` | The minimap canvas: bottom right; `--start`, `--top`. Hidden on a phone. |
| `.graph-tip` | The app's tooltip: `.graph-tip-head`, `-title`, `-meta`, `-ref`. |
| `.graph-menu` | On a `.menu`: the context menu, opened where the pointer is. |
| `.graph-wait` | Covers the canvas while loading — on a `.skeleton`. |
| `.graph-empty` | Covers the canvas when there is nothing — on an `.empty-state`; `--filtered`, `--error`. |
| `.graph-legend`, `.graph-key` | A row of keys. |
| `.graph-swatch` | A record's key, in a `.series-*` colour: `--square`, `--rounded`, `--diamond`, `--triangle`, `--hexagon`, `--star`, `--tag`, `--muted`, `--group`. |
| `.graph-line` | A link's key: `--dashed`, `--dotted`, `--light`, `--heavy`, `--arrow`. |
| `.graph-line--flow` | The key for a running link: dashes in the agent colour, moving towards the arrowhead; still under reduced motion. |
| `.graph-frame` | A graph with what goes around it — what full screen takes. |
| `.graph-detail` | The side panel: as tall as the graph and no taller, in full screen as well. On a `.card.card--fill` its head and foot stay and its body scrolls. |
| `.graph-direction` | A neighbour's direction in the panel: `-out`, `-in`, `-both` icons, one shown. |

**The graph is one stop in the tab order**, with a ring drawn inside its frame. It gets
`tabindex="0"` and `role="application"` unless the app wrote its own; its name —
`aria-label` — and `aria-roledescription`, if the word "graph" should be said, are the app's.

## Controls

Ordinary controls, anywhere in the page, drive a graph with no script of the app's own. A control
drives the graph named by `data-graph-for` on it or on a container around it, else the graph it is
inside, else the one in its `[data-graph-frame]`.

| Attribute | On | |
|---|---|---|
| `data-graph-action="…"` | a button | `zoom-in`, `zoom-out`, `fit`, `arrange`, `fullscreen`, `export-png`, `export-svg`, `reset`, `select`, `focus`, `unfocus`, `open`, `clear`, `hide`, `show-all`, `expand`, `collapse`, `expand-all`, `collapse-all`, `connect`, `hulls`, `reload`. An action on one record takes its `value`, else the menu's record, else the selection. |
| `data-graph-filter="field"` | a chip or a select | A chip — a checkbox, or a `button` with `aria-pressed` — **hides its value while it is off**. A select or radios **choose** one, and `""` chooses all. `edge.kind` filters links. |
| `data-graph-show="muted"` / `"isolated"` | a checkbox or toggle | Show muted records / records with no visible link. |
| `data-graph-search` | a text field | Marks matches as it is typed in. |
| `data-graph-option="…"` | a select or radios | `layout`, `direction`, `spacing`, `labels`, `edge-labels`, `colour`, `nodes`, `curve`, `arrows`, `depth`. |
| `data-graph-stats="{nodes} records · {edges} links"` | any element | Kept current in the app's words: `{nodes}`, `{edges}`, `{matches}`, `{totalNodes}`, `{totalEdges}`. `data-graph-stats-match` replaces it while a search marks records. |

A toggle `button` is flipped by the script, so its `aria-pressed` is the script's; a checkbox, a
radio, a select and a text field keep their native state, which a Blazor binding can own. A
`button.chip` with `aria-pressed="true"` looks chosen by itself. Reset puts back everything as it was
first drawn: every control, and what the controls and calls changed — the filter, the focus, the
search, the layout, the colouring and the other view options, the outlines, drawing mode and the folds
the data started with.

### Filling the app's markup

Every `[data-graph-field="name"]` inside the tooltip, the menu and the side panel gets the record's
value of that field as text — never as markup — and one the record lacks is hidden, together with what
belongs to it: the `<dt>` before a `<dd>` slot, and any element marked `data-graph-if="name"`, which is
how a label written beside a value leaves with it. `[data-graph-icon]` takes the record's icon class. `data-graph-tone` on the filled element carries the
record's tone.

- **Tooltip** — `[data-graph-tip]` inside the graph; `[data-graph-tip="edge"]` for links. Without one,
  the library's hover-hint bubble says the record's name and `meta`.
- **Menu** — `[data-graph-menu]` on a `.menu` inside the graph, opened by a right click, a long press or
  the context-menu key. Its `data-graph-action` items act on that record, and an item that does not
  apply hides itself — `open` without a `data-href`, `expand` on a record that is no folded group. A
  `.menu-sep` shows only between two things that are showing.
- **Side panel** — `[data-graph-detail]`, shown while a record is selected. Rows are cloned from its
  `<template data-graph-neighbour>`, one per neighbour, each with `data-graph-direction` (`out`, `in`,
  `both`); `[data-graph-link]` takes the link's label, and a row's `data-graph-action` acts on that
  neighbour.
- **Announcements** — `[data-graph-live]`, a polite live region, says each record the keyboard reaches
  in `data-graph-announce`, and a selection in `data-graph-announce-select`: `{label}`, `{kind}`,
  `{meta}`, any field, and `{links}`.

## Reading a graph

| Pointer | Keyboard | |
|---|---|---|
| hover | ← ↑ → ↓ | Light a record's neighbourhood; the tooltip. |
| — | Page Down / Up | The next / previous linked record — walking the links. |
| — | Home | The root, else the most connected record. |
| click | Space | Select, or clear. |
| double click | Enter | Open: `sedna-graph-open`, then `data-href`. On a group that folds, fold or unfold it. |
| right click, long press | context-menu key, Shift + F10 | The menu. |
| drag the background, wheel | Shift + arrows, + / −, 0 | Pan, zoom, fit. |
| — | Escape | Clear the selection, then the ring. |

Names are placed the way a map places them: the most important first, and one that would run into a
name already placed is left out. Zooming in makes room, so more appear. A name keeps its size on screen
down to a zoom of 0.4; further out it shrinks with the drawing, and one too small to read is not drawn. Search marks rather than
hides, because where a record sits among the others is what the graph is for.

## Layouts

| | For |
|---|---|
| `force` | A map of records, shaped by its links. |
| `islands` | A large graph read by its groups: one island per `data-group`, packed in rows as wide as the frame, a `data-cluster`'s side by side. What joins islands sits between them. |
| `rings` | Hops around the root or the focus. |
| `concentric` | The most connected in the middle. |
| `tree` | Breadth-first from the roots, in `data-graph-direction`. |
| `dagre` | A layered hierarchy — an org chart, a flow, a dependency tree. Each link is drawn along the route the layout found through the ranks between its ends, unless `data-graph-curve` chooses a curve. A plugin. |
| `fcose` | Springs that also lay out nested groups. A plugin. |
| `grid`, `circle` | Busiest first. |
| `preset` | Where `data-x` and `data-y` put each record. |
| any other name | A cytoscape layout of that name, when one is registered on the engine the handle's `cy` exposes. |

Every layout is deterministic: the drawing a reader learned yesterday is the one that comes back.

In a `tree` or `dagre` that runs sideways — `LR`, `RL` — a dot's name is written beside it, towards the
next level, rather than beneath it. Where a layout makes room for names — groups, `dagre` — it is run
at the zoom the drawing will be seen at, because a name is held at its screen size and is larger in a
drawing seen zoomed out. Unfolding a group arranges the drawing again around it.

## Plugins

Each loads the first time a graph uses it.

- **Minimap** — Sedna.UI's own: `<canvas class="graph-minimap" data-graph-minimap aria-hidden="true">`.
  The drawing in miniature with the view framed; press to move there, drag to pan, the wheel zooms.
- **Drawing links** (cytoscape-edgehandles) — `data-graph-connect`, or a `connect` toggle. Dragging from
  one record to another dispatches `sedna-graph-connect { source, target }`; the line is removed again
  and **the app adds the link to its own data**, as a drop is the app's to make. `data-graph-managed`
  keeps it instead.
- **Folding groups** (cytoscape-expand-collapse) — `data-graph-collapse` gives every `data-parent` group
  a fold cue; `expand`, `collapse`, `expand-all` and `collapse-all` act on them.
- **Outlines** (cytoscape-bubblesets) — `data-graph-hulls`, or a `hulls` toggle: a soft outline in its
  tone around every `data-group` of two or more, routed around the records not in it.
- **Hierarchies** (cytoscape-dagre) and **compound springs** (cytoscape-fcose) — the `dagre` and
  `fcose` layouts.
- **Export** — `export-svg` is Sedna.UI's own: each record a group with a `<title>`, the text as text,
  the outlines around groups included, every colour the one the canvas resolved. `export-png` is the engine's, at twice the resolution.
  Both export the whole drawing, not only the part in view. `data-graph-filename` names the file.

## A run

A graph shows a run live — an automation flow, a pipeline, an approval chain. Draw the flow as one:
`data-graph-layout="dagre"`, `data-graph-direction="LR"`, `data-graph-nodes="box"`,
`data-graph-arrows="target"`. Give each record the state the run has reached in `data-state`:

| State | Drawn |
|---|---|
| none | Not reached yet: drawn as without a run. |
| `next` | Runs next: a dashed border in the brand colour. |
| `running` | Working now: a ring in the agent colour, and a halo that breathes. |
| `waiting` | Reached, and waiting on something outside the run — a person, a reply: a dashed ring in the warning colour. |
| `done` | Finished: a ring in the go colour. |
| `failed` | A heavier ring in the danger colour. |
| `skipped` | A branch the run did not take: quieter, with a dashed border. |

```html
<li data-node="classify" data-icon="ri-sparkling-2-line" data-state="running" data-meta="Running">Classify with AI</li>
<li data-node="summarise" data-icon="ri-quill-pen-line" data-state="done" data-meta="Done in 1.2 s">Summarise with AI</li>
<li data-edge data-source="email" data-target="classify">The cleaned email is classified</li>
<li data-node="chat" data-icon="ri-chat-3-line" data-state="skipped">Join the chat transcript</li>
<li data-edge data-source="chat" data-target="classify">A chat transcript would be classified</li>
```

- **A record keeps its tone, its fill and its icon.** The state is drawn on its border, a ring outside
  it and a halo.
- **A link takes its target's state**, so set states on records and the links follow. A link into a
  running record is drawn in the agent colour with dashes moving from its source to its target; into a
  waiting or a done one, solid in the go colour; into a failed one, in the danger colour; into the next
  one, dashed; into a skipped one, dashed and quiet. A link out of a skipped record is skipped too, so a
  branch not taken stays quiet all the way to where the branches meet. A link's own `data-state` wins.
- **New states are new data.** Send the whole list again — `set()`, `SetDataAsync`, a render that
  changes the list — and only the records and links whose state changed are restyled. Nothing is laid
  out again.
- **What moves is decoration.** The halo and the dashes stand still under `prefers-reduced-motion` and in
  a tab nobody is looking at, and move at the theme's `--progress-duration` and `--pulse-duration`.
- **Colour is never the only carrier.** A running record breathes, `waiting`, `next` and `skipped` are
  dashed, and `failed` is heavier. Say the state in words as well: in `data-meta`, which a box writes under
  its name, and so in the tooltip, the search and the announcements.
- **The legend is the app's markup**: a `.graph-swatch` for each state on screen, and
  `<span class="graph-line graph-line--flow graph-line--arrow">` for a running link.

`state` is a field like any other: a filter, a tooltip slot and the record in every event carry it. In
C#, `State` on `SednaGraphNode` and `SednaGraphEdge` takes a `SednaGraphState`.

## Events

Bubbling from the `[data-graph]` element, with plain data in `detail`:

| Event | `detail` | |
|---|---|---|
| `sedna-graph-ready` | stats | Once, after the first drawing. |
| `sedna-graph-change` | stats | After a filter, a search, new data or a fold. |
| `sedna-graph-select` | record, or `{ id: null }` | |
| `sedna-graph-open` | record | Cancelable: cancelled, `data-href` is not followed. |
| `sedna-graph-hover` | record | Pointed at, or reached by keyboard. |
| `sedna-graph-context` | record + `x`, `y` | Cancelable: cancelled, the app's `[data-graph-menu]` does not open. |
| `sedna-graph-connect` | `{ source, target }` | A link the reader drew. |
| `sedna-graph-expand`, `-collapse` | `{ id, label }` | |

Stats are `{ nodes, edges, matches, totalNodes, totalEdges, selected }`. A record is
`{ id, label, kind, group, cluster, parent, meta, href, tone, state, tags, fields, keyboard }`.

## Script

`sednaUi.graph.get(elementOrId)` resolves the graph's handle, starting it if it has not started:

| Member | |
|---|---|
| `set(data, { relayout })` | New records `{ nodes, edges }`; resolves stats. |
| `filter(spec)` | `{ nodes, except, edges, edgesExcept, hide, muted, isolated, focus, depth }`; controls apply on top. |
| `search(text)`, `select(id)`, `focus(id, depth)` | |
| `layout(name, { direction, spacing })`, `option(name, value)` | |
| `fit()`, `zoom(factor)` | |
| `export('svg' \| 'png')` | A `Blob` of the whole drawing. |
| `download(format, filename)` | |
| `collapse(id?)`, `expand(id?)`, `connect(on)`, `hulls(on)` | |
| `style(rules)` | The app's own engine rules — `[{ selector, style }]` in cytoscape's style language — on top of the library's and kept across every repaint. A value written `var(--token)` is resolved through the theme like every other colour. |
| `reload()` | Fetches `data-graph-src` again and shows what changed; also the `reload` action. |
| `stats()`, `ready()` | |
| `cy` | The engine itself. Its API is cytoscape's and is **not versioned by Sedna.UI** — reach for it for what this does not offer. |
| `destroy()` | |

`sednaUi.graph.init(root)` starts every graph in `root` at once. A graph whose element leaves the
document is taken down with it.

## C#

`ISednaGraphs`, registered by `AddSednaUi()`, calls the same handle by the element's id, from
`OnAfterRenderAsync`:

```csharp
@inject ISednaGraphs Graphs

<div class="graph" data-graph id="deps" data-graph-deferred aria-label="Service dependencies"
     @onsedna-graph-select="Selected">
    <div class="graph-canvas"></div>
    <div class="graph-wait skeleton skeleton-block"></div>
</div>

@code {
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await Graphs.SetDataAsync("deps", new SednaGraphData(
                [new SednaGraphNode("api", "Orders API") { Tone = SednaGraphTone.Series(2) }, …],
                [new SednaGraphEdge("api", "db") { Label = "reads" }]));
    }

    private void Selected(SednaGraphNodeEventArgs e) => … e.Id …;
}
```

`SetDataAsync`, `FilterAsync`, `SearchAsync`, `SelectAsync`, `FocusAsync`, `LayoutAsync`,
`SetOptionAsync`, `FitAsync`, `ZoomAsync`, `ExportAsync` (the file's bytes, streamed — no circuit message-size limit applies), `DownloadAsync`,
`CollapseAsync`, `ExpandAsync`, `SetDrawingAsync`, `SetHullsAsync`, `ReloadAsync`, `StatsAsync`. A graph that is not
in the page answers null with a console warning, never an exception into the circuit.

`@onsedna-graph-select`, `-open`, `-hover`, `-context`, `-connect`, `-expand`, `-collapse`, `-change`
and `-ready` are bindable with `@using Sedna.UI`, with `SednaGraphNodeEventArgs`,
`SednaGraphContextEventArgs`, `SednaGraphConnectEventArgs` and `SednaGraphEventArgs`. A record's
`State` — a `SednaGraphState`, sent only when set — travels on `SednaGraphNode` and `SednaGraphEdge`
and comes back on `SednaGraphNodeEventArgs`.

**A large graph over Blazor Server** is best served by `data-graph-src`, which the browser fetches
itself, or by `SetDataAsync`; a list rendered by Razor is held in the circuit's render tree and
diffed through it on every change.

**On a prerendered page** a graph waits until Blazor has started and replaced the prerendered
markup, so it draws once, in the element that stays.

## Colour and theme

Every colour on the canvas is a token, read through the browser — so the variant, the colour-vision
setting, the contrast setting, forced colours and an app's own brand all reach it — and repainted
whenever any of them changes. A label's outline is whatever the graph sits on. Colour is never the
only carrier: give kinds a shape, links a line style and a label.

## Large graphs

- **Reading costs what is lit and what is in view**, not the size of the graph: pointing, selecting,
  the keyboard, the search, zooming and panning touch the neighbourhood and the part of the drawing
  near the view. Names and sizes further off are brought up to date when the view reaches them.
- **Arranging costs the whole drawing**: the first drawing, a filter, a layout, a direction or
  spacing, and new data that brings more than a few records lay the drawing out again. For thousands
  of records use `islands`, and give records a `data-group`.
- **Records arrive by `data-graph-src` or a call** rather than as a list Razor renders — see
  [C#](#c) — and a record whose data did not change is not redrawn when new data arrives.
- **A very large graph on a real GPU** can use `data-graph-renderer="webgl"`; check it on the
  hardware the app runs on.

## Engine and plugins

Vendored under `wwwroot/lib/cytoscape/` by `build/vendor-cytoscape.sh`, pinned by version and sha512,
and loaded by relative import from `Sedna.UI.graph.js`, which `Sedna.UI.js` imports on demand:

| Package | Licence | Loaded |
|---|---|---|
| cytoscape | MIT | with the first graph |
| cytoscape-dagre (bundling @dagrejs/dagre and @dagrejs/graphlib) | MIT | `dagre` layout |
| cytoscape-fcose, cose-base, layout-base | MIT | `fcose` layout |
| cytoscape-edgehandles, lodash.memoize, lodash.throttle | MIT | drawing links |
| cytoscape-expand-collapse | MIT | folding groups |
| cytoscape-bubblesets, bubblesets-js, cytoscape-layers | MIT | outlines |

The versions are in `wwwroot/lib/cytoscape/VENDORED.txt` and `THIRD-PARTY-NOTICES.md`, and a test
holds the two together. The package's size and a page's download are measured in the release notes.

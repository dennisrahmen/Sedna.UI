# Charts

Lines, areas, columns, bars, pies, donuts and scatters, drawn in SVG from a table the app writes. A
tier 3 surface — see [surfaces.md](surfaces.md) — delivered **drawn**: the library's own module draws
it, with no engine behind it. The catalogue's **Charts** page shows every part of this running; this
file is the reference.

Everything a reader sees *around* the drawing — the legend, the tooltip, the wait, the empty state, the
card — is the app's markup with the library's classes, and the script only fills and places it.

## A chart

```html
<div class="chart" data-chart="column" id="orders-by-month"
     aria-label="Orders per month, this year and last" aria-roledescription="chart">
    <table class="chart-data" data-chart-data>
        <caption>Orders per month</caption>
        <thead>
            <tr><th scope="col">Month</th><th scope="col">2026</th><th scope="col">2025</th></tr>
        </thead>
        <tbody>
            <tr><th scope="row">Jan</th><td>1240</td><td>1100</td></tr>
            <tr><th scope="row">Feb</th><td>1310</td><td>1180</td></tr>
            <tr><th scope="row">Mar</th><td data-value="1480">1 480</td><td></td></tr>
        </tbody>
    </table>
    <div class="chart-plot"></div>
    <div class="chart-wait skeleton skeleton-block"></div>
</div>
```

Nothing goes in the host page: `Sedna.UI.js` finds every `[data-chart]` and imports the drawing the
first time one comes within a screen of the viewport. A page without a chart never downloads it.

- **`.chart-plot` is required.** The script draws its `<svg>` into it.
- **The table is the data, and the text alternative.** `.chart-data` is visually hidden and read by a
  screen reader as a table; with scripting off it is shown in place of the drawing. Put `hidden` on it
  when the page already shows the same numbers another way.
- **The header row names the series**, one column each after the first; **each body row is a
  category**, its `<th>` the label. A cell's number is its `data-value`, else its text read as a plain
  number — `1240`, `-3.5`, spaces dropped. A comma is never guessed at, because it is a decimal point
  in half the world: such a cell carries `data-value`. The cell's text is what the tooltip and the
  announcement say. An empty cell is a gap.
- **A render that changes the table, or `data-chart-json`, changes the drawing.** The point the reader was on stays where it
  still exists. There is no C# service: a Blazor component renders the table and the chart follows.

### JSON instead of a table

For a series too long to read as a table, the data goes in `data-chart-json` on the chart's element —
an attribute, so a Blazor component binds it and a render redraws the chart:

```html
<div class="chart" data-chart="line" id="queue-depth" aria-label="…"
     data-chart-json='{ "categories": ["09:10", "09:20", "09:30"],
       "series": [{ "name": "Inbound", "tone": "2", "values": [620, 710, null] }] }'>
    <div class="chart-plot"></div>
</div>
```

A category is a label or `{ "label", "x", "tone" }`; a value is a number, `null`, or
`{ "value", "text" }`. A value given as a bare number is said in the chart's format. With no table there
is no text alternative but the chart's own `aria-label`, so name what the data shows in it.

### The chart's options

On the `[data-chart]` element:

| Attribute | Values | |
|---|---|---|
| `data-chart` | `line` (default), `area`, `column`, `bar`, `pie`, `donut`, `scatter` | The type. Changing it redraws. |
| `data-chart-stack` | — | Stack the series of a `column`, `bar` or `area` chart rather than setting them side by side or over each other. |
| `data-chart-min`, `data-chart-max` | a number | Fix an end of the value axis. Otherwise it takes in zero: a bar that does not start at zero misstates its value. |
| `data-chart-min="auto"` | — | A `line` or a `scatter` starts near its lowest value instead of at zero. |
| `data-chart-format` | JSON `Intl.NumberFormat` options | How the axis writes its numbers, and a value given as a bare number: `{"notation":"compact"}`, `{"style":"currency","currency":"EUR"}`, `{"style":"percent"}`. In the page's language, from the nearest `lang`. |
| `data-chart-eager` | — | Start at once instead of when scrolled near. |

On a header cell, a series: `data-tone` — `1`–`6`, `go`, `warn`, `danger`, `info` or `muted`, the colour
of the `.series-*` class of the same name; by default the series take `1` to `6` in order — and
`data-shape`, its marker: `circle`, `square`, `diamond`, `triangle`, `triangle-down` or `cross`, in that
order by default. On a body row, `data-tone` colours that category alone: a pie's slice, or one column
of a chart with a single series.

### The types

| Type | Draws |
|---|---|
| `line` | A line per series, with its marker at every value while a series has no more than 60; a gap where a value is missing. |
| `area` | A line per series over its filled area. Stacked, a missing value adds nothing to the layer below. |
| `column` | A bar per value, the series side by side in each category, or stacked. |
| `bar` | The same lying down: categories run down the side. For long category names. |
| `scatter` | A marker per value, at the category's own number across — its `<th>`'s `data-value` or text — and the value up. |
| `pie`, `donut` | The first series shown, a slice per category, from twelve o'clock clockwise. Slices take `1` to `6` in order unless a row names its tone. |

Right to left, the categories run from the right, the value axis's numbers sit on the right and a bar
grows from the right. The value axis still rises upwards.

## The frame

```html
<div class="card" data-chart-frame>
    <div class="card-head">
        <h3>Orders per month</h3>
    </div>
    <div class="card-body">
        <div class="chart-legend">
            <button class="chart-key" type="button" data-chart-toggle="2026" aria-pressed="true">
                <span class="chart-swatch chart-swatch--block series-1"></span><span>2026</span></button>
            <button class="chart-key" type="button" data-chart-toggle="2025" aria-pressed="true">
                <span class="chart-swatch chart-swatch--block series-2"></span><span>2025</span></button>
        </div>
        <div class="chart" data-chart="column" …>
            …
            <div class="chart-tip" data-chart-tip hidden>
                <span class="chart-tip-title" data-chart-field="category"></span>
                <template data-chart-tip-row>
                    <span class="chart-tip-row"><span class="chart-tip-swatch"></span>
                        <span data-chart-field="series"></span>
                        <span class="chart-tip-value" data-chart-field="value"></span></span>
                </template>
            </div>
        </div>
        <p class="visually-hidden" data-chart-live aria-live="polite"
           data-chart-announce="{category}, {series}: {value}."></p>
    </div>
</div>
```

| Class | |
|---|---|
| `.chart` | The frame around the drawing: `--chart-height` (16rem), `--sm` (10rem), `--lg` (24rem), `--fill` (its container's height). The width is always its container's, and the drawing follows it. |
| `.chart-plot` | What the script draws into. |
| `.chart-data` | The data as a table, visually hidden. |
| `.chart-tip` | The app's tooltip: `.chart-tip-title`, `.chart-tip-row` with its `.chart-tip-swatch` and `.chart-tip-value`. |
| `.chart-wait` | Covers the plot until the drawing is there, and while the chart is `aria-busy="true"` — on a `.skeleton`. |
| `.chart-empty` | Covers the plot when the data holds no value — on an `.empty-state`; `--error` when the app sets `data-chart-error` on the chart. |
| `.chart-total` | A figure over a donut's hole: `.chart-total-value` and `.chart-total-label`. |
| `.chart-legend`, `.chart-key` | A row of keys, with a gap from a chart just above or below it. A `button.chart-key` with `data-chart-toggle` hides and shows its series. |
| `.chart-swatch` | A series' key, in its `.series-*` colour and its marker's shape: `--square`, `--diamond`, `--triangle`, `--triangle-down`, `--cross`; `--block` for a filled mark, `--line` for a line. |

The classes on the drawing are the script's — `.chart-svg`, `.chart-grid`, `.chart-axis`,
`.chart-tick`, `.chart-category`, `.chart-mark`, `.chart-line`, `.chart-area`, `.chart-bar`,
`.chart-marker`, `.chart-marker--active`, `.chart-slice`, `.chart-ring`, `.chart-guide` and
`.chart-band`. An app never writes one, and may restyle one from its own stylesheet like any class.

`data-chart-state` on the element says where it is — `ready`, or `empty` when the data holds no value —
and the stylesheet shows the app's `.chart-wait` until it is written and `.chart-empty` when it is
`empty`. Loading and failing are the app's to say, because only the app knows: `aria-busy="true"` while
it fetches brings the wait back, and `data-chart-error` shows `.chart-empty--error`.

### Colour

Every mark carries the `.series-*` class of its tone and paints `currentColor`, so a chart is in the
theme's series colours — the same a legend swatch, a bar list and a graph record wear — in every variant,
colour vision and contrast setting and under forced colours, and repaints with no script when any of them
changes. There are no chart colour tokens: a chart's colours are the series.

**A series is told apart by its shape as well as its colour.** Each has its own marker, and a legend
says it with the swatch of the same shape: `.chart-swatch` is series one's circle, `--square` series
two's, and so on. The closest series colours rely on that second channel under colour-vision deficiency.

### Filling the app's markup

Every `[data-chart-field="name"]` inside the tooltip gets that field of the point as text — never as
markup — and one that is empty is hidden, with any element marked `data-chart-if="name"`. The fields,
for the tooltip and the announcements alike:

| Field | |
|---|---|
| `{series}` | the series' name, its column heading |
| `{category}` | the category's label, its row heading |
| `{value}` | the value as the app wrote it, the cell's text |
| `{number}` | the bare number |
| `{percent}` | its share of the category's total, or of the whole in a pie |
| `{total}` | the category's total, in the chart's format |
| `{index}`, `{count}` | which category, from 1, and how many there are |

`data-chart-tone` on the tip is the point's tone, and colours its `.chart-tip-swatch`. A
`<template data-chart-tip-row>` inside the tip makes it say the whole category: the row is cloned once per
series shown, where the template sits, and filled with that series' point; the reader's carries
`data-chart-current`. With no tooltip of the app's, the library's own hover-hint bubble says the
category, the series and the value.

The legend's buttons are delegated from `document`: a `[data-chart-toggle]` drives the chart named by
`data-chart-for` on it or on a container around it, else the one it is inside, else the one in its
`[data-chart-frame]`. `aria-pressed="true"` is shown. In a pie, the toggle names a category.

## The keyboard, and what a screen reader hears

The chart is one stop in the tab order. It gets `tabindex="0"` and `role="application"` unless the app
wrote its own; its name — `aria-label` — and `aria-roledescription`, if the word "chart" should be said,
are the app's. Reaching it by keyboard puts the reader on the first point.

| Key | |
|---|---|
| ← → | The previous / next category, as drawn: right is the next one left to right, the previous one right to left. In a bar chart, the next / previous series. |
| ↑ ↓ | The next / previous series at the same category. In a bar chart, the previous / next category. |
| Home, End | The first / last category. |
| Enter, Space | Choose the point: `sedna-chart-select`. |
| Escape | Let go of the point. |

A pie has one series, so every arrow walks its slices. The point the reader is on is ringed, its
category marked — a guide line through a line or an area, a band behind a column or a bar — and the
tooltip shows it. Every move is said through the app's live region, in the app's words:

```html
<p class="visually-hidden" data-chart-live aria-live="polite"
   data-chart-announce="{category}, {series}: {value}."
   data-chart-announce-select="{category} chosen."></p>
```

The region is the one inside the chart, else the one inside its `[data-chart-frame]`. Without
`data-chart-announce` it says the category, the series and the value; an empty
`data-chart-announce-select` says nothing on a choice, which is the default.

While a point is active the element carries `data-chart-active`. A click or a tap focuses the chart without the ring: the script sets `data-chart-pointer` on the element
and takes it off at the first key or when the focus leaves.

## Events

`sedna-chart-select` bubbles from the chart's element when a point is clicked, tapped, or chosen with
Enter or Space, with `{ chart, series, category, value, text, index, seriesIndex }` — `value` the number
or null, `text` the cell's own words. In Razor it is `@onsedna-chart-select`, with a
`SednaChartEventArgs`.

```razor
<div class="chart" data-chart="column" id="orders-by-month" @onsedna-chart-select="OpenMonth" …>
```

## Script

`sednaUi.chart.get(elOrId)` resolves the chart's handle, starting it if need be; `sednaUi.chart.init(root)`
starts every chart in `root` at once.

| Member | |
|---|---|
| `element` | The chart's element. |
| `type` | The type drawn. |
| `data` | The data drawn, as `{ categories, series: [{ name, tone, hidden, values }] }`. |
| `set(data)` | Draws this data — the JSON shape — instead of the markup's, until `reset()`. |
| `reset()` | Back to the markup's data. |
| `toggle(name, shown)` | Hides or shows a series by its name, a slice by its category in a pie; returns whether it is shown. |
| `redraw()` | Draws again now. |

## Lifecycle

`js-parts/43-chart.js` registers `[data-chart]` with the surface loader, which imports
`Sedna.UI.chart.js` relative to the main script the first time a page shows one, starts each chart as it
comes within a screen of the viewport, and disposes one whose element has gone. On a prerendered Blazor
page charts wait for the interactive render, as every surface does.

The first drawing fades in, once — the svg carries `data-chart-enter` for it — and under
`prefers-reduced-motion` it is drawn whole at once. Nothing after it moves: new data, a new size and a
series switched off are drawn at once.

## Without the script

The chart is its table: with scripting off `.chart-data` is shown in place of the plot and the wait.

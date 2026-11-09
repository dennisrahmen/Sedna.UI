# QR code

The app's text or address as a QR code, for a camera to read. A tier 3 surface — see
[surfaces.md](surfaces.md) — delivered **drawn**: Sedna.UI's own encoder draws the symbol as an SVG,
with no engine behind it. The app writes the element, its name and the human-readable text beside it.

## Markup

```html
<figure class="qr-figure">
  <span class="qr" data-qr="https://example.com/pair/7Q4K" aria-label="QR code to pair the device"></span>
  <figcaption class="qr-caption">example.com/pair/7Q4K</figcaption>
</figure>
```

| Attribute | Values |
|---|---|
| `data-qr` | The text to encode. Change it and the code is drawn again |
| `data-qr-level` | The error correction level: `L` (7% of the symbol can be lost), `M` (15%, the default), `Q` (25%), `H` (30%) |
| `data-qr-eager` | Start at once rather than when the code comes within a screen of the viewport |
| `aria-label` | Required. The code draws no words, so its name is the app's: say what scanning it does |

The script writes these on the element:

| Attribute | Means |
|---|---|
| `data-qr-state` | `ready`; `empty` when `data-qr` is empty; `overflow` when the text is more than the largest symbol holds at that level, and nothing is drawn |
| `data-qr-version` | The symbol's version, 1 to 40 — how dense it is. Absent unless `ready` |

`role="img"` is added when the element has no role.

| Class | Size |
|---|---|
| `.qr--sm` | 6rem |
| `.qr` | 10rem |
| `.qr--lg` | 15rem |

`--qr-size` sets any other size. The size is the whole square, the quiet zone included: the four light
modules the standard needs around a symbol are part of the drawing. A denser code needs a bigger
square — keep a module at least two device pixels, and prefer a short address to a large square.

`.qr-figure` stacks a code over its `.qr-caption`, the text the code holds, written out by the app for
the reader who cannot scan and the one who wants to check where it goes.

## Encoding

ISO/IEC 18004, as written:

- The text is UTF-8 in **byte mode**, with no ECI header; readers take a byte-mode payload that is valid
  UTF-8 as UTF-8.
- The level is the app's and is never raised.
- The **smallest version** — 1 to 40 — that holds the text at that level is used.
- The **mask** is the one of the eight with the lowest penalty under the standard's four rules, scored
  with its format information drawn; of equals, the first.

The most a code holds is 2953 bytes at `L`, 2331 at `M`, 1663 at `Q` and 1273 at `H`. A text that does
not fit draws nothing and sets `data-qr-state="overflow"`, and the console says so.

## Colour

A camera reads dark modules on light paper, so the paper stays light and the ink dark in every variant.
`--qr-ink` and `--qr-paper` re-point them; the contrast of what the app picks is the app's. Under
forced colours a code keeps its colours, as an image does — a dark palette would invert it into a code
few cameras read. It prints as it shows, and never across two pages.

## Lifecycle

`js-parts/48-qr.js` registers `[data-qr]` with the surface loader, which imports `Sedna.UI.qr.js`
relative to the main script the first time a page shows one, starts each code as it comes within a
screen of the viewport, and disposes one whose element has gone. On a prerendered Blazor page codes
wait for the interactive render, as every surface does.

## Without the script

An empty light square. The caption the app writes beside the code is the alternative, there and for
every reader who cannot scan.

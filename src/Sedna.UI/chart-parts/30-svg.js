/* ── Drawing in SVG ───────────────────────────────────────────────────────────
   The chart is SVG elements in the page, so its colour is CSS: every mark carries
   the `.series-*` class of its tone, and `54-chart.css` paints it `currentColor`.
   The browser resolves the token through every layer of the theme — the variant,
   colour vision, contrast, forced colours, the app's brand — exactly as it does for a
   legend swatch wearing the same class, and repaints it the moment any of them
   changes. The class is the probe; nothing here reads or names a colour.

   Every element is made with `createElementNS` and every word goes in as
   `textContent` — a category label and a tick are data, never markup.
   ─────────────────────────────────────────────────────────────────────────── */
const SVG_NS = 'http://www.w3.org/2000/svg';

function node(tag, attrs, parent) {
    const e = document.createElementNS(SVG_NS, tag);
    if (attrs) for (const k in attrs) if (attrs[k] !== null && attrs[k] !== undefined) e.setAttribute(k, attrs[k]);
    if (parent) parent.appendChild(e);
    return e;
}

function label(parent, x, y, words, cls, anchor, baseline) {
    const t = node('text', { x: round(x), y: round(y), class: cls, 'text-anchor': anchor || 'middle', 'dominant-baseline': baseline || 'auto' }, parent);
    t.textContent = words;
    return t;
}

const round = v => Math.round(v * 10) / 10;

function toneClass(tone) {
    return 'series-' + (tone || '1');
}

/* A series' marker, centred on (x, y), about `r` across its middle. */
function markerPath(shape, x, y, r) {
    const p = (...pts) => 'M' + pts.map(([px, py]) => `${round(px)} ${round(py)}`).join(' L') + ' Z';
    switch (shape) {
        case 'square': {
            const s = r * 0.88;
            return p([x - s, y - s], [x + s, y - s], [x + s, y + s], [x - s, y + s]);
        }
        case 'diamond': {
            const s = r * 1.25;
            return p([x, y - s], [x + s, y], [x, y + s], [x - s, y]);
        }
        case 'triangle':
            return p([x, y - r * 1.2], [x + r * 1.15, y + r * 0.8], [x - r * 1.15, y + r * 0.8]);
        case 'triangle-down':
            return p([x, y + r * 1.2], [x + r * 1.15, y - r * 0.8], [x - r * 1.15, y - r * 0.8]);
        case 'cross': {
            const a = r * 1.1, w = r * 0.42;
            return p([x - w, y - a], [x + w, y - a], [x + w, y - w], [x + a, y - w], [x + a, y + w], [x + w, y + w],
                [x + w, y + a], [x - w, y + a], [x - w, y + w], [x - a, y + w], [x - a, y - w], [x - w, y - w]);
        }
        default: {
            // A circle as a path, so every marker is one kind of element.
            return `M${round(x - r)} ${round(y)} a${round(r)} ${round(r)} 0 1 0 ${round(2 * r)} 0 a${round(r)} ${round(r)} 0 1 0 ${round(-2 * r)} 0 Z`;
        }
    }
}

/* Text widths, measured in the chart's own font without putting anything on screen. */
let measurer = null;
function measure(svg) {
    if (!measurer) measurer = document.createElement('canvas').getContext('2d');
    const style = getComputedStyle(svg);
    measurer.font = `${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
    const size = parseFloat(style.fontSize) || 12;
    return { size, width: words => measurer.measureText(words).width };
}

/* A label longer than `room` is cut, and ends in an ellipsis; the whole of it is in
   the table, the tooltip and the announcement. */
function fit(words, room, width) {
    if (width(words) <= room) return words;
    let lo = 0, hi = words.length;
    while (lo < hi) {
        const mid = (lo + hi + 1) >> 1;
        if (width(words.slice(0, mid) + '…') <= room) lo = mid; else hi = mid - 1;
    }
    return lo > 0 ? words.slice(0, lo).trimEnd() + '…' : '';
}

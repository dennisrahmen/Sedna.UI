/* ── The model: the app's table, or its JSON, read into one shape ────────────────
   A chart's data is the app's markup. The table is the data and the text alternative
   at once — visually hidden, read by a screen reader, readable with scripting off:

     <table class="chart-data" data-chart-data>
       <thead><tr><th>Month</th><th data-tone="1">2026</th><th data-tone="6">2025</th></tr></thead>
       <tbody>
         <tr><th>Jan</th><td>1 240</td><td data-value="1100">1,100</td></tr>
       </tbody>
     </table>

   The header row names the series, one column each, after the first; each body row is
   one category — its `<th>` the label, each cell a value. A cell's number is its
   `data-value`, else its text read as a plain number (`1240`, `-3.5`; spaces dropped).
   Its text is what the tooltip and the announcement say, so it is written the way the
   app writes a number. An empty cell, or one that is no number, is a gap.

   For a long series the app writes JSON instead, in `data-chart-json` on the chart's
   element — an attribute, so a Blazor component binds it and a render redraws — or
   hands the handle `set(data)`:

     data-chart-json='{ "categories": ["Jan", "Feb"],
       "series": [{ "name": "2026", "tone": "1", "values": [1240, 1310] }] }'

   Every shape lands here as { categories: [{ label, x, tone }], series: [{ name, tone,
   shape, values: [{ v, text }] }] }.
   ─────────────────────────────────────────────────────────────────────────── */
const TYPES = ['line', 'area', 'column', 'bar', 'pie', 'donut', 'scatter'];
const TONES = new Set(['1', '2', '3', '4', '5', '6', 'go', 'warn', 'danger', 'info', 'muted']);

/* A marker shape per series, so a series is told apart by its shape as well as its
   colour — the second channel the closest series colours need. In this order, so a
   legend swatch can name the same shape: `.chart-swatch--square` is series two's. */
const SHAPES = ['circle', 'square', 'diamond', 'triangle', 'triangle-down', 'cross'];

const warned = new Set();
function warnOnce(key, message) {
    if (warned.has(key)) return;
    warned.add(key);
    try { console.warn('Sedna.UI chart: ' + message); } catch (e) { /* ignore */ }
}

function text(value) {
    if (value === undefined || value === null) return '';
    return String(value).trim();
}

function toneOf(value, fallback) {
    const s = text(value);
    if (!s) return fallback;
    if (TONES.has(s)) return s;
    warnOnce('tone:' + s, `"${s}" is not a series tone. Use 1–6, go, warn, danger, info or muted.`);
    return fallback;
}

function shapeOf(value, index) {
    const s = text(value);
    if (s && SHAPES.includes(s)) return s;
    if (s) warnOnce('shape:' + s, `"${s}" is not a marker shape. Use ${SHAPES.join(', ')}.`);
    return SHAPES[index % SHAPES.length];
}

/* A plain number from a cell or an attribute: digits, one point, a sign. Spaces of any
   width are dropped, so `1 240` reads; a comma is not guessed at, because it is a
   decimal point in half the world — such a cell carries `data-value`. */
function plainNumber(value) {
    if (typeof value === 'number') return Number.isFinite(value) ? value : null;
    const s = text(value).replace(/[\s  ]/g, '').replace(/^−/, '-');
    if (!s || !/^[-+]?(\d+\.?\d*|\.\d+)(e[-+]?\d+)?$/i.test(s)) return null;
    const n = Number(s);
    return Number.isFinite(n) ? n : null;
}

function cellNumber(cell) {
    return plainNumber(cell.hasAttribute('data-value') ? cell.getAttribute('data-value') : cell.textContent);
}

function fromTable(table) {
    const head = table.tHead && table.tHead.rows.length ? table.tHead.rows[0] : table.rows[0];
    if (!head) return { categories: [], series: [] };
    const series = Array.from(head.cells).slice(1).map((th, i) => ({
        name: text(th.textContent),
        tone: toneOf(th.getAttribute('data-tone'), String((i % 6) + 1)),
        shape: shapeOf(th.getAttribute('data-shape'), i),
        values: [],
    }));
    const rows = [];
    for (const body of Array.from(table.tBodies)) rows.push(...Array.from(body.rows));
    const categories = [];
    for (const row of rows) {
        if (row === head || !row.cells.length) continue;
        const cells = Array.from(row.cells);
        categories.push({
            label: text(cells[0].textContent),
            x: cellNumber(cells[0]),
            tone: row.hasAttribute('data-tone') ? toneOf(row.getAttribute('data-tone'), null) : null,
        });
        series.forEach((s, i) => {
            const cell = cells[i + 1];
            s.values.push(cell ? { v: cellNumber(cell), text: text(cell.textContent) } : { v: null, text: '' });
        });
    }
    return { categories, series };
}

/* The JSON shape, and what `set(data)` takes. A category is a label or { label, x,
   tone }; a value is a number, null, or { value, text }. */
function fromObject(data, format) {
    if (!data || typeof data !== 'object') return { categories: [], series: [] };
    const categories = (Array.isArray(data.categories) ? data.categories : []).map(c =>
        c !== null && typeof c === 'object'
            ? { label: text(c.label), x: plainNumber(c.x ?? c.label), tone: c.tone ? toneOf(c.tone, null) : null }
            : { label: text(c), x: plainNumber(c), tone: null });
    const series = (Array.isArray(data.series) ? data.series : []).map((s, i) => ({
        name: text(s && s.name),
        tone: toneOf(s && s.tone, String((i % 6) + 1)),
        shape: shapeOf(s && s.shape, i),
        values: categories.map((_, j) => {
            const raw = s && Array.isArray(s.values) ? s.values[j] : null;
            const v = raw !== null && typeof raw === 'object' ? plainNumber(raw.value) : plainNumber(raw);
            const said = raw !== null && typeof raw === 'object' && raw.text !== undefined ? text(raw.text) : '';
            return { v, text: said || (v === null ? '' : format.value(v)) };
        }),
    }));
    return { categories, series };
}

function readJson(raw) {
    try {
        return JSON.parse(raw || 'null');
    } catch (e) {
        warnOnce('json', 'data-chart-json is not valid JSON.');
        return null;
    }
}

/* Where a chart's data is: an app's call, else its JSON, else its table. */
function readData(c) {
    if (c.given) return fromObject(c.given, c.format);
    if (c.el.hasAttribute('data-chart-json')) return fromObject(readJson(c.el.getAttribute('data-chart-json')), c.format);
    const table = c.el.querySelector('table[data-chart-data]');
    return table ? fromTable(table) : { categories: [], series: [] };
}

function typeOf(el) {
    const t = text(el.getAttribute('data-chart')) || 'line';
    if (TYPES.includes(t)) return t;
    warnOnce('type:' + t, `"${t}" is not a chart type. Use ${TYPES.join(', ')}.`);
    return 'line';
}

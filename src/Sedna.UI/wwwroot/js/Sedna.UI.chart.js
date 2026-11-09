/*! Sedna.UI — generated, comments stripped. The commented copy ships in the package as readable/Sedna.UI.chart.js. */
const TYPES = ['line', 'area', 'column', 'bar', 'pie', 'donut', 'scatter'];
const TONES = new Set(['1', '2', '3', '4', '5', '6', 'go', 'warn', 'danger', 'info', 'muted']);
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
function languageOf(el) {
    const holder = el.closest('[lang]');
    return (holder && holder.getAttribute('lang')) || document.documentElement.lang || undefined;
}
function formatter(el) {
    const lang = languageOf(el);
    let options = {};
    const raw = el.getAttribute('data-chart-format');
    if (raw) {
        try {
            options = JSON.parse(raw) || {};
        } catch (e) {
            warnOnce('format:' + raw, 'data-chart-format is not JSON. Write the Intl.NumberFormat options, e.g. {"notation":"compact"}.');
        }
    }
    let full, percent;
    try {
        full = new Intl.NumberFormat(lang, options);
    } catch (e) {
        warnOnce('format-options:' + raw, 'data-chart-format names options Intl.NumberFormat refuses.');
        full = new Intl.NumberFormat(lang);
    }
    try { percent = new Intl.NumberFormat(lang, { style: 'percent', maximumFractionDigits: 1 }); }
    catch (e) { percent = new Intl.NumberFormat(undefined, { style: 'percent' }); }
    const ticks = new Map();
    return {
        lang,
        value: v => full.format(v),
        percent: v => percent.format(v),
        ticksOf(axis) {
            let out = [];
            for (let extra = 0; extra <= 3; extra++) {
                out = axis.ticks.map(t => this.tick(t, axis.step, extra));
                if (new Set(out).size === out.length) break;
            }
            return out;
        },
        tick(v, step, extra = 0) {
            const decimals = (String(clean(Math.abs(step))).split('.')[1] || '').length;
            const digits = Math.max(0, Math.min(6, decimals + extra - (options.style === 'percent' ? 2 : 0)));
            const key = digits;
            if (!ticks.has(key)) {
                const o = Object.assign({}, options, { maximumFractionDigits: digits });
                o.minimumFractionDigits = Math.min(o.minimumFractionDigits ?? 0, digits);
                delete o.maximumSignificantDigits;
                delete o.minimumSignificantDigits;
                let f;
                try { f = new Intl.NumberFormat(lang, o); } catch (e) { f = full; }
                ticks.set(key, f);
            }
            return ticks.get(key).format(v);
        },
    };
}
const clean = v => +v.toFixed(10);
function niceStep(span, count) {
    const raw = span / Math.max(1, count);
    const mag = Math.pow(10, Math.floor(Math.log10(raw)));
    const f = raw / mag;
    return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10) * mag;
}
function niceAxis(lo, hi, count, fixed) {
    if (!Number.isFinite(lo) || !Number.isFinite(hi)) { lo = 0; hi = 1; }
    if (lo === hi) {
        if (lo === 0) hi = 1;
        else if (lo > 0) lo = fixed.min ?? 0;
        else hi = fixed.max ?? 0;
        if (lo === hi) { lo -= 1; hi += 1; }
    }
    const step = niceStep(hi - lo, count);
    const a = fixed.min ?? clean(Math.floor(lo / step + 1e-9) * step);
    const b = fixed.max ?? clean(Math.ceil(hi / step - 1e-9) * step);
    const ticks = [];
    const first = clean(Math.ceil(a / step - 1e-9) * step);
    for (let t = first; t <= b + step * 1e-6; t = clean(t + step)) ticks.push(t);
    return { lo: a, hi: b > a ? b : a + step, step, ticks };
}
function attrNumber(el, name) {
    const raw = el.getAttribute(name);
    return raw === null || raw === 'auto' ? null : plainNumber(raw);
}
function linear(lo, hi, from, to) {
    const span = hi - lo || 1;
    const f = v => from + ((v - lo) / span) * (to - from);
    f.lo = lo; f.hi = hi; f.from = from; f.to = to;
    return f;
}
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
            return `M${round(x - r)} ${round(y)} a${round(r)} ${round(r)} 0 1 0 ${round(2 * r)} 0 a${round(r)} ${round(r)} 0 1 0 ${round(-2 * r)} 0 Z`;
        }
    }
}
let measurer = null;
function measure(svg) {
    if (!measurer) measurer = document.createElement('canvas').getContext('2d');
    const style = getComputedStyle(svg);
    measurer.font = `${style.fontWeight} ${style.fontSize} ${style.fontFamily}`;
    const size = parseFloat(style.fontSize) || 12;
    return { size, width: words => measurer.measureText(words).width };
}
function fit(words, room, width) {
    if (width(words) <= room) return words;
    let lo = 0, hi = words.length;
    while (lo < hi) {
        const mid = (lo + hi + 1) >> 1;
        if (width(words.slice(0, mid) + '…') <= room) lo = mid; else hi = mid - 1;
    }
    return lo > 0 ? words.slice(0, lo).trimEnd() + '…' : '';
}
function shownSeries(c) {
    const out = [];
    c.data.series.forEach((s, i) => { if (!c.hidden.has(s.name)) out.push({ s, i }); });
    return out;
}
function valueExtent(c, shown, stacked) {
    let lo = Infinity, hi = -Infinity;
    const n = c.data.categories.length;
    if (stacked) {
        for (let j = 0; j < n; j++) {
            let up = 0, down = 0;
            for (const { s } of shown) {
                const v = s.values[j] && s.values[j].v;
                if (v === null || v === undefined) continue;
                if (v >= 0) up += v; else down += v;
            }
            lo = Math.min(lo, down);
            hi = Math.max(hi, up);
        }
    } else {
        for (const { s } of shown) for (const p of s.values) {
            if (p.v === null) continue;
            lo = Math.min(lo, p.v);
            hi = Math.max(hi, p.v);
        }
    }
    if (lo === Infinity) { lo = 0; hi = 0; }
    return { lo, hi };
}
function layoutCartesian(c, shown, stacked) {
    const { w, h } = c.size;
    const m = c.measure;
    const fs = m.size;
    const across = c.type !== 'bar';
    const scatter = c.type === 'scatter';
    const cats = c.data.categories;
    const n = cats.length;
    const fixed = { min: attrNumber(c.el, 'data-chart-min'), max: attrNumber(c.el, 'data-chart-max') };
    const auto = c.el.getAttribute('data-chart-min') === 'auto' && (c.type === 'line' || scatter);
    let { lo, hi } = valueExtent(c, shown, stacked);
    if (!auto && fixed.min === null) { lo = Math.min(lo, 0); hi = Math.max(hi, 0); }
    if (fixed.min !== null) lo = Math.min(fixed.min, hi);
    if (fixed.max !== null) hi = Math.max(fixed.max, lo);
    const pad = 8;
    const top = Math.ceil(fs * 0.6) + 2;
    const catWidths = cats.map(cat => m.width(cat.label));
    const widest = catWidths.length ? Math.max(...catWidths) : 0;
    let plot, values, catAt, band, xAxis = null;
    if (across) {
        const bottom = fs + 12;
        const plotH = Math.max(10, h - top - bottom);
        const axis = niceAxis(lo, hi, Math.max(2, Math.floor(plotH / (fs * 3.2))), fixed);
        const tickText = c.format.ticksOf(axis);
        const gutter = Math.ceil(Math.max(0, ...tickText.map(m.width))) + 10;
        const startPad = gutter;
        let endPad = pad;
        if (scatter || c.type === 'line' || c.type === 'area') endPad = Math.max(pad, Math.ceil((scatter ? 0 : Math.min(widest, 120)) / 2));
        const x0 = c.rtl ? endPad : startPad;
        const x1 = w - (c.rtl ? startPad : endPad);
        plot = { x0, x1, y0: top, y1: top + plotH };
        values = linear(axis.lo, axis.hi, plot.y1, plot.y0);
        values.axis = axis;
        values.text = tickText;
        const span = x1 - x0;
        if (scatter) {
            const xs = cats.map(cat => cat.x).filter(x => x !== null);
            const xAxisTicks = niceAxis(Math.min(...xs, Infinity) === Infinity ? 0 : Math.min(...xs),
                xs.length ? Math.max(...xs) : 1, Math.max(2, Math.floor(span / 90)), {});
            const sx = c.rtl ? linear(xAxisTicks.lo, xAxisTicks.hi, x1, x0) : linear(xAxisTicks.lo, xAxisTicks.hi, x0, x1);
            xAxis = { scale: sx, axis: xAxisTicks, text: c.format.ticksOf(xAxisTicks) };
            catAt = j => (cats[j].x === null ? null : sx(cats[j].x));
            band = 0;
        } else if (c.type === 'column') {
            band = span / Math.max(1, n);
            catAt = j => (c.rtl ? x1 - (j + 0.5) * band : x0 + (j + 0.5) * band);
        } else {
            band = span / Math.max(1, n);
            const inset = n > 1 ? Math.min(band / 2, Math.max(pad, Math.min(widest, 120) / 2)) : span / 2;
            const step = n > 1 ? (span - 2 * inset) / (n - 1) : 0;
            catAt = j => (c.rtl ? x1 - inset - j * step : x0 + inset + j * step);
        }
    } else {
        const bottom = fs + 12;
        const labelRoom = Math.min(Math.ceil(widest), Math.floor(w * 0.35)) + 10;
        const x0 = c.rtl ? pad : labelRoom;
        const x1 = w - (c.rtl ? labelRoom : pad);
        const axisGuess = niceAxis(lo, hi, Math.max(2, Math.floor((x1 - x0) / 80)), fixed);
        const tickText = c.format.ticksOf(axisGuess);
        const half = Math.ceil(Math.max(0, ...tickText.map(m.width)) / 2);
        plot = { x0: c.rtl ? x0 + half : x0, x1: c.rtl ? x1 : x1 - half, y0: pad, y1: h - bottom };
        values = c.rtl ? linear(axisGuess.lo, axisGuess.hi, plot.x1, plot.x0) : linear(axisGuess.lo, axisGuess.hi, plot.x0, plot.x1);
        values.axis = axisGuess;
        values.text = tickText;
        values.labelRoom = labelRoom;
        band = (plot.y1 - plot.y0) / Math.max(1, n);
        catAt = j => plot.y0 + (j + 0.5) * band;
    }
    return { plot, values, catAt, band, across, xAxis, catWidths };
}
function drawAxes(c, g, L) {
    const { plot, values, across, xAxis } = L;
    const m = c.measure;
    const fs = m.size;
    const grid = node('g', { class: 'chart-grid' }, g);
    const ticks = node('g', { class: 'chart-ticks' }, g);
    const axisLine = (x1, y1, x2, y2) => node('line', { x1: round(x1), y1: round(y1), x2: round(x2), y2: round(y2), class: 'chart-axis' }, g);
    values.axis.ticks.forEach((t, k) => {
        const p = round(values(t));
        if (across) {
            node('line', { x1: plot.x0, x2: plot.x1, y1: p, y2: p }, grid);
            label(ticks, c.rtl ? plot.x1 + 8 : plot.x0 - 8, p, values.text[k], 'chart-tick', c.rtl ? 'start' : 'end', 'central');
        } else {
            node('line', { x1: p, x2: p, y1: plot.y0, y2: plot.y1 }, grid);
            label(ticks, p, plot.y1 + fs + 4, values.text[k], 'chart-tick', 'middle');
        }
    });
    const zero = values.axis.lo <= 0 && values.axis.hi >= 0 ? 0 : values.axis.lo;
    const z = values(zero);
    if (across) axisLine(plot.x0, z, plot.x1, z);
    else axisLine(z, plot.y0, z, plot.y1);
    if (xAxis) {
        xAxis.axis.ticks.forEach((t, k) => {
            const p = round(xAxis.scale(t));
            node('line', { x1: p, x2: p, y1: plot.y0, y2: plot.y1 }, grid);
            label(ticks, p, plot.y1 + fs + 4, xAxis.text[k], 'chart-tick', 'middle');
        });
        return;
    }
    const cats = c.data.categories;
    const n = cats.length;
    if (!n) return;
    if (across) {
        const spacing = n > 1 ? Math.abs(L.catAt(1) - L.catAt(0)) : plot.x1 - plot.x0;
        const widest = Math.max(...L.catWidths);
        const every = Math.max(1, Math.ceil((widest + 12) / Math.max(1, spacing)));
        const room = Math.max(24, every * spacing - 8);
        for (let j = 0; j < n; j += every) {
            let anchor = 'middle';
            const x = L.catAt(j);
            if (c.type !== 'column') {
                const halfW = Math.min(L.catWidths[j], room) / 2;
                if (x - halfW < 0) anchor = 'start';
                else if (x + halfW > c.size.w) anchor = 'end';
            }
            label(ticks, anchor === 'start' ? 0 : anchor === 'end' ? c.size.w : x, plot.y1 + fs + 4,
                fit(cats[j].label, room, m.width), 'chart-tick chart-category', anchor);
        }
    } else {
        const every = Math.max(1, Math.ceil((fs + 4) / Math.max(1, L.band)));
        const room = values.labelRoom - 10;
        for (let j = 0; j < n; j += every) {
            label(ticks, c.rtl ? plot.x1 + 8 : plot.x0 - 8, L.catAt(j), fit(cats[j].label, room, m.width),
                'chart-tick chart-category', c.rtl ? 'start' : 'end', 'central');
        }
    }
}
const MARKERS_UP_TO = 60;
function drawCartesian(c, g) {
    const shown = shownSeries(c);
    const stacked = c.el.hasAttribute('data-chart-stack') && (c.type === 'column' || c.type === 'bar' || c.type === 'area');
    const L = layoutCartesian(c, shown, stacked);
    c.layout = L;
    drawAxes(c, g, L);
    const marks = node('g', { class: 'chart-marks' }, g);
    const cats = c.data.categories;
    const n = cats.length;
    const v = L.values;
    if (c.type === 'column' || c.type === 'bar') {
        const across = L.across;
        const width = L.band * (stacked || shown.length < 2 ? 0.62 : 0.78);
        const each = stacked ? width : width / Math.max(1, shown.length);
        for (let j = 0; j < n; j++) {
            let up = 0, down = 0;
            shown.forEach(({ s, i }, k) => {
                const value = s.values[j] && s.values[j].v;
                if (value === null || value === undefined) return;
                let from, to;
                if (stacked) {
                    from = value >= 0 ? up : down;
                    to = from + value;
                    if (value >= 0) up = to; else down = to;
                } else {
                    from = Math.max(v.axis.lo, Math.min(0, v.axis.hi));
                    to = value;
                }
                const a = v(from), b = v(to);
                const centre = L.catAt(j);
                const offset = stacked ? 0 : (k - (shown.length - 1) / 2) * each * (across && c.rtl ? -1 : 1);
                const mid = centre + offset;
                const gap = each > 6 && !stacked ? 1 : 0;
                const box = across
                    ? { x: mid - each / 2 + gap, y: Math.min(a, b), w: Math.max(1, each - 2 * gap), h: Math.max(0.5, Math.abs(b - a)) }
                    : { x: Math.min(a, b), y: mid - each / 2 + gap, w: Math.max(0.5, Math.abs(b - a)), h: Math.max(1, each - 2 * gap) };
                const tone = shown.length === 1 && cats[j].tone ? cats[j].tone : s.tone;
                node('rect', {
                    x: round(box.x), y: round(box.y), width: round(box.w), height: round(box.h),
                    rx: Math.min(2, box.w / 2, box.h / 2) > 0 ? round(Math.min(2, box.w / 2, box.h / 2)) : null,
                    class: 'chart-mark chart-bar ' + toneClass(tone),
                }, marks);
                const tip = across ? { x: mid, y: value >= 0 ? box.y : box.y + box.h } : { x: (value >= 0) === !c.rtl ? box.x + box.w : box.x, y: mid };
                c.points.push({ s: i, i: j, x: tip.x, y: tip.y, box, tone, shape: s.shape });
            });
        }
        return;
    }
    const lowest = v(Math.max(v.axis.lo, Math.min(0, v.axis.hi)));
    const running = new Array(n).fill(0);
    const areas = node('g', { class: 'chart-areas' }, marks);
    const lines = node('g', { class: 'chart-lines' }, marks);
    const dots = node('g', { class: 'chart-markers' }, marks);
    for (const { s, i } of shown) {
        const pts = [];
        for (let j = 0; j < n; j++) {
            let value = s.values[j] && s.values[j].v;
            const x = L.catAt(j);
            if ((value === null || value === undefined) && stacked) value = 0;
            if (value === null || value === undefined || x === null) { pts.push(null); continue; }
            const before = stacked ? running[j] : null;
            const total = stacked ? before + value : value;
            if (stacked) running[j] = total;
            const p = { s: i, i: j, x, y: v(total), floor: stacked ? v(before) : lowest, tone: s.tone, shape: s.shape };
            pts.push(p);
            c.points.push(p);
        }
        if (c.type !== 'scatter') {
            let d = '', area = '', run = [];
            const flush = () => {
                if (!run.length) return;
                d += 'M' + run.map(p => `${round(p.x)} ${round(p.y)}`).join(' L');
                if (c.type === 'area') {
                    area += 'M' + run.map(p => `${round(p.x)} ${round(p.y)}`).join(' L')
                        + ' L' + run.slice().reverse().map(p => `${round(p.x)} ${round(p.floor)}`).join(' L') + ' Z';
                }
                run = [];
            };
            for (const p of pts) { if (p) run.push(p); else flush(); }
            flush();
            if (area) node('path', { d: area, class: 'chart-mark chart-area ' + toneClass(s.tone) }, areas);
            if (d) node('path', { d, class: 'chart-mark chart-line ' + toneClass(s.tone) }, lines);
        }
        const count = pts.filter(Boolean).length;
        const markers = c.type === 'scatter' || (c.type === 'line' && count <= MARKERS_UP_TO) || (c.type === 'area' && count <= 24 && !stacked);
        if (markers) {
            const r = c.type === 'scatter' ? 4.5 : 3.5;
            for (const p of pts) {
                if (!p) continue;
                node('path', { d: markerPath(s.shape, p.x, p.y, r), class: 'chart-mark chart-marker ' + toneClass(s.tone) }, dots);
            }
        }
    }
}
function drawPie(c, g) {
    const series = shownSeries(c)[0] || (c.data.series[0] ? { s: c.data.series[0], i: 0 } : null);
    c.layout = null;
    if (!series) return;
    const { w, h } = c.size;
    const r = Math.max(4, Math.min(w, h) / 2 - 6);
    const r0 = c.type === 'donut' ? r * 0.62 : 0;
    const cx = w / 2, cy = h / 2;
    const cats = c.data.categories;
    const slices = [];
    let total = 0;
    cats.forEach((cat, j) => {
        if (c.hidden.has(cat.label)) return;
        const value = series.s.values[j] && series.s.values[j].v;
        if (value === null || value === undefined || value <= 0) return;
        slices.push({ j, value, tone: cat.tone || String((j % 6) + 1) });
        total += value;
    });
    c.pie = { cx, cy, r, r0, total };
    const marks = node('g', { class: 'chart-marks' }, g);
    let angle = -Math.PI / 2;
    for (const slice of slices) {
        const sweep = (slice.value / total) * Math.PI * 2;
        const a0 = angle, a1 = angle + sweep;
        angle = a1;
        node('path', { d: slicePath(cx, cy, r0, r, a0, a1), class: 'chart-mark chart-slice ' + toneClass(slice.tone) }, marks);
        const mid = (a0 + a1) / 2;
        const at = r0 ? (r0 + r) / 2 : r * 0.62;
        c.points.push({
            s: series.i, i: slice.j, x: cx + Math.cos(mid) * at, y: cy + Math.sin(mid) * at,
            slice: { a0, a1, r0, r }, tone: slice.tone, shape: series.s.shape, share: slice.value / total,
        });
    }
}
function slicePath(cx, cy, r0, r, a0, a1) {
    const pt = (rad, a) => `${round(cx + Math.cos(a) * rad)} ${round(cy + Math.sin(a) * rad)}`;
    if (a1 - a0 >= Math.PI * 2 - 1e-6) {
        const mid = a0 + Math.PI;
        const outer = `M${pt(r, a0)} A${round(r)} ${round(r)} 0 1 1 ${pt(r, mid)} A${round(r)} ${round(r)} 0 1 1 ${pt(r, a0)} Z`;
        if (!r0) return outer;
        return outer + ` M${pt(r0, a0)} A${round(r0)} ${round(r0)} 0 1 0 ${pt(r0, mid)} A${round(r0)} ${round(r0)} 0 1 0 ${pt(r0, a0)} Z`;
    }
    const large = a1 - a0 > Math.PI ? 1 : 0;
    if (!r0) return `M${round(cx)} ${round(cy)} L${pt(r, a0)} A${round(r)} ${round(r)} 0 ${large} 1 ${pt(r, a1)} Z`;
    return `M${pt(r, a0)} A${round(r)} ${round(r)} 0 ${large} 1 ${pt(r, a1)} L${pt(r0, a1)} A${round(r0)} ${round(r0)} 0 ${large} 0 ${pt(r0, a0)} Z`;
}
function fields(c, p) {
    const s = c.data.series[p.s];
    const cat = c.data.categories[p.i];
    const cell = s && s.values[p.i];
    const v = cell ? cell.v : null;
    let total = 0;
    if (c.pie) total = c.pie.total;
    else for (const { s: other } of shownSeries(c)) {
        const o = other.values[p.i] && other.values[p.i].v;
        if (o !== null && o !== undefined) total += o;
    }
    const share = p.share ?? (total ? v / total : null);
    return {
        series: s ? s.name : '',
        category: cat ? cat.label : '',
        value: cell ? cell.text || (v === null ? '' : c.format.value(v)) : '',
        number: v === null ? '' : String(v),
        percent: share === null || !Number.isFinite(share) ? '' : c.format.percent(share),
        total: c.format.value(total),
        index: String(p.i + 1),
        count: String(c.data.categories.length),
    };
}
function fillTemplate(template, f) {
    return template.replace(/\{([a-z]+)\}/g, (_, name) => (f[name] ?? ''))
        .replace(/\s+([,.;:])/g, '$1')
        .replace(/([,.;:])(?=[,.;:])/g, '')
        .replace(/\s{2,}/g, ' ')
        .trim();
}
function samePoint(a, b) {
    return !!a && !!b && a.s === b.s && a.i === b.i;
}
function pick(c, x, y) {
    const pts = c.points;
    if (!pts.length) return null;
    if (c.pie) {
        const { cx, cy, r, r0 } = c.pie;
        const d = Math.hypot(x - cx, y - cy);
        if (d > r + 4 || d < r0 - 2) return null;
        let a = Math.atan2(y - cy, x - cx);
        if (a < -Math.PI / 2) a += Math.PI * 2;
        return pts.find(p => a >= p.slice.a0 && a < p.slice.a1) || null;
    }
    const L = c.layout;
    if (!L) return null;
    const { plot } = L;
    if (x < plot.x0 - 24 || x > plot.x1 + 24 || y < plot.y0 - 24 || y > plot.y1 + 24) return null;
    if (c.type === 'scatter') {
        let best = null, bestD = 32;
        for (const p of pts) {
            const d = Math.hypot(p.x - x, p.y - y);
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }
    if (c.type === 'column' || c.type === 'bar') {
        const hit = pts.find(p => x >= p.box.x - 1 && x <= p.box.x + p.box.w + 1 && y >= p.box.y - 1 && y <= p.box.y + p.box.h + 1);
        if (hit) return hit;
    }
    const along = L.across ? x : y;
    let j = -1, jd = Infinity;
    for (const p of pts) {
        const d = Math.abs((L.across ? p.x : p.y) - along);
        const centre = Math.abs(L.catAt(p.i) - along);
        const dd = c.type === 'column' || c.type === 'bar' ? centre : d;
        if (dd < jd) { jd = dd; j = p.i; }
    }
    if (j < 0 || jd > Math.max(L.band, 24)) return null;
    let best = null, bestD = Infinity;
    for (const p of pts) {
        if (p.i !== j) continue;
        const d = L.across ? Math.abs((p.box ? p.box.y + p.box.h / 2 : p.y) - y) : Math.abs((p.box ? p.box.x + p.box.w / 2 : p.x) - x);
        if (d < bestD) { bestD = d; best = p; }
    }
    return best;
}
function drawActive(c) {
    const layer = c.focusLayer, under = c.underLayer;
    if (!layer || !under) return;
    layer.replaceChildren();
    under.replaceChildren();
    const p = c.active;
    c.el.toggleAttribute('data-chart-active', !!p);
    if (!p) return;
    const L = c.layout;
    if (p.slice) {
        node('path', { d: slicePath(c.pie.cx, c.pie.cy, p.slice.r0, p.slice.r + 3, p.slice.a0, p.slice.a1), class: 'chart-ring' }, layer);
        return;
    }
    if (L && (c.type === 'line' || c.type === 'area')) {
        node('line', { x1: round(p.x), x2: round(p.x), y1: L.plot.y0, y2: L.plot.y1, class: 'chart-guide' }, under);
    } else if (L && (c.type === 'column' || c.type === 'bar')) {
        const at = L.catAt(p.i);
        const half = L.band / 2;
        const band = L.across
            ? { x: at - half, y: L.plot.y0, width: L.band, height: L.plot.y1 - L.plot.y0 }
            : { x: L.plot.x0, y: at - half, width: L.plot.x1 - L.plot.x0, height: L.band };
        node('rect', { x: round(band.x), y: round(band.y), width: round(band.width), height: round(band.height), class: 'chart-band' }, under);
    }
    if (p.box) {
        node('rect', { x: round(p.box.x - 2), y: round(p.box.y - 2), width: round(p.box.w + 4), height: round(p.box.h + 4), rx: 3, class: 'chart-ring' }, layer);
        return;
    }
    node('path', { d: markerPath(p.shape, p.x, p.y, 4.5), class: 'chart-mark chart-marker chart-marker--active ' + toneClass(p.tone) }, layer);
    node('circle', { cx: round(p.x), cy: round(p.y), r: 8.5, class: 'chart-ring' }, layer);
}
function fillSlots(root, f, outside) {
    const all = (sel) => (root.matches(sel) ? [root] : []).concat(Array.from(root.querySelectorAll(sel)));
    for (const slot of all('[data-chart-field]')) {
        if (outside && slot.closest('[data-chart-row]')) continue;
        const value = f[slot.getAttribute('data-chart-field')] ?? '';
        slot.textContent = value;
        slot.hidden = value === '';
    }
    for (const dep of all('[data-chart-if]')) {
        if (outside && dep.closest('[data-chart-row]')) continue;
        dep.hidden = !(f[dep.getAttribute('data-chart-if')] ?? '');
    }
}
function tipOf(c) {
    return c.el.querySelector('[data-chart-tip]');
}
function showTip(c, p) {
    const tip = tipOf(c);
    if (!tip) {
        const f = fields(c, p);
        const said = c.data.series.length > 1 && !c.pie ? `${f.series}: ${f.value}` : f.value;
        const words = [f.category, said].filter(Boolean).join(' — ');
        const host = c.svg.getBoundingClientRect();
        const r = p.box
            ? { left: host.left + p.box.x, top: host.top + p.box.y, width: p.box.w, height: p.box.h }
            : { left: host.left + p.x - 6, top: host.top + p.y - 6, width: 12, height: 12 };
        r.right = r.left + r.width;
        r.bottom = r.top + r.height;
        if (words && window.sednaUi && window.sednaUi.tips && window.sednaUi.tips.at) window.sednaUi.tips.at(r, words, 'top');
        return;
    }
    const f = fields(c, p);
    for (const old of tip.querySelectorAll('[data-chart-row]')) old.remove();
    const row = tip.querySelector('template[data-chart-tip-row]');
    if (row && !c.pie) {
        let after = row;
        for (const { s, i } of shownSeries(c)) {
            const point = c.points.find(q => q.s === i && q.i === p.i);
            if (!point) continue;
            const frag = row.content.cloneNode(true);
            const made = Array.from(frag.children);
            for (const el of made) {
                el.setAttribute('data-chart-row', '');
                el.setAttribute('data-chart-tone', point.tone || s.tone);
                el.toggleAttribute('data-chart-current', i === p.s);
                fillSlots(el, fields(c, point), false);
            }
            row.parentNode.insertBefore(frag, after.nextSibling);
            if (made.length) after = made[made.length - 1];
        }
        fillSlots(tip, f, true);
    } else {
        fillSlots(tip, f, false);
    }
    tip.setAttribute('data-chart-tone', p.tone || '1');
    placeTip(c, tip, p);
}
function placeTip(c, tip, p) {
    const stage = c.el.getBoundingClientRect();
    const host = c.svg.getBoundingClientRect();
    tip.hidden = false;
    tip.style.transform = 'translate(0px, 0px)';
    const t = tip.getBoundingClientRect();
    const ox = host.left - stage.left, oy = host.top - stage.top;
    const px = ox + p.x, py = oy + p.y;
    const gap = 12;
    let x = c.rtl ? px - gap - t.width : px + gap;
    if (x + t.width > stage.width - 4 || x < 4) x = c.rtl ? px + gap : px - gap - t.width;
    let y = py - t.height - gap;
    if (y < 4) y = py + gap;
    x = Math.max(4, Math.min(x, stage.width - t.width - 4));
    y = Math.max(4, Math.min(y, Math.max(4, stage.height - t.height - 4)));
    tip.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
}
function hideTip(c) {
    const tip = tipOf(c);
    if (tip) tip.hidden = true;
    else if (window.sednaUi && window.sednaUi.tips && window.sednaUi.tips.hide) window.sednaUi.tips.hide();
}
function liveOf(c) {
    const inside = c.el.querySelector('[data-chart-live]');
    if (inside) return inside;
    const frame = c.el.closest('[data-chart-frame]');
    return frame ? frame.querySelector('[data-chart-live]') : null;
}
function announce(c, p, which) {
    const region = liveOf(c);
    if (!region || !p) return;
    const template = region.getAttribute(which === 'select' ? 'data-chart-announce-select' : 'data-chart-announce')
        ?? (which === 'select' ? '' : '{category} {series} {value}');
    if (!template) return;
    const words = fillTemplate(template, fields(c, p));
    region.textContent = '';
    clearTimeout(c.saying);
    c.saying = setTimeout(() => { region.textContent = words; }, 30);
}
function inOrder(c, series) {
    const pts = c.points.filter(p => p.s === series);
    if (c.type === 'scatter') pts.sort((a, b) => (c.data.categories[a.i].x - c.data.categories[b.i].x) || a.i - b.i);
    return pts;
}
function firstPoint(c) {
    if (c.pie) return c.points[0] || null;
    const shown = shownSeries(c);
    for (const { i } of shown) {
        const pts = inOrder(c, i);
        if (pts.length) return pts[0];
    }
    return null;
}
function along(c, from, step) {
    if (c.pie) {
        const at = c.points.findIndex(p => samePoint(p, from));
        return c.points[(at + step + c.points.length) % c.points.length] || null;
    }
    const pts = inOrder(c, from.s);
    const at = pts.findIndex(p => samePoint(p, from));
    return pts[at + step] || null;
}
function across(c, from, step) {
    if (c.pie) return along(c, from, step);
    const shown = shownSeries(c).map(x => x.i);
    let k = shown.indexOf(from.s);
    for (k += step; k >= 0 && k < shown.length; k += step) {
        const hit = c.points.find(p => p.s === shown[k] && p.i === from.i);
        if (hit) return hit;
    }
    return null;
}
function ends(c, from, last) {
    if (c.pie) return c.points[last ? c.points.length - 1 : 0] || null;
    const pts = inOrder(c, from ? from.s : (firstPoint(c) || {}).s);
    return pts[last ? pts.length - 1 : 0] || null;
}
function onKey(c, e) {
    if (e.target !== c.el || e.altKey || e.ctrlKey || e.metaKey) return;
    c.el.removeAttribute('data-chart-pointer');
    const from = c.active;
    const lying = c.type === 'bar';
    const forward = c.rtl ? -1 : 1;
    let next = undefined;
    if (c.pie && /^Arrow/.test(e.key)) {
        next = !from ? firstPoint(c) : along(c, from, e.key === 'ArrowRight' || e.key === 'ArrowDown' ? 1 : -1);
        e.preventDefault();
        if (next) activate(c, next, true);
        return;
    }
    switch (e.key) {
        case 'ArrowRight':
            next = !from ? firstPoint(c) : lying ? across(c, from, forward) : along(c, from, forward);
            break;
        case 'ArrowLeft':
            next = !from ? firstPoint(c) : lying ? across(c, from, -forward) : along(c, from, -forward);
            break;
        case 'ArrowDown':
            next = !from ? firstPoint(c) : lying ? along(c, from, 1) : across(c, from, -1);
            break;
        case 'ArrowUp':
            next = !from ? firstPoint(c) : lying ? along(c, from, -1) : across(c, from, 1);
            break;
        case 'Home':
            next = ends(c, from, false);
            break;
        case 'End':
            next = ends(c, from, true);
            break;
        case 'Enter': case ' ': case 'Spacebar':
            if (!from) next = firstPoint(c);
            else { choose(c, from); e.preventDefault(); return; }
            break;
        case 'Escape':
            if (!from) return;
            activate(c, null);
            e.preventDefault();
            return;
        default:
            return;
    }
    e.preventDefault();
    if (next) activate(c, next, true);
}
function choose(c, p) {
    const f = fields(c, p);
    const cell = c.data.series[p.s] && c.data.series[p.s].values[p.i];
    c.el.dispatchEvent(new CustomEvent('sedna-chart-select', {
        bubbles: true,
        detail: {
            chart: c.el.id || '',
            series: f.series,
            category: f.category,
            value: cell && cell.v !== null ? cell.v : null,
            text: f.value,
            index: p.i,
            seriesIndex: p.s,
        },
    }));
    announce(c, p, 'select');
}
const charts = new Set();
const OPTIONS = ['data-chart', 'data-chart-json', 'data-chart-stack', 'data-chart-min', 'data-chart-max', 'data-chart-format', 'dir', 'lang'];
function plotOf(el) {
    const plot = el.querySelector('.chart-plot');
    if (plot) return plot;
    warnOnce('plot', 'a [data-chart] has no .chart-plot to draw into; it draws into the element itself.');
    return el;
}
function hasValues(data) {
    return data.categories.length > 0 && data.series.some(s => s.values.some(v => v.v !== null));
}
function render(c) {
    c.queued = false;
    c.type = typeOf(c.el);
    c.format = formatter(c.el);
    c.data = readData(c);
    c.rtl = getComputedStyle(c.el).direction === 'rtl';
    c.el.setAttribute('data-chart-state', hasValues(c.data) ? 'ready' : 'empty');
    const box = c.plot.getBoundingClientRect();
    c.size = { w: Math.round(box.width), h: Math.round(box.height) };
    const svg = c.svg;
    if (c.drawn) svg.removeAttribute('data-chart-enter');
    svg.replaceChildren();
    c.points = [];
    c.pie = null;
    c.layout = null;
    svg.setAttribute('viewBox', `0 0 ${Math.max(1, c.size.w)} ${Math.max(1, c.size.h)}`);
    if (c.size.w < 16 || c.size.h < 16 || !hasValues(c.data)) {
        c.focusLayer = c.underLayer = null;
        setActive(c, null);
        return;
    }
    c.measure = measure(svg);
    c.underLayer = node('g', { class: 'chart-under' }, svg);
    const body = node('g', null, svg);
    if (c.type === 'pie' || c.type === 'donut') drawPie(c, body);
    else drawCartesian(c, body);
    const marks = body.querySelector('.chart-marks');
    if (marks) body.insertBefore(c.underLayer, marks);
    c.focusLayer = node('g', { class: 'chart-focus' }, svg);
    c.drawn = true;
    const was = c.active;
    c.active = null;
    if (was) setActive(c, c.points.find(p => samePoint(p, was)) || null, c.keyed, true);
}
function schedule(c) {
    if (c.queued) return;
    c.queued = true;
    setTimeout(() => { if (c.queued && charts.has(c)) render(c); }, 0);
}
function setActive(c, p, keyed, quiet) {
    const same = samePoint(c.active, p);
    c.active = p || null;
    c.keyed = !!(p && keyed);
    drawActive(c);
    if (!p) { hideTip(c); return; }
    showTip(c, p);
    if (keyed && !quiet && !same) announce(c, p, 'focus');
}
function activate(c, p, keyed) { setActive(c, p, keyed, false); }
function pointerAt(c, e) {
    const r = c.svg.getBoundingClientRect();
    return pick(c, e.clientX - r.left, e.clientY - r.top);
}
const sizes = new ResizeObserver(entries => {
    for (const e of entries) {
        const c = e.target.__sednaChartOf;
        if (!c) continue;
        const box = e.contentRect;
        if (c.size && Math.round(box.width) === c.size.w && Math.round(box.height) === c.size.h) continue;
        render(c);
    }
});
let watchingRoot = false;
function watchTheRoot() {
    if (watchingRoot) return;
    watchingRoot = true;
    new MutationObserver(() => { for (const c of charts) schedule(c); })
        .observe(document.documentElement, { attributes: true, attributeFilter: ['dir', 'lang'] });
}
function attach(el) {
    if (el.__sednaChart) return el.__sednaChart.handle;
    watchTheRoot();
    const plot = plotOf(el);
    const svg = node('svg', { class: 'chart-svg', 'aria-hidden': 'true', focusable: 'false', 'data-chart-enter': '' });
    plot.appendChild(svg);
    const c = {
        el, plot, svg,
        type: 'line', data: { categories: [], series: [] }, given: null,
        hidden: new Set(), points: [], active: null, keyed: false,
        size: null, drawn: false, queued: false, saying: 0,
    };
    el.__sednaChart = c;
    plot.__sednaChartOf = c;
    charts.add(c);
    c.watch = new MutationObserver(records => {
        for (const r of records) {
            const t = r.target.nodeType === 1 ? r.target : r.target.parentElement;
            if (!t) continue;
            if (t === el) { if (r.type === 'attributes' || Array.from(r.addedNodes).concat(Array.from(r.removedNodes)).some(n => n !== svg)) { schedule(c); return; } continue; }
            if (t.closest('table[data-chart-data]')) { schedule(c); return; }
        }
    });
    c.watch.observe(el, { attributes: true, attributeFilter: OPTIONS, childList: true, subtree: true, characterData: true });
    c.listeners = {
        move: e => {
            if (e.pointerType === 'touch' && e.type === 'pointermove' && !c.active) return;
            const p = pointerAt(c, e);
            if (!samePoint(p, c.active)) setActive(c, p, false);
        },
        leave: e => { if (e.pointerType !== 'touch' && !c.keyed) setActive(c, null); },
        down: e => {
            el.setAttribute('data-chart-pointer', '');
            const p = pointerAt(c, e);
            if (p && !samePoint(p, c.active)) setActive(c, p, false);
        },
        click: e => {
            const p = pointerAt(c, e);
            if (p) choose(c, p);
        },
        key: e => onKey(c, e),
        focus: () => {
            if (el.hasAttribute('data-chart-pointer') || c.active) return;
            const first = firstPoint(c);
            if (first) activate(c, first, true);
        },
        blur: () => {
            el.removeAttribute('data-chart-pointer');
            setActive(c, null);
        },
    };
    svg.addEventListener('pointermove', c.listeners.move);
    svg.addEventListener('pointerdown', c.listeners.down);
    svg.addEventListener('pointerleave', c.listeners.leave);
    svg.addEventListener('click', c.listeners.click);
    el.addEventListener('keydown', c.listeners.key);
    el.addEventListener('focus', c.listeners.focus);
    el.addEventListener('blur', c.listeners.blur);
    c.handle = {
        element: el,
        get type() { return c.type; },
        get data() {
            return {
                categories: c.data.categories.map(cat => cat.label),
                series: c.data.series.map(s => ({ name: s.name, tone: s.tone, hidden: c.hidden.has(s.name), values: s.values.map(v => v.v) })),
            };
        },
        set(data) { c.given = data || null; render(c); return c.handle; },
        reset() { c.given = null; render(c); return c.handle; },
        toggle(name, shown) {
            const key = String(name);
            const on = shown === undefined ? c.hidden.has(key) : !!shown;
            if (on) c.hidden.delete(key); else c.hidden.add(key);
            render(c);
            return on;
        },
        redraw() { render(c); return c.handle; },
    };
    render(c);
    sizes.observe(plot);
    return c.handle;
}
function sweep() {
    for (const c of charts) {
        if (c.el.isConnected) continue;
        charts.delete(c);
        sizes.unobserve(c.plot);
        c.watch.disconnect();
        clearTimeout(c.saying);
        c.svg.removeEventListener('pointermove', c.listeners.move);
        c.svg.removeEventListener('pointerdown', c.listeners.down);
        c.svg.removeEventListener('pointerleave', c.listeners.leave);
        c.svg.removeEventListener('click', c.listeners.click);
        c.el.removeEventListener('keydown', c.listeners.key);
        c.el.removeEventListener('focus', c.listeners.focus);
        c.el.removeEventListener('blur', c.listeners.blur);
        c.svg.remove();
        delete c.plot.__sednaChartOf;
        delete c.el.__sednaChart;
    }
}
export { attach, sweep };

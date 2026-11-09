/* ── Marks ────────────────────────────────────────────────────────────────────
   What each type draws, and the points the reader moves between. Every point is
   recorded in `c.points` — its series, its category, where it is on the plot and the
   box it fills — so the pointer, the keyboard and the tooltip all read one list.

     line      a line per series, with a marker at every value; a gap where a value is missing
     area      a line per series over its filled area; stacked with data-chart-stack
     column    a bar per value, side by side in its category; stacked with data-chart-stack
     bar       the same, lying down: categories run down the side
     scatter   a marker per value, at the category's own number across and the value up
     pie       one series, a slice per category; donut is the same with a hole

   A single series in a column or a bar chart may colour a category on its own:
   `data-tone` on the row.
   ─────────────────────────────────────────────────────────────────────────── */

/* A marker on every value while there is room for it; past this many in a series,
   only the point the reader is on is marked. */
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

    // Lines, areas, scatter: a point per value.
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
            // A stack has no gap: a missing value adds nothing to the layer below.
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
            // A path of runs; a missing value breaks the line rather than inventing one.
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
        // The whole circle: two half arcs, since one arc cannot end where it starts.
        const mid = a0 + Math.PI;
        const outer = `M${pt(r, a0)} A${round(r)} ${round(r)} 0 1 1 ${pt(r, mid)} A${round(r)} ${round(r)} 0 1 1 ${pt(r, a0)} Z`;
        if (!r0) return outer;
        return outer + ` M${pt(r0, a0)} A${round(r0)} ${round(r0)} 0 1 0 ${pt(r0, mid)} A${round(r0)} ${round(r0)} 0 1 0 ${pt(r0, a0)} Z`;
    }
    const large = a1 - a0 > Math.PI ? 1 : 0;
    if (!r0) return `M${round(cx)} ${round(cy)} L${pt(r, a0)} A${round(r)} ${round(r)} 0 ${large} 1 ${pt(r, a1)} Z`;
    return `M${pt(r, a0)} A${round(r)} ${round(r)} 0 ${large} 1 ${pt(r, a1)} L${pt(r0, a1)} A${round(r0)} ${round(r0)} 0 ${large} 0 ${pt(r0, a0)} Z`;
}

/* ── The point the reader is on ───────────────────────────────────────────────
   One point at a time is active: the one under the pointer, or the one the keyboard
   moved to. It is ringed, its category is marked — a guide line through a line or an
   area, a band behind a column or a bar — and the tooltip says what it is. The ring is
   drawn in a layer of its own, so moving it never redraws the chart.

   `fields` is everything the app's words can name, for the tooltip's slots and the
   announcements alike:

     {series}    the series' name, its column heading
     {category}  the category's label, its row heading
     {value}     the value as the app wrote it — the cell's text
     {number}    the bare number
     {percent}   its share of the category's total — of the whole, in a pie
     {total}     the category's total, in the format
     {index}     which category, from 1;  {count}  how many there are
   ─────────────────────────────────────────────────────────────────────────── */

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

/* The point under the pointer, by what is drawn there. */
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
    // The nearest category along the axis, then the nearest of its points across it.
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
        // Behind the bars, in the layer drawn before them.
        node('rect', { x: round(band.x), y: round(band.y), width: round(band.width), height: round(band.height), class: 'chart-band' }, under);
    }
    if (p.box) {
        node('rect', { x: round(p.box.x - 2), y: round(p.box.y - 2), width: round(p.box.w + 4), height: round(p.box.h + 4), rx: 3, class: 'chart-ring' }, layer);
        return;
    }
    node('path', { d: markerPath(p.shape, p.x, p.y, 4.5), class: 'chart-mark chart-marker chart-marker--active ' + toneClass(p.tone) }, layer);
    node('circle', { cx: round(p.x), cy: round(p.y), r: 8.5, class: 'chart-ring' }, layer);
}

/* ── Axes ─────────────────────────────────────────────────────────────────────
   Every chart but a pie sits on two axes: one of categories (or, for a scatter, of
   numbers) and one of values. A column, a line and an area run their categories
   across, a bar runs them down. The value axis's numbers sit at the start of the
   line, its gridlines run across the plot, and zero gets a stronger line of its own.

   Right to left, the categories run from the right and the value axis's numbers sit on
   the right — the first category is where a reader of that language starts. A bar
   grows from the right. The value axis itself still rises upwards: up is more in every
   script.

   Labels are thinned to every second, third… when they would collide, and cut with an
   ellipsis when a single one has no room; every one is whole in the table.
   ─────────────────────────────────────────────────────────────────────────── */

/* Visible series, with their index in the data, in order. */
function shownSeries(c) {
    const out = [];
    c.data.series.forEach((s, i) => { if (!c.hidden.has(s.name)) out.push({ s, i }); });
    return out;
}

/* The lowest and highest the value axis has to show. */
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

/* Where everything goes: the plot's rectangle, the value scale and the category
   positions, worked out from the room there is and the width of the labels. */
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
        // A scatter's last number, or a line's last category, needs half its width past the plot.
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
        // The number at the far end needs half its width inside the box.
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

    // Zero, where the values cross it; else the axis the values stand on.
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
            // The first and the last label of a line keep inside the box.
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

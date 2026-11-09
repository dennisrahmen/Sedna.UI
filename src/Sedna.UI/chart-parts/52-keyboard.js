/* ── The keyboard, and what a screen reader hears ─────────────────────────────
   The chart is one stop in the tab order, and inside it the keys move between points:

     ← →            the previous / next category — across the chart, as it is drawn,
                    so right is the next one in a left-to-right page and the previous
                    one in a right-to-left page
     ↑ ↓            the next / previous series at the same category
                    (a bar chart lies down, so there ↑ ↓ move between categories and
                    ← → between series)
     Home / End     the first / last category
     Enter, Space   choose the point: `sedna-chart-select`
     Escape         let go of the point

   A pie has one series, so every arrow walks its slices.

   Every move is said through the app's live region, in the app's words:

     <p class="visually-hidden" data-chart-live aria-live="polite"
        data-chart-announce="{category}, {series}: {value}."
        data-chart-announce-select="{category} chosen."></p>

   The region is the one inside the chart, else the one inside the `[data-chart-frame]`
   around it. With none, the ring still moves and the tooltip still says the point.

   The chart's element gets `tabindex="0"` and `role="application"` unless the app wrote
   its own. Its name — `aria-label` — and `aria-roledescription`, if the app wants the
   word "chart" said, are the app's to write. The table inside it is the text
   alternative a screen reader can read as a table.
   ─────────────────────────────────────────────────────────────────────────── */

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
    // Cleared first, so the same point said twice is still a change.
    region.textContent = '';
    clearTimeout(c.saying);
    c.saying = setTimeout(() => { region.textContent = words; }, 30);
}

/* The points of one series in the order a reader meets them: by category, or for a
   scatter by its number across. */
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
    // A pie is one ring of slices: down and right are clockwise, up and left back.
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

/* A point chosen — by Enter, Space or a click. The app decides what that means. */
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

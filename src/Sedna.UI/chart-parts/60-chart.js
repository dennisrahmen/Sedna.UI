/* ── A chart ──────────────────────────────────────────────────────────────────
   `attach` starts one on the app's element: an `<svg>` inside its `.chart-plot`, sized
   to it, drawn from its data. From there what changes a chart is watched, never polled:

     * its data — the table inside it, or its `data-chart-json`, changed by a render:
       read again and drawn again, the point the reader was on kept where it still exists;
     * its options — `data-chart`, `data-chart-stack`, `data-chart-min`,
       `data-chart-max`, `data-chart-format` — and its direction and language;
     * its size — a ResizeObserver on the plot, so the chart follows its container;
     * a series switched off or on by a legend button (`data-chart-toggle`, in the
       front door) — redrawn without it, the axis fitted to what is left.

   A change of theme needs nothing: the marks are coloured by their `.series-*`
   classes, and the browser repaints them itself.

   The first drawing fades in, once, unless the reader asked for less motion — the
   stylesheet decides, from `data-chart-enter` on the svg. Nothing after it moves.

   `sweep` lets go of every chart whose element has left the document.
   ─────────────────────────────────────────────────────────────────────────── */
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
    // The guide and the band go over the axes and under the marks.
    const marks = body.querySelector('.chart-marks');
    if (marks) body.insertBefore(c.underLayer, marks);
    c.focusLayer = node('g', { class: 'chart-focus' }, svg);
    c.drawn = true;
    // The point the reader was on, if it is still drawn.
    const was = c.active;
    c.active = null;
    if (was) setActive(c, c.points.find(p => samePoint(p, was)) || null, c.keyed, true);
}

function schedule(c) {
    if (c.queued) return;
    c.queued = true;
    // A timer, never an animation frame: a tab that is not painted gets no frames.
    setTimeout(() => { if (c.queued && charts.has(c)) render(c); }, 0);
}

/* Makes `p` the active point: ringed, its tooltip shown. `quiet` skips the announcement. */
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
    // The page's direction and language change the axes; nothing else on the root does.
    new MutationObserver(() => { for (const c of charts) schedule(c); })
        .observe(document.documentElement, { attributes: true, attributeFilter: ['dir', 'lang'] });
}

/** Starts the chart on an element the app wrote. Resolves its handle. */
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

    // The data, wherever the app re-renders it: the element's own attributes and
    // children, and everything inside the table — but never the svg, the tooltip or the
    // live region, which this module writes itself.
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
        /** The data drawn, as plain data: categories and series with their numbers. */
        get data() {
            return {
                categories: c.data.categories.map(cat => cat.label),
                series: c.data.series.map(s => ({ name: s.name, tone: s.tone, hidden: c.hidden.has(s.name), values: s.values.map(v => v.v) })),
            };
        },
        /** Draws this data instead of the markup's, until `reset()`. */
        set(data) { c.given = data || null; render(c); return c.handle; },
        /** Back to the data in the markup. */
        reset() { c.given = null; render(c); return c.handle; },
        /** Hides or shows a series by its name — a slice by its category in a pie. */
        toggle(name, shown) {
            const key = String(name);
            const on = shown === undefined ? c.hidden.has(key) : !!shown;
            if (on) c.hidden.delete(key); else c.hidden.add(key);
            render(c);
            return on;
        },
        /** Draws again now. */
        redraw() { render(c); return c.handle; },
    };

    render(c);
    sizes.observe(plot);
    return c.handle;
}

/** Lets go of every chart whose element is no longer in the document. */
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

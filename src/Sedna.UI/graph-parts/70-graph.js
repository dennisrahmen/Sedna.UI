/* ── A graph ──────────────────────────────────────────────────────────────────
   One `[data-graph]` element, from its markup to a drawing:

     1. its options, from its attributes (and from a call's, which win);
     2. its records, from its list, its URL or the call;
     3. an engine made without its canvas — cytoscape prepares every element it is
        handed for drawing, and most of a first filter's work is hiding elements, so
        the canvas is given only what is left, once the first layout has run;
     4. the controls read, the filter applied, the layout run, the canvas mounted and
        the view fitted — so the first drawing is already the one the reader asked for;
     5. `data-graph-state` on the element: loading, ready, empty, filtered or error.
        The stylesheet shows the app's `.graph-wait` and `.graph-empty` from it. A graph
        with `data-graph-deferred` stays loading until its records arrive by call.

   Events, bubbling from the element, each with plain data in `detail`:

     sedna-graph-ready     { nodes, edges, matches, totalNodes, totalEdges, selected }
     sedna-graph-change    the same, after a filter, a search, new data or a fold
     sedna-graph-select    the record selected, or { id: null }
     sedna-graph-open      the record opened — cancelable; else its data-href is followed
     sedna-graph-hover     the record pointed at or reached by keyboard
     sedna-graph-context   { id, x, y } — cancelable; else the app's [data-graph-menu] opens
     sedna-graph-connect   { source, target } — a link the reader drew
     sedna-graph-expand, sedna-graph-collapse   { id }

   A record's detail is { id, label, kind, group, cluster, parent, meta, href, tone,
   tags, fields, keyboard }.
   ─────────────────────────────────────────────────────────────────────────── */

const graphs = new Map();

function readOptions(el, given) {
    const d = el.dataset;
    const has = name => el.hasAttribute('data-graph-' + name) && el.getAttribute('data-graph-' + name) !== 'false';
    const o = {
        layout: d.graphLayout || 'force',
        direction: d.graphDirection || 'TB',
        spacing: d.graphSpacing || 'normal',
        nodes: d.graphNodes === 'box' ? 'box' : 'dot',
        labels: d.graphLabels || 'auto',
        edgeLabels: d.graphEdgeLabels || null,
        curve: d.graphCurve || null,
        arrows: d.graphArrows || 'none',
        colourBy: d.graphColour || d.graphColor || 'tone',
        open: d.graphOpen || 'dbltap',
        wheel: d.graphWheel || 'focus',
        hover: d.graphHover || 'light',
        select: d.graphSelect || 'single',
        drag: d.graphDrag !== 'false',
        renderer: d.graphRenderer === 'webgl' ? 'webgl' : 'canvas',
        src: d.graphSrc || null,
        focus: d.graphFocus || null,
        depth: Number(d.graphFocusDepth) || 1,
        managed: has('managed'),
        deferred: has('deferred'),
        connect: has('connect'),
        collapse: has('collapse'),
        hulls: has('hulls'),
    };
    Object.assign(o, given || {});
    if (!knownLayout(o.layout)) {
        warnOnce('layout:' + o.layout, `"${o.layout}" is not a layout; using force.`);
        o.layout = 'force';
    }
    o.direction = DIRECTIONS[o.direction] || 'TB';
    // Chosen, or following the drawing: boxes write their links' labels, dots only when lit.
    o.edgeLabelsChosen = !!o.edgeLabels;
    followEdgeLabels(o);
    o.curveChosen = !!o.curve;
    o.curve = o.curve || 'bezier';
    return o;
}

/* The options a reader can change, as they were when the graph was first drawn — what
   Reset puts back. */
const VIEW_OPTIONS = ['layout', 'direction', 'spacing', 'nodes', 'labels', 'edgeLabels', 'edgeLabelsChosen', 'colourBy', 'curve', 'arrows'];
const viewOf = o => Object.fromEntries(VIEW_OPTIONS.map(k => [k, o[k]]));

function followEdgeLabels(o) {
    if (!o.edgeLabelsChosen) o.edgeLabels = o.nodes === 'box' ? 'always' : 'hover';
}

// The view options a control that is already on at the start asks for — a checked
// layout radio wins over nothing, never over the element's own attribute.
function optionsFromControls(g) {
    for (const c of controlsOf(g)) {
        if (!c.hasAttribute('data-graph-option')) continue;
        const name = c.getAttribute('data-graph-option');
        const on = c.tagName === 'SELECT' ? true : (c.type === 'radio' || c.type === 'checkbox') ? c.checked : false;
        if (!on) continue;
        const key = { layout: 'layout', direction: 'direction', spacing: 'spacing', labels: 'labels', colour: 'colourBy',
            color: 'colourBy', 'edge-labels': 'edgeLabels', curve: 'curve', nodes: 'nodes', arrows: 'arrows' }[name];
        const attr = 'data-graph-' + (name === 'colour' || name === 'color' ? 'colour' : name);
        if (key && !g.el.hasAttribute(attr) && !(key in (g.given || {}))) {
            g.options[key] = key === 'nodes' ? (c.value === 'box' ? 'box' : 'dot') : c.value;
            if (key === 'edgeLabels') g.options.edgeLabelsChosen = true;
        }
        if (name === 'depth') g.depth = Math.max(1, Math.min(Number(c.value) || 1, 6));
    }
    followEdgeLabels(g.options);
}

let boxCtx = null;
function boxMetrics(node, font) {
    boxCtx = boxCtx || document.createElement('canvas').getContext('2d');
    const label = node.meta ? node.label + '\n' + node.meta : node.label;
    const iconRoom = node.icon ? 24 : 0;
    const maxText = 200;
    let widest = 0, lines = 0;
    for (const [i, line] of label.split('\n').entries()) {
        boxCtx.font = `${i === 0 && (node.hub || node.root) ? 600 : 400} 12.5px ${font}`;
        const w = boxCtx.measureText(line).width;
        widest = Math.max(widest, Math.min(w, maxText));
        lines += Math.max(1, Math.ceil(w / maxText));
    }
    return {
        boxLabel: label,
        boxW: Math.round(Math.min(Math.max(widest + 28 + iconRoom, 72), maxText + 28 + iconRoom)),
        boxH: Math.round(14 + lines * 17),
    };
}

/* The model as cytoscape elements, with what is worked out from the whole graph: each
   record's degree, its size, whether it heads a group, a box's measurements, and
   whether a link crosses between islands. */
function toElements(g) {
    const { nodes, edges } = g.model;
    const degree = new Map();
    edges.forEach(e => {
        degree.set(e.source, (degree.get(e.source) || 0) + 1);
        degree.set(e.target, (degree.get(e.target) || 0) + 1);
    });
    const groupNames = new Set(nodes.map(n => n.group).filter(Boolean));
    const groupSize = new Map();
    nodes.forEach(n => { if (n.group) groupSize.set(n.group, (groupSize.get(n.group) || 0) + 1); });
    const parents = new Set(nodes.map(n => n.parent).filter(Boolean));
    const byId = new Map(nodes.map(n => [n.id, n]));
    const font = g.colours ? g.colours.font() : 'system-ui, sans-serif';
    const out = [];
    for (const n of nodes) {
        const d = degree.get(n.id) || 0;
        const display = n.display || g.options.nodes;
        const data = Object.assign({}, n, {
            parent: n.parent || undefined,
            degree: d,
            // A group's own record also weighs what is filed under it, so it is its island's largest.
            size: sizeOf(n.weight ?? d + (groupSize.get(n.id) || 0) / 2),
            hub: n.hub || n.root || groupNames.has(n.id) || parents.has(n.id),
            display,
            zoom: g.step || 1,
        }, display === 'box' ? boxMetrics(n, font) : { boxLabel: n.label, boxW: null, boxH: null });
        if (!data.parent) delete data.parent;
        const el = { group: 'nodes', data };
        if (n.x !== null && n.y !== null) el.position = { x: n.x, y: n.y };
        out.push(el);
    }
    for (const e of edges) {
        const s = byId.get(e.source), t = byId.get(e.target);
        out.push({
            group: 'edges',
            data: Object.assign({}, e, { across: !!(s.group && t.group && s.group !== t.group), zoom: g.step || 1 }),
        });
    }
    return out;
}

function measureBoxes(g) {
    const font = g.colours.font();
    const byId = new Map(g.model.nodes.map(m => [m.id, m]));
    g.cy.batch(() => g.cy.nodes().forEach(n => {
        const model = byId.get(n.id());
        if (!model) return;
        const mode = model.display || g.options.nodes;
        n.data(Object.assign({ display: mode }, mode === 'box' ? boxMetrics(model, font) : { boxLabel: model.label }));
    }));
}

function nodeDetail(ele, extra) {
    const d = ele.data();
    const fields = {};
    for (const [k, v] of Object.entries(d.fields || {})) fields[k] = v === null || v === undefined ? null : String(v);
    return Object.assign({
        id: d.id,
        label: d.label ?? null,
        kind: d.kind ?? null,
        group: d.group ?? null,
        cluster: d.cluster ?? null,
        parent: d.parent ?? null,
        meta: d.meta ?? null,
        href: d.href ?? null,
        tone: d.tone ?? null,
        tags: d.tags || [],
        fields,
        keyboard: false,
    }, extra || {});
}

function restyle(g) {
    g.cy.style(styleFor(g.colours, g.icons, g.options, g.extraStyle));
    colouring(g);
}

// The colouring on show, for the stylesheet: an app's legend for each colouring can
// follow it with CSS alone — `[data-graph-colouring="status"]`.
function colouring(g) {
    g.el.setAttribute('data-graph-colouring', g.options.colourBy || 'tone');
}

function changed(g) {
    const s = stats(g);
    const empty = g.model.nodes.length === 0;
    // Still waiting for the records a call will bring: the wait stays up, not "empty".
    // Data that could not be read is the error state until it can.
    if (g.failed) g.el.setAttribute('data-graph-state', 'error');
    else if (!g.awaiting) g.el.setAttribute('data-graph-state', empty ? 'empty' : s.nodes === 0 ? 'filtered' : 'ready');
    writeStats(g, s);
    g.emit('sedna-graph-change', s);
    return s;
}

function select(g, ele) {
    const cy = g.cy;
    const next = ele && ele.nonempty() && !ele.hasClass('hidden') && g.options.select !== 'none' ? ele : null;
    const same = (!next && !g.selected) || (next && g.selected && g.selected.same(next));
    cy.batch(() => {
        cy.elements(':selected').unselect();
        if (next) next.select();
    });
    g.selected = next;
    light(g, resting(g));
    declutter(g);
    showDetail(g);
    if (!same) g.emit('sedna-graph-select', next ? nodeDetail(next) : { id: null });
    writeStats(g, stats(g));
}

/* `sedna-graph-open` first; the record's data-href only if nobody cancelled it — by
   clicking a real anchor, so Blazor's router and every other client-side router routes
   it instead of reloading the page. */
function open(g, ele, keyboard) {
    const detail = nodeDetail(ele, { keyboard: !!keyboard });
    if (!g.emit('sedna-graph-open', detail, true) || !detail.href) return;
    const a = document.createElement('a');
    a.href = detail.href;
    document.body.appendChild(a);
    a.click();
    a.remove();
}

/* Lays the drawing out again and, the first time, gives it its canvas. With `animate`,
   the records travel from where they were — except in a crowd, where a hundred moving
   dots read as noise and cost frames the reader wanted for something else. */
async function arrange(g, animate) {
    const cy = g.cy;
    const run = (g.arranging = (g.arranging || 0) + 1);
    const moving = cy.nodes().not('.hidden').filter(n => !n.isParent());
    const before = animate && cy.container() && moving.length <= 600 && !reducedMotion() && document.visibilityState === 'visible'
        ? new Map(moving.map(n => [n.id(), Object.assign({}, n.position())])) : null;
    unzoomed(g);
    // Mounted before the first layout, not after: cytoscape's springs and rings size their
    // box from the container, and a drawing made headless could never be arranged back to.
    // The filter has already hidden what will not be shown, so only that is prepared, and
    // the app's .graph-wait covers the canvas until the state leaves loading.
    mount(g);
    // A layout that makes room for names measures every name, not just the ones the
    // declutter is showing now: which those are depends on where the records were, and a
    // drawing measured with half its names would grow into itself once they came back.
    const quiet = cy.nodes('.unlabelled');
    if (quiet.nonempty()) cy.batch(() => quiet.removeClass('unlabelled'));
    try {
        await arrangeWith(cy, g.options.layout, g.options, g.el);
        await atScale(g, run);
    } finally {
        if (quiet.nonempty()) cy.batch(() => quiet.filter(n => n.inside()).addClass('unlabelled'));
    }
    if (run !== g.arranging || g.disposed) return;
    if (before) {
        const after = new Map(moving.map(n => [n.id(), Object.assign({}, n.position())]));
        if (!g.touched) fitView(g, null, true);
        cy.batch(() => moving.forEach(n => { if (before.has(n.id())) n.position(before.get(n.id())); }));
        moving.forEach(n => n.animate({ position: after.get(n.id()) }, { duration: 380, easing: 'ease-in-out-cubic' }));
        // While the records travel, a frame that changes size only resizes; it fits once
        // they have landed, to where they landed.
        g.travelling = true;
        setTimeout(() => {
            g.travelling = false;
            if (g.disposed) return;
            if (g.refit && !g.touched) fitView(g, null, false);
            g.refit = false;
            settle(g);
            g.minimap?.now();
            if (g.hullsOn) drawHulls(g);
        }, 420);
    } else {
        if (!g.touched) fitView(g, null, false);
        else settle(g);
        g.minimap?.now();
        if (g.hullsOn) drawHulls(g);
    }
}

/* A name around dots is held at its screen size, so a drawing seen zoomed out has larger
   names in it than the one the layout measured — and where the layout made room for names,
   in a group's box or a layered hierarchy, they grow into their neighbours. So there the
   layout runs again at the zoom the drawing will be seen at, until the two agree. */
async function atScale(g, run) {
    const cy = g.cy;
    if (g.options.nodes === 'box' || g.touched) return;
    const measures = g.options.layout === 'dagre' || cy.nodes(':parent').not('.hidden').nonempty();
    if (!measures) return;
    for (let pass = 0; pass < 3; pass++) {
        const shown = cy.nodes().not('.hidden');
        if (shown.empty()) return;
        const seen = stepOf(Math.min(1, fitOf(g, shown).zoom));
        if (seen === g.step) return;
        g.step = seen;
        cy.batch(() => cy.elements().data('zoom', seen));
        await arrangeWith(cy, g.options.layout, g.options, g.el);
        if (run !== g.arranging || g.disposed) return;
    }
}

/* A dot is held at a readable screen size by shrinking it in the drawing as the zoom grows,
   and a layout measures records — so it is handed their own size first, and the view puts
   the zoom back when it settles. Otherwise arranging zoomed in is a different drawing. */
function unzoomed(g) {
    if (g.step === 1) return;
    g.step = 1;
    g.cy.batch(() => g.cy.elements().data('zoom', 1));
}

function mount(g) {
    const cy = g.cy;
    if (cy.container()) return;
    // Which renderer is decided here, once the records are known — a deferred graph has
    // none when it is made. cytoscape reads its renderer hints from the options it was
    // made with when it mounts, so the hint is set on them; the vendored version is pinned.
    // WebGL is cytoscape's newer renderer: faster on a real GPU for many thousands of
    // elements, and still marked experimental upstream — so it is the app's choice,
    // `data-graph-renderer="webgl"`, never switched on by a count.
    const count = cy.elements().length;
    const webgl = g.options.renderer === 'webgl';
    try {
        cy._private.options.webgl = webgl;
        cy._private.options.textureOnViewport = !webgl && count > 1500;
        cy._private.options.hideEdgesOnViewport = !webgl && count > 5000;
    } catch (e) { /* an engine that moved its options: the canvas renderer, as made */ }
    cy.mount(g.host);
    // Where cytoscape registers an instance made with a container, which mounting does
    // not: devtools, and the browser tests, find the graph there.
    g.host._cyreg = Object.assign({}, g.host._cyreg, { cy });
    const canvas = g.el.querySelector('[data-graph-minimap]');
    if (canvas) g.minimap = minimap(g, canvas);
}

async function fetchData(url) {
    const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error(`data-graph-src "${url}" answered ${response.status}.`);
    return response.json();
}

function positionNear(g, id, i) {
    const cy = g.cy;
    const next = g.model.edges.filter(e => e.source === id || e.target === id).map(e => (e.source === id ? e.target : e.source));
    const placed = next.map(o => cy.getElementById(o)).filter(o => o.nonempty() && o.inside());
    if (placed.length) {
        const x = placed.reduce((s, o) => s + o.position('x'), 0) / placed.length;
        const y = placed.reduce((s, o) => s + o.position('y'), 0) / placed.length;
        return { x: x + 40 * Math.cos((i + 1) * golden), y: y + 40 * Math.sin((i + 1) * golden) };
    }
    const bb = cy.nodes().not('.hidden').boundingBox({ includeLabels: false });
    return { x: (Number.isFinite(bb.x2) ? bb.x2 : 0) + 60, y: (Number.isFinite(bb.y1) ? bb.y1 : 0) + 40 * i };
}

/* New data for a drawing that stands. What stayed keeps its place; what changed is
   restyled in place; what left goes; what arrived is put beside what it links to and
   the springs make room for it — so a link written while the page is open appears
   where the reader is looking, rather than the whole map rearranging under the pointer.
   A layout whose shape is the point — a hierarchy, a grid — runs again instead. */
async function setData(g, data, opts = {}) {
    await g.started;
    if (g.disposed) return;
    const cy = g.cy;
    const first = g.awaiting;
    if (first) {
        // The first records of a deferred graph: a first drawing, laid out whole, and
        // not animated — there is no earlier drawing for the records to travel from.
        g.awaiting = false;
        opts = Object.assign({}, opts, { relayout: true });
    }
    const folded = foldedIds(g);
    g.quietFolds = true;
    if (g.ec && folded.length) g.ec.expandAll({ animate: false, fisheye: false });
    g.model = normalise(data);
    const els = toElements(g);
    const wanted = new Map(els.map(e => [e.data.id, e]));
    const plugin = e => e.hasClass('eh-ghost') || e.hasClass('eh-handle') || e.hasClass('eh-preview') || e.hasClass('eh-ghost-edge');
    const removed = cy.elements().filter(e => !wanted.has(e.id()) && !plugin(e));
    const fresh = els.filter(e => cy.getElementById(e.data.id).empty());
    removed.remove();
    // New records first, placed beside what they link to — so an existing record can be
    // moved into a group that has only just arrived, and a new link has both its ends.
    let i = 0;
    const addedNodes = cy.add(fresh.filter(e => e.group === 'nodes')
        .map(e => Object.assign(e, { position: e.position || positionNear(g, e.data.id, i++) })));
    cy.batch(() => {
        els.forEach(e => {
            const ex = cy.getElementById(e.data.id);
            if (ex.empty() || addedNodes.contains(ex)) return;
            // id, source, target and parent are immutable as data: an end or a group that
            // changed is a move.
            // A move replaces the element, so the data goes on what it returns.
            const d = Object.assign({}, e.data);
            let target = ex;
            if (e.group === 'nodes') {
                if ((ex.data('parent') || null) !== (d.parent || null)) target = ex.move({ parent: d.parent || null });
                delete d.parent;
            } else {
                if (ex.data('source') !== d.source || ex.data('target') !== d.target) target = ex.move({ source: d.source, target: d.target });
                delete d.source;
                delete d.target;
            }
            delete d.id;
            target.data(d);
        });
    });
    const added = addedNodes.union(cy.add(fresh.filter(e => e.group === 'edges')));
    if (!reducedMotion()) {
        added.addClass('entering');
        setTimeout(() => added.removeClass('entering'), 30);
    }
    applyFilter(g, filterFromControls(g).spec);
    if (g.query) search(g, g.query, false);
    const shapeMatters = ['dagre', 'tree', 'grid', 'circle', 'concentric', 'fcose'].includes(g.options.layout);
    const many = addedNodes.length > Math.max(3, 0.25 * cy.nodes().length);
    if (opts.relayout || (!opts.quiet && (many || (shapeMatters && (addedNodes.nonempty() || removed.nodes().nonempty()))))) {
        await arrange(g, !first);
    } else if (addedNodes.nonempty() && g.options.layout !== 'preset') {
        // A record in a group cannot be laid out without its group, so a compound drawing
        // is laid out whole; the springs make room around new records everywhere else.
        const compound = addedNodes.some(n => n.isChild() || n.isParent());
        if (compound) {
            await arrange(g, true);
        } else {
            unzoomed(g);
            const around = addedNodes.union(addedNodes.neighborhood()).not('.hidden');
            const still = cy.nodes().not(around);
            still.lock();
            try {
                await settled(springs(around.union(around.edgesWith(around)), around.nodes().length, SPACING[g.options.spacing] || 1, { numIter: 300 }));
            } catch (e) {
                report(e);
                still.unlock();
                await arrange(g, true);
            } finally {
                still.unlock();
            }
            settle(g);
            g.minimap?.now();
        }
    } else {
        settle(g);
        g.minimap?.now();
    }
    const arriving = addedNodes.filter(n => n.isParent() && flag(n.data('fields')?.collapsed));
    if (arriving.nonempty() && !g.ec) await collapsible(g).catch(report);
    if (g.ec) {
        const again = cy.collection(folded.map(id => cy.getElementById(id)).filter(n => n.nonempty())).union(arriving);
        if (again.nonempty()) g.ec.collapse(again, { animate: false, fisheye: false });
    }
    g.quietFolds = false;
    if (g.hullsOn) drawHulls(g);
    if (g.selected && (g.selected.removed() || g.selected.hasClass('hidden'))) select(g, null);
    else showDetail(g);
    const s = changed(g);
    if (first) g.emit('sedna-graph-ready', s);
}

function repaint(g) {
    if (g.disposed || !g.cy) return;
    g.colours.clear();
    g.icons.clear();
    restyle(g);
    if (g.hullsOn) drawHulls(g);
    if (g.ec) {
        const p = palette(g.colours);
        g.ec.setOption('expandCueImage', g.icons.image('ri-add-box-line', p.fg, 28) || undefined);
        g.ec.setOption('collapseCueImage', g.icons.image('ri-checkbox-indeterminate-line', p.fg, 28) || undefined);
    }
    g.minimap?.now();
}

function wire(g) {
    const cy = g.cy;
    const el = g.el;
    const on = (target, type, fn, opts) => {
        target.addEventListener(type, fn, opts);
        g.listeners.push(() => target.removeEventListener(type, fn, opts));
    };

    // Names are placed again once the wheel rests, not while it turns: restyling every
    // record mid-gesture is what made zooming stutter. A timer rather than an animation
    // frame: a frame never comes for a tab that is not being painted.
    let pending = 0;
    cy.on('zoom', () => {
        clearTimeout(pending);
        pending = setTimeout(() => { pending = 0; if (!g.disposed) settle(g); }, 140);
    });
    g.listeners.push(() => clearTimeout(pending));

    let dragging = false, leaving = 0;
    cy.on('grab', 'node', () => { dragging = true; });
    cy.on('free', 'node', () => {
        dragging = false;
        declutter(g);
        if (g.hullsOn) g.bb?.update(true);
    });

    cy.on('mouseover', 'node', event => {
        if (dragging || g.drawing) return;
        const node = event.target;
        if (node.hasClass('eh-handle') || node.hasClass('eh-ghost')) return;
        clearTimeout(leaving);
        if (g.options.hover !== 'none') light(g, node);
        g.tip.show(node);
        g.host.style.cursor = 'pointer';
        g.emit('sedna-graph-hover', nodeDetail(node));
    });
    cy.on('mouseout', 'node', () => {
        g.tip.hide();
        g.host.style.cursor = '';
        clearTimeout(leaving);
        leaving = setTimeout(() => { if (!g.disposed) light(g, resting(g)); }, 90);
    });
    cy.on('mouseover', 'edge', event => {
        if (dragging || !event.target.data('label')) return;
        g.tip.show(event.target);
    });
    cy.on('mouseout', 'edge', () => g.tip.hide());
    // A pointer that leaves the canvas in one jump never passes a record's edge on the way
    // out, so the canvas's own edge hides the tooltip too.
    on(g.host, 'pointerleave', () => g.tip.hide());
    g.listeners.push(() => clearTimeout(leaving));

    cy.on('tap', 'node', event => {
        const node = event.target;
        if (node.hasClass('eh-handle') || node.hasClass('eh-ghost') || g.drawing) return;
        g.menu.close(false);
        if (g.options.open === 'tap') {
            open(g, node, false);
            return;
        }
        if (g.keyed) { g.keyed.removeClass('keyed'); g.keyed = null; }
        select(g, node);
    });
    cy.on('dbltap', 'node', event => {
        if (g.options.open !== 'none' && !g.drawing && !event.target.isParent()) open(g, event.target, false);
    });
    cy.on('tap', event => {
        if (event.target !== cy) return;
        g.menu.close(false);
        if (g.selected) select(g, null);
    });
    cy.on('cxttap', event => {
        const ele = event.target === cy || !event.target.isNode() ? null : event.target;
        context(g, ele, event.renderedPosition);
    });
    cy.on('taphold', 'node', event => {
        if (event.originalEvent && event.originalEvent.pointerType === 'mouse') return;
        context(g, event.target, event.renderedPosition);
    });

    // A graph inside a page that scrolls must not swallow the scroll: with "focus", the
    // wheel zooms once the reader has pressed on the canvas, until the pointer leaves it.
    if (g.options.wheel === 'focus') {
        on(g.host, 'pointerdown', () => cy.userZoomingEnabled(true));
        on(g.host, 'pointerleave', () => cy.userZoomingEnabled(false));
    }
    // Until the reader has moved the view themselves — dragged it, scrolled or pinched
    // it — the view follows the frame. A click on a record is not moving the view.
    cy.on('dragpan scrollzoom pinchzoom', () => { g.touched = true; });
    // A click on the canvas puts focus on the graph, so the keys carry on from there —
    // without the focus ring, which is for the keyboard. On the way up, not down: the
    // engine blurs whatever is focused when a press starts on its canvas.
    on(g.host, 'pointerdown', () => { g.pointerAt = Date.now(); });
    on(g.host, 'pointerup', () => { if (document.activeElement !== el) el.focus({ preventScroll: true }); });

    // A frame that changes size — a panel opening beside it, a window resized — keeps the
    // point the reader was looking at in the middle, rather than pinned to the top left.
    let was = null;
    const size = new ResizeObserver(() => {
        if (!cy.container() || g.disposed) return;
        const before = was || { w: cy.width(), h: cy.height() };
        cy.resize();
        was = { w: cy.width(), h: cy.height() };
        if (g.travelling) g.refit = true;
        else if (!g.touched) fitView(g, null, false);
        else {
            cy.panBy({ x: (was.w - before.w) / 2, y: (was.h - before.h) / 2 });
            settle(g);
        }
    });
    size.observe(g.host);
    g.observers.push(size);

    // The theme is the library's to change at any moment — variant, colour vision,
    // contrast — and the canvas is the one surface CSS cannot reach.
    let paint = 0;
    const soon = () => { clearTimeout(paint); paint = setTimeout(() => repaint(g), 16); };
    const theme = new MutationObserver(soon);
    theme.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme', 'data-variant', 'data-cvd', 'data-density', 'class', 'style'] });
    g.observers.push(theme);
    for (const q of ['(prefers-color-scheme: dark)', '(prefers-contrast: more)', '(forced-colors: active)']) {
        const mq = window.matchMedia(q);
        mq.addEventListener('change', soon);
        g.listeners.push(() => mq.removeEventListener('change', soon));
    }
    g.listeners.push(() => clearTimeout(paint));

    on(document, 'fullscreenchange', () => {
        const frame = el.closest('[data-graph-frame]') || el;
        const full = document.fullscreenElement === frame;
        controlsOf(g).filter(c => c.getAttribute('data-graph-action') === 'fullscreen')
            .forEach(c => c.setAttribute('aria-pressed', full ? 'true' : 'false'));
        setTimeout(() => {
            if (g.disposed) return;
            cy.resize();
            g.touched = false;
            fitView(g, null, false);
        }, 60);
    });

    // The markup is the data: a render that changes the list changes the drawing.
    const lists = readList(el);
    if (lists.length && !g.given.data && !g.options.src) {
        let queued = 0;
        const reread = new MutationObserver(() => {
            clearTimeout(queued);
            queued = setTimeout(() => {
                if (!g.disposed) setData(g, readMarkup(el) || { nodes: [], edges: [] }).catch(report);
            }, 40);
        });
        lists.forEach(list => reread.observe(list, { childList: true, subtree: true, attributes: true, characterData: true }));
        g.observers.push(reread);
        g.listeners.push(() => clearTimeout(queued));
    }
}

function report(e) {
    try { console.error('Sedna.UI graph:', e); } catch (x) { /* ignore */ }
}

async function start(g) {
    const el = g.el;
    const host = el.querySelector('[data-graph-canvas]') || el.querySelector('.graph-canvas');
    if (!host) {
        el.setAttribute('data-graph-state', 'error');
        throw new Error('a [data-graph] element needs a <div class="graph-canvas"> inside it to draw into.');
    }
    g.host = host;
    el.setAttribute('data-graph-state', 'loading');
    g.colours = colourReader(el);
    g.icons = iconPainter(el);
    optionsFromControls(g);
    g.startView = viewOf(g.options);
    colouring(g);
    g.focusId = g.options.focus;
    g.depth = g.depth || g.options.depth;
    g.startDepth = g.depth;

    // `data-graph-deferred`: the records arrive by call — graph.set, ISednaGraph.SetDataAsync —
    // and the wait stays up until they do, rather than an empty state flashing first.
    g.awaiting = g.options.deferred && !g.given.data;
    let raw = g.given.data || readMarkup(el);
    if (!raw && g.options.src) {
        try {
            raw = await fetchData(g.options.src);
        } catch (e) {
            g.failed = true;
            warnOnce('src:' + g.options.src, e.message);
        }
    }
    raw = raw || { nodes: [], edges: [] };
    g.model = normalise(raw);
    if (g.model.nodes.some(n => n.icon)) await Promise.race([g.icons.ready(), new Promise(r => setTimeout(r, 1500))]);
    if (g.disposed) return;

    const count = g.model.nodes.length + g.model.edges.length;
    g.cy = cytoscape({
        headless: true,
        styleEnabled: true,
        elements: toElements(g),
        style: styleFor(g.colours, g.icons, g.options, g.extraStyle),
        minZoom: 0.05,
        maxZoom: g.options.nodes === 'box' ? 4 : 8,
        boxSelectionEnabled: false,
        selectionType: 'single',
        autoungrabify: !g.options.drag,
        autounselectify: g.options.select === 'none',
        userZoomingEnabled: g.options.wheel === 'always',
        // A large graph: while the view moves, move a picture of it rather than drawing
        // every line again each frame, and leave the edges out entirely past that.
        textureOnViewport: count > 1500,
        hideEdgesOnViewport: count > 5000,
    });
    g.cy.scratch('_sedna', g);

    g.tip = tips(g);
    g.menu = menus(g);
    wire(g);
    g.keys = keyboard(g);
    rememberControls(g);
    applyFilter(g, filterFromControls(g).spec);
    const typed = controlsOf(g).find(c => c.hasAttribute('data-graph-search'));
    if (typed && typed.value) g.query = typed.value;

    await arrange(g, false);
    if (g.disposed) return;
    if (g.query) search(g, g.query, false);
    // The drawing is shown now; what a plugin adds arrives a moment later, rather than
    // holding the whole graph behind its wait while a plugin downloads.
    showDetail(g);
    changed(g);
    await plugins(g);
    if (g.disposed) return;
    if (!g.awaiting) g.emit('sedna-graph-ready', stats(g));
}

async function plugins(g) {
    const pressed = (action, on) => controlsOf(g).filter(c => c.getAttribute('data-graph-action') === action)
        .forEach(c => c.setAttribute('aria-pressed', on ? 'true' : 'false'));
    const jobs = [];
    if (g.options.collapse || g.model.nodes.some(n => n.fields.collapsed !== undefined)) {
        jobs.push(collapsible(g).then(ec => {
            const folded = g.cy.nodes(':parent').filter(n => flag(n.data('fields')?.collapsed));
            g.quietFolds = true;
            try {
                if (folded.nonempty()) ec.collapse(folded, { animate: false, fisheye: false });
            } finally {
                g.quietFolds = false;
            }
        }));
    }
    if (g.options.hulls) jobs.push(hulls(g, true).then(() => pressed('hulls', true)));
    if (g.options.connect) jobs.push(connect(g, true).then(() => pressed('connect', true)));
    const results = await Promise.allSettled(jobs);
    results.filter(r => r.status === 'rejected').forEach(r => report(r.reason));
    if (jobs.length) changed(g);
}

function dispose(g) {
    if (g.disposed) return;
    g.disposed = true;
    graphs.delete(g.el);
    clearTimeout(g.typing);
    clearTimeout(g.unfolding);
    g.listeners.splice(0).forEach(off => { try { off(); } catch (e) { /* ignore */ } });
    g.observers.splice(0).forEach(o => o.disconnect());
    g.keys?.destroy();
    g.menu?.destroy();
    g.minimap?.destroy();
    g.tip?.hide();
    try { g.eh?.destroy(); } catch (e) { /* ignore */ }
    try { g.bb?.destroy(); } catch (e) { /* ignore */ }
    try { g.cy?.destroy(); } catch (e) { /* ignore */ }
    g.colours?.remove();
    g.icons?.remove();
    g.el.removeAttribute('data-graph-state');
    g.el.removeAttribute('data-graph-colouring');
}

async function invoke(g, method, args) {
    const a = args || [];
    const api = g.api;
    switch (method) {
        case 'set': return api.set(a[0], a[1] || {});
        case 'filter': return api.filter(a[0]);
        case 'search': return api.search(a[0]);
        case 'select': api.select(a[0] || null); return stats(g);
        case 'focus': return api.focus(a[0] || null, a[1]);
        case 'layout': await api.layout(a[0], a[1] || {}); return stats(g);
        case 'fit': api.fit(); return null;
        case 'zoom': api.zoom(Number(a[0]) || 1); return null;
        case 'option': await api.option(a[0], a[1]); return stats(g);
        case 'export': {
            // Bytes, not a data: URL: .NET reads them as a stream, which no message-size
            // limit applies to — a PNG of a few records is already past a circuit's default.
            const blob = await api.export(a[0] === 'png' ? 'png' : 'svg');
            return new Uint8Array(await blob.arrayBuffer());
        }
        case 'download': await api.download(a[0] === 'png' ? 'png' : 'svg', a[1] || null); return null;
        case 'collapse': await api.collapse(a[0] || null); return stats(g);
        case 'expand': await api.expand(a[0] || null); return stats(g);
        case 'connect': await api.connect(a[0] !== false); return null;
        case 'hulls': await api.hulls(a[0] !== false); return null;
        case 'stats': return stats(g);
        case 'reload': return api.reload();
        default:
            warnOnce('invoke:' + method, `"${method}" is not a graph method.`);
            return null;
    }
}

function makeApi(g) {
    return {
        get element() { return g.el; },
        /* The engine itself. Its API is cytoscape's and is not versioned by Sedna.UI:
           reach for it for what this module does not offer, and expect to revisit it
           when the vendored cytoscape.js changes. */
        get cy() { return g.cy; },
        ready: () => g.started.then(() => stats(g)),
        set: (data, opts) => setData(g, data, opts).then(() => stats(g)),
        filter: async spec => {
            g.baseFilter = Object.assign({}, spec || {});
            g.hiddenIds.clear();
            // The focus is its own question: a filter that does not name one keeps it.
            if (spec && 'focus' in spec) g.focusId = spec.focus ? String(spec.focus) : null;
            if (spec && spec.depth) g.depth = Number(spec.depth) || g.depth;
            delete g.baseFilter.focus;
            delete g.baseFilter.depth;
            await refilter(g, true);
            return stats(g);
        },
        search: text => {
            search(g, text, true);
            changed(g);
            return stats(g);
        },
        select: id => {
            const e = id ? g.cy.getElementById(String(id)) : null;
            select(g, e && e.nonempty() ? e : null);
            if (e && e.nonempty() && !e.hasClass('hidden')) centreOn(g, e);
        },
        focus: async (id, depth) => {
            if (depth) g.depth = Math.max(1, Math.min(Number(depth) || 1, 6));
            await focusOn(g, id ? String(id) : null);
            return stats(g);
        },
        layout: async (name, opts) => {
            if (name) {
                if (!knownLayout(name)) return warnOnce('layout:' + name, `"${name}" is not a layout.`);
                g.options.layout = name;
            }
            for (const k of ['direction', 'spacing']) if (opts && opts[k]) g.options[k] = k === 'direction' ? (DIRECTIONS[opts[k]] || 'TB') : opts[k];
            g.touched = false;
            restyle(g);
            await arrange(g, true);
            changed(g);
        },
        fit: () => fitAll(g),
        zoom: factor => zoomBy(g, factor),
        option: (name, value) => option(g, name, value),
        /* The app's own engine rules, on top of the library's and kept across every
           repaint: [{ selector, style }], a colour written as 'var(--token)'. Replaces
           what an earlier call set. */
        style: rules => {
            g.extraStyle = Array.isArray(rules) ? rules : [];
            restyle(g);
        },
        /* Fetches data-graph-src again and shows what changed. */
        reload: async () => {
            if (!g.options.src) return stats(g);
            let data;
            try {
                data = await fetchData(g.options.src);
            } catch (e) {
                g.failed = true;
                try { console.warn('Sedna.UI graph: ' + e.message); } catch (x) { /* ignore */ }
                changed(g);
                return stats(g);
            }
            const recovering = g.failed;
            g.failed = false;
            await setData(g, data, { relayout: recovering });
            return stats(g);
        },
        export: format => exportImage(g, format === 'png' ? 'png' : 'svg'),
        download: (format, filename) => download(g, format === 'png' ? 'png' : 'svg', filename),
        collapse: id => collapseGroups(g, 'collapse', id ? g.cy.getElementById(String(id)) : null),
        expand: id => collapseGroups(g, 'expand', id ? g.cy.getElementById(String(id)) : null),
        connect: on => connect(g, on !== false),
        hulls: on => hulls(g, on !== false),
        stats: () => stats(g),
        control: (c, type) => control(g, c, type),
        invoke: (method, args) => invoke(g, method, args),
        destroy: () => dispose(g),
    };
}

/* The graph behind an element, made the first time it is asked for. A second call while
   the first is still starting gets the same one. */
function attach(el, given) {
    let g = graphs.get(el);
    if (!g) {
        g = {
            el,
            given: given || {},
            options: null,
            model: { nodes: [], edges: [] },
            step: 1,
            lit: null,
            selected: null,
            keyed: null,
            hiddenIds: new Set(),
            focusId: null,
            depth: 0,
            query: '',
            baseFilter: {},
            filter: {},
            touched: false,
            disposed: false,
            listeners: [],
            observers: [],
        };
        g.options = readOptions(el, g.given.options);
        g.emit = (name, detail, cancelable) =>
            el.dispatchEvent(new CustomEvent(name, { bubbles: true, cancelable: !!cancelable, detail }));
        g.set = (data, opts) => setData(g, data, opts);
        g.api = makeApi(g);
        graphs.set(el, g);
        g.started = start(g).catch(e => {
            el.setAttribute('data-graph-state', 'error');
            report(e);
            throw e;
        });
    }
    return g.started.then(() => g.api);
}

/* Takes down every graph whose element has left the document. */
function sweep() {
    for (const g of [...graphs.values()]) if (!g.el.isConnected) dispose(g);
}

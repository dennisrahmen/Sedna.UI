/* ── Controls ─────────────────────────────────────────────────────────────────
   Ordinary form controls and buttons, written by the app with the library's classes,
   that drive a graph with no script of the app's own:

     data-graph-action="…"     a button: zoom-in, zoom-out, fit, arrange, fullscreen,
                               export-png, export-svg, reset, select, focus, unfocus,
                               open, clear, hide, show-all, expand, collapse,
                               expand-all, collapse-all, connect, hulls, reload. An action on
                               one record takes its `value`, else the selection.
     data-graph-filter="kind"  a chip — a checkbox, or a toggle button with aria-pressed
                               — hides its `value` while it is off; a select or radios
                               choose one value, and "" chooses all. `edge.kind`
                               filters links.
     data-graph-show="muted"   a checkbox or toggle button: show muted records, or
                     "isolated"  records with no visible link.
     data-graph-search         a text field: marks matching records as it is typed in.
     data-graph-option="layout" a select or radios: layout, direction, spacing, labels,
                               edge-labels, colour, depth, nodes.
     data-graph-stats="{nodes} records · {edges} links"
                               text the script keeps current, in the app's words.

   A toggle button is flipped by the script (`aria-pressed`), so its state is the
   script's; a checkbox, a radio, a select and a text field keep their native state,
   which a Blazor binding can own. Reset puts every one back as it was first drawn.
   ─────────────────────────────────────────────────────────────────────────── */

const CONTROL_SELECTOR = '[data-graph-action],[data-graph-filter],[data-graph-show],[data-graph-search],[data-graph-option]';

/* The app's elements of one kind that belong to a graph — its controls, its stats, its
   panel, its live region. One belongs to the graph that the nearest `data-graph-for`
   around it names, else to the graph it is inside, else to the first graph in its
   `[data-graph-frame]`; and everything inside an element that names the graph with
   `data-graph-for` is the graph's, wherever that element is on the page. */
function scoped(g, selector) {
    const found = new Set();
    const mine = c => {
        const named = c.closest('[data-graph-for]');
        if (named && named.getAttribute('data-graph-for')) return named.getAttribute('data-graph-for') === g.el.id;
        const inside = c.closest('[data-graph]');
        if (inside) return inside === g.el;
        const frame = c.closest('[data-graph-frame]');
        return !!frame && frame.querySelector('[data-graph]') === g.el;
    };
    const frame = g.el.closest('[data-graph-frame]');
    (frame || g.el).querySelectorAll(selector).forEach(c => { if (mine(c)) found.add(c); });
    if (g.el.id) {
        document.querySelectorAll(`[data-graph-for="${CSS.escape(g.el.id)}"]`).forEach(scope => {
            if (scope.matches(selector)) found.add(scope);
            scope.querySelectorAll(selector).forEach(c => { if (mine(c)) found.add(c); });
        });
    }
    return [...found].filter(c => !c.closest('template'));
}

function controlsOf(g) {
    return scoped(g, CONTROL_SELECTOR).filter(c => !c.closest('[data-graph-row]'));
}

const isToggle = c => c.tagName === 'BUTTON' && (c.hasAttribute('data-graph-filter') || c.hasAttribute('data-graph-show')
    || ['connect', 'hulls', 'fullscreen'].includes(c.getAttribute('data-graph-action')));
const isOn = c => (c.type === 'checkbox' || c.type === 'radio' ? c.checked : c.getAttribute('aria-pressed') === 'true');

/* What was on screen at the start, so Reset can put it back. */
function rememberControls(g) {
    for (const c of controlsOf(g)) {
        if (c.__graphStart !== undefined) continue;
        if (c.type === 'checkbox' || c.type === 'radio') c.__graphStart = c.checked;
        else if (c.tagName === 'SELECT') c.__graphStart = Array.from(c.options).map(o => o.selected);
        else if (isToggle(c)) c.__graphStart = c.getAttribute('aria-pressed') === 'true';
        else if (c.hasAttribute('data-graph-search')) c.__graphStart = c.value;
        else c.__graphStart = null;
    }
}

function restoreControls(g) {
    for (const c of controlsOf(g)) {
        if (c.__graphStart === undefined || c.__graphStart === null) continue;
        if (c.type === 'checkbox' || c.type === 'radio') c.checked = c.__graphStart;
        else if (c.tagName === 'SELECT') Array.from(c.options).forEach((o, i) => { o.selected = !!c.__graphStart[i]; });
        else if (isToggle(c)) c.setAttribute('aria-pressed', c.__graphStart ? 'true' : 'false');
        else if (c.hasAttribute('data-graph-search')) c.value = c.__graphStart;
    }
}

/* The filter the controls add up to, over the one the app set by call. A chip — a
   checkbox or a toggle button — that is off hides its value (`except`); a select or a
   radio group chooses (`nodes`), and its empty value chooses everything. A field no
   control mentions is left as the call set it. */
function filterFromControls(g) {
    const spec = JSON.parse(JSON.stringify(g.baseFilter || {}));
    for (const k of ['nodes', 'except', 'edges', 'edgesExcept']) spec[k] = spec[k] || {};
    const chosen = new Map();
    const off = new Map();
    const split = v => String(v ?? '').split(/\s+/).filter(Boolean);
    let touched = false;
    for (const c of controlsOf(g)) {
        if (c.hasAttribute('data-graph-filter')) {
            touched = true;
            const field = c.getAttribute('data-graph-filter');
            if (c.tagName === 'SELECT') {
                const values = Array.from(c.selectedOptions).map(o => o.value);
                chosen.set(field, values.includes('') || !values.length ? null : new Set(values));
            } else if (c.type === 'radio') {
                if (c.checked) chosen.set(field, c.value === '' ? null : new Set(split(c.value)));
            } else {
                if (!off.has(field)) off.set(field, new Set());
                if (!isOn(c)) split(c.value ?? c.getAttribute('value')).forEach(v => off.get(field).add(v));
            }
        } else if (c.hasAttribute('data-graph-show')) {
            touched = true;
            spec[c.getAttribute('data-graph-show')] = isOn(c);
        }
    }
    const place = (field, values, nodeKey, edgeKey) => {
        const edgeField = field.startsWith('edge.');
        const name = edgeField ? field.slice(5) : field;
        const into = spec[edgeField ? edgeKey : nodeKey];
        if (!values || values.size === 0) delete into[name];
        else into[name] = [...values];
    };
    for (const [field, values] of chosen) place(field, values, 'nodes', 'edges');
    for (const [field, values] of off) place(field, values, 'except', 'edgesExcept');
    spec.focus = g.focusId || null;
    spec.depth = g.depth;
    spec.hide = [...new Set([...(spec.hide || []), ...g.hiddenIds])];
    return { spec, touched };
}

function writeStats(g, s) {
    for (const el of scoped(g, '[data-graph-stats]')) {
        const template = el.getAttribute('data-graph-stats');
        if (!template) continue;
        // `data-graph-stats-match` replaces the sentence while a search is marking records.
        const which = s.matches && g.query && el.getAttribute('data-graph-stats-match') ? el.getAttribute('data-graph-stats-match') : template;
        el.textContent = which.replace(/\{(\w+)\}/g, (_, k) => (k in s && s[k] !== null ? String(s[k]) : ''));
    }
}

async function control(g, c, type) {
    if (c.hasAttribute('data-graph-search')) {
        // Marked as it is typed, but the view settles on the matches once the typing
        // pauses: a view that flies to each keystroke's matches is impossible to read.
        clearTimeout(g.typing);
        const value = c.value;
        g.typing = setTimeout(() => {
            if (g.disposed) return;
            search(g, value, true);
            changed(g);
        }, 140);
        return;
    }
    if (c.hasAttribute('data-graph-option')) {
        const name = c.getAttribute('data-graph-option');
        if ((c.type === 'radio' || c.type === 'checkbox') && !c.checked) return;
        return option(g, name, c.value);
    }
    if (isToggle(c) && type === 'click') {
        c.setAttribute('aria-pressed', c.getAttribute('aria-pressed') === 'true' ? 'false' : 'true');
    }
    if (c.hasAttribute('data-graph-filter') || c.hasAttribute('data-graph-show')) {
        if (c.tagName === 'BUTTON' && type !== 'click') return;
        return refilter(g, true);
    }
    const action = c.getAttribute('data-graph-action');
    if (!action || (type !== 'click' && c.tagName === 'BUTTON')) return;
    const value = c.getAttribute('value') || c.value || null;
    return act(g, action, value, c);
}

async function refilter(g, animate) {
    const { spec } = filterFromControls(g);
    applyFilter(g, spec);
    if (g.query) search(g, g.query, false);
    await arrange(g, animate);
    changed(g);
}

function target(g, value) {
    const cy = g.cy;
    if (value) {
        const e = cy.getElementById(value);
        return e.nonempty() ? e : null;
    }
    if (g.menu?.target) return g.menu.target;
    return g.selected && g.selected.nonempty() ? g.selected : g.keyed || null;
}

async function act(g, action, value, c) {
    const ele = ['select', 'focus', 'open', 'hide', 'expand', 'collapse'].includes(action) ? target(g, value) : null;
    switch (action) {
        case 'zoom-in': return zoomBy(g, 1.3);
        case 'zoom-out': return zoomBy(g, 1 / 1.3);
        case 'fit': return fitAll(g);
        case 'arrange': return arrange(g, true);
        case 'fullscreen': return fullscreen(g, c);
        case 'export-png': return download(g, 'png', c?.getAttribute('data-graph-filename'));
        case 'export-svg': return download(g, 'svg', c?.getAttribute('data-graph-filename'));
        case 'reset': return reset(g);
        case 'select':
            if (ele) {
                select(g, ele);
                centreOn(g, ele);
            }
            return;
        case 'clear': return select(g, null);
        case 'focus': return focusOn(g, ele ? ele.id() : null);
        case 'unfocus': return focusOn(g, null);
        case 'open': return ele ? open(g, ele, false) : undefined;
        case 'hide':
            if (ele) {
                g.hiddenIds.add(ele.id());
                await refilter(g, true);
            }
            return;
        case 'show-all':
            g.hiddenIds.clear();
            return refilter(g, true);
        case 'expand': return ele ? collapseGroups(g, 'expand', ele) : undefined;
        case 'collapse': return ele ? collapseGroups(g, 'collapse', ele) : undefined;
        case 'expand-all': return collapseGroups(g, 'expand', null);
        case 'collapse-all': return collapseGroups(g, 'collapse', null);
        case 'connect': return connect(g, c ? c.getAttribute('aria-pressed') === 'true' : !g.drawing);
        case 'hulls': return hulls(g, c ? c.getAttribute('aria-pressed') === 'true' : !g.hullsOn);
        case 'reload': return g.api.reload();
        default:
            warnOnce('action:' + action, `"${action}" is not a graph action.`);
    }
}

async function option(g, name, value) {
    const o = g.options;
    switch (name) {
        case 'layout':
            if (!knownLayout(value)) return warnOnce('layout:' + value, `"${value}" is not a layout.`);
            o.layout = value;
            g.touched = false;
            restyle(g);
            await arrange(g, true);
            break;
        case 'direction': o.direction = value; g.touched = false; restyle(g); await arrange(g, true); break;
        case 'spacing': o.spacing = value; g.touched = false; await arrange(g, true); break;
        case 'nodes':
            o.nodes = value === 'box' ? 'box' : 'dot';
            followEdgeLabels(o);
            measureBoxes(g);
            restyle(g);
            g.touched = false;
            await arrange(g, true);
            break;
        case 'labels': o.labels = value; restyle(g); declutter(g); break;
        case 'edge-labels':
            o.edgeLabels = value;
            o.edgeLabelsChosen = true;
            // Names written on every link are held at their size from now on, so the links
            // take the zoom they have been skipping.
            g.cy.batch(() => g.cy.edges().data('zoom', g.step));
            restyle(g);
            break;
        case 'colour': case 'color': o.colourBy = value || 'tone'; restyle(g); break;
        case 'curve': o.curve = value; restyle(g); break;
        case 'arrows': o.arrows = value; restyle(g); break;
        case 'depth':
            g.depth = Math.max(1, Math.min(Number(value) || 1, 6));
            if (g.focusId) await refilter(g, true);
            break;
        default:
            return warnOnce('option:' + name, `"${name}" is not a graph option.`);
    }
    changed(g);
}

/* Everything back as it was first drawn: the controls, and what they changed — the
   filter, the focus, the search, the view options, the outlines, drawing mode and the
   folds the data started with. */
async function reset(g) {
    restoreControls(g);
    g.hiddenIds.clear();
    g.focusId = g.options.focus || null;
    g.depth = g.startDepth || g.options.depth;
    g.query = '';
    select(g, null);
    const start = g.startView || {};
    const boxesChanged = start.nodes !== g.options.nodes;
    Object.assign(g.options, start);
    if (boxesChanged) measureBoxes(g);
    restyle(g);
    if (!!g.hullsOn !== !!g.options.hulls) await hulls(g, g.options.hulls);
    if (!!g.drawing !== !!g.options.connect) await connect(g, g.options.connect);
    if (g.ec) {
        const cy = g.cy;
        g.quietFolds = true;
        try {
            g.ec.expandAll({ animate: false, fisheye: false });
            const folded = cy.nodes(':parent').filter(n => flag(n.data('fields')?.collapsed));
            if (folded.nonempty()) g.ec.collapse(folded, { animate: false, fisheye: false });
        } finally {
            g.quietFolds = false;
        }
    }
    g.touched = false;
    await refilter(g, true);
    search(g, '', false);
    changed(g);
}

async function focusOn(g, id) {
    g.focusId = id;
    await refilter(g, true);
}

function centreOn(g, ele) {
    const cy = g.cy;
    cy.stop();
    if (reducedMotion() || document.visibilityState !== 'visible') cy.center(ele);
    else cy.animate({ center: { eles: ele } }, { duration: 220, easing: 'ease-out-cubic', complete: () => settle(g) });
}

/* The browser's own full screen, on the frame around the graph — `[data-graph-frame]`
   when the app wrote one, so its toolbar and panel come along — else the graph. */
function fullscreen(g, button) {
    const frame = g.el.closest('[data-graph-frame]') || g.el;
    if (document.fullscreenElement) document.exitFullscreen?.();
    else frame.requestFullscreen?.().catch(() => button?.setAttribute('aria-pressed', 'false'));
}

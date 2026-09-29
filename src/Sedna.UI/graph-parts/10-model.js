/* ── The model ────────────────────────────────────────────────────────────────
   A graph's records arrive three ways and all three end as the same plain model:

     1. markup    `li[data-node]` and `li[data-edge]` inside the graph's
                  `[data-graph-data]` list — or the list `data-graph-data="#id"` names.
                  Read once, and again whenever the list changes, so a Blazor render
                  that adds a record adds it to the drawing.
     2. a URL     `data-graph-src="/api/graph"`, fetched as JSON { nodes, edges }. The
                  records never cross a Blazor circuit.
     3. a call    graph.set({ nodes, edges }) from script; ISednaGraph.SetDataAsync.

   A node:  { id, label, kind, tone, tones, shape, icon, group, cluster, parent,
              weight, muted, root, hub, href, meta, tags, x, y, display, fields }
   An edge: { id, source, target, label, kind, tone, line, weight, arrow, muted, fields }

   In markup each is the attribute of the same name — `data-tone="3"`, `data-muted` —
   and the element's text is its label unless `data-label` says otherwise, so the list
   reads as sentences to a screen reader ("Orders API reads the orders database") while
   the canvas writes the short form ("reads"). Every other `data-*` attribute is kept
   in `fields`, which is what a filter or a tooltip slot can name.
   ─────────────────────────────────────────────────────────────────────────── */

/* A tone is one of the series the stylesheet's .series-* classes paint — so a legend
   swatch and the canvas are one colour by construction — or a token name. Never a
   literal colour: nothing on the canvas bypasses the theme. */
const TONE_TOKENS = {
    '1': '--viz-1', '2': '--viz-2', '3': '--viz-3', '4': '--viz-4', '5': '--viz-5', '6': '--viz-6',
    go: '--go-solid', warn: '--warn-solid', danger: '--danger-solid', info: '--info-solid',
    muted: '--border-strong', brand: '--brand', accent: '--accent',
};

/* Friendly names first, then cytoscape's own, which are accepted as they are. The
   rounded variants are the default drawing of a polygon: a hard corner reads as a
   different shape at 14 pixels. */
const SHAPES = {
    circle: 'ellipse', dot: 'ellipse', ellipse: 'ellipse',
    square: 'rectangle', rectangle: 'rectangle', rounded: 'round-rectangle', box: 'round-rectangle',
    diamond: 'round-diamond', hexagon: 'round-hexagon', octagon: 'round-octagon',
    pentagon: 'round-pentagon', heptagon: 'round-heptagon', triangle: 'round-triangle', tag: 'round-tag',
    star: 'star', barrel: 'barrel', rhomboid: 'rhomboid', vee: 'vee', pill: 'round-rectangle',
    'round-rectangle': 'round-rectangle', 'round-diamond': 'round-diamond', 'round-hexagon': 'round-hexagon',
    'round-octagon': 'round-octagon', 'round-pentagon': 'round-pentagon', 'round-heptagon': 'round-heptagon',
    'round-triangle': 'round-triangle', 'round-tag': 'round-tag', 'cut-rectangle': 'cut-rectangle',
    'bottom-round-rectangle': 'bottom-round-rectangle', 'concave-hexagon': 'concave-hexagon',
};

const LINES = new Set(['solid', 'dashed', 'dotted']);
// A lookup that answers only for its own keys: `data-tone="constructor"` is not a tone.
const own = (table, key) => (Object.hasOwn(table, key) ? table[key] : undefined);
const ARROWS = new Set(['none', 'target', 'source', 'both']);
const WEIGHTS = { light: 0.6, thin: 0.6, normal: 1, heavy: 2, strong: 2, bold: 2.6 };

let warned = new Set();
function warnOnce(key, message) {
    if (warned.has(key)) return;
    warned.add(key);
    try { console.warn('Sedna.UI graph: ' + message); } catch (e) { /* ignore */ }
}

function toneOf(value) {
    if (value === undefined || value === null || value === '') return null;
    const t = String(value).trim();
    if (own(TONE_TOKENS, t)) return t;
    if (/^series-/.test(t) && own(TONE_TOKENS, t.slice(7))) return t.slice(7);
    if (/^--[a-z0-9-]+$/i.test(t)) return t;
    warnOnce('tone:' + t, `"${t}" is not a tone. Use 1–6, go, warn, danger, info, muted, brand, accent, or a token name.`);
    return null;
}

const tokenOfTone = tone => (tone ? (own(TONE_TOKENS, tone) || tone) : null);

function flag(value) {
    if (value === true || value === false) return value;
    if (value === undefined || value === null) return false;
    const s = String(value).trim().toLowerCase();
    return s === '' || s === 'true' || s === '1' || s === 'yes';
}

function number(value) {
    if (value === undefined || value === null || value === '') return null;
    const n = Number(value);
    return Number.isFinite(n) ? n : null;
}

function text(value) {
    if (value === undefined || value === null) return null;
    const s = String(value).trim();
    return s.length ? s : null;
}

const camel = s => String(s).replace(/-([a-z0-9])/g, (_, c) => c.toUpperCase());

function listOf(value) {
    if (value === undefined || value === null || value === '') return [];
    if (Array.isArray(value)) return value.map(String).filter(Boolean);
    return String(value).split(/[\s,]+/).filter(Boolean);
}

const NODE_KEYS = new Set(['id', 'node', 'label', 'kind', 'tone', 'tones', 'shape', 'icon', 'group', 'cluster',
    'parent', 'weight', 'muted', 'root', 'hub', 'href', 'meta', 'tags', 'x', 'y', 'display', 'fields']);
const EDGE_KEYS = new Set(['id', 'edge', 'source', 'target', 'label', 'kind', 'tone', 'line', 'weight', 'arrow',
    'muted', 'fields']);

function node(raw) {
    const id = text(raw.id ?? raw.node);
    if (!id) return null;
    const tones = {};
    for (const [k, v] of Object.entries(raw.tones || {})) {
        const t = toneOf(v);
        if (t) tones[camel(k)] = t;
    }
    const fields = Object.assign({}, raw.fields || {});
    for (const [k, v] of Object.entries(raw)) {
        if (!NODE_KEYS.has(k) && (typeof v !== 'object' || v === null)) fields[k] = v;
    }
    const shape = text(raw.shape);
    if (shape && !own(SHAPES, shape)) warnOnce('shape:' + shape, `"${shape}" is not a shape; drawing a circle.`);
    const display = text(raw.display);
    return {
        id,
        label: text(raw.label) ?? id,
        kind: text(raw.kind),
        tone: toneOf(raw.tone),
        tones,
        shape: shape ? (own(SHAPES, shape) || null) : null,
        icon: text(raw.icon),
        group: text(raw.group),
        cluster: text(raw.cluster),
        parent: text(raw.parent),
        weight: number(raw.weight),
        muted: flag(raw.muted),
        root: flag(raw.root),
        hub: flag(raw.hub),
        href: text(raw.href),
        meta: text(raw.meta),
        tags: listOf(raw.tags),
        x: number(raw.x),
        y: number(raw.y),
        display: display === 'box' || display === 'dot' ? display : null,
        fields,
    };
}

function edge(raw, seen) {
    const source = text(raw.source), target = text(raw.target);
    if (!source || !target) return null;
    let id = text(raw.id ?? raw.edge);
    if (!id) {
        // A stable id for an edge the app did not name, so a re-read keeps the same one
        // for the same line: its ends, and how many lines between them came before it.
        const base = source + '→' + target;
        const n = (seen.get(base) || 0) + 1;
        seen.set(base, n);
        id = n === 1 ? base : base + '#' + n;
    }
    const fields = Object.assign({}, raw.fields || {});
    for (const [k, v] of Object.entries(raw)) {
        if (!EDGE_KEYS.has(k) && (typeof v !== 'object' || v === null)) fields[k] = v;
    }
    const line = text(raw.line);
    const arrow = text(raw.arrow);
    const w = raw.weight;
    return {
        id,
        source,
        target,
        label: text(raw.label),
        kind: text(raw.kind),
        tone: toneOf(raw.tone),
        line: line && LINES.has(line) ? line : 'solid',
        weight: typeof w === 'string' && own(WEIGHTS, w) ? WEIGHTS[w] : (number(w) ?? 1),
        arrow: arrow && ARROWS.has(arrow) ? arrow : null,
        muted: flag(raw.muted),
        fields,
    };
}

/* Checks what the app handed over and drops what cannot be drawn, saying so once:
   a duplicate id, an edge whose end is not a record, a parent that does not exist. */
function normalise(data) {
    const nodes = [];
    const byId = new Map();
    for (const raw of (data && data.nodes) || []) {
        const n = node(raw || {});
        if (!n) continue;
        if (byId.has(n.id)) {
            warnOnce('dup:' + n.id, `two records share the id "${n.id}"; the second is ignored.`);
            continue;
        }
        byId.set(n.id, n);
        nodes.push(n);
    }
    for (const n of nodes) {
        if (n.parent && (!byId.has(n.parent) || n.parent === n.id)) {
            warnOnce('parent:' + n.parent, `"${n.id}" names the parent "${n.parent}", which is not a record.`);
            n.parent = null;
        }
    }
    // A cycle of parents cannot be drawn: break it at the record that closes it.
    for (const n of nodes) {
        const chain = new Set([n.id]);
        let p = n.parent ? byId.get(n.parent) : null;
        while (p) {
            if (chain.has(p.id)) {
                warnOnce('cycle:' + n.id, `the parents of "${n.id}" form a cycle; it is drawn without one.`);
                n.parent = null;
                break;
            }
            chain.add(p.id);
            p = p.parent ? byId.get(p.parent) : null;
        }
    }
    const edges = [];
    const seen = new Map();
    const edgeIds = new Set();
    let dropped = 0;
    for (const raw of (data && data.edges) || []) {
        const e = edge(raw || {}, seen);
        if (!e) continue;
        if (!byId.has(e.source) || !byId.has(e.target)) {
            dropped++;
            continue;
        }
        if (edgeIds.has(e.id) || byId.has(e.id)) {
            warnOnce('edup:' + e.id, `the id "${e.id}" is used twice; the second link is ignored.`);
            continue;
        }
        edgeIds.add(e.id);
        edges.push(e);
    }
    if (dropped) warnOnce('dangling:' + dropped, `${dropped} link(s) name a record that is not in the graph and are not drawn.`);
    return { nodes, edges };
}

/* ── Reading the app's markup ──────────────────────────────────────────────── */

function readList(el) {
    const named = el.getAttribute('data-graph-data');
    const lists = named && named.charAt(0) === '#'
        ? [document.getElementById(named.slice(1))].filter(Boolean)
        : Array.from(el.querySelectorAll('[data-graph-data]'));
    return lists;
}

function fromElement(li, isEdge) {
    const raw = {};
    for (const [k, v] of Object.entries(li.dataset)) {
        // data-tone-<name> is a colouring; data-toner is a field of the app's own.
        if (/^tone[A-Z]/.test(k)) {
            raw.tones = raw.tones || {};
            raw.tones[k.charAt(4).toLowerCase() + k.slice(5)] = v;
            continue;
        }
        raw[k] = v;
    }
    if (raw.label === undefined) raw.label = li.textContent.replace(/\s+/g, ' ').trim();
    if (isEdge) raw.id = raw.edge || undefined;
    else raw.id = raw.node;
    delete raw.node;
    delete raw.edge;
    return raw;
}

function readMarkup(el) {
    const lists = readList(el);
    if (!lists.length) return null;
    const nodes = [], edges = [];
    for (const list of lists) {
        for (const li of list.querySelectorAll('[data-node]')) nodes.push(fromElement(li, false));
        for (const li of list.querySelectorAll('[data-edge]')) edges.push(fromElement(li, true));
    }
    return { nodes, edges };
}

/* Whether a node or an edge matches a filter's field: the named property, else the
   field of that name, else its camelCase form — `data-team-lead` is `teamLead`. */
function valueOf(item, field) {
    if (field in item && field !== 'fields') return item[field];
    const f = item.fields || {};
    if (field in f) return f[field];
    const c = camel(field);
    return c in f ? f[c] : undefined;
}

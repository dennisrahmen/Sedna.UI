/*! Sedna.UI — generated, comments stripped. The commented copy ships in the package as readable/Sedna.UI.graph.js. */
import cytoscape from '../lib/cytoscape/cytoscape.js';
const TONE_TOKENS = {
    '1': '--viz-1', '2': '--viz-2', '3': '--viz-3', '4': '--viz-4', '5': '--viz-5', '6': '--viz-6',
    go: '--go-solid', warn: '--warn-solid', danger: '--danger-solid', info: '--info-solid',
    muted: '--muted', brand: '--brand', accent: '--accent',
};
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
const own = (table, key) => (Object.hasOwn(table, key) ? table[key] : undefined);
const ARROWS = new Set(['none', 'target', 'source', 'both']);
const WEIGHTS = { light: 0.6, thin: 0.6, normal: 1, heavy: 2, strong: 2, bold: 2.6 };
const STATES = new Set(['next', 'running', 'waiting', 'done', 'failed', 'skipped']);
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
function stateOf(value) {
    const s = text(value);
    if (!s) return null;
    if (STATES.has(s)) return s;
    warnOnce('state:' + s, `"${s}" is not a run state. Use next, running, waiting, done, failed or skipped.`);
    return null;
}
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
    'parent', 'weight', 'muted', 'root', 'hub', 'href', 'meta', 'tags', 'x', 'y', 'display', 'state', 'placeholder', 'fields']);
const EDGE_KEYS = new Set(['id', 'edge', 'source', 'target', 'label', 'kind', 'tone', 'line', 'weight', 'arrow',
    'muted', 'state', 'fields']);
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
    const placeholder = flag(raw.placeholder);
    return {
        id,
        label: text(raw.label) ?? id,
        kind: text(raw.kind),
        tone: toneOf(raw.tone),
        tones,
        shape: shape ? (own(SHAPES, shape) || null) : null,
        icon: text(raw.icon) ?? (placeholder ? 'ri-add-line' : null),
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
        state: stateOf(raw.state),
        placeholder,
        fields,
    };
}
function edge(raw, seen) {
    const source = text(raw.source), target = text(raw.target);
    if (!source || !target) return null;
    let id = text(raw.id ?? raw.edge);
    if (!id) {
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
        state: stateOf(raw.state),
        fields,
    };
}
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
function valueOf(item, field) {
    if (field in item && field !== 'fields') return item[field];
    const f = item.fields || {};
    if (field in f) return f[field];
    const c = camel(field);
    return c in f ? f[c] : undefined;
}
function colourReader(host) {
    const probe = document.createElement('span');
    probe.setAttribute('aria-hidden', 'true');
    probe.style.cssText = 'position:absolute;width:0;height:0;overflow:hidden;visibility:hidden;pointer-events:none';
    host.appendChild(probe);
    const pixel = document.createElement('canvas');
    pixel.width = pixel.height = 1;
    const ctx = pixel.getContext('2d', { willReadFrequently: true });
    const cache = new Map();
    function rgba(value, fallback) {
        if (!value || value === 'transparent') return fallback;
        if (/^rgba?\(/.test(value) && !/\//.test(value)) return value;
        ctx.clearRect(0, 0, 1, 1);
        ctx.fillStyle = CSS.supports('color', value) ? value : 'transparent';
        ctx.fillRect(0, 0, 1, 1);
        const [r, g, b, a] = ctx.getImageData(0, 0, 1, 1).data;
        if (a === 0) return fallback;
        return a === 255 ? `rgb(${r}, ${g}, ${b})` : `rgba(${r}, ${g}, ${b}, ${(a / 255).toFixed(3)})`;
    }
    return {
        token(name, fallback) {
            if (!name) return fallback;
            if (!cache.has(name)) {
                probe.style.color = '';
                probe.style.color = `var(${name})`;
                cache.set(name, rgba(getComputedStyle(probe).color, null));
            }
            return cache.get(name) ?? fallback;
        },
        ground(fallback) {
            if (!cache.has('@ground')) {
                let value = null;
                for (let el = host; el && el.nodeType === 1; el = el.parentElement) {
                    const bg = getComputedStyle(el).backgroundColor;
                    if (bg && bg !== 'transparent' && !/rgba\(\s*0,\s*0,\s*0,\s*0\s*\)/.test(bg)) {
                        value = rgba(bg, null);
                        if (value) break;
                    }
                }
                cache.set('@ground', value ?? this.token('--surface-content', null) ?? this.token('--bg', null));
            }
            return cache.get('@ground') ?? fallback;
        },
        font() {
            if (!cache.has('@font')) cache.set('@font', getComputedStyle(host).fontFamily || 'system-ui, sans-serif');
            return cache.get('@font');
        },
        clear: () => cache.clear(),
        refresh() {
            const before = new Map(cache);
            cache.clear();
            let moved = false;
            for (const [key, value] of before) {
                const now = key === '@ground' ? this.ground(null) : key === '@font' ? this.font() : this.token(key, null);
                if ((now ?? null) !== (value ?? null)) moved = true;
            }
            return moved;
        },
        remove: () => probe.remove(),
        probe,
    };
}
function palette(colours) {
    const c = (token, fallback) => colours.token(token, fallback);
    const fg = c('--fg');
    return {
        fg,
        soft: c('--fg-soft', fg),
        muted: c('--muted', fg),
        line: c('--border-strong', fg),
        border: c('--border', fg),
        brand: c('--brand', fg),
        selected: c('--selected', c('--brand', fg)),
        accent: c('--accent', fg),
        ring: c('--brand-ring', fg),
        raised: c('--surface-raised-2', c('--card-bg')),
        tint: c('--brand-tint', c('--card-bg')),
        ground: colours.ground(c('--card-bg')),
        font: colours.font(),
    };
}
function withAlpha(colour, alpha) {
    const m = /rgba?\(\s*(\d+),\s*(\d+),\s*(\d+)/.exec(colour || '');
    return m ? `rgba(${m[1]}, ${m[2]}, ${m[3]}, ${alpha})` : colour;
}
function iconPainter(host) {
    const probe = document.createElement('i');
    probe.setAttribute('aria-hidden', 'true');
    probe.style.cssText = 'position:absolute;width:0;height:0;overflow:hidden;visibility:hidden;pointer-events:none';
    host.appendChild(probe);
    const glyphs = new Map();
    const images = new Map();
    let fontReady = null;
    function glyph(cls) {
        if (!probe.isConnected) return null;
        if (!glyphs.has(cls)) {
            probe.className = cls;
            const style = getComputedStyle(probe, '::before');
            const content = style.content;
            const ch = content && content !== 'none' && content !== 'normal' ? content.replace(/^["']|["']$/g, '') : '';
            glyphs.set(cls, ch ? { ch, family: style.fontFamily } : null);
            if (!ch) warnOnce('icon:' + cls, `"${cls}" is not a Remix Icon class; the record is drawn without it.`);
        }
        return glyphs.get(cls);
    }
    return {
        ready() {
            if (!fontReady) {
                fontReady = (document.fonts && document.fonts.load
                    ? document.fonts.load('32px remixicon').catch(() => null)
                    : Promise.resolve()).then(() => { glyphs.clear(); images.clear(); });
            }
            return fontReady;
        },
        image(cls, colour, px = 32) {
            if (!cls) return null;
            const scale = Math.min(Math.max(pixelRatioOf(host), 1), 3) * 2;
            const key = cls + '|' + colour + '|' + px + '|' + scale;
            if (images.has(key)) return images.get(key);
            const g = glyph(cls);
            if (!g) {
                images.set(key, null);
                return null;
            }
            const size = Math.round(px * scale);
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = size;
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = colour;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.font = `${Math.round(size * 0.86)}px ${g.family}`;
            ctx.fillText(g.ch, size / 2, size / 2 + size * 0.02);
            const url = canvas.toDataURL('image/png');
            images.set(key, url);
            return url;
        },
        clear: () => images.clear(),
        remove: () => probe.remove(),
    };
}
const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches;
const GLIDE_LIMIT = 12000;
const glides = g => !reducedMotion() && document.visibilityState === 'visible' && g.cy.elements().length <= GLIDE_LIMIT;
const zoomOf = ele => ele.data('zoom') ?? 1;
const HOLD_FLOOR = 0.4;
const MIN_ZOOM = 0.05;
const held = ele => (zoomOf(ele) <= 1 ? 1 : 1 / Math.pow(zoomOf(ele), 0.8));
const LABEL_MAX_PX = 180;
const MIN_NAME_PX = 6;
const labelPx = n => 10.5 + Math.min(n.data('degree') || 0, 14) * 0.2 + (n.data('hub') ? 1.5 : 0);
const labelSize = n => labelPx(n) / zoomOf(n);
let textCtx = null;
const textWidths = new Map();
function textWidth(text, weight, px, family) {
    const at = Math.max(1, Math.round(px));
    const key = weight + '\u0000' + at + family + '\u0000' + text;
    let w = textWidths.get(key);
    if (w === undefined) {
        textCtx = textCtx || document.createElement('canvas').getContext('2d');
        textCtx.font = `${weight} ${at}px ${family}`;
        w = textCtx.measureText(text).width;
        if (textWidths.size > 50000) textWidths.clear();
        textWidths.set(key, w);
    }
    return w * px / at;
}
const sizeOf = weight => Math.min(14 + 5 * Math.sqrt(Math.max(weight, 0)), 46);
const labelSide = options => (options.nodes !== 'box' && (options.layout === 'tree' || options.layout === 'dagre')
    ? options.direction === 'LR' ? 'right' : options.direction === 'RL' ? 'left' : 'bottom'
    : 'bottom');
function nodeTone(n, options) {
    const key = options.colourBy;
    const alt = key && key !== 'tone' ? (n.data('tones') || {})[camel(key)] : null;
    return tokenOfTone(alt ?? n.data('tone') ?? (n.data('muted') || n.data('placeholder') ? 'muted' : '1'));
}
function resolveRules(rules, colours) {
    const resolve = v => {
        if (typeof v !== 'string') return v;
        const m = /^var\((--[a-z0-9-]+)\)$/i.exec(v.trim());
        return m ? colours.token(m[1], v) : v;
    };
    return (rules || []).map(r => ({
        selector: r.selector,
        style: Object.fromEntries(Object.entries(r.style || {}).map(([k, v]) => [k, resolve(v)])),
    }));
}
function styleFor(colours, icons, options, extra) {
    const p = palette(colours);
    const c = (token, fallback) => colours.token(token, fallback);
    const toneColour = n => c(nodeTone(n, options), p.line);
    const box = n => (n.data('display') || options.nodes) === 'box';
    const nameOf = n => (box(n) ? n.data('boxLabel') : n.data('label')) || '';
    const edgeTone = e => c(tokenOfTone(e.data('tone')), p.line);
    const edgeWidth = e => (e.data('weight') || 1) * held(e) * (options.nodes === 'box' ? 1.25 : 1);
    const run = { agent: c('--agent-to', p.brand), go: c('--go-solid', p.line), warn: c('--warn-solid', p.line), danger: c('--danger-solid', p.line) };
    const lineIn = colour => ({ 'line-color': colour, 'target-arrow-color': colour, 'source-arrow-color': colour });
    const arrows = a => ({
        'target-arrow-shape': a === 'both' || a === 'target' ? 'triangle' : 'none',
        'source-arrow-shape': a === 'both' || a === 'source' ? 'triangle' : 'none',
    });
    const iconFor = n => icons.image(n.data('icon'), box(n) || n.data('placeholder') ? toneColour(n) : p.ground, 32) || 'none';
    const dots = options.nodes !== 'box';
    const routed = options.layout === 'dagre' && !options.curveChosen;
    const route = routed ? {
        'curve-style': 'unbundled-bezier',
        'control-point-weights': e => e.scratch('controlPointWeights') || [0.5],
        'control-point-distances': e => e.scratch('controlPointDistances') || [0],
        'edge-distances': 'intersection',
    } : {};
    const edgeName = dots ? {
        'font-size': e => 10 / zoomOf(e),
        'text-background-padding': e => 2 / zoomOf(e),
    } : {};
    const raise = z => (options.veil === false ? { 'z-index': z } : {});
    const side = labelSide(options);
    const beside = side === 'bottom' ? {} : {
        'text-valign': 'center',
        'text-halign': side,
        'text-margin-y': 0,
        'text-margin-x': n => (side === 'right' ? 4 : -4) / zoomOf(n),
    };
    return [
        {
            selector: 'node',
            style: {
                'shape': 'ellipse',
                'background-color': toneColour,
                'background-image': 'none',
                'background-fit': 'none',
                'background-image-containment': 'inside',
                'background-clip': 'none',
                'width': n => (n.data('size') || 20) * held(n),
                'height': n => (n.data('size') || 20) * held(n),
                'border-width': n => 1.5 * held(n),
                'border-color': p.ground,
                'label': nameOf,
                'color': p.soft,
                'font-size': n => labelSize(n),
                'font-family': p.font,
                'text-valign': 'bottom',
                'text-margin-y': n => 3 / zoomOf(n),
                'text-wrap': 'ellipsis',
                'text-max-width': n => LABEL_MAX_PX / zoomOf(n),
                'min-zoomed-font-size': MIN_NAME_PX * options.scale,
                'text-outline-color': p.ground,
                'text-outline-width': n => 0.22 * labelSize(n),
                'text-outline-opacity': 0.95,
                'transition-property': 'opacity, background-opacity',
                'transition-duration': reducedMotion() ? 0 : 160,
                'outline-width': 0,
                'outline-color': p.ring,
                'outline-offset': 2,
                ...beside,
            },
        },
        { selector: 'node[?shape]', style: { 'shape': 'data(shape)' } },
        {
            selector: 'node[?icon]',
            style: {
                'background-image': iconFor,
                'background-width': n => (n.data('size') || 20) * held(n) * 0.56,
                'background-height': n => (n.data('size') || 20) * held(n) * 0.56,
            },
        },
        { selector: 'node[?hub]', style: { 'font-weight': 600, 'color': p.fg } },
        { selector: 'node[?muted]', style: { 'background-opacity': 0.45, 'color': p.muted } },
        {
            selector: 'node[display = "box"]',
            style: {
                'shape': 'round-rectangle',
                'label': nameOf,
                'width': n => n.data('boxW') || 120,
                'height': n => n.data('boxH') || 36,
                'background-color': p.raised,
                'background-opacity': 1,
                'background-width': 16,
                'background-height': 16,
                'background-position-x': 12,
                'background-position-y': '50%',
                'border-width': 1.5,
                'border-color': toneColour,
                'color': p.fg,
                'font-size': 12.5,
                'font-weight': n => (n.data('hub') || n.data('root') ? 600 : 400),
                'text-valign': 'center',
                'text-halign': 'center',
                'text-margin-y': 0,
                'text-margin-x': n => (n.data('icon') ? 10 : 0),
                'text-wrap': 'wrap',
                'text-max-width': n => (n.data('boxW') || 120) - (n.data('icon') ? 44 : 20),
                'line-height': 1.35,
                'text-outline-width': 0,
                'outline-offset': 3,
                'min-zoomed-font-size': 0,
            },
        },
        { selector: 'node[display = "box"][?shape]', style: { 'shape': 'data(shape)' } },
        { selector: 'node[display = "box"][?muted]', style: { 'background-opacity': 0.6, 'border-opacity': 0.6, 'color': p.muted } },
        {
            selector: 'node[?placeholder]',
            style: {
                'background-opacity': 0,
                'border-style': 'dashed',
                'border-dash-pattern': [5, 3],
                'border-color': toneColour,
                'border-opacity': 1,
                'border-width': n => (box(n) ? 1.5 : 1.5 * held(n)),
                'color': p.soft,
            },
        },
        {
            selector: ':parent',
            style: {
                'shape': 'round-rectangle',
                'background-color': toneColour,
                'background-opacity': 0.07,
                'background-image': 'none',
                'border-width': 1.5,
                'border-style': 'dashed',
                'border-color': toneColour,
                'border-opacity': 0.7,
                'padding': 18,
                'text-valign': 'top',
                'text-halign': 'center',
                'text-margin-y': -6,
                'font-weight': 600,
                'color': p.fg,
                'font-size': 12,
                'text-outline-width': 0,
                'text-background-color': p.ground,
                'text-background-opacity': 0.9,
                'text-background-padding': 3,
                'text-background-shape': 'round-rectangle',
                ...(options.nodes === 'box' ? {} : {
                    'font-size': n => 12 / zoomOf(n),
                    'text-margin-y': n => -6 / zoomOf(n),
                    'text-background-padding': n => 3 / zoomOf(n),
                }),
            },
        },
        {
            selector: 'node.cy-expand-collapse-collapsed-node',
            style: {
                'shape': 'round-rectangle',
                'width': n => Math.max(90, (n.data('boxW') || 90)),
                'height': 34,
                'background-color': toneColour,
                'background-opacity': 0.16,
                'border-width': 1.5,
                'border-style': 'dashed',
                'border-color': toneColour,
                'text-valign': 'center',
                'text-margin-y': 0,
                'color': p.fg,
                'font-weight': 600,
            },
        },
        { selector: 'node.unlabelled', style: { 'label': '' } },
        {
            selector: 'node[?root], node.focus',
            style: { 'border-width': n => (box(n) ? 2.5 : 3 * held(n)), 'border-color': p.selected, 'color': p.fg, 'font-weight': 600, 'label': nameOf },
        },
        {
            selector: 'node[state = "next"]',
            style: {
                'border-style': 'dashed',
                'border-dash-pattern': [5, 3],
                'border-color': p.brand,
                'border-opacity': 0.6,
                'border-width': n => (box(n) ? 2 : 2 * held(n)),
            },
        },
        {
            selector: 'node[state = "skipped"]',
            style: {
                'border-style': 'dashed',
                'border-dash-pattern': [5, 3],
                'border-color': n => (box(n) ? toneColour(n) : p.line),
                'border-opacity': 0.6,
                'background-opacity': n => (box(n) ? 0.6 : 0.45),
                'color': p.muted,
            },
        },
        {
            selector: 'node[state = "running"], node[state = "waiting"], node[state = "done"], node[state = "failed"]',
            style: {
                'outline-width': n => (box(n) ? 2 : 2 * held(n)),
                'outline-offset': n => (box(n) ? 3 : 2 * held(n)),
                'outline-opacity': 1,
                'outline-style': 'solid',
            },
        },
        {
            selector: 'node[state = "running"]',
            style: {
                'outline-color': run.agent,
                'underlay-color': run.agent,
                'underlay-opacity': n => 0.08 + 0.22 * (n.data('breath') ?? 0.5),
                'underlay-padding': n => (box(n) ? 9 : 9 * held(n)),
                'underlay-shape': n => (box(n) ? 'round-rectangle' : 'ellipse'),
            },
        },
        { selector: 'node[state = "waiting"]', style: { 'outline-color': run.warn, 'outline-style': 'dashed' } },
        { selector: 'node[state = "done"]', style: { 'outline-color': run.go } },
        { selector: 'node[state = "failed"]', style: { 'outline-color': run.danger, 'outline-width': n => (box(n) ? 3.5 : 3.5 * held(n)) } },
        {
            selector: 'edge',
            style: {
                'width': edgeWidth,
                'curve-style': options.curve,
                'taxi-direction': options.direction === 'LR' || options.direction === 'RL' ? 'horizontal' : 'vertical',
                'taxi-turn': '50%',
                'line-color': p.line,
                'line-style': 'solid',
                'line-dash-pattern': [6, 4],
                'target-arrow-color': p.line,
                'source-arrow-color': p.line,
                ...arrows(options.arrows),
                'arrow-scale': options.nodes === 'box' ? 0.9 : 0.7,
                'line-opacity': options.nodes === 'box' ? 0.8 : 0.4,
                'label': '',
                'font-size': dots ? 10 : 11,
                'font-family': p.font,
                'color': p.soft,
                'text-background-color': p.ground,
                'text-background-opacity': 0.92,
                'text-background-padding': 2,
                'text-background-shape': 'round-rectangle',
                'text-rotation': options.nodes === 'box' ? 'none' : 'autorotate',
                'transition-property': 'opacity',
                'transition-duration': reducedMotion() ? 0 : 160,
                ...route,
            },
        },
        { selector: 'edge[?tone]', style: { 'line-color': edgeTone, 'target-arrow-color': edgeTone, 'source-arrow-color': edgeTone } },
        { selector: 'edge[line = "dashed"]', style: { 'line-style': 'dashed' } },
        { selector: 'edge[line = "dotted"]', style: { 'line-style': 'dotted', 'line-dash-pattern': [1.5, 3.5] } },
        ...['none', 'target', 'source', 'both'].map(a => ({ selector: `edge[arrow = "${a}"]`, style: arrows(a) })),
        ...(options.edgeLabels === 'always' ? [{ selector: 'edge[?label]', style: { 'label': 'data(label)', ...edgeName } }] : []),
        { selector: 'edge.cy-expand-collapse-meta-edge', style: { 'curve-style': 'straight' } },
        { selector: 'edge[?muted]', style: { 'line-opacity': 0.2 } },
        { selector: 'edge[?across]', style: { 'line-opacity': options.nodes === 'box' ? 0.5 : 0.16 } },
        { selector: 'edge[run = "next"]', style: { 'line-style': 'dashed', 'line-dash-pattern': [6, 4], ...lineIn(p.brand), 'line-opacity': 0.5 } },
        {
            selector: 'edge[run = "skipped"]',
            style: { 'line-style': 'dashed', 'line-dash-pattern': [6, 4], ...lineIn(p.line), 'line-opacity': options.nodes === 'box' ? 0.35 : 0.2 },
        },
        { selector: 'edge[run = "waiting"], edge[run = "done"]', style: { 'line-style': 'solid', ...lineIn(run.go), 'line-opacity': 1 } },
        { selector: 'edge[run = "failed"]', style: { 'line-style': 'solid', ...lineIn(run.danger), 'line-opacity': 1, 'width': e => 1.5 * edgeWidth(e) } },
        {
            selector: 'edge[run = "running"]',
            style: {
                'line-style': 'dashed',
                'line-dash-pattern': FLOW_DASH,
                'line-dash-offset': e => e.data('dash') ?? 0,
                ...lineIn(run.agent),
                'line-opacity': 1,
                'width': e => 1.5 * edgeWidth(e),
            },
        },
        { selector: '.hidden', style: { 'display': 'none' } },
        { selector: '.dim', style: { 'opacity': 0.1, 'text-opacity': 0 } },
        { selector: '.hushed', style: { 'text-opacity': 0 } },
        { selector: 'node.lit', style: { 'color': p.fg, 'label': nameOf, ...raise(10) } },
        {
            selector: 'edge.lit',
            style: { 'line-opacity': 1, 'width': e => Math.max(2, (e.data('weight') || 1) * 1.6) * held(e), ...raise(10) },
        },
        ...(options.edgeLabels === 'none' ? [] : [{ selector: 'edge.lit[?label]', style: { 'label': 'data(label)', ...edgeName } }]),
        {
            selector: 'node.match',
            style: {
                'border-width': n => (box(n) ? 2.5 : 2.5 * held(n)),
                'border-color': p.fg,
                'underlay-color': p.accent,
                'underlay-opacity': 0.55,
                'underlay-padding': n => (box(n) ? 5 : 6 * held(n)),
                'underlay-shape': n => (box(n) ? 'round-rectangle' : 'ellipse'),
                'color': p.fg,
                'label': nameOf,
            },
        },
        { selector: 'node:selected', style: { 'border-width': n => (box(n) ? 2.5 : 3 * held(n)), 'border-color': p.selected, 'color': p.fg, 'label': nameOf } },
        { selector: 'edge:selected', style: { 'line-opacity': 1, 'line-color': p.selected, 'target-arrow-color': p.selected, 'source-arrow-color': p.selected } },
        {
            selector: 'node.keyed',
            style: {
                'outline-width': n => (box(n) ? 3 : 3 * held(n)),
                'outline-color': p.ring,
                'outline-style': 'solid',
                'outline-opacity': 1,
                'label': nameOf,
                'color': p.fg,
                ...raise(20),
            },
        },
        { selector: '.entering', style: { 'opacity': 0 } },
        { selector: '.eh-handle', style: { 'width': 10, 'height': 10, 'shape': 'ellipse', 'background-color': p.brand, 'border-width': 0, 'label': '', 'background-image': 'none' } },
        { selector: '.eh-source, .eh-target', style: { 'border-width': 3, 'border-color': p.brand } },
        { selector: '.eh-preview, .eh-ghost-edge', style: { 'line-color': p.brand, 'target-arrow-color': p.brand, 'target-arrow-shape': 'triangle', 'line-style': 'dashed', 'line-opacity': 1, 'width': 2 } },
        { selector: '.eh-ghost-edge.eh-preview-active', style: { 'opacity': 0 } },
        { selector: '.eh-ghost-node', style: { 'width': 1, 'height': 1, 'opacity': 0, 'label': '' } },
    ].concat(resolveRules(extra, colours));
}
const PLUGINS = {
    dagre: 'cytoscape-dagre.js',
    fcose: 'cytoscape-fcose.js',
    edgehandles: 'cytoscape-edgehandles.js',
    expandCollapse: 'cytoscape-expand-collapse.js',
    layers: 'cytoscape-layers.js',
    bubblesets: 'cytoscape-bubblesets.js',
};
const pluginLoads = new Map();
function plugin(name) {
    if (!pluginLoads.has(name)) {
        const url = new URL('../lib/cytoscape/' + PLUGINS[name], import.meta.url).href;
        pluginLoads.set(name, import(url).then(m => {
            cytoscape.use(m.default);
            return m;
        }).catch(e => {
            pluginLoads.delete(name);
            throw e;
        }));
    }
    return pluginLoads.get(name);
}
const LAYOUT_NAMES = ['force', 'islands', 'rings', 'concentric', 'tree', 'dagre', 'fcose', 'grid', 'circle', 'preset'];
function knownLayout(name) {
    if (LAYOUT_NAMES.includes(name)) return true;
    try { return !!cytoscape('layout', name); } catch (e) { return false; }
}
const DIRECTIONS = { TB: 'TB', LR: 'LR', BT: 'BT', RL: 'RL', down: 'TB', right: 'LR', up: 'BT', left: 'RL' };
const SPACING = { compact: 0.7, normal: 1, loose: 1.45 };
const directionOf = value => own(DIRECTIONS, value) || 'TB';
const spacingOf = value => own(SPACING, value) ?? (Number(value) || 1);
const golden = Math.PI * (3 - Math.sqrt(5));
const most = (items, value, floor = -Infinity) => {
    let m = floor;
    const take = item => { m = Math.max(m, value(item)); };
    if (typeof items.forEach === 'function') items.forEach(take);
    else for (const item of items) take(item);
    return m;
};
const byId = (a, b) => (a.id() < b.id() ? -1 : a.id() > b.id() ? 1 : 0);
const holding = nodes => {
    const free = nodes.filter(n => !n.locked());
    free.lock();
    return free;
};
const settled = layout => new Promise(resolve => {
    if (layout.cy().destroyed()) return resolve();
    layout.one('layoutstop', resolve);
    layout.run();
});
const springs = (eles, count, spacing, overrides = {}) => eles.layout({
    name: 'cose',
    randomize: false,
    animate: false,
    fit: false,
    nodeRepulsion: n => (count > 120 ? 40000 : 6000) * spacing + 600 * (n.data('degree') || 0),
    nodeOverlap: 16,
    idealEdgeLength: e => spacing * (count > 120 ? 1.6 : 1) * (e.data('across') ? 90 : e.data('weight') > 1 ? 56 : 70),
    edgeElasticity: () => 100,
    componentSpacing: 48 * spacing,
    gravity: count > 120 ? 0.25 : 0.6,
    numIter: count > 600 ? 700 : count > 250 ? 1000 : 1500,
    refresh: 1e6,
    nodeDimensionsIncludeLabels: false,
    ...overrides,
});
async function rings(visible, options, spacing, box) {
    const names = options.labels === 'all' && options.nodes !== 'box';
    const nodes = visible.nodes().filter(n => !n.isParent());
    const maxDegree = most(nodes, n => n.data('degree') || 0, 1);
    const centre = visible.nodes('.focus').nonempty() ? visible.nodes('.focus') : visible.nodes('[?root]');
    const hops = new Map();
    if (centre.nonempty()) {
        visible.bfs({ roots: centre, visit: (node, edge, previous, index, depth) => { hops.set(node.id(), depth); } });
    }
    visible.layout({
        name: 'concentric',
        fit: false,
        animate: false,
        avoidOverlap: true,
        nodeDimensionsIncludeLabels: names,
        minNodeSpacing: 30 * spacing,
        boundingBox: box,
        concentric: n => (centre.nonempty() ? 10 - (hops.get(n.id()) ?? 9) : Math.round(6 * (n.data('degree') || 0) / maxDegree)),
        levelWidth: () => 1,
    }).run();
    const held = holding(centre);
    try {
        await settled(springs(visible, nodes.length, spacing, { initialTemp: 30, numIter: 160, gravity: 0.1, nodeDimensionsIncludeLabels: names }));
    } finally {
        held.unlock();
    }
}
async function arrangeWith(cy, name, options, frame) {
    const visible = cy.elements().not('.hidden').not('.eh-ghost, .eh-preview, .eh-handle');
    if (visible.nodes().length === 0) return;
    visible.nodes().forEach(n => { n.pstyle('display'); });
    const spacing = spacingOf(options.spacing);
    const direction = directionOf(options.direction);
    const box = options.nodes === 'box' || visible.nodes('[display = "box"]').nonempty();
    const count = visible.nodes().length;
    const aspect = aspectOf(frame);
    const names = options.labels === 'all' && !box;
    const scale = Math.max(1, Math.sqrt(count / 30));
    const fw = (frame && frame.clientWidth) || 900, fh = (frame && frame.clientHeight) || 500;
    const frameBox = { x1: 0, y1: 0, w: fw * scale * spacing, h: fh * scale * spacing };
    switch (name) {
        case 'preset': {
            const placed = visible.nodes().filter(n => n.data('x') !== null && n.data('x') !== undefined);
            cy.batch(() => placed.forEach(n => n.position({ x: n.data('x'), y: n.data('y') })));
            const loose = visible.nodes().not(placed).filter(n => !n.isParent());
            if (loose.nonempty()) {
                const held = holding(placed);
                try {
                    await settled(springs(visible, count, spacing, { randomize: false, nodeDimensionsIncludeLabels: names }));
                } finally {
                    held.unlock();
                }
            }
            return;
        }
        case 'grid':
        case 'circle':
            visible.layout({
                name,
                fit: false,
                animate: false,
                avoidOverlap: true,
                nodeDimensionsIncludeLabels: names,
                boundingBox: frameBox,
                spacingFactor: spacing * (box ? 1.1 : 1.25),
                sort: (a, b) => (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b),
                ...(name === 'grid' ? { condense: true } : {}),
            }).run();
            return;
        case 'concentric':
            visible.layout({
                name: 'concentric',
                fit: false,
                animate: false,
                avoidOverlap: true,
                nodeDimensionsIncludeLabels: names,
                boundingBox: frameBox,
                minNodeSpacing: (box ? 24 : 18) * spacing,
                concentric: n => n.data('root') ? 1e6 : (n.data('degree') || 0),
                levelWidth: nodes => Math.max(1, nodes.maxDegree() / 4),
            }).run();
            return;
        case 'tree': {
            const roots = visible.nodes('[?root]');
            const run = bb => visible.layout({
                name: 'breadthfirst',
                fit: false,
                animate: false,
                boundingBox: bb,
                directed: true,
                roots: roots.nonempty() ? roots : undefined,
                spacingFactor: 1,
                avoidOverlap: true,
                grid: false,
                circle: false,
            }).run();
            run(frameBox);
            const leaves = visible.nodes().filter(n => !n.isParent());
            const levels = new Map();
            leaves.forEach(n => {
                const y = Math.round(n.position('y'));
                levels.set(y, (levels.get(y) || 0) + 1);
            });
            const widest = most(levels.values(), v => v, 1);
            const sideways = direction === 'LR' || direction === 'RL';
            const boxW = box ? most(leaves, n => n.data('boxW') || 120) : 0;
            const boxH = box ? most(leaves, n => n.data('boxH') || 36) : 0;
            const beside = (box ? (sideways ? boxH + 20 : boxW + 28) : (sideways ? 30 : 112)) * (1 + (spacing - 1) * 0.5);
            const after = (box ? (sideways ? boxW + 72 : boxH + 64) : (sideways ? 190 : 92)) * spacing;
            const across = widest * beside;
            const shape = sideways ? 1 / aspect : aspect;
            const down = Math.max(Math.max(1, levels.size) * after, Math.min(across / shape, Math.max(1, levels.size) * after * 2.2));
            run({ x1: 0, y1: 0, w: across, h: down });
            orient(visible, direction);
            return;
        }
        case 'dagre':
            await plugin('dagre');
            if (cy.destroyed()) return;
            await settled(visible.layout({
                name: 'dagre',
                fit: false,
                animate: false,
                rankDir: direction,
                nodeSep: (box ? 28 : 36) * spacing,
                rankSep: (box ? 56 : 72) * spacing,
                edgeSep: 12 * spacing,
                ranker: 'network-simplex',
                edgeWeight: e => e.data('weight') || 1,
                nodeDimensionsIncludeLabels: !box,
                useDagreEdgeControlPoints: true,
            }));
            visible.edges().updateStyle();
            return;
        case 'fcose':
            await nestedSprings(visible, count, spacing, box);
            return;
        case 'rings':
            await rings(visible, options, spacing, frameBox);
            stretch(visible, aspect);
            return;
        case 'islands':
            cy.startBatch();
            try {
                if (await islands(cy, visible, aspect, spacing, frameBox)) return;
            } finally {
                cy.endBatch();
            }
            await rings(visible, options, spacing, frameBox);
            stretch(visible, aspect);
            return;
        case 'force':
        default:
            if (!LAYOUT_NAMES.includes(name) && knownLayout(name)) {
                await settled(visible.layout({ name, fit: false, animate: false }));
                return;
            }
            if (visible.nodes(':parent').nonempty()) {
                await nestedSprings(visible, count, spacing, box);
                stretch(visible, aspect);
                return;
            }
            if (visible.nodes('.focus').nonempty() || visible.nodes('[?root]').nonempty()) {
                await rings(visible, options, spacing, frameBox);
            } else {
                await settled(visible.layout({
                    name: 'concentric', fit: false, animate: false, avoidOverlap: true, minNodeSpacing: 24 * spacing,
                    boundingBox: frameBox, concentric: n => n.data('degree') || 0, levelWidth: () => 2,
                }));
                await settled(springs(visible, count, spacing, { nodeDimensionsIncludeLabels: names }));
            }
            stretch(visible, aspect);
            return;
    }
}
async function nestedSprings(visible, count, spacing, box) {
    await plugin('fcose');
    const turn = springsQueue.then(() => nestedSpringsNow(visible, count, spacing, box));
    springsQueue = turn.catch(() => {});
    return turn;
}
let springsQueue = Promise.resolve();
async function nestedSpringsNow(visible, count, spacing, box) {
    if (visible.cy().destroyed()) return;
    seeded();
    try {
        await settled(visible.layout({
            name: 'fcose',
            quality: count > 800 ? 'draft' : 'default',
            randomize: true,
            animate: false,
            fit: false,
            nodeRepulsion: () => 6500 * spacing,
            idealEdgeLength: () => (box ? 90 : 70) * spacing,
            nodeSeparation: 75 * spacing,
            packComponents: true,
            tile: true,
            nestingFactor: 0.1,
            gravity: 0.25,
            numIter: 2500,
            nodeDimensionsIncludeLabels: box,
        }));
    } finally {
        restoreRandom();
    }
    if (box) untangle(visible);
}
function untangle(visible) {
    const parents = visible.nodes(':parent');
    if (parents.empty()) return;
    const margin = 14;
    const depthOf = n => n.ancestors().length;
    const levels = new Map();
    visible.nodes().forEach(n => {
        const key = n.parent().nonempty() ? n.parent().id() : '';
        if (!levels.has(key)) levels.set(key, { depth: n.parent().nonempty() ? depthOf(n.parent()) + 1 : 0, items: [] });
        levels.get(key).items.push(n);
    });
    const order = [...levels.values()].filter(l => l.items.length > 1 && l.items.some(n => n.isParent()))
        .sort((a, b) => b.depth - a.depth);
    const move = (n, dx, dy) => (n.isParent() ? n.descendants().filter(d => !d.isParent()) : n).shift({ x: dx, y: dy });
    {
        for (const level of order) {
            const items = level.items.slice().sort(byId);
            const boxes = items.map(n => n.boundingBox({ includeLabels: true, includeOverlays: false }));
            for (let round = 0; round < 60; round++) {
                let moved = false;
                for (let i = 0; i < items.length; i++) {
                    for (let j = i + 1; j < items.length; j++) {
                        if (!items[i].isParent() && !items[j].isParent()) continue;
                        const a = boxes[i], b = boxes[j];
                        const ox = Math.min(a.x2, b.x2) - Math.max(a.x1, b.x1) + margin;
                        const oy = Math.min(a.y2, b.y2) - Math.max(a.y1, b.y1) + margin;
                        if (ox <= 0 || oy <= 0) continue;
                        const ax = (a.x1 + a.x2) / 2, bx = (b.x1 + b.x2) / 2, ay = (a.y1 + a.y2) / 2, by = (b.y1 + b.y2) / 2;
                        const [dx, dy] = ox < oy ? [(ax <= bx ? -1 : 1) * ox / 2, 0] : [0, (ay <= by ? -1 : 1) * oy / 2];
                        move(items[i], dx, dy);
                        move(items[j], -dx, -dy);
                        for (const [k, s] of [[i, 1], [j, -1]]) {
                            boxes[k] = { x1: boxes[k].x1 + s * dx, x2: boxes[k].x2 + s * dx, y1: boxes[k].y1 + s * dy, y2: boxes[k].y2 + s * dy };
                        }
                        moved = true;
                    }
                }
                if (!moved) break;
            }
        }
    }
}
function stretch(visible, aspect) {
    const box = visible.nodes().boundingBox({ includeLabels: false });
    const factor = Math.min(Math.max(aspect / (box.w / Math.max(box.h, 1)), 1), 1.8);
    if (factor > 1.05 && box.w > 0) {
        const centre = box.x1 + box.w / 2;
        visible.cy().batch(() => visible.nodes().filter(n => !n.isParent())
            .forEach(n => n.position('x', centre + (n.position('x') - centre) * factor)));
    }
}
function orient(visible, direction) {
    if (direction === 'TB') return;
    visible.cy().batch(() => visible.nodes().filter(n => !n.isParent()).forEach(n => {
        const { x, y } = n.position();
        if (direction === 'BT') n.position({ x, y: -y });
        else if (direction === 'LR') n.position({ x: y, y: x });
        else n.position({ x: -y, y: x });
    }));
}
const aspectOf = frame => (frame && frame.clientWidth > 0 && frame.clientHeight > 0 ? frame.clientWidth / frame.clientHeight : 1.8);
let savedRandom = null;
function seeded() {
    savedRandom = Math.random;
    let s = 0x2f6b4a;
    Math.random = () => {
        s = (s * 1664525 + 1013904223) >>> 0;
        return s / 4294967296;
    };
}
function restoreRandom() {
    if (savedRandom) {
        Math.random = savedRandom;
        savedRandom = null;
    }
}
function componentsOf(visible) {
    const up = new Map();
    const root = id => {
        let r = id;
        while (up.get(r) !== r) r = up.get(r);
        for (let x = id; up.get(x) !== r;) { const next = up.get(x); up.set(x, r); x = next; }
        return r;
    };
    visible.nodes().forEach(n => { up.set(n.id(), n.id()); });
    visible.edges().forEach(e => {
        const a = e.data('source'), b = e.data('target');
        if (!up.has(a) || !up.has(b)) return;
        const ra = root(a), rb = root(b);
        if (ra !== rb) up.set(ra, rb);
    });
    const parts = new Map();
    visible.nodes().forEach(n => {
        if (n.isParent()) return;
        const r = root(n.id());
        if (!parts.has(r)) parts.set(r, []);
        parts.get(r).push(n);
    });
    return parts.values();
}
function shapeKey(group, own, spacing) {
    const parts = [spacing];
    for (const n of group) {
        const d = n.data();
        parts.push(d.id, d.size, d.degree, d.display === 'box' ? d.boxW + 'x' + d.boxH : '');
    }
    own.forEach(e => {
        const d = e.data();
        parts.push(d.id, d.source, d.target, d.weight, d.across ? 1 : 0);
    });
    return parts.join('\u0001');
}
async function islands(cy, visible, aspect, spacing, box) {
    const nodes = visible.nodes().filter(n => !n.isParent());
    const gap = 36 * spacing;
    const home = new Map();
    nodes.forEach(n => {
        if (n.data('group')) home.set(n.id(), n.data('group'));
    });
    if (home.size === 0) return false;
    for (let pass = 0; pass < 2; pass++) {
        nodes.filter(n => !home.has(n.id())).forEach(n => {
            const theirs = new Set(n.neighborhood().nodes().not('.hidden').map(o => home.get(o.id())).filter(Boolean));
            if (theirs.size === 1) home.set(n.id(), [...theirs][0]);
        });
    }
    const members = new Map();
    nodes.forEach(n => {
        const island = home.get(n.id());
        if (island) {
            if (!members.has(island)) members.set(island, []);
            members.get(island).push(n);
        }
    });
    const g = cy.scratch('_sedna');
    const shapes = g ? (g.islandShapes = g.islandShapes || new Map()) : new Map();
    const seen = new Set();
    const shape = async (key, group) => {
        const eles = cy.collection(group);
        const own = eles.edgesWith(eles).not('.hidden');
        const sign = shapeKey(group, own, spacing);
        const kept = shapes.get(key) || [];
        const known = kept.find(k => k.sign === sign);
        seen.add(key);
        if (known) {
            cy.batch(() => group.forEach(n => n.position(known.at.get(n.id()))));
            return eles;
        }
        group.forEach((n, i) => {
            const r = gap * Math.sqrt(i);
            n.position({ x: r * Math.cos(i * golden), y: r * Math.sin(i * golden) });
        });
        if (group.length > 2 && own.nonempty()) await settled(springs(eles.union(own), group.length, spacing, { numIter: 400 }));
        const tied = eles.filter(n => n.connectedEdges().intersection(own).nonempty());
        const loose = eles.not(tied);
        if (loose.nonempty()) {
            const core = tied.nonempty() ? tied.boundingBox({ includeLabels: false }) : { x1: 0, y1: 0, w: 0, h: 0 };
            const cx = core.x1 + core.w / 2;
            const cy0 = core.y1 + core.h / 2;
            const shore = Math.max(core.w, core.h) / 2;
            loose.forEach((n, j) => {
                const r = shore + gap * Math.sqrt(j + 1);
                n.position({ x: cx + r * Math.cos(j * golden), y: cy0 + r * Math.sin(j * golden) });
            });
        }
        shapes.set(key, [{ sign, at: new Map(group.map(n => [n.id(), Object.assign({}, n.position())])) }, ...kept].slice(0, 2));
        return eles;
    };
    const list = [];
    for (const [id, group] of members) {
        group.sort((a, b) => (a.id() === id ? -1 : b.id() === id ? 1 : (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b)));
        const eles = await shape('group:' + id, group);
        const heart = cy.getElementById(id);
        const cluster = group[0].data('cluster') ?? (heart.nonempty() ? heart.data('cluster') : null) ?? '';
        list.push({ id, eles, cluster });
    }
    const orphans = new Set();
    for (const parted of componentsOf(visible)) {
        if (parted.length > 1 && parted.every(n => !home.has(n.id()))) {
            const group = parted.sort((a, b) => (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b));
            group.forEach(n => orphans.add(n.id()));
            const eles = await shape('part:' + group[0].id(), group);
            list.push({ id: group[0].id(), eles, cluster: '' });
        }
    }
    for (const key of [...shapes.keys()]) if (!seen.has(key)) shapes.delete(key);
    list.forEach(i => { i.box = i.eles.boundingBox({ includeLabels: false }); });
    const weight = new Map();
    list.forEach(i => weight.set(i.cluster, (weight.get(i.cluster) ?? 0) + i.eles.length));
    list.sort((a, b) => weight.get(b.cluster) - weight.get(a.cluster)
        || (a.cluster < b.cluster ? -1 : a.cluster > b.cluster ? 1 : 0)
        || b.eles.length - a.eles.length || (a.id < b.id ? -1 : 1));
    const pack = (rowWidth, move) => {
        let x = 0, y = 0, rowHeight = 0, width = 0;
        const put = (eles, box) => {
            if (x > 0 && x + box.w > rowWidth) {
                x = 0;
                y += rowHeight + gap * 2;
                rowHeight = 0;
            }
            if (move) {
                const dx = x - box.x1;
                const dy = y - box.y1;
                eles.forEach(n => n.position({ x: n.position('x') + dx, y: n.position('y') + dy }));
            }
            width = Math.max(width, x + box.w);
            x += box.w + gap * 2;
            rowHeight = Math.max(rowHeight, box.h);
        };
        list.forEach(i => put(i.eles, i.box));
        return { put, scale: Math.min(aspect / Math.max(width, 1), 1 / Math.max(y + rowHeight, 1)) };
    };
    const widest = most(list, i => i.box.w, 1);
    const total = list.reduce((sum, i) => sum + i.box.w + gap * 2, 0);
    let rowWidth = widest;
    for (let k = 0, best = 0; k <= 48; k++) {
        const candidate = widest + (total - widest) * k / 48;
        const { scale } = pack(candidate, false);
        if (scale > best * 1.001) {
            best = scale;
            rowWidth = candidate;
        }
    }
    const { put } = pack(rowWidth, true);
    const shores = list.map(i => {
        const b = i.eles.boundingBox({ includeLabels: false });
        return { x: b.x1 + b.w / 2, y: b.y1 + b.h / 2, r: Math.max(b.w, b.h) / 2 + gap / 2 };
    });
    const placed = new Set([...home.keys(), ...orphans]);
    const cellSize = 64;
    const occupied = new Map();
    const occupy = (x, y, r) => {
        const key = Math.floor(x / cellSize) * 65536 + Math.floor(y / cellSize);
        if (!occupied.has(key)) occupied.set(key, []);
        occupied.get(key).push({ x, y, r });
    };
    const clear = (px, py, r) => {
        const cx = Math.floor(px / cellSize), cy1 = Math.floor(py / cellSize);
        for (let gx = cx - 1; gx <= cx + 1; gx++) {
            for (let gy = cy1 - 1; gy <= cy1 + 1; gy++) {
                for (const b of occupied.get(gx * 65536 + gy) ?? []) {
                    if (Math.hypot(px - b.x, py - b.y) < r + b.r) return false;
                }
            }
        }
        return true;
    };
    nodes.filter(n => placed.has(n.id())).forEach(n => occupy(n.position('x'), n.position('y'), Math.max(n.data('size') || 20, 20) * 0.75));
    let waiting = nodes.filter(n => !placed.has(n.id())).toArray().sort((a, b) => (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b));
    for (let pass = 0; pass < 3 && waiting.length > 0; pass++) {
        const still = [];
        waiting.forEach((n, i) => {
            const near = n.neighborhood().nodes().not('.hidden').filter(o => placed.has(o.id()));
            if (near.empty()) {
                still.push(n);
                return;
            }
            const mx = near.reduce((s, o) => s + o.position('x'), 0) / near.length;
            const my = near.reduce((s, o) => s + o.position('y'), 0) / near.length;
            const r = Math.max(n.data('size') || 20, 20) * 0.75 + 6;
            let px = mx + gap * Math.cos(i * golden), py = my + gap * Math.sin(i * golden);
            for (let k = 1; k < 32 && !clear(px, py, r); k++) {
                const a = (i + k) * golden, d = gap * (1 + k * 0.35);
                px = mx + d * Math.cos(a);
                py = my + d * Math.sin(a);
            }
            for (const shore of shores) {
                const d = Math.hypot(px - shore.x, py - shore.y);
                if (d < shore.r) {
                    const a = d > 0 ? Math.atan2(py - shore.y, px - shore.x) : i * golden;
                    px = shore.x + shore.r * Math.cos(a);
                    py = shore.y + shore.r * Math.sin(a);
                }
            }
            n.position({ x: px, y: py });
            occupy(px, py, r);
            placed.add(n.id());
        });
        waiting = still;
    }
    if (waiting.length > 0) {
        waiting.forEach((n, i) => {
            const r = gap * Math.sqrt(i);
            n.position({ x: r * Math.cos(i * golden), y: r * Math.sin(i * golden) });
        });
        const eles = cy.collection(waiting);
        put(eles, eles.boundingBox({ includeLabels: false }));
    }
    return true;
}
function fitView(g, eles, animate) {
    const cy = g.cy;
    const nodes = (eles || cy.elements().not('.hidden')).nodes();
    if (nodes.length === 0) return;
    cy.resize();
    const { zoom, pan } = fitOf(g, nodes);
    cy.stop();
    if (animate && glides(g)) {
        cy.animate({ zoom, pan }, { duration: 260, easing: 'ease-out-cubic', complete: () => settle(g) });
    } else {
        cy.viewport({ zoom, pan });
        settle(g);
    }
}
const ownZoom = g => g.cy.zoom() / g.options.scale;
const holdFloor = g => (g.options.hold ? MIN_ZOOM : HOLD_FLOOR);
function fitOf(g, nodes) {
    const cy = g.cy;
    const s = g.options.scale;
    const box = nodes.boundingBox({ includeLabels: g.options.nodes === 'box', includeOverlays: false });
    const side = g.options.nodes === 'box' ? null : labelSide(g.options);
    const sideRoom = side && side !== 'bottom' ? Math.min((LABEL_MAX_PX * 0.8 + 16) * s, cy.width() * 0.3) : 0;
    const labelRoom = g.options.nodes === 'box' ? Math.min(48 * s, cy.width() * 0.2)
        : sideRoom || Math.min((LABEL_MAX_PX + 32) * s, cy.width() * 0.2);
    const room = {
        w: Math.max(cy.width() - labelRoom, cy.width() / 2),
        h: Math.max(cy.height() - Math.min((g.options.nodes === 'box' ? 48 : sideRoom ? 32 : 88) * s, cy.height() * 0.2), cy.height() / 2),
    };
    const zoom = Math.max(cy.minZoom(), Math.min(room.w / Math.max(box.w, 1), room.h / Math.max(box.h, 1), fitCeiling(g)));
    const shift = side === 'right' ? -sideRoom / 2 : side === 'left' ? sideRoom / 2 : 0;
    const pan = { x: cy.width() / 2 + shift - zoom * (box.x1 + box.w / 2), y: cy.height() / 2 - zoom * (box.y1 + box.h / 2) };
    return { zoom, pan };
}
function fitCeiling(g) {
    const asked = g.options.fitMax;
    const ceiling = asked > 0 ? asked : g.options.nodes === 'box' ? 1.1 : 1.25;
    return Math.min(ceiling * g.options.scale, g.cy.maxZoom());
}
function pixelRatioOf(el) {
    const screen = window.devicePixelRatio || 1;
    return Math.min(screen * cssZoomOf(el), Math.max(screen, 3));
}
function cssZoomOf(el) {
    if (!el) return 1;
    if (el.currentCSSZoom > 0) return el.currentCSSZoom;
    const laid = el.offsetWidth;
    const drawn = laid > 0 ? el.getBoundingClientRect().width : 0;
    return drawn > 0 ? drawn / laid : 1;
}
function fitAll(g) {
    g.touched = false;
    if (g.travelling || g.laying) g.refit = true;
    else fitView(g, null, true);
}
const stepOf = zoom => Math.exp(Math.round(Math.log(zoom) / 0.07) * 0.07);
function nearView(cy) {
    const e = cy.extent();
    return { x1: e.x1 - e.w, x2: e.x2 + e.w, y1: e.y1 - e.h, y2: e.y2 + e.h };
}
const inBox = (p, b) => p.x >= b.x1 && p.x <= b.x2 && p.y >= b.y1 && p.y <= b.y2;
function rezoom(g, all) {
    const cy = g.cy;
    if (g.options.nodes === 'box') return;
    const step = g.step;
    const box = all ? null : nearView(cy);
    const labelled = g.options.edgeLabels === 'always';
    const stale = [];
    cy.nodes().forEach(n => {
        const z = n.data('zoom');
        if (z === step || n.hasClass('hidden') || (box && !inBox(n.position(), box))) return;
        if (step <= 1 && (z ?? 1) <= 1 && n.hasClass('unlabelled') && !n.hasClass('lit')) return;
        if (!n.isParent() && n.data('display') === 'box') return;
        stale.push(n);
    });
    cy.edges().forEach(e => {
        const z = e.data('zoom');
        if (z === step || e.hasClass('hidden') || !(step > 1 || z > 1 || labelled || e.hasClass('lit'))) return;
        if (!box || inBox(e.source().position(), box) || inBox(e.target().position(), box)) stale.push(e);
    });
    if (stale.length) cy.batch(() => cy.collection(stale).data('zoom', step));
}
function named(g, nodes) {
    const stale = nodes.filter(n => n.data('zoom') !== g.step);
    if (stale.nonempty()) stale.data('zoom', g.step);
    nodes.removeClass('unlabelled');
}
function settle(g) {
    const cy = g.cy;
    if (g.laying) return;
    if (g.options.nodes !== 'box') g.step = stepOf(Math.max(ownZoom(g), holdFloor(g)));
    rezoom(g, false);
    declutter(g);
    const pan = cy.pan();
    g.settledAt = { zoom: cy.zoom(), x: pan.x, y: pan.y };
    g.minimap?.draw();
}
const insists = n => n.data('root') || n.hasClass('focus') || n.hasClass('match') || n.hasClass('keyed')
    || n.hasClass('lit') || n.selected();
const labelPriority = n => (insists(n) ? 1e6 : 0)
    + (n.data('hub') ? 2000 : 0)
    + (n.data('marked') ? 800 : 0)
    + (n.isParent() ? 1500 : 0)
    + (n.data('degree') || 0)
    - (n.data('muted') ? 100 : 0);
function declutter(g, all) {
    placeNames(g, all);
    if (g.veil && (g.veil.up || (g.hushed && g.hushed.nonempty()))) hush(g);
}
function placeNames(g, all) {
    const cy = g.cy;
    const box = cy.container() && !all ? nearView(cy) : null;
    const nodes = cy.nodes().filter(n => !n.hasClass('hidden') && (n.data('display') || g.options.nodes) !== 'box'
        && !n.isParent() && (!box || inBox(n.position(), box)));
    if (nodes.empty()) return;
    if (g.options.labels === 'all') {
        cy.batch(() => named(g, nodes.filter('.unlabelled')));
        return;
    }
    if (g.options.labels === 'none') {
        cy.batch(() => {
            named(g, nodes.filter(n => insists(n) && n.hasClass('unlabelled')));
            nodes.filter(n => !insists(n) && !n.hasClass('unlabelled')).addClass('unlabelled');
        });
        return;
    }
    const s = g.options.scale;
    const cell = 80 * s;
    const grid = new Map();
    const cells = (b, fn) => {
        for (let gx = Math.floor(b.x1 / cell); gx <= Math.floor(b.x2 / cell); gx++) {
            for (let gy = Math.floor(b.y1 / cell); gy <= Math.floor(b.y2 / cell); gy++) {
                if (fn(gx * 65536 + gy)) return true;
            }
        }
        return false;
    };
    const collides = b => cells(b, key => (grid.get(key) ?? []).some(o => b.x1 < o.x2 && o.x1 < b.x2 && b.y1 < o.y2 && o.y1 < b.y2));
    const place = b => cells(b, key => {
        if (!grid.has(key)) grid.set(key, []);
        grid.get(key).push(b);
        return false;
    });
    const zoom = cy.zoom();
    const pan = cy.pan();
    const side = labelSide(g.options);
    const font = g.colours.font();
    const show = [];
    const hide = [];
    const drawn = zoom / (g.step || 1);
    const ranked = nodes.toArray().map(n => ({ n, rank: labelPriority(n) })).sort((a, b) => b.rank - a.rank);
    for (const { n, rank } of ranked) {
        const at = n.position();
        const x = at.x * zoom + pan.x;
        const y = at.y * zoom + pan.y;
        const r = ((n.data('size') || 20) * held(n) * zoom) / 2;
        const px = labelPx(n) * drawn;
        const bold = n.data('hub') || n.data('root') || n.hasClass('focus');
        const w = Math.min(textWidth(n.data('label') || '', bold ? 600 : 400, px, font), LABEL_MAX_PX * drawn) + 8 * s;
        const box = side === 'right' ? { x1: x + r + 3 * s, x2: x + r + 3 * s + w, y1: y - px * 0.85, y2: y + px * 0.85 }
            : side === 'left' ? { x1: x - r - 3 * s - w, x2: x - r - 3 * s, y1: y - px * 0.85, y2: y + px * 0.85 }
                : { x1: x - w / 2, x2: x + w / 2, y1: y + r + 2 * s, y2: y + r + 2 * s + px * 1.7 };
        place({ x1: x - r, x2: x + r, y1: y - r, y2: y + r });
        const labelled = rank >= 1e6 || !collides(box);
        if (labelled) place(box);
        if (labelled === n.hasClass('unlabelled')) (labelled ? show : hide).push(n);
    }
    if (show.length || hide.length) {
        cy.batch(() => {
            named(g, cy.collection(show));
            cy.collection(hide).addClass('unlabelled');
        });
    }
}
const VEIL = 0.9;
function light(g, node) {
    const cy = g.cy;
    let near = node && node.nonempty() ? node.closedNeighborhood() : cy.collection();
    if (node && node.nonempty() && node.isParent()) {
        const inside = node.descendants();
        near = near.union(inside).union(inside.edgesWith(inside));
    }
    near = near.not('.hidden');
    const lit = g.lit || cy.collection();
    const veil = veilOf(g);
    const keep = near.union(near.ancestors());
    cy.batch(() => {
        lit.not(near).removeClass('lit');
        near.not(lit).addClass('lit');
        if (!veil) {
            const kept = g.kept || cy.collection();
            if (near.empty()) cy.elements('.dim').removeClass('dim');
            else if (kept.empty()) cy.elements().not(keep).addClass('dim');
            else {
                kept.not(keep).addClass('dim');
                keep.not(kept).removeClass('dim');
            }
        }
        if (g.options.nodes !== 'box') near.filter(e => e.data('zoom') !== g.step).data('zoom', g.step);
    });
    g.lit = near;
    g.kept = near.empty() ? cy.collection() : keep;
    if (veil) {
        if (near.empty()) veil.hide();
        else veil.show(keep);
        hush(g);
        return;
    }
    const hullLayer = g.bb && g.bb.layer && g.bb.layer.node;
    if (hullLayer) {
        hullLayer.style.transition = reducedMotion() ? '' : 'opacity 160ms';
        hullLayer.style.opacity = near.empty() ? '' : '0.35';
    }
}
function hush(g) {
    const cy = g.cy;
    const want = [];
    if (g.veil && g.veil.up) {
        const box = nearView(cy);
        const kept = g.kept || cy.collection();
        const zoom = cy.zoom();
        cy.nodes().forEach(n => {
            if (n.hasClass('hidden') || kept.has(n) || !inBox(n.position(), box)) return;
            const dot = !n.isParent() && (n.data('display') || g.options.nodes) !== 'box';
            if (dot && (n.hasClass('unlabelled') && !insists(n) || labelSize(n) * zoom < MIN_NAME_PX * g.options.scale)) return;
            want.push(n);
        });
        if (g.options.edgeLabels === 'always') {
            cy.edges().forEach(e => {
                if (e.hasClass('hidden') || kept.has(e) || !e.data('label')) return;
                if (inBox(e.source().position(), box) || inBox(e.target().position(), box)) want.push(e);
            });
        }
    }
    const had = g.hushed || cy.collection();
    const next = cy.collection(want);
    if (had.empty() && next.empty()) return;
    cy.batch(() => {
        had.not(next).removeClass('hushed');
        next.not(had).addClass('hushed');
    });
    g.hushed = next;
}
function veilOf(g) {
    if (g.veil !== undefined) return g.veil;
    const cy = g.cy;
    const container = cy.container();
    if (!container) return null;
    let r = null;
    try { r = cy.renderer(); } catch (e) { r = null; }
    const nodeLayer = () => container.querySelector('canvas[data-id$="-node"]');
    if (g.options.renderer === 'webgl' || !r || typeof r.drawCachedElement !== 'function'
        || typeof r.getPixelRatio !== 'function' || !nodeLayer()) {
        g.veil = null;
        g.options.veil = false;
        restyle(g);
        return null;
    }
    const canvas = document.createElement('canvas');
    canvas.setAttribute('data-graph-veil', '');
    canvas.setAttribute('aria-hidden', 'true');
    canvas.style.cssText = 'position:absolute;left:0;top:0;width:100%;height:100%;pointer-events:none;opacity:0';
    const ctx = canvas.getContext('2d');
    let shown = null, up = false, fading = 0;
    const place = () => {
        const layer = nodeLayer();
        if (!layer) return;
        if (canvas.previousElementSibling !== layer) layer.after(canvas);
        canvas.style.zIndex = layer.style.zIndex;
    };
    const draw = () => {
        if (!shown || cy.destroyed()) return;
        const pr = r.getPixelRatio();
        const w = Math.max(1, Math.round(cy.width() * pr)), h = Math.max(1, Math.round(cy.height() * pr));
        if (canvas.width !== w || canvas.height !== h) {
            canvas.width = w;
            canvas.height = h;
        }
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, w, h);
        ctx.globalAlpha = VEIL;
        ctx.fillStyle = palette(g.colours).ground;
        ctx.fillRect(0, 0, w, h);
        ctx.globalAlpha = 1;
        const zoom = cy.zoom(), pan = cy.pan();
        ctx.setTransform(zoom * pr, 0, 0, zoom * pr, pan.x * pr, pan.y * pr);
        const extent = cy.extent();
        for (const ele of shown) {
            if (!ele.removed()) r.drawCachedElement(ctx, ele, pr, extent);
        }
    };
    cy.on('render', draw);
    const ordered = eles => {
        const depth = n => n.ancestors().length;
        const groups = eles.nodes().filter(n => n.isParent()).toArray().sort((a, b) => depth(a) - depth(b));
        const top = n => n.hasClass('keyed') || n.selected() || n.hasClass('focus');
        const records = eles.nodes().filter(n => !n.isParent());
        return groups.concat(eles.edges().toArray(), records.filter(n => !top(n)).toArray(), records.filter(top).toArray());
    };
    g.veil = {
        canvas,
        show(eles) {
            clearTimeout(fading);
            shown = ordered(eles);
            place();
            draw();
            if (!up) {
                up = true;
                canvas.style.transition = reducedMotion() ? '' : 'opacity 160ms';
                canvas.style.opacity = '1';
            }
        },
        hide() {
            if (!up) return;
            up = false;
            canvas.style.transition = reducedMotion() ? '' : 'opacity 160ms';
            canvas.style.opacity = '0';
            clearTimeout(fading);
            fading = setTimeout(() => { if (!up) shown = null; }, 200);
        },
        get up() { return up; },
        destroy() {
            clearTimeout(fading);
            if (!cy.destroyed()) cy.off('render', draw);
            canvas.remove();
        },
    };
    return g.veil;
}
const resting = g => (g.keyed && g.keyed.nonempty() ? g.keyed : g.selected && g.selected.nonempty() ? g.selected : null);
function filterOf(spec) {
    const s = spec || {};
    const fields = o => {
        const out = {};
        for (const [k, v] of Object.entries(o || {})) {
            if (v === undefined || v === null) continue;
            out[k] = new Set((Array.isArray(v) ? v : [v]).map(String));
        }
        return out;
    };
    return {
        nodes: fields(s.nodes),
        except: fields(s.except),
        edges: fields(s.edges),
        edgesExcept: fields(s.edgesExcept),
        hide: new Set((s.hide || []).map(String)),
        muted: s.muted !== false,
        isolated: s.isolated !== false,
        focus: s.focus ? String(s.focus) : null,
        depth: Math.max(1, Math.min(Number(s.depth) || 1, 6)),
    };
}
function valuesOf(data, field) {
    const v = valueOf(data, field);
    return Array.isArray(v) ? v : v === undefined || v === null || v === '' ? [] : [v];
}
function passes(ele, only, not) {
    const data = ele.data();
    for (const [field, allowed] of Object.entries(only)) {
        if (!valuesOf(data, field).some(x => allowed.has(String(x)))) return false;
    }
    for (const [field, refused] of Object.entries(not || {})) {
        if (valuesOf(data, field).some(x => refused.has(String(x)))) return false;
    }
    return true;
}
function applyFilter(g, spec) {
    const cy = g.cy;
    const f = filterOf(spec);
    g.filter = spec || {};
    const pinned = n => n.id() === f.focus;
    const hidden = new Set();
    const shown = ele => !hidden.has(ele);
    const nodes = cy.nodes();
    nodes.forEach(n => {
        if (!pinned(n) && (f.hide.has(n.id()) || !passes(n, f.nodes, f.except) || (!f.muted && n.data('muted')))) hidden.add(n);
    });
    nodes.forEach(n => { if (hidden.has(n) && n.isParent()) n.descendants().forEach(d => hidden.add(d)); });
    nodes.forEach(n => { if (!hidden.has(n) && n.isChild()) n.ancestors().forEach(a => hidden.delete(a)); });
    cy.edges().forEach(e => {
        if (f.hide.has(e.id()) || !passes(e, f.edges, f.edgesExcept) || hidden.has(e.source()[0]) || hidden.has(e.target()[0])) hidden.add(e);
    });
    if (!f.isolated) {
        nodes.forEach(n => {
            if (shown(n) && !n.isParent() && !pinned(n) && !n.data('root') && !n.connectedEdges().some(shown)) hidden.add(n);
        });
        nodes.forEach(p => { if (shown(p) && p.isParent() && !p.descendants().some(shown)) hidden.add(p); });
    }
    const focus = f.focus ? cy.getElementById(f.focus) : null;
    const focused = focus && focus.nonempty() && shown(focus[0]) ? focus : cy.collection();
    if (focused.nonempty()) {
        let reach = focused;
        for (let i = 0; i < f.depth; i++) reach = reach.union(reach.neighborhood().filter(shown));
        const keep = reach.union(reach.nodes().edgesWith(reach.nodes()).filter(shown)).union(reach.ancestors());
        cy.elements().forEach(e => { if (!keep.has(e)) hidden.add(e); });
    }
    cy.batch(() => {
        cy.elements().filter(e => hidden.has(e) !== e.hasClass('hidden')).toggleClass('hidden');
        cy.nodes('.focus').not(focused).removeClass('focus');
        focused.not('.focus').addClass('focus');
    });
    if (g.selected && g.selected.hasClass('hidden')) select(g, null);
    if (g.keyed && g.keyed.hasClass('hidden')) g.keyed = null;
    if (g.tip?.showing && (g.tip.showing.removed() || g.tip.showing.hasClass('hidden'))) g.tip.hide();
}
function searchText(n) {
    const d = n.data();
    return [d.label, d.id, d.meta, d.kind, ...(d.tags || [])].filter(Boolean).join(' ').toLowerCase();
}
function search(g, text, fit = true) {
    const cy = g.cy;
    const words = String(text ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean);
    g.query = words.join(' ');
    const matches = words.length
        ? cy.nodes().filter(n => {
            if (n.hasClass('hidden')) return false;
            const hay = searchText(n);
            return words.every(w => hay.includes(w));
        })
        : cy.collection();
    cy.batch(() => {
        cy.nodes('.match').not(matches).removeClass('match');
        matches.not('.match').addClass('match');
    });
    if (fit && matches.nonempty()) fitView(g, matches.closedNeighborhood().not('.hidden'), true);
    else declutter(g);
    return matches;
}
function stats(g) {
    const cy = g.cy;
    const shownNodes = cy.nodes().not('.hidden').filter(n => !n.hasClass('eh-ghost') && !n.hasClass('eh-handle'));
    return {
        nodes: shownNodes.length,
        edges: cy.edges().not('.hidden').filter(e => !e.hasClass('eh-preview') && !e.hasClass('eh-ghost-edge')).length,
        matches: cy.nodes('.match').length,
        totalNodes: g.model.nodes.length,
        totalEdges: g.model.edges.length,
        selected: g.selected && g.selected.nonempty() ? g.selected.id() : null,
    };
}
const FLOW_TICK = 50;
const FLOW_DASH = [6, 4];
const FLOW_PERIOD = FLOW_DASH[0] + FLOW_DASH[1];
function durationOf(g, token) {
    const m = /^(\d*\.?\d+)(ms|s)$/.exec(getComputedStyle(g.el).getPropertyValue(token).trim());
    return m ? Number(m[1]) * (m[2] === 's' ? 1000 : 1) : 0;
}
const runningOf = g => g.cy.elements('node[state = "running"], edge[run = "running"]');
function rest(g, eles) {
    const moved = eles.filter(e => !e.removed() && (e.data('dash') !== undefined || e.data('breath') !== undefined));
    if (moved.empty()) return;
    g.cy.batch(() => moved.removeData('dash breath'));
    moved.updateStyle();
}
function flow(g) {
    const cy = g.cy;
    if (!cy || g.disposed || cy.destroyed()) return;
    const now = runningOf(g);
    rest(g, (g.running || cy.collection()).not(now));
    g.running = now;
    const pace = { dash: durationOf(g, '--progress-duration'), breath: durationOf(g, '--pulse-duration') };
    const still = now.empty() || reducedMotion() || document.visibilityState !== 'visible' || !cy.container()
        || (pace.dash <= 0 && pace.breath <= 0);
    if (still) {
        clearTimeout(g.flowing);
        g.flowing = 0;
        rest(g, now);
        return;
    }
    g.pace = pace;
    if (g.flowing) return;
    const begun = Date.now();
    const tick = () => {
        g.flowing = 0;
        if (g.disposed || cy.destroyed()) return;
        const t = Date.now() - begun;
        const { dash, breath } = g.pace;
        const shown = g.running.filter(e => !e.removed() && !e.hasClass('hidden'));
        const links = shown.edges(), records = shown.nodes();
        cy.batch(() => {
            if (dash > 0 && links.nonempty()) links.data('dash', -(((t / dash) * FLOW_PERIOD) % FLOW_PERIOD));
            if (breath > 0 && records.nonempty()) records.data('breath', (1 - Math.cos((2 * Math.PI * t) / breath)) / 2);
        });
        g.flowing = setTimeout(tick, FLOW_TICK);
    };
    tick();
}
function minimap(g, canvas) {
    const cy = g.cy;
    const buffer = document.createElement('canvas');
    let scale = 1, ox = 0, oy = 0, dirty = true, timer = 0, dragging = false;
    function measure() {
        const dpr = Math.min(pixelRatioOf(canvas), 3);
        const w = Math.max(1, Math.round(canvas.clientWidth * dpr));
        const h = Math.max(1, Math.round(canvas.clientHeight * dpr));
        if (canvas.width !== w || canvas.height !== h) {
            canvas.width = buffer.width = w;
            canvas.height = buffer.height = h;
            dirty = true;
        }
        return dpr;
    }
    function paintRecords(dpr) {
        const ctx = buffer.getContext('2d');
        ctx.clearRect(0, 0, buffer.width, buffer.height);
        const nodes = cy.nodes().not('.hidden').filter(n => !n.isParent() && !n.hasClass('eh-ghost') && !n.hasClass('eh-handle'));
        if (nodes.empty()) return;
        const bb = { x1: Infinity, y1: Infinity, x2: -Infinity, y2: -Infinity };
        nodes.forEach(n => {
            const { x, y } = n.position();
            const d = n.data();
            const box = (d.display || g.options.nodes) === 'box';
            const hw = (box ? d.boxW || 120 : (d.size || 20) * held(n)) / 2;
            const hh = box ? (d.boxH || 36) / 2 : hw;
            if (x - hw < bb.x1) bb.x1 = x - hw;
            if (x + hw > bb.x2) bb.x2 = x + hw;
            if (y - hh < bb.y1) bb.y1 = y - hh;
            if (y + hh > bb.y2) bb.y2 = y + hh;
        });
        bb.w = bb.x2 - bb.x1;
        bb.h = bb.y2 - bb.y1;
        const pad = 8 * dpr;
        scale = Math.min((buffer.width - pad * 2) / Math.max(bb.w, 1), (buffer.height - pad * 2) / Math.max(bb.h, 1));
        ox = pad + (buffer.width - pad * 2 - bb.w * scale) / 2 - bb.x1 * scale;
        oy = pad + (buffer.height - pad * 2 - bb.h * scale) / 2 - bb.y1 * scale;
        const p = palette(g.colours);
        const edges = cy.edges().not('.hidden');
        if (edges.length < 6000) {
            ctx.strokeStyle = p.line;
            ctx.globalAlpha = 0.35;
            ctx.lineWidth = Math.max(0.5, dpr * 0.6);
            ctx.beginPath();
            edges.forEach(e => {
                const s = e.source().position(), t = e.target().position();
                ctx.moveTo(ox + s.x * scale, oy + s.y * scale);
                ctx.lineTo(ox + t.x * scale, oy + t.y * scale);
            });
            ctx.stroke();
        }
        ctx.globalAlpha = 1;
        const own = g.extraStyle && g.extraStyle.length;
        const toneOf = n => (own
            ? n.style(n.isParent() || (n.data('display') || g.options.nodes) === 'box' ? 'border-color' : 'background-color')
            : g.colours.token(nodeTone(n, g.options), p.line));
        cy.nodes(':parent').not('.hidden').forEach(parent => {
            const b = parent.boundingBox({ includeLabels: false });
            ctx.fillStyle = withAlpha(toneOf(parent), 0.12);
            ctx.fillRect(ox + b.x1 * scale, oy + b.y1 * scale, b.w * scale, b.h * scale);
        });
        const runs = new Map();
        nodes.forEach(n => {
            const pos = n.position();
            const box = (n.data('display') || g.options.nodes) === 'box';
            const width = box ? (n.data('boxW') || 120) : (n.data('size') || 20) * held(n);
            const r = Math.max(1.5 * dpr, Math.min(width * scale / 2, 6 * dpr));
            const alpha = n.hasClass('dim') ? 0.25 : n.data('muted') ? 0.5 : 1;
            const colour = toneOf(n);
            const key = colour + '|' + alpha;
            let run = runs.get(key);
            if (!run) runs.set(key, (run = { colour, alpha, path: new Path2D() }));
            const x = ox + pos.x * scale, y = oy + pos.y * scale;
            run.path.moveTo(x + r, y);
            run.path.arc(x, y, r, 0, Math.PI * 2);
        });
        for (const run of runs.values()) {
            ctx.globalAlpha = run.alpha;
            ctx.fillStyle = run.colour;
            ctx.fill(run.path);
        }
        ctx.globalAlpha = 1;
    }
    function draw() {
        clearTimeout(timer);
        timer = 0;
        if (!canvas.isConnected || canvas.clientWidth === 0) return;
        const dpr = measure();
        if (dirty) {
            paintRecords(dpr);
            dirty = false;
        }
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(buffer, 0, 0);
        const p = palette(g.colours);
        const ext = cy.extent();
        const x = ox + ext.x1 * scale, y = oy + ext.y1 * scale, w = ext.w * scale, h = ext.h * scale;
        ctx.save();
        ctx.beginPath();
        ctx.rect(0, 0, canvas.width, canvas.height);
        ctx.rect(x, y, w, h);
        ctx.fillStyle = withAlpha(p.ground, 0.55);
        ctx.fill('evenodd');
        ctx.restore();
        const lw = 1.5 * dpr;
        const fx = Math.max(x, lw / 2), fy = Math.max(y, lw / 2);
        const fw = Math.min(x + w, canvas.width - lw / 2) - fx, fh = Math.min(y + h, canvas.height - lw / 2) - fy;
        if (fw > 0 && fh > 0) {
            ctx.strokeStyle = p.brand;
            ctx.lineWidth = lw;
            ctx.strokeRect(fx, fy, fw, fh);
        }
    }
    const soon = (records) => {
        if (records) dirty = true;
        if (!timer) timer = setTimeout(draw, 32);
    };
    const toModel = e => {
        const r = canvas.getBoundingClientRect();
        const dpr = canvas.width / Math.max(canvas.clientWidth, 1);
        const px = (e.clientX - r.left - canvas.clientLeft) * dpr;
        const py = (e.clientY - r.top - canvas.clientTop) * dpr;
        return { x: (px - ox) / scale, y: (py - oy) / scale };
    };
    const centreOn = e => {
        const m = toModel(e);
        cy.stop();
        cy.pan({ x: cy.width() / 2 - m.x * cy.zoom(), y: cy.height() / 2 - m.y * cy.zoom() });
    };
    const onDown = e => {
        if (e.button !== 0) return;
        dirty = true;
        draw();
        dragging = true;
        canvas.setPointerCapture?.(e.pointerId);
        g.touched = true;
        centreOn(e);
        e.preventDefault();
    };
    const onMove = e => { if (dragging) centreOn(e); };
    const onUp = e => {
        dragging = false;
        canvas.releasePointerCapture?.(e.pointerId);
        settle(g);
    };
    const onWheel = e => {
        e.preventDefault();
        g.touched = true;
        const m = toModel(e);
        const level = Math.max(cy.minZoom(), Math.min(cy.maxZoom(), cy.zoom() * (e.deltaY < 0 ? 1.2 : 1 / 1.2)));
        cy.zoom({ level, position: m });
    };
    canvas.addEventListener('pointerdown', onDown);
    canvas.addEventListener('pointermove', onMove);
    canvas.addEventListener('pointerup', onUp);
    canvas.addEventListener('pointercancel', onUp);
    canvas.addEventListener('wheel', onWheel, { passive: false });
    const onViewport = () => soon(false);
    const onRecords = () => soon(true);
    cy.on('viewport', onViewport);
    cy.on('position add remove', onRecords);
    const size = new ResizeObserver(() => soon(true));
    try { size.observe(canvas, { box: 'device-pixel-content-box' }); } catch (e) { size.observe(canvas); }
    return {
        draw: () => soon(true),
        now: () => { dirty = true; draw(); },
        destroy() {
            clearTimeout(timer);
            size.disconnect();
            cy.off('viewport', onViewport);
            cy.off('position add remove', onRecords);
            canvas.removeEventListener('pointerdown', onDown);
            canvas.removeEventListener('pointermove', onMove);
            canvas.removeEventListener('pointerup', onUp);
            canvas.removeEventListener('pointercancel', onUp);
            canvas.removeEventListener('wheel', onWheel);
        },
    };
}
function tips(g) {
    const stage = g.el;
    const forNodes = stage.querySelector('[data-graph-tip]:not([data-graph-tip="edge"])');
    const forEdges = stage.querySelector('[data-graph-tip="edge"]');
    let showing = null, timer = 0;
    function fill(tip, ele) {
        const data = ele.data();
        fillSlots(tip, data);
        tip.setAttribute('data-graph-tone', data.tone || '');
    }
    function rectOf(ele) {
        const host = g.host.getBoundingClientRect();
        const bb = ele.isNode()
            ? ele.renderedBoundingBox({ includeLabels: false, includeOverlays: false })
            : ele.renderedBoundingBox({ includeLabels: false });
        return {
            left: host.left + bb.x1, top: host.top + bb.y1, right: host.left + bb.x2, bottom: host.top + bb.y2,
            width: bb.w, height: bb.h,
        };
    }
    function place(tip, ele) {
        const stageBox = stage.getBoundingClientRect();
        const r = rectOf(ele);
        tip.hidden = false;
        tip.style.transform = 'translate(0px, 0px)';
        const t = tip.getBoundingClientRect();
        let x = r.right - stageBox.left + 10;
        let y = r.bottom - stageBox.top + 6;
        if (x + t.width > stageBox.width - 8) x = r.left - stageBox.left - t.width - 10;
        if (y + t.height > stageBox.height - 8) y = r.top - stageBox.top - t.height - 6;
        x = Math.max(8, Math.min(x, stageBox.width - t.width - 8));
        y = Math.max(8, Math.min(y, stageBox.height - t.height - 8));
        tip.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
    }
    function show(ele, now) {
        if (!ele || ele.empty() || ele.removed()) return hide();
        clearTimeout(timer);
        showing = ele;
        const go = () => {
            if (showing !== ele || !ele.inside() || ele.hasClass('hidden')) return;
            const tip = ele.isNode() ? forNodes : forEdges;
            if (tip) {
                fill(tip, ele);
                place(tip, ele);
                (ele.isNode() ? forEdges : forNodes)?.setAttribute('hidden', '');
                return;
            }
            const d = ele.data();
            const words = ele.isNode() ? [d.label, d.meta].filter(Boolean).join(' — ') : d.label;
            if (words && window.sednaUi?.tips?.at) window.sednaUi.tips.at(rectOf(ele), words, 'top');
        };
        if (now) go();
        else timer = setTimeout(go, 120);
    }
    function hide() {
        clearTimeout(timer);
        if (!showing) return;
        showing = null;
        forNodes?.setAttribute('hidden', '');
        forEdges?.setAttribute('hidden', '');
        window.sednaUi?.tips?.hide?.();
    }
    return { show, hide, get showing() { return showing; } };
}
function fillAnnouncement(template, ele) {
    const data = ele.data();
    const links = ele.connectedEdges().not('.hidden').length;
    return template.replace(/\{([a-zA-Z0-9_-]+)\}/g, (_, name) => {
        if (name === 'links') return String(links);
        const v = valueOf(data, name);
        return Array.isArray(v) ? v.join(', ') : v === undefined || v === null ? '' : String(v);
    }).replace(/\s+([,.;:])/g, '$1').replace(/([,.;:])(?=[,.;:])/g, '').trim();
}
function keyboard(g) {
    const el = g.el;
    const cy = g.cy;
    if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex', '0');
    if (!el.hasAttribute('role')) el.setAttribute('role', 'application');
    const live = () => scoped(g, '[data-graph-live]')[0] || null;
    function announce(ele, which) {
        const region = live();
        if (!region || !ele) return;
        const template = region.getAttribute(which === 'select' ? 'data-graph-announce-select' : 'data-graph-announce')
            || (which === 'select' ? '' : '{label}');
        if (!template) return;
        region.textContent = '';
        setTimeout(() => { region.textContent = fillAnnouncement(template, ele); }, 30);
    }
    const shown = () => cy.nodes().not('.hidden').filter(n => !n.hasClass('eh-ghost') && !n.hasClass('eh-handle'));
    function start() {
        const nodes = shown();
        if (nodes.empty()) return null;
        if (g.selected && g.selected.nonempty() && !g.selected.hasClass('hidden')) return g.selected;
        const root = nodes.filter('[?root], .focus');
        if (root.nonempty()) return root.first();
        return nodes.toArray().sort((a, b) => (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b))[0];
    }
    function key(n, say = true) {
        if (g.keyed && g.keyed.nonempty()) g.keyed.removeClass('keyed');
        g.keyed = n && n.nonempty() ? n : null;
        if (!g.keyed) {
            g.tip.hide();
            light(g, resting(g));
            declutter(g);
            return;
        }
        g.keyed.addClass('keyed');
        light(g, g.keyed);
        declutter(g);
        const ext = cy.extent();
        const p = g.keyed.position();
        const mx = ext.w * 0.12, my = ext.h * 0.12;
        if (p.x < ext.x1 + mx || p.x > ext.x2 - mx || p.y < ext.y1 + my || p.y > ext.y2 - my) {
            cy.stop();
            if (!glides(g)) cy.center(g.keyed);
            else cy.animate({ center: { eles: g.keyed } }, { duration: 180, complete: () => { if (g.keyed) g.tip.show(g.keyed, true); } });
        }
        g.tip.show(g.keyed, true);
        if (say) announce(g.keyed, 'focus');
        g.emit('sedna-graph-hover', nodeDetail(g.keyed, { keyboard: true }));
    }
    function towards(dx, dy) {
        const from = g.keyed;
        if (!from) return start();
        const a = from.position();
        let best = null, bestScore = Infinity;
        shown().forEach(n => {
            if (n.same(from) || n.isParent()) return;
            const b = n.position();
            const vx = b.x - a.x, vy = b.y - a.y;
            const dist = Math.hypot(vx, vy);
            if (dist < 1) return;
            const cos = (vx * dx + vy * dy) / dist;
            if (cos < 0.5) return;            // within 60° of the arrow
            const score = dist * (2 - cos);   // straight ahead beats a little closer
            if (score < bestScore) { bestScore = score; best = n; }
        });
        return best;
    }
    function along(step) {
        const from = g.keyed || start();
        if (!from) return null;
        const around = from.neighborhood().nodes().not('.hidden').toArray()
            .sort((a, b) => {
                const pa = a.position(), pb = b.position(), c = from.position();
                return Math.atan2(pa.y - c.y, pa.x - c.x) - Math.atan2(pb.y - c.y, pb.x - c.x);
            });
        if (!around.length) return null;
        g.walk = g.walk && g.walk.from === from.id() ? g.walk : { from: from.id(), at: -1 };
        g.walk.at = g.walk.at < 0
            ? (step > 0 ? 0 : around.length - 1)
            : (g.walk.at + step + around.length) % around.length;
        const next = around[g.walk.at];
        g.walk = { from: next.id(), at: -1 };
        return next;
    }
    function onKey(e) {
        if (e.target !== el || e.altKey || e.ctrlKey || e.metaKey) return;
        const pan = 60;
        let handled = true;
        switch (e.key) {
            case 'ArrowLeft': case 'ArrowRight': case 'ArrowUp': case 'ArrowDown': {
                const dx = e.key === 'ArrowLeft' ? -1 : e.key === 'ArrowRight' ? 1 : 0;
                const dy = e.key === 'ArrowUp' ? -1 : e.key === 'ArrowDown' ? 1 : 0;
                if (e.shiftKey) {
                    g.touched = true;
                    cy.panBy({ x: -dx * pan, y: -dy * pan });
                    break;
                }
                const next = towards(dx, dy);
                if (next) key(next);
                break;
            }
            case 'PageDown': case 'PageUp': {
                const next = along(e.key === 'PageDown' ? 1 : -1);
                if (next) key(next);
                break;
            }
            case 'Home': {
                const first = start();
                if (first) key(first);
                break;
            }
            case ' ': case 'Spacebar':
                if (!g.keyed) key(start());
                else if (g.selected && g.selected.same(g.keyed)) select(g, null);
                else {
                    select(g, g.keyed);
                    announce(g.keyed, 'select');
                }
                break;
            case 'Enter':
                if (g.keyed && !toggleFold(g, g.keyed)) open(g, g.keyed, true);
                break;
            case '+': case '=':
                zoomBy(g, 1.25);
                break;
            case '-': case '_':
                zoomBy(g, 1 / 1.25);
                break;
            case '0':
                g.touched = false;
                fitView(g, null, true);
                break;
            case 'Escape':
                if (g.selected && g.selected.nonempty()) select(g, null);
                else if (g.keyed) key(null);
                else handled = false;
                break;
            case 'ContextMenu':
                if (g.keyed) context(g, g.keyed, null);
                break;
            case 'F10':
                if (e.shiftKey && g.keyed) context(g, g.keyed, null);
                else handled = false;
                break;
            default:
                handled = false;
        }
        if (handled) e.preventDefault();
    }
    function onFocus() {
        if (!g.keyed && !g.pressing && el.matches(':focus-visible')) key(start());
    }
    function onBlur(e) {
        if (e.relatedTarget && el.contains(e.relatedTarget)) return;
        if (g.keyed) key(null);
    }
    el.addEventListener('keydown', onKey);
    el.addEventListener('focus', onFocus);
    el.addEventListener('blur', onBlur);
    return {
        key,
        announce,
        destroy() {
            el.removeEventListener('keydown', onKey);
            el.removeEventListener('focus', onFocus);
            el.removeEventListener('blur', onBlur);
        },
    };
}
function zoomBy(g, factor) {
    const cy = g.cy;
    g.touched = true;
    const level = Math.max(cy.minZoom(), Math.min(cy.maxZoom(), cy.zoom() * factor));
    const centre = g.keyed && g.keyed.nonempty() ? g.keyed.renderedPosition() : { x: cy.width() / 2, y: cy.height() / 2 };
    cy.stop();
    if (!glides(g)) {
        cy.zoom({ level, renderedPosition: centre });
        settle(g);
    } else {
        cy.animate({ zoom: { level, renderedPosition: centre } }, { duration: 160, complete: () => settle(g) });
    }
}
function menus(g) {
    const menu = g.el.querySelector('[data-graph-menu]');
    let target = null;
    function items() {
        return Array.from(menu.querySelectorAll('.menu-item, [role="menuitem"]'))
            .filter(i => !i.hidden && !i.disabled && i.getAttribute('aria-disabled') !== 'true');
    }
    function open(ele, at, keyboard) {
        if (!menu) return;
        g.tip.hide();
        target = ele;
        menu.setAttribute('data-graph-target', ele ? ele.id() : '');
        fillSlots(menu, ele ? ele.data() : {});
        menu.querySelectorAll('[data-graph-action]').forEach(item => {
            const a = item.getAttribute('data-graph-action');
            const needs = ['open', 'focus', 'select', 'hide', 'expand', 'collapse'].includes(a);
            const group = a === 'expand' ? ele && g.ec && g.ec.isExpandable(ele)
                : a === 'collapse' ? ele && g.ec && g.ec.isCollapsible(ele) : true;
            item.hidden = (needs && !ele) || !group || (a === 'open' && ele && !ele.data('href') && !item.hasAttribute('data-graph-always'));
        });
        tidy();
        menu.hidden = false;
        const stage = g.el.getBoundingClientRect();
        const m = menu.getBoundingClientRect();
        let x = at.x, y = at.y;
        if (x + m.width > stage.width - 6) x = Math.max(6, at.x - m.width);
        if (y + m.height > stage.height - 6) y = Math.max(6, at.y - m.height);
        menu.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
        if (keyboard) items()[0]?.focus();
    }
    function tidy() {
        const isSep = el => el.matches('.menu-sep, hr, [role="separator"]');
        let before = false, pending = null;
        for (const el of menu.children) {
            if (isSep(el)) {
                el.hidden = true;
                if (before) pending = el;
                before = false;
                continue;
            }
            if (el.hidden || el.tagName === 'TEMPLATE') continue;
            if (pending) pending.hidden = false;
            pending = null;
            before = true;
        }
    }
    function close(refocus) {
        if (!menu) return;
        if (!menu.hidden) {
            menu.hidden = true;
            target = null;
        }
        if (refocus) g.el.focus({ preventScroll: true });
    }
    function onKey(e) {
        if (menu.hidden) return;
        const list = items();
        const at = list.indexOf(document.activeElement);
        if (e.key === 'Escape') { close(true); e.preventDefault(); e.stopPropagation(); }
        else if (e.key === 'ArrowDown') { list[(at + 1) % list.length]?.focus(); e.preventDefault(); }
        else if (e.key === 'ArrowUp') { list[(at - 1 + list.length) % list.length]?.focus(); e.preventDefault(); }
        else if (e.key === 'Home') { list[0]?.focus(); e.preventDefault(); }
        else if (e.key === 'End') { list[list.length - 1]?.focus(); e.preventDefault(); }
        else if (e.key === 'Tab') close(false);
    }
    const onOutside = e => { if (!menu.hidden && !menu.contains(e.target)) close(false); };
    const onEscape = e => {
        if (menu.hidden || e.key !== 'Escape' || menu.contains(e.target)) return;
        close(g.el.contains(document.activeElement) || document.activeElement === document.body);
        e.preventDefault();
    };
    const onScroll = e => { if (!menu.hidden && !menu.contains(e.target)) close(false); };
    const onViewport = () => { if (!menu.hidden && !picking) close(false); };
    let picking = false;
    const onPick = e => {
        const item = e.target.closest('.menu-item, [role="menuitem"]');
        if (!item || !menu.contains(item)) return;
        picking = true;
        setTimeout(() => {
            picking = false;
            close(true);
        }, 0);
    };
    if (menu) {
        menu.addEventListener('keydown', onKey);
        menu.addEventListener('click', onPick);
        document.addEventListener('pointerdown', onOutside, true);
        document.addEventListener('keydown', onEscape);
        window.addEventListener('scroll', onScroll, true);
        g.cy.on('pan zoom', onViewport);
    }
    return {
        present: !!menu,
        open,
        close,
        get target() { return target; },
        destroy() {
            if (!menu) return;
            menu.removeEventListener('keydown', onKey);
            menu.removeEventListener('click', onPick);
            document.removeEventListener('pointerdown', onOutside, true);
            document.removeEventListener('keydown', onEscape);
            window.removeEventListener('scroll', onScroll, true);
            g.cy.off('pan zoom', onViewport);
        },
    };
}
function context(g, ele, at) {
    const point = at || (ele ? ele.renderedPosition() : { x: g.cy.width() / 2, y: g.cy.height() / 2 });
    const detail = Object.assign(ele ? nodeDetail(ele) : { id: null }, { x: Math.round(point.x), y: Math.round(point.y) });
    const go = g.emit('sedna-graph-context', detail, true);
    if (go && g.menu.present) g.menu.open(ele, point, !at);
}
const detailPanels = g => scoped(g, '[data-graph-detail]');
const textOf = v => (Array.isArray(v) ? v.join(', ') : v === undefined || v === null ? '' : String(v));
function fillSlots(root, data) {
    const own = el => !el.closest('template');
    root.querySelectorAll('[data-graph-field]').forEach(slot => {
        if (!own(slot)) return;
        const s = textOf(valueOf(data, slot.getAttribute('data-graph-field')));
        slot.textContent = s;
        slot.hidden = s === '';
        const term = slot.tagName === 'DD' ? slot.previousElementSibling : null;
        if (term && term.tagName === 'DT') term.hidden = s === '';
    });
    root.querySelectorAll('[data-graph-if]').forEach(el => {
        if (own(el)) el.hidden = textOf(valueOf(data, el.getAttribute('data-graph-if'))) === '';
    });
    root.querySelectorAll('[data-graph-icon]').forEach(i => {
        if (!own(i)) return;
        if (i.__base === undefined) i.__base = i.className;
        i.className = (i.__base + ' ' + (data.icon || '')).trim();
        i.hidden = !data.icon;
    });
}
function cloneTemplate(template) {
    const first = (template.content && template.content.firstElementChild) || template.firstElementChild;
    return first ? first.cloneNode(true) : null;
}
function showDetail(g) {
    const panels = detailPanels(g);
    if (!panels.length) return;
    const node = g.selected && g.selected.nonempty() ? g.selected : null;
    for (const panel of panels) {
        panel.hidden = !node;
        if (!node) continue;
        const data = node.data();
        fillSlots(panel, data);
        panel.setAttribute('data-graph-tone', data.tone || '');
        const list = panel.querySelector('[data-graph-neighbours]');
        const template = list?.querySelector('template[data-graph-neighbour]') || panel.querySelector('template[data-graph-neighbour]');
        if (!list || !template) continue;
        list.querySelectorAll('[data-graph-row]').forEach(r => r.remove());
        const rows = new Map();
        node.connectedEdges().not('.hidden').forEach(e => {
            const out = e.source().same(node);
            const other = out ? e.target() : e.source();
            if (other.same(node)) return;
            const seen = rows.get(other.id());
            if (seen) {
                if (seen.out !== out) seen.direction = 'both';
                seen.links.push(e.data('label'));
                return;
            }
            rows.set(other.id(), { other, out, direction: out ? 'out' : 'in', links: [e.data('label')] });
        });
        const ordered = [...rows.values()].sort((a, b) =>
            String(a.other.data('label')).localeCompare(String(b.other.data('label'))));
        for (const r of ordered) {
            const row = cloneTemplate(template);
            if (!row) break;
            row.setAttribute('data-graph-row', '');
            row.setAttribute('data-graph-direction', r.direction);
            fillSlots(row, r.other.data());
            const link = [...new Set(r.links.filter(Boolean))].join(', ');
            row.querySelectorAll('[data-graph-link]').forEach(s => {
                s.textContent = link;
                s.hidden = !link;
            });
            const acts = row.matches('[data-graph-action]') ? [row] : [];
            row.querySelectorAll('[data-graph-action]').forEach(a => acts.push(a));
            acts.forEach(a => {
                if (!a.getAttribute('value')) a.setAttribute('value', r.other.id());
            });
            list.insertBefore(row, template);
        }
        list.setAttribute('data-graph-count', String(ordered.length));
    }
}
const CONTROL_SELECTOR = '[data-graph-action],[data-graph-filter],[data-graph-show],[data-graph-search],[data-graph-option]';
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
        const which = s.matches && g.query && el.getAttribute('data-graph-stats-match') ? el.getAttribute('data-graph-stats-match') : template;
        el.textContent = which.replace(/\{(\w+)\}/g, (_, k) => (k in s && s[k] !== null ? String(s[k]) : ''));
    }
}
async function control(g, c, type) {
    if (c.hasAttribute('data-graph-search')) {
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
        case 'direction': o.direction = directionOf(value); g.touched = false; restyle(g); await arrange(g, true); break;
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
            g.cy.batch(() => g.cy.edges().data('zoom', g.step));
            restyle(g);
            break;
        case 'colour': case 'color': o.colourBy = value || 'tone'; restyle(g); break;
        case 'curve': o.curve = value || 'bezier'; o.curveChosen = !!value; restyle(g); break;
        case 'arrows': o.arrows = value; restyle(g); break;
        case 'zoom': return zoomTo(g, value);
        case 'depth':
            g.depth = Math.max(1, Math.min(Number(value) || 1, 6));
            if (g.focusId) await refilter(g, true);
            break;
        default:
            return warnOnce('option:' + name, `"${name}" is not a graph option.`);
    }
    changed(g);
}
const percentOf = g => Math.round(ownZoom(g) * 100);
function zoomTo(g, percent) {
    const cy = g.cy;
    const asked = Number(percent);
    if (!(asked > 0)) return;
    g.touched = true;
    const level = Math.max(cy.minZoom(), Math.min(cy.maxZoom(), (asked / 100) * g.options.scale));
    cy.stop();
    cy.zoom({ level, renderedPosition: { x: cy.width() / 2, y: cy.height() / 2 } });
}
function showZoom(g, all) {
    if (g.disposed || !g.cy) return;
    const percent = percentOf(g);
    if (!all && g.shownZoom === percent) return;
    g.shownZoom = percent;
    for (const c of controlsOf(g)) {
        if (c.getAttribute('data-graph-option') !== 'zoom') continue;
        if (c.tagName === 'SELECT') {
            const options = Array.from(c.options);
            const at = options.findIndex(o => o.value !== '' && Math.round(Number(o.value)) === percent);
            const index = at >= 0 ? at : options.findIndex(o => o.value === '');
            if (c.selectedIndex !== index) c.selectedIndex = index;
        } else if (c.type === 'radio') {
            const on = Math.round(Number(c.value)) === percent;
            if (c.checked !== on) c.checked = on;
        } else if (c.tagName === 'INPUT') {
            const range = percentRange(g);
            if (!c.hasAttribute('min')) c.min = String(range.min);
            if (!c.hasAttribute('max')) c.max = String(range.max);
            if (c.value !== String(percent)) c.value = String(percent);
        }
    }
}
const percentRange = g => ({
    min: Math.round((g.cy.minZoom() / g.options.scale) * 100),
    max: Math.round((g.cy.maxZoom() / g.options.scale) * 100),
});
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
    showZoom(g, true);
}
async function focusOn(g, id) {
    g.focusId = id;
    await refilter(g, true);
}
function centreOn(g, ele) {
    const cy = g.cy;
    cy.stop();
    if (!glides(g)) cy.center(ele);
    else cy.animate({ center: { eles: ele } }, { duration: 220, easing: 'ease-out-cubic', complete: () => settle(g) });
}
function fullscreen(g, button) {
    const frame = g.el.closest('[data-graph-frame]') || g.el;
    if (document.fullscreenElement === frame) document.exitFullscreen?.();
    else frame.requestFullscreen?.().catch(() => button?.setAttribute('aria-pressed', 'false'));
}
function edgeTools(g) {
    const templates = [...g.el.querySelectorAll('template[data-graph-edge-tool]')].filter(t => t.closest('[data-graph]') === g.el);
    if (!templates.length) return null;
    const cy = g.cy;
    const copies = new Map();
    let queued = false;
    const kindsOf = t => (t.getAttribute('data-graph-edge-tool') || '').split(/\s+/).filter(Boolean);
    const templateFor = e => templates.find(t => {
        const kinds = kindsOf(t);
        return !kinds.length || kinds.includes(e.data('kind') || '');
    });
    const eligible = e => !e.hasClass('hidden') && !e.hasClass('eh-preview') && !e.hasClass('eh-ghost-edge')
        && !e.hasClass('cy-expand-collapse-meta-edge') && !e.source().hasClass('hidden') && !e.target().hasClass('hidden');
    function sync() {
        if (g.disposed) return;
        const want = new Map();
        cy.edges().forEach(e => {
            if (!eligible(e)) return;
            const t = templateFor(e);
            if (t) want.set(e.id(), { e, t });
        });
        for (const [id, copy] of copies) {
            if (want.get(id)?.t === copy.__graphTemplate) continue;
            copy.remove();
            copies.delete(id);
        }
        for (const [id, { e, t }] of want) {
            let copy = copies.get(id);
            if (!copy) {
                copy = cloneTemplate(t);
                if (!copy) continue;
                copy.__graphTemplate = t;
                copy.setAttribute('data-graph-edge-id', id);
                copy.hidden = true;
                t.parentNode.insertBefore(copy, t);
                copies.set(id, copy);
            }
            fillSlots(copy, e.data());
            copy.setAttribute('data-graph-tone', e.data('tone') || '');
        }
        place();
    }
    function place() {
        queued = false;
        if (g.disposed || !copies.size) return;
        const zoom = cy.zoom(), pan = cy.pan(), w = cy.width(), h = cy.height();
        for (const [id, copy] of copies) {
            const e = cy.getElementById(id);
            let m = null;
            try {
                m = e.nonempty() ? e.midpoint() : null;
            } catch (x) { /* not drawn yet */ }
            const x = m ? m.x * zoom + pan.x : NaN, y = m ? m.y * zoom + pan.y : NaN;
            const off = !Number.isFinite(x) || !Number.isFinite(y) || x < 0 || y < 0 || x > w || y > h;
            if (copy.hidden !== off) copy.hidden = off;
            if (!off) copy.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
        }
    }
    const soon = () => {
        if (queued) return;
        queued = true;
        queueMicrotask(place);
    };
    function onClick(ev) {
        const copy = ev.target.closest?.('[data-graph-edge-id]');
        if (!copy || !copies.has(copy.getAttribute('data-graph-edge-id'))) return;
        const button = ev.target.closest('button, [role="button"]');
        if (!button || !copy.contains(button)) return;
        const e = cy.getElementById(copy.getAttribute('data-graph-edge-id'));
        if (e.empty()) return;
        const detail = edgeDetail(e, button.getAttribute('value') || null);
        if (!g.emit('sedna-graph-edge', detail, true)) return;
        if (g.options.managed && detail.value === 'remove') {
            g.set({ nodes: g.model.nodes, edges: g.model.edges.filter(x => x.id !== detail.id) }, { quiet: true }).catch(report);
        }
    }
    cy.on('viewport resize', soon);
    cy.on('position', 'node', soon);
    g.el.addEventListener('click', onClick);
    return {
        sync,
        destroy() {
            cy.off('viewport resize', soon);
            cy.off('position', 'node', soon);
            g.el.removeEventListener('click', onClick);
            for (const copy of copies.values()) copy.remove();
            copies.clear();
        },
    };
}
function edgeDetail(e, value) {
    const d = e.data();
    const fields = {};
    for (const [k, v] of Object.entries(d.fields || {})) fields[k] = v === null || v === undefined ? null : String(v);
    return {
        id: d.id,
        source: d.source,
        target: d.target,
        label: d.label ?? null,
        kind: d.kind ?? null,
        tone: d.tone ?? null,
        state: d.state ?? null,
        fields,
        value: value ?? null,
    };
}
const SVG_NS = 'http://www.w3.org/2000/svg';
async function exportImage(g, format) {
    const ground = palette(g.colours).ground;
    rezoom(g, true);
    declutter(g, true);
    if (format === 'png') {
        return g.cy.png({ output: 'blob-promise', bg: ground, full: true, scale: 2, maxWidth: 8000, maxHeight: 8000 });
    }
    return new Blob([svgOf(g, ground)], { type: 'image/svg+xml' });
}
async function download(g, format, filename) {
    const blob = await exportImage(g, format === 'png' ? 'png' : 'svg');
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = (filename || g.el.id || 'graph').replace(/\.(svg|png)$/i, '') + '.' + format;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}
const opacityOf = ele => (ele.hasClass('dim') ? 1 : ele.numericStyle('opacity'));
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const num = n => (Math.round(n * 100) / 100).toString();
function ngon(sides, rotation = 0) {
    const pts = [];
    for (let i = 0; i < sides; i++) {
        const a = rotation - Math.PI / 2 + (i * 2 * Math.PI) / sides;
        pts.push([Math.cos(a), Math.sin(a)]);
    }
    const xs = pts.map(p => p[0]), ys = pts.map(p => p[1]);
    const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
    return pts.map(([x, y]) => [((x - minX) / (maxX - minX)) * 2 - 1, ((y - minY) / (maxY - minY)) * 2 - 1]);
}
function star() {
    const pts = [];
    for (let i = 0; i < 10; i++) {
        const r = i % 2 === 0 ? 1 : 0.4;
        const a = -Math.PI / 2 + (i * Math.PI) / 5;
        pts.push([Math.cos(a) * r, Math.sin(a) * r]);
    }
    return pts;
}
const POLYGONS = {
    triangle: [[0, -1], [1, 1], [-1, 1]],
    diamond: [[0, -1], [1, 0], [0, 1], [-1, 0]],
    tag: [[-1, -1], [0.25, -1], [1, 0], [0.25, 1], [-1, 1]],
    vee: [[-1, -1], [0, -0.333], [1, -1], [0, 1]],
    rhomboid: [[-1, -1], [0.333, -1], [1, 1], [-0.333, 1]],
    pentagon: ngon(5),
    hexagon: ngon(6),
    heptagon: ngon(7),
    octagon: ngon(8, Math.PI / 8),
    star: star(),
};
function shapeSvg(shape, x, y, w, h, attrs) {
    const base = shape.replace(/^round-/, '');
    const rounded = shape.startsWith('round-');
    if (base === 'ellipse') return `<ellipse cx="${num(x)}" cy="${num(y)}" rx="${num(w / 2)}" ry="${num(h / 2)}" ${attrs}/>`;
    if (POLYGONS[base]) {
        const pts = POLYGONS[base].map(([px, py]) => `${num(x + (px * w) / 2)},${num(y + (py * h) / 2)}`).join(' ');
        return `<polygon points="${pts}" stroke-linejoin="${rounded ? 'round' : 'miter'}" ${attrs}/>`;
    }
    const r = base === 'rectangle' && !rounded ? 0 : Math.min(w / 4, h / 4, 8);
    return `<rect x="${num(x - w / 2)}" y="${num(y - h / 2)}" width="${num(w)}" height="${num(h)}" rx="${num(r)}" ${attrs}/>`;
}
let measureCtx = null;
function wrapLines(text, maxWidth, font) {
    measureCtx = measureCtx || document.createElement('canvas').getContext('2d');
    measureCtx.font = font;
    const out = [];
    for (const para of String(text).split('\n')) {
        const words = para.split(/\s+/).filter(Boolean);
        let line = '';
        for (const w of words) {
            const next = line ? line + ' ' + w : w;
            if (line && measureCtx.measureText(next).width > maxWidth) {
                out.push(line);
                line = w;
            } else {
                line = next;
            }
        }
        out.push(line);
    }
    return out;
}
function edgePath(e) {
    const s = e.sourceEndpoint(), t = e.targetEndpoint();
    const curve = e.style('curve-style');
    let d = `M ${num(s.x)} ${num(s.y)}`;
    let before = s;
    if (/taxi|segments/.test(curve)) {
        const pts = e.segmentPoints() || [];
        pts.forEach(p => { d += ` L ${num(p.x)} ${num(p.y)}`; });
        if (pts.length) before = pts[pts.length - 1];
        d += ` L ${num(t.x)} ${num(t.y)}`;
    } else if (/bezier/.test(curve)) {
        const cps = e.controlPoints() || [];
        if (cps.length === 0) {
            d += ` L ${num(t.x)} ${num(t.y)}`;
        } else {
            for (let i = 0; i < cps.length; i++) {
                const cp = cps[i];
                const end = i < cps.length - 1 ? { x: (cp.x + cps[i + 1].x) / 2, y: (cp.y + cps[i + 1].y) / 2 } : t;
                d += ` Q ${num(cp.x)} ${num(cp.y)} ${num(end.x)} ${num(end.y)}`;
            }
            before = cps[cps.length - 1];
        }
    } else {
        d += ` L ${num(t.x)} ${num(t.y)}`;
    }
    return { d, s, t, before, after: (/bezier/.test(curve) && (e.controlPoints() || []).length) ? e.controlPoints()[0] : ((e.segmentPoints() || [])[0] || t) };
}
function arrowSvg(tip, from, size, colour) {
    const a = Math.atan2(tip.y - from.y, tip.x - from.x);
    const l = size, w = size * 0.6;
    const bx = tip.x - Math.cos(a) * l, by = tip.y - Math.sin(a) * l;
    const p1 = [bx + Math.cos(a + Math.PI / 2) * w / 2, by + Math.sin(a + Math.PI / 2) * w / 2];
    const p2 = [bx + Math.cos(a - Math.PI / 2) * w / 2, by + Math.sin(a - Math.PI / 2) * w / 2];
    return `<polygon points="${num(tip.x)},${num(tip.y)} ${num(p1[0])},${num(p1[1])} ${num(p2[0])},${num(p2[1])}" fill="${colour}"/>`;
}
function svgOf(g, ground) {
    const cy = g.cy;
    const visible = cy.elements().not('.hidden').not('.eh-ghost, .eh-ghost-edge, .eh-preview, .eh-handle');
    const bb = visible.boundingBox({ includeLabels: true, includeOverlays: false });
    const pad = 24;
    const x0 = bb.x1 - pad, y0 = bb.y1 - pad, w = bb.w + pad * 2, h = bb.h + pad * 2;
    const font = palette(g.colours).font;
    const out = [];
    const title = g.el.getAttribute('aria-label');
    out.push(`<svg xmlns="${SVG_NS}" viewBox="${num(x0)} ${num(y0)} ${num(w)} ${num(h)}" width="${Math.round(w)}" height="${Math.round(h)}" role="img"${title ? ` aria-label="${esc(title)}"` : ''} font-family="${esc(font)}">`);
    if (title) out.push(`<title>${esc(title)}</title>`);
    out.push(`<rect x="${num(x0)}" y="${num(y0)}" width="${num(w)}" height="${num(h)}" fill="${ground}"/>`);
    if (g.hullsOn && g.bb) {
        for (const path of g.bb.getPaths()) {
            const d = path.node.getAttribute('d');
            if (!d) continue;
            const st = path.node.style;
            out.push(`<path class="graph-outline" d="${esc(d)}" fill="${st.fill || 'none'}" stroke="${st.stroke || 'none'}" stroke-width="${st.strokeWidth || 1}"/>`);
        }
    }
    const parents = visible.nodes(':parent').toArray().sort((a, b) => a.ancestors().length - b.ancestors().length);
    for (const p of parents) {
        const b = p.boundingBox({ includeLabels: false });
        const fill = p.style('background-color'), stroke = p.style('border-color');
        out.push(`<g class="graph-group"><title>${esc(p.data('label') || p.id())}</title>`
            + `<rect x="${num(b.x1)}" y="${num(b.y1)}" width="${num(b.w)}" height="${num(b.h)}" rx="8" fill="${fill}" fill-opacity="${p.numericStyle('background-opacity')}" stroke="${stroke}" stroke-dasharray="6 4" stroke-width="${p.numericStyle('border-width')}"/>`
            + (p.style('label') ? `<text x="${num(b.x1 + b.w / 2)}" y="${num(b.y1 - 6)}" text-anchor="middle" font-size="${num(p.numericStyle('font-size'))}" font-weight="600" fill="${p.style('color')}">${esc(p.style('label'))}</text>` : '')
            + '</g>');
    }
    for (const e of visible.edges().toArray()) {
        const colour = e.style('line-color');
        const width = e.numericStyle('width');
        const opacity = opacityOf(e) * e.numericStyle('line-opacity');
        const dash = e.style('line-style') === 'dashed' ? ' stroke-dasharray="6 4"' : e.style('line-style') === 'dotted' ? ' stroke-dasharray="1.5 3.5" stroke-linecap="round"' : '';
        const geo = edgePath(e);
        const size = (5 + width * 2.5) * e.numericStyle('arrow-scale');
        let arrows = '';
        if (e.style('target-arrow-shape') !== 'none') arrows += arrowSvg(geo.t, geo.before, size, colour);
        if (e.style('source-arrow-shape') !== 'none') arrows += arrowSvg(geo.s, geo.after, size, colour);
        const label = e.style('label');
        const mid = e.midpoint();
        out.push(`<g class="graph-link" opacity="${num(opacity)}"><title>${esc(e.data('label') || `${e.source().data('label')} → ${e.target().data('label')}`)}</title>`
            + `<path d="${geo.d}" fill="none" stroke="${colour}" stroke-width="${num(width)}"${dash}/>${arrows}`
            + (label ? `<text x="${num(mid.x)}" y="${num(mid.y)}" text-anchor="middle" dominant-baseline="middle" font-size="${num(e.numericStyle('font-size'))}" fill="${e.style('color')}" stroke="${ground}" stroke-width="3" paint-order="stroke">${esc(label)}</text>` : '')
            + '</g>');
    }
    for (const n of visible.nodes().not(':parent').toArray()) {
        const p = n.position();
        const nw = n.width(), nh = n.height();
        const box = (n.data('display') || g.options.nodes) === 'box';
        const fill = n.style('background-color');
        const stroke = n.style('border-color');
        const bw = n.numericStyle('border-width');
        const dashed = n.style('border-style') === 'dashed' ? ' stroke-dasharray="5 3"' : '';
        const attrs = `fill="${fill}" fill-opacity="${num(n.numericStyle('background-opacity'))}" stroke="${stroke}" stroke-opacity="${num(n.numericStyle('border-opacity'))}" stroke-width="${num(bw)}"${dashed}`;
        const parts = [`<g class="graph-record" opacity="${num(opacityOf(n))}"><title>${esc([n.data('label'), n.data('meta')].filter(Boolean).join(' — '))}</title>`];
        parts.push(shapeSvg(n.style('shape'), p.x, p.y, nw, nh, attrs));
        const ring = n.hasClass('keyed') ? 0 : n.numericStyle('outline-width');
        if (ring > 0 && n.numericStyle('outline-opacity') > 0) {
            const grow = bw + ring + n.numericStyle('outline-offset');
            const ringDash = n.style('outline-style') === 'dashed' ? ' stroke-dasharray="4 2"' : '';
            parts.push(shapeSvg(n.style('shape'), p.x, p.y, nw + grow, nh + grow,
                `class="graph-ring" fill="none" stroke="${n.style('outline-color')}" stroke-width="${num(ring)}"${ringDash}`));
        }
        const icon = n.data('icon') ? g.icons.image(n.data('icon'), box ? stroke : ground, 32) : null;
        if (icon) {
            const size = box ? 16 : Math.min(nw, nh) * 0.56;
            const ix = box ? p.x - nw / 2 + 12 : p.x - size / 2;
            parts.push(`<image href="${icon}" x="${num(ix)}" y="${num(p.y - size / 2)}" width="${num(size)}" height="${num(size)}"/>`);
        }
        const label = n.style('label');
        if (label) {
            const size = n.numericStyle('font-size');
            const weight = n.style('font-weight');
            const colour = n.style('color');
            if (box) {
                const maxW = n.numericStyle('text-max-width');
                const lines = wrapLines(label, maxW, `${weight} ${size}px ${font}`);
                const lh = size * 1.35;
                const top = p.y - ((lines.length - 1) * lh) / 2;
                const tx = p.x + (n.data('icon') ? 10 : 0);
                parts.push(`<text x="${num(tx)}" y="${num(top)}" text-anchor="middle" dominant-baseline="middle" font-size="${num(size)}" font-weight="${weight}" fill="${colour}">`
                    + lines.map((l, i) => `<tspan x="${num(tx)}" y="${num(top + i * lh)}">${esc(l)}</tspan>`).join('') + '</text>');
            } else {
                const halign = n.style('text-halign');
                const beside = n.style('text-valign') === 'center' && (halign === 'right' || halign === 'left');
                const tx = beside ? p.x + (halign === 'right' ? 1 : -1) * nw / 2 + n.numericStyle('text-margin-x') : p.x;
                const ty = beside ? p.y : p.y + nh / 2 + n.numericStyle('text-margin-y') + size;
                const anchor = beside ? (halign === 'right' ? 'start' : 'end') : 'middle';
                parts.push(`<text x="${num(tx)}" y="${num(ty)}" text-anchor="${anchor}"${beside ? ' dominant-baseline="middle"' : ''} font-size="${num(size)}" font-weight="${weight}" fill="${colour}" stroke="${ground}" stroke-width="${num(size * 0.22 * 2)}" stroke-linejoin="round" paint-order="stroke">${esc(label)}</text>`);
            }
        }
        parts.push('</g>');
        out.push(parts.join(''));
    }
    out.push('</svg>');
    return out.filter(Boolean).join('\n');
}
async function connect(g, on) {
    g.drawing = !!on;
    if (!on) {
        g.eh?.disableDrawMode();
        g.el.removeAttribute('data-graph-drawing');
        return;
    }
    await plugin('edgehandles');
    if (!g.eh) {
        g.eh = g.cy.edgehandles({
            canConnect: (source, target) => !source.same(target) && !source.isParent() && !target.isParent()
                && !target.hasClass('hidden'),
            edgeParams: (source, target) => ({ data: { source: source.id(), target: target.id(), drawn: true } }),
            hoverDelay: 120,
            snap: true,
            snapThreshold: 36,
            snapFrequency: 15,
            noEdgeEventsInDraw: true,
            disableBrowserGestures: true,
        });
        g.cy.on('ehcomplete', (event, source, target, added) => {
            const detail = { source: source.id(), target: target.id() };
            added.remove();
            if (g.options.managed) {
                const next = { nodes: g.model.nodes, edges: g.model.edges.concat([{ source: detail.source, target: detail.target, kind: 'drawn' }]) };
                g.set(next, { quiet: true });
            }
            g.emit('sedna-graph-connect', detail);
        });
    }
    if (g.drawing) {
        g.eh.enableDrawMode();
        g.el.setAttribute('data-graph-drawing', '');
    }
}
async function collapsible(g) {
    if (g.ec) return g.ec;
    await plugin('expandCollapse');
    const p = palette(g.colours);
    g.ec = g.cy.expandCollapse({
        layoutBy: null,
        fisheye: false,
        animate: !reducedMotion(),
        animationDuration: 240,
        undoable: false,
        cueEnabled: true,
        expandCollapseCuePosition: 'top-left',
        expandCollapseCueSize: 14,
        expandCollapseCueLineSize: 9,
        expandCueImage: g.icons.image('ri-add-box-line', p.fg, 28) || undefined,
        collapseCueImage: g.icons.image('ri-checkbox-indeterminate-line', p.fg, 28) || undefined,
        expandCollapseCueSensitivity: 1,
        zIndex: 2,
    });
    let cue = 0;
    g.cy.on('resize', () => {
        clearTimeout(cue);
        cue = setTimeout(() => {
            if (g.disposed) return;
            const chosen = g.cy.nodes(':selected');
            if (chosen.length === 1 && isGroup(chosen)) chosen.emit('select');
        }, 320);
    });
    g.listeners.push(() => clearTimeout(cue));
    g.cy.on('expandcollapse.aftercollapse', 'node', e => {
        if (!g.quietFolds) g.emit('sedna-graph-collapse', { id: e.target.id(), label: e.target.data('label') });
        afterStructure(g);
    });
    g.cy.on('expandcollapse.afterexpand', 'node', e => {
        measureBoxes(g);
        if (g.options.nodes !== 'box') g.cy.batch(() => e.target.union(e.target.descendants()).union(e.target.descendants().connectedEdges()).data('zoom', g.step));
        if (g.quietFolds) {
            afterStructure(g);
            return;
        }
        g.emit('sedna-graph-expand', { id: e.target.id(), label: e.target.data('label') });
        afterStructure(g);
        clearTimeout(g.unfolding);
        g.unfolding = setTimeout(() => { if (!g.disposed) arrange(g, true).catch(report); }, reducedMotion() ? 0 : 300);
    });
    return g.ec;
}
async function collapseGroups(g, mode, ele) {
    const ec = await collapsible(g);
    if (ele) {
        if (mode === 'expand' && ec.isExpandable(ele)) ec.expand(ele);
        else if (mode === 'collapse' && ec.isCollapsible(ele)) ec.collapse(ele);
    } else if (mode === 'expand') {
        ec.expandAll();
    } else {
        ec.collapseAll();
    }
}
const isGroup = n => n.isParent() || n.hasClass('cy-expand-collapse-collapsed-node');
function toggleFold(g, n) {
    if (!g.ec || !isGroup(n)) return false;
    if (g.ec.isExpandable(n)) g.ec.expand(n);
    else if (g.ec.isCollapsible(n)) g.ec.collapse(n);
    else return false;
    return true;
}
function foldedIds(g) {
    if (!g.ec) return [];
    return g.cy.nodes('.cy-expand-collapse-collapsed-node').map(n => n.id());
}
function afterStructure(g) {
    if (g.hullsOn) drawHulls(g);
    g.minimap?.draw();
    declutter(g);
    changed(g);
}
async function hulls(g, on) {
    g.hullsOn = !!on;
    if (!on) {
        clearHulls(g);
        return;
    }
    await plugin('layers');
    await plugin('bubblesets');
    if (!g.bb) g.bb = g.cy.bubbleSets({ interactive: false, throttle: 60 });
    drawHulls(g);
}
function clearHulls(g) {
    if (!g.bb) return;
    g.bb.getPaths().slice().forEach(p => g.bb.removePath(p));
    if (g.cy.destroyed()) return;
    g.cy.elements().forEach(e => {
        const scratch = e.scratch();
        if (scratch && 'bubbleSets' in scratch) delete scratch.bubbleSets;
    });
}
function drawHulls(g) {
    if (!g.bb) return;
    const cy = g.cy;
    clearHulls(g);
    const shown = cy.nodes().not('.hidden').filter(n => !n.isParent() && n.data('group'));
    const groups = new Map();
    shown.forEach(n => {
        const k = n.data('group');
        if (!groups.has(k)) groups.set(k, []);
        groups.get(k).push(n);
    });
    const all = cy.nodes().not('.hidden').filter(n => !n.isParent());
    for (const [key, members] of groups) {
        if (members.length < 2) continue;
        const nodes = cy.collection(members);
        const heart = cy.getElementById(key);
        let tone = heart.nonempty() ? nodeTone(heart, g.options) : null;
        if (!tone) {
            const count = new Map();
            members.forEach(m => { const t = nodeTone(m, g.options); count.set(t, (count.get(t) || 0) + 1); });
            tone = [...count.entries()].sort((a, b) => b[1] - a[1])[0][0];
        }
        const colour = g.colours.token(tone, palette(g.colours).line);
        g.bb.addPath(nodes, nodes.edgesWith(nodes).not('.hidden'), all.not(nodes), {
            virtualEdges: true,
            style: { fill: withAlpha(colour, 0.12), stroke: withAlpha(colour, 0.55), strokeWidth: '1.5' },
        });
    }
    setTimeout(() => { if (!g.disposed && g.bb) g.bb.update(true); }, 0);
}
const graphs = new Map();
function arrive(g, nodes) {
    const region = scoped(g, '[data-graph-live]')[0];
    const template = region?.getAttribute('data-graph-announce-arrive');
    if (template) {
        region.textContent = '';
        const words = nodes.not('.hidden').map(n => fillAnnouncement(template, n)).join(' ');
        setTimeout(() => { if (!g.disposed) region.textContent = words; }, 30);
    }
    if (g.options.arrive !== 'pop' || reducedMotion() || document.visibilityState !== 'visible' || nodes.length > 24) return;
    nodes.not('.hidden').forEach(n => {
        const colour = n.style('border-color');
        n.style({ 'overlay-color': colour, 'overlay-opacity': 0.4, 'overlay-padding': 2 });
        n.animation({ style: { 'overlay-opacity': 0, 'overlay-padding': 18 }, duration: 700, easing: 'ease-out' })
            .play().promise('completed')
            .then(() => { if (!g.disposed && n.inside()) n.removeStyle('overlay-color overlay-opacity overlay-padding'); });
    });
}
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
        fitMax: fitMaxOf(d.graphFitMax),
        scale: scaleOf(d.graphScale),
        hold: has('hold'),
        managed: has('managed'),
        deferred: has('deferred'),
        connect: has('connect'),
        collapse: has('collapse'),
        hulls: has('hulls'),
        arrive: d.graphArrive === 'pop' ? 'pop' : null,
    };
    Object.assign(o, given || {});
    if (!knownLayout(o.layout)) {
        warnOnce('layout:' + o.layout, `"${o.layout}" is not a layout; using force.`);
        o.layout = 'force';
    }
    o.direction = directionOf(o.direction);
    o.edgeLabelsChosen = !!o.edgeLabels;
    followEdgeLabels(o);
    o.curveChosen = !!o.curve;
    o.curve = o.curve || 'bezier';
    return o;
}
function fitMaxOf(value) {
    if (value === undefined || value === '') return null;
    const n = Number(value);
    if (Number.isFinite(n) && n > 0) return n;
    warnOnce('fit-max:' + value, `data-graph-fit-max "${value}" is not a zoom above 0; the fit keeps its own ceiling.`);
    return null;
}
function scaleOf(value) {
    if (value === undefined || value === '') return 1;
    const n = Number(value);
    if (Number.isFinite(n) && n > 0) return n;
    warnOnce('scale:' + value, `data-graph-scale "${value}" is not a number above 0; the graph is drawn at its own size.`);
    return 1;
}
const VIEW_OPTIONS = ['layout', 'direction', 'spacing', 'nodes', 'labels', 'edgeLabels', 'edgeLabelsChosen', 'colourBy', 'curve', 'curveChosen', 'arrows'];
const viewOf = o => Object.fromEntries(VIEW_OPTIONS.map(k => [k, o[k]]));
function followEdgeLabels(o) {
    if (!o.edgeLabelsChosen) o.edgeLabels = o.nodes === 'box' ? 'always' : 'hover';
}
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
            g.options[key] = key === 'nodes' ? (c.value === 'box' ? 'box' : 'dot') : key === 'direction' ? directionOf(c.value) : c.value;
            if (key === 'edgeLabels') g.options.edgeLabelsChosen = true;
            if (key === 'curve') {
                g.options.curveChosen = !!c.value;
                g.options.curve = c.value || 'bezier';
            }
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
            size: sizeOf(n.weight ?? d + (groupSize.get(n.id) || 0) / 2),
            hub: n.hub || n.root || groupNames.has(n.id) || parents.has(n.id),
            marked: !!n.hub,
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
            data: Object.assign({}, e, {
                across: !!(s.group && t.group && s.group !== t.group),
                run: e.state || (s.state === 'skipped' ? 'skipped' : t.state) || null,
                zoom: g.step || 1,
            }),
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
        const next = Object.assign({ display: mode }, mode === 'box' ? boxMetrics(model, font) : { boxLabel: model.label });
        if (!sameData(n.data(), next)) n.data(next);
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
        state: d.state ?? null,
        placeholder: !!d.placeholder,
        tags: d.tags || [],
        fields,
        keyboard: false,
    }, extra || {});
}
const STYLE_OPTIONS = ['nodes', 'layout', 'direction', 'curve', 'curveChosen', 'edgeLabels', 'arrows', 'colourBy', 'veil'];
const styleKeyOf = o => STYLE_OPTIONS.map(k => String(o[k])).join('|');
function restyle(g, force) {
    const key = styleKeyOf(g.options);
    if (force || key !== g.styleKey) {
        g.styleKey = key;
        g.cy.style(styleFor(g.colours, g.icons, g.options, g.extraStyle));
    }
    colouring(g);
}
function colouring(g) {
    g.el.setAttribute('data-graph-colouring', g.options.colourBy || 'tone');
}
function changed(g) {
    const s = stats(g);
    const empty = g.model.nodes.length === 0;
    if (g.failed) g.el.setAttribute('data-graph-state', 'error');
    else if (!g.awaiting) g.el.setAttribute('data-graph-state', empty ? 'empty' : s.nodes === 0 ? 'filtered' : 'ready');
    writeStats(g, s);
    g.edgeTools?.sync();
    showZoom(g, true);
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
function open(g, ele, keyboard) {
    const detail = nodeDetail(ele, { keyboard: !!keyboard });
    const href = window.sednaUi?._?.safeHref?.(detail.href);
    if (!g.emit('sedna-graph-open', detail, true) || !href) return;
    const a = document.createElement('a');
    a.href = href;
    document.body.appendChild(a);
    a.click();
    a.remove();
}
async function arrange(g, animate) {
    const cy = g.cy;
    const run = (g.arranging = (g.arranging || 0) + 1);
    const moving = cy.nodes().not('.hidden').filter(n => !n.isParent());
    const before = animate && cy.container() && moving.length <= 600 && !reducedMotion() && document.visibilityState === 'visible'
        ? new Map(moving.map(n => [n.id(), Object.assign({}, n.position())])) : null;
    unzoomed(g);
    mount(g);
    const measuring = measuresNames(g);
    const quiet = measuring ? cy.nodes('.unlabelled') : cy.collection();
    if (quiet.nonempty()) cy.batch(() => quiet.removeClass('unlabelled'));
    if (measuring) cy.nodes().updateStyle();
    g.laying = (g.laying || 0) + 1;
    try {
        await arrangeWith(cy, g.options.layout, g.options, g.el);
        await atScale(g, run);
    } finally {
        g.laying--;
        if (quiet.nonempty() && !cy.destroyed()) cy.batch(() => quiet.filter(n => n.inside()).addClass('unlabelled'));
    }
    if (run !== g.arranging || g.disposed) return;
    if (before) {
        const after = new Map(moving.map(n => [n.id(), Object.assign({}, n.position())]));
        if (!g.touched) fitView(g, null, true);
        cy.batch(() => moving.forEach(n => { if (before.has(n.id())) n.position(before.get(n.id())); }));
        moving.forEach(n => n.animate({ position: after.get(n.id()) }, { duration: 380, easing: 'ease-in-out-cubic' }));
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
const measuresNames = g => g.options.nodes !== 'box'
    && (g.options.layout === 'dagre' || g.options.labels === 'all' || g.cy.nodes(':parent').not('.hidden').nonempty());
async function atScale(g, run) {
    const cy = g.cy;
    if (g.touched || !measuresNames(g)) return;
    cy.resize();
    const visible = () => cy.elements().not('.hidden').not('.eh-ghost, .eh-preview, .eh-handle');
    const grouped = g.options.layout !== 'dagre' && g.options.labels !== 'all';
    if (grouped) untangle(visible());
    for (let pass = 0; pass < 6; pass++) {
        const shown = cy.nodes().not('.hidden');
        if (shown.empty()) return;
        const seen = stepOf(Math.min(1, Math.max(holdFloor(g), fitOf(g, shown).zoom / g.options.scale)));
        if (seen >= g.step) return;
        g.step = seen;
        cy.batch(() => cy.elements().data('zoom', seen));
        cy.nodes().updateStyle();
        if (grouped) {
            untangle(visible());
        } else {
            await arrangeWith(cy, g.options.layout, g.options, g.el);
            if (run !== g.arranging || g.disposed) return;
        }
    }
}
function unzoomed(g) {
    g.step = 1;
    const names = measuresNames(g);
    const stale = g.cy.nodes().filter(n => (names ? n.data('zoom') !== 1 : n.data('zoom') > 1));
    if (stale.nonempty()) g.cy.batch(() => stale.data('zoom', 1));
}
function mount(g) {
    const cy = g.cy;
    if (cy.container()) return;
    const count = cy.elements().length;
    const webgl = g.options.renderer === 'webgl';
    try {
        cy._private.options.webgl = webgl;
        cy._private.options.textureOnViewport = !webgl && count > 1500;
        cy._private.options.hideEdgesOnViewport = !webgl && count > 5000;
        cy._private.options.pixelRatio = pixelRatioOf(g.host);
    } catch (e) { /* an engine that moved its options: the canvas renderer, as made */ }
    cy.mount(g.host);
    g.host._cyreg = Object.assign({}, g.host._cyreg, { cy });
    veilOf(g);
    const canvas = g.el.querySelector('[data-graph-minimap]');
    if (canvas) g.minimap = minimap(g, canvas);
}
async function fetchData(url) {
    const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error(`data-graph-src "${url}" answered ${response.status}.`);
    return response.json();
}
function sameData(had, next) {
    for (const k in next) {
        const a = had[k], b = next[k];
        if (a === b) continue;
        if (a && b && typeof a === 'object' && typeof b === 'object') {
            if (JSON.stringify(a) !== JSON.stringify(b)) return false;
            continue;
        }
        if ((a ?? null) !== (b ?? null)) return false;
    }
    return true;
}
function positionsNear(g, ids) {
    const cy = g.cy;
    const fresh = new Set(ids);
    const next = new Map();
    for (const e of g.model.edges) {
        if (fresh.has(e.source)) (next.get(e.source) || next.set(e.source, []).get(e.source)).push(e.target);
        if (fresh.has(e.target)) (next.get(e.target) || next.set(e.target, []).get(e.target)).push(e.source);
    }
    let bb = null;
    let loose = 0;
    return ids.map((id, i) => {
        const placed = (next.get(id) || []).map(o => cy.getElementById(o)).filter(o => o.nonempty() && o.inside());
        if (placed.length) {
            const x = placed.reduce((s, o) => s + o.position('x'), 0) / placed.length;
            const y = placed.reduce((s, o) => s + o.position('y'), 0) / placed.length;
            return { x: x + 40 * Math.cos((i + 1) * golden), y: y + 40 * Math.sin((i + 1) * golden) };
        }
        bb = bb || cy.nodes().not('.hidden').boundingBox({ includeLabels: false });
        return { x: (Number.isFinite(bb.x2) ? bb.x2 : 0) + 60, y: (Number.isFinite(bb.y1) ? bb.y1 : 0) + 40 * loose++ };
    });
}
async function setData(g, data, opts = {}) {
    await g.started;
    if (g.disposed) return;
    const cy = g.cy;
    const first = g.awaiting;
    if (first) {
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
    const leaving = removed.nodes().filter(n => n.isParent());
    if (leaving.nonempty()) {
        leaving.children().filter(c => wanted.has(c.id()) && !leaving.contains(c)).forEach(c => c.move({ parent: null }));
    }
    removed.remove();
    const freshNodes = fresh.filter(e => e.group === 'nodes');
    const near = positionsNear(g, freshNodes.filter(e => !e.position).map(e => e.data.id));
    let i = 0;
    const addedNodes = cy.add(freshNodes.map(e => Object.assign(e, { position: e.position || near[i++] })));
    cy.batch(() => {
        els.forEach(e => {
            const ex = cy.getElementById(e.data.id);
            if (ex.empty() || addedNodes.contains(ex)) return;
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
            delete d.zoom;
            if (target !== ex || !sameData(ex.data(), d)) target.data(d);
        });
    });
    const added = addedNodes.union(cy.add(fresh.filter(e => e.group === 'edges')));
    if (!reducedMotion() && !first && added.length <= 240) {
        added.addClass('entering');
        setTimeout(() => { if (!g.disposed) added.removeClass('entering'); }, 30);
    }
    applyFilter(g, filterFromControls(g).spec);
    if (g.query) search(g, g.query, false);
    const shapeMatters = ['dagre', 'tree', 'grid', 'circle', 'concentric', 'fcose'].includes(g.options.layout);
    const many = addedNodes.length > Math.max(3, 0.25 * cy.nodes().length);
    if (opts.relayout || (!opts.quiet && (many || (shapeMatters && (addedNodes.nonempty() || removed.nodes().nonempty()))))) {
        await arrange(g, !first);
    } else if (addedNodes.nonempty() && g.options.layout !== 'preset') {
        const compound = addedNodes.some(n => n.isChild() || n.isParent());
        if (compound) {
            await arrange(g, true);
        } else {
            unzoomed(g);
            const around = addedNodes.union(addedNodes.neighborhood()).not('.hidden');
            try {
                await settled(springs(around.union(around.edgesWith(around)), around.nodes().length, spacingOf(g.options.spacing), { numIter: 300 }));
            } catch (e) {
                report(e);
                await arrange(g, true);
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
    flow(g);
    if (!first && addedNodes.nonempty()) arrive(g, addedNodes);
    const s = changed(g);
    if (first) g.emit('sedna-graph-ready', s);
}
function repaint(g) {
    if (g.disposed || !g.cy) return;
    if (!g.colours.refresh()) return;
    g.icons.clear();
    restyle(g, true);
    flow(g);
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
    let pending = 0;
    cy.on('viewport', () => {
        showZoom(g, false);
        clearTimeout(pending);
        pending = setTimeout(() => {
            pending = 0;
            if (g.disposed) return;
            const at = g.settledAt, pan = cy.pan();
            if (at && at.zoom === cy.zoom() && at.x === pan.x && at.y === pan.y) return;
            settle(g);
        }, 140);
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
    on(g.host, 'pointerleave', () => {
        g.tip.hide();
        g.host.style.cursor = '';
        clearTimeout(leaving);
        if (!dragging && !g.disposed) light(g, resting(g));
    });
    g.listeners.push(() => clearTimeout(leaving));
    cy.on('tap', 'node', event => {
        const node = event.target;
        if (node.hasClass('eh-handle') || node.hasClass('eh-ghost') || g.drawing) return;
        g.menu.close(false);
        if (g.options.open === 'tap' || (node.data('placeholder') && g.options.open !== 'none')) {
            open(g, node, false);
            return;
        }
        if (g.keyed) { g.keyed.removeClass('keyed'); g.keyed = null; }
        select(g, node);
    });
    cy.on('dbltap', 'node', event => {
        if (g.drawing) return;
        if (toggleFold(g, event.target)) return;
        if (g.options.open !== 'none' && !event.target.isParent() && !event.target.data('placeholder')) open(g, event.target, false);
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
    if (g.options.wheel === 'focus') {
        on(g.host, 'pointerdown', () => cy.userZoomingEnabled(true));
        on(g.host, 'pointerleave', () => cy.userZoomingEnabled(false));
    }
    cy.on('dragpan scrollzoom pinchzoom', () => { g.touched = true; });
    const pointed = yes => {
        if (yes !== el.hasAttribute('data-graph-pointer')) el.toggleAttribute('data-graph-pointer', yes);
    };
    on(g.host, 'pointerdown', () => { g.pressing = true; });
    on(g.host, 'pointerup', () => {
        if (document.activeElement !== el) el.focus({ preventScroll: true });
        pointed(document.activeElement === el);
    });
    on(window, 'pointerup', () => { g.pressing = false; });
    on(window, 'pointercancel', () => { g.pressing = false; });
    on(el, 'keydown', e => { if (!['Shift', 'Control', 'Alt', 'Meta'].includes(e.key)) pointed(false); });
    on(el, 'focusout', e => { if (!el.contains(e.relatedTarget)) pointed(false); });
    let was = null;
    const size = new ResizeObserver(() => {
        if (!cy.container() || g.disposed) return;
        const before = was || { w: cy.width(), h: cy.height() };
        cy.resize();
        was = { w: cy.width(), h: cy.height() };
        if (g.travelling || g.laying) g.refit = true;
        else if (!g.touched) fitView(g, null, false);
        else {
            cy.panBy({ x: (was.w - before.w) / 2, y: (was.h - before.h) / 2 });
            settle(g);
        }
    });
    size.observe(g.host);
    g.observers.push(size);
    const sharp = new ResizeObserver(() => {
        if (!cy.container() || g.disposed) return;
        const r = cy.renderer();
        const ratio = pixelRatioOf(g.host);
        if (!r || r.forcedPixelRatio === ratio) return;
        r.forcedPixelRatio = ratio;
        cy.resize();
        if (g.model?.nodes.some(n => n.icon)) {
            g.icons.clear();
            restyle(g, true);
        }
    });
    try { sharp.observe(g.host, { box: 'device-pixel-content-box' }); } catch (e) { sharp.observe(g.host); }
    g.observers.push(sharp);
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
    on(document, 'visibilitychange', () => flow(g));
    const calm = window.matchMedia('(prefers-reduced-motion: reduce)');
    const moved = () => flow(g);
    calm.addEventListener('change', moved);
    g.listeners.push(() => calm.removeEventListener('change', moved));
    g.listeners.push(() => clearTimeout(g.flowing));
    let wasFull = false;
    on(document, 'fullscreenchange', () => {
        const frame = el.closest('[data-graph-frame]') || el;
        const full = document.fullscreenElement === frame;
        if (!full && !wasFull) return;
        wasFull = full;
        controlsOf(g).filter(c => c.getAttribute('data-graph-action') === 'fullscreen')
            .forEach(c => c.setAttribute('aria-pressed', full ? 'true' : 'false'));
        setTimeout(() => {
            if (g.disposed) return;
            cy.resize();
            g.touched = false;
            fitView(g, null, false);
        }, 60);
    });
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
        minZoom: MIN_ZOOM * g.options.scale,
        maxZoom: (g.options.nodes === 'box' ? 4 : 8) * g.options.scale,
        boxSelectionEnabled: false,
        selectionType: 'single',
        autoungrabify: !g.options.drag,
        autounselectify: g.options.select === 'none',
        userZoomingEnabled: g.options.wheel === 'always',
        textureOnViewport: count > 1500,
        hideEdgesOnViewport: count > 5000,
    });
    g.cy.scratch('_sedna', g);
    g.styleKey = styleKeyOf(g.options);
    g.tip = tips(g);
    g.menu = menus(g);
    g.edgeTools = edgeTools(g);
    wire(g);
    g.keys = keyboard(g);
    rememberControls(g);
    applyFilter(g, filterFromControls(g).spec);
    const typed = controlsOf(g).find(c => c.hasAttribute('data-graph-search'));
    if (typed && typed.value) g.query = typed.value;
    await arrange(g, false);
    if (g.disposed) return;
    if (g.query) search(g, g.query, false);
    showDetail(g);
    flow(g);
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
    g.edgeTools?.destroy();
    g.minimap?.destroy();
    g.tip?.hide();
    try { g.eh?.destroy(); } catch (e) { /* ignore */ }
    try { g.bb?.destroy(); } catch (e) { /* ignore */ }
    try { g.veil?.destroy(); } catch (e) { /* ignore */ }
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
        get cy() { return g.cy; },
        ready: () => g.started.then(() => stats(g)),
        set: (data, opts) => setData(g, data, opts).then(() => stats(g)),
        filter: async spec => {
            g.baseFilter = Object.assign({}, spec || {});
            g.hiddenIds.clear();
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
            for (const k of ['direction', 'spacing']) if (opts && opts[k]) g.options[k] = k === 'direction' ? directionOf(opts[k]) : opts[k];
            g.touched = false;
            restyle(g);
            await arrange(g, true);
            changed(g);
        },
        fit: () => fitAll(g),
        zoom: factor => zoomBy(g, factor),
        option: (name, value) => option(g, name, value),
        style: rules => {
            g.extraStyle = Array.isArray(rules) ? rules : [];
            restyle(g, true);
        },
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
function sweep() {
    for (const g of [...graphs.values()]) if (!g.el.isConnected) dispose(g);
}
export { attach, sweep, normalise };

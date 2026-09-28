/* ═══════════════════════════════════════════════════════════════════════════
   GENERATED FILE — DO NOT EDIT.

   Built by build/bundle-js.sh from src/Sedna.UI/graph-parts/. Edit the part
   that owns the behaviour and re-run that script; a guard test fails the build
   if this file and the parts disagree. Adding a part needs no change here —
   the directory is the source of truth.

   Contents, in load order:
     00-imports.js
     10-model.js
     20-tokens.js
     21-icons.js
     22-style.js
     30-layouts.js
     31-islands.js
     40-view.js
     41-light.js
     42-filter.js
     50-minimap.js
     51-tip.js
     52-keyboard.js
     53-menu.js
     54-detail.js
     55-controls.js
     60-export.js
     61-plugins.js
     70-graph.js
     99-exports.js
   ═══════════════════════════════════════════════════════════════════════════ */

/* ── 00-imports.js ──────────────────────────────────────────────── */
/* Sedna.UI — the graph.
   ───────────────────────────────────────────────────────────────────────────
   An ES module, imported by Sedna.UI.js the first time a page shows a `[data-graph]`
   element. No app references this file: a page without a graph never downloads it,
   or the engine below it.

   cytoscape.js draws the canvas and runs the layouts. Everything a reader sees AROUND
   the canvas — the toolbar, the legend, the filters, the panel beside it, the tooltip,
   the empty state, the minimap's frame — is the app's own markup with the library's
   classes. What this module adds is the part markup cannot be:

     * the records, read from the app's markup, a URL or a call, into one model;
     * every colour on the canvas taken from a Sedna.UI token, repainted whenever the
       theme, the variant, the colour-vision setting or forced colours change;
     * layouts that read a real graph well — islands per group, rings around a record,
       hierarchies, compound groups — and names placed the way a map places them;
     * filters, search, focus, selection and the controls that drive them;
     * a keyboard and screen-reader model for a surface that is otherwise a picture;
     * the minimap, the SVG and PNG export, and the plugins, loaded when first used.

   The parts in graph-parts/ are this one module cut into files. They share its
   top-level scope: only 00 imports and only 99 exports. See graph-parts/CLAUDE.md.
   ─────────────────────────────────────────────────────────────────────────── */
import cytoscape from '../lib/cytoscape/cytoscape.js';

/* ── 10-model.js ──────────────────────────────────────────────── */
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
    if (TONE_TOKENS[t]) return t;
    if (/^series-/.test(t) && TONE_TOKENS[t.slice(7)]) return t.slice(7);
    if (/^--[a-z0-9-]+$/i.test(t)) return t;
    warnOnce('tone:' + t, `"${t}" is not a tone. Use 1–6, go, warn, danger, info, muted, brand, accent, or a token name.`);
    return null;
}

const tokenOfTone = tone => (tone ? (TONE_TOKENS[tone] || tone) : null);

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
const kebab = s => String(s).replace(/[A-Z]/g, c => '-' + c.toLowerCase());

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
    if (shape && !SHAPES[shape]) warnOnce('shape:' + shape, `"${shape}" is not a shape; drawing a circle.`);
    const display = text(raw.display);
    return {
        id,
        label: text(raw.label) ?? id,
        kind: text(raw.kind),
        tone: toneOf(raw.tone),
        tones,
        shape: shape ? (SHAPES[shape] || null) : null,
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
        weight: typeof w === 'string' && WEIGHTS[w] ? WEIGHTS[w] : (number(w) ?? 1),
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
        if (k.startsWith('tone') && k.length > 4) {
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

/* ── 20-tokens.js ──────────────────────────────────────────────── */
/* ── Colour, from the tokens ──────────────────────────────────────────────────
   A canvas cannot paint `var(--viz-2)`. A probe element can resolve it: the browser
   works the token out through every layer of the theme — variant, colour vision,
   contrast, forced colours, an app's own brand file — and a one-pixel canvas turns
   whatever it serialises as (`rgb()`, `color(srgb …)` for a color-mix() token) into the
   rgba() cytoscape parses. Cached per token until the theme changes.

   The background a label is outlined against is not a token at all: it is whatever
   the graph actually sits on, read from the nearest ancestor that paints one. A graph
   in a card outlines its labels in the card's colour, one on the page in the page's.
   ─────────────────────────────────────────────────────────────────────────── */

function colourReader(host) {
    const probe = document.createElement('span');
    probe.setAttribute('aria-hidden', 'true');
    probe.style.cssText = 'position:absolute;width:0;height:0;overflow:hidden;visibility:hidden;pointer-events:none';
    host.appendChild(probe);
    const pixel = document.createElement('canvas');
    pixel.width = pixel.height = 1;
    const ctx = pixel.getContext('2d', { willReadFrequently: true });
    const cache = new Map();

    // Whatever the browser serialised, as rgba() — by painting it and reading it back.
    function rgba(value, fallback) {
        if (!value || value === 'transparent') return fallback;
        if (/^rgba?\(/.test(value) && !/\//.test(value)) return value;
        // Reset to nothing first: a value the canvas cannot parse leaves the previous fill
        // in place, and reads back as transparent — so as the fallback — rather than as
        // whatever was painted last.
        ctx.clearRect(0, 0, 1, 1);
        ctx.fillStyle = 'transparent';
        ctx.fillStyle = value;
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
        // The colour behind the graph: the first ancestor that paints a background.
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
        remove: () => probe.remove(),
        probe,
    };
}

/* The neutral colours every drawing uses, resolved once per paint. Tokens only, and no
   literal behind them: a token the page does not declare leaves the probe's colour to
   inherit, so it reads as the text around the graph rather than failing. */
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
        accent: c('--accent', fg),
        ring: c('--brand-ring', fg),
        raised: c('--surface-raised-2', c('--card-bg')),
        tint: c('--brand-tint', c('--card-bg')),
        ground: colours.ground(c('--card-bg')),
        font: colours.font(),
    };
}

// A colour with its alpha replaced — for a group's fill, drawn at a whisper of its tone.
function withAlpha(colour, alpha) {
    const m = /rgba?\(\s*(\d+),\s*(\d+),\s*(\d+)/.exec(colour || '');
    return m ? `rgba(${m[1]}, ${m[2]}, ${m[3]}, ${alpha})` : colour;
}

/* ── 21-icons.js ──────────────────────────────────────────────── */
/* ── Icons on the canvas ──────────────────────────────────────────────────────
   `data-icon="ri-server-line"` puts the Remix Icon glyph inside a record. The canvas
   cannot use the icon font's classes, so the glyph is read from the class's own
   `::before` content, drawn once onto a small canvas in the colour it needs, and handed
   to cytoscape as an image. The font is the one the stylesheet already loads; a glyph
   asked for before it has arrived is drawn again once it has.

   Nothing is fetched: an icon the font does not have draws nothing, and says so once.
   ─────────────────────────────────────────────────────────────────────────── */

function iconPainter(host) {
    const probe = document.createElement('i');
    probe.setAttribute('aria-hidden', 'true');
    probe.style.cssText = 'position:absolute;width:0;height:0;overflow:hidden;visibility:hidden;pointer-events:none';
    host.appendChild(probe);
    const glyphs = new Map();
    const images = new Map();
    let fontReady = null;

    function glyph(cls) {
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
        /* Resolves once the icon font can draw — so the first paint has its icons.
           Settles either way: a page that blocks the font still gets its graph. */
        ready() {
            if (!fontReady) {
                fontReady = (document.fonts && document.fonts.load
                    ? document.fonts.load('32px remixicon').catch(() => null)
                    : Promise.resolve()).then(() => { glyphs.clear(); images.clear(); });
            }
            return fontReady;
        },
        // A data: URL of the glyph in `colour`, square, `px` CSS pixels at device resolution.
        image(cls, colour, px = 32) {
            if (!cls) return null;
            const key = cls + '|' + colour + '|' + px;
            if (images.has(key)) return images.get(key);
            const g = glyph(cls);
            if (!g) {
                images.set(key, null);
                return null;
            }
            const scale = Math.min(Math.max(window.devicePixelRatio || 1, 1), 3) * 2;
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

/* ── 22-style.js ──────────────────────────────────────────────── */
/* ── The canvas's stylesheet ──────────────────────────────────────────────────
   Two ways to draw a record:

     dot   a shape sized by how connected it is, its name beneath — a map of many
           records, where the shape of the whole is the point. Names are held at a
           readable screen size whatever the zoom, and placed by the declutter
           (40-view.js) so they never write over each other.
     box   the name inside a box sized to fit it — a diagram of a few dozen, where each
           record is read: an org chart, a flow, a state machine, a dependency tree.

   `data-graph-nodes="box"` switches a whole graph; `data-display` one record.

   Every colour is a token (20-tokens.js), and colour is never the only thing that
   carries meaning: a kind has its shape, a link its line style and its label.
   ─────────────────────────────────────────────────────────────────────────── */

const reducedMotion = () => window.matchMedia('(prefers-reduced-motion: reduce)').matches;

/* Sizes that are held on screen, whatever the zoom. cytoscape works a style out once
   per state of an element's data, so the zoom reaches the style as data — `zoom`, in
   steps of a few percent (40-view.js) — rather than as a call to cy.zoom(). */
const zoomOf = ele => ele.data('zoom') ?? 1;
const held = ele => (zoomOf(ele) <= 1 ? 1 : 1 / Math.pow(zoomOf(ele), 0.8));
const LABEL_MAX_PX = 180;
const labelPx = n => 10.5 + Math.min(n.data('degree') || 0, 14) * 0.2 + (n.data('hub') ? 1.5 : 0);
const labelSize = n => labelPx(n) / zoomOf(n);
const sizeOf = weight => Math.min(14 + 5 * Math.sqrt(Math.max(weight, 0)), 46);

/* Where a dot's name goes. Beneath it, except in a hierarchy that runs sideways: there a
   level is a column, a name beneath its record would make every column as tall as its
   records and their names, and beside it — towards the next level — is where the room is. */
const labelSide = options => (options.nodes !== 'box' && (options.layout === 'tree' || options.layout === 'dagre')
    ? options.direction === 'LR' ? 'right' : options.direction === 'RL' ? 'left' : 'bottom'
    : 'bottom');

/* Which tone a record wears: the one for the colouring the reader chose
   (`data-graph-colour="team"` reads `data-tone-team`), else its own, else the brand. */
function nodeTone(n, options) {
    const key = options.colourBy;
    const alt = key && key !== 'tone' ? (n.data('tones') || {})[camel(key)] : null;
    return tokenOfTone(alt ?? n.data('tone') ?? (n.data('muted') ? 'muted' : '1'));
}

/* An app's own rules on top of the library's — `graph.style([...])`. A value written
   `var(--token)` is resolved through the same probe as everything else, so an app's rule
   follows the theme too; any other value is handed to the engine as it is. */
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
    const arrowOf = (e, end) => {
        const a = e.data('arrow') || options.arrows;
        return a === 'both' || a === end ? 'triangle' : 'none';
    };
    const iconFor = n => {
        const cls = n.data('icon');
        if (!cls) return 'none';
        return icons.image(cls, box(n) ? toneColour(n) : p.ground, 32) || 'none';
    };
    const lineStyle = e => (e.data('line') === 'dotted' ? 'dotted' : e.data('line') === 'dashed' ? 'dashed' : 'solid');
    // A layered layout knows where each link has to go to pass the ranks between its ends;
    // its route is drawn unless the app chose a curve. Read from where the layout left it.
    const routed = options.layout === 'dagre' && !options.curveChosen;
    const route = routed ? {
        'curve-style': 'unbundled-bezier',
        'control-point-weights': e => e.scratch('controlPointWeights') || [0.5],
        'control-point-distances': e => e.scratch('controlPointDistances') || [0],
        'edge-distances': 'intersection',
    } : {};
    const edgeLabel = options.edgeLabels === 'always' ? (e => e.data('label') || '') : '';
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
                'shape': n => n.data('shape') || 'ellipse',
                'background-color': toneColour,
                'background-image': iconFor,
                'background-fit': 'none',
                'background-width': n => (n.data('size') || 20) * held(n) * 0.56,
                'background-height': n => (n.data('size') || 20) * held(n) * 0.56,
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
                'min-zoomed-font-size': 0,
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
        { selector: 'node[?hub]', style: { 'font-weight': 600, 'color': p.fg } },
        // Settled or less important records stay on the map, quieter: they explain how something got here.
        { selector: 'node[?muted]', style: { 'background-opacity': 0.45, 'color': p.muted } },
        {
            // A box: the name inside, sized to fit, an icon before it. Drawn at the zoom like a diagram is.
            selector: 'node[display = "box"]',
            style: {
                'shape': n => n.data('shape') || 'round-rectangle',
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
            },
        },
        { selector: 'node[display = "box"][?muted]', style: { 'background-opacity': 0.6, 'border-opacity': 0.6, 'color': p.muted } },
        // A group that holds records — a compound node. A whisper of its tone, its name at the top,
        // held at a screen size around dots like the names inside it.
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
        // A group folded away (expand-collapse): its name inside, dashed, holding its count.
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
        // A name there was no room for. Declared before everything that insists on a name, which wins over it.
        { selector: 'node.unlabelled', style: { 'label': '' } },
        {
            selector: 'node[?root], node.focus',
            style: { 'border-width': n => (box(n) ? 2.5 : 3 * held(n)), 'border-color': p.brand, 'color': p.fg, 'font-weight': 600, 'label': nameOf },
        },
        {
            selector: 'edge',
            style: {
                'width': e => (e.data('weight') || 1) * held(e) * (options.nodes === 'box' ? 1.25 : 1),
                'curve-style': options.curve,
                'taxi-direction': options.direction === 'LR' || options.direction === 'RL' ? 'horizontal' : 'vertical',
                'taxi-turn': '50%',
                'line-color': edgeTone,
                'line-style': lineStyle,
                'line-dash-pattern': e => (e.data('line') === 'dotted' ? [1.5, 3.5] : [6, 4]),
                'target-arrow-color': edgeTone,
                'source-arrow-color': edgeTone,
                'target-arrow-shape': e => arrowOf(e, 'target'),
                'source-arrow-shape': e => arrowOf(e, 'source'),
                'arrow-scale': options.nodes === 'box' ? 0.9 : 0.7,
                // The line fades, not the label on it — arrowheads fade with the line.
                'line-opacity': options.nodes === 'box' ? 0.8 : 0.4,
                'label': edgeLabel,
                'font-size': e => (options.nodes === 'box' ? 11 : 10 / zoomOf(e)),
                'font-family': p.font,
                'color': p.soft,
                'text-background-color': p.ground,
                'text-background-opacity': 0.92,
                'text-background-padding': e => 2 / (options.nodes === 'box' ? 1 : zoomOf(e)),
                'text-background-shape': 'round-rectangle',
                'text-rotation': options.nodes === 'box' ? 'none' : 'autorotate',
                'transition-property': 'opacity',
                'transition-duration': reducedMotion() ? 0 : 160,
                ...route,
            },
        },
        // The links of a folded group, redrawn to it: straight, so every link between the same
        // two ends is one line rather than a fan of parallel curves.
        { selector: 'edge.cy-expand-collapse-meta-edge', style: { 'curve-style': 'straight' } },
        { selector: 'edge[?muted]', style: { 'line-opacity': 0.2 } },
        // A bridge between two islands is the quietest line on the map until its record is pointed at.
        { selector: 'edge[?across]', style: { 'line-opacity': options.nodes === 'box' ? 0.5 : 0.16 } },

        { selector: '.hidden', style: { 'display': 'none' } },
        { selector: '.dim', style: { 'opacity': 0.1, 'text-opacity': 0 } },
        { selector: 'node.lit', style: { 'color': p.fg, 'label': nameOf, 'z-index': 10 } },
        {
            selector: 'edge.lit',
            style: { 'line-opacity': 1, 'width': e => Math.max(2, (e.data('weight') || 1) * 1.6) * held(e), 'label': e => (options.edgeLabels === 'none' ? '' : e.data('label') || ''), 'z-index': 10 },
        },
        {
            // A match: a ring in the text colour, which stands out on every tone, inside a halo
            // in the accent, which stands out on the ground — so it reads whatever the tone.
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
        { selector: 'node:selected', style: { 'border-width': n => (box(n) ? 2.5 : 3 * held(n)), 'border-color': p.brand, 'color': p.fg, 'label': nameOf } },
        { selector: 'edge:selected', style: { 'line-opacity': 1, 'line-color': p.brand, 'target-arrow-color': p.brand, 'source-arrow-color': p.brand } },
        // The keyboard's place: a ring outside the record, in the focus ring's colour.
        { selector: 'node.keyed', style: { 'outline-width': n => (box(n) ? 3 : 3 * held(n)), 'label': nameOf, 'color': p.fg, 'z-index': 20 } },
        { selector: '.entering', style: { 'opacity': 0 } },

        // Drawing a link (cytoscape-edgehandles): the line follows the pointer in the brand colour.
        { selector: '.eh-handle', style: { 'width': 10, 'height': 10, 'shape': 'ellipse', 'background-color': p.brand, 'border-width': 0, 'label': '', 'background-image': 'none' } },
        { selector: '.eh-source, .eh-target', style: { 'border-width': 3, 'border-color': p.brand } },
        { selector: '.eh-preview, .eh-ghost-edge', style: { 'line-color': p.brand, 'target-arrow-color': p.brand, 'target-arrow-shape': 'triangle', 'line-style': 'dashed', 'line-opacity': 1, 'width': 2 } },
        { selector: '.eh-ghost-edge.eh-preview-active', style: { 'opacity': 0 } },
        { selector: '.eh-ghost-node', style: { 'width': 1, 'height': 1, 'opacity': 0, 'label': '' } },
    ].concat(resolveRules(extra, colours));
}

/* ── 30-layouts.js ──────────────────────────────────────────────── */
/* ── Layouts ──────────────────────────────────────────────────────────────────
   `data-graph-layout` names one:

     force        springs — cytoscape's cose, tuned so a crowd keeps room per head
     islands      one island per `data-group`, each shaped by its own springs, packed
                  in rows as wide as the frame, a `data-cluster`'s islands side by side
                  (31-islands.js). Falls back to force when nothing has a group.
     rings        hops from the root (`data-root`) or the focus, then springs
     concentric   busiest in the middle
     tree         breadth-first from the roots, in `data-graph-direction`
     dagre        a layered hierarchy — org charts, flows, dependency trees (plugin)
     fcose        fast springs that also lay out nested groups (plugin)
     grid, circle
     preset       where `data-x` / `data-y` put each record

   Any other name is run as a cytoscape layout of that name, if one is registered —
   an extension an app added through the handle's `cy` (`cytoscape.use` on the engine it
   exposes) — with its own defaults. An unknown name draws `force`, and says so once.

   Every run is deterministic — ordered by degree and id, never by chance — so the
   drawing a reader learned yesterday is the one that comes back today.

   A plugin is imported the first time a layout needs it, from beside the engine, and
   registered once per page.
   ─────────────────────────────────────────────────────────────────────────── */

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

// A layout name this graph can draw: one of ours, or one registered with the engine.
function knownLayout(name) {
    if (LAYOUT_NAMES.includes(name)) return true;
    try { return !!cytoscape('layout', name); } catch (e) { return false; }
}
const DIRECTIONS = { TB: 'TB', LR: 'LR', BT: 'BT', RL: 'RL', down: 'TB', right: 'LR', up: 'BT', left: 'RL' };
const SPACING = { compact: 0.7, normal: 1, loose: 1.45 };

const golden = Math.PI * (3 - Math.sqrt(5));
const byId = (a, b) => (a.id() < b.id() ? -1 : a.id() > b.id() ? 1 : 0);

/* A layout run to its end. Without animation a layout finishes before run() returns,
   but that is its to decide. */
const settled = layout => new Promise(resolve => {
    layout.one('layoutstop', resolve);
    layout.run();
});

/* Springs for one neighbourhood: what is filed under a group is held close to it, a
   typed link a little looser. Every round in one go — cose otherwise runs a few rounds
   per animation frame even when it is not animating, and a tab that is not painted
   never gets a frame, so a graph opened in a background tab never drew. */
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

/* Hop rings first, then springs — every run starts from the same place, so the force
   pass only pushes overlaps apart instead of finding a shape from noise. */
async function rings(visible, options, spacing, box) {
    const nodes = visible.nodes().filter(n => !n.isParent());
    const maxDegree = Math.max(1, ...nodes.map(n => n.data('degree') || 0));
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
        minNodeSpacing: 30 * spacing,
        boundingBox: box,
        concentric: n => (centre.nonempty() ? 10 - (hops.get(n.id()) ?? 9) : Math.round(6 * (n.data('degree') || 0) / maxDegree)),
        levelWidth: () => 1,
    }).run();
    await settled(springs(visible, nodes.length, spacing));
}

/* What the layout runs over: everything shown, parents included for the layouts that
   understand them. */
async function arrangeWith(cy, name, options, frame) {
    const visible = cy.elements().not('.hidden').not('.eh-ghost, .eh-preview, .eh-handle');
    if (visible.nodes().length === 0) return;
    const spacing = SPACING[options.spacing] ?? (Number(options.spacing) || 1);
    const direction = DIRECTIONS[options.direction] || 'TB';
    const box = options.nodes === 'box' || visible.nodes('[display = "box"]').nonempty();
    const count = visible.nodes().length;
    const aspect = aspectOf(frame);
    // The box a layout that fills one is given: the frame's, since the first drawing is made
    // before the engine has a canvas to measure — scaled up with the crowd, so a large graph
    // is not packed into the pixels of the frame. `turned` is the same box on its side.
    const scale = Math.max(1, Math.sqrt(count / 30));
    const fw = (frame && frame.clientWidth) || 900, fh = (frame && frame.clientHeight) || 500;
    const frameBox = { x1: 0, y1: 0, w: fw * scale * spacing, h: fh * scale * spacing };
    const turned = { x1: 0, y1: 0, w: frameBox.h, h: frameBox.w };

    switch (name) {
        case 'preset': {
            const placed = visible.nodes().filter(n => n.data('x') !== null && n.data('x') !== undefined);
            cy.batch(() => placed.forEach(n => n.position({ x: n.data('x'), y: n.data('y') })));
            const loose = visible.nodes().not(placed).filter(n => !n.isParent());
            if (loose.nonempty()) {
                // The placed records stay where the app put them; the springs place the rest.
                placed.lock();
                try {
                    await settled(springs(visible, count, spacing, { randomize: false }));
                } finally {
                    placed.unlock();
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
                boundingBox: frameBox,
                minNodeSpacing: (box ? 24 : 18) * spacing,
                concentric: n => n.data('root') ? 1e6 : (n.data('degree') || 0),
                levelWidth: nodes => Math.max(1, nodes.maxDegree() / 4),
            }).run();
            return;
        case 'tree': {
            // breadthfirst spreads each level across its box and the levels down it, so the
            // box decides the drawing. It is sized from the tree — how many levels, how many
            // records in the widest — and from what a record needs beside and after it, which
            // depends on which way the tree runs: a name is wide and one line tall.
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
            const widest = Math.max(1, ...levels.values());
            const sideways = direction === 'LR' || direction === 'RL';
            const boxW = box ? Math.max(...leaves.map(n => n.data('boxW') || 120)) : 0;
            const boxH = box ? Math.max(...leaves.map(n => n.data('boxH') || 36)) : 0;
            // A sideways tree writes its names beside its dots (labelSide), so a record needs
            // a line's height beside it and a name's width after it.
            const beside = (box ? (sideways ? boxH + 20 : boxW + 28) : (sideways ? 30 : 112)) * (1 + (spacing - 1) * 0.5);
            const after = (box ? (sideways ? boxW + 72 : boxH + 64) : (sideways ? 190 : 92)) * spacing;
            const across = widest * beside;
            // The levels are spread as far as the frame's shape allows, so the drawing fills it
            // rather than sitting in a band across its middle.
            const shape = sideways ? 1 / aspect : aspect;
            const down = Math.max(Math.max(1, levels.size) * after, Math.min(across / shape, Math.max(1, levels.size) * after * 2.2));
            run({ x1: 0, y1: 0, w: across, h: down });
            orient(visible, direction);
            return;
        }
        case 'dagre':
            await plugin('dagre');
            await settled(visible.layout({
                name: 'dagre',
                fit: false,
                animate: false,
                rankDir: direction,
                nodeSep: (box ? 28 : 36) * spacing,
                rankSep: (box ? 56 : 72) * spacing,
                edgeSep: 12 * spacing,
                ranker: 'network-simplex',
                nodeDimensionsIncludeLabels: !box,
                // Each link's route through the ranks, kept for the stylesheet to draw.
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
            // Groups drawn around their records (data-parent) are fcose's to lay out: cose
            // leaves a record outside a group lying across the group's edge.
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
                await settled(springs(visible, count, spacing));
            }
            stretch(visible, aspect);
            return;
    }
}

/* fcose: springs that understand groups inside groups. Seeded, so the same graph comes out
   the same way on every load, and measuring names where there are groups — a group is drawn
   around its records' names, and two groups measured without them come out over each other. */
async function nestedSprings(visible, count, spacing, box) {
    await plugin('fcose');
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
            nodeDimensionsIncludeLabels: box || visible.nodes(':parent').nonempty(),
        }));
    } finally {
        restoreRandom();
    }
}

/* cose settles into something round whatever the frame is, and frames are wide.
   Labels run horizontally, so the width that would go unused is the room they need. */
function stretch(visible, aspect) {
    const box = visible.nodes().boundingBox({ includeLabels: false });
    const factor = Math.min(Math.max(aspect / (box.w / Math.max(box.h, 1)), 1), 1.8);
    if (factor > 1.05 && box.w > 0) {
        const centre = box.x1 + box.w / 2;
        visible.cy().batch(() => visible.nodes().filter(n => !n.isParent())
            .forEach(n => n.position('x', centre + (n.position('x') - centre) * factor)));
    }
}

// breadthfirst lays out top-down; the other three directions are that, turned.
function orient(visible, direction) {
    if (direction === 'TB') return;
    visible.cy().batch(() => visible.nodes().filter(n => !n.isParent()).forEach(n => {
        const { x, y } = n.position();
        if (direction === 'BT') n.position({ x, y: -y });
        else if (direction === 'LR') n.position({ x: y, y: x });
        else n.position({ x: -y, y: x });
    }));
}

/* How wide the frame is for its height, from the element rather than from cytoscape,
   which has not measured it yet when the first drawing is made. */
const aspectOf = frame => (frame && frame.clientWidth > 0 && frame.clientHeight > 0 ? frame.clientWidth / frame.clientHeight : 1.8);

/* fcose randomises its start. A fixed seed makes its drawing the same on every load;
   the page's own Math.random is put back the moment the layout has run. */
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

/* ── 31-islands.js ──────────────────────────────────────────────── */
/* ── Islands ──────────────────────────────────────────────────────────────────
   A large graph is read by its groups — a project and what is filed under it, a team
   and its services, a rack and its hosts — so it is laid out as islands: each
   `data-group` with its members, drawn by its own springs, and the islands packed in
   rows as wide as the frame, a `data-cluster`'s islands side by side.

   Springs alone never find that shape. A record works across groups, a link joins two
   of them, and cose pulls with the square of the distance — so the few long bridges
   pull harder than everything inside a group and the clusters melt into one ball.

   The record whose id IS a group's name is that group's heart: it is placed first, at
   the centre of its island. What joins several islands — a person in three teams, a
   shared library — is put between them, where its neighbours are.
   ─────────────────────────────────────────────────────────────────────────── */

async function islands(cy, visible, aspect, spacing, box) {
    const nodes = visible.nodes().filter(n => !n.isParent());
    const gap = 36 * spacing;

    // Which island a record is on: its group's — or, for one in no group, the one island
    // all its neighbours are on, asked twice so a record reached only through another
    // ungrouped record finds it too.
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

    // Each island on its own: seeded on a sunflower spiral, its heart at the centre,
    // then its own springs with the bridges left out. One run an island rather than one
    // over everything: cose weighs every record against every other in its run, so
    // twenty islands of thirty cost a twentieth of one run over six hundred.
    const list = [];
    const inside = cy.collection();
    for (const [id, group] of members) {
        group.sort((a, b) => (a.id() === id ? -1 : b.id() === id ? 1 : (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b)));
        group.forEach((n, i) => {
            const r = gap * Math.sqrt(i);
            n.position({ x: r * Math.cos(i * golden), y: r * Math.sin(i * golden) });
        });
        const eles = cy.collection(group);
        const own = eles.edgesWith(eles).not('.hidden');
        inside.merge(own);
        if (group.length > 2 && own.nonempty()) await settled(springs(eles.union(own), group.length, spacing, { numIter: 400 }));
        const heart = cy.getElementById(id);
        const cluster = group[0].data('cluster') ?? (heart.nonempty() ? heart.data('cluster') : null) ?? '';
        list.push({ id, eles, cluster });
    }

    // A cluster no group reaches — records linked only among themselves — is an island of
    // its own, rather than waiting for neighbours that will never be placed.
    const orphans = new Set();
    for (const part of visible.components()) {
        const parted = part.nodes().filter(n => !n.isParent());
        if (parted.length > 1 && parted.every(n => !home.has(n.id()))) {
            const group = parted.toArray().sort((a, b) => (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b));
            group.forEach((n, i) => {
                const r = gap * Math.sqrt(i);
                n.position({ x: r * Math.cos(i * golden), y: r * Math.sin(i * golden) });
                orphans.add(n.id());
            });
            const eles = cy.collection(group);
            const own = eles.edgesWith(eles).not('.hidden');
            inside.merge(own);
            if (group.length > 2) await settled(springs(eles.union(own), group.length, spacing, { numIter: 400 }));
            list.push({ id: group[0].id(), eles, cluster: '' });
        }
    }

    // cose packs whatever is not connected on its own terms, so a record with no link
    // inside its island would be sent away from it. It is put back, round the shore.
    list.forEach(i => {
        const tied = i.eles.filter(n => n.connectedEdges().intersection(inside).nonempty());
        const loose = i.eles.not(tied);
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
        i.box = i.eles.boundingBox({ includeLabels: false });
    });

    // Packed in rows: a cluster's islands together, the biggest cluster first, rows as
    // wide as the frame is for its height — tried, not guessed, since islands differ.
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
    const widest = Math.max(...list.map(i => i.box.w), 1);
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

    // The bridges, between the islands they join: at their neighbours' middle, pushed
    // out of any island that middle falls inside, to its shore. A pass at a time, so a
    // cluster's heart finds its islands and then its people find it.
    const shores = list.map(i => {
        const b = i.eles.boundingBox({ includeLabels: false });
        return { x: b.x1 + b.w / 2, y: b.y1 + b.h / 2, r: Math.max(b.w, b.h) / 2 + gap / 2 };
    });
    const placed = new Set([...home.keys(), ...orphans]);
    // A bridge is kept a record's breadth from everything already placed — an island's
    // members and the bridges before it — or two records land on top of each other.
    // A grid of what is placed, so the check stays local in a large graph.
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

    // What touches nothing on the map is one more island, last.
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

/* ── 40-view.js ──────────────────────────────────────────────── */
/* ── The view: fitting, zoom, and where names go ──────────────────────────────
   Fitting a drawing never blows it up: three records filling a whole frame are a worse
   drawing than three records at their size.

   A name is read on the screen, so in a map of dots it is sized on the screen: the same
   few pixels at every zoom, a hub's a little larger. Zooming in is asking for room, not
   for bigger letters, and zooming out is asking for the shape — so how many names are
   shown is decided by the room there is for them. They are placed the way a map places
   them: the most important first, and one that would run into a name already placed is
   left out rather than drawn over it.

   `data-graph-labels`: auto (that), all (every name, always), none (a name only on the
   record being pointed at, selected, matched or focused).
   ─────────────────────────────────────────────────────────────────────────── */

function fitView(g, eles, animate) {
    const cy = g.cy;
    const nodes = (eles || cy.elements().not('.hidden')).nodes();
    if (nodes.length === 0) return;
    cy.resize();
    const { zoom, pan } = fitOf(g, nodes);
    cy.stop();
    // Not waited for: an animation runs on animation frames, which a tab that is not
    // painted never gets. The names are placed again when the zoom comes to rest.
    if (animate && !reducedMotion() && document.visibilityState === 'visible') {
        cy.animate({ zoom, pan }, { duration: 260, easing: 'ease-out-cubic', complete: () => settle(g) });
    } else {
        cy.viewport({ zoom, pan });
        settle(g);
    }
}

/* The zoom and pan that show these records whole, with room for their names. */
function fitOf(g, nodes) {
    const cy = g.cy;
    const box = nodes.boundingBox({ includeLabels: g.options.nodes === 'box', includeOverlays: false });
    // Room for the names, which are held at a screen size and so are not in the drawing's
    // box: beneath a record, half a name either side and a line below it; beside one, a
    // name's width on that side — never more than a fraction of a narrow frame, where the
    // drawing would otherwise shrink to a speck.
    const side = g.options.nodes === 'box' ? null : labelSide(g.options);
    const sideRoom = side && side !== 'bottom' ? Math.min(LABEL_MAX_PX * 0.8 + 16, cy.width() * 0.3) : 0;
    const labelRoom = g.options.nodes === 'box' ? Math.min(48, cy.width() * 0.2)
        : sideRoom || Math.min(LABEL_MAX_PX + 32, cy.width() * 0.2);
    const room = {
        w: Math.max(cy.width() - labelRoom, cy.width() / 2),
        h: Math.max(cy.height() - Math.min(g.options.nodes === 'box' ? 48 : sideRoom ? 32 : 88, cy.height() * 0.2), cy.height() / 2),
    };
    const ceiling = g.options.nodes === 'box' ? 1.1 : 1.25;
    const zoom = Math.max(cy.minZoom(), Math.min(room.w / Math.max(box.w, 1), room.h / Math.max(box.h, 1), ceiling));
    // Names on one side: the drawing moves over by half their room, so they have all of it.
    const shift = side === 'right' ? -sideRoom / 2 : side === 'left' ? sideRoom / 2 : 0;
    const pan = { x: cy.width() / 2 + shift - zoom * (box.x1 + box.w / 2), y: cy.height() / 2 - zoom * (box.y1 + box.h / 2) };
    return { zoom, pan };
}

/* The whole drawing, from a button or a call. While the records are still travelling to
   a new layout, the fit waits for them to land: fitting now would fit where they were. */
function fitAll(g) {
    g.touched = false;
    if (g.travelling) g.refit = true;
    else fitView(g, null, true);
}

const stepOf = zoom => Math.exp(Math.round(Math.log(zoom) / 0.07) * 0.07);

/* After anything that moves records or the view: the zoom handed to the elements as
   data, then the names placed for it. An edge takes it only where it changes something
   — above 1, where widths are held, and on a lit edge, whose label is showing — because
   restyling a thousand edges for nothing is the most expensive thing this could do. */
function settle(g) {
    const cy = g.cy;
    if (g.options.nodes !== 'box') {
        const next = stepOf(cy.zoom());
        if (next !== g.step) {
            // Link names that are always written are held at their size like record names;
            // otherwise only a lit link's name is showing.
            const edges = Math.max(next, g.step) > 1 || g.options.edgeLabels === 'always' ? cy.edges() : cy.edges('.lit');
            g.step = next;
            cy.batch(() => cy.nodes().union(edges).data('zoom', g.step));
        }
    }
    declutter(g);
    g.minimap?.draw();
}

const insists = n => n.data('root') || n.hasClass('focus') || n.hasClass('match') || n.hasClass('keyed')
    || n.hasClass('lit') || n.selected();

/* Which name wins where two would collide: the one somebody is looking at, then the
   groups' own records, then the busiest. Quieter records give way to everything else. */
const labelPriority = n => (insists(n) ? 1e6 : 0)
    + (n.data('hub') ? 2000 : 0)
    + (n.isParent() ? 1500 : 0)
    + (n.data('degree') || 0)
    - (n.data('muted') ? 100 : 0);

function declutter(g) {
    const cy = g.cy;
    const nodes = cy.nodes().not('.hidden').filter(n => (n.data('display') || g.options.nodes) !== 'box' && !n.isParent());
    if (nodes.empty()) return;
    if (g.options.labels === 'all') {
        cy.batch(() => nodes.removeClass('unlabelled'));
        return;
    }
    if (g.options.labels === 'none') {
        cy.batch(() => {
            nodes.filter(insists).removeClass('unlabelled');
            nodes.filter(n => !insists(n)).addClass('unlabelled');
        });
        return;
    }
    const cell = 80;
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
    // Measured from the model, not asked of the renderer: this runs over every record
    // whenever the view settles, and a label's box follows from its length and size.
    const zoom = cy.zoom();
    const pan = cy.pan();
    const side = labelSide(g.options);
    const show = [];
    const hide = [];
    const ranked = nodes.toArray().map(n => ({ n, rank: labelPriority(n) })).sort((a, b) => b.rank - a.rank);
    for (const { n, rank } of ranked) {
        const at = n.position();
        const x = at.x * zoom + pan.x;
        const y = at.y * zoom + pan.y;
        const r = ((n.data('size') || 20) * held(n) * zoom) / 2;
        const px = labelPx(n);
        const w = Math.min((n.data('label') || '').length * px * 0.6, LABEL_MAX_PX) + 10;
        const box = side === 'right' ? { x1: x + r + 3, x2: x + r + 3 + w, y1: y - px * 0.85, y2: y + px * 0.85 }
            : side === 'left' ? { x1: x - r - 3 - w, x2: x - r - 3, y1: y - px * 0.85, y2: y + px * 0.85 }
                : { x1: x - w / 2, x2: x + w / 2, y1: y + r + 2, y2: y + r + 2 + px * 1.7 };
        // A name written across a record hides it — but only a record that outranks it
        // is kept clear: in a crowd a hub's name over a leaf is the map, and a leaf's
        // name over a hub is the hairball.
        place({ x1: x - r, x2: x + r, y1: y - r, y2: y + r });
        const labelled = rank >= 1e6 || !collides(box);
        if (labelled) place(box);
        // Only what changes is touched: a class set on a node is a restyle of it.
        if (labelled === n.hasClass('unlabelled')) (labelled ? show : hide).push(n);
    }
    if (show.length || hide.length) {
        cy.batch(() => {
            cy.collection(show).removeClass('unlabelled');
            cy.collection(hide).addClass('unlabelled');
        });
    }
}

/* ── 41-light.js ──────────────────────────────────────────────── */
/* ── Lighting a neighbourhood ─────────────────────────────────────────────────
   Pointing at a record, selecting it or reaching it by keyboard lights it and what it
   touches, and dims the rest. A class set on an element is a restyle of it — over a
   large graph, tens of milliseconds a time — so only the difference is restyled: from
   one neighbourhood to the next, what leaves it and what joins it. Everything is
   touched only when the lighting starts or ends.

   `data-graph-hover="none"` keeps the drawing still under the pointer; selection and
   the keyboard still light.
   ─────────────────────────────────────────────────────────────────────────── */

function light(g, node) {
    const cy = g.cy;
    const near = node && node.nonempty() ? node.closedNeighborhood().not('.hidden') : cy.collection();
    const lit = g.lit || cy.collection();
    cy.batch(() => {
        if (near.empty()) {
            cy.elements('.dim').removeClass('dim');
            lit.removeClass('lit');
        } else if (lit.empty()) {
            cy.elements().not(near).not(near.ancestors()).addClass('dim');
            near.addClass('lit');
        } else {
            lit.not(near).removeClass('lit').addClass('dim');
            near.not(lit).removeClass('dim').addClass('lit');
            near.ancestors().removeClass('dim');
        }
        // A lit edge shows its label, which is sized on the screen: it takes the zoom
        // the nodes already have.
        if (g.options.nodes !== 'box') near.edges().data('zoom', g.step);
    });
    g.lit = near;
    // The outlines around groups step back with everything else that is not lit.
    const hullLayer = g.bb && g.bb.layer && g.bb.layer.node;
    if (hullLayer) {
        hullLayer.style.transition = reducedMotion() ? '' : 'opacity 160ms';
        hullLayer.style.opacity = near.empty() ? '' : '0.35';
    }
}

/* What should be lit when nothing is being pointed at: the keyboard's record, else the
   selection, else nothing. */
const resting = g => (g.keyed && g.keyed.nonempty() ? g.keyed : g.selected && g.selected.nonempty() ? g.selected : null);

/* ── 42-filter.js ──────────────────────────────────────────────── */
/* ── Filters, search and focus ────────────────────────────────────────────────
   Filters narrow a drawing that is already on screen; they never ask for new data.

     {
       nodes:       { kind: ['service', 'db'] },   only records with one of these values
       except:      { group: ['payments'] },       not records with one of these values
       edges:       { kind: ['calls'] },            only links …
       edgesExcept: { kind: ['mentions'] },         not links …
       hide:        ['legacy-api'],                 ids, records or links
       muted:       true,     show records marked data-muted (default: shown)
       isolated:    true,     show records with no visible link (default: shown)
       focus:       'orders-api', depth: 2          one record's neighbourhood
     }

   The two are different questions. `nodes` is a choice — "this customer" — so a record
   without the field is not what was chosen, and goes. `except` is a switch — a legend
   chip turned off — so it hides what it names and nothing else: a record in no group
   stays when a group's chip goes off. That is why chips (checkboxes, toggle buttons)
   fill `except` and a select or radios fill `nodes`.

   A field names a property (kind, group, cluster, tone, tags, …) or any other data-*
   attribute; a record matches when one of its values is listed, so `tags` matches on
   any tag. Order matters: records by their fields, then links by theirs and by whether
   both ends survived, then — if asked — the records nothing visible touches, then the
   focus.

   Search marks rather than hides: where a record sits among the others is the thing a
   graph is for. Every word typed has to appear in the record's name, id, `data-meta`,
   kind or tags.
   ─────────────────────────────────────────────────────────────────────────── */

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

// Allowed by every "only these", and named by no "not these".
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
    // The focus is what the reader is looking at, so a filter never takes it away. A root
    // is kept only from being hidden as unlinked — a chip that is off hides it like any record.
    const pinned = n => n.id() === f.focus;
    cy.batch(() => {
        cy.elements().removeClass('hidden focus');
        cy.nodes().filter(n => !pinned(n) && (
            f.hide.has(n.id())
            || !passes(n, f.nodes, f.except)
            || (!f.muted && n.data('muted')))).addClass('hidden');
        // A group hidden takes its members with it; a member that survived keeps its group.
        cy.nodes('.hidden').descendants().addClass('hidden');
        cy.nodes().not('.hidden').ancestors().removeClass('hidden');
        cy.edges().filter(e =>
            f.hide.has(e.id())
            || !passes(e, f.edges, f.edgesExcept)
            || e.source().hasClass('hidden') || e.target().hasClass('hidden')).addClass('hidden');
        if (!f.isolated) {
            cy.nodes().not('.hidden').filter(n => !n.isParent() && !pinned(n) && !n.data('root')
                && n.connectedEdges().not('.hidden').empty()).addClass('hidden');
            cy.nodes(':parent').not('.hidden').filter(p => p.descendants().not('.hidden').empty()).addClass('hidden');
        }
        const focus = f.focus ? cy.getElementById(f.focus) : null;
        if (focus && focus.nonempty() && !focus.hasClass('hidden')) {
            focus.addClass('focus');
            let reach = focus;
            for (let i = 0; i < f.depth; i++) reach = reach.union(reach.neighborhood().not('.hidden'));
            const keep = reach.union(reach.nodes().edgesWith(reach.nodes()).not('.hidden')).union(reach.ancestors());
            cy.elements().not(keep).addClass('hidden');
        }
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
    cy.batch(() => {
        cy.nodes('.match').removeClass('match');
        if (words.length) {
            cy.nodes().not('.hidden').filter(n => {
                const hay = searchText(n);
                return words.every(w => hay.includes(w));
            }).addClass('match');
        }
    });
    const matches = cy.nodes('.match');
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

/* ── 50-minimap.js ──────────────────────────────────────────────── */
/* ── The minimap ──────────────────────────────────────────────────────────────
   The whole drawing in miniature, with the part on screen framed — for a graph larger
   than its frame, where zooming in loses the reader's sense of where they are.

     <canvas class="graph-minimap" data-graph-minimap aria-hidden="true"></canvas>

   The app writes the canvas, inside the graph, and the stylesheet places it
   (`.graph-minimap--start`, `--top`); this draws into it. Pressing on it moves the view
   there, dragging pans, and the wheel zooms about the point under the pointer. It is a
   pointer convenience: the keyboard already has the arrows, + and −, and 0 to fit, so
   it is hidden from the accessibility tree.

   The records are painted once into a buffer whenever they move or change; a view that
   pans only redraws the frame over it. Colours are the canvas's own, so the minimap is
   the drawing, smaller — never a second palette.
   ─────────────────────────────────────────────────────────────────────────── */

function minimap(g, canvas) {
    const cy = g.cy;
    const buffer = document.createElement('canvas');
    let scale = 1, ox = 0, oy = 0, dirty = true, timer = 0, dragging = false;

    function measure() {
        const dpr = Math.min(window.devicePixelRatio || 1, 3);
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
        const bb = nodes.boundingBox({ includeLabels: false, includeOverlays: false });
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
        cy.nodes(':parent').not('.hidden').forEach(parent => {
            const b = parent.boundingBox({ includeLabels: false });
            ctx.fillStyle = withAlpha(parent.style('border-color'), 0.12);
            ctx.fillRect(ox + b.x1 * scale, oy + b.y1 * scale, b.w * scale, b.h * scale);
        });
        nodes.forEach(n => {
            const pos = n.position();
            const r = Math.max(1.5 * dpr, Math.min(n.width() * scale / 2, 6 * dpr));
            ctx.globalAlpha = n.hasClass('dim') ? 0.25 : n.data('muted') ? 0.5 : 1;
            ctx.fillStyle = (n.data('display') || g.options.nodes) === 'box' ? n.style('border-color') : n.style('background-color');
            ctx.beginPath();
            ctx.arc(ox + pos.x * scale, oy + pos.y * scale, r, 0, Math.PI * 2);
            ctx.fill();
        });
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
        // What is off screen is veiled; the part in view is left clear and framed. When
        // everything is in view there is nothing to veil and the frame sits on the edge.
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

    // A timer, not an animation frame: a frame never comes for a tab that is not painted.
    const soon = (records) => {
        if (records) dirty = true;
        if (!timer) timer = setTimeout(draw, 32);
    };

    // From the pointer to the drawing, through the canvas's content box — its border is not
    // part of what was drawn, and a pixel there is a pixel of error, magnified by the zoom.
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
    size.observe(canvas);

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

/* ── 51-tip.js ──────────────────────────────────────────────── */
/* ── Tooltips ─────────────────────────────────────────────────────────────────
   Pointing at a record, or reaching it by keyboard, says what it is.

   With nothing else written, that is the library's own hover-hint bubble — the same
   `.sedna-tip` every `data-tip` on the page uses — holding the record's name and its
   `data-meta`. A link says its label.

   A richer tooltip is the app's markup, written once inside the graph and filled per
   record:

     <div class="graph-tip" data-graph-tip hidden>
       <span class="graph-tip-title" data-graph-field="label"></span>
       <span class="graph-tip-meta" data-graph-field="meta"></span>
       <span class="graph-tip-meta" data-graph-field="owner"></span>
     </div>

   Every `[data-graph-field]` gets the record's value of that field as text — never as
   markup — and a field the record does not have is hidden, so one tooltip serves
   records with different fields. `[data-graph-icon]` takes the record's icon class.
   `data-graph-tip="edge"` is the one for links. The script only fills and places it; the
   words, the order and the look are the app's.
   ─────────────────────────────────────────────────────────────────────────── */

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

    // Where an element is on the viewport, from where it is on the canvas.
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
            // Above the record: its name is written beneath it, and a bubble below would cover it.
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

/* ── 52-keyboard.js ──────────────────────────────────────────────── */
/* ── The keyboard, and what a screen reader hears ─────────────────────────────
   A canvas is a picture to assistive technology. The graph is made a single stop in
   the tab order, and inside it the keys move a ring from record to record:

     ← ↑ → ↓            the nearest record in that direction
     Page Down / Up     the next / previous record linked to this one — walking the links
     Home               the root, else the most connected record
     Space              select, or clear the selection
     Enter              open it — `sedna-graph-open`, then its data-href
     Shift + arrows     move the view
     + / −   0          zoom in / out, fit everything
     Escape             clear the selection, then the ring

   Every move is said through the app's live region, in the app's words:

     <p class="visually-hidden" data-graph-live aria-live="polite"
        data-graph-announce="{label}, {kind}. {links} links."
        data-graph-announce-select="{label} selected."></p>

   `{name}` is any field of the record — label, kind, meta, group, or a data-* of its
   own — plus `{links}`, how many links on screen touch it. With no live region the ring
   still moves, and the tooltip still says the record's name.

   The graph's element gets `tabindex="0"` and `role="application"` unless the app wrote
   its own. Its accessible name — and `aria-roledescription`, if the app wants the word
   "graph" said — are the app's to write, like every other word on the page.
   ─────────────────────────────────────────────────────────────────────────── */

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

    const live = () => {
        const inside = el.querySelector('[data-graph-live]');
        if (inside) return inside;
        const frame = el.closest('[data-graph-frame]');
        return frame ? frame.querySelector('[data-graph-live]') : (el.id ? document.querySelector(`[data-graph-live][data-graph-for="${CSS.escape(el.id)}"]`) : null);
    };

    function announce(ele, which) {
        const region = live();
        if (!region || !ele) return;
        const template = region.getAttribute(which === 'select' ? 'data-graph-announce-select' : 'data-graph-announce')
            || (which === 'select' ? '' : '{label}');
        if (!template) return;
        // Cleared first, so saying the same record twice is still a change.
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
        // Keep the ring on screen: a record at the edge is brought in, not the whole view moved.
        const ext = cy.extent();
        const p = g.keyed.position();
        const mx = ext.w * 0.12, my = ext.h * 0.12;
        if (p.x < ext.x1 + mx || p.x > ext.x2 - mx || p.y < ext.y1 + my || p.y > ext.y2 - my) {
            cy.stop();
            if (reducedMotion() || document.visibilityState !== 'visible') cy.center(g.keyed);
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
                if (g.keyed) open(g, g.keyed, true);
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
        // Only for the keyboard: a click that focuses the graph has already chosen a record.
        const pointer = Date.now() - (g.pointerAt || 0) < 800;
        if (!g.keyed && !pointer && el.matches(':focus-visible')) key(start());
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
    if (reducedMotion() || document.visibilityState !== 'visible') {
        cy.zoom({ level, renderedPosition: centre });
        settle(g);
    } else {
        cy.animate({ zoom: { level, renderedPosition: centre } }, { duration: 160, complete: () => settle(g) });
    }
}

/* ── 53-menu.js ──────────────────────────────────────────────── */
/* ── The context menu ─────────────────────────────────────────────────────────
   A right click, a long press, or the context-menu key on the keyboard's record
   dispatches `sedna-graph-context` { id, x, y } on the graph — cancelable, for an app
   that opens something of its own. If it is not cancelled and the app wrote a menu
   inside the graph, that menu opens where the pointer is:

     <div class="menu graph-menu" data-graph-menu role="menu" hidden>
       <span class="menu-label" data-graph-field="label"></span>
       <button class="menu-item" role="menuitem" type="button" data-graph-action="open">
         <i class="ri-external-link-line"></i> Open</button>
       <button class="menu-item" role="menuitem" type="button" data-graph-action="focus">
         <i class="ri-focus-3-line"></i> Show its neighbourhood</button>
     </div>

   It is the library's `.menu`, with the app's items and words. An item with a
   `data-graph-action` acts on the record the menu was opened on; any other item is the
   app's own, and the event told it which record that was. Escape, a click elsewhere,
   a scroll or picking an item closes it, and focus goes back to the graph.
   ─────────────────────────────────────────────────────────────────────────── */

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
        // An item that needs a record, or a group, hides itself where there is none.
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

    // A separator only between two things that are showing: none first, none last, never two
    // in a row — the items around one hide by record, so which of them is left changes.
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
    // Opened by the pointer, the menu has no focus, so Escape is heard on the document.
    const onEscape = e => {
        if (menu.hidden || e.key !== 'Escape' || menu.contains(e.target)) return;
        close(g.el.contains(document.activeElement) || document.activeElement === document.body);
        e.preventDefault();
    };
    // A scroll or a move of the view takes the record from under the menu.
    const onScroll = e => { if (!menu.hidden && !menu.contains(e.target)) close(false); };
    const onViewport = () => { if (!menu.hidden && !picking) close(false); };
    // After the item's own action has read the menu's record, not before.
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

/* `sedna-graph-context` first; the app's menu only if nobody cancelled it. `at` is a
   point in the graph's own pixels; from the keyboard it is the record's centre. */
function context(g, ele, at) {
    const point = at || (ele ? ele.renderedPosition() : { x: g.cy.width() / 2, y: g.cy.height() / 2 });
    const detail = Object.assign(ele ? nodeDetail(ele) : { id: null }, { x: Math.round(point.x), y: Math.round(point.y) });
    const go = g.emit('sedna-graph-context', detail, true);
    if (go && g.menu.present) g.menu.open(ele, point, !at);
}

/* ── 54-detail.js ──────────────────────────────────────────────── */
/* ── The selected record, beside the graph ────────────────────────────────────
   What the canvas cannot be: text. A panel the app wrote — usually a `.card` in a
   `.sedna-split-aside` — shows the selected record and every record it touches, as
   controls a keyboard and a screen reader reach:

     <aside class="card" data-graph-detail data-graph-for="deps" hidden>
       <div class="card-head"><strong data-graph-field="label"></strong></div>
       <div class="card-body"><span data-graph-field="meta"></span></div>
       <ul class="list" data-graph-neighbours>
         <template data-graph-neighbour>
           <li><button class="list-row" type="button" data-graph-action="select">
             <span class="list-title" data-graph-field="label"></span>
             <span class="list-sub" data-graph-link></span>
           </button></li>
         </template>
       </ul>
     </aside>

   The panel is shown while a record is selected and hidden while none is. Its
   `[data-graph-field]` slots take the record's fields; each row cloned from
   `<template data-graph-neighbour>` takes a neighbour's, `[data-graph-link]` takes the
   link's label, and the row carries `data-graph-direction` — `out`, `in` or `both` —
   for the stylesheet or the app to draw an arrow from. A `data-graph-action` in a row
   acts on that neighbour.

   A Blazor app renders its own panel from `sedna-graph-select` instead, and leaves
   `data-graph-detail` off: this is for a page with no app behind it.
   ─────────────────────────────────────────────────────────────────────────── */

function detailPanels(g) {
    const id = g.el.id;
    const frame = g.el.closest('[data-graph-frame]');
    const panels = new Set();
    g.el.querySelectorAll('[data-graph-detail]').forEach(p => panels.add(p));
    frame?.querySelectorAll('[data-graph-detail]').forEach(p => {
        if (!p.getAttribute('data-graph-for') || p.getAttribute('data-graph-for') === id) panels.add(p);
    });
    if (id) document.querySelectorAll(`[data-graph-detail][data-graph-for="${CSS.escape(id)}"]`).forEach(p => panels.add(p));
    return [...panels];
}

/* A record's fields into the app's slots — the tooltip's, the menu's and the panel's.
   Text only, never markup. A field the record does not have hides its slot, and what
   belongs to the slot with it: the `<dt>` before a `<dd>` slot, and any element with
   `data-graph-if="field"`, which is how a label written beside a value leaves with it. */
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

// The first element of a template, cloned — from .content when the parser filled it, or
// from the element itself when Blazor did (its renderer appends into the template).
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

/* ── 55-controls.js ──────────────────────────────────────────────── */
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

function controlsOf(g) {
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
    (frame || g.el).querySelectorAll(CONTROL_SELECTOR).forEach(c => { if (mine(c)) found.add(c); });
    if (g.el.id) {
        document.querySelectorAll(`[data-graph-for="${CSS.escape(g.el.id)}"]`).forEach(scope => {
            if (scope.matches(CONTROL_SELECTOR)) found.add(scope);
            scope.querySelectorAll(CONTROL_SELECTOR).forEach(c => found.add(c));
        });
    }
    return [...found].filter(c => !c.closest('template') && !c.closest('[data-graph-row]'));
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
    const out = [];
    const frame = g.el.closest('[data-graph-frame]');
    (frame || g.el).querySelectorAll('[data-graph-stats]').forEach(e => out.push(e));
    if (g.el.id) document.querySelectorAll(`[data-graph-stats][data-graph-for="${CSS.escape(g.el.id)}"]`).forEach(e => out.push(e));
    for (const el of new Set(out)) {
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

/* ── 60-export.js ──────────────────────────────────────────────── */
/* ── Export ───────────────────────────────────────────────────────────────────
   The whole drawing — every record on screen, not only the part in view — as a picture:

     data-graph-action="export-png"   cytoscape's own renderer, at twice the resolution
     data-graph-action="export-svg"   written here, from the drawing's geometry

   The SVG is Sedna.UI's own rather than the cytoscape-svg plugin, which is GPL-3.0 and
   cannot ship in an Apache-2.0 package. It is also the better file: each record is a
   group with a <title>, the text is text, and every colour is the colour the canvas
   resolved from the tokens at the moment of export — so a dark-theme export is dark.

   `data-graph-filename` on the button names the file; otherwise it is the graph's id.
   From script, graph.export('svg' | 'png') resolves a Blob.
   ─────────────────────────────────────────────────────────────────────────── */

const SVG_NS = 'http://www.w3.org/2000/svg';

async function exportImage(g, format) {
    const ground = palette(g.colours).ground;
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

const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const num = n => (Math.round(n * 100) / 100).toString();

// A regular polygon with `sides`, point up, fitted to the square [-1, 1].
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
    // rectangle, round-rectangle, cut-rectangle, barrel, bottom-round-rectangle and the rest.
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

    // The outlines around groups, underneath everything: their paths are already in the
    // drawing's own coordinates.
    if (g.hullsOn && g.bb) {
        for (const path of g.bb.getPaths()) {
            const d = path.node.getAttribute('d');
            if (!d) continue;
            const st = path.node.style;
            out.push(`<path class="graph-outline" d="${esc(d)}" fill="${st.fill || 'none'}" stroke="${st.stroke || 'none'}" stroke-width="${st.strokeWidth || 1}"/>`);
        }
    }

    // Groups underneath, outermost first.
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
        const opacity = e.numericStyle('opacity') * e.numericStyle('line-opacity');
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
        const attrs = `fill="${fill}" fill-opacity="${num(n.numericStyle('background-opacity'))}" stroke="${stroke}" stroke-width="${num(bw)}"`;
        const parts = [`<g class="graph-record" opacity="${num(n.numericStyle('opacity'))}"><title>${esc([n.data('label'), n.data('meta')].filter(Boolean).join(' — '))}</title>`];
        parts.push(shapeSvg(n.style('shape'), p.x, p.y, nw, nh, attrs));
        const icon = n.data('icon') ? g.icons.image(n.data('icon'), box ? stroke : ground, 32) : null;
        if (icon) {
            // A box's icon sits 12px in from its left edge (background-position-x); a dot's is centred.
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
                // Beneath the record, or beside it where the stylesheet put it there.
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

/* ── 61-plugins.js ──────────────────────────────────────────────── */
/* ── Plugins: drawing links, folding groups, outlining groups ─────────────────
   Each is loaded the first time a graph asks for it, from beside the engine.

   Drawing a link — cytoscape-edgehandles. `data-graph-connect` starts a graph in
   drawing mode; `data-graph-action="connect"` on a toggle button switches it. In it,
   dragging from one record to another draws a link, and the graph dispatches
   `sedna-graph-connect` { source, target }. The line drawn is removed again: the app
   adds the link to its own data and the graph shows it, as a drop in a drag-and-drop
   list is the app's to make. `data-graph-managed` keeps it instead, for a page with no
   app behind it.

   Folding groups — cytoscape-expand-collapse. Records with a `data-parent` are drawn
   inside it; `data-graph-collapse` gives every group a fold cue, drawn with the
   library's own icons in the token colours. `data-collapsed` on a group starts it
   folded. Folding and unfolding dispatch `sedna-graph-collapse` / `sedna-graph-expand`.

   Outlining groups — cytoscape-bubblesets. `data-graph-hulls`, or the `hulls` toggle,
   draws a soft outline in its tone around every `data-group` of two or more records,
   routed around the records that are not in it. It follows the records as they move.
   ─────────────────────────────────────────────────────────────────────────── */

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
    g.cy.on('expandcollapse.aftercollapse', 'node', e => {
        if (!g.quietFolds) g.emit('sedna-graph-collapse', { id: e.target.id(), label: e.target.data('label') });
        afterStructure(g);
    });
    g.cy.on('expandcollapse.afterexpand', 'node', e => {
        if (g.quietFolds) {
            afterStructure(g);
            return;
        }
        g.emit('sedna-graph-expand', { id: e.target.id(), label: e.target.data('label') });
        afterStructure(g);
        // Unfolded where it was folded from, over whatever the drawing put there since: the
        // drawing is arranged again around it, once, however many groups opened at once —
        // after the plugin's own unfolding has finished moving them, or it moves them back.
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

// The ids of the groups folded now, and every record hidden inside them.
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
        if (g.bb) g.bb.getPaths().slice().forEach(p => g.bb.removePath(p));
        return;
    }
    await plugin('layers');
    await plugin('bubblesets');
    if (!g.bb) g.bb = g.cy.bubbleSets({ interactive: false, throttle: 60 });
    drawHulls(g);
}

function drawHulls(g) {
    if (!g.bb) return;
    const cy = g.cy;
    g.bb.getPaths().slice().forEach(p => g.bb.removePath(p));
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
        // The group's own tone: its heart's, else what most of its members wear.
        let tone = heart.nonempty() ? nodeTone(heart, g.options) : null;
        if (!tone) {
            const count = new Map();
            members.forEach(m => { const t = nodeTone(m, g.options); count.set(t, (count.get(t) || 0) + 1); });
            tone = [...count.entries()].sort((a, b) => b[1] - a[1])[0][0];
        }
        const colour = g.colours.token(tone, palette(g.colours).line);
        // Routed through lines of its own between members that no link joins, so a group
        // spread across the drawing is still one outline, not none.
        g.bb.addPath(nodes, nodes.edgesWith(nodes).not('.hidden'), all.not(nodes), {
            virtualEdges: true,
            style: { fill: withAlpha(colour, 0.12), stroke: withAlpha(colour, 0.55), strokeWidth: '1.5' },
        });
    }
    // Drawn again once the renderer has caught up with the positions it was just given.
    setTimeout(() => { if (!g.disposed && g.bb) g.bb.update(true); }, 0);
}

/* ── 70-graph.js ──────────────────────────────────────────────── */
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

/* ── 99-exports.js ──────────────────────────────────────────────── */
/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these three.
   A script reaches a graph through `sednaUi.graph.get(el)`, which returns the handle
   `attach` resolves — never through an import of this file, whose URL is the package's
   business.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep, normalise };


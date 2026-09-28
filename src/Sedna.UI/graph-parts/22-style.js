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

/* How wide a line of text is in a font, measured by the browser once and remembered — the
   declutter asks for every name each time the view settles. */
let textCtx = null;
const textWidths = new Map();
function textWidth(text, font) {
    const key = font + '\u0000' + text;
    let w = textWidths.get(key);
    if (w === undefined) {
        textCtx = textCtx || document.createElement('canvas').getContext('2d');
        textCtx.font = font;
        w = textCtx.measureText(text).width;
        if (textWidths.size > 5000) textWidths.clear();
        textWidths.set(key, w);
    }
    return w;
}
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

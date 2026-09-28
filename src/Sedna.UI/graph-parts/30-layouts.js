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

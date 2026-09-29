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
    // The fold cue is drawn on the plugin's own canvas when a group is selected, and that
    // canvas is cleared whenever the engine resizes — which it does on its own when any
    // ancestor's attributes change. So the cue is drawn again once the resize has settled.
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
        // Folded records were out of the drawing while the view options and the zoom
        // changed; they come back drawn as the rest are.
        measureBoxes(g);
        if (g.options.nodes !== 'box') g.cy.batch(() => e.target.union(e.target.descendants()).union(e.target.descendants().connectedEdges()).data('zoom', g.step));
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

// A group that can fold or unfold: one drawn around its records, or one folded away.
const isGroup = n => n.isParent() || n.hasClass('cy-expand-collapse-collapsed-node');

// Folds an open group and unfolds a folded one.
function toggleFold(g, n) {
    if (!g.ec || !isGroup(n)) return false;
    if (g.ec.isExpandable(n)) g.ec.expand(n);
    else if (g.ec.isCollapsible(n)) g.ec.collapse(n);
    else return false;
    return true;
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
        clearHulls(g);
        return;
    }
    await plugin('layers');
    await plugin('bubblesets');
    if (!g.bb) g.bb = g.cy.bubbleSets({ interactive: false, throttle: 60 });
    drawHulls(g);
}

/* Every outline taken away. bubblesets leaves an empty object in each element's scratch as
   it removes a path, and reads that as a cached measurement the next time a path covers
   the element, which throws — so the scratch goes with the paths. Deleted from the scratch
   itself: the engine's removeScratch restyles every element it is called on, and this runs
   whenever the outlines are drawn again. */
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

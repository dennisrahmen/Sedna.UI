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
        if (!g.quietFolds) g.emit('sedna-graph-expand', { id: e.target.id(), label: e.target.data('label') });
        afterStructure(g);
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
        g.bb.addPath(nodes, nodes.edgesWith(nodes).not('.hidden'), all.not(nodes), {
            style: { fill: withAlpha(colour, 0.12), stroke: withAlpha(colour, 0.55), strokeWidth: '1.5' },
        });
    }
    // Drawn again once the renderer has caught up with the positions it was just given.
    setTimeout(() => { if (!g.disposed && g.bb) g.bb.update(true); }, 0);
}

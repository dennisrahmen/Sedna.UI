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
    const box = nodes.boundingBox({ includeLabels: g.options.nodes === 'box', includeOverlays: false });
    // Room at the sides for half a name, which runs out either side of its record, and
    // below for one line of it.
    // Room at the sides for half a name and below for a line of it — never more than a
    // fifth of a narrow frame, where the drawing would otherwise shrink to a speck.
    const labelRoom = Math.min(g.options.nodes === 'box' ? 48 : LABEL_MAX_PX + 32, cy.width() * 0.2);
    const room = {
        w: Math.max(cy.width() - labelRoom, cy.width() / 2),
        h: Math.max(cy.height() - Math.min(g.options.nodes === 'box' ? 48 : 88, cy.height() * 0.2), cy.height() / 2),
    };
    const ceiling = g.options.nodes === 'box' ? 1.1 : 1.25;
    const zoom = Math.max(cy.minZoom(), Math.min(room.w / Math.max(box.w, 1), room.h / Math.max(box.h, 1), ceiling));
    const pan = { x: cy.width() / 2 - zoom * (box.x1 + box.w / 2), y: cy.height() / 2 - zoom * (box.y1 + box.h / 2) };
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
            const edges = Math.max(next, g.step) > 1 ? cy.edges() : cy.edges('.lit');
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
        const box = { x1: x - w / 2, x2: x + w / 2, y1: y + r + 2, y2: y + r + 2 + px * 1.7 };
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

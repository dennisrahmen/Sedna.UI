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

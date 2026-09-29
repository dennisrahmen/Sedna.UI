/* ── Lighting a neighbourhood ─────────────────────────────────────────────────
   Pointing at a record, selecting it or reaching it by keyboard lights it and what it
   touches, and dims the rest.

   The rest is dimmed by a veil, not by restyling it. A class set on an element is a
   restyle of it, and dimming by class restyled every element in the graph each time the
   pointer found a record or left one — seconds at a few thousand records, frame after
   frame while the dimming faded in. The veil is one canvas of the engine's own, laid
   over its drawing and under the layer it drags records on: the colour the graph sits
   on at nine tenths, which is exactly what an element drawn at a tenth of its opacity
   looked like, with the lit neighbourhood drawn again on top of it through the engine's
   own element cache. Lighting costs what the neighbourhood costs, and the fade is one
   CSS transition however large the graph is.

   Only the neighbourhood takes a class — `lit`, which names it and thickens its links —
   and only the difference is restyled: from one neighbourhood to the next, what leaves
   it and what joins it.

   The WebGL renderer draws no element into a canvas of ours, so a graph drawn with it
   dims by class, with what is lit raised above the rest.

   `data-graph-hover="none"` keeps the drawing still under the pointer; selection and
   the keyboard still light.
   ─────────────────────────────────────────────────────────────────────────── */

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
    // What stays bright: the neighbourhood, and the groups it is drawn inside.
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
        // What is lit shows its name, which is sized on the screen: it takes the zoom a
        // name that is showing has — a link's, and a record's the declutter had hidden.
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
    // The outlines around groups step back with everything else that is not lit.
    const hullLayer = g.bb && g.bb.layer && g.bb.layer.node;
    if (hullLayer) {
        hullLayer.style.transition = reducedMotion() ? '' : 'opacity 160ms';
        hullLayer.style.opacity = near.empty() ? '' : '0.35';
    }
}

/* A name under the veil would still show, faintly, where dimming by class hid it. So
   while the veil is up the names that are showing near the view, outside what is lit,
   are hushed — as many as the declutter found room for, never the whole graph — and a
   link's only where every link is named. */
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
            // A dot's name too small to be drawn needs no hushing: far out, that is nearly all of them.
            if (dot && (n.hasClass('unlabelled') && !insists(n) || labelSize(n) * zoom < MIN_NAME_PX)) return;
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

/* The veil: made when the graph is mounted, or null where the engine cannot draw an
   element into it. */
function veilOf(g) {
    if (g.veil !== undefined) return g.veil;
    const cy = g.cy;
    const container = cy.container();
    // Not mounted yet: asked again once it is.
    if (!container) return null;
    let r = null;
    try { r = cy.renderer(); } catch (e) { r = null; }
    const nodeLayer = () => container.querySelector('canvas[data-id$="-node"]');
    if (g.options.renderer === 'webgl' || !r || typeof r.drawCachedElement !== 'function'
        || typeof r.getPixelRatio !== 'function' || !nodeLayer()) {
        // Dimmed by class, then, with what is lit raised above what is not.
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

    // Straight above the engine's drawing and below the layer it drags on — kept there,
    // since the outlines' plugin reorders the engine's canvases when it arrives.
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
    // The engine emits `render` after every frame it draws — a record moved, restyled,
    // or the view changed — so the lit records are drawn again over the same frame.
    cy.on('render', draw);

    // In the order the engine stacks them: groups outermost first, then links, then
    // records, and the one the reader is on last, so its name is on top.
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
            // Still drawn while it fades, so a view that moves meanwhile moves it too.
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

/* What should be lit when nothing is being pointed at: the keyboard's record, else the
   selection, else nothing. */
const resting = g => (g.keyed && g.keyed.nonempty() ? g.keyed : g.selected && g.selected.nonempty() ? g.selected : null);

/* ── A button on a link ───────────────────────────────────────────────────────
   A control at the middle of each link — remove it, put a step into it — for a
   diagram the reader edits: a flow, a pipeline, a state machine. The control is the
   app's markup, written once inside the graph as a template:

     <template data-graph-edge-tool>
       <span class="graph-edge-tool">
         <button class="btn btn--sm btn--icon" type="button" value="remove">
           <i class="ri-close-line" aria-hidden="true"></i>
           <span class="visually-hidden">Remove the link <span data-graph-field="label"></span></span>
         </button>
       </span>
     </template>

   One copy per link on screen, beside the template, with `data-graph-edge-id` naming its
   link and every `[data-graph-field]` slot filled with the link's field as text. Each sits
   on its link's midpoint — the engine's own, read from the drawing, so nothing is
   measured — and follows the view, a record dragged and a layout as they move; one whose
   midpoint is outside the frame is hidden. `data-graph-edge-tool="calls reads"` gives
   the control only to links of those kinds, and a template for each kind can give each
   its own.

   Pressing a button in a copy dispatches `sedna-graph-edge` on the graph — the link, and
   the button's `value` — cancelable. The app changes its own data, as it does for a link
   the reader drew. `data-graph-managed`, for a page with no app behind it, removes the
   link for `value="remove"` unless the event was cancelled.

   For a diagram the reader edits, not a large map: every copy is placed again whenever
   the view moves.
   ─────────────────────────────────────────────────────────────────────────── */

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
    // A link of the app's: on screen, and neither a line being drawn nor a folded group's stand-in.
    const eligible = e => !e.hasClass('hidden') && !e.hasClass('eh-preview') && !e.hasClass('eh-ghost-edge')
        && !e.hasClass('cy-expand-collapse-meta-edge') && !e.source().hasClass('hidden') && !e.target().hasClass('hidden');

    /* The copies the links on screen ask for: made, filled, and taken away again. */
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

    /* Each copy on its link's midpoint, in the frame's own pixels: the canvas fills the
       frame, so the drawing's point times the zoom plus the pan is where it is. */
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

    // Placed once for everything that moved in one turn — a layout moves every record.
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

/* A link as the events carry it: plain data, every field a string or null. */
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

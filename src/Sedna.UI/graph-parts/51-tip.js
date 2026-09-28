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

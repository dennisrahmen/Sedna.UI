/* ── The tooltip ──────────────────────────────────────────────────────────────
   Pointing at a point, or reaching it by keyboard, says what it is.

   With nothing else written, that is the library's own hover-hint bubble — the same
   `.sedna-tip` every `data-tip` on the page uses — holding the category, the series and
   the value as the app wrote them.

   A richer tooltip is the app's markup, written once inside the chart and filled per
   point:

     <div class="chart-tip" data-chart-tip hidden>
       <span class="chart-tip-title" data-chart-field="category"></span>
       <span class="chart-tip-row">
         <span class="chart-tip-swatch"></span>
         <span data-chart-field="series"></span>
         <span class="chart-tip-value" data-chart-field="value"></span>
       </span>
     </div>

   Every `[data-chart-field]` gets that field of the point as text — never as markup —
   and one that is empty is hidden, with any element marked `data-chart-if="name"`.
   `data-chart-tone` on the tip is the point's tone, which colours `.chart-tip-swatch`.

   A `<template data-chart-tip-row>` inside the tip makes it say the whole category:
   the row is cloned once per series shown, in the place the template sits, and filled
   with that series' point; the one the reader is on carries `data-chart-current`.
   ─────────────────────────────────────────────────────────────────────────── */

/* Fills the slots in `root`, itself included; `outside` leaves the cloned rows alone. */
function fillSlots(root, f, outside) {
    const all = (sel) => (root.matches(sel) ? [root] : []).concat(Array.from(root.querySelectorAll(sel)));
    for (const slot of all('[data-chart-field]')) {
        if (outside && slot.closest('[data-chart-row]')) continue;
        const value = f[slot.getAttribute('data-chart-field')] ?? '';
        slot.textContent = value;
        slot.hidden = value === '';
    }
    for (const dep of all('[data-chart-if]')) {
        if (outside && dep.closest('[data-chart-row]')) continue;
        dep.hidden = !(f[dep.getAttribute('data-chart-if')] ?? '');
    }
}

function tipOf(c) {
    return c.el.querySelector('[data-chart-tip]');
}

function showTip(c, p) {
    const tip = tipOf(c);
    if (!tip) {
        const f = fields(c, p);
        const said = c.data.series.length > 1 && !c.pie ? `${f.series}: ${f.value}` : f.value;
        const words = [f.category, said].filter(Boolean).join(' — ');
        const host = c.svg.getBoundingClientRect();
        const r = p.box
            ? { left: host.left + p.box.x, top: host.top + p.box.y, width: p.box.w, height: p.box.h }
            : { left: host.left + p.x - 6, top: host.top + p.y - 6, width: 12, height: 12 };
        r.right = r.left + r.width;
        r.bottom = r.top + r.height;
        if (words && window.sednaUi && window.sednaUi.tips && window.sednaUi.tips.at) window.sednaUi.tips.at(r, words, 'top');
        return;
    }
    const f = fields(c, p);
    for (const old of tip.querySelectorAll('[data-chart-row]')) old.remove();
    const row = tip.querySelector('template[data-chart-tip-row]');
    if (row && !c.pie) {
        let after = row;
        for (const { s, i } of shownSeries(c)) {
            const point = c.points.find(q => q.s === i && q.i === p.i);
            if (!point) continue;
            const frag = row.content.cloneNode(true);
            const made = Array.from(frag.children);
            for (const el of made) {
                el.setAttribute('data-chart-row', '');
                el.setAttribute('data-chart-tone', point.tone || s.tone);
                el.toggleAttribute('data-chart-current', i === p.s);
                fillSlots(el, fields(c, point), false);
            }
            row.parentNode.insertBefore(frag, after.nextSibling);
            if (made.length) after = made[made.length - 1];
        }
        // The rows say each series; the rest of the tip says the category.
        fillSlots(tip, f, true);
    } else {
        fillSlots(tip, f, false);
    }
    tip.setAttribute('data-chart-tone', p.tone || '1');
    placeTip(c, tip, p);
}

function placeTip(c, tip, p) {
    const stage = c.el.getBoundingClientRect();
    const host = c.svg.getBoundingClientRect();
    tip.hidden = false;
    tip.style.transform = 'translate(0px, 0px)';
    const t = tip.getBoundingClientRect();
    const ox = host.left - stage.left, oy = host.top - stage.top;
    const px = ox + p.x, py = oy + p.y;
    const gap = 12;
    // Beside the point, on the side the reading runs to; the other side when that has no room.
    let x = c.rtl ? px - gap - t.width : px + gap;
    if (x + t.width > stage.width - 4 || x < 4) x = c.rtl ? px + gap : px - gap - t.width;
    let y = py - t.height - gap;
    if (y < 4) y = py + gap;
    x = Math.max(4, Math.min(x, stage.width - t.width - 4));
    y = Math.max(4, Math.min(y, Math.max(4, stage.height - t.height - 4)));
    tip.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
}

function hideTip(c) {
    const tip = tipOf(c);
    if (tip) tip.hidden = true;
    else if (window.sednaUi && window.sednaUi.tips && window.sednaUi.tips.hide) window.sednaUi.tips.hide();
}

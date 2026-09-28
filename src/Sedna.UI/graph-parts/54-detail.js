/* ── The selected record, beside the graph ────────────────────────────────────
   What the canvas cannot be: text. A panel the app wrote — usually a `.card` in a
   `.sedna-split-aside` — shows the selected record and every record it touches, as
   controls a keyboard and a screen reader reach:

     <aside class="card" data-graph-detail data-graph-for="deps" hidden>
       <div class="card-head"><strong data-graph-field="label"></strong></div>
       <div class="card-body"><span data-graph-field="meta"></span></div>
       <ul class="list" data-graph-neighbours>
         <template data-graph-neighbour>
           <li><button class="list-row" type="button" data-graph-action="select">
             <span class="list-title" data-graph-field="label"></span>
             <span class="list-sub" data-graph-link></span>
           </button></li>
         </template>
       </ul>
     </aside>

   The panel is shown while a record is selected and hidden while none is. Its
   `[data-graph-field]` slots take the record's fields; each row cloned from
   `<template data-graph-neighbour>` takes a neighbour's, `[data-graph-link]` takes the
   link's label, and the row carries `data-graph-direction` — `out`, `in` or `both` —
   for the stylesheet or the app to draw an arrow from. A `data-graph-action` in a row
   acts on that neighbour.

   A Blazor app renders its own panel from `sedna-graph-select` instead, and leaves
   `data-graph-detail` off: this is for a page with no app behind it.
   ─────────────────────────────────────────────────────────────────────────── */

function detailPanels(g) {
    const id = g.el.id;
    const frame = g.el.closest('[data-graph-frame]');
    const panels = new Set();
    g.el.querySelectorAll('[data-graph-detail]').forEach(p => panels.add(p));
    frame?.querySelectorAll('[data-graph-detail]').forEach(p => {
        if (!p.getAttribute('data-graph-for') || p.getAttribute('data-graph-for') === id) panels.add(p);
    });
    if (id) document.querySelectorAll(`[data-graph-detail][data-graph-for="${CSS.escape(id)}"]`).forEach(p => panels.add(p));
    return [...panels];
}

function fillSlots(root, data) {
    root.querySelectorAll('[data-graph-field]').forEach(slot => {
        if (slot.closest('template')) return;
        const v = valueOf(data, slot.getAttribute('data-graph-field'));
        const s = Array.isArray(v) ? v.join(', ') : v === undefined || v === null ? '' : String(v);
        slot.textContent = s;
        slot.hidden = s === '';
    });
    root.querySelectorAll('[data-graph-icon]').forEach(i => {
        if (i.closest('template')) return;
        if (i.__base === undefined) i.__base = i.className;
        i.className = (i.__base + ' ' + (data.icon || '')).trim();
        i.hidden = !data.icon;
    });
}

// The first element of a template, cloned — from .content when the parser filled it, or
// from the element itself when Blazor did (its renderer appends into the template).
function cloneTemplate(template) {
    const first = (template.content && template.content.firstElementChild) || template.firstElementChild;
    return first ? first.cloneNode(true) : null;
}

function showDetail(g) {
    const panels = detailPanels(g);
    if (!panels.length) return;
    const node = g.selected && g.selected.nonempty() ? g.selected : null;
    for (const panel of panels) {
        panel.hidden = !node;
        if (!node) continue;
        const data = node.data();
        fillSlots(panel, data);
        panel.setAttribute('data-graph-tone', data.tone || '');
        const list = panel.querySelector('[data-graph-neighbours]');
        const template = list?.querySelector('template[data-graph-neighbour]') || panel.querySelector('template[data-graph-neighbour]');
        if (!list || !template) continue;
        list.querySelectorAll('[data-graph-row]').forEach(r => r.remove());
        const rows = new Map();
        node.connectedEdges().not('.hidden').forEach(e => {
            const out = e.source().same(node);
            const other = out ? e.target() : e.source();
            if (other.same(node)) return;
            const seen = rows.get(other.id());
            if (seen) {
                if (seen.out !== out) seen.direction = 'both';
                seen.links.push(e.data('label'));
                return;
            }
            rows.set(other.id(), { other, out, direction: out ? 'out' : 'in', links: [e.data('label')] });
        });
        const ordered = [...rows.values()].sort((a, b) =>
            String(a.other.data('label')).localeCompare(String(b.other.data('label'))));
        for (const r of ordered) {
            const row = cloneTemplate(template);
            if (!row) break;
            row.setAttribute('data-graph-row', '');
            row.setAttribute('data-graph-direction', r.direction);
            fillSlots(row, r.other.data());
            const link = [...new Set(r.links.filter(Boolean))].join(', ');
            row.querySelectorAll('[data-graph-link]').forEach(s => {
                s.textContent = link;
                s.hidden = !link;
            });
            const acts = row.matches('[data-graph-action]') ? [row] : [];
            row.querySelectorAll('[data-graph-action]').forEach(a => acts.push(a));
            acts.forEach(a => {
                if (!a.getAttribute('value')) a.setAttribute('value', r.other.id());
            });
            list.insertBefore(row, template);
        }
        list.setAttribute('data-graph-count', String(ordered.length));
    }
}

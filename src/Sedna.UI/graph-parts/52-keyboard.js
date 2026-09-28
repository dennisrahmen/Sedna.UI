/* ── The keyboard, and what a screen reader hears ─────────────────────────────
   A canvas is a picture to assistive technology. The graph is made a single stop in
   the tab order, and inside it the keys move a ring from record to record:

     ← ↑ → ↓            the nearest record in that direction
     Page Down / Up     the next / previous record linked to this one — walking the links
     Home               the root, else the most connected record
     Space              select, or clear the selection
     Enter              open it — `sedna-graph-open`, then its data-href — or fold or
                        unfold it, when it is a group that folds
     Shift + arrows     move the view
     + / −   0          zoom in / out, fit everything
     Escape             clear the selection, then the ring

   Every move is said through the app's live region, in the app's words:

     <p class="visually-hidden" data-graph-live aria-live="polite"
        data-graph-announce="{label}, {kind}. {links} links."
        data-graph-announce-select="{label} selected."></p>

   `{name}` is any field of the record — label, kind, meta, group, or a data-* of its
   own — plus `{links}`, how many links on screen touch it. With no live region the ring
   still moves, and the tooltip still says the record's name.

   The graph's element gets `tabindex="0"` and `role="application"` unless the app wrote
   its own. Its accessible name — and `aria-roledescription`, if the app wants the word
   "graph" said — are the app's to write, like every other word on the page.
   ─────────────────────────────────────────────────────────────────────────── */

function fillAnnouncement(template, ele) {
    const data = ele.data();
    const links = ele.connectedEdges().not('.hidden').length;
    return template.replace(/\{([a-zA-Z0-9_-]+)\}/g, (_, name) => {
        if (name === 'links') return String(links);
        const v = valueOf(data, name);
        return Array.isArray(v) ? v.join(', ') : v === undefined || v === null ? '' : String(v);
    }).replace(/\s+([,.;:])/g, '$1').replace(/([,.;:])(?=[,.;:])/g, '').trim();
}

function keyboard(g) {
    const el = g.el;
    const cy = g.cy;
    if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex', '0');
    if (!el.hasAttribute('role')) el.setAttribute('role', 'application');

    const live = () => scoped(g, '[data-graph-live]')[0] || null;

    function announce(ele, which) {
        const region = live();
        if (!region || !ele) return;
        const template = region.getAttribute(which === 'select' ? 'data-graph-announce-select' : 'data-graph-announce')
            || (which === 'select' ? '' : '{label}');
        if (!template) return;
        // Cleared first, so saying the same record twice is still a change.
        region.textContent = '';
        setTimeout(() => { region.textContent = fillAnnouncement(template, ele); }, 30);
    }

    const shown = () => cy.nodes().not('.hidden').filter(n => !n.hasClass('eh-ghost') && !n.hasClass('eh-handle'));

    function start() {
        const nodes = shown();
        if (nodes.empty()) return null;
        if (g.selected && g.selected.nonempty() && !g.selected.hasClass('hidden')) return g.selected;
        const root = nodes.filter('[?root], .focus');
        if (root.nonempty()) return root.first();
        return nodes.toArray().sort((a, b) => (b.data('degree') || 0) - (a.data('degree') || 0) || byId(a, b))[0];
    }

    function key(n, say = true) {
        if (g.keyed && g.keyed.nonempty()) g.keyed.removeClass('keyed');
        g.keyed = n && n.nonempty() ? n : null;
        if (!g.keyed) {
            g.tip.hide();
            light(g, resting(g));
            declutter(g);
            return;
        }
        g.keyed.addClass('keyed');
        light(g, g.keyed);
        declutter(g);
        // Keep the ring on screen: a record at the edge is brought in, not the whole view moved.
        const ext = cy.extent();
        const p = g.keyed.position();
        const mx = ext.w * 0.12, my = ext.h * 0.12;
        if (p.x < ext.x1 + mx || p.x > ext.x2 - mx || p.y < ext.y1 + my || p.y > ext.y2 - my) {
            cy.stop();
            if (reducedMotion() || document.visibilityState !== 'visible') cy.center(g.keyed);
            else cy.animate({ center: { eles: g.keyed } }, { duration: 180, complete: () => { if (g.keyed) g.tip.show(g.keyed, true); } });
        }
        g.tip.show(g.keyed, true);
        if (say) announce(g.keyed, 'focus');
        g.emit('sedna-graph-hover', nodeDetail(g.keyed, { keyboard: true }));
    }

    function towards(dx, dy) {
        const from = g.keyed;
        if (!from) return start();
        const a = from.position();
        let best = null, bestScore = Infinity;
        shown().forEach(n => {
            if (n.same(from) || n.isParent()) return;
            const b = n.position();
            const vx = b.x - a.x, vy = b.y - a.y;
            const dist = Math.hypot(vx, vy);
            if (dist < 1) return;
            const cos = (vx * dx + vy * dy) / dist;
            if (cos < 0.5) return;            // within 60° of the arrow
            const score = dist * (2 - cos);   // straight ahead beats a little closer
            if (score < bestScore) { bestScore = score; best = n; }
        });
        return best;
    }

    function along(step) {
        const from = g.keyed || start();
        if (!from) return null;
        const around = from.neighborhood().nodes().not('.hidden').toArray()
            .sort((a, b) => {
                const pa = a.position(), pb = b.position(), c = from.position();
                return Math.atan2(pa.y - c.y, pa.x - c.x) - Math.atan2(pb.y - c.y, pb.x - c.x);
            });
        if (!around.length) return null;
        g.walk = g.walk && g.walk.from === from.id() ? g.walk : { from: from.id(), at: -1 };
        g.walk.at = g.walk.at < 0
            ? (step > 0 ? 0 : around.length - 1)
            : (g.walk.at + step + around.length) % around.length;
        const next = around[g.walk.at];
        g.walk = { from: next.id(), at: -1 };
        return next;
    }

    function onKey(e) {
        if (e.target !== el || e.altKey || e.ctrlKey || e.metaKey) return;
        const pan = 60;
        let handled = true;
        switch (e.key) {
            case 'ArrowLeft': case 'ArrowRight': case 'ArrowUp': case 'ArrowDown': {
                const dx = e.key === 'ArrowLeft' ? -1 : e.key === 'ArrowRight' ? 1 : 0;
                const dy = e.key === 'ArrowUp' ? -1 : e.key === 'ArrowDown' ? 1 : 0;
                if (e.shiftKey) {
                    g.touched = true;
                    cy.panBy({ x: -dx * pan, y: -dy * pan });
                    break;
                }
                const next = towards(dx, dy);
                if (next) key(next);
                break;
            }
            case 'PageDown': case 'PageUp': {
                const next = along(e.key === 'PageDown' ? 1 : -1);
                if (next) key(next);
                break;
            }
            case 'Home': {
                const first = start();
                if (first) key(first);
                break;
            }
            case ' ': case 'Spacebar':
                if (!g.keyed) key(start());
                else if (g.selected && g.selected.same(g.keyed)) select(g, null);
                else {
                    select(g, g.keyed);
                    announce(g.keyed, 'select');
                }
                break;
            case 'Enter':
                if (g.keyed && !toggleFold(g, g.keyed)) open(g, g.keyed, true);
                break;
            case '+': case '=':
                zoomBy(g, 1.25);
                break;
            case '-': case '_':
                zoomBy(g, 1 / 1.25);
                break;
            case '0':
                g.touched = false;
                fitView(g, null, true);
                break;
            case 'Escape':
                if (g.selected && g.selected.nonempty()) select(g, null);
                else if (g.keyed) key(null);
                else handled = false;
                break;
            case 'ContextMenu':
                if (g.keyed) context(g, g.keyed, null);
                break;
            case 'F10':
                if (e.shiftKey && g.keyed) context(g, g.keyed, null);
                else handled = false;
                break;
            default:
                handled = false;
        }
        if (handled) e.preventDefault();
    }

    function onFocus() {
        // Only for the keyboard: a click that focuses the graph has already chosen a record.
        const pointer = Date.now() - (g.pointerAt || 0) < 800;
        if (!g.keyed && !pointer && el.matches(':focus-visible')) key(start());
    }
    function onBlur(e) {
        if (e.relatedTarget && el.contains(e.relatedTarget)) return;
        if (g.keyed) key(null);
    }

    el.addEventListener('keydown', onKey);
    el.addEventListener('focus', onFocus);
    el.addEventListener('blur', onBlur);

    return {
        key,
        announce,
        destroy() {
            el.removeEventListener('keydown', onKey);
            el.removeEventListener('focus', onFocus);
            el.removeEventListener('blur', onBlur);
        },
    };
}

function zoomBy(g, factor) {
    const cy = g.cy;
    g.touched = true;
    const level = Math.max(cy.minZoom(), Math.min(cy.maxZoom(), cy.zoom() * factor));
    const centre = g.keyed && g.keyed.nonempty() ? g.keyed.renderedPosition() : { x: cy.width() / 2, y: cy.height() / 2 };
    cy.stop();
    if (reducedMotion() || document.visibilityState !== 'visible') {
        cy.zoom({ level, renderedPosition: centre });
        settle(g);
    } else {
        cy.animate({ zoom: { level, renderedPosition: centre } }, { duration: 160, complete: () => settle(g) });
    }
}

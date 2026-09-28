/* ── The context menu ─────────────────────────────────────────────────────────
   A right click, a long press, or the context-menu key on the keyboard's record
   dispatches `sedna-graph-context` { id, x, y } on the graph — cancelable, for an app
   that opens something of its own. If it is not cancelled and the app wrote a menu
   inside the graph, that menu opens where the pointer is:

     <div class="menu graph-menu" data-graph-menu role="menu" hidden>
       <span class="menu-label" data-graph-field="label"></span>
       <button class="menu-item" role="menuitem" type="button" data-graph-action="open">
         <i class="ri-external-link-line"></i> Open</button>
       <button class="menu-item" role="menuitem" type="button" data-graph-action="focus">
         <i class="ri-focus-3-line"></i> Show its neighbourhood</button>
     </div>

   It is the library's `.menu`, with the app's items and words. An item with a
   `data-graph-action` acts on the record the menu was opened on; any other item is the
   app's own, and the event told it which record that was. Escape, a click elsewhere,
   a scroll or picking an item closes it, and focus goes back to the graph.
   ─────────────────────────────────────────────────────────────────────────── */

function menus(g) {
    const menu = g.el.querySelector('[data-graph-menu]');
    let target = null;

    function items() {
        return Array.from(menu.querySelectorAll('.menu-item, [role="menuitem"]'))
            .filter(i => !i.hidden && !i.disabled && i.getAttribute('aria-disabled') !== 'true');
    }

    function open(ele, at, keyboard) {
        if (!menu) return;
        g.tip.hide();
        target = ele;
        menu.setAttribute('data-graph-target', ele ? ele.id() : '');
        const data = ele ? ele.data() : {};
        menu.querySelectorAll('[data-graph-field]').forEach(slot => {
            const v = valueOf(data, slot.getAttribute('data-graph-field'));
            const s = Array.isArray(v) ? v.join(', ') : v === undefined || v === null ? '' : String(v);
            slot.textContent = s;
            slot.hidden = s === '';
        });
        // An item that needs a record, or a group, hides itself where there is none.
        menu.querySelectorAll('[data-graph-action]').forEach(item => {
            const a = item.getAttribute('data-graph-action');
            const needs = ['open', 'focus', 'select', 'hide', 'expand', 'collapse'].includes(a);
            const group = a === 'expand' ? ele && g.ec && g.ec.isExpandable(ele)
                : a === 'collapse' ? ele && g.ec && g.ec.isCollapsible(ele) : true;
            item.hidden = (needs && !ele) || !group || (a === 'open' && ele && !ele.data('href') && !item.hasAttribute('data-graph-always'));
        });
        menu.hidden = false;
        const stage = g.el.getBoundingClientRect();
        const m = menu.getBoundingClientRect();
        let x = at.x, y = at.y;
        if (x + m.width > stage.width - 6) x = Math.max(6, at.x - m.width);
        if (y + m.height > stage.height - 6) y = Math.max(6, at.y - m.height);
        menu.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
        if (keyboard) items()[0]?.focus();
    }

    function close(refocus) {
        if (!menu || menu.hidden) return;
        menu.hidden = true;
        target = null;
        if (refocus) g.el.focus({ preventScroll: true });
    }

    function onKey(e) {
        if (menu.hidden) return;
        const list = items();
        const at = list.indexOf(document.activeElement);
        if (e.key === 'Escape') { close(true); e.preventDefault(); e.stopPropagation(); }
        else if (e.key === 'ArrowDown') { list[(at + 1) % list.length]?.focus(); e.preventDefault(); }
        else if (e.key === 'ArrowUp') { list[(at - 1 + list.length) % list.length]?.focus(); e.preventDefault(); }
        else if (e.key === 'Home') { list[0]?.focus(); e.preventDefault(); }
        else if (e.key === 'End') { list[list.length - 1]?.focus(); e.preventDefault(); }
        else if (e.key === 'Tab') close(false);
    }
    const onOutside = e => { if (!menu.hidden && !menu.contains(e.target)) close(false); };
    const onPick = e => {
        const item = e.target.closest('.menu-item, [role="menuitem"]');
        if (item && menu.contains(item)) setTimeout(() => close(true), 0);
    };

    if (menu) {
        menu.addEventListener('keydown', onKey);
        menu.addEventListener('click', onPick);
        document.addEventListener('pointerdown', onOutside, true);
    }

    return {
        present: !!menu,
        open,
        close,
        get target() { return target; },
        destroy() {
            if (!menu) return;
            menu.removeEventListener('keydown', onKey);
            menu.removeEventListener('click', onPick);
            document.removeEventListener('pointerdown', onOutside, true);
        },
    };
}

/* `sedna-graph-context` first; the app's menu only if nobody cancelled it. `at` is a
   point in the graph's own pixels; from the keyboard it is the record's centre. */
function context(g, ele, at) {
    const point = at || (ele ? ele.renderedPosition() : { x: g.cy.width() / 2, y: g.cy.height() / 2 });
    const detail = Object.assign(ele ? nodeDetail(ele) : { id: null }, { x: Math.round(point.x), y: Math.round(point.y) });
    const go = g.emit('sedna-graph-context', detail, true);
    if (go && g.menu.present) g.menu.open(ele, point, !at);
}

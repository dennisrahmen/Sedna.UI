/* ── Selection toolbar ───────────────────────────────────────────────────────
   Text the reader selects inside an element carrying
   `data-selection-toolbar="<id>"` shows the app's toolbar of that id over the
   selection — quote it, copy it, ask about it:

     <div class="chat" role="log" data-selection-toolbar="sel-bar">…messages…</div>
     <div class="selection-toolbar" id="sel-bar" popover="manual" role="toolbar" aria-label="Selected text">
       <button class="btn btn--sm btn--ghost" type="button" data-selection-action="quote">…Quote…</button>
       <button class="btn btn--sm btn--ghost" type="button" data-selection-action="explain">…Explain…</button>
     </div>

   A press on a `[data-selection-action]` control dispatches a bubbling
   `sedna-selection` on the toolbar with `action` (the attribute's value), `text`
   (what was selected), `item` (the `data-selection-item` of the element both ends
   of the selection sit in — a message's id — or null) and `source` (the id of the
   `data-selection-toolbar` element, or null), then closes the toolbar. What the
   action does is the app's.

   The toolbar opens once the selection is made — the pointer released, or a
   keyboard selection changed — and closes when the selection collapses or leaves
   the element, on Escape, on a scroll, and when focus leaves it; a resize places it
   again.
   Alt+F10 moves focus into an open toolbar, ← → Home End move inside it, mirrored
   right to left, and Escape gives focus back.

   ── Measured in JavaScript, by decision ──
   Everywhere else in the library a floating panel is placed by CSS anchor
   positioning and nothing is measured. That cannot place this one: a selection is
   a range of text, not an element, so there is nothing to carry an `anchor-name`
   and nothing for `position-area` to place against. So this part measures the
   selection's rectangle and writes `left` and `top` on the toolbar — above the
   selection, below it when there is no room above, clamped into the viewport —
   and `data-side` says which. It is an exception the owner accepted for this one
   case. As with an anchored menu (22-anchored.js), it is placed when it opens and
   closes on a scroll rather than following.

   The toolbar is a manual popover, so it is in the top layer above every z-index;
   a toolbar without a `popover` attribute is given one. A selection inside an open
   modal `<dialog>` needs its toolbar inside that dialog too, or it is inert.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var GAP = 8;            // between the selection and the toolbar
    var MARGIN = 8;         // between the toolbar and the viewport's edge

    var open = null;        // { bar, host, text, item, from, stop } while one is open
    var pressing = false;   // a pointer is down: a selection being dragged is not made yet

    function elementOf(node) {
        return node ? (node.nodeType === 1 ? node : node.parentElement) : null;
    }

    function hostOf(node) {
        var el = elementOf(node);
        return el && el.closest ? el.closest('[data-selection-toolbar]') : null;
    }

    function barOf(host) {
        var id = host.getAttribute('data-selection-toolbar');
        return id ? document.getElementById(id) : null;
    }

    function isOpen(bar) {
        try { return bar.matches(':popover-open'); } catch (e) { return false; }
    }

    // The selection as this part understands it, or null for none worth a toolbar.
    function read() {
        var sel = window.getSelection ? window.getSelection() : null;
        if (!sel || !sel.rangeCount || sel.isCollapsed) return null;
        var host = hostOf(sel.anchorNode);
        if (!host || hostOf(sel.focusNode) !== host) return null;
        // A field's own selection is the field's, and its menu is the platform's.
        var active = document.activeElement;
        if (active && host.contains(active) && active.matches('input, textarea')) return null;

        var text = sel.toString();
        if (!text || !text.trim()) return null;
        var bar = barOf(host);
        if (!bar) return null;

        var range = sel.getRangeAt(0);
        var rect = range.getBoundingClientRect();
        if (!rect.width && !rect.height) {
            var rects = range.getClientRects();
            if (!rects.length) return null;
            rect = rects[0];
        }
        var common = elementOf(range.commonAncestorContainer);
        var item = common ? common.closest('[data-selection-item]') : null;
        return {
            host: host, bar: bar, text: text, rect: rect,
            item: item && host.contains(item) ? item.getAttribute('data-selection-item') : null
        };
    }

    function place(bar, rect) {
        // Parked at the origin first, so it is measured at its full width rather than
        // shrunk against whatever is left of the viewport where it last stood.
        bar.style.left = '0px';
        bar.style.top = '0px';
        var w = bar.offsetWidth, h = bar.offsetHeight;
        var vw = document.documentElement.clientWidth, vh = document.documentElement.clientHeight;

        var side = 'top';
        var top = rect.top - GAP - h;
        if (top < MARGIN) { side = 'bottom'; top = rect.bottom + GAP; }
        // A selection taller than the screen leaves room on neither side: inside it, then.
        top = Math.max(MARGIN, Math.min(top, vh - MARGIN - h));
        var left = rect.left + rect.width / 2 - w / 2;
        left = Math.max(MARGIN, Math.min(left, vw - MARGIN - w));

        bar.style.left = Math.round(left) + 'px';
        bar.style.top = Math.round(top) + 'px';
        bar.setAttribute('data-side', side);
    }

    function show(found) {
        var bar = found.bar;
        if (open && open.bar !== bar) close(false);
        if (!bar.hasAttribute('popover')) bar.setAttribute('popover', 'manual');
        try { if (!isOpen(bar)) bar.showPopover(); } catch (e) { return; }
        place(bar, found.rect);

        var from = open && open.bar === bar ? open.from : null;
        if (open && open.stop) open.stop();
        open = {
            bar: bar, host: found.host, text: found.text, item: found.item, from: from,
            stop: ui._.layer.whenGone(found.host, function () { close(false); })
        };
    }

    // `restore` gives focus back to where it was before Alt+F10, when it is in the bar.
    function close(restore) {
        if (!open) return;
        var was = open;
        open = null;
        if (was.stop) was.stop();
        var inside = was.bar.contains(document.activeElement);
        try { if (isOpen(was.bar)) was.bar.hidePopover(); } catch (e) { /* already closed */ }
        if (restore && inside) {
            var back = was.from && was.from.isConnected && was.from !== document.body ? was.from : null;
            try {
                if (back) back.focus({ preventScroll: true });
                else if (document.activeElement && document.activeElement.blur) document.activeElement.blur();
            } catch (e) { /* refused */ }
        }
    }

    function update() {
        var found = read();
        if (found) { show(found); return; }
        // Focus moved into the bar may take the selection with it; the text is kept.
        if (open && !open.bar.contains(document.activeElement)) close(false);
    }

    function controls(bar) {
        var all = bar.querySelectorAll('button, [href], input, select, textarea, [tabindex]');
        var list = [];
        for (var i = 0; i < all.length; i++) {
            var c = all[i];
            if (c.disabled || c.getAttribute('tabindex') === '-1' || !c.getClientRects().length) continue;
            list.push(c);
        }
        return list;
    }

    document.addEventListener('pointerdown', function (e) {
        if (open && open.bar.contains(e.target)) return;
        pressing = true;
    }, true);

    // A press in the toolbar would take the selection away before the click lands.
    document.addEventListener('mousedown', function (e) {
        if (open && open.bar.contains(e.target)) e.preventDefault();
    }, true);

    function released() {
        if (!pressing) return;
        pressing = false;
        // A zero delay: the selection a double or triple click makes is set after the
        // pointer is released.
        setTimeout(update, 0);
    }
    document.addEventListener('pointerup', released, true);
    document.addEventListener('pointercancel', released, true);

    document.addEventListener('selectionchange', function () {
        if (pressing) {
            if (open) {
                var sel = window.getSelection();
                if (!sel || sel.isCollapsed) close(false);
            }
            return;
        }
        update();
    });

    document.addEventListener('keydown', function (e) {
        if (!open || e.defaultPrevented) return;
        var bar = open.bar;

        if (e.key === 'Escape') {
            e.preventDefault();
            e.stopPropagation();
            close(true);
            return;
        }
        if (e.key === 'F10' && e.altKey) {
            var first = controls(bar)[0];
            if (!first) return;
            e.preventDefault();
            if (!bar.contains(document.activeElement)) open.from = document.activeElement;
            try { first.focus({ preventScroll: true }); } catch (err) { /* refused */ }
            return;
        }
        if (!bar.contains(e.target)) return;

        var list = controls(bar);
        var at = list.indexOf(document.activeElement);
        if (at < 0) return;
        var rtl = getComputedStyle(bar).direction === 'rtl';
        var next = -1;
        if (e.key === 'ArrowRight') next = at + (rtl ? -1 : 1);
        else if (e.key === 'ArrowLeft') next = at + (rtl ? 1 : -1);
        else if (e.key === 'Home') next = 0;
        else if (e.key === 'End') next = list.length - 1;
        else return;
        e.preventDefault();
        next = (next + list.length) % list.length;
        list[next].focus({ preventScroll: true });
    }, true);

    document.addEventListener('focusout', function (e) {
        if (!open || !open.bar.contains(e.target)) return;
        var to = e.relatedTarget;
        if (to && !open.bar.contains(to)) close(false);
    });

    document.addEventListener('click', function (e) {
        var control = e.target && e.target.closest ? e.target.closest('[data-selection-action]') : null;
        if (!control || !open || !open.bar.contains(control)) return;
        var was = open;
        var detail = {
            action: control.getAttribute('data-selection-action'),
            text: was.text,
            item: was.item,
            source: was.host.id || null
        };
        close(true);
        was.bar.dispatchEvent(new CustomEvent('sedna-selection', { bubbles: true, detail: detail }));
    });

    // Placed once, like an anchored menu: when the page moves under it, it closes.
    document.addEventListener('scroll', function (e) {
        if (!open) return;
        var t = e.target;
        if (t && t.nodeType === 1 && open.bar.contains(t)) return;
        close(false);
    }, true);
    // A resize moves the text under it, and the selection with it: placed again.
    window.addEventListener('resize', function () { if (open) update(); });

    ui.selection = {
        /* The open toolbar's selection — { text, item, source } — or null. */
        current: function () {
            return open ? { text: open.text, item: open.item, source: open.host.id || null } : null;
        },
        /* Closes the open toolbar, leaving the selection as it is. */
        close: function () { close(true); }
    };

})(window.sednaUi);

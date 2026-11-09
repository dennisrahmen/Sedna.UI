/* ── Resizing by a handle ────────────────────────────────────────────────────
   A card the reader makes taller, an aside the reader makes wider: the app writes the
   handle, and the script moves one custom property the CSS already reads.

     <div class="card card--resizable" id="w1">
       …
       <div class="card-resize" data-resize="--card-height" role="separator"
            aria-orientation="horizontal" aria-label="Resize Open orders" tabindex="0"></div>
     </div>

   `data-resize` names the property, written on the handle's parent — or on the element
   `data-resize-on` names by id. The size measured is that element's own, or that of the
   child `data-resize-measure` selects within it (a split's aside). `aria-orientation`
   says which way it moves: horizontal resizes the block size, vertical the inline size.
   A drag snaps to `data-resize-step` pixels (8 unless set); `aria-valuemin` and
   `aria-valuemax` bound it. The arrow keys move the handle a step, Shift four steps,
   and Home or a double-click puts the size back to what the CSS gives it.

   The app writes the handle's starting `aria-valuenow` — a separator that takes the focus
   needs one — and the script keeps it current. Nothing else is written: `aria-valuenow`
   on the handle, and the property on the element. A bubbling `sedna-resize` on that element, with `property` and `value` in
   pixels (null once reset), says where it ended, so an app can keep it.

   A TABLE COLUMN. A `.col-resize` grip in a header cell of a `.table--fixed` is a handle
   on `--col-width`, written on that cell. `data-resize-key` names the column, and
   `sedna-resize` carries it as `key`, so an app keeps a width per column. Without
   `aria-valuemin` and `aria-valuemax`, the bounds are the handle's `--resize-min` and
   `--resize-max` — lengths, which a grip takes from its column's `--col-min` and
   `--col-max`. In a keyboard grid the grip is one of the cell's controls and keeps the
   arrows along its axis and Home; the rest of the keys are the grid's. On a cell that
   holds a grip, Alt+Shift with an arrow moves it a step, and Alt+Shift+Home resets it,
   without leaving the cell.

   FOLDING. `data-resize-fold="<px>"` lets the reader put a thing away by dragging it
   small: a drag that ends with the pointer below that size, or an arrow key towards
   smaller when the size is already at its minimum, folds instead of resizing. The size
   goes back to what it was before, so unfolding returns to the reader's height, and
   `sedna-resize` says `folded: true` with that size as `value`; the app folds — a card
   takes `.card--folded` — the way a drop moves nothing and the app moves the item. While
   a drag is below the threshold the handle carries `data-resize-folding`, for the app to
   show what letting go will do.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function parts(handle) {
        var on = handle.getAttribute('data-resize-on');
        var target = on ? document.getElementById(on) : handle.parentElement;
        if (!target) return null;
        var sel = handle.getAttribute('data-resize-measure');
        var measured = sel ? target.querySelector(sel) : target;
        if (!measured) return null;
        var block = handle.getAttribute('aria-orientation') !== 'vertical';
        return {
            target: target, measured: measured, block: block,
            property: handle.getAttribute('data-resize'), key: handle.getAttribute('data-resize-key')
        };
    }

    function size(p) {
        var r = p.measured.getBoundingClientRect();
        return p.block ? r.height : r.width;
    }

    /* +1 when the handle moving down or right on screen grows the measured box: the box
       sits above or to the left of it. Measured on screen, so a right-to-left page needs
       nothing of its own here. */
    function sign(handle, p) {
        var m = p.measured.getBoundingClientRect(), h = handle.getBoundingClientRect();
        if (p.block) return (m.top + m.height / 2) <= (h.top + h.height / 2) ? 1 : -1;
        var before = (m.left + m.width / 2) <= (h.left + h.width / 2);
        return before ? 1 : -1;
    }

    /* `aria-valuemin` and `aria-valuemax`, else the handle's `--resize-min` and
       `--resize-max`: registered lengths, so the browser has already resolved a rem or a
       percentage to pixels, and `none` where nothing set one. */
    function bounds(handle) {
        var min = parseFloat(handle.getAttribute('aria-valuemin'));
        var max = parseFloat(handle.getAttribute('aria-valuemax'));
        if (isNaN(min) || isNaN(max)) {
            var cs = getComputedStyle(handle);
            if (isNaN(min)) min = parseFloat(cs.getPropertyValue('--resize-min'));
            if (isNaN(max)) max = parseFloat(cs.getPropertyValue('--resize-max'));
        }
        return { min: isNaN(min) ? 48 : min, max: isNaN(max) ? Infinity : max };
    }

    function stepOf(handle) {
        var s = parseFloat(handle.getAttribute('data-resize-step'));
        return isNaN(s) || s <= 0 ? 8 : s;
    }

    function apply(handle, p, value) {
        var b = bounds(handle), step = stepOf(handle);
        value = Math.min(b.max, Math.max(b.min, Math.round(value / step) * step));
        p.target.style.setProperty(p.property, value + 'px');
        handle.setAttribute('aria-valuenow', String(value));
        return value;
    }

    function done(p, value, folded) {
        p.target.dispatchEvent(new CustomEvent('sedna-resize', {
            bubbles: true,
            detail: { property: p.property, value: value, folded: !!folded, key: p.key }
        }));
    }

    function foldAt(handle) {
        var f = parseFloat(handle.getAttribute('data-resize-fold'));
        return isNaN(f) ? null : f;
    }

    /* Puts the property and the handle's value back to what they were before, and says it
       folded. `value` is what the property held, in pixels, or null when it held nothing. */
    function restore(handle, p, before, now) {
        if (before) p.target.style.setProperty(p.property, before);
        else p.target.style.removeProperty(p.property);
        if (now === null) handle.removeAttribute('aria-valuenow');
        else handle.setAttribute('aria-valuenow', now);
    }

    function fold(handle, p, before, now) {
        restore(handle, p, before, now);
        var value = parseFloat(before);
        done(p, isNaN(value) ? null : value, true);
    }

    function reset(handle) {
        var p = parts(handle);
        if (!p) return;
        p.target.style.removeProperty(p.property);
        handle.removeAttribute('aria-valuenow');
        done(p, null);
    }

    ui.resize = { reset: reset };

    document.addEventListener('pointerdown', function (e) {
        if (e.button !== 0) return;
        var handle = e.target instanceof Element ? e.target.closest('[data-resize]') : null;
        if (!handle) return;
        var p = parts(handle);
        if (!p) return;
        e.preventDefault();
        var start = size(p), dir = sign(handle, p), origin = p.block ? e.clientY : e.clientX;
        var value = start, folding = false, threshold = foldAt(handle);
        var before = p.target.style.getPropertyValue(p.property), now = handle.getAttribute('aria-valuenow');
        try { handle.setPointerCapture(e.pointerId); } catch (err) { /* already released */ }
        handle.setAttribute('data-resizing', '');

        function move(ev) {
            var at = p.block ? ev.clientY : ev.clientX;
            var wanted = start + (at - origin) * dir;
            folding = threshold !== null && wanted < threshold;
            if (folding) handle.setAttribute('data-resize-folding', '');
            else handle.removeAttribute('data-resize-folding');
            value = apply(handle, p, wanted);
        }
        function up(ev) {
            handle.removeEventListener('pointermove', move);
            handle.removeEventListener('pointerup', up);
            handle.removeEventListener('pointercancel', up);
            handle.removeAttribute('data-resizing');
            handle.removeAttribute('data-resize-folding');
            // A cancelled drag below the threshold folds nothing: the size goes back, unsaid.
            if (folding && ev.type === 'pointerup') fold(handle, p, before, now);
            else if (folding) restore(handle, p, before, now);
            else if (value !== start) done(p, value);
        }
        handle.addEventListener('pointermove', move);
        handle.addEventListener('pointerup', up);
        handle.addEventListener('pointercancel', up);
    });

    document.addEventListener('dblclick', function (e) {
        var handle = e.target instanceof Element ? e.target.closest('[data-resize]') : null;
        if (handle) reset(handle);
    });

    /* +1 or -1 for an arrow along the handle's axis — the way it moves the handle on
       screen, as a drag would — or 0 for any other key. */
    function arrow(handle, key) {
        var block = handle.getAttribute('aria-orientation') !== 'vertical';
        var forward = block ? { ArrowDown: 1, ArrowUp: -1 } : { ArrowRight: 1, ArrowLeft: -1 };
        return forward[key] || 0;
    }

    /* One key on a handle: Home resets it, an arrow along its axis moves it `steps` steps.
       False for any other key. */
    function press(handle, key, steps) {
        var p = parts(handle);
        if (!p) return false;
        if (key === 'Home') { reset(handle); return true; }
        var by = arrow(handle, key);
        if (!by) return false;
        var current = size(p), wanted = current + by * sign(handle, p) * stepOf(handle) * steps;
        // Already as small as it goes, and asked for less: put it away.
        if (foldAt(handle) !== null && wanted < current && current <= bounds(handle).min + 0.5) {
            fold(handle, p, p.target.style.getPropertyValue(p.property), handle.getAttribute('aria-valuenow'));
            return true;
        }
        done(p, apply(handle, p, wanted));
        return true;
    }

    /* The grip that belongs to `el` itself — a header cell's own — not one further in. */
    function ownGrip(el) {
        for (var c = el.firstElementChild; c; c = c.nextElementSibling) {
            if (c.hasAttribute('data-resize')) return c;
        }
        return null;
    }

    document.addEventListener('keydown', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el || e.ctrlKey || e.metaKey) return;
        if (el.hasAttribute('data-resize')) {
            if (e.altKey) return;
            if (press(el, e.key, e.shiftKey ? 4 : 1)) e.preventDefault();
            return;
        }
        // Alt+Shift on the cell a grip sits in: the column resizes and the focus stays.
        if (!e.altKey || !e.shiftKey) return;
        var grip = ownGrip(el);
        if (grip && press(grip, e.key, 1)) e.preventDefault();
    });

    /* In a keyboard grid the grip is one of its cell's controls, and the grid moves on from
       a control with the arrows. Its own axis and Home are the grip's; the grid keeps the
       rest — the other axis, Escape back to the cell. */
    if (ui._.grid) {
        ui._.grid.keys.push(function (e, grid, cell) {
            var el = e.target;
            if (el === cell || !el.hasAttribute('data-resize') || e.altKey || e.ctrlKey || e.metaKey) return false;
            return e.key === 'Home' || arrow(el, e.key) !== 0;
        });
    }

})(window.sednaUi);

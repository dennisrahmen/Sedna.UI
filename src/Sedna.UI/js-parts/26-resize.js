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
        return { target: target, measured: measured, block: block, property: handle.getAttribute('data-resize') };
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

    function bounds(handle) {
        var min = parseFloat(handle.getAttribute('aria-valuemin'));
        var max = parseFloat(handle.getAttribute('aria-valuemax'));
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
            detail: { property: p.property, value: value, folded: !!folded }
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

    document.addEventListener('keydown', function (e) {
        var handle = e.target instanceof Element ? e.target : null;
        if (!handle || !handle.hasAttribute('data-resize')) return;
        var p = parts(handle);
        if (!p) return;
        if (e.key === 'Home') { e.preventDefault(); reset(handle); return; }
        // An arrow moves the handle that way on screen, as a drag would.
        var forward = p.block ? { ArrowDown: 1, ArrowUp: -1 } : { ArrowRight: 1, ArrowLeft: -1 };
        var by = forward[e.key];
        if (!by) return;
        e.preventDefault();
        var current = size(p), wanted = current + by * sign(handle, p) * stepOf(handle) * (e.shiftKey ? 4 : 1);
        // Already as small as it goes, and asked for less: put it away.
        if (foldAt(handle) !== null && wanted < current && current <= bounds(handle).min + 0.5) {
            fold(handle, p, p.target.style.getPropertyValue(p.property), handle.getAttribute('aria-valuenow'));
            return;
        }
        done(p, apply(handle, p, wanted));
    });

})(window.sednaUi);

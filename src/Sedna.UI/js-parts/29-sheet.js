/* ── Sheet, drag to dismiss ──────────────────────────────────────────────────
   Opt-in wiring for a bottom sheet, because the grab handle is drawn as a promise
   the CSS cannot keep:

     <dialog class="drawer sheet" data-sheet>
       <div class="sheet-handle"></div>
       <div class="drawer-header">…</div>
       <div class="drawer-body">…</div>
     </dialog>

   Drag the handle or the header downwards and the sheet follows the pointer; let go
   past a quarter of its height, or flick it, and it closes. Anything short of that
   springs back.

   `data-sheet` rather than automatic, for the reason `data-tabs` is opt-in: an app
   with its own gesture would otherwise have two things closing one dialog.

   <dialog> only. The div form's open state is a class the app owns — in Blazor,
   something the framework rewrites on the next render — so removing it here would be
   reverted silently and only sometimes.

   Four things here are the reason this is not five lines in each app:

     · `touch-action: none` on the grips, in 63-drawer.css. Without it the browser
       claims a vertical drag for scrolling before the first pointermove arrives, and
       the sheet does not move under a finger at all.
     · dismissal is distance OR velocity. Distance alone means a fast short flick —
       which is what a phone user actually does — springs back instead of closing.
     · the closing slide is the platform's, not a second animation. Restoring the
       transition, dropping the inline transform and calling close() in ONE task gives
       the browser a single style recomputation: it sees the drag position as the
       before-change style and `.sheet`'s own translateY(100%) as the after, so it
       interpolates from where the finger let go. Anything that clears the transform
       in an earlier task snaps the panel back to rest for a frame first, and that
       bounce is the whole reason to do it this way.
     · which also means reduced motion needs no branch here: 95-reduced-motion.css
       already takes the transition off `dialog.drawer`, so close() simply hides it.

   The close button is still what makes a sheet dismissible. This is an addition on
   top of it and never the only way out: the handle is not focusable, so a drag is
   not reachable from the keyboard by design.

   SNAP POINTS — data-sheet-snap. A sheet that carries `data-sheet-snap="50%"` rests at
   the heights it lists as well as at its full height: each is the share of the sheet
   that shows. It opens at the first one listed, a drag lets go at the nearest one — a
   flick goes on to the next in its direction — and a drag down past the lowest closes
   it. The sheet is never measured into a coordinate: the rest is written as
   `--sheet-rest`, a translate in percent of the sheet's own height, and the share it
   rests at as `data-sheet-at`. A control inside it with `data-sheet-expand` — the
   handle itself, written as a <button> — is the keyboard's way: it steps to the next
   height up, from the full one back to the first, and says `aria-expanded` while the
   sheet is at its full height.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    // Past a quarter of the sheet's own height it is a dismissal, whatever the speed.
    var DISTANCE = 0.25;
    // …and below that, px per millisecond that counts as a flick, with a floor so the
    // twitch inside a tap is never one.
    var FLICK = 0.5;
    var FLICK_FLOOR = 24;

    var DRAGGING = 'sheet--dragging';

    var drag = null;

    /* The sheet this pointer may drag, or null. A control inside the header is a
       control — the close button has to stay a button, and pressing it must not start
       a gesture instead. */
    function gripped(target) {
        if (!(target instanceof Element)) return null;
        var control = target.closest('button, a, input, select, textarea, [contenteditable]');
        // A handle written as a button is still the handle: a press drags, a click steps.
        if (control && !control.classList.contains('sheet-handle')) return null;

        var grip = target.closest('.sheet-handle, .drawer-header');
        if (!grip) return null;

        var sheet = grip.closest('dialog.sheet[data-sheet]');
        // Direct children only, matching the CSS that makes them draggable. A header
        // nested deeper belongs to something else the sheet happens to contain.
        return sheet && sheet.hasAttribute('open') && grip.parentElement === sheet ? sheet : null;
    }

    /* One task, three mutations, in this order — see the note above. A dismissal is
       a close request like Escape, so it raises `cancel` first: a sheet that asks
       before closing (`data-close-guard` in 42-modal.js) stays open, and with the
       transform already cleared that is the spring back. */
    function slideOut(sheet) {
        sheet.classList.remove(DRAGGING);   // the transition comes back…
        sheet.style.transform = '';         // …and the target becomes .sheet's own
        if (typeof sheet.requestClose === 'function') sheet.requestClose();
        else sheet.close();                 //    translateY(100%), once [open] goes
    }

    function springBack(sheet) {
        sheet.classList.remove(DRAGGING);
        sheet.style.transform = '';
    }

    /* The shares a snapping sheet rests at, smallest first, its full height always
       among them; and the one it opens at. Null for a sheet that does not snap. */
    function snaps(sheet) {
        var raw = sheet.getAttribute('data-sheet-snap');
        if (raw === null) return null;
        var list = [], first = null;
        raw.split(/[\s,]+/).forEach(function (part) {
            var n = parseFloat(part);
            if (!isFinite(n) || n <= 0 || n > 100) return;
            n = Math.round(n * 100) / 10000;
            if (first === null) first = n;
            if (list.indexOf(n) < 0) list.push(n);
        });
        if (list.indexOf(1) < 0) list.push(1);
        list.sort(function (a, b) { return a - b; });
        return { list: list, first: first === null ? 1 : first };
    }

    function shareOf(sheet) {
        var at = parseFloat(sheet.getAttribute('data-sheet-at'));
        return isFinite(at) ? at / 100 : 1;
    }

    function format(share) { return Math.round(share * 10000) / 100 + '%'; }

    /* One task with the drag's own end, for the reason slideOut gives: the platform
       then slides from where the finger let go to the new rest. */
    function rest(sheet, share) {
        sheet.classList.remove(DRAGGING);
        sheet.style.transform = '';
        sheet.style.setProperty('--sheet-rest', format(1 - share));
        sheet.setAttribute('data-sheet-at', format(share));
        var expand = sheet.querySelectorAll('[data-sheet-expand]');
        for (var i = 0; i < expand.length; i++) expand[i].setAttribute('aria-expanded', String(share >= 1));
    }

    /* A snapping sheet opens at its first height, however it was opened — the presenter,
       the app's own showModal() or show(). The open attribute changes synchronously and
       this runs before the first frame, so the panel slides in to that height. */
    function opened(sheet) {
        var s = sheet.matches && sheet.matches('dialog.sheet[data-sheet-snap]') && sheet.open ? snaps(sheet) : null;
        if (s) rest(sheet, s.first);
    }
    if (typeof MutationObserver === 'function') {
        new MutationObserver(function (records) {
            records.forEach(function (r) { opened(r.target); });
        }).observe(document.documentElement, { attributes: true, attributeFilter: ['open'], subtree: true });
    }
    document.querySelectorAll('dialog.sheet[data-sheet-snap][open]').forEach(opened);

    var dragged = false;   // the last press moved the sheet, so its click is not a step

    document.addEventListener('click', function (e) {
        var control = e.target instanceof Element ? e.target.closest('[data-sheet-expand]') : null;
        var sheet = control ? control.closest('dialog.sheet[data-sheet-snap]') : null;
        if (!sheet || !sheet.open) return;
        // detail 0 is a click from the keyboard, which no drag came before.
        if (dragged && e.detail !== 0) { dragged = false; return; }
        var list = snaps(sheet).list, now = shareOf(sheet), next = list[0];
        for (var i = 0; i < list.length; i++) {
            if (list[i] > now + 0.001) { next = list[i]; break; }
        }
        rest(sheet, next);
    });

    document.addEventListener('pointerdown', function (e) {
        if (drag || (e.pointerType === 'mouse' && e.button !== 0)) return;

        dragged = false;
        var sheet = gripped(e.target);
        if (!sheet) return;

        var height = sheet.getBoundingClientRect().height;
        var snap = snaps(sheet);
        drag = {
            sheet: sheet,
            id: e.pointerId,
            y0: e.clientY,
            t0: e.timeStamp,
            dy: 0,
            moved: 0,
            captured: false,
            height: height,
            snap: snap,
            // Where it rests now, in px below its full height: zero for a sheet that does not snap.
            rest: snap ? (1 - shareOf(sheet)) * height : 0
        };
        sheet.classList.add(DRAGGING);

        // Stops the drag selecting the header's text. Focus is untouched — a control
        // never reaches here, and the dialog already holds focus.
        e.preventDefault();
    });

    document.addEventListener('pointermove', function (e) {
        if (!drag || e.pointerId !== drag.id) return;

        // Capture on the sheet once the pointer moves, so the gesture survives it leaving
        // the grip — which it does at once, because the grip is at the top and the drag
        // goes down. Not on the press: a press that does not move is a click, and a handle
        // written as a button has to receive it. Not fatal if the browser refuses it: the
        // listeners are on document.
        var moved = Math.abs(e.clientY - drag.y0);
        if (moved > 3 && !drag.captured) {
            drag.captured = true;
            try { drag.sheet.setPointerCapture(e.pointerId); } catch (err) { /* carry on */ }
        }
        drag.moved = Math.max(drag.moved, moved);

        // Never above its full height. A sheet is anchored to the bottom edge, so dragging
        // it further up lifts it off that edge and shows the page through the gap; a sheet
        // resting lower may be dragged up as far as that.
        drag.dy = Math.max(-drag.rest, e.clientY - drag.y0);
        drag.sheet.style.transform = 'translateY(' + (drag.rest + drag.dy) + 'px)';
    });

    function finish(e) {
        if (!drag || e.pointerId !== drag.id) return;

        var d = drag;
        drag = null;

        try { d.sheet.releasePointerCapture(e.pointerId); } catch (err) { /* already gone */ }

        var speed = d.dy / Math.max(1, e.timeStamp - d.t0);
        if (d.moved > 3) dragged = true;

        if (d.snap) { settle(d, speed); return; }
        if (d.dy > d.height * DISTANCE || (speed > FLICK && d.dy > FLICK_FLOOR)) slideOut(d.sheet);
        else springBack(d.sheet);
    }

    /* Where a snapping sheet goes when the finger lifts. A flick goes to the next height
       in its direction, or closes the sheet from its lowest; a slow drag goes to the
       nearest height, and closes the sheet once it is past a quarter of what the lowest
       one shows. */
    function settle(d, speed) {
        var list = d.snap.list, h = Math.max(1, d.height);
        var share = 1 - (d.rest + d.dy) / h;   // what shows where the finger let go
        var from = 1 - d.rest / h;
        var flick = Math.abs(speed) > FLICK && Math.abs(d.dy) > FLICK_FLOOR;
        var lowest = list[0], i;

        if (flick && speed > 0) {
            for (i = list.length - 1; i >= 0; i--) if (list[i] < from - 0.001) { rest(d.sheet, list[i]); return; }
            slideOut(d.sheet);
            return;
        }
        if (flick) {
            for (i = 0; i < list.length; i++) if (list[i] > from + 0.001) { rest(d.sheet, list[i]); return; }
            rest(d.sheet, 1);
            return;
        }
        if (share < lowest * (1 - DISTANCE)) { slideOut(d.sheet); return; }
        var best = lowest;
        for (i = 0; i < list.length; i++) if (Math.abs(list[i] - share) < Math.abs(best - share)) best = list[i];
        rest(d.sheet, best);
    }

    document.addEventListener('pointerup', finish);
    document.addEventListener('pointercancel', finish);

})(window.sednaUi);

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
        if (target.closest('button, a, input, select, textarea, [contenteditable]')) return null;

        var grip = target.closest('.sheet-handle, .drawer-header');
        if (!grip) return null;

        var sheet = grip.closest('dialog.sheet[data-sheet]');
        // Direct children only, matching the CSS that makes them draggable. A header
        // nested deeper belongs to something else the sheet happens to contain.
        return sheet && sheet.hasAttribute('open') && grip.parentElement === sheet ? sheet : null;
    }

    /* One task, three mutations, in this order — see the note above. */
    function slideOut(sheet) {
        sheet.classList.remove(DRAGGING);   // the transition comes back…
        sheet.style.transform = '';         // …and the target becomes .sheet's own
        sheet.close();                      //    translateY(100%), once [open] goes
    }

    function springBack(sheet) {
        sheet.classList.remove(DRAGGING);
        sheet.style.transform = '';
    }

    document.addEventListener('pointerdown', function (e) {
        if (drag || (e.pointerType === 'mouse' && e.button !== 0)) return;

        var sheet = gripped(e.target);
        if (!sheet) return;

        drag = {
            sheet: sheet,
            id: e.pointerId,
            y0: e.clientY,
            t0: e.timeStamp,
            dy: 0,
            height: sheet.getBoundingClientRect().height
        };
        sheet.classList.add(DRAGGING);

        // Capture on the sheet, so the gesture survives the pointer leaving the grip —
        // which it does immediately, because the grip is at the top and the drag goes
        // down. Not fatal if the browser refuses it: the listeners are on document.
        try { sheet.setPointerCapture(e.pointerId); } catch (err) { /* carry on */ }

        // Stops the drag selecting the header's text. Focus is untouched — a control
        // never reaches here, and the dialog already holds focus.
        e.preventDefault();
    });

    document.addEventListener('pointermove', function (e) {
        if (!drag || e.pointerId !== drag.id) return;

        // Downward only. A sheet is anchored to the bottom edge, so dragging it up
        // lifts it off that edge and shows the page through the gap.
        drag.dy = Math.max(0, e.clientY - drag.y0);
        drag.sheet.style.transform = 'translateY(' + drag.dy + 'px)';
    });

    function finish(e) {
        if (!drag || e.pointerId !== drag.id) return;

        var d = drag;
        drag = null;

        try { d.sheet.releasePointerCapture(e.pointerId); } catch (err) { /* already gone */ }

        var speed = d.dy / Math.max(1, e.timeStamp - d.t0);

        if (d.dy > d.height * DISTANCE || (speed > FLICK && d.dy > FLICK_FLOOR)) slideOut(d.sheet);
        else springBack(d.sheet);
    }

    document.addEventListener('pointerup', finish);
    document.addEventListener('pointercancel', finish);

})(window.sednaUi);

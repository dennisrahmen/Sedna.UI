/* ── The dialog presenter ────────────────────────────────────────────────────
   await sednaUi.modal.show(id)    → dialog.showModal(); resolves with returnValue
   sednaUi.modal.close(id, value)  → dialog.close(value)
   await sednaUi.modal.idle(id)    → resolves once a closed dialog's transition ends

   The presenter for markup the app wrote: a `.modal`, a `.drawer` or a `.sheet`,
   each a <dialog>. The platform gives the top layer, a focus trap, Escape-to-close
   and inert content behind it — all four, none reachable from Blazor without
   IJSRuntime, which the consuming rules say an app should not inject. An app that
   followed those rules otherwise fell back to a `.modal-backdrop` div behind an
   `@if` and lost all four.

   show() resolves with the dialog's returnValue when it closes, or null when it
   closed without one — Escape, close() with no value, a button whose value is
   empty. So a confirmation is the app's own markup and one comparison:

     if (await sednaUi.modal.show('delete-queue') === 'delete') …

   SETTLED FROM THE `open` ATTRIBUTE, NEVER FROM THE `close` EVENT. Every way a
   dialog closes — Escape, a <form method="dialog"> submit, close() from anywhere —
   removes the attribute synchronously, and a MutationObserver sees that as a
   microtask. The `close` event is queued as a task instead, and a background or
   non-compositing tab applies close() without ever dispatching it: an await hanging
   off the event never returns, and in a Blazor handler that is an action that
   silently stops working.

   A dialog removed from the document while open settles null too, so a page
   navigated away from under an open dialog does not leave its caller waiting.

   returnValue is cleared as the dialog opens, because the platform keeps the last
   one: without that, Escape on a second opening reports the first opening's button.

   An id that is not a <dialog> resolves null with a console warning rather than a
   throw. The call sites are Blazor event handlers, where an exception crossing the
   interop boundary tears down the circuit — a wrong id should cost a line in the
   console, not the reader's page.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    // The cap on idle(). A closing transition is --motion-mid; this only has to be
    // longer than any transition an app would give a dialog, and short enough that a
    // background tab, which does not advance animations, is not waited on for long.
    var IDLE_CAP_MS = 1000;

    // One pending promise per open dialog, so a second show() on a dialog that is
    // already open — a Blazor re-render calling it twice — joins the first wait
    // instead of starting a rival one.
    var pending = new WeakMap();

    function dialogById(id, verb) {
        var el = document.getElementById(id);
        if (!el) {
            console.warn('sednaUi.modal.' + verb + ': no element with id "' + id + '".');
            return null;
        }
        if (el.tagName !== 'DIALOG') {
            console.warn('sednaUi.modal.' + verb + ': #' + id + ' is a <' +
                el.tagName.toLowerCase() + '>, not a <dialog>. Only a <dialog> has showModal().');
            return null;
        }
        return el;
    }

    function watch(d) {
        return new Promise(function (resolve) {
            var done = false;
            var attrs = new MutationObserver(check);
            var tree = new MutationObserver(check);

            function check() {
                if (done || (d.open && d.isConnected)) return;
                done = true;
                attrs.disconnect();
                tree.disconnect();
                pending.delete(d);
                // Removed while still open is not an answer, whatever returnValue says.
                resolve(!d.open && d.returnValue ? d.returnValue : null);
            }

            attrs.observe(d, { attributes: true, attributeFilter: ['open'] });
            tree.observe(document, { childList: true, subtree: true });
        });
    }

    ui.modal = {
        show: function (id) {
            var d = dialogById(id, 'show');
            if (!d) return Promise.resolve(null);

            // Already open — by this call before, or by the app's own showModal():
            // showModal() on an open dialog throws InvalidStateError, so join the wait.
            if (!d.open) {
                d.returnValue = '';
                try { d.showModal(); }
                catch (e) {
                    console.warn('sednaUi.modal.show: ' + e.message);
                    return Promise.resolve(null);
                }
            }

            var waiting = pending.get(d);
            if (!waiting) {
                waiting = watch(d);
                pending.set(d, waiting);
            }
            return waiting;
        },

        // The value becomes the dialog's returnValue, which is what show() resolves
        // with — the same place a <form method="dialog"> puts its submitter's value, so
        // both routes out of the dialog are read the same way.
        close: function (id, value) {
            var d = dialogById(id, 'close');
            if (!d || !d.open) return;
            try {
                if (value === undefined || value === null) d.close();
                else d.close(String(value));
            } catch (e) { console.warn('sednaUi.modal.close: ' + e.message); }
        },

        /* Resolves once a dialog that has just closed has finished animating out.

           For something about to remove the dialog from the document, which is what
           an overlay host does after show() resolves: a `.drawer` slides out over
           --motion-mid, and removing it mid-slide cuts the panel off where it stands.
           Reading computed style first is what starts the closing transition, so
           getAnimations() sees it without waiting a frame — and a frame never comes in
           a background tab. A hidden page resolves at once, because nobody can see the
           slide and its animations do not advance; the cap covers the rest. */
        idle: function (id) {
            var d = document.getElementById(id);
            if (!d || d.open || typeof d.getAnimations !== 'function') return Promise.resolve();
            if (document.visibilityState === 'hidden') return Promise.resolve();

            getComputedStyle(d).display;
            var running = d.getAnimations().map(function (a) {
                return a.finished.catch(function () { /* cancelled is finished too */ });
            });
            if (!running.length) return Promise.resolve();

            return Promise.race([
                Promise.all(running),
                new Promise(function (r) { setTimeout(r, IDLE_CAP_MS); })
            ]).then(function () { });
        }
    };

})(window.sednaUi);

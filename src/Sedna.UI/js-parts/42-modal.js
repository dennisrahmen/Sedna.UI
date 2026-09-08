/* ── The platform dialog ─────────────────────────────────────────────────────
   sednaUi.modal.show(id)          → dialog.showModal()
   sednaUi.modal.close(id, value)  → dialog.close(value)

   Two calls, and the reason they exist is not convenience. `.modal` on a <dialog>
   gets the top layer, a focus trap, Escape-to-close and inert content behind it —
   all four from the platform, none of them reachable from Blazor without
   IJSRuntime, which the consuming rules say an app should not inject. So an app
   that followed those rules fell back to a `.modal-backdrop` div behind an `@if`
   and lost all four; sednaUi.confirm already calls showModal() internally, so the
   capability was in the file and simply had no door.

   `confirm` is not a substitute: it takes strings and returns a bool, and these
   dialogs hold forms.

   An id that is not a <dialog> is a no-op with a console warning rather than a
   throw. The call sites are Blazor event handlers, where an exception crossing the
   interop boundary tears down the circuit — a wrong id should cost a line in the
   console, not the reader's page.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

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

    ui.modal = {
        show: function (id) {
            var d = dialogById(id, 'show');
            // Already open: showModal() on an open dialog throws InvalidStateError,
            // and a re-render that calls show() twice is ordinary in Blazor.
            if (!d || d.open) return;
            try { d.showModal(); } catch (e) { console.warn('sednaUi.modal.show: ' + e.message); }
        },

        // returnValue reaches the app through the dialog's own `close` event, which
        // is where a <form method="dialog"> puts its submitter value too — so both
        // routes out of the dialog are read the same way.
        close: function (id, value) {
            var d = dialogById(id, 'close');
            if (!d || !d.open) return;
            try {
                if (value === undefined || value === null) d.close();
                else d.close(String(value));
            } catch (e) { console.warn('sednaUi.modal.close: ' + e.message); }
        }
    };

})(window.sednaUi);

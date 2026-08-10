/* ── Confirmation dialog ─────────────────────────────────────────────────────
   await sednaUi.confirm({ title, message, confirm, cancel, danger })
     → true if confirmed, false if cancelled or dismissed.

   Built on <dialog>.showModal(), which is the whole reason this exists rather than
   an app hand-rolling a .modal-backdrop: the platform gives the top layer, a focus
   trap, Escape-to-close and inert content behind, and none of those are things a
   div-based overlay can do without a lot of code that is usually wrong.

   Replaces window.confirm(), which blocks the thread, cannot be styled, and in
   Blazor Server blocks the circuit while it is open.

   No fallback for a browser without <dialog>. The supported floor is Chromium —
   current Chrome and Edge — which has had it for years; a fallback path would be
   untested code that only ever runs where the library is not supported anyway.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    ui.confirm = function (opts) {
        opts = opts || {};
        var title = opts.title || 'Are you sure?';
        var message = opts.message || '';
        var confirmLabel = opts.confirm || 'Confirm';
        var cancelLabel = opts.cancel || 'Cancel';

        return new Promise(function (resolve) {
            var dialog = document.createElement('dialog');
            dialog.className = 'modal modal-sm';

            var header = el('div', 'modal-header');
            var h3 = el('h3', null, title);
            header.appendChild(h3);

            var body = el('div', 'modal-body');
            if (message) body.appendChild(el('p', null, message));

            var footer = el('div', 'modal-footer');
            var cancel = el('button', 'btn', cancelLabel);
            cancel.type = 'button';
            var ok = el('button', 'btn ' + (opts.danger ? 'btn-danger' : 'btn-primary'), confirmLabel);
            ok.type = 'button';
            footer.appendChild(cancel);
            footer.appendChild(ok);

            dialog.appendChild(header);
            if (message) dialog.appendChild(body);
            dialog.appendChild(footer);
            document.body.appendChild(dialog);

            // Settle on the ACTION, not only on the dialog's `close` event.
            //
            // Resolving purely from `close` gives the promise a single point of
            // failure: if that event does not arrive — and it does not, for instance,
            // in a background or non-compositing tab, where close() still takes
            // effect but the queued event is never dispatched — then `await confirm()`
            // never returns. In a Blazor handler that is an action that silently stops
            // working, with no error anywhere.
            //
            // So every route a user can take settles directly: both buttons, and the
            // `cancel` event that Escape fires. The `close` listener is a third line
            // only — it covers an app calling close() on the dialog itself, and it is
            // no more reliable than the event it hangs off, which is the point. Nothing
            // a user can do depends on it.
            //
            // settled makes the first route win and the rest no-ops, so the paths
            // cannot double-resolve or double-remove.
            var settled = false;
            function settle(value) {
                if (settled) return;
                settled = true;
                try { if (dialog.open) dialog.close(); } catch (e) { /* already closed */ }
                if (dialog.parentNode) dialog.parentNode.removeChild(dialog);
                resolve(value);
            }

            ok.addEventListener('click', function () { settle(true); });
            cancel.addEventListener('click', function () { settle(false); });
            dialog.addEventListener('cancel', function () { settle(false); });   // Escape
            dialog.addEventListener('close', function () { settle(false); });

            dialog.showModal();

            // Focus the SAFE choice. showModal() focuses the first focusable element,
            // which would be Cancel here by source order — but for a destructive
            // action that ordering is the point, so it is made explicit rather than
            // left to depend on the DOM order.
            (opts.danger ? cancel : ok).focus();
        });
    };

})(window.sednaUi);

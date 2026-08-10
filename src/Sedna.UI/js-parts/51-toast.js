/* ── Toasts ──────────────────────────────────────────────────────────────────
   sednaUi.toast('Dispatched ORD-4182', { kind: 'go' })

   For confirming something that already happened. Anything the user must act on is
   an .alert, which stays until the state changes — a toast that carries a required
   action is an action nobody performs.

   The stack is created on first use and reused, so an app renders nothing and
   positions nothing.

   It is found by `data-sedna-toasts`, not by `.toast-stack`, and that distinction is
   load-bearing: only a stack this code created is appended to, re-labelled or removed.
   Matching the class would adopt a stack the app wrote for its own reasons — a
   server-rendered one, an example of the markup on a documentation page — append into
   it wherever it sits, overwrite its aria-live, and remove it with the last toast.

   Announced through aria-live on the stack rather than by moving focus: stealing
   focus to say "saved" interrupts whatever the user is typing. `polite` for the
   ordinary kinds and `assertive` for danger, because a failure is worth cutting in
   for and a success is not.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var ICONS = {
        go: 'ri-check-line',
        warn: 'ri-alert-line',
        danger: 'ri-error-warning-line',
        info: 'ri-information-line'
    };

    var OWN = '[data-sedna-toasts]';

    function stack() {
        var el = document.querySelector(OWN);
        if (!el) {
            el = document.createElement('div');
            el.className = 'toast-stack';
            // The marker is what makes this OURS: only a stack the library created is
            // ever appended to, re-labelled, or removed.
            el.setAttribute('data-sedna-toasts', '');
            // The region is a status log, not a landmark to navigate to.
            el.setAttribute('role', 'status');
            el.setAttribute('aria-live', 'polite');
            document.body.appendChild(el);
        }
        return el;
    }

    /**
     * message  the line to show; a plain string, inserted as text
     * opts     { kind: 'go'|'warn'|'danger'|'info', title, timeout, dismissible }
     * returns  a function that removes this toast early
     */
    ui.toast = function (message, opts) {
        opts = opts || {};
        var kind = ICONS[opts.kind] ? opts.kind : 'info';
        var host = stack();

        // A failure interrupts; a confirmation waits its turn.
        host.setAttribute('aria-live', kind === 'danger' ? 'assertive' : 'polite');

        var el = document.createElement('div');
        el.className = 'toast toast-' + kind;

        var icon = document.createElement('i');
        icon.className = ICONS[kind];
        icon.setAttribute('aria-hidden', 'true');

        var body = document.createElement('div');
        body.className = 'toast-body';
        if (opts.title) {
            var strong = document.createElement('strong');
            strong.textContent = opts.title;
            body.appendChild(strong);
        }
        // textContent, never innerHTML: the message often contains a value from the
        // server, and this is the one place an app would hand us one.
        body.appendChild(document.createTextNode(message == null ? '' : String(message)));

        el.appendChild(icon);
        el.appendChild(body);

        var timer = 0;
        function remove() {
            clearTimeout(timer);
            if (el.parentNode) el.parentNode.removeChild(el);
            if (!host.children.length && host.parentNode) host.parentNode.removeChild(host);
        }

        if (opts.dismissible !== false) {
            var close = document.createElement('button');
            close.type = 'button';
            close.className = 'toast-close';
            close.setAttribute('aria-label', 'Dismiss');
            close.innerHTML = '<i class="ri-close-line" aria-hidden="true"></i>';
            close.addEventListener('click', remove);
            el.appendChild(close);
        }

        host.appendChild(el);

        // 0 means "stays until dismissed" — for a failure the user has to read.
        var ms = opts.timeout === undefined ? 4000 : opts.timeout;
        if (ms > 0) timer = setTimeout(remove, ms);

        return remove;
    };

})(window.sednaUi);

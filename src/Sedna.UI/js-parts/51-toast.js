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

   One of the two pieces of UI the library draws itself (the hover-hint bubble is the
   other): a toast is one line with nothing to author. Its one word of the library's
   own — the close button's label — comes from `dismissLabel`, per call or through
   configure({ toastDismissLabel }), so it is never English in a German app.

   A HANDLE, NOT ONLY A FUNCTION. toast() returns a remover, which JavaScript can
   hold; C# cannot, because a function does not cross the interop boundary. So every
   toast also has an id — toast.show() returns it — and toast.dismiss(id) and
   toast.replace(id, message, opts) act on it: the "Uploading…" toast turns into
   "Uploaded" in place instead of flickering out and back.
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

    var live = {};       // id -> { el, remove, timer }
    var nextId = 1;

    // Fills a toast element for its kind, title and message, with the close button
    // and the timeout. Shared by a new toast and a replaced one, so replacing is
    // exactly showing again in the same place.
    function paint(entry, message, opts) {
        opts = opts || {};
        var el = entry.el;
        var kind = ICONS[opts.kind] ? opts.kind : 'info';
        var host = stack();

        // A failure interrupts; a confirmation waits its turn.
        host.setAttribute('aria-live', kind === 'danger' ? 'assertive' : 'polite');

        el.className = 'toast toast-' + kind;
        el.textContent = '';

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

        if (opts.dismissible !== false) {
            var close = document.createElement('button');
            close.type = 'button';
            close.className = 'toast-close';
            close.setAttribute('aria-label', opts.dismissLabel || ui._.config.toastDismissLabel || 'Dismiss');
            var x = document.createElement('i');
            x.className = 'ri-close-line';
            x.setAttribute('aria-hidden', 'true');
            close.appendChild(x);
            close.addEventListener('click', entry.remove);
            el.appendChild(close);
        }

        clearTimeout(entry.timer);
        // 0 means "stays until dismissed" — for a failure the user has to read, or for
        // work still running that will replace this toast when it ends.
        var ms = opts.timeout === undefined ? 4000 : opts.timeout;
        entry.timer = ms > 0 ? setTimeout(entry.remove, ms) : 0;
    }

    /**
     * message  the line to show; a plain string, inserted as text
     * opts     { kind: 'go'|'warn'|'danger'|'info', title, timeout, dismissible, dismissLabel }
     * returns  a function that removes this toast early; its `id` names the toast for
     *          toast.dismiss() and toast.replace()
     */
    ui.toast = function (message, opts) {
        var host = stack();
        var id = nextId++;
        var entry = { el: document.createElement('div'), timer: 0 };

        entry.remove = function () {
            clearTimeout(entry.timer);
            delete live[id];
            if (entry.el.parentNode) entry.el.parentNode.removeChild(entry.el);
            if (!host.children.length && host.parentNode) host.parentNode.removeChild(host);
        };
        entry.remove.id = id;
        live[id] = entry;

        paint(entry, message, opts);
        host.appendChild(entry.el);

        return entry.remove;
    };

    /* Shows a toast and returns its id — the form C# can hold. */
    ui.toast.show = function (message, opts) {
        return ui.toast(message, opts).id;
    };

    /* Removes a toast early. False when it has already gone, which is not an error:
       a timeout and a dismissal race whenever the work ends near the deadline. */
    ui.toast.dismiss = function (id) {
        var entry = live[id];
        if (!entry) return false;
        entry.remove();
        return true;
    };

    /* Replaces a toast's kind, title, message and timeout in place, keeping its
       position in the stack. When it has already gone, shows a new one instead — the
       outcome of the work still has to be reported — and returns that one's id. */
    ui.toast.replace = function (id, message, opts) {
        var entry = live[id];
        if (!entry) return ui.toast.show(message, opts);
        paint(entry, message, opts);
        return id;
    };

})(window.sednaUi);

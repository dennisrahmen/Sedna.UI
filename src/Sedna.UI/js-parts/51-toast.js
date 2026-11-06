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

   IN THE TOP LAYER, AND INSIDE AN OPEN MODAL. showModal() puts a <dialog> in the top
   layer, above every z-index, so a stack that is merely `position: fixed` paints under
   the dialog's backdrop — the failed save a dialog itself reports is never seen. The
   stack is therefore a manual popover, re-shown for every toast so it is promoted above
   whatever opened since. That alone is not enough: everything outside an open modal is
   inert, and inertness follows the DOM, not the paint order, so a stack painted over
   the dialog but outside it would be visible, unclickable and — being inert — never
   announced. So while a modal is open the stack is MOVED into the one on top, and
   back to <body> when it closes; 41-spotlight.js does the same with its bubble. The
   stack is the library's own node, so moving it disturbs nothing a framework rendered.

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
        info: 'ri-information-line',
        brand: 'ri-megaphone-line',
        secret: 'ri-lock-line'
    };

    var host = null;     // our stack, while it exists
    var unwatch = null;  // stops watching for the stack being taken out with a dialog
    var layer = ui._.layer;

    /* Moves the stack to where it can be seen and reached, promoted above everything
       already in the top layer (00-layer.js). `except` is a dialog that is closing and
       still matches :modal. */
    function raise(except) {
        if (!host) return;
        layer.raise(host, except);
        // A dialog a framework removes takes the stack with it, before any close event
        // can say so; then it comes back to wherever it can be seen.
        if (unwatch) unwatch();
        unwatch = layer.whenGone(host, function () { unwatch = null; if (host && host.children.length) raise(); });
    }

    function stack() {
        if (host && !host.isConnected && host.children.length) {
            raise();     // taken out with a dialog, toasts and all: put it back
            return host;
        }
        if (!host || !host.isConnected) {
            host = document.createElement('div');
            host.className = 'toast-stack';
            // The marker is what makes this OURS: only a stack the library created is
            // ever appended to, re-labelled, or removed.
            host.setAttribute('data-sedna-toasts', '');
            // The region is a status log, not a landmark to navigate to.
            host.setAttribute('role', 'status');
            host.setAttribute('aria-live', 'polite');
            host.setAttribute('popover', 'manual');
        }
        raise();
        return host;
    }

    function drop(el) {
        if (el.parentNode) el.parentNode.removeChild(el);
        if (host && !host.children.length) {
            if (unwatch) { unwatch(); unwatch = null; }
            if (host.parentNode) host.parentNode.removeChild(host);
            host = null;
        }
    }

    // A modal opening or closing moves a stack that is showing toasts with it.
    layer.onChange(function (closing) { if (host && host.children.length) raise(closing); });

    var live = {};       // id -> { el, remove, timer }
    var REGION_SETTLE_MS = 150;
    var nextId = 1;

    // Fills a toast element for its kind, title and message, with the close button
    // and the timeout. Shared by a new toast and a replaced one, so replacing is
    // exactly showing again in the same place.
    function paint(entry, message, opts) {
        opts = opts || {};
        var el = entry.el;
        var kind = ICONS[opts.kind] ? opts.kind : 'info';
        stack();

        // A failure interrupts; a confirmation waits its turn.
        host.setAttribute('aria-live', kind === 'danger' ? 'assertive' : 'polite');

        el.className = 'toast toast--' + kind;
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
        entry.timer = 0;
        // 0 means "stays until dismissed" — for a failure the user has to read, or for
        // work still running that will replace this toast when it ends.
        var ms = opts.timeout === undefined ? 4000 : opts.timeout;
        entry.left = ms;
        entry.started = Date.now();
        entry.timer = ms > 0 && !entry.held ? setTimeout(entry.remove, ms) : 0;
    }

    /* A toast does not run out while the reader is on it (WCAG 2.2.1): the pointer over
       it or the focus in it holds the clock, and leaving gives back what was left — at
       least a second, so it does not vanish the instant the pointer moves off. */
    function hold(entry) {
        if (entry.held) return;
        entry.held = true;
        if (!entry.timer) return;
        clearTimeout(entry.timer);
        entry.timer = 0;
        entry.left = Math.max(0, entry.left - (Date.now() - entry.started));
    }
    function release(entry) {
        if (!entry.held) return;
        entry.held = false;
        if (entry.left > 0 && !entry.timer) {
            entry.started = Date.now();
            entry.left = Math.max(entry.left, 1000);
            entry.timer = setTimeout(entry.remove, entry.left);
        }
    }

    /**
     * message  the line to show; a plain string, inserted as text
     * opts     { kind: 'go'|'warn'|'danger'|'info', title, timeout, dismissible, dismissLabel }
     * returns  a function that removes this toast early; its `id` names the toast for
     *          toast.dismiss() and toast.replace()
     */
    ui.toast = function (message, opts) {
        var id = nextId++;
        var entry = { el: document.createElement('div'), timer: 0, held: false, left: 0, started: 0 };
        entry.el.addEventListener('mouseenter', function () { hold(entry); });
        entry.el.addEventListener('mouseleave', function () { if (!entry.el.contains(document.activeElement)) release(entry); });
        entry.el.addEventListener('focusin', function () { hold(entry); });
        entry.el.addEventListener('focusout', function (e) { if (!entry.el.contains(e.relatedTarget) && !entry.el.matches(':hover')) release(entry); });

        entry.remove = function () {
            clearTimeout(entry.timer);
            delete live[id];
            drop(entry.el);
        };
        entry.remove.id = id;
        live[id] = entry;

        var fresh = !host || !host.isConnected;
        paint(entry, message, opts);
        // A live region announces what is ADDED to it, so it has to be in the page before
        // its first toast: a region created with its content already in it is read by many
        // screen readers as nothing. A new stack therefore takes its first toast a moment
        // after it appears; every later one goes in at once.
        if (fresh) {
            entry.el.hidden = true;
            host.appendChild(entry.el);
            setTimeout(function () { entry.el.hidden = false; }, REGION_SETTLE_MS);
        } else {
            host.appendChild(entry.el);
        }

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

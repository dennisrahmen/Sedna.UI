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
        info: 'ri-information-line'
    };

    var host = null;     // our stack, while it exists
    var opened = [];     // modal dialogs, in the order they opened — the top layer's order
    var watch = null;    // notices the dialog holding the stack being removed

    function isModal(el) {
        try { return !!el && el.isConnected && el.matches('dialog:modal'); } catch (e) { return false; }
    }

    /* The modal the stack belongs in, or null for <body>. The last one opened is the one
       on top; one opened before this script ran is not in `opened`, so document order,
       where a nested dialog follows its parent, stands in for it. */
    function topModal(except) {
        for (var i = opened.length - 1; i >= 0; i--) {
            if (opened[i] !== except && isModal(opened[i])) return opened[i];
        }
        var all;
        try { all = document.querySelectorAll('dialog:modal'); } catch (e) { return null; }
        for (var j = all.length - 1; j >= 0; j--) if (all[j] !== except) return all[j];
        return null;
    }

    /* Moves the stack to where it can be seen and reached, and promotes it above
       everything already in the top layer. `except` is a dialog that is closing and
       still matches :modal. */
    function raise(except) {
        if (!host) return;
        var parent = topModal(except) || document.body;
        if (host.parentNode !== parent) {
            // Moving a node blurs whatever inside it had focus — a toast's close button.
            var focused = host.contains(document.activeElement) ? document.activeElement : null;
            parent.appendChild(host);
            if (focused && focused !== document.activeElement) {
                try { focused.focus({ preventScroll: true }); } catch (e) { /* refused */ }
            }
        }
        try {
            if (host.matches(':popover-open')) host.hidePopover();
            host.showPopover();
        } catch (e) { /* no popover support: z-index 600 is the fallback */ }
        follow(parent !== document.body ? parent : null);
    }

    /* A dialog a framework removes takes the stack with it, before any close event can
       say so. While the stack is in one, a removal brings it back to <body>. */
    function follow(dialog) {
        if (watch) { watch.disconnect(); watch = null; }
        if (!dialog) return;
        try {
            watch = new MutationObserver(function () {
                if (host && !host.isConnected) raise();
            });
            watch.observe(document.body, { childList: true, subtree: true });
        } catch (e) { /* no observer: the next toast finds it */ }
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
            follow(null);
            if (host.parentNode) host.parentNode.removeChild(host);
            host = null;
        }
    }

    /* Capture, because none of these bubble, and from the document because a dialog is
       a node a framework replaces like any other. `beforetoggle` is the one that fires
       while a closing dialog is still displayed, so the stack leaves before it hides. */
    function track(e) {
        var dialog = e.target;
        if (!(dialog instanceof HTMLDialogElement)) return;
        var closing = e.type === 'close' || e.newState === 'closed';
        var at = opened.indexOf(dialog);
        if (at >= 0) opened.splice(at, 1);
        if (!closing && isModal(dialog)) opened.push(dialog);
        if (host && host.children.length) raise(closing ? dialog : null);
    }
    document.addEventListener('beforetoggle', track, true);
    document.addEventListener('toggle', track, true);
    document.addEventListener('close', track, true);

    var live = {};       // id -> { el, remove, timer }
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
        var id = nextId++;
        var entry = { el: document.createElement('div'), timer: 0 };

        entry.remove = function () {
            clearTimeout(entry.timer);
            delete live[id];
            drop(entry.el);
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

/* ── The top layer, for what the library draws itself ────────────────────────
   showModal() puts a <dialog> in the top layer, above every z-index, and makes
   everything outside it inert — and inertness follows the DOM, not the paint order.
   So a node the library draws (the toast stack, the hover-hint bubble) has to be
   INSIDE the modal on top to be seen, reached and announced, and promoted above it.

     ui._.layer.raise(node, except)   moves `node` into the topmost open modal, or to
                                      <body>, and shows it as a manual popover again so
                                      it is above whatever opened since. `except` is a
                                      dialog that is closing and still matches :modal.
     ui._.layer.onChange(fn)          calls fn(closingDialog | null) whenever a modal
                                      dialog opens or closes.
     ui._.layer.whenGone(el, fn)      calls fn once, when `el` leaves the document — a
                                      trigger a render removed while its hint or its
                                      panel was showing. Returns a function that stops
                                      watching.

   Only nodes the library created go through raise(): moving a node a framework
   rendered would fight its renderer. 41-spotlight.js keeps its own, richer handling
   because its hole and bubble follow a target the app wrote.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var opened = [];     // modal dialogs, in the order they opened — the top layer's order
    var listeners = [];

    function isModal(el) {
        try { return !!el && el.isConnected && el.matches('dialog:modal'); } catch (e) { return false; }
    }

    /* The modal on top, or null. The last one opened is on top; one opened before this
       script ran is not in `opened`, so document order — where a nested dialog follows
       its parent — stands in for it. */
    function topModal(except) {
        for (var i = opened.length - 1; i >= 0; i--) {
            if (opened[i] !== except && isModal(opened[i])) return opened[i];
        }
        var all;
        try { all = document.querySelectorAll('dialog:modal'); } catch (e) { return null; }
        for (var j = all.length - 1; j >= 0; j--) if (all[j] !== except) return all[j];
        return null;
    }

    function raise(node, except) {
        if (!node) return;
        var parent = topModal(except) || document.body;
        if (!parent) return;
        if (node.parentNode !== parent) {
            // Moving a node blurs whatever inside it had focus — a toast's close button.
            var focused = node.contains(document.activeElement) ? document.activeElement : null;
            parent.appendChild(node);
            if (focused && focused !== document.activeElement) {
                try { focused.focus({ preventScroll: true }); } catch (e) { /* refused */ }
            }
        }
        if (!node.hasAttribute('popover')) node.setAttribute('popover', 'manual');
        try {
            if (node.matches(':popover-open')) node.hidePopover();
            node.showPopover();
        } catch (e) { /* no popover support: its z-index is the fallback */ }
    }

    /* Capture, because none of these bubble, and from the document because a dialog is
       a node a framework replaces like any other. `beforetoggle` fires while a closing
       dialog is still displayed, so what sits in it can leave before it hides. */
    function track(e) {
        var dialog = e.target;
        if (!(dialog instanceof HTMLDialogElement)) return;
        var closing = e.type === 'close' || e.newState === 'closed';
        var at = opened.indexOf(dialog);
        if (at >= 0) opened.splice(at, 1);
        if (!closing && isModal(dialog)) opened.push(dialog);
        for (var i = 0; i < listeners.length; i++) {
            try { listeners[i](closing ? dialog : null); } catch (err) { /* one listener must not stop the rest */ }
        }
    }
    document.addEventListener('beforetoggle', track, true);
    document.addEventListener('toggle', track, true);
    document.addEventListener('close', track, true);

    // One observer for every watched element, alive only while something is watched.
    var watched = [];
    var observer = null;
    function check() {
        var gone = watched.filter(function (w) { return !w.el.isConnected; });
        if (!gone.length) return;
        watched = watched.filter(function (w) { return w.el.isConnected; });
        if (!watched.length && observer) { observer.disconnect(); observer = null; }
        gone.forEach(function (w) { try { w.fn(); } catch (e) { /* the next one still runs */ } });
    }
    function whenGone(el, fn) {
        if (!el || typeof MutationObserver !== 'function') return function () { };
        var entry = { el: el, fn: fn };
        watched.push(entry);
        if (!observer) {
            observer = new MutationObserver(check);
            observer.observe(document.documentElement, { childList: true, subtree: true });
        }
        return function () {
            watched = watched.filter(function (w) { return w !== entry; });
            if (!watched.length && observer) { observer.disconnect(); observer = null; }
        };
    }

    ui._.layer = {
        topModal: topModal,
        raise: raise,
        onChange: function (fn) { listeners.push(fn); },
        whenGone: whenGone
    };
})(window.sednaUi);

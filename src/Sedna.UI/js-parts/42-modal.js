/* ── The dialog presenter ────────────────────────────────────────────────────
   await sednaUi.modal.show(id)    → dialog.showModal(); resolves with returnValue
   sednaUi.modal.close(id, value)  → dialog.close(value)
   await sednaUi.modal.idle(id)    → resolves once a closed dialog's transition ends

   The presenter for markup the app wrote: a `.modal`, a `.drawer` or a `.sheet`,
   each a <dialog>.

   NOT MODAL — data-modeless. A drawer or a sheet that carries `data-modeless` is
   opened with show() instead: no backdrop, nothing behind it made inert and no focus
   trap, so the page stays usable while it is open — a filter panel the reader keeps
   beside the list it filters. Everything else is the same: show() resolves with its
   returnValue when it closes, and Escape with the focus inside it closes it, which the
   platform does only for a modal one. The platform gives the top layer, a focus trap, Escape-to-close
   and inert content behind it — all four, none reachable from Blazor without
   IJSRuntime, which the consuming rules say an app should not inject. An app that
   followed those rules otherwise fell back to a `.modal-backdrop` div behind an
   `@if` and lost all four.

   A TOGGLE KEPT IN STEP. A control that carries `aria-controls` naming a dialog and an
   `aria-expanded` of its own — the button that opens and closes a docked assistant —
   has its `aria-expanded` set from the dialog's `open` whenever that changes, by
   whatever route: the toggle, the dialog's own close button, Escape, the app.

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

   ASKING BEFORE IT CLOSES — data-close-guard. A dialog holding unsaved work does
   not close on a close request — Escape, a swipe down on a `.sheet`, a
   command="request-close" button. It raises `cancel`, the app asks, and the app
   closes it. In Blazor that is one attribute and one handler:

     <dialog id="reply" class="modal" data-close-guard @oncancel="Dismiss">

   The script refuses the `cancel`, not the app, because Razor cannot: it writes
   `@oncancel:preventDefault` out as a literal attribute, and the render throws.

   Refusing is not enough on its own either. Chromium lets a page refuse a close
   request only so often without a click or a keypress in between — Escape is not
   one — and then fires a `cancel` it cannot cancel and closes the dialog anyway:
   five Escapes in a row through a "discard this?" dialog closed the form under it.
   On a guarded dialog Escape becomes requestClose(), whose `cancel` is always
   cancelable, so the app is asked every time. Escape another handler already took
   — a combo, a drag — and Escape meant for an open popover are left alone, exactly
   as the platform would leave them.

   ASKING BEFORE THE PAGE IS LEFT — data-close-guard with data-dirty. A form, or any
   element, that carries both holds work that is not saved, and closing the tab,
   reloading or following a link out of the app raises the browser's own "leave
   this page?" prompt. Whether the work is unsaved is the app's to say: it writes
   `data-dirty` while what is on screen differs from what was saved and takes it off
   when it saves or discards, and the guard is answered from the attribute as it
   stands when the page is left — nothing is tracked here. In Blazor:

     <form data-close-guard data-dirty="@_dirty" @onsubmit="Save">

   The prompt's words are the browser's; no page can set them. A navigation inside a
   Blazor app never unloads the page, so it raises no prompt: that is NavigationLock's
   OnBeforeInternalNavigation, which asks with the app's own dialog.
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
                try { if (d.hasAttribute('data-modeless')) d.show(); else d.showModal(); }
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

            // An engine without `overlay` has no closing transition of its own;
            // 42-modal-exit.js plays it, and starts it here so it can be waited for.
            if (ui._.slideOut) ui._.slideOut(d);
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

    /* The guarded dialog this Escape would close, or null. Focus is inside the top
       modal dialog, since everything else is inert — unless a re-render removed the
       focused control and left focus on the body. With one modal open that one is
       still the one Escape closes; with more, which is on top is not readable, and the
       platform is left to it. */
    function guardedTarget(target) {
        var d = target instanceof Element ? target.closest('dialog') : null;
        if (!d) {
            var open = document.querySelectorAll('dialog:modal');
            d = open.length === 1 ? open[0] : null;
        }
        return d && d.hasAttribute('data-close-guard') && d.matches(':modal') &&
            typeof d.requestClose === 'function' ? d : null;
    }

    // Bubbling, so a combo or a drag that handles its own Escape has cancelled it first.
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || e.defaultPrevented || e.isComposing) return;
        if (document.querySelector(':popover-open:not([popover="manual"])')) return;

        var d = guardedTarget(e.target);
        if (!d) return;

        e.preventDefault();
        d.requestClose();
    });

    /* Escape inside a dialog opened without a modal closes it, as it closes a modal one:
       a close request, so a guarded one asks first. */
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || e.defaultPrevented || e.isComposing) return;
        var d = e.target instanceof Element ? e.target.closest('dialog[data-modeless][open]') : null;
        if (!d || d.matches(':modal')) return;
        if (document.querySelector(':popover-open:not([popover="manual"])')) return;
        e.preventDefault();
        if (typeof d.requestClose === 'function') d.requestClose(); else d.close();
    });

    // A toggle's aria-expanded follows the dialog it controls.
    function syncToggles(d) {
        if (!d.id) return;
        var toggles = document.querySelectorAll('[aria-controls][aria-expanded]');
        for (var i = 0; i < toggles.length; i++) {
            var names = (toggles[i].getAttribute('aria-controls') || '').split(/\s+/);
            if (names.indexOf(d.id) < 0) continue;
            var value = d.open ? 'true' : 'false';
            if (toggles[i].getAttribute('aria-expanded') !== value) toggles[i].setAttribute('aria-expanded', value);
        }
    }
    if (typeof MutationObserver === 'function') {
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                if (records[i].target.tagName === 'DIALOG') syncToggles(records[i].target);
            }
        }).observe(document.documentElement, { attributes: true, attributeFilter: ['open'], subtree: true });
    }

    // `cancel` does not bubble, so it is caught on the way down.
    document.addEventListener('cancel', function (e) {
        var d = e.target;
        if (d instanceof Element && d.tagName === 'DIALOG' && d.hasAttribute('data-close-guard')) {
            e.preventDefault();
        }
    }, true);

    // preventDefault() is the standard ask; a returnValue is what older engines read.
    window.addEventListener('beforeunload', function (e) {
        if (!document.querySelector('[data-close-guard][data-dirty]')) return;
        e.preventDefault();
        e.returnValue = true;
    });

})(window.sednaUi);

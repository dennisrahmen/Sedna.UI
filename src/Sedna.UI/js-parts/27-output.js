/* ── Output pane, follow-tail ─────────────────────────────────────────────────
   Opt-in wiring for .output, so a pane that is being appended to stays on the newest
   line:

     <ul class="output" data-follow>…</ul>

   Following is a mode, not an action. The pane sticks to the bottom while the reader
   is at the bottom, releases the moment they scroll up to read something, and
   re-attaches when they scroll back down. Without the release, reading anything in a
   live stream is impossible; without the re-attach, it never resumes and the reader
   has to reload.

   "At the bottom" is measured with a tolerance, because scrollHeight, clientHeight and
   scrollTop are fractional on a scaled display and an exact comparison is false as
   often as it is true.

   A MutationObserver rather than a call the app makes after each append: the app is
   Blazor, and the lines arrive from a render rather than from code that could call
   anything. One observer per pane, created the first time the pane is seen.

   The app owns whether a pane follows at all: `data-follow` is the switch, so a Pause
   button is the app rendering the pane without it, and rendering it again jumps to the
   newest line. The script owns only where the reader has scrolled, and says so twice:

     data-follow-paused   on the pane while the reader has scrolled away from the
                          newest line — .output-jump reads it, so a "jump to latest"
                          button appears with no app code at all
     sedna-follow         an event on the pane when that changes, bubbling, with
                          `detail.following` — for an app that counts the lines the
                          reader has not seen yet

   Two more things hold the pane still, because both mean the reader is busy with a
   line: opening a row's <details> releases following when the row grew past the
   bottom, and an open menu in the pane keeps new lines from scrolling it out from
   under the pointer.

   `data-output-follow` on a button scrolls its pane back down and follows again. The
   pane is the one `aria-controls` names, or the `.output` in the button's
   `.output-panel`.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var STATE = '_sednaFollow';
    var TOLERANCE = 4;
    var PAUSED = 'data-follow-paused';

    function atBottom(pane) {
        return pane.scrollHeight - pane.clientHeight - pane.scrollTop <= TOLERANCE;
    }

    function toBottom(pane) {
        pane.scrollTop = pane.scrollHeight;
    }

    function menuOpen(pane) {
        return !!pane.querySelector('[data-menu-toggle][aria-expanded="true"]');
    }

    /* The one place the mode changes, so the mark and the event cannot disagree. A pane
       the app has switched off is never marked: there is nothing to jump back to. */
    function setFollowing(pane, following) {
        var state = pane[STATE];
        if (!state) return;
        var enabled = pane.hasAttribute('data-follow');
        var changed = state.following !== following;
        state.following = following;
        pane.toggleAttribute(PAUSED, enabled && !following);
        if (!changed || !enabled) return;
        try {
            pane.dispatchEvent(new CustomEvent('sedna-follow', {
                bubbles: true,
                detail: { following: following }
            }));
        } catch (e) { /* an old engine without CustomEvent: the mark still says it */ }
    }

    function bind(pane) {
        if (pane[STATE]) return;

        // Starts attached, so a pane rendered with history already in it opens on the
        // newest line rather than the oldest.
        pane[STATE] = { following: true };
        toBottom(pane);

        pane.addEventListener('scroll', function () { setFollowing(pane, atBottom(pane)); });

        // `toggle` does not bubble, so it is caught on the way down.
        pane.addEventListener('toggle', function (e) {
            if (e.target && e.target.tagName === 'DETAILS' && e.target.open) setFollowing(pane, atBottom(pane));
        }, true);

        var observer = new MutationObserver(function () {
            if (!pane.hasAttribute('data-follow') || !pane[STATE].following || menuOpen(pane)) return;
            toBottom(pane);
        });
        observer.observe(pane, { childList: true, subtree: true, characterData: true });
    }

    function follow(pane) {
        if (!pane) return;
        bind(pane);
        toBottom(pane);
        setFollowing(pane, true);
    }

    ui.output = {
        /* Scrolls a pane to its newest line and re-attaches following. For a "jump to
           latest" button, and for an app that appends outside the DOM the observer
           watches. */
        follow: follow,

        /* Whether the reader is on the newest line — for showing that button only when
           it would do something. */
        isFollowing: function (pane) { return !!pane && atBottom(pane); }
    };

    function paneFor(button) {
        var id = button.getAttribute('aria-controls');
        if (id) return document.getElementById(id);
        var panel = button.closest('.output-panel');
        return panel ? panel.querySelector('.output') : null;
    }

    document.addEventListener('click', function (e) {
        var button = e.target && e.target.closest ? e.target.closest('[data-output-follow]') : null;
        if (!button) return;
        var pane = paneFor(button);
        if (!pane) return;
        e.preventDefault();
        follow(pane);
    });

    function bindAll(root) {
        var panes = (root || document).querySelectorAll('.output[data-follow]');
        for (var i = 0; i < panes.length; i++) bind(panes[i]);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { bindAll(document); });
    } else {
        bindAll(document);
    }

    // A pane rendered later — by a Blazor render, by a modal opening — is picked up
    // here. Watching the document for added panes is the same delegation the rest of
    // this script uses, expressed the only way a scroll container allows. The same
    // observer sees the app switch `data-follow` off and on again.
    new MutationObserver(function (records) {
        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (record.type === 'attributes') {
                var pane = record.target;
                if (!pane.matches || !pane.matches('.output')) continue;
                var on = pane.hasAttribute('data-follow');
                // A value rewritten with the switch still on is not the app resuming.
                if (on && record.oldValue !== null) continue;
                if (on) follow(pane);
                else pane.removeAttribute(PAUSED);
                continue;
            }
            var added = record.addedNodes;
            for (var j = 0; j < added.length; j++) {
                if (added[j].nodeType !== 1) continue;
                if (added[j].matches && added[j].matches('.output[data-follow]')) bind(added[j]);
                bindAll(added[j]);
            }
        }
    }).observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeOldValue: true,
        attributeFilter: ['data-follow']
    });

})(window.sednaUi);

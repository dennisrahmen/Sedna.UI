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
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var BOUND = '_drFollowBound';
    var TOLERANCE = 4;

    function atBottom(pane) {
        return pane.scrollHeight - pane.clientHeight - pane.scrollTop <= TOLERANCE;
    }

    function toBottom(pane) {
        pane.scrollTop = pane.scrollHeight;
    }

    function bind(pane) {
        if (pane[BOUND]) return;
        pane[BOUND] = true;

        // Starts attached, so a pane rendered with history already in it opens on the
        // newest line rather than the oldest.
        var following = true;
        toBottom(pane);

        pane.addEventListener('scroll', function () { following = atBottom(pane); });

        var observer = new MutationObserver(function () {
            if (following) toBottom(pane);
        });
        observer.observe(pane, { childList: true, subtree: true, characterData: true });
    }

    ui.output = {
        /* Scrolls a pane to its newest line and re-attaches following. For a "jump to
           latest" button, and for an app that appends outside the DOM the observer
           watches. */
        follow: function (pane) {
            if (!pane) return;
            bind(pane);
            toBottom(pane);
        },

        /* Whether the reader is on the newest line — for showing that button only when
           it would do something. */
        isFollowing: function (pane) { return !!pane && atBottom(pane); }
    };

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
    // this script uses, expressed the only way a scroll container allows.
    new MutationObserver(function (records) {
        for (var i = 0; i < records.length; i++) {
            var added = records[i].addedNodes;
            for (var j = 0; j < added.length; j++) {
                if (added[j].nodeType !== 1) continue;
                if (added[j].matches && added[j].matches('.output[data-follow]')) bind(added[j]);
                bindAll(added[j]);
            }
        }
    }).observe(document.documentElement, { childList: true, subtree: true });

})(window.sednaUi);

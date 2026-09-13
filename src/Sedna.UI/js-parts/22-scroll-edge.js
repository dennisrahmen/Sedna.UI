/* ── Scroll edges, for engines without scroll-state() ─────────────────────────
   A pinned table column paints an edge shadow only while there is a column
   scrolled under it. The stylesheet decides that with a scroll-state container
   query on `.sedna-scroll-x`, which WebKit does not have — so on an iPad a table
   that fits still drew the shadow, as though there were something to scroll to.

   This writes the same two facts as attributes on every `.sedna-scroll-x`:

     data-scroll-start   the scroller is at its inline start (or does not scroll)
     data-scroll-end     the scroller is at its inline end (or does not scroll)

   and 59-table-extensions.css reads them beside the container query. Where the
   query works the two agree; where it does not, the attributes are the only signal.
   They are attributes an app never renders, so a Blazor re-render neither reverts
   them nor fights over them — the observer below puts them back on a node that was
   replaced. Nothing here is a public member: the attributes are the whole contract.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function mark(el) {
        var max = el.scrollWidth - el.clientWidth;
        // RTL scrolls into negative values in every current engine; the distance
        // from the start is the magnitude either way.
        var pos = Math.abs(el.scrollLeft);
        el.toggleAttribute('data-scroll-start', max <= 1 || pos <= 1);
        el.toggleAttribute('data-scroll-end', max <= 1 || pos >= max - 1);
    }

    function markAll(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var all = scope.querySelectorAll('.sedna-scroll-x');
        for (var i = 0; i < all.length; i++) mark(all[i]);
        if (scope !== document && scope.classList && scope.classList.contains('sedna-scroll-x')) mark(scope);
    }

    /* Capture, because `scroll` does not bubble. */
    document.addEventListener('scroll', function (e) {
        var el = e.target;
        if (el && el.classList && el.classList.contains('sedna-scroll-x')) mark(el);
    }, true);

    window.addEventListener('resize', function () { markAll(); });

    /* Content rendered after load — a table a Blazor page adds, or a re-render that
       replaced the scroller — is marked when it lands. Childlist only: the marks
       themselves are attribute changes, and watching those would loop. */
    function watch() {
        markAll();
        if (!('MutationObserver' in window)) return;
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    if (added[j].nodeType === 1) markAll(added[j]);
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();

})(window.sednaUi);

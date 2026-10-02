/* ── Mochi out of view ────────────────────────────────────────────────────────
   Mochi is CSS and needs no script; this part only saves the work a Mochi nobody can
   see would cost. A browser keeps running the loops of a drawing that is off screen —
   breathing, blinking, a wave — and every step repaints its clay shading, so a page
   with Mochi all the way down spent most of every frame on drawings below the fold.

   Each `.mochi` is watched as it lands. While it is more than a little way out of the
   viewport it carries

     data-mochi-away     68-mochi.css holds every loop where it is, and does not
                         render the drawing at all

   and the attribute goes again as it comes back, so the loops pick up where they
   stopped. It is an attribute an app never renders, so a Blazor re-render leaves it be,
   and a Mochi the render replaced is watched afresh. Nothing here is a public member.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    if (typeof IntersectionObserver !== 'function') return;

    var seen = new IntersectionObserver(function (entries) {
        for (var i = 0; i < entries.length; i++) {
            entries[i].target.toggleAttribute('data-mochi-away', !entries[i].isIntersecting);
        }
    }, { rootMargin: '120px' });

    function within(root, fn) {
        if (root.classList && root.classList.contains('mochi')) fn(root);
        if (!root.querySelectorAll) return;
        var all = root.querySelectorAll('.mochi');
        for (var i = 0; i < all.length; i++) fn(all[i]);
    }

    function watch(el) {
        if (el.__sednaMochiSeen) return;
        el.__sednaMochiSeen = true;
        seen.observe(el);
    }

    function forget(el) {
        if (!el.__sednaMochiSeen || el.isConnected) return;
        el.__sednaMochiSeen = false;
        seen.unobserve(el);
    }

    function start() {
        within(document, watch);
        if (typeof MutationObserver !== 'function') return;
        // Childlist only: the mark is an attribute, and watching attributes would loop.
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes, removed = records[i].removedNodes;
                for (var j = 0; j < added.length; j++) if (added[j].nodeType === 1) within(added[j], watch);
                for (var k = 0; k < removed.length; k++) if (removed[k].nodeType === 1) within(removed[k], forget);
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();

})(window.sednaUi);

/* ── Spotlight positioning ───────────────────────────────────────────────────
   `.spotlight-hole` dims the page except one box. The box is what only the browser
   knows, so this measures a target and writes the four values onto the hole:

     sednaUi.spotlight.at(hole, target)
     sednaUi.spotlight.at(hole, target, { pad: 6 })

   The steps, the copy and the order are the app's. This is deliberately not a tour:
   a tour is a sequence with its own state, and a library that owned it would also own
   what "next" means, whether a step can be skipped, and where the bubble goes — all
   of which differ per app.

   The hole is positioned against its offset parent, which has to be positioned. Pass
   the tip too and it is placed under the hole, flipped above when there is no room.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function offsetParentRect(el) {
        var parent = el.offsetParent || document.body;
        return parent.getBoundingClientRect();
    }

    ui.spotlight = {
        /* Puts `hole` over `target`. `pad` grows the hole beyond the target so the
           ring does not sit on its edge; it defaults to 4px. Returns the rectangle
           used, in the hole's own coordinate space, so an app can place a bubble
           without measuring twice. */
        at: function (hole, target, options) {
            if (!hole || !target) return null;

            var pad = (options && typeof options.pad === 'number') ? options.pad : 4;
            var box = target.getBoundingClientRect();
            var origin = offsetParentRect(hole);

            var rect = {
                top: box.top - origin.top - pad,
                left: box.left - origin.left - pad,
                width: box.width + pad * 2,
                height: box.height + pad * 2
            };

            hole.style.top = rect.top + 'px';
            hole.style.left = rect.left + 'px';
            hole.style.width = rect.width + 'px';
            hole.style.height = rect.height + 'px';
            // The ring follows the target's own rounding, so a pill-shaped button is
            // not highlighted with a rectangle.
            hole.style.borderRadius = getComputedStyle(target).borderRadius;

            return rect;
        },

        /* Places `tip` under the rectangle `at()` returned, or above it when the
           viewport has no room below. */
        tipAt: function (tip, rect, gap) {
            if (!tip || !rect) return;

            var space = gap === undefined ? 12 : gap;
            tip.style.left = rect.left + 'px';
            tip.style.top = (rect.top + rect.height + space) + 'px';

            var below = tip.getBoundingClientRect();
            if (below.bottom > window.innerHeight) {
                tip.style.top = (rect.top - below.height - space) + 'px';
            }
        }
    };

})(window.sednaUi);

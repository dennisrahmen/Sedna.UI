/* ── Anchored panels close when the page moves under them ────────────────────
   CSS anchor positioning places `.menu` and `.popover` against the control that
   opened them, with no measuring in JavaScript. That is the right trade and it
   stays — but Chromium computes an anchored `position: fixed` panel's offset when
   the panel becomes VISIBLE and does not recompute it while the anchor scrolls.
   The panel is pinned to the viewport, the trigger travels out from under it, and
   the gap grows by exactly the scroll distance and stays there. Measured on the
   catalogue's own pages: a `.menu` 4px under its trigger sat 154px under it after
   a 150px scroll, and scrolling back overshot to −146px. Hiding and re-showing the
   panel puts it right, which is what identifies the cause.

   So the panel closes instead. A dropdown whose trigger has scrolled away is
   stale rather than merely misplaced, and closing it is what a platform menu
   does — which is why this is the fix rather than repositioning in JavaScript.
   Nothing here measures anything or writes a coordinate: the CSS is still the
   whole positioning model, and it is right every time a panel opens.

   The collapsed rail's flyout (12-frame-collapsed-rail.css) has the same mechanism
   and no fix: it is a CSS hover state with no open/closed to toggle. It is also
   the mildest case — the pointer has to stay on the rail item for the flyout to
   exist at all, and leaving closes it.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    /* The scrolled node as an element. A document-level scroll reports the
       document, which is not one. */
    function scrolledElement(target) {
        if (target === document || target === window) return document.documentElement;
        return target && target.nodeType === 1 ? target : null;
    }

    /* One rule for both panels: close it when the scroll actually moved its
       trigger, and never when the scroll came from inside the panel — a long menu
       is itself a scroller, and so is a `.sedna-scroll` within one. */
    function stale(scroller, panel, trigger) {
        if (!scroller || !trigger) return false;
        if (panel === scroller || panel.contains(scroller)) return false;
        return scroller !== trigger && scroller.contains(trigger);
    }

    function closeMenus(scroller) {
        var toggle = document.querySelector('[data-menu-toggle][aria-expanded="true"]');
        if (!toggle) return;

        var anchor = toggle.closest('.menu-anchor');
        var panel = anchor ? anchor.querySelector('.menu') : null;
        if (panel && stale(scroller, panel, toggle)) ui.menu.closeAll();
    }

    function closePopovers(scroller) {
        var open;
        /* :popover-open is the platform's own state and the only honest way to ask.
           Guarded because a selector a browser does not know throws rather than
           matching nothing. */
        try { open = document.querySelectorAll('.popover:popover-open'); }
        catch (err) { return; }

        for (var i = 0; i < open.length; i++) {
            var panel = open[i];
            /* `popovertarget` IS the anchor — 65-popover.css requires opening a
               popover from one, which is also what makes the invoker the implicit
               anchor position-area places against. So the same test applies, with
               no need for an API that exposes the invoker. */
            var trigger = panel.id
                ? document.querySelector('[popovertarget="' + panel.id + '"]')
                : null;

            if (stale(scroller, panel, trigger)) {
                try { panel.hidePopover(); } catch (err2) { /* already closing */ }
            }
        }
    }

    /* Capture, because `scroll` does not bubble: without it a scroll on anything
       but the document goes unheard, and the frame's own `.page` is a container.
       Delegated from document, so a panel rendered by a later Blazor render is
       covered with nothing re-bound.

       Both branches open with one querySelector that finds nothing, which is the
       case on every scroll event but a handful. */
    document.addEventListener('scroll', function (e) {
        var scroller = scrolledElement(e.target);
        if (!scroller) return;
        closeMenus(scroller);
        closePopovers(scroller);
    }, true);

})(window.sednaUi);

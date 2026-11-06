/* ── A stuck viewport on iOS ─────────────────────────────────────────────────
   Safari on iOS 26 can leave the visual viewport offset after the page has scrolled to
   its end and back, or after the keyboard has closed: `visualViewport.offsetTop` stays
   above zero, and every fixed and sticky element — the bottom bar, a floating button, the
   sticky header of `.layout--flow` — is drawn that far from where it belongs, in the
   middle of the content. It is WebKit's fault and an open bug there; nothing in the
   stylesheet causes or can undo it.

   The visual viewport can also stay shorter than the window after the keyboard has
   gone, which shifts the same elements the same way. Either is the fault.

   Scrolling the document by a pixel and back makes Safari compute the viewport again —
   confirmed on an iPhone by an app that ships this nudge itself.
   This does that, once per occurrence, and only where it can be this fault: a page in
   `.layout--flow`, at phone width, with the visual viewport still offset after things
   settled, nothing focused that has a keyboard, and no pinch zoom — a reader who zoomed
   in has offset the viewport on purpose. Anywhere else it does nothing. */
(function () {

    var timer = 0, nudged = false;

    function typing() {
        var a = document.activeElement;
        return !!a && (a.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(a.tagName));
    }

    function check() {
        var vv = window.visualViewport;
        if (!vv || window.innerWidth > 560 || !document.querySelector('.layout--flow')) return;
        var shifted = vv.offsetTop > 0.5 || window.innerHeight - vv.height > 1;
        if (!shifted) { nudged = false; return; }
        if (nudged || typing() || Math.abs(vv.scale - 1) > 0.01) return;
        nudged = true;
        window.scrollBy(0, 1);
        // A timer and not a frame: a frame waits on a page that is not being painted.
        setTimeout(function () { window.scrollBy(0, -1); }, 50);
    }

    function settle() {
        clearTimeout(timer);
        timer = setTimeout(check, 150);
    }

    window.addEventListener('scroll', settle, { passive: true });
    window.addEventListener('resize', settle);
    document.addEventListener('focusout', settle);
    if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', settle);
        window.visualViewport.addEventListener('scroll', settle);
    }

})();

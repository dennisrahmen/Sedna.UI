/* ── Same-page fragment links ─────────────────────────────────────────────────
   A link written as a bare fragment — `href="#main"`, `href="#email"` — whose
   target is on the page moves the reader there: the target is scrolled into view
   and focused, which is what a browser does on a page with no <base>. Two things
   every Blazor app has break that:

   - `<base href="/">`, which the host page requires. A bare fragment resolves
     against the base, so "#main" on /orders means /#main — another page — and the
     router goes there. A skip link took the reader to the start page.
   - Blazor's handling of a same-page fragment, for a link that does carry the
     path. It scrolls and never moves focus, so a skip link skipped nothing and a
     validation summary's link left focus where it was.

   Cancelling the click stops both: Blazor ignores a click something has already
   cancelled. The address does not change, so a link meant to be copied carries the
   page's path and stays the router's.

   A target that cannot take focus gets tabindex="-1" until it loses it — the only
   way script has to move the point the next Tab starts from.

   `#top`, with no element of that id, is the top of the page, as it is to a
   browser: the scroller the link is in — the frame's `.page`, a `.sedna-scroll`, or
   the document — goes back to its start, and focus moves to that scroller, so the
   next Tab is the first control in it. A `.back-to-top` button is written that way.

   After every other delegated part, and a click one of them cancelled is left
   alone, so a tab or a menu item that is also a fragment link keeps its own
   behaviour. No member: the markup is the whole API. */
(function (ui) {
    // The element that scrolls `el`: its nearest ancestor whose overflow scrolls on
    // the block axis, or null for the document. 29-spy.js observes against the same.
    function scrollerOf(el) {
        for (var p = el.parentElement; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
            var o = getComputedStyle(p).overflowY;
            if (o === 'auto' || o === 'scroll') return p;
        }
        return null;
    }
    ui._.scrollerOf = scrollerOf;

    // An element, `{ top: scroller }` for the top of a page, or null to leave the
    // click to the browser.
    function targetOf(link) {
        var href = link.getAttribute('href');
        if (!href || href.length < 2 || href.charAt(0) !== '#') return null;
        var id;
        try {
            id = decodeURIComponent(href.slice(1));
        } catch (e) {
            return null;
        }
        var target = document.getElementById(id);
        if (target || id.toLowerCase() !== 'top') return target;
        return { top: scrollerOf(link) };
    }

    function focusOn(target) {
        target.focus({ preventScroll: true });
        if (document.activeElement === target || target.hasAttribute('tabindex')) return;

        target.setAttribute('tabindex', '-1');
        target.addEventListener('blur', function () {
            target.removeAttribute('tabindex');
        }, { once: true });
        target.focus({ preventScroll: true });
    }

    function moveTo(target) {
        if (target.nodeType !== 1) {
            var scroller = target.top;
            if (scroller) scroller.scrollTop = 0;
            else window.scrollTo(0, 0);
            focusOn(scroller || document.querySelector('main') || document.body);
            return;
        }
        target.scrollIntoView();
        focusOn(target);
    }

    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0) return;
        if (e.ctrlKey || e.shiftKey || e.altKey || e.metaKey) return;
        if (!(e.target instanceof Element)) return;

        var link = e.target.closest('a[href^="#"]');
        if (!link || link.hasAttribute('download')) return;
        var frame = link.getAttribute('target');
        if (frame && frame !== '_self') return;

        var target = targetOf(link);
        if (!target) return;

        e.preventDefault();
        moveTo(target);
    });
})(window.sednaUi);

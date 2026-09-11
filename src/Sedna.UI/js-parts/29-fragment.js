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

   After every other delegated part, and a click one of them cancelled is left
   alone, so a tab or a menu item that is also a fragment link keeps its own
   behaviour. No member: the markup is the whole API. */
(function (ui) {
    function targetOf(link) {
        var href = link.getAttribute('href');
        if (!href || href.length < 2 || href.charAt(0) !== '#') return null;
        try {
            return document.getElementById(decodeURIComponent(href.slice(1)));
        } catch (e) {
            return null;
        }
    }

    function moveTo(target) {
        target.scrollIntoView();
        target.focus({ preventScroll: true });
        if (document.activeElement === target || target.hasAttribute('tabindex')) return;

        target.setAttribute('tabindex', '-1');
        target.addEventListener('blur', function () {
            target.removeAttribute('tabindex');
        }, { once: true });
        target.focus({ preventScroll: true });
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

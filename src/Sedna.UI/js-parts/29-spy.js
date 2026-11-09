/* ── Scroll spy: the section in view ──────────────────────────────────────────
   A nav carrying `data-spy` lists the page's sections as bare fragment links — the
   `.page-toc` contents in 57-navigation-aids.css — and this marks the link to the
   section the reader is in `aria-current="location"`, and takes it off the others:

     <nav class="page-toc" aria-label="On this page" data-spy>
       <ol class="page-toc-list">
         <li><a class="page-toc-link" href="#delivery">Delivery</a></li>
         …

   The section in view is the one whose heading has most recently come into the top
   part of what scrolls it — the frame's `.page`, a `.sedna-scroll`, or the document.
   An IntersectionObserver says which headings are in that band and which have passed
   above it, so nothing here measures a heading. At the top of the page, before any
   heading has arrived, the first link is current; scrolled to the very end, the last
   one is, though its heading may never reach the band.

   A click on one of the links — 29-fragment.js moves the reader to its heading —
   makes that link current at once and keeps it so until the reader scrolls again: a
   short section near the end can be jumped to and never reach the band.

   Lists the app renders later, and headings a render replaces, are picked up as they
   land; a nav that leaves the page is let go. Nothing here is a public member: the
   attribute is the whole contract, and `aria-current` is an attribute the app does not
   render on these links itself.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {
    if (!('IntersectionObserver' in window)) return;

    // nav → { links, targets, root, io, state, pinned, pinnedAt }
    var spies = new Map();

    function idOf(link) {
        var href = link.getAttribute('href') || '';
        if (href.length < 2 || href.charAt(0) !== '#') return null;
        try { return decodeURIComponent(href.slice(1)); } catch (e) { return null; }
    }

    function linksOf(nav) {
        var all = nav.querySelectorAll('a[href^="#"]');
        var links = [], targets = [];
        for (var i = 0; i < all.length; i++) {
            var id = idOf(all[i]);
            var target = id && document.getElementById(id);
            if (!target) continue;
            links.push(all[i]);
            targets.push(target);
        }
        return { links: links, targets: targets };
    }

    function scrollerOf(el) {
        return ui._.scrollerOf ? ui._.scrollerOf(el) : null;
    }

    function scrollingElement(spy) {
        return spy.root || document.scrollingElement || document.documentElement;
    }

    function atEnd(spy) {
        var el = scrollingElement(spy);
        var max = el.scrollHeight - el.clientHeight;
        return max > 1 && el.scrollTop >= max - 1;
    }

    function mark(spy, index) {
        for (var i = 0; i < spy.links.length; i++) {
            if (i === index) {
                if (spy.links[i].getAttribute('aria-current') !== 'location') spy.links[i].setAttribute('aria-current', 'location');
            } else if (spy.links[i].getAttribute('aria-current') === 'location') {
                spy.links[i].removeAttribute('aria-current');
            }
        }
    }

    function current(spy) {
        if (spy.links.length === 0) return -1;
        if (atEnd(spy)) return spy.links.length - 1;
        var above = -1;
        for (var i = 0; i < spy.targets.length; i++) {
            var s = spy.state.get(spy.targets[i]);
            if (s === 'in') return i;
            if (s === 'above') above = i;
        }
        return above >= 0 ? above : 0;
    }

    function update(spy) {
        if (spy.pinned >= 0) mark(spy, spy.pinned);
        else mark(spy, current(spy));
    }

    function stop(nav) {
        var spy = spies.get(nav);
        if (!spy) return;
        spy.io.disconnect();
        spies.delete(nav);
    }

    function start(nav) {
        var found = linksOf(nav);
        var spy = {
            links: found.links,
            targets: found.targets,
            root: found.targets.length ? scrollerOf(found.targets[0]) : null,
            state: new Map(),
            pinned: -1,
            pinnedAt: null,
            io: null
        };
        // The band is the top two fifths of the scroller: a heading that has come that
        // far is the one being read. Above it is passed; below it, not yet reached.
        spy.io = new IntersectionObserver(function (entries) {
            for (var i = 0; i < entries.length; i++) {
                var e = entries[i];
                var top = e.rootBounds ? e.rootBounds.top : 0;
                spy.state.set(e.target, e.isIntersecting ? 'in' : (e.boundingClientRect.top < top ? 'above' : 'below'));
            }
            update(spy);
        }, { root: spy.root, rootMargin: '0px 0px -60% 0px', threshold: 0 });
        for (var i = 0; i < spy.targets.length; i++) spy.io.observe(spy.targets[i]);
        spies.set(nav, spy);
        update(spy);
    }

    // The same links to the same headings, all still on the page.
    function unchanged(nav, spy) {
        var found = linksOf(nav);
        if (found.links.length !== spy.links.length) return false;
        for (var i = 0; i < found.links.length; i++) {
            if (found.links[i] !== spy.links[i] || found.targets[i] !== spy.targets[i]) return false;
        }
        return true;
    }

    function sweep() {
        spies.forEach(function (spy, nav) {
            if (!nav.isConnected || !nav.hasAttribute('data-spy')) stop(nav);
        });
        var navs = document.querySelectorAll('[data-spy]');
        for (var i = 0; i < navs.length; i++) {
            var spy = spies.get(navs[i]);
            if (spy && unchanged(navs[i], spy)) continue;
            stop(navs[i]);
            start(navs[i]);
        }
    }

    document.addEventListener('scroll', function (e) {
        var scroller = e.target === document ? null : e.target;
        spies.forEach(function (spy) {
            if (spy.root !== scroller && !(scroller === document.scrollingElement && spy.root === null)) return;
            if (spy.pinned >= 0) {
                // The scroll event of the jump the click made is at the place it landed;
                // the reader's own scroll after it lets the band decide again.
                if (Math.abs(scrollingElement(spy).scrollTop - spy.pinnedAt) < 2) return;
                spy.pinned = -1;
                spy.pinnedAt = null;
            }
            update(spy);
        });
    }, true);

    // After 29-fragment.js, which moved the reader: the link clicked is the section now.
    document.addEventListener('click', function (e) {
        if (!e.defaultPrevented || !(e.target instanceof Element)) return;
        var link = e.target.closest('[data-spy] a[href^="#"]');
        if (!link) return;
        var spy = spies.get(link.closest('[data-spy]'));
        if (!spy) return;
        var index = spy.links.indexOf(link);
        if (index < 0) return;
        spy.pinned = index;
        // The scroll the jump made has already happened, or there was none to make.
        spy.pinnedAt = scrollingElement(spy).scrollTop;
        update(spy);
    });

    var queued = false;
    function queue() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(function () { queued = false; sweep(); });
    }

    function watch() {
        sweep();
        if (!('MutationObserver' in window)) return;
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                if (records[i].type === 'attributes' || records[i].addedNodes.length || records[i].removedNodes.length) {
                    queue();
                    return;
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-spy', 'href', 'id'] });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();

})(window.sednaUi);

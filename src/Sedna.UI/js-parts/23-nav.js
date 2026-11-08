/* ── Nav filter, area rail and bottom bar, delegated ──────────────────────────
   Three nav behaviours, each opt-in by attribute and each read by CSS in the frame
   parts: 14-frame-nav-filter.css, 22-frame-nav-areas.css and 24-frame-bottombar.css.

   THE FILTER. An input carrying data-nav-filter narrows the .nav it sits in:

     <div class="nav-filter">
       <i class="ri-filter-3-line" aria-hidden="true"></i>
       <input class="nav-filter-input" type="search" aria-label="Filter pages" data-nav-filter>
     </div>

   While it holds text, .nav gets data-filtering and every .nav-scroll link that does
   not match gets data-nav-miss; the CSS hides those, the sections and groups left
   empty, and opens the closed groups that still hold a match. A link matches when
   every word typed appears in its text or its data-keywords. Enter follows the first
   match, Escape clears, and following any link clears too, so the nav the reader
   lands on is whole again.

   It tests containment and does not call ui._.score. That matcher ranks, and a
   ranked list can afford a subsequence hit because a weak match sinks to the
   bottom. A filter has no bottom: every hit shows at the same weight, and "tab"
   would keep "Status bar" beside "Tables".

   THE AREA RAIL. A .nav-area button carrying data-nav-area shows the panel named by
   its aria-controls and hides its siblings' panels, keeping aria-expanded in step:

     <button class="nav-area" type="button" aria-controls="area-stock"
             aria-expanded="false" data-nav-area>…</button>

   An app that decides the current area in C# should NOT add data-nav-area — it would
   then have two things setting `hidden`. Render the panel from the current address
   and make each rail item a link to its area, as the tabs note says of data-tabs.

   THE BOTTOM BAR. A .bottombar carrying data-hide-on-scroll steps its items out of the
   way while the reader scrolls down and brings them back on the way up, by writing
   data-away. It follows the scroller it shares a container with — .page, beside it in
   .content — or the document. Focus moving into the bar brings it back, so a keyboard
   user never tabs onto an item they cannot see.

   MOVING BETWEEN REGIONS. F6 moves focus to the next region of the frame and Shift+F6
   to the previous, wrapping at both ends. A region is a landmark — header, nav, main,
   aside, footer or an element with a landmark role (a header or footer inside an
   article, aside, main, nav or section is not one) — a .topbar, .sidebar or .bottombar,
   or an open dialog that is not modal: a modeless drawer or sheet. Only the outermost
   counts, so the nav inside a sidebar is not a stop of its own; a region inside an open
   dialog is part of the dialog. Regions are visited in document order, skipping one
   that is not rendered, is invisible, is inert or aria-hidden, or lies wholly beside the
   viewport, as an off-canvas drawer does.

   Focus goes to the region's first element Tab would reach. A region with none takes
   the focus itself: the script gives it tabindex="-1" for as long as it holds the
   focus, unless it already had a tabindex, and takes the attribute away on blur.

   While a modal dialog is open, F6 is left to the browser: the dialog is the only
   region there is, and the rest of the page is inert.

   None of these behaviours inserts or removes an element, and each writes only attributes
   an app does not render itself, so a Blazor re-render has nothing to revert.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function words(text) {
        return text.toLowerCase().split(/\s+/).filter(function (w) { return w.length > 0; });
    }

    function links(nav) {
        return nav.querySelectorAll('.nav-scroll .nav-link');
    }

    function apply(input) {
        var nav = input.closest('.nav');
        if (!nav) return 0;

        var terms = words(input.value);
        var all = links(nav);
        var hits = 0;

        if (terms.length === 0) nav.removeAttribute('data-filtering');
        else nav.setAttribute('data-filtering', '');

        for (var i = 0; i < all.length; i++) {
            var hay = (all[i].textContent + ' ' + (all[i].getAttribute('data-keywords') || '')).toLowerCase();
            var hit = terms.every(function (t) { return hay.indexOf(t) >= 0; });
            if (hit) { hits++; all[i].removeAttribute('data-nav-miss'); }
            else all[i].setAttribute('data-nav-miss', '');
        }
        return hits;
    }

    function clear(input) {
        if (!input.value) return;
        input.value = '';
        apply(input);
    }

    function showArea(item) {
        var sidebar = item.closest('.sidebar') || document;
        var items = sidebar.querySelectorAll('[data-nav-area]');
        for (var i = 0; i < items.length; i++) {
            var isIt = items[i] === item;
            items[i].setAttribute('aria-expanded', String(isIt));
            var panel = document.getElementById(items[i].getAttribute('aria-controls') || '');
            if (panel) panel.hidden = !isIt;
        }
    }

    ui.nav = {
        // Filters the nav an input belongs to, optionally setting what it holds
        // first. Returns how many links match; a blank query restores the nav.
        filter: function (input, query) {
            if (!input) return 0;
            if (typeof query === 'string') input.value = query;
            return apply(input);
        },
        // Shows an area's panel, by rail item or by the panel's id.
        showArea: function (itemOrPanelId) {
            var item = typeof itemOrPanelId === 'string'
                ? document.querySelector('[data-nav-area][aria-controls="' + itemOrPanelId + '"]')
                : itemOrPanelId;
            if (item) showArea(item);
        },
        // Moves the focus to the next region (1) or the previous (-1), as F6 and
        // Shift+F6 do. Answers the region, or null.
        moveRegion: function (step) { return moveRegion(step < 0 ? -1 : 1); }
    };

    // Direction, not offset: the bar leaves on the way down and returns on the way up,
    // wherever on the page that happens. A few pixels either way is a trackpad settling,
    // not a decision.
    //
    // The bar is in the flow, so its leaving makes the scroller taller — and near the
    // end of a page the browser answers by clamping scrollTop upwards, which reads as
    // the reader scrolling back up and brings the bar straight back. So for a moment
    // after the bar moves, the scroll position is followed but not acted on.
    var lastY = new WeakMap();
    var SETTLE_MS = 400;
    var settledAt = new WeakMap();

    document.addEventListener('scroll', function (e) {
        var bars = document.querySelectorAll('.bottombar[data-hide-on-scroll]');
        if (bars.length === 0) return;

        var root = document.scrollingElement;
        var scroller = e.target === document ? root : e.target;
        if (!scroller || scroller.nodeType !== 1) return;

        var y = scroller.scrollTop;
        var last = lastY.has(scroller) ? lastY.get(scroller) : 0;
        if ((settledAt.get(scroller) || 0) > Date.now()) { lastY.set(scroller, y); return; }
        // Up, and still at the very end: that is the browser clamping a scroller that grew,
        // never the reader, who leaves the end on the way up. A loaded machine can deliver the
        // clamp after the moment above has passed, so it is recognised, not only timed out.
        if (y < last && y >= scroller.scrollHeight - scroller.clientHeight - 1) { lastY.set(scroller, y); return; }
        if (Math.abs(y - last) < 8) return;
        lastY.set(scroller, y);

        var away = y > last && y > 40;
        for (var i = 0; i < bars.length; i++) {
            var shared = scroller === root || (bars[i].parentElement && bars[i].parentElement.contains(scroller));
            if (!shared || bars[i].contains(scroller)) continue;
            var was = bars[i].hasAttribute('data-away');
            if (away && !was && !bars[i].contains(document.activeElement)) bars[i].setAttribute('data-away', '');
            else if (!away && was) bars[i].removeAttribute('data-away');
            else continue;
            settledAt.set(scroller, Date.now() + SETTLE_MS);
        }
    }, true);

    document.addEventListener('focusin', function (e) {
        var bar = e.target.closest && e.target.closest('.bottombar[data-away]');
        if (bar) bar.removeAttribute('data-away');
    });

    document.addEventListener('input', function (e) {
        if (e.target.matches && e.target.matches('[data-nav-filter]')) apply(e.target);
    });

    document.addEventListener('keydown', function (e) {
        var input = e.target.closest && e.target.closest('[data-nav-filter]');
        if (!input) return;

        if (e.key === 'Escape' && input.value) {
            e.preventDefault();
            clear(input);
        } else if (e.key === 'Enter') {
            var nav = input.closest('.nav');
            var first = nav && nav.querySelector('.nav-scroll .nav-link:not([data-nav-miss])');
            if (first && nav.hasAttribute('data-filtering')) {
                e.preventDefault();
                first.click();
            }
        }
    });

    document.addEventListener('click', function (e) {
        var area = e.target.closest('[data-nav-area]');
        if (area) {
            e.preventDefault();
            showArea(area);
            return;
        }

        // Following a link out of a filtered nav clears the filter. Deferred, so the
        // click's own navigation is not raced by the re-render it would cause.
        var link = e.target.closest('.nav[data-filtering] .nav-scroll .nav-link');
        if (link) {
            var input = link.closest('.nav').querySelector('[data-nav-filter]');
            if (input) setTimeout(function () { clear(input); }, 0);
        }
    });

    /* ── Moving between regions ───────────────────────────────────────────────── */

    var REGION = 'header, nav, main, aside, footer, [role="banner"], [role="navigation"], [role="main"], ' +
        '[role="complementary"], [role="contentinfo"], [role="search"], [role="region"], ' +
        '.topbar, .sidebar, .bottombar, dialog[open]';
    var TABBABLE = 'a[href], area[href], button, input:not([type="hidden"]), select, textarea, summary, ' +
        'iframe, audio[controls], video[controls], [contenteditable=""], [contenteditable="true"], [tabindex]';

    function shown(el) {
        if (el.closest('[inert], [aria-hidden="true"]')) return false;
        if (el.checkVisibility && !el.checkVisibility({ visibilityProperty: true })) return false;
        if (!el.getClientRects().length) return false;
        var r = el.getBoundingClientRect();
        // Wholly beside the viewport: an off-canvas drawer, moved out by a transform.
        return r.right > 0 && r.left < window.innerWidth;
    }

    function landmark(el) {
        if (el.tagName === 'DIALOG') return !el.matches(':modal');
        // A header or footer scoped to a section of content is not a landmark.
        if (!el.hasAttribute('role') && (el.tagName === 'HEADER' || el.tagName === 'FOOTER') &&
            !el.matches('.topbar, .bottombar') &&
            el.parentElement && el.parentElement.closest('article, aside, main, nav, section')) return false;
        return true;
    }

    // The frame's regions, outermost only, in document order.
    function regions() {
        var all = Array.prototype.filter.call(document.querySelectorAll(REGION), landmark);
        return all.filter(function (el) {
            for (var i = 0; i < all.length; i++) {
                // A dialog is a region of its own wherever it sits; anything else inside
                // another region is part of it.
                if (all[i] !== el && all[i].contains(el) && el.tagName !== 'DIALOG') return false;
            }
            return shown(el);
        });
    }

    function firstTabbable(region) {
        var all = region.querySelectorAll(TABBABLE);
        for (var i = 0; i < all.length; i++) {
            var el = all[i];
            if (el.tabIndex < 0 || el.disabled) continue;
            if (!shown(el)) continue;
            // Inside a closed <details>, only its summary is reachable.
            var closed = el.closest('details:not([open])');
            if (closed && el.tagName !== 'SUMMARY' && !el.closest('summary')) continue;
            return el;
        }
        return null;
    }

    function enterRegion(region) {
        var target = firstTabbable(region);
        if (target) { target.focus(); return; }
        if (!region.hasAttribute('tabindex')) {
            region.setAttribute('tabindex', '-1');
            region.addEventListener('blur', function lose() {
                region.removeEventListener('blur', lose);
                region.removeAttribute('tabindex');
            });
        }
        region.focus();
    }

    /* Moves the focus one region on (1) or back (-1) and answers the region, or null
       with none to go to or while a modal dialog is open. */
    function moveRegion(step) {
        if (document.querySelector('dialog:modal')) return null;
        var list = regions();
        if (!list.length) return null;

        // The innermost region holding the focus: descendants follow their ancestors in
        // document order, so the last one that contains it.
        var active = document.activeElement;
        var at = -1;
        for (var i = 0; i < list.length; i++) if (active && list[i].contains(active)) at = i;

        var next = at < 0
            ? (step > 0 ? 0 : list.length - 1)
            : (at + (step > 0 ? 1 : -1) + list.length) % list.length;
        enterRegion(list[next]);
        return list[next];
    }

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'F6' || e.defaultPrevented || e.ctrlKey || e.altKey || e.metaKey) return;
        if (moveRegion(e.shiftKey ? -1 : 1)) e.preventDefault();
    });

})(window.sednaUi);

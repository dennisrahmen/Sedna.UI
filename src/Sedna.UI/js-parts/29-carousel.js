/* ── Carousel ────────────────────────────────────────────────────────────────
   The behaviour behind a `.carousel` with `data-carousel` (62-carousel.css has the
   markup). The track scrolls and snaps on its own; this adds what scrolling cannot:

     · `data-carousel-prev` / `data-carousel-next` move one slide back or on, and
       `data-carousel-to="3"` goes to the third — smoothly, unless the reader asked for
       reduced motion, when the track lands at once;
     · the slide in view and its dot carry `aria-current="true"`, and the previous or
       next button that has nowhere to go carries `aria-disabled="true"` and stays
       focusable;
     · on the focused track ← → move a slide (mirrored right to left), Home and End go
       to the first and the last;
     · once the track comes to rest on another slide, the app's `[data-carousel-live]`
       region says so: its `data-carousel-say` sentence with {position}, {count} and
       {label} filled in, else the slide's own `aria-label`. Nothing is said while the
       page loads.

   The slides are the track's element children. The current one is the slide whose
   inline start is nearest the track's, which is where a snap leaves one. Nothing here
   rotates on a timer: a carousel that moves on its own takes the page from a reader
   who is still reading it.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var reduce = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null;
    var said = typeof WeakMap === 'function' ? new WeakMap() : null;
    var settling = typeof WeakMap === 'function' ? new WeakMap() : null;

    function rootOf(el) {
        if (typeof el === 'string') el = document.getElementById(el);
        return el && el.closest ? el.closest('[data-carousel]') : null;
    }

    function trackOf(c) { return c.querySelector('.carousel-track'); }

    function slidesOf(track) {
        var out = [];
        for (var i = 0; i < track.children.length; i++) out.push(track.children[i]);
        return out;
    }

    function rtl(track) { return getComputedStyle(track).direction === 'rtl'; }

    /* How far a slide's inline start is past the track's, in the direction the slides
       run — positive for one further on. */
    function offset(track, slide, backwards) {
        var t = track.getBoundingClientRect(), s = slide.getBoundingClientRect();
        return backwards ? t.right - s.right : s.left - t.left;
    }

    function current(c) {
        var track = trackOf(c);
        if (!track) return -1;
        var slides = slidesOf(track), back = rtl(track), best = -1, bestAt = Infinity;
        for (var i = 0; i < slides.length; i++) {
            var at = Math.abs(offset(track, slides[i], back));
            if (at < bestAt - 0.5) { best = i; bestAt = at; }
        }
        return best;
    }

    function edges(track) {
        var max = track.scrollWidth - track.clientWidth;
        // RTL scrolls into negative values; the distance from the start is the magnitude.
        var pos = Math.abs(track.scrollLeft);
        return { start: max <= 1 || pos <= 1, end: max <= 1 || pos >= max - 1 };
    }

    function dotIndex(dot, order) {
        var n = parseInt(dot.getAttribute('data-carousel-to'), 10);
        return isNaN(n) ? order : n - 1;
    }

    function setFlag(el, name, on, value) {
        if (on) { if (el.getAttribute(name) !== value) el.setAttribute(name, value); }
        else if (el.hasAttribute(name)) el.removeAttribute(name);
    }

    function announce(c, index, slides) {
        var live = c.querySelector('[data-carousel-live]');
        if (!live || index < 0) return;
        var slide = slides[index];
        var label = slide.getAttribute('aria-label') || '';
        var sentence = live.getAttribute('data-carousel-say');
        var text = sentence
            ? sentence.replace(/\{(position|count|label)\}/g, function (all, name) {
                if (name === 'position') return String(index + 1);
                if (name === 'count') return String(slides.length);
                return label;
            })
            : label;
        if (!text) return;
        live.textContent = live.textContent === text ? text + ' ' : text;
    }

    /* Marks the current slide, its dot and the two ends; says the position when asked
       to and it has changed since it was last said. */
    function mark(c, speak) {
        var track = trackOf(c);
        if (!track) return;
        var slides = slidesOf(track), index = current(c), end = edges(track);

        for (var i = 0; i < slides.length; i++) setFlag(slides[i], 'aria-current', i === index, 'true');

        var dots = c.querySelectorAll('[data-carousel-to]');
        for (var d = 0; d < dots.length; d++) setFlag(dots[d], 'aria-current', dotIndex(dots[d], d) === index, 'true');

        var prev = c.querySelectorAll('[data-carousel-prev]'), next = c.querySelectorAll('[data-carousel-next]');
        for (var p = 0; p < prev.length; p++) setFlag(prev[p], 'aria-disabled', end.start, 'true');
        for (var n = 0; n < next.length; n++) setFlag(next[n], 'aria-disabled', end.end, 'true');

        if (!said) return;
        // The first mark is the page loading, which says nothing.
        if (!said.has(c)) { said.set(c, index); return; }
        if (!speak || said.get(c) === index) return;
        said.set(c, index);
        announce(c, index, slides);
    }

    function go(el, index) {
        var c = rootOf(el);
        if (!c) return -1;
        var track = trackOf(c);
        if (!track) return -1;
        var slides = slidesOf(track);
        if (!slides.length) return -1;
        index = Math.max(0, Math.min(slides.length - 1, index | 0));

        var back = rtl(track);
        var by = offset(track, slides[index], back);
        var left = track.scrollLeft + (back ? -by : by);
        var smooth = !(reduce && reduce.matches);
        track.scrollTo({ left: left, behavior: smooth ? 'smooth' : 'auto' });
        // An instant scroll has landed already; a smooth one is marked as it moves.
        if (!smooth) mark(c, true);
        return index;
    }

    ui.carousel = {
        /* Goes to the slide at `index`, counted from 0, in the carousel `el` is or is
           inside — an element or an id. Returns the index it goes to, or -1. */
        go: go,
        next: function (el) { var c = rootOf(el); return c ? go(c, current(c) + 1) : -1; },
        prev: function (el) { var c = rootOf(el); return c ? go(c, current(c) - 1) : -1; },
        /* The index of the slide in view, or -1. */
        current: function (el) { var c = rootOf(el); return c ? current(c) : -1; }
    };

    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-carousel-prev], [data-carousel-next], [data-carousel-to]');
        if (!button) return;
        var c = rootOf(button);
        if (!c) return;
        e.preventDefault();
        if (button.getAttribute('aria-disabled') === 'true') return;
        if (button.hasAttribute('data-carousel-prev')) ui.carousel.prev(c);
        else if (button.hasAttribute('data-carousel-next')) ui.carousel.next(c);
        else {
            var dots = c.querySelectorAll('[data-carousel-to]');
            go(c, dotIndex(button, Array.prototype.indexOf.call(dots, button)));
        }
    });

    document.addEventListener('keydown', function (e) {
        if (e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return;
        var track = e.target.closest && e.target.closest('.carousel-track');
        if (!track) return;
        // A field inside a slide keeps its own arrow keys.
        if (e.target !== track && (e.target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName))) return;
        var c = rootOf(track);
        if (!c) return;
        var back = rtl(track), now = current(c), to;
        if (e.key === 'ArrowRight') to = back ? now - 1 : now + 1;
        else if (e.key === 'ArrowLeft') to = back ? now + 1 : now - 1;
        else if (e.key === 'Home') to = 0;
        else if (e.key === 'End') to = slidesOf(track).length - 1;
        else return;
        e.preventDefault();
        go(c, to);
    });

    /* Marked as it scrolls, a frame at a time; said once it comes to rest — at
       `scrollend` where the engine has it, else when a moment passes with no scroll. */
    var frame = null;
    function settle(c) {
        if (!settling) return;
        clearTimeout(settling.get(c));
        settling.delete(c);
        mark(c, true);
    }

    document.addEventListener('scroll', function (e) {
        var track = e.target;
        if (!track || !track.classList || !track.classList.contains('carousel-track')) return;
        var c = rootOf(track);
        if (!c) return;
        if (!frame) frame = requestAnimationFrame(function () { frame = null; mark(c, false); });
        if (settling) {
            clearTimeout(settling.get(c));
            // page-timer: the fallback for an engine without `scrollend`.
            settling.set(c, setTimeout(function () { settle(c); }, 150));
        }
    }, true);

    document.addEventListener('scrollend', function (e) {
        var track = e.target;
        if (!track || !track.classList || !track.classList.contains('carousel-track')) return;
        var c = rootOf(track);
        if (c) settle(c);
    }, true);

    function markAll(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var all = scope.querySelectorAll('[data-carousel]');
        for (var i = 0; i < all.length; i++) mark(all[i], false);
        if (scope !== document && scope.matches && scope.matches('[data-carousel]')) mark(scope, false);
    }

    function watch() {
        markAll();
        window.addEventListener('resize', function () { markAll(); });
        if (!('MutationObserver' in window)) return;
        // Childlist only: the marks are attribute changes, and watching those would loop.
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    if (added[j].nodeType !== 1) continue;
                    var c = rootOf(added[j]);
                    if (c) mark(c, false);
                    else markAll(added[j]);
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();

})(window.sednaUi);

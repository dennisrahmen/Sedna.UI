/* ── Spotlight positioning ───────────────────────────────────────────────────
   `.spotlight-hole` dims the page except one box. The box is what only the browser
   knows, so this measures a target and writes the four values onto the hole:

     sednaUi.spotlight.at(hole, target)
     sednaUi.spotlight.at(hole, target, { pad: 6 })
     sednaUi.spotlight.at(hole, target, { include: '[data-extend]' })
     sednaUi.spotlight.tipAt(tip, rect, { placement: 'auto' })
     var step = sednaUi.spotlight.follow(hole, '[data-step="two"]', { tip: pop });
     sednaUi.spotlight.lock({ allow: pop });

   The steps, the copy and the order are the app's. This is deliberately not a tour:
   a tour is a sequence with its own state, and a library that owned it would also own
   what "next" means, whether a step can be skipped, and where the bubble goes — all
   of which differ per app.

   What is here instead is the geometry and the input model, which are the same in
   every tour and are where the bugs live: measuring a target, unioning it with the UI
   it produced, placing a bubble that does not fall off the viewport, staying attached
   while the page moves, and making the rest of the page inert while one step is live.

   A target is an element, a list of elements, or a CSS selector. Prefer the selector
   for anything long-lived: a framework re-render replaces the node, and an element
   captured once then points at a detached copy.

   The hole is positioned against its offset parent, which has to be positioned.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    /* The lock is security-shaped and is not security. Capture-phase guards and
       `pointer-events: none` stop a person, not a script — never treat a live step as
       an authorization boundary. */
    var lock = null;          // { allow, block, gate } while a lock is up, else null
    var marked = null;        // the element currently carrying .spotlight-allowed

    // What the lock lets through whatever the caller passed: the bubble itself, whatever
    // the step has marked usable, and Blazor's reconnect UI — a dropped circuit has to
    // stay recoverable mid-step.
    var ALWAYS = '.spotlight-tip, .spotlight-allowed, #components-reconnect-modal';

    function toArray(v) {
        if (!v) return [];
        if (typeof v === 'string') {
            var found = document.querySelectorAll(v), list = [];
            for (var i = 0; i < found.length; i++) list.push(found[i]);
            return list;
        }
        if (v.nodeType === 1) return [v];
        if (typeof v.length === 'number') {
            var out = [];
            for (var j = 0; j < v.length; j++) {
                var one = toArray(v[j]);
                for (var k = 0; k < one.length; k++) out.push(one[k]);
            }
            return out;
        }
        return [];
    }

    /* An element a framework has rendered but not shown yet has no client rects.
       Unioning its 0×0 box at the document origin would drag the hole into the
       top-left corner, which is why every rect here is filtered rather than trusted. */
    function visible(el) { return !!(el && el.getClientRects().length); }

    function resolve(target, extra) {
        return toArray(target).concat(toArray(extra)).filter(visible);
    }

    /* Every element this file takes may be given as a selector instead, and that is the
       form to prefer: the hole, the bubble and the anchor are all nodes a framework can
       replace on any render, and a reference captured once then points at a detached
       copy. It also means a Blazor app can drive the whole thing from C# with strings,
       rather than threading an ElementReference through every call. */
    function one(v) {
        var list = toArray(v);
        return list.length ? list[0] : null;
    }

    function union(els) {
        var left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
        for (var i = 0; i < els.length; i++) {
            var b = els[i].getBoundingClientRect();
            if (b.left < left) left = b.left;
            if (b.top < top) top = b.top;
            if (b.right > right) right = b.right;
            if (b.bottom > bottom) bottom = b.bottom;
        }
        return {
            left: left, top: top, right: right, bottom: bottom,
            width: right - left, height: bottom - top
        };
    }

    function offsetParentRect(el) {
        var parent = el.offsetParent || document.body;
        return parent.getBoundingClientRect();
    }

    // ── The interaction lock ────────────────────────────────────────────────

    function allowed(node) {
        if (!lock) return true;
        if (!(node instanceof Node)) return false;
        var regions = toArray(ALWAYS).concat(toArray(lock.allow));
        for (var i = 0; i < regions.length; i++) {
            if (!regions[i].contains(node)) continue;
            // An opt-out for a control inside a live region the step must not let the
            // reader hit — the Cancel beside the button the step is about.
            if (lock.block && node instanceof Element && node.closest(lock.block)) return false;
            return true;
        }
        return false;
    }

    function mark(el) {
        if (marked && marked !== el) marked.classList.remove('spotlight-allowed');
        marked = el || null;
        if (marked) marked.classList.add('spotlight-allowed');
    }

    function guardPointer(e) {
        if (!allowed(e.target)) { e.stopPropagation(); e.preventDefault(); }
    }

    /* Where a space bar is a character rather than a press. Getting this wrong is how
       a lock takes the space out of someone's typing, and it is invisible until a
       reader tries to write two words. */
    function typing(el) {
        if (el.isContentEditable) return true;
        if (el.tagName === 'TEXTAREA') return true;
        if (el.tagName !== 'INPUT') return false;
        var type = (el.getAttribute('type') || 'text').toLowerCase();
        return type !== 'button' && type !== 'submit' && type !== 'reset'
            && type !== 'checkbox' && type !== 'radio' && type !== 'image' && type !== 'file';
    }

    function guardKey(e) {
        // Only keyboard activation is blocked. Space where it types a character, and
        // space on the page body where it scrolls, both stay free — a lock that
        // swallowed every key would break both.
        if (e.key !== 'Enter' && e.key !== ' ') return;
        var t = e.target;
        if (!(t instanceof Element)) return;
        if (e.key === ' ' && typing(t)) return;
        if (!t.closest('button, a, input, select, textarea, [tabindex]')) return;
        if (!allowed(t)) { e.stopPropagation(); e.preventDefault(); }
    }

    // ── Public surface ──────────────────────────────────────────────────────

    ui.spotlight = {
        /* Puts `hole` over `target`, or over the union of several targets. `pad` grows
           the hole beyond them so the ring does not sit on its edge; it defaults to 4px.
           `include` names extra elements that widen the box without being the anchor —
           a control plus the dropdown it opened. Invisible elements are skipped, and
           null comes back when nothing visible is left, which is the signal to hide the
           hole rather than park it at the origin.

           Returns the rectangle used, in the hole's own coordinate space, so an app can
           place a bubble without measuring twice. */
        at: function (hole, target, options) {
            hole = one(hole);
            if (!hole) return null;

            var opts = options || {};
            var pad = (typeof opts.pad === 'number') ? opts.pad : 4;
            var targets = resolve(target, opts.include);
            if (!targets.length) return null;

            var box = union(targets);
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
            // One target: the ring follows its own rounding, so a pill-shaped button is
            // not highlighted with a rectangle. A union is not any one of their shapes,
            // so the inline value is cleared and the stylesheet's radius applies.
            hole.style.borderRadius = targets.length === 1
                ? getComputedStyle(targets[0]).borderRadius
                : '';

            return rect;
        },

        /* Places `tip` beside the rectangle `at()` returned and reports the side it
           used, so a caller can point an arrow. Options:

             placement  'bottom' (default), 'top', 'left', 'right', or 'auto' —
                        the side with the most free space
             gap        distance from the rectangle, 12
             margin     smallest distance from the boundary's edge, 10
             boundary   what the bubble has to stay inside, the viewport by default.
                        An element or a selector, for a tour that runs inside a panel
                        rather than across the page

           A number is still accepted in place of the options and means `gap`.

           The chosen side flips to its opposite when the preferred one does not fit,
           the bubble is centred on the rectangle's cross axis, and the result is
           clamped into the boundary so a bubble beside an edge anchor stays whole. */
        tipAt: function (tip, rect, options) {
            tip = one(tip);
            if (!tip || !rect) return null;

            var opts = (typeof options === 'number') ? { gap: options } : (options || {});
            var gap = (typeof opts.gap === 'number') ? opts.gap : 12;
            var margin = (typeof opts.margin === 'number') ? opts.margin : 10;

            /* `rect` is in the hole's coordinate space and the clamp is against the
               viewport, so the offset between the two is measured rather than assumed:
               park the tip at the origin of that space and read back where it landed.
               That also yields the size, which is what decides which side fits and is
               known only once this step's text is laid out in it.

               The origin, not the rectangle: an absolutely positioned box with only
               `left` set is shrink-to-fit against what remains of its containing block,
               so measuring it beside a right-hand target returns a box far narrower than
               the one that will actually be drawn — and the placement then puts it off
               the edge. Parking at 0 measures it against the full width.

               The measured width is then pinned, because the same shrink-to-fit would
               re-narrow the box the moment it is moved. Cleared first, so a step whose
               text is shorter is not held at the previous step's width. */
            tip.style.width = '';
            tip.style.left = '0px';
            tip.style.top = '0px';
            var probe = tip.getBoundingClientRect();
            var dx = probe.left, dy = probe.top;
            var w = probe.width;
            tip.style.width = w + 'px';
            var h = tip.getBoundingClientRect().height;

            var edge = one(opts.boundary);
            var vp = edge ? edge.getBoundingClientRect()
                          : { left: 0, top: 0, right: window.innerWidth, bottom: window.innerHeight };
            var r = {
                left: rect.left + dx,
                top: rect.top + dy,
                right: rect.left + dx + rect.width,
                bottom: rect.top + dy + rect.height,
                width: rect.width,
                height: rect.height
            };

            var space = {
                top: r.top - vp.top, bottom: vp.bottom - r.bottom,
                left: r.left - vp.left, right: vp.right - r.right
            };
            var opposite = { top: 'bottom', bottom: 'top', left: 'right', right: 'left' };

            var side = opts.placement || 'bottom';
            if (side === 'auto') {
                side = ['right', 'left', 'bottom', 'top'].sort(function (a, b) {
                    return space[b] - space[a];
                })[0];
            }
            /* Flip only towards more room. A one-way flip can land the bubble on a side
               that is worse than the one it left — which then reads as the clamp having
               shoved it over the thing it is explaining. */
            var need = ((side === 'left' || side === 'right') ? w : h) + gap + margin;
            if (space[side] < need && space[opposite[side]] > space[side]) side = opposite[side];

            var x, y;
            if (side === 'left') { x = r.left - w - gap; y = r.top + r.height / 2 - h / 2; }
            else if (side === 'right') { x = r.right + gap; y = r.top + r.height / 2 - h / 2; }
            else if (side === 'top') { x = r.left + r.width / 2 - w / 2; y = r.top - h - gap; }
            else { x = r.left + r.width / 2 - w / 2; y = r.bottom + gap; }

            x = Math.max(vp.left + margin, Math.min(x, vp.right - w - margin));
            y = Math.max(vp.top + margin, Math.min(y, vp.bottom - h - margin));

            tip.style.left = Math.round(x - dx) + 'px';
            tip.style.top = Math.round(y - dy) + 'px';
            return side;
        },

        /* Keeps the hole — and the bubble, when `tip` is passed — attached to a target
           while the page moves under them. Takes everything `at()` and `tipAt()` take,
           plus:

             tip     the bubble to place beside the hole
             root    what to watch for DOM changes, `document.body`. A selector or an
                     element; narrowing it is the escape hatch on a very large page
             lock    true, or { interactive, allow, block }, to make the rest of the
                     page inert while this step is live. `interactive` lets the reader
                     act on the anchor itself

           Returns { update, stop, side }. `update()` re-places now and returns the side
           used; `stop()` detaches, unlocks and hides the hole.

           Scroll is listened for in the capture phase so a nested scroller counts, and a
           MutationObserver covers the case neither scroll nor resize fires: a framework
           re-render moving the DOM. The observer must not watch attributes — placing
           writes `style`, which would loop it. */
        follow: function (hole, target, options) {
            var opts = options || {};
            var lockOpts = opts.lock === true ? {} : (opts.lock || null);
            var root = one(opts.root) || document.body;
            var raf = 0, stopped = false, observer = null;

            function place() {
                if (stopped) return null;
                // Resolved every time rather than captured: when these were given as
                // selectors, a re-render has replaced the nodes they named.
                var box = one(hole);
                var rect = ui.spotlight.at(box, target, opts);
                if (!rect) {
                    if (box) box.style.display = 'none';
                    if (lockOpts) mark(null);
                    handle.side = null;
                    return null;
                }
                box.style.display = '';
                // Re-applied every time for the same reason: a re-render can replace the
                // anchor and drop the class with it, and the lock would then shut the
                // live step out of the page.
                if (lockOpts) mark(lockOpts.interactive ? resolve(target)[0] : null);
                handle.side = opts.tip ? ui.spotlight.tipAt(opts.tip, rect, opts) : null;
                return handle.side;
            }

            function schedule() {
                if (raf || stopped) return;
                raf = requestAnimationFrame(function () { raf = 0; place(); });
            }

            var handle = {
                side: null,
                update: function () { return place(); },
                stop: function () {
                    if (stopped) return;
                    stopped = true;
                    if (raf) { cancelAnimationFrame(raf); raf = 0; }
                    window.removeEventListener('scroll', schedule, true);
                    window.removeEventListener('resize', schedule);
                    if (observer) { observer.disconnect(); observer = null; }
                    if (lockOpts) ui.spotlight.unlock();
                    var box = one(hole);
                    if (box) box.style.display = 'none';
                    handle.side = null;
                }
            };

            if (lockOpts) ui.spotlight.lock(lockOpts);
            window.addEventListener('scroll', schedule, true);
            window.addEventListener('resize', schedule);
            observer = new MutationObserver(schedule);
            observer.observe(root, { childList: true, subtree: true });
            place();

            return handle;
        },

        /* Makes the page inert except the bubble, anything marked `.spotlight-allowed`
           and whatever `allow` names. Scrolling and typing stay free; only pointer input
           and Enter/Space activation are taken away.

             allow  element, list or selector that stays usable
             block  selector for a control that stays dead even inside an allowed
                    region, '[data-spotlight-block]'

           Hover hints are gated too, because a control the reader cannot press must not
           still explain itself — the coupling every app building this has had to
           discover for itself. Any gate the app had set is chained, not replaced, and
           restored by unlock(). */
        lock: function (options) {
            var o = options || {};
            var already = !!lock;

            lock = {
                allow: o.allow || null,
                block: o.block === undefined ? '[data-spotlight-block]' : o.block,
                gate: already ? lock.gate : null
            };
            // A second lock() only re-aims an existing one. Wiring twice would stack a
            // gate on top of our own and leave the app's original unreachable.
            if (already) return;

            document.body.classList.add('spotlight-lock');
            document.addEventListener('pointerdown', guardPointer, true);
            document.addEventListener('click', guardPointer, true);
            document.addEventListener('keydown', guardKey, true);

            if (ui.tips) {
                var previous = ui.tips.gate;
                lock.gate = previous;
                ui.tips.gate = function (el) {
                    if (!allowed(el)) return false;
                    return typeof previous === 'function' ? previous(el) : true;
                };
            }
        },

        /* Ends the lock and puts the app's own hover-hint gate back. */
        unlock: function () {
            if (!lock) return;
            if (ui.tips) ui.tips.gate = lock.gate;
            lock = null;
            mark(null);
            document.body.classList.remove('spotlight-lock');
            document.removeEventListener('pointerdown', guardPointer, true);
            document.removeEventListener('click', guardPointer, true);
            document.removeEventListener('keydown', guardKey, true);
        }
    };

})(window.sednaUi);

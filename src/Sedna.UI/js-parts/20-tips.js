/* ── Hover hints (data-tip) ──────────────────────────────────────────────────
   One floating bubble, driven by [data-tip] through delegation on document — so
   it covers content rendered after load (Blazor re-renders) with no re-wiring.
   The bubble is appended to <body> and fixed-positioned, so a card's or table's
   overflow never clips it (a pure-CSS ::after tooltip is clipped).

   Elements inside .sidebar are skipped: the collapsed rail has its own CSS
   flyout, and both firing would double the tooltip.

   On touch there is no hover, so press and hold shows the hint instead — see the
   pointer handlers at the end.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    ui.tips = (function () {
        var tipEl = null, showTimer = null, current = null;
        var SHOW_DELAY = 130;   // ms — long enough not to flash on a passing cursor

        function ensureEl() {
            if (!tipEl) {
                tipEl = document.createElement('div');
                tipEl.className = 'sedna-tip';
                tipEl.setAttribute('role', 'tooltip');
                document.body.appendChild(tipEl);
            }
            return tipEl;
        }

        function trigger(t) {
            if (!t || !t.closest) return null;
            var el = t.closest('[data-tip]');
            if (!el || el.closest('.sidebar')) return null;
            // Optional app gate — e.g. a guided tour suppressing hints outside
            // the live step. Set sednaUi.tips.gate = function (el) { … }.
            if (typeof api.gate === 'function' && !api.gate(el)) return null;
            // Off altogether — the reader switched hints off in the app's settings.
            // The C# form of the gate, since a predicate does not cross into C#.
            if (api.enabled === false) return null;
            return el;
        }

        // True when the trigger, or anything inside it, is narrower than its own text
        // and clips the rest — a truncated label.
        function clipped(el) {
            var nodes = [el].concat(Array.prototype.slice.call(el.querySelectorAll('*')));
            for (var i = 0; i < nodes.length; i++) {
                var n = nodes[i];
                if (n.scrollWidth > n.clientWidth + 1 && getComputedStyle(n).overflowX !== 'visible') return true;
            }
            return false;
        }

        function place(el) {
            var tip = el.getAttribute('data-tip');
            if (!tip) return hide();
            // Redundant when the trigger's own visible text already spells out
            // the whole hint (innerText respects CSS visibility, so a hidden
            // label correctly counts as absent). Not when that text is cut off,
            // though: an ellipsis hides exactly the part the hint would give back.
            var vis = (el.innerText || '').trim();
            if (vis && vis.indexOf(tip) !== -1 && !clipped(el)) return hide();

            var box = ensureEl();
            box.textContent = tip;
            // Measure at the origin with a settled width, then position.
            box.style.left = '0px';
            box.style.top = '0px';
            box.classList.add('sedna-tip--visible');

            var r = el.getBoundingClientRect();
            var b = box.getBoundingClientRect();
            var pos = el.getAttribute('data-tip-pos') || 'top';
            var gap = 8, m = 6, vw = window.innerWidth, vh = window.innerHeight;

            // Vertical auto-flip when the preferred side has no room.
            if (pos === 'top' && r.top < b.height + gap + m) pos = 'bottom';
            else if (pos === 'bottom' && r.bottom + b.height + gap + m > vh) pos = 'top';

            var x, y;
            if (pos === 'left')        { x = r.left - b.width - gap; y = r.top + r.height / 2 - b.height / 2; }
            else if (pos === 'right')  { x = r.right + gap;          y = r.top + r.height / 2 - b.height / 2; }
            else if (pos === 'bottom') { x = r.left + r.width / 2 - b.width / 2; y = r.bottom + gap; }
            else                       { x = r.left + r.width / 2 - b.width / 2; y = r.top - b.height - gap; }

            // Keep the whole bubble inside the viewport.
            x = Math.max(m, Math.min(x, vw - b.width - m));
            y = Math.max(m, Math.min(y, vh - b.height - m));
            box.style.left = Math.round(x) + 'px';
            box.style.top = Math.round(y) + 'px';
        }

        function show(el) {
            if (current === el) return;   // already showing / queued for this one
            current = el;
            clearTimeout(showTimer);
            showTimer = setTimeout(function () { if (current === el) place(el); }, SHOW_DELAY);
        }

        function hide() {
            current = null;
            clearTimeout(showTimer);
            if (tipEl) tipEl.classList.remove('sedna-tip--visible');
        }

        document.addEventListener('mouseover', function (e) {
            var el = trigger(e.target);
            if (el) show(el);
        });
        document.addEventListener('mouseout', function (e) {
            var el = trigger(e.target);
            if (!el || el !== current) return;
            // Ignore moves that stay inside the same trigger (e.g. onto its icon).
            if (e.relatedTarget && el.contains(e.relatedTarget)) return;
            hide();
        });
        document.addEventListener('focusin', function (e) {
            var el = trigger(e.target);
            if (el) { current = el; place(el); }   // no delay for keyboard focus
        });
        document.addEventListener('focusout', hide);
        document.addEventListener('mousedown', function () {
            if (!held) hide();   // a click dismisses its own hint; a long press is showing it
        });

        /* Touch has no hover, so a hint would never appear at all. Press and hold
           shows it, as the platform shows a link's address: the trigger's own action
           does not fire — the click that follows a long press is swallowed — and the
           hint stays a moment after the finger lifts, so it can be read with the
           finger out of the way. A tap is unchanged, which is why the hold is longer
           than any tap — and shorter than the platform's own long press, 500ms,
           which selects the trigger's text and shows the loupe: 300ms sits between. */
        var HOLD = 300, LINGER = 1500;
        var holdTimer = null, held = null, lingerTimer = null, swallow = false;

        function release() {
            clearTimeout(holdTimer);
            holdTimer = null;
            if (!held) return;
            var el = held;
            held = null;
            clearTimeout(lingerTimer);
            lingerTimer = setTimeout(function () { if (current === el) hide(); }, LINGER);
        }

        document.addEventListener('pointerdown', function (e) {
            if (e.pointerType !== 'touch') return;
            var el = trigger(e.target);
            if (!el) return;
            clearTimeout(holdTimer);
            holdTimer = setTimeout(function () {
                held = el;
                swallow = true;
                current = el;
                place(el);
            }, HOLD);
        }, true);
        document.addEventListener('pointerup', release, true);
        document.addEventListener('pointercancel', release, true);
        document.addEventListener('pointermove', function (e) {
            // A finger that moves is scrolling, not holding.
            if (e.pointerType === 'touch' && holdTimer && !held) { clearTimeout(holdTimer); holdTimer = null; }
        }, true);
        // The press that showed the hint is not also the press that acts.
        document.addEventListener('click', function (e) {
            if (!swallow) return;
            swallow = false;
            e.preventDefault();
            e.stopPropagation();
        }, true);
        document.addEventListener('contextmenu', function (e) {
            if (held || holdTimer) e.preventDefault();
        });
        window.addEventListener('scroll', hide, true);   // capture: any scroll container
        window.addEventListener('resize', hide);

        var api = {
            gate: null,
            enabled: true,
            hide: hide,
            // A boolean, where the gate is a function: what an app holds in C#.
            setEnabled: function (on) {
                api.enabled = on !== false;
                if (!api.enabled) hide();
            }
        };
        return api;
    })();

})(window.sednaUi);

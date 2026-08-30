/* ═══════════════════════════════════════════════════════════════════════════
   GENERATED FILE — DO NOT EDIT.

   Built by build/bundle-js.sh from src/Sedna.UI/js-parts/. Edit the part
   that owns the behaviour and re-run that script; a guard test fails the build
   if this file and the parts disagree. Adding a part needs no change here —
   the directory is the source of truth.

   Contents, in load order:
     00-core.js
     10-settings.js
     20-tips.js
     21-copy.js
     22-menu.js
     23-tabs.js
     24-palette.js
     25-search.js
     26-dropzone.js
     27-output.js
     28-code-block.js
     29-sheet.js
     30-markdown.js
     40-interop.js
     41-spotlight.js
     50-notify.js
     51-toast.js
     52-confirm.js
   ═══════════════════════════════════════════════════════════════════════════ */

/* ── 00-core.js ──────────────────────────────────────────────── */
/* Sedna.UI — shared browser behaviour.
   ───────────────────────────────────────────────────────────────────────────
   Load at the END of <body>:

     <script src="_content/Sedna.UI/js/Sedna.UI.js"></script>

   configure() is optional:

     <script>sednaUi.configure({ notifyIcon: '/images/logo.png' });</script>

   Everything here is generic UI behaviour. App-specific interop stays in the
   app's own script — do not grow this file with business logic.

   The global is `sednaUi` (the JS-identifier form of the package name; a
   single global cannot contain the dot).
   ─────────────────────────────────────────────────────────────────────────── */
window.sednaUi = window.sednaUi || {};

(function (ui) {

    var config = {
        // localStorage key prefix. localStorage is origin-scoped, so apps on
        // separate domains cannot collide and this needs no changing. Override it
        // only when two apps share one origin under different paths — and then set
        // the same value in data-prefix on the boot script.
        storagePrefix: 'sedna.',
        // Icon used for desktop notifications. null = browser default.
        notifyIcon: null,
        // Also mirror the language into a "<prefix>lang" cookie, so a server-
        // rendered app can prerender in the chosen language.
        langCookie: false
    };

    function key(k) { return config.storagePrefix + k; }

    function readRaw(k) {
        try { return localStorage.getItem(k); } catch (e) { return null; }
    }

    /* How well `needle` matches `haystack`. Returns a score — higher is better —
       or -1 for no match. `penalty` demotes a hit in a secondary field, so a
       command found by its keywords never outranks one found by its name.

       A hand-rolled subsequence matcher, about thirty lines. No fuse.js: this
       package loads nothing at runtime, and a fuzzy matcher good enough for a
       short label is smaller than the argument for taking a dependency.

       Ranking, in the order it matters:
         1. a prefix match            — you typed the start of the name
         2. a word-start match        — "ai" finds "Approve Item"
         3. a contiguous run inside the string
         4. any subsequence, penalised by how spread out it is

       Lives here because the palette and the header search must rank the same
       way. Two matchers would drift, and the second one would be discovered by
       someone finding the same query ordered differently in two places. */
    function score(needle, haystack, penalty) {
        if (!needle) return 1;
        if (!haystack) return -1;

        var n = needle.toLowerCase();
        var h = haystack.toLowerCase();

        var idx = h.indexOf(n);
        if (idx === 0) return 1000 - penalty;                       // prefix
        if (idx > 0) {
            // A run that starts a word beats one buried inside it.
            var wordStart = /[\s\-_/.]/.test(h[idx - 1]);
            return (wordStart ? 800 : 600) - idx - penalty;
        }

        // Subsequence. Track the span it occupies: a match spread across the whole
        // string is a worse match than a tight one, which is what stops "ae"
        // ranking "Approve … escalate" above "Archive entry".
        var first = -1, last = -1, hi = 0;
        for (var ni = 0; ni < n.length; ni++) {
            var found = h.indexOf(n[ni], hi);
            if (found < 0) return -1;
            if (first < 0) first = found;
            last = found;
            hi = found + 1;
        }
        var span = last - first + 1;
        return 400 - (span - n.length) - first - penalty;
    }

    // Shared internals for the other parts. The underscore means exactly one
    // thing: NOT part of the public contract. Nothing outside js-parts/ may read
    // it, and it may change in a patch release. Everything an app is allowed to
    // touch is a named member on `ui` itself.
    ui._ = { config: config, key: key, readRaw: readRaw, score: score };

    ui.configure = function (opts) {
        if (!opts) return;
        Object.keys(opts).forEach(function (k) {
            if (k in config) config[k] = opts[k];
        });
        // Guarded so core-plus-nothing still works for anyone using the parts
        // à la carte; in the shipped bundle settings is always present.
        if (ui.settings) ui.settings.apply();
    };

})(window.sednaUi);

/* ── 10-settings.js ──────────────────────────────────────────────── */
/* ── Theme / accessibility settings ──────────────────────────────────────────
   localStorage is the source of truth; the data-theme / data-variant / data-cvd /
   data-density attributes and the dir attribute on <html> drive the CSS. The boot
   script applies them before first paint; save() keeps them applied.

   data-theme and data-variant are ALWAYS written, never absent — consuming apps
   brand the light palette with `:root[data-variant="light"]`, so that selector has
   to match whenever the light palette is in use. data-theme accepts any theme
   name; "sedna" is the fallback with nothing stored.

   "system" is a real, storable variant preference — not collapsed to dark/light on
   load — so a settings UI can show "follow system" as selected rather than
   whichever side the OS happens to be on right now. matchMedia is guarded: it is
   missing in some embedded webviews, and an exception there must not stop the
   theme applying.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var core = ui._;
    var config = core.config, key = core.key, readRaw = core.readRaw;

    function systemPrefersLight() {
        return !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches);
    }

    ui.settings = {
        load: function () {
            var g = function (k) { return readRaw(key(k)); };
            var v = g('variant');
            return {
                // The document's own language before the browser's: boot.js leaves
                // <html lang> alone unless a choice was stored, so reporting
                // navigator.language here would tell an app's language picker
                // something different from what the page is actually marked as.
                lang:    g('lang') || document.documentElement.lang
                             || (navigator.language || 'en').slice(0, 2).toLowerCase(),
                theme:   g('theme') || 'sedna',
                variant: (v === 'light' || v === 'dark' || v === 'system') ? v : 'dark',
                cvd:     g('cvd') === '1',
                compact: g('density') === 'compact',
                // The document's own direction when nothing is stored, for the same
                // reason as lang above: the host page is the authority until the
                // reader chooses otherwise.
                dir:     g('dir') === 'rtl' ? 'rtl' : (g('dir') === 'ltr' ? 'ltr'
                             : (document.documentElement.dir || 'ltr'))
            };
        },
        save: function (k, value) {
            try { localStorage.setItem(key(k), value); } catch (e) { /* ignore */ }
            if (k === 'lang') {
                if (config.langCookie) {
                    try {
                        document.cookie = key('lang') + '=' + value + ';path=/;max-age=31536000;SameSite=Lax';
                    } catch (e) { /* ignore */ }
                }
                document.documentElement.lang = value;
            }
            this.apply();
        },
        apply: function () {
            var g = function (k) { return readRaw(key(k)); };
            var root = document.documentElement;
            root.setAttribute('data-theme', g('theme') || 'sedna');

            var v = g('variant');
            var variant;
            if (v === 'light' || v === 'dark') variant = v;
            else if (v === 'system') variant = systemPrefersLight() ? 'light' : 'dark';
            else variant = 'dark';
            root.setAttribute('data-variant', variant);

            if (g('cvd') === '1') root.setAttribute('data-cvd', '1');
            else root.removeAttribute('data-cvd');
            if (g('density') === 'compact') root.setAttribute('data-density', 'compact');
            else root.removeAttribute('data-density');
            // Only a stored choice writes dir, and "ltr" is stored explicitly rather
            // than treated as absent — otherwise switching back would delete a dir
            // the host page set for itself. An app whose document is RTL by default
            // says so in its own markup and this leaves it alone.
            var dir = g('dir');
            if (dir === 'rtl' || dir === 'ltr') root.dir = dir;
        }
    };

    // Live tracking: while the stored preference is literally "system", the
    // variant follows the OS without a reload. A stored "dark" or "light" is an
    // explicit choice and must not be disturbed by this listener.
    try {
        if (window.matchMedia) {
            window.matchMedia('(prefers-color-scheme: light)').addEventListener('change', function () {
                if (readRaw(key('variant')) === 'system') ui.settings.apply();
            });
        }
    } catch (e) { /* ignore */ }

})(window.sednaUi);

/* ── 20-tips.js ──────────────────────────────────────────────── */
/* ── Hover hints (data-tip) ──────────────────────────────────────────────────
   One floating bubble, driven by [data-tip] through delegation on document — so
   it covers content rendered after load (Blazor re-renders) with no re-wiring.
   The bubble is appended to <body> and fixed-positioned, so a card's or table's
   overflow never clips it (a pure-CSS ::after tooltip is clipped).

   Elements inside .sidebar are skipped: the collapsed rail has its own CSS
   flyout, and both firing would double the tooltip.
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
            return el;
        }

        function place(el) {
            var tip = el.getAttribute('data-tip');
            if (!tip) return hide();
            // Redundant when the trigger's own visible text already spells out
            // the whole hint (innerText respects CSS visibility, so a hidden
            // label correctly counts as absent).
            var vis = (el.innerText || '').trim();
            if (vis && vis.indexOf(tip) !== -1) return hide();

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
        document.addEventListener('mousedown', hide);    // a click dismisses its own hint
        window.addEventListener('scroll', hide, true);   // capture: any scroll container
        window.addEventListener('resize', hide);

        var api = { gate: null, hide: hide };
        return api;
    })();

})(window.sednaUi);

/* ── 21-copy.js ──────────────────────────────────────────────── */
/* ── Declarative copy-to-clipboard ───────────────────────────────────────────
   Put data-copy on a button and the click is handled for you:

     data-copy="literal text"      copies that text
     data-copy-target="#sel"       copies that element's textContent
     data-copy-target              (empty) copies the nearest .code-block's <pre>

   Delegated from document, so a button rendered by a later Blazor render works with
   no wiring — and nothing has to be re-bound on every render, which is how per-
   element handlers leak.

   The confirmation is swapped into the button and put back after 1.4s. The original
   HTML is stashed on the element rather than in a closure, so two rapid clicks
   cannot restore a "Copied" label as if it were the original.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var RESTORE_MS = 1400;

    function textFor(btn) {
        if (btn.hasAttribute('data-copy')) return btn.getAttribute('data-copy') || '';

        var sel = btn.getAttribute('data-copy-target');
        var node = sel
            ? document.querySelector(sel)
            // The <pre> of the code block this button belongs to.
            : (btn.closest('.code-block') || document).querySelector('pre');

        return node ? (node.innerText || node.textContent || '') : '';
    }

    function flash(btn, ok) {
        // Only stash on the first click; a second click mid-flash must not stash
        // the confirmation as the thing to restore.
        if (btn.dataset.copyOriginal === undefined) {
            btn.dataset.copyOriginal = btn.innerHTML;
        }
        clearTimeout(+btn.dataset.copyTimer || 0);

        btn.innerHTML = ok
            ? '<i class="ri-check-line"></i><span>Copied</span>'
            : '<i class="ri-error-warning-line"></i><span>Copy failed</span>';

        btn.dataset.copyTimer = setTimeout(function () {
            btn.innerHTML = btn.dataset.copyOriginal;
            delete btn.dataset.copyOriginal;
            delete btn.dataset.copyTimer;
        }, RESTORE_MS);
    }

    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-copy], [data-copy-target]');
        if (!btn) return;

        var text = textFor(btn);
        if (!text) return;

        e.preventDefault();
        // copyText resolves false rather than throwing, so the button always
        // reports what actually happened.
        ui.copyText(text).then(function (ok) { flash(btn, ok); });
    });

})(window.sednaUi);

/* ── 22-menu.js ──────────────────────────────────────────────── */
/* ── Menus, delegated ────────────────────────────────────────────────────────
   Opt-in wiring for the .menu panel, so a plain HTML page (or a Razor page that
   would rather not hold the state) gets a working dropdown:

     <div class="menu-anchor">
       <button data-menu-toggle aria-expanded="false">Actions</button>
       <div class="menu" hidden>…</div>
     </div>

   The `hidden` attribute is the closed state, not a class: a panel that is
   `hidden` is out of the accessibility tree and out of the tab order, which a
   `display:none` class also achieves but an `opacity:0` one does not.

   Delegated from document, so a menu rendered by a later Blazor render works with
   nothing re-bound. The frame's own user menu does NOT use this: it holds its state
   in C#, because the frame has to work with scripting blocked.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function panelOf(toggle) {
        var anchor = toggle.closest('.menu-anchor');
        return anchor ? anchor.querySelector('.menu') : null;
    }

    function setOpen(toggle, panel, open) {
        panel.hidden = !open;
        toggle.setAttribute('aria-expanded', String(open));
    }

    function closeAll(except) {
        var toggles = document.querySelectorAll('[data-menu-toggle][aria-expanded="true"]');
        for (var i = 0; i < toggles.length; i++) {
            if (toggles[i] === except) continue;
            var panel = panelOf(toggles[i]);
            if (panel) setOpen(toggles[i], panel, false);
        }
    }

    ui.menu = {
        // Closes every open menu. An app calls this after navigating, so a menu does
        // not survive into a back-navigation from the browser cache.
        closeAll: function () { closeAll(null); }
    };

    document.addEventListener('click', function (e) {
        var toggle = e.target.closest('[data-menu-toggle]');

        if (toggle) {
            var panel = panelOf(toggle);
            if (!panel) return;
            e.preventDefault();
            var open = toggle.getAttribute('aria-expanded') === 'true';
            // Only one menu open at a time: two panels overlapping is never wanted,
            // and the second one silently covers the first.
            closeAll(toggle);
            setOpen(toggle, panel, !open);
            return;
        }

        // A click inside a panel that is not on an item leaves it open; a click on
        // an item closes it, because the item did something.
        var item = e.target.closest('.menu-item');
        if (item) { closeAll(null); return; }
        if (e.target.closest('.menu')) return;

        closeAll(null);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var open = document.querySelector('[data-menu-toggle][aria-expanded="true"]');
        if (!open) return;
        closeAll(null);
        // Focus goes back to the control that opened it. Without this, focus is left
        // on a node that has just been hidden and the next Tab starts from the top
        // of the document.
        try { open.focus(); } catch (err) { /* detached */ }
    });

})(window.sednaUi);

/* ── 23-tabs.js ──────────────────────────────────────────────── */
/* ── Tabs, delegated ─────────────────────────────────────────────────────────
   Opt-in wiring for .tabs, and the reason it exists is the keyboard: the CSS can
   colour a selected tab, but arrow-key movement between tabs and the single-stop
   tab order are behaviour, and a tablist without them is a tablist in name only.

   Add data-tabs to the .tabs container. Each tab needs role="tab",
   aria-controls="<panel id>" and aria-selected; each panel needs role="tabpanel"
   and a matching id.

     <div class="tabs" role="tablist" data-tabs>
       <button class="tab" role="tab" aria-selected="true"  aria-controls="p1">Open</button>
       <button class="tab" role="tab" aria-selected="false" aria-controls="p2">All</button>
     </div>
     <div class="tab-panel" role="tabpanel" id="p1">…</div>
     <div class="tab-panel" role="tabpanel" id="p2" hidden>…</div>

   An app that drives the tabs from C# should NOT add data-tabs — it would then have
   two things setting aria-selected. Wire the arrow keys in the component instead.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function tabsIn(list) {
        return Array.prototype.filter.call(
            list.querySelectorAll('[role="tab"]'),
            function (t) { return !t.disabled && t.getAttribute('aria-disabled') !== 'true'; });
    }

    function select(list, tab) {
        var all = list.querySelectorAll('[role="tab"]');
        for (var i = 0; i < all.length; i++) {
            var isIt = all[i] === tab;
            all[i].setAttribute('aria-selected', String(isIt));
            // Roving tabindex: only the selected tab is a tab stop, so Tab moves past
            // the whole tablist rather than through every tab in it.
            all[i].tabIndex = isIt ? 0 : -1;

            var panel = document.getElementById(all[i].getAttribute('aria-controls') || '');
            if (panel) panel.hidden = !isIt;
        }
    }

    ui.tabs = {
        // Selects a tab programmatically, by element or by its aria-controls id.
        select: function (tabOrPanelId) {
            var tab = typeof tabOrPanelId === 'string'
                ? document.querySelector('[role="tab"][aria-controls="' + tabOrPanelId + '"]')
                : tabOrPanelId;
            var list = tab && tab.closest('[data-tabs]');
            if (list) select(list, tab);
        }
    };

    document.addEventListener('click', function (e) {
        var tab = e.target.closest('[data-tabs] [role="tab"]');
        if (!tab || tab.disabled) return;
        e.preventDefault();
        select(tab.closest('[data-tabs]'), tab);
    });

    document.addEventListener('keydown', function (e) {
        var tab = e.target.closest('[data-tabs] [role="tab"]');
        if (!tab) return;

        var list = tab.closest('[data-tabs]');
        var tabs = tabsIn(list);
        var at = tabs.indexOf(tab);
        if (at < 0) return;

        // Home/End as well as the arrows: with a dozen tabs, holding an arrow key to
        // reach the last one is the kind of thing that makes people use a mouse.
        var to = -1;
        if (e.key === 'ArrowRight' || e.key === 'ArrowDown') to = (at + 1) % tabs.length;
        else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') to = (at - 1 + tabs.length) % tabs.length;
        else if (e.key === 'Home') to = 0;
        else if (e.key === 'End') to = tabs.length - 1;
        else return;

        e.preventDefault();
        select(list, tabs[to]);
        tabs[to].focus();
    });

})(window.sednaUi);

/* ── 24-palette.js ──────────────────────────────────────────────── */
/* ── Command palette ─────────────────────────────────────────────────────────
   sednaUi.palette.register([{ label, icon, group, note, run, keywords }])
   sednaUi.palette.open()      — or Ctrl/Cmd-K, which is wired for you

   The scorer is ui._.score in 00-core.js, shared with the header search so the
   two cannot rank the same query differently. Matches in `keywords` score below
   the same match in the label, so a command is never outranked by one that
   merely mentions the word.

   Everything is built from the classes in css-parts/64-palette-spotlight.css, so a
   palette opened by this looks exactly like the one the catalogue documents.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var score = ui._.score;

    var commands = [];
    var dialog = null;
    var input = null;
    var list = null;
    var shown = [];      // the currently visible commands, in ranked order
    var at = 0;          // index into shown

    function rank(query) {
        var out = [];
        for (var i = 0; i < commands.length; i++) {
            var c = commands[i];
            var best = score(query, c.label, 0);
            if (c.keywords) {
                // 200 keeps a keyword hit strictly below the same hit in a label.
                var k = score(query, c.keywords, 200);
                if (k > best) best = k;
            }
            if (best >= 0) out.push({ c: c, s: best, i: i });
        }
        // Stable: equal scores keep registration order, so the list does not
        // reshuffle between keystrokes that do not change the ranking.
        out.sort(function (a, b) { return b.s - a.s || a.i - b.i; });
        return out.map(function (r) { return r.c; });
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    function render(query) {
        shown = rank(query);
        at = 0;
        list.textContent = '';

        if (!shown.length) {
            var empty = el('li');
            empty.setAttribute('role', 'presentation');
            // Says what was searched, not just "no results" — the reader needs to
            // know the query was what they thought it was.
            empty.appendChild(el('div', 'palette-empty',
                query ? 'Nothing matches “' + query + '”.' : 'No commands registered.'));
            list.appendChild(empty);
            input.removeAttribute('aria-activedescendant');
            return;
        }

        var lastGroup = null;
        shown.forEach(function (c, i) {
            // Groups are only meaningful in registration order, so they are dropped
            // once a query has reordered the list — a heading over unrelated results
            // is worse than no heading.
            if (!query && c.group && c.group !== lastGroup) {
                // role="presentation" is load-bearing: a listbox may only own
                // options, and a bare <li> here breaks aria-required-children — while
                // also ceasing to be a listitem, because the <ul> is no longer a list.
                // Presentation makes the <li> transparent to both rules.
                var head = el('li');
                head.setAttribute('role', 'presentation');
                head.appendChild(el('div', 'palette-group', c.group));
                list.appendChild(head);
                lastGroup = c.group;
            }

            // role="option" on a <div>, not a <button>: an option is not a button,
            // and being one inside a listbox is what makes aria-selected and
            // aria-activedescendant legal. Activated by click and by the input's
            // Enter handler, never by receiving focus.
            var li = el('li');
            li.setAttribute('role', 'presentation');
            var btn = el('div', 'palette-item');
            btn.setAttribute('role', 'option');
            btn.id = 'sedna-palette-' + i;
            btn.setAttribute('aria-selected', String(i === 0));

            if (c.icon) {
                var icon = el('i', c.icon);
                icon.setAttribute('aria-hidden', 'true');
                btn.appendChild(icon);
            }
            btn.appendChild(document.createTextNode(c.label));
            if (c.note) btn.appendChild(el('span', 'palette-item-note', c.note));

            btn.addEventListener('click', function () { run(i); });
            li.appendChild(btn);
            list.appendChild(li);
        });

        input.setAttribute('aria-activedescendant', 'sedna-palette-0');
    }

    function items() { return list.querySelectorAll('.palette-item'); }

    function highlight(next) {
        var all = items();
        if (!all.length) return;

        at = (next + all.length) % all.length;
        for (var i = 0; i < all.length; i++) {
            all[i].setAttribute('aria-selected', String(i === at));
        }
        input.setAttribute('aria-activedescendant', all[at].id);
        // Keeps the highlight in view without moving focus, which stays in the input
        // so typing continues to work — that is the whole reason for
        // aria-activedescendant rather than moving focus down the list.
        all[at].scrollIntoView({ block: 'nearest' });
    }

    function run(i) {
        var c = shown[i];
        close();
        // After close(), so a command that opens a modal is not fighting a dialog
        // that is still shutting.
        if (!c) return;
        // `run` first: a command that has both is doing something more than
        // navigating, and `href` is then only there for a middle-click.
        if (typeof c.run === 'function') c.run();
        // `href` is what a command registered from C# uses. A callback cannot cross
        // that boundary — the library never calls back into .NET — so navigation is
        // the one action a serialisable command can carry.
        else if (c.href) go(c.href);
    }

    /* Navigates by CLICKING a real anchor, never by assigning window.location.

       An assignment is always a full page load. In a Blazor Server app that tears
       down the SignalR circuit and builds a new one, so every scoped service is
       re-created — a demo mode, a guided tour, a queue selection, an unsaved form
       all end silently, because a palette command is the last thing anyone suspects.
       Blazor's router, and every other SPA router, intercepts a click on a
       same-origin <a> and routes it client-side instead.

       On a plain server-rendered page the two are indistinguishable: no router
       listens, and the click navigates exactly as the assignment did. So this is
       never worse and is sometimes the difference between a working app and a
       mystery.

       The anchor has to be IN the document for the click to navigate — a detached
       one is inert — and it is removed immediately afterwards. It carries no text
       and is never focused, so nothing sees it. */
    function go(href) {
        var a = document.createElement('a');
        a.href = href;
        document.body.appendChild(a);
        a.click();
        a.remove();
    }

    function build() {
        dialog = document.createElement('dialog');
        dialog.className = 'palette';

        input = el('input', 'palette-input');
        input.type = 'text';
        input.placeholder = 'Search commands…';
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-expanded', 'true');
        input.setAttribute('aria-controls', 'sedna-palette-list');
        input.setAttribute('aria-label', 'Search commands');
        input.setAttribute('autocomplete', 'off');

        list = el('ul', 'palette-list');
        list.id = 'sedna-palette-list';
        // A real listbox owned by the combobox input. Claimed because the keyboard
        // contract behind it is implemented in full below: arrows, Home/End, Enter,
        // and the highlight moving while focus stays in the input. axe rejects
        // aria-selected on a plain button, correctly — the attribute means nothing
        // without the role.
        list.setAttribute('role', 'listbox');
        list.setAttribute('aria-label', 'Commands');

        var foot = el('div', 'palette-footer');
        foot.innerHTML =
            '<span><span class="kbd">&uarr;</span> <span class="kbd">&darr;</span> to move</span>' +
            '<span><span class="kbd">Enter</span> to run</span>' +
            '<span><span class="kbd">Esc</span> to close</span>';

        dialog.append(input, list, foot);
        document.body.appendChild(dialog);

        input.addEventListener('input', function () { render(input.value); });

        input.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); highlight(at + 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(at - 1); }
            else if (e.key === 'Home') { e.preventDefault(); highlight(0); }
            else if (e.key === 'End') { e.preventDefault(); highlight(items().length - 1); }
            else if (e.key === 'Enter') { e.preventDefault(); if (items().length) run(at); }
        });

        // Clicking the backdrop closes it. The dialog fills only part of the top
        // layer, so a click whose target IS the dialog element landed outside the
        // panel's own children.
        dialog.addEventListener('click', function (e) { if (e.target === dialog) close(); });
    }

    function close() {
        if (dialog && dialog.open) dialog.close();
    }

    ui.palette = {
        /* Replaces the whole command list. An app calls this whenever what is
           available changes — after a permission check, or on navigation. */
        register: function (list_) {
            commands = Array.isArray(list_) ? list_.slice() : [];
        },

        open: function () {
            if (!dialog) build();
            if (dialog.open) return true;

            input.value = '';
            render('');
            dialog.showModal();
            input.focus();
            return true;
        },

        close: close,

        /* Exposed for tests and for an app that wants the same ranking in its own
           UI. Returns the matching commands, best first. */
        rank: rank
    };

    document.addEventListener('keydown', function (e) {
        // metaKey for macOS, ctrlKey everywhere else. Checking both rather than
        // sniffing the platform: a Mac user on an external PC keyboard uses Ctrl.
        if (e.key !== 'k' && e.key !== 'K') return;
        if (!e.ctrlKey && !e.metaKey) return;
        if (!commands.length) return;      // nothing registered: leave the browser's own binding alone

        e.preventDefault();
        ui.palette.open();
    });

})(window.sednaUi);

/* ── 25-search.js ──────────────────────────────────────────────── */
/* ── Header search, delegated ────────────────────────────────────────────────
   The topbar's free-text box. Register what is searchable once, write the box in
   markup, and the dropdown, the ranking, the keyboard and the clear button come
   from here:

     sednaUi.search.register([
       { title, meta, code, tag, tone, href, keywords }, …
     ]);

     <div class="search">
       <i class="ri-search-line search-icon"></i>
       <input class="search-input" type="search" data-search placeholder="Search…">
       <button class="search-clear" type="button" aria-label="Clear"><i class="ri-close-line"></i></button>
     </div>

   Only `title` is required. `href` is where choosing the result goes; an item
   without one is inert unless it carries a `run` callback, which only a source
   registered from JavaScript can have — the library never calls back into .NET.

   THE INDEX IS CLIENT-SIDE, and that is the whole design. A per-keystroke round trip
   is an app's decision to make, not a shared library's: it needs a debounce whose
   length depends on the backend, a cancellation story for superseded keystrokes, and a
   busy state. An app searching a database renders its own results with these classes
   and leaves data-search off the input.

   What this file writes to the DOM is one panel, appended to <body>. Nothing is
   inserted into the box itself, so a framework that owns that subtree — Blazor
   does — cannot revert it. The clear button is app markup shown by CSS on
   :placeholder-shown, so it works with this script blocked.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var score = ui._.score;

    // Eight rows is what fits the panel's 360px before it scrolls. A ninth result
    // nobody scrolls to is not a result; the count of what was cut is shown
    // instead, because a silently truncated list reads as "that is everything".
    var MAX = 8;

    var items = [];
    var panel = null;      // the dropdown, in <body>
    var list = null;
    var box = null;        // the .search the panel is currently anchored to
    var input = null;
    var shown = [];
    var at = 0;
    var total = 0;         // matches before the cut, for the "+N more" line

    /* Every field, worst penalty last. A hit in the title always beats the same
       hit in a secondary field, which is why the demotions are this coarse: they
       are far apart, so no combination of a long field and a short one crosses
       them.

       THE SUBSEQUENCE MATCHER ONLY RUNS ON THE TITLE. A secondary field is long —
       an example's keywords are every class it writes — and a subsequence over a
       kilobyte of text matches nearly everything, at scores that mean nothing.
       Requiring a real substring there is what stops an unrelated card example
       outranking the page the reader asked for. */
    function best(item, term) {
        var s = score(term, item.title, 0);

        var fields = [[item.code, 150], [item.keywords, 250], [item.meta, 350]];
        for (var i = 0; i < fields.length; i++) {
            var text = fields[i][0];
            if (!text || text.toLowerCase().indexOf(term) < 0) continue;
            var other = score(term, text, fields[i][1]);
            if (other > s) s = other;
        }
        return s;
    }

    /* Multi-word queries are AND over the terms, scored as their mean — so a
       two-word query and a five-word one are comparable, and an item matching
       only half the words is not a result at all. The whole query is scored as
       one string first: an item whose title contains the literal phrase must
       beat one that merely holds both words somewhere. */
    function rank(query) {
        var q = (query || '').trim().toLowerCase();
        if (!q) return [];

        var terms = q.split(/\s+/);
        var out = [];

        for (var i = 0; i < items.length; i++) {
            var s = best(items[i], q);

            if (terms.length > 1) {
                var sum = 0, all = true;
                for (var t = 0; t < terms.length; t++) {
                    var each = best(items[i], terms[t]);
                    if (each < 0) { all = false; break; }
                    sum += each;
                }
                // 50 below the phrase, so scattered words never tie with the
                // phrase itself.
                if (all) {
                    var mean = sum / terms.length - 50;
                    if (mean > s) s = mean;
                }
            }

            if (s >= 0) out.push({ item: items[i], s: s, i: i });
        }

        // Stable: equal scores keep registration order, so the list does not
        // reshuffle between keystrokes that do not change the ranking.
        out.sort(function (a, b) { return b.s - a.s || a.i - b.i; });
        return out.map(function (r) { return r.item; });
    }

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }

    function build() {
        // sedna-scroll, because the panel scrolls past eight rows and the OS default
        // bar is the one thing on it that would not follow the theme.
        panel = el('div', 'search-panel sedna-scroll');
        panel.id = 'sedna-search-panel';
        panel.hidden = true;

        list = el('div');
        list.id = 'sedna-search-list';
        // A real listbox owned by the input as a combobox. The claim is made only
        // because the keyboard contract behind it is implemented in full below:
        // arrows, Home/End, Enter, and the highlight moving while focus stays in
        // the input.
        list.setAttribute('role', 'listbox');
        panel.appendChild(list);

        document.body.appendChild(panel);

        // mousedown, not click: the default would blur the input before the click
        // lands, and the focusout handler would close the panel out from under the
        // pointer. Preventing it keeps focus where the combobox pattern wants it.
        panel.addEventListener('mousedown', function (e) { e.preventDefault(); });
    }

    function place() {
        if (!box || !panel || panel.hidden) return;
        var r = box.getBoundingClientRect();
        panel.style.top = (r.bottom + 6) + 'px';
        panel.style.left = r.left + 'px';
        panel.style.width = r.width + 'px';
    }

    function row(item, i) {
        // An <a> when the result navigates, so the browser's own affordances come
        // with it — middle-click, "open in new tab", and a framework router that
        // intercepts internal links to navigate without a reload.
        var node = el(item.href ? 'a' : 'div', 'search-item');
        if (item.href) node.href = item.href;
        node.setAttribute('role', 'option');
        node.setAttribute('aria-selected', String(i === 0));
        node.setAttribute('tabindex', '-1');
        node.id = 'sedna-search-item-' + i;
        if (i === 0) node.classList.add('search-item--sel');

        node.appendChild(el('span', 'search-item-title', item.title));

        if (item.code || item.meta || item.tag) {
            var meta = el('span', 'search-item-meta');
            if (item.code) meta.appendChild(el('span', 'text-mono', item.code));
            if (item.meta) meta.appendChild(el('span', null, item.meta));
            if (item.tag) {
                meta.appendChild(el(
                    'span',
                    'search-tag' + (item.tone === 'warn' ? ' search-tag--warn' : ''),
                    item.tag));
            }
            node.appendChild(meta);
        }

        node.addEventListener('click', function (e) { pick(i, e); });
        node.addEventListener('mouseenter', function () { highlight(i); });
        return node;
    }

    function render(query) {
        if (!panel) build();

        var all = rank(query);
        total = all.length;
        shown = all.slice(0, MAX);
        at = 0;
        list.textContent = '';

        if (!shown.length) {
            // Says what was searched rather than just "no results": the reader
            // needs to see the query was what they thought it was.
            list.appendChild(el('div', 'search-status', 'Nothing matches “' + query + '”.'));
            input.removeAttribute('aria-activedescendant');
        } else {
            for (var i = 0; i < shown.length; i++) list.appendChild(row(shown[i], i));
            var cut = total - shown.length;
            if (cut > 0) {
                list.appendChild(el('div', 'search-status',
                    cut + (cut === 1 ? ' more match' : ' more matches') + '. Keep typing to narrow it down.'));
            }
            input.setAttribute('aria-activedescendant', 'sedna-search-item-0');
        }

        open();
    }

    function open() {
        if (!panel || !panel.hidden) { place(); return; }
        panel.hidden = false;
        input.setAttribute('aria-expanded', 'true');
        place();
    }

    function close() {
        if (!panel || panel.hidden) return;
        panel.hidden = true;
        if (input) {
            input.setAttribute('aria-expanded', 'false');
            input.removeAttribute('aria-activedescendant');
        }
    }

    function rows() { return list ? list.querySelectorAll('.search-item') : []; }

    function highlight(next) {
        var all = rows();
        if (!all.length) return;

        at = (next + all.length) % all.length;
        for (var i = 0; i < all.length; i++) {
            all[i].setAttribute('aria-selected', String(i === at));
            all[i].classList.toggle('search-item--sel', i === at);
        }
        input.setAttribute('aria-activedescendant', all[at].id);
        // Keeps the highlight in view without moving focus, which stays in the
        // input so typing continues to work — the reason for aria-activedescendant
        // rather than walking focus down the list.
        all[at].scrollIntoView({ block: 'nearest' });
    }

    function pick(i, e) {
        var item = shown[i];
        // Resolved before close(), which is what a keyboard Enter needs: it has no
        // event of its own to let through, so it clicks the row instead.
        var node = (!e && item && item.href) ? list.querySelector('#sedna-search-item-' + i) : null;
        close();
        // The query is spent. Left in place it would survive a router navigation
        // and not a full page load, so the box would sometimes hold the last
        // search and sometimes not — and re-focusing it would reopen results for
        // the page the reader has just left. Focus is NOT taken back: it belongs
        // to wherever the result went.
        reset(input);
        if (!item) return;

        // `run` first: an item that has both is doing something more than
        // navigating, and `href` is then only there for a middle-click.
        if (typeof item.run === 'function') {
            if (e) e.preventDefault();
            item.run();
            return;
        }
        // A real click on the <a> already navigates, and letting it through is what
        // gives a router the chance to intercept it and skip the page load.
        if (node && node.click) node.click();
    }

    /* Empties the box the way a user would, so a framework binding sees it.
       Assigning .value alone is invisible to Blazor's @bind and to any other
       listener — the event is the part that matters. */
    function reset(target) {
        if (!target || !target.value) return;
        target.value = '';
        target.dispatchEvent(new Event('input', { bubbles: true }));
    }

    /* The clear button and Escape: the box empties and the reader carries on
       typing in it, so focus goes back. */
    function clear(target) {
        reset(target);
        target.focus();
        close();
    }

    function inside(node) {
        if (!(node instanceof Element)) return false;
        return !!(node.closest('.search') || node.closest('.search-panel'));
    }

    ui.search = {
        /* Replaces the whole searchable list. An app calls this once, or whenever
           what is searchable changes. */
        register: function (list_) {
            items = Array.isArray(list_) ? list_.slice() : [];
            // A list that shrank while a panel was open would leave results on
            // screen that no longer exist.
            close();
        },

        /* Exposed for tests, and for an app that wants this ranking in its own UI.
           Returns every match, best first — the panel's own cut is not applied. */
        rank: rank,

        close: close
    };

    /* Adopts a box as the active one and claims the combobox role on it. The role
       is set here rather than asked of the markup because it is a promise about
       behaviour — arrows, Enter, aria-activedescendant — and only this file can
       keep it. An app whose box is never reached by this code keeps a plain
       input, which is the honest markup for one. */
    function adopt(target) {
        input = target;
        box = target.closest('.search') || target;
        if (!panel) build();
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-controls', 'sedna-search-list');
        input.setAttribute('aria-autocomplete', 'list');
    }

    document.addEventListener('input', function (e) {
        var target = e.target;
        if (!(target instanceof Element) || !target.matches('[data-search]')) return;
        // Nothing registered: the box is somebody else's, rendering its own results
        // with these classes. Saying "nothing matches" over them would be a lie.
        if (!items.length) return;

        adopt(target);
        if (!target.value.trim()) { close(); return; }
        render(target.value);
    });

    document.addEventListener('keydown', function (e) {
        var target = e.target;
        if (!(target instanceof Element) || !target.matches('[data-search]')) return;

        if (e.key === 'Escape') {
            // Escape on an open panel closes it; on a closed one it clears the box,
            // so the same key always undoes the last thing that happened.
            //
            // preventDefault in BOTH branches, because a type="search" input has a
            // native Escape that empties it. Without this the first Escape would
            // close the panel and throw the query away in the same keystroke — the
            // browser's default runs after this handler, not instead of it.
            e.preventDefault();
            if (panel && !panel.hidden) close();
            else if (target.value) clear(target);
            return;
        }

        if (!panel || panel.hidden || !rows().length) return;

        if (e.key === 'ArrowDown') { e.preventDefault(); highlight(at + 1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(at - 1); }
        else if (e.key === 'Home') { e.preventDefault(); highlight(0); }
        else if (e.key === 'End') { e.preventDefault(); highlight(rows().length - 1); }
        else if (e.key === 'Enter') { e.preventDefault(); pick(at, null); }
    });

    document.addEventListener('click', function (e) {
        var target = e.target;
        if (!(target instanceof Element)) return;

        var button = target.closest('.search-clear');
        if (!button) return;
        var field = button.closest('.search');
        var control = field && field.querySelector('.search-input, [data-search]');
        if (control) { e.preventDefault(); clear(control); }
    });

    // Re-opening a box that still holds a query shows what it found last time,
    // rather than an empty panel the reader has to retype into.
    document.addEventListener('focusin', function (e) {
        var target = e.target;
        if (target instanceof Element && target.matches('[data-search]')) {
            if (!items.length || !target.value.trim()) return;
            adopt(target);
            render(target.value);
            return;
        }
        if (!inside(target)) close();
    });

    document.addEventListener('pointerdown', function (e) {
        if (!inside(e.target)) close();
    });

    // The panel is fixed to a box that can move under it: a window resize, or a
    // .search placed somewhere that scrolls. Capture, because the scroll happens
    // on an ancestor and does not bubble.
    window.addEventListener('resize', place);
    document.addEventListener('scroll', place, true);

})(window.sednaUi);

/* ── 26-dropzone.js ──────────────────────────────────────────────── */
/* ── Dropzone, delegated ─────────────────────────────────────────────────────
   Opt-in wiring for .dropzone, because there is no CSS pseudo-class for "something
   is being dragged over me" and it is the same fifteen lines in every app:

     <label class="dropzone" data-dropzone>
       <i class="ri-upload-cloud-2-line"></i>
       <span>Drop files here, or click to choose</span>
       <input type="file" multiple hidden />
     </label>

   Two things here are easy to get wrong, so they are done once.

   First, `dragleave` fires when the pointer moves onto a CHILD of the zone, so an
   "add on enter, remove on leave" pair flickers and then sticks in the wrong state as
   soon as the zone has an icon and a label inside it. The fix is a depth counter, held
   on the element so two zones on a page cannot confuse each other.

   Second, `dragover` MUST have its default prevented or the browser refuses the
   drop and then navigates to the dropped file — losing the page, which is a
   spectacular failure for a form.

   On drop the files are put into the zone's own <input type="file"> and a bubbling
   `change` event is dispatched, so the app's existing handler — including Blazor's
   InputFile — sees a dropped file exactly as it sees a chosen one, and there is
   nothing extra to bind. Nothing here calls into .NET.

   Delegated from document, so a zone rendered by a later render works unwired.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var DEPTH = '_drDropDepth';
    var OVER = 'dropzone--over';

    function zoneOf(target) {
        return target instanceof Element ? target.closest('.dropzone[data-dropzone]') : null;
    }

    function setOver(zone, on) {
        zone.classList.toggle(OVER, on);
        if (!on) zone[DEPTH] = 0;
    }

    ui.dropzone = {
        // Clears the highlight on every zone. An app calls this if it tears a zone
        // down mid-drag, when no dragleave or drop will ever arrive.
        reset: function () {
            var zones = document.querySelectorAll('.dropzone.' + OVER);
            for (var i = 0; i < zones.length; i++) setOver(zones[i], false);
        }
    };

    document.addEventListener('dragenter', function (e) {
        var zone = zoneOf(e.target);
        if (!zone) return;
        e.preventDefault();
        zone[DEPTH] = (zone[DEPTH] || 0) + 1;
        zone.classList.add(OVER);
    });

    document.addEventListener('dragover', function (e) {
        var zone = zoneOf(e.target);
        if (!zone) return;
        // Without this the drop is refused and the browser opens the file, replacing
        // the page.
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
    });

    document.addEventListener('dragleave', function (e) {
        var zone = zoneOf(e.target);
        if (!zone) return;
        zone[DEPTH] = (zone[DEPTH] || 1) - 1;
        if (zone[DEPTH] <= 0) setOver(zone, false);
    });

    document.addEventListener('drop', function (e) {
        var zone = zoneOf(e.target);
        if (!zone) return;
        e.preventDefault();
        setOver(zone, false);

        var input = zone.querySelector('input[type="file"]');
        if (!input || !e.dataTransfer || !e.dataTransfer.files.length) return;

        try {
            // Assigning a FileList is only possible through DataTransfer, and only
            // this way round: input.files = e.dataTransfer.files works in Chromium
            // and is not universally settable, so the list is rebuilt.
            var transfer = new DataTransfer();
            var files = e.dataTransfer.files;
            var many = input.multiple ? files.length : Math.min(1, files.length);
            for (var i = 0; i < many; i++) transfer.items.add(files[i]);
            input.files = transfer.files;
        } catch (err) {
            return;    // no DataTransfer constructor: the drop simply does nothing
        }

        // Bubbling, so a delegated handler and Blazor's InputFile both see it. The
        // app's change handler is the one place that knows what a file means here.
        input.dispatchEvent(new Event('change', { bubbles: true }));
    });

})(window.sednaUi);

/* ── 27-output.js ──────────────────────────────────────────────── */
/* ── Output pane, follow-tail ─────────────────────────────────────────────────
   Opt-in wiring for .output, so a pane that is being appended to stays on the newest
   line:

     <ul class="output" data-follow>…</ul>

   Following is a mode, not an action. The pane sticks to the bottom while the reader
   is at the bottom, releases the moment they scroll up to read something, and
   re-attaches when they scroll back down. Without the release, reading anything in a
   live stream is impossible; without the re-attach, it never resumes and the reader
   has to reload.

   "At the bottom" is measured with a tolerance, because scrollHeight, clientHeight and
   scrollTop are fractional on a scaled display and an exact comparison is false as
   often as it is true.

   A MutationObserver rather than a call the app makes after each append: the app is
   Blazor, and the lines arrive from a render rather than from code that could call
   anything. One observer per pane, created the first time the pane is seen.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var BOUND = '_drFollowBound';
    var TOLERANCE = 4;

    function atBottom(pane) {
        return pane.scrollHeight - pane.clientHeight - pane.scrollTop <= TOLERANCE;
    }

    function toBottom(pane) {
        pane.scrollTop = pane.scrollHeight;
    }

    function bind(pane) {
        if (pane[BOUND]) return;
        pane[BOUND] = true;

        // Starts attached, so a pane rendered with history already in it opens on the
        // newest line rather than the oldest.
        var following = true;
        toBottom(pane);

        pane.addEventListener('scroll', function () { following = atBottom(pane); });

        var observer = new MutationObserver(function () {
            if (following) toBottom(pane);
        });
        observer.observe(pane, { childList: true, subtree: true, characterData: true });
    }

    ui.output = {
        /* Scrolls a pane to its newest line and re-attaches following. For a "jump to
           latest" button, and for an app that appends outside the DOM the observer
           watches. */
        follow: function (pane) {
            if (!pane) return;
            bind(pane);
            toBottom(pane);
        },

        /* Whether the reader is on the newest line — for showing that button only when
           it would do something. */
        isFollowing: function (pane) { return !!pane && atBottom(pane); }
    };

    function bindAll(root) {
        var panes = (root || document).querySelectorAll('.output[data-follow]');
        for (var i = 0; i < panes.length; i++) bind(panes[i]);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { bindAll(document); });
    } else {
        bindAll(document);
    }

    // A pane rendered later — by a Blazor render, by a modal opening — is picked up
    // here. Watching the document for added panes is the same delegation the rest of
    // this script uses, expressed the only way a scroll container allows.
    new MutationObserver(function (records) {
        for (var i = 0; i < records.length; i++) {
            var added = records[i].addedNodes;
            for (var j = 0; j < added.length; j++) {
                if (added[j].nodeType !== 1) continue;
                if (added[j].matches && added[j].matches('.output[data-follow]')) bind(added[j]);
                bindAll(added[j]);
            }
        }
    }).observe(document.documentElement, { childList: true, subtree: true });

})(window.sednaUi);

/* ── 28-code-block.js ──────────────────────────────────────────────── */
/* ── Code block, expand a clamped one ────────────────────────────────────────
   A `.code-block--clamped` is bounded to --code-clamp and scrolls. This is the
   control that opens it in full:

     <div class="code-block code-block--clamped">
       <pre tabindex="0"><code>…</code></pre>
       <div class="code-block-lip">
         <button class="code-block-expand" data-code-expand aria-expanded="false">
           <i class="ri-arrow-down-s-line"></i> Show all 42 lines
         </button>
       </div>
     </div>

   The clamp is CSS, so the block is bounded and scrollable with scripting blocked;
   this only removes the bound. `aria-expanded` moves with it, and the label swaps to
   the collapse wording from `data-code-collapse` if one is given.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var CLAMPED = 'code-block--clamped';

    function labelFor(button, expanded) {
        var other = expanded ? button.getAttribute('data-code-collapse')
                             : button.getAttribute('data-code-expand-label');
        if (!other) return;
        var text = button.querySelector('span');
        if (text) text.textContent = other;
    }

    ui.codeBlock = {
        /* Expands or collapses a block. Exposed so a "collapse all" control, or an app
           that renders its own lip, does not have to reproduce the class name. */
        toggle: function (block, expanded) {
            if (!block) return;
            var open = expanded === undefined ? block.classList.contains(CLAMPED) : expanded;
            block.classList.toggle(CLAMPED, !open);

            var button = block.querySelector('[data-code-expand]');
            if (button) {
                button.setAttribute('aria-expanded', String(open));
                labelFor(button, open);
            }
        }
    };

    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-code-expand]');
        if (!button) return;

        var block = button.closest('.code-block');
        if (!block) return;

        e.preventDefault();
        ui.codeBlock.toggle(block);

        // A collapse leaves the reader looking at the middle of the block. Put them
        // back at its top, which is where the collapsed view starts.
        if (block.classList.contains(CLAMPED)) {
            var pre = block.querySelector('pre');
            if (pre) pre.scrollTop = 0;
        }
    });

})(window.sednaUi);

/* ── 29-sheet.js ──────────────────────────────────────────────── */
/* ── Sheet, drag to dismiss ──────────────────────────────────────────────────
   Opt-in wiring for a bottom sheet, because the grab handle is drawn as a promise
   the CSS cannot keep:

     <dialog class="drawer sheet" data-sheet>
       <div class="sheet-handle"></div>
       <div class="drawer-header">…</div>
       <div class="drawer-body">…</div>
     </dialog>

   Drag the handle or the header downwards and the sheet follows the pointer; let go
   past a quarter of its height, or flick it, and it closes. Anything short of that
   springs back.

   `data-sheet` rather than automatic, for the reason `data-tabs` is opt-in: an app
   with its own gesture would otherwise have two things closing one dialog.

   <dialog> only. The div form's open state is a class the app owns — in Blazor,
   something the framework rewrites on the next render — so removing it here would be
   reverted silently and only sometimes.

   Four things here are the reason this is not five lines in each app:

     · `touch-action: none` on the grips, in 63-drawer.css. Without it the browser
       claims a vertical drag for scrolling before the first pointermove arrives, and
       the sheet does not move under a finger at all.
     · dismissal is distance OR velocity. Distance alone means a fast short flick —
       which is what a phone user actually does — springs back instead of closing.
     · the closing slide is the platform's, not a second animation. Restoring the
       transition, dropping the inline transform and calling close() in ONE task gives
       the browser a single style recomputation: it sees the drag position as the
       before-change style and `.sheet`'s own translateY(100%) as the after, so it
       interpolates from where the finger let go. Anything that clears the transform
       in an earlier task snaps the panel back to rest for a frame first, and that
       bounce is the whole reason to do it this way.
     · which also means reduced motion needs no branch here: 95-reduced-motion.css
       already takes the transition off `dialog.drawer`, so close() simply hides it.

   The close button is still what makes a sheet dismissible. This is an addition on
   top of it and never the only way out: the handle is not focusable, so a drag is
   not reachable from the keyboard by design.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    // Past a quarter of the sheet's own height it is a dismissal, whatever the speed.
    var DISTANCE = 0.25;
    // …and below that, px per millisecond that counts as a flick, with a floor so the
    // twitch inside a tap is never one.
    var FLICK = 0.5;
    var FLICK_FLOOR = 24;

    var DRAGGING = 'sheet--dragging';

    var drag = null;

    /* The sheet this pointer may drag, or null. A control inside the header is a
       control — the close button has to stay a button, and pressing it must not start
       a gesture instead. */
    function gripped(target) {
        if (!(target instanceof Element)) return null;
        if (target.closest('button, a, input, select, textarea, [contenteditable]')) return null;

        var grip = target.closest('.sheet-handle, .drawer-header');
        if (!grip) return null;

        var sheet = grip.closest('dialog.sheet[data-sheet]');
        // Direct children only, matching the CSS that makes them draggable. A header
        // nested deeper belongs to something else the sheet happens to contain.
        return sheet && sheet.hasAttribute('open') && grip.parentElement === sheet ? sheet : null;
    }

    /* One task, three mutations, in this order — see the note above. */
    function slideOut(sheet) {
        sheet.classList.remove(DRAGGING);   // the transition comes back…
        sheet.style.transform = '';         // …and the target becomes .sheet's own
        sheet.close();                      //    translateY(100%), once [open] goes
    }

    function springBack(sheet) {
        sheet.classList.remove(DRAGGING);
        sheet.style.transform = '';
    }

    document.addEventListener('pointerdown', function (e) {
        if (drag || (e.pointerType === 'mouse' && e.button !== 0)) return;

        var sheet = gripped(e.target);
        if (!sheet) return;

        drag = {
            sheet: sheet,
            id: e.pointerId,
            y0: e.clientY,
            t0: e.timeStamp,
            dy: 0,
            height: sheet.getBoundingClientRect().height
        };
        sheet.classList.add(DRAGGING);

        // Capture on the sheet, so the gesture survives the pointer leaving the grip —
        // which it does immediately, because the grip is at the top and the drag goes
        // down. Not fatal if the browser refuses it: the listeners are on document.
        try { sheet.setPointerCapture(e.pointerId); } catch (err) { /* carry on */ }

        // Stops the drag selecting the header's text. Focus is untouched — a control
        // never reaches here, and the dialog already holds focus.
        e.preventDefault();
    });

    document.addEventListener('pointermove', function (e) {
        if (!drag || e.pointerId !== drag.id) return;

        // Downward only. A sheet is anchored to the bottom edge, so dragging it up
        // lifts it off that edge and shows the page through the gap.
        drag.dy = Math.max(0, e.clientY - drag.y0);
        drag.sheet.style.transform = 'translateY(' + drag.dy + 'px)';
    });

    function finish(e) {
        if (!drag || e.pointerId !== drag.id) return;

        var d = drag;
        drag = null;

        try { d.sheet.releasePointerCapture(e.pointerId); } catch (err) { /* already gone */ }

        var speed = d.dy / Math.max(1, e.timeStamp - d.t0);

        if (d.dy > d.height * DISTANCE || (speed > FLICK && d.dy > FLICK_FLOOR)) slideOut(d.sheet);
        else springBack(d.sheet);
    }

    document.addEventListener('pointerup', finish);
    document.addEventListener('pointercancel', finish);

})(window.sednaUi);

/* ── 30-markdown.js ──────────────────────────────────────────────── */
/* ── Markdown editor ─────────────────────────────────────────────────────────
   Toolbar + textarea + live preview inside one .md-editor root. Blazor owns the
   value through the textarea's two-way @bind (@bind:event="oninput"); toolbar
   edits mutate the textarea and dispatch a bubbling 'input' event so the binding
   picks them up — this code never calls back into .NET.

   init() is idempotent per editor, since Blazor re-renders its host. Call it with no
   argument to wire every .md-editor in the document, with a container to wire the ones
   inside it, or with an editor to wire exactly that one — an app renders editors and
   then calls init(), and does not have to know how many there are or hold a reference
   to each. From C#: ISednaUi.InitMarkdownAsync().
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    ui.md = {
        /* Counter for the per-editor radio group name. Private. */
        _seq: 0,

        init: function (root) {
            root = root || document;
            // An editor initialises itself; anything else initialises the editors
            // inside it. Each one is wired against its OWN root, so two editors on a
            // page get separate radio groups and separate listeners — which they would
            // not if a shared container were treated as the root.
            var editors = root.matches && root.matches('.md-editor')
                ? [root]
                : root.querySelectorAll('.md-editor');

            for (var i = 0; i < editors.length; i++) this._initOne(editors[i]);
        },

        /* One editor. Private: init() is the entry point. */
        _initOne: function (root) {
            if (!root || root.dataset.mdReady === '1') return;
            root.dataset.mdReady = '1';
            var self = this;
            var ta = root.querySelector('[data-md-input]');
            var preview = root.querySelector('[data-md-preview]');
            if (!ta) return;

            var renderPreview = function () {
                if (preview) preview.innerHTML = self.render(ta.value);
            };

            // The Write/Preview switch is a .segmented control, so it is a radio
            // group: the checked state comes from the platform and CSS draws it with
            // :has(input:checked). Nothing here toggles a class.
            //
            // The radios need a shared `name` to be one group, and it has to be unique
            // per editor or two editors on a page fight over one selection. Assigned
            // here rather than in the markup, because only this code knows how many
            // roots exist.
            var views = root.querySelectorAll('input[data-md-tab]');
            if (views.length) {
                var group = 'sedna-md-view-' + (++ui.md._seq);
                views.forEach(function (r) { r.name = group; });
            }

            root.addEventListener('click', function (e) {
                var cmdBtn = e.target.closest('[data-md-cmd]');
                if (cmdBtn && root.contains(cmdBtn)) {
                    e.preventDefault();
                    self.apply(ta, cmdBtn.getAttribute('data-md-cmd'));
                    renderPreview();
                }
            });

            root.addEventListener('change', function (e) {
                var radio = e.target.closest('input[data-md-tab]');
                if (!radio || !root.contains(radio) || !radio.checked) return;

                var view = radio.getAttribute('data-md-tab');
                // Carry the height between panes (both are resize:vertical), reading
                // the visible one before the flip. The preview then fills the same box
                // and scrolls internally instead of ballooning its host on long text,
                // and a manual resize in either pane sticks across the switch.
                if (view === 'preview') {
                    renderPreview();
                    if (preview) preview.style.height = ta.offsetHeight + 'px';
                } else if (preview && preview.offsetHeight) {
                    ta.style.height = preview.offsetHeight + 'px';
                }
                root.setAttribute('data-md-view', view);
            });

            ta.addEventListener('input', renderPreview);
            renderPreview();
        },

        // Apply a toolbar command to the current selection, then fire the input
        // event so the binding captures the new value.
        apply: function (ta, cmd) {
            var v = ta.value, s = ta.selectionStart, e = ta.selectionEnd;
            var sel = v.slice(s, e);
            var wrap = function (before, after, ph) {
                var body = sel || ph;
                ta.value = v.slice(0, s) + before + body + after + v.slice(e);
                ta.selectionStart = s + before.length;
                ta.selectionEnd = s + before.length + body.length;
            };
            var linePrefix = function (prefix) {
                // Expand the selection to whole lines, then prefix each.
                var ls = v.lastIndexOf('\n', s - 1) + 1;
                var le = v.indexOf('\n', e); if (le === -1) le = v.length;
                var block = v.slice(ls, le) || prefix.trim();
                var prefixed = block.split('\n').map(function (line, i) {
                    return (cmd === 'ol' ? (i + 1) + '. ' : prefix) + line;
                }).join('\n');
                ta.value = v.slice(0, ls) + prefixed + v.slice(le);
                ta.selectionStart = ls;
                ta.selectionEnd = ls + prefixed.length;
            };
            switch (cmd) {
                case 'bold':   wrap('**', '**', 'bold text'); break;
                case 'italic': wrap('_', '_', 'italic text'); break;
                case 'code':   wrap('`', '`', 'code'); break;
                case 'h2':     linePrefix('## '); break;
                case 'ul':     linePrefix('- '); break;
                case 'ol':     linePrefix('1. '); break;
                case 'quote':  linePrefix('> '); break;
                case 'link':   wrap('[', '](https://)', 'link text'); break;
                default: return;
            }
            ta.dispatchEvent(new Event('input', { bubbles: true }));
            ta.focus();
        },

        // Minimal, self-contained Markdown → HTML. HTML is escaped FIRST; only a
        // fixed set of block/inline constructs is re-introduced, and link hrefs
        // are scheme-checked. Not a spec-complete parser — enough for authored
        // prose, and safe enough that its output can be injected.
        //
        // esc() escapes the double quote as well, so its output is safe in an
        // attribute value and not only in a text node. Without it a link target of
        // https://x"onmouseover=alert(1) closes the href and opens a handler.
        render: function (src) {
            if (!src) return '';
            var esc = function (s) {
                return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                    .replace(/"/g, '&quot;');
            };
            // Pull fenced code blocks out first so their contents are never formatted.
            var blocks = [];
            src = src.replace(/```([\s\S]*?)```/g, function (_, code) {
                blocks.push('<pre><code>' + esc(code.replace(/^\n/, '').replace(/\n$/, '')) + '</code></pre>');
                return '  B' + (blocks.length - 1) + ' ';
            });
            var inline = function (t) {
                t = esc(t);
                t = t.replace(/`([^`]+)`/g, '<code>$1</code>');
                t = t.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
                t = t.replace(/_([^_]+)_/g, '<em>$1</em>');
                // The whole line went through esc() above, so `url` is already escaped
                // — escaping it a second time turned every & in a query string into
                // &amp;amp; and the browser rendered the entity rather than the &.
                t = t.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, function (_, txt, url) {
                    var safe = /^(https?:|mailto:|\/)/i.test(url) ? url : '#';
                    return '<a href="' + safe + '" target="_blank" rel="noopener">' + txt + '</a>';
                });
                return t;
            };
            var out = [], list = null;
            var closeList = function () { if (list) { out.push('</' + list + '>'); list = null; } };
            src.split(/\r?\n/).forEach(function (line) {
                var ph = line.match(/^  B(\d+) $/);
                if (ph) { closeList(); out.push(blocks[+ph[1]]); return; }
                if (!line.trim()) { closeList(); return; }
                var m;
                if ((m = line.match(/^(#{1,6})\s+(.*)$/))) {
                    closeList();
                    var n = m[1].length;
                    out.push('<h' + n + '>' + inline(m[2]) + '</h' + n + '>');
                    return;
                }
                if (/^(---|\*\*\*|___)\s*$/.test(line)) { closeList(); out.push('<hr>'); return; }
                if ((m = line.match(/^>\s?(.*)$/))) {
                    closeList(); out.push('<blockquote>' + inline(m[1]) + '</blockquote>'); return;
                }
                if ((m = line.match(/^[-*]\s+(.*)$/))) {
                    if (list !== 'ul') { closeList(); out.push('<ul>'); list = 'ul'; }
                    out.push('<li>' + inline(m[1]) + '</li>'); return;
                }
                if ((m = line.match(/^\d+\.\s+(.*)$/))) {
                    if (list !== 'ol') { closeList(); out.push('<ol>'); list = 'ol'; }
                    out.push('<li>' + inline(m[1]) + '</li>'); return;
                }
                closeList(); out.push('<p>' + inline(line) + '</p>');
            });
            closeList();
            return out.join('');
        }
    };

})(window.sednaUi);

/* ── 40-interop.js ──────────────────────────────────────────────── */
/* ── Small interop helpers ───────────────────────────────────────────────────
   Generic browser calls a Blazor component cannot make on its own. Note that
   getItem / setItem take the RAW key and do not apply the storage prefix — they
   are a plain localStorage bridge for an app's own keys, not a view onto the
   library's settings, which live under the prefix and are reached through
   sednaUi.settings.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var readRaw = ui._.readRaw;

    ui.openTab = function (url) {
        try { window.open(url, '_blank', 'noopener'); } catch (e) { /* ignore */ }
    };

    // Returns whether the copy succeeded, so the caller can toast either way.
    // Falls back to a hidden textarea where the async Clipboard API is
    // unavailable (older browsers, insecure origins).
    ui.copyText = async function (text) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(text);
                return true;
            }
        } catch (e) { /* fall through to the legacy path */ }
        try {
            var ta = document.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.focus(); ta.select();
            var ok = document.execCommand('copy');
            document.body.removeChild(ta);
            return ok;
        } catch (e) { return false; }
    };

    ui.viewportWidth = function () {
        return window.innerWidth || document.documentElement.clientWidth || 0;
    };

    /* Scrolls the frame's page column back to the top.

       `.page` is the only scroll container in the frame, so the window's own scroll
       position is always 0 and nothing the router does moves it. Navigating therefore
       leaves the new page at the previous page's offset — halfway down, on a route the
       reader has just arrived at. Call this from a LocationChanged handler.

       Falls back to the window for a page that is not inside the frame, such as a
       bare-layout sign-in screen. */
    ui.scrollPageTop = function () {
        var page = document.querySelector('.page');
        if (page) { page.scrollTop = 0; return; }
        try { window.scrollTo(0, 0); } catch (e) { /* ignore */ }
    };

    ui.getItem = function (k) { return readRaw(k); };

    ui.setItem = function (k, value) {
        try { localStorage.setItem(k, value); } catch (e) { /* ignore */ }
    };

})(window.sednaUi);

/* ── 41-spotlight.js ──────────────────────────────────────────────── */
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

/* ── 50-notify.js ──────────────────────────────────────────────── */
/* ── Desktop notifications and the audio ping ────────────────────────────────
   Both are best-effort: a browser may refuse either, and the caller should stay
   working when it does. Neither ships an asset.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var config = ui._.config;

    ui.requestNotify = function () {
        try {
            if ('Notification' in window && Notification.permission === 'default') {
                Notification.requestPermission();
            }
        } catch (e) { /* notifications unavailable */ }
    };

    ui.notify = function (title, body) {
        try {
            if ('Notification' in window && Notification.permission === 'granted') {
                var opts = { body: body };
                if (config.notifyIcon) opts.icon = config.notifyIcon;
                new Notification(title, opts);
            }
        } catch (e) { /* ignore */ }
    };

    // Short two-tone ping via WebAudio — no audio asset to ship. The context is
    // created lazily; browsers only allow it after a user gesture anyway. `this`
    // is the sednaUi object when called as sednaUi.ping(), so the context is
    // cached across calls on the global rather than rebuilt each time.
    ui.ping = function () {
        try {
            var ctx = this._audio ||
                (this._audio = new (window.AudioContext || window.webkitAudioContext)());
            if (ctx.state === 'suspended') ctx.resume();
            var t = ctx.currentTime;
            var osc = ctx.createOscillator(), gain = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(880, t);
            osc.frequency.setValueAtTime(660, t + 0.12);
            gain.gain.setValueAtTime(0.0001, t);
            gain.gain.exponentialRampToValueAtTime(0.12, t + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.3);
            osc.connect(gain); gain.connect(ctx.destination);
            osc.start(t); osc.stop(t + 0.32);
        } catch (e) { /* audio unavailable — the visual notification still fires */ }
    };

})(window.sednaUi);

/* ── 51-toast.js ──────────────────────────────────────────────── */
/* ── Toasts ──────────────────────────────────────────────────────────────────
   sednaUi.toast('Dispatched ORD-4182', { kind: 'go' })

   For confirming something that already happened. Anything the user must act on is
   an .alert, which stays until the state changes — a toast that carries a required
   action is an action nobody performs.

   The stack is created on first use and reused, so an app renders nothing and
   positions nothing.

   It is found by `data-sedna-toasts`, not by `.toast-stack`, and that distinction is
   load-bearing: only a stack this code created is appended to, re-labelled or removed.
   Matching the class would adopt a stack the app wrote for its own reasons — a
   server-rendered one, an example of the markup on a documentation page — append into
   it wherever it sits, overwrite its aria-live, and remove it with the last toast.

   Announced through aria-live on the stack rather than by moving focus: stealing
   focus to say "saved" interrupts whatever the user is typing. `polite` for the
   ordinary kinds and `assertive` for danger, because a failure is worth cutting in
   for and a success is not.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var ICONS = {
        go: 'ri-check-line',
        warn: 'ri-alert-line',
        danger: 'ri-error-warning-line',
        info: 'ri-information-line'
    };

    var OWN = '[data-sedna-toasts]';

    function stack() {
        var el = document.querySelector(OWN);
        if (!el) {
            el = document.createElement('div');
            el.className = 'toast-stack';
            // The marker is what makes this OURS: only a stack the library created is
            // ever appended to, re-labelled, or removed.
            el.setAttribute('data-sedna-toasts', '');
            // The region is a status log, not a landmark to navigate to.
            el.setAttribute('role', 'status');
            el.setAttribute('aria-live', 'polite');
            document.body.appendChild(el);
        }
        return el;
    }

    /**
     * message  the line to show; a plain string, inserted as text
     * opts     { kind: 'go'|'warn'|'danger'|'info', title, timeout, dismissible }
     * returns  a function that removes this toast early
     */
    ui.toast = function (message, opts) {
        opts = opts || {};
        var kind = ICONS[opts.kind] ? opts.kind : 'info';
        var host = stack();

        // A failure interrupts; a confirmation waits its turn.
        host.setAttribute('aria-live', kind === 'danger' ? 'assertive' : 'polite');

        var el = document.createElement('div');
        el.className = 'toast toast-' + kind;

        var icon = document.createElement('i');
        icon.className = ICONS[kind];
        icon.setAttribute('aria-hidden', 'true');

        var body = document.createElement('div');
        body.className = 'toast-body';
        if (opts.title) {
            var strong = document.createElement('strong');
            strong.textContent = opts.title;
            body.appendChild(strong);
        }
        // textContent, never innerHTML: the message often contains a value from the
        // server, and this is the one place an app would hand us one.
        body.appendChild(document.createTextNode(message == null ? '' : String(message)));

        el.appendChild(icon);
        el.appendChild(body);

        var timer = 0;
        function remove() {
            clearTimeout(timer);
            if (el.parentNode) el.parentNode.removeChild(el);
            if (!host.children.length && host.parentNode) host.parentNode.removeChild(host);
        }

        if (opts.dismissible !== false) {
            var close = document.createElement('button');
            close.type = 'button';
            close.className = 'toast-close';
            close.setAttribute('aria-label', 'Dismiss');
            close.innerHTML = '<i class="ri-close-line" aria-hidden="true"></i>';
            close.addEventListener('click', remove);
            el.appendChild(close);
        }

        host.appendChild(el);

        // 0 means "stays until dismissed" — for a failure the user has to read.
        var ms = opts.timeout === undefined ? 4000 : opts.timeout;
        if (ms > 0) timer = setTimeout(remove, ms);

        return remove;
    };

})(window.sednaUi);

/* ── 52-confirm.js ──────────────────────────────────────────────── */
/* ── Confirmation dialog ─────────────────────────────────────────────────────
   await sednaUi.confirm({ title, message, confirm, cancel, danger })
     → true if confirmed, false if cancelled or dismissed.

   Built on <dialog>.showModal(), which is the whole reason this exists rather than
   an app hand-rolling a .modal-backdrop: the platform gives the top layer, a focus
   trap, Escape-to-close and inert content behind, and none of those are things a
   div-based overlay can do without a lot of code that is usually wrong.

   Replaces window.confirm(), which blocks the thread, cannot be styled, and in
   Blazor Server blocks the circuit while it is open.

   No fallback for a browser without <dialog>. The supported floor is Chromium —
   current Chrome and Edge — which has had it for years; a fallback path would be
   untested code that only ever runs where the library is not supported anyway.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    }

    ui.confirm = function (opts) {
        opts = opts || {};
        var title = opts.title || 'Are you sure?';
        var message = opts.message || '';
        var confirmLabel = opts.confirm || 'Confirm';
        var cancelLabel = opts.cancel || 'Cancel';

        return new Promise(function (resolve) {
            var dialog = document.createElement('dialog');
            dialog.className = 'modal modal-sm';

            var header = el('div', 'modal-header');
            var h3 = el('h3', null, title);
            header.appendChild(h3);

            var body = el('div', 'modal-body');
            if (message) body.appendChild(el('p', null, message));

            var footer = el('div', 'modal-footer');
            var cancel = el('button', 'btn', cancelLabel);
            cancel.type = 'button';
            var ok = el('button', 'btn ' + (opts.danger ? 'btn-danger' : 'btn-primary'), confirmLabel);
            ok.type = 'button';
            footer.appendChild(cancel);
            footer.appendChild(ok);

            dialog.appendChild(header);
            if (message) dialog.appendChild(body);
            dialog.appendChild(footer);
            document.body.appendChild(dialog);

            // Settle on the ACTION, not only on the dialog's `close` event.
            //
            // Resolving purely from `close` gives the promise a single point of
            // failure: if that event does not arrive — and it does not, for instance,
            // in a background or non-compositing tab, where close() still takes
            // effect but the queued event is never dispatched — then `await confirm()`
            // never returns. In a Blazor handler that is an action that silently stops
            // working, with no error anywhere.
            //
            // So every route a user can take settles directly: both buttons, and the
            // `cancel` event that Escape fires. The `close` listener is a third line
            // only — it covers an app calling close() on the dialog itself, and it is
            // no more reliable than the event it hangs off, which is the point. Nothing
            // a user can do depends on it.
            //
            // settled makes the first route win and the rest no-ops, so the paths
            // cannot double-resolve or double-remove.
            var settled = false;
            function settle(value) {
                if (settled) return;
                settled = true;
                try { if (dialog.open) dialog.close(); } catch (e) { /* already closed */ }
                if (dialog.parentNode) dialog.parentNode.removeChild(dialog);
                resolve(value);
            }

            ok.addEventListener('click', function () { settle(true); });
            cancel.addEventListener('click', function () { settle(false); });
            dialog.addEventListener('cancel', function () { settle(false); });   // Escape
            dialog.addEventListener('close', function () { settle(false); });

            dialog.showModal();

            // Focus the SAFE choice. showModal() focuses the first focusable element,
            // which would be Cancel here by source order — but for a destructive
            // action that ordering is the point, so it is made explicit rather than
            // left to depend on the DOM order.
            (opts.danger ? cancel : ok).focus();
        });
    };

})(window.sednaUi);


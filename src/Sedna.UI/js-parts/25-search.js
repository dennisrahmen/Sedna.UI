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

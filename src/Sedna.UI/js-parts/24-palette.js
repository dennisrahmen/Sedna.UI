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
        else if (c.href) window.location.assign(c.href);
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

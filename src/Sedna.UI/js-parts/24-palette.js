/* ── Command palette ─────────────────────────────────────────────────────────
   sednaUi.palette.register([{ label, icon, group, note, run, href, keywords }])
   sednaUi.palette.open()      — or Ctrl/Cmd-K, which is wired for you

   THE MARKUP IS THE APP'S. The script draws nothing: the app writes one
   <dialog class="palette" data-palette> with its input, its list and its footer,
   in its own words, and a <template> for each row shape —

     <dialog class="palette" data-palette aria-label="Commands">
       <input class="palette-input" type="text" placeholder="Search commands…" aria-label="Search commands">
       <ul class="palette-list" aria-label="Commands"></ul>
       <div class="palette-footer">…</div>
       <template data-palette-item>
         <li role="presentation">
           <div class="palette-item" role="option">
             <i data-icon aria-hidden="true"></i><span data-label></span>
             <span class="palette-item-note" data-note></span>
           </div>
         </li>
       </template>
       <template data-palette-group>
         <li role="presentation"><div class="palette-group" data-group></div></li>
       </template>
       <template data-palette-empty>
         <li role="presentation"><div class="palette-empty">Nothing matches “<span data-query></span>”.</div></li>
       </template>
     </dialog>

   — and the script clones those templates into the list and fills their slots
   (ui._.fill in 00-core.js). A slot with nothing to say is removed; `data-icon`
   takes the command's icon class. Leave the list empty in the markup: the script
   owns its children, and a framework rendering some of its own would fight it.
   The group and empty templates are optional.

   The script adds what is behaviour, not appearance: the combobox and listbox roles,
   aria-expanded, aria-controls, aria-activedescendant, aria-selected and each
   option's id, because those are a promise about the keyboard contract only this
   file keeps.

   The scorer is ui._.score in 00-core.js, shared with the header search so the
   two cannot rank the same query differently. Matches in `keywords` score below
   the same match in the label, so a command is never outranked by one that
   merely mentions the word.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var score = ui._.score;

    var commands = [];
    var dialog = null;
    var input = null;
    var list = null;
    var shown = [];      // the currently visible commands, in ranked order
    var at = 0;          // index into shown
    var wired = [];      // dialogs whose listeners are attached
    var warned = false;

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

    var fill = ui._.fill;

    function template(name) {
        return dialog.querySelector('template[data-palette-' + name + ']');
    }

    function render(query) {
        shown = rank(query);
        at = 0;
        list.textContent = '';

        if (!shown.length) {
            // Says what was searched, not just "no results" — the reader needs to
            // know the query was what they thought it was. The words are the app's.
            var empty = fill(template('empty'), { query: query });
            if (empty) list.appendChild(empty);
            input.removeAttribute('aria-activedescendant');
            return;
        }

        var lastGroup = null;
        shown.forEach(function (c, i) {
            // Groups are only meaningful in registration order, so they are dropped
            // once a query has reordered the list — a heading over unrelated results
            // is worse than no heading.
            if (!query && c.group && c.group !== lastGroup) {
                var head = fill(template('group'), { group: c.group });
                if (head) list.appendChild(head);
                lastGroup = c.group;
            }

            var row = fill(template('item'), { label: c.label, note: c.note });
            if (!row) return;

            var icon = row.querySelector('[data-icon]');
            if (icon && c.icon) c.icon.split(/\s+/).forEach(function (k) { if (k) icon.classList.add(k); });
            else if (icon) icon.remove();

            // role="option" is the app's template's, and the row it sits on is what the
            // keyboard moves over. The template's outer element is often an
            // <li role="presentation"> around it, which a listbox needs.
            var option = row.matches('[role="option"]') ? row : row.querySelector('[role="option"]') || row;
            option.setAttribute('role', 'option');
            option.id = list.id + '-' + i;
            option.setAttribute('aria-selected', String(i === 0));
            option.addEventListener('click', function () { run(i); });

            list.appendChild(row);
        });

        input.setAttribute('aria-activedescendant', list.id + '-0');
    }

    function items() { return list.querySelectorAll('[role="option"]'); }

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
        // `href` is what a command registered from C# uses. A function does not
        // cross into C#, so navigation is the one action a serialisable command can
        // carry.
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

    /* Finds the app's palette and claims the roles on it. Re-run on every open, because
       a framework may have replaced the dialog since the last one; the listeners are
       attached once per element. */
    function adopt() {
        var found = document.querySelector('dialog[data-palette]');
        if (!found) {
            if (!warned) console.warn('sednaUi.palette: no <dialog data-palette> in the document. The palette is app markup — see the Command palette page.');
            warned = true;
            return false;
        }

        dialog = found;
        input = dialog.querySelector('.palette-input, input');
        list = dialog.querySelector('.palette-list, ul');
        if (!input || !list) {
            console.warn('sednaUi.palette: the palette needs an input and a list.');
            return false;
        }

        if (!list.id) list.id = 'sedna-palette-list';
        list.setAttribute('role', 'listbox');
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-expanded', 'true');
        input.setAttribute('aria-controls', list.id);
        input.setAttribute('autocomplete', 'off');

        if (wired.indexOf(dialog) >= 0) return true;
        wired.push(dialog);

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
        return true;
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
            if (!adopt()) return false;
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
        // Nor with no palette to open: swallowing the key for a warning helps nobody.
        if (!document.querySelector('dialog[data-palette]')) return;

        e.preventDefault();
        ui.palette.open();
    });

})(window.sednaUi);

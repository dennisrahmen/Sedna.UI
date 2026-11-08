/* ── Command palette ─────────────────────────────────────────────────────────
   sednaUi.palette.register([{ label, icon, group, note, run, href, keywords, page, opens, pinned }])
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

   PAGES. A command with `opens: 'theme'` opens a page of its own commands — those
   registered with `page: 'theme'` — instead of running; its `run`, if it has one,
   is called as the page opens, so an app can start loading what the page lists.
   A command with no `page` is on the root. Backspace in an empty input, and
   Escape, go back one page; Escape on the root closes. The way back is the app's
   too, and all of it optional:

     <div class="palette-path" data-palette-path hidden>
       <button class="btn btn--sm btn--icon" type="button" data-palette-back aria-label="Back">…</button>
       <span class="palette-trail" data-palette-trail></span>
     </div>
     <template data-palette-crumb><span class="palette-crumb" data-label></span></template>

   The script shows the path (removes `hidden`) on a page and hides it on the
   root, and fills the trail with one crumb per page opened, labelled with the
   command that opened it. Going back restores that page's query and highlight.

   LOADING. While the app sets aria-busy="true" on the <dialog data-palette>,
   the list ends with a clone of <template data-palette-loading>, and the empty
   row is held back — "nothing matches" is not yet true. The list is redrawn when
   the attribute changes and when register() is called while the palette is open.

   GROUPS, SEPARATORS AND PINNED COMMANDS. With nothing typed the list is in
   registration order, a heading from <template data-palette-group> over each run
   of one group, and a clone of <template data-palette-separator> between two
   runs. A query reorders the list, so both are dropped while one is typed. A
   command with `pinned: true` is never ranked: it follows the matches whatever is
   typed — "search the documentation for this" — and does not count as a match.

   The arrow keys wrap: Down on the last row goes to the first, Up on the first
   to the last.
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
    // The pages opened since the root, innermost last: { id, label, query, at } —
    // the page's id, the label of the command that opened it, and the query and
    // highlight of the page it was opened FROM, which going back restores.
    var stack = [];

    function here() { return stack.length ? stack[stack.length - 1].id : null; }

    // A command with no page is on the root; `page` names any other.
    function on(c, page) { return (c.page || null) === (page || null); }

    function rank(query, page) {
        var out = [];
        for (var i = 0; i < commands.length; i++) {
            var c = commands[i];
            if (!on(c, page)) continue;
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

    function busy() { return dialog.getAttribute('aria-busy') === 'true'; }

    function render(query, keep) {
        var page = here();
        var ranked = rank(query, page);
        if (query) {
            // Pinned commands are not ranked: they follow the matches, in
            // registration order, whatever is typed.
            ranked = ranked.filter(function (c) { return !c.pinned; });
            shown = ranked.concat(commands.filter(function (c) { return c.pinned && on(c, page); }));
        } else {
            shown = ranked;
        }
        at = 0;
        list.textContent = '';
        trail();

        if (!ranked.length && query && !busy()) {
            // Says what was searched, not just "no results" — the reader needs to
            // know the query was what they thought it was. The words are the app's.
            var empty = fill(template('empty'), { query: query });
            if (empty) list.appendChild(empty);
        }

        var lastGroup = null;
        shown.forEach(function (c, i) {
            // Groups are only meaningful in registration order, so they are dropped
            // once a query has reordered the list — a heading over unrelated results
            // is worse than no heading. A separator goes between two runs, never
            // above the first.
            if (!query && c.group && c.group !== lastGroup) {
                if (lastGroup !== null) {
                    var rule = fill(template('separator'), {});
                    if (rule) list.appendChild(rule);
                }
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

        if (busy()) {
            var loading = fill(template('loading'), {});
            if (loading) list.appendChild(loading);
        }

        if (shown.length && keep > 0) highlight(Math.min(keep, items().length - 1));
        else if (shown.length) input.setAttribute('aria-activedescendant', list.id + '-0');
        else input.removeAttribute('aria-activedescendant');
    }

    /* Shows the app's path on a page and hides it on the root, with one crumb per
       page opened. Nothing happens without the markup: the pages still work, the
       reader just has no trail. */
    function trail() {
        var path = dialog.querySelector('[data-palette-path]');
        if (path) path.hidden = !stack.length;
        var holder = dialog.querySelector('[data-palette-trail]');
        if (!holder) return;
        holder.textContent = '';
        stack.forEach(function (p) {
            var crumb = fill(template('crumb'), { label: p.label });
            if (crumb) holder.appendChild(crumb);
        });
    }

    function enter(c) {
        stack.push({ id: c.opens, label: c.label, query: input.value, at: at });
        input.value = '';
        // Before the page is drawn, so a `run` that sets aria-busy or registers the
        // page's commands is in the first render rather than a second.
        if (typeof c.run === 'function') c.run();
        render('');
        input.focus();
    }

    /* One page back, to the query and the row the reader left. False on the root,
       where there is nowhere to go. */
    function back() {
        if (!stack.length) return false;
        var was = stack.pop();
        input.value = was.query;
        render(was.query, was.at);
        input.focus();
        return true;
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
        if (c && c.opens) { enter(c); return; }
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
        else if (ui._.safeHref(c.href)) go(ui._.safeHref(c.href));
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

        // Closured on the element, not the module's `dialog`, which a later adopt()
        // may have pointed at a replacement.
        var own = dialog;

        input.addEventListener('input', function () { render(input.value); });

        input.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); highlight(at + 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(at - 1); }
            else if (e.key === 'Home') { e.preventDefault(); highlight(0); }
            else if (e.key === 'End') { e.preventDefault(); highlight(items().length - 1); }
            else if (e.key === 'Enter') { e.preventDefault(); if (items().length) run(at); }
            // Only in an EMPTY input: with text in it, Backspace is editing.
            else if (e.key === 'Backspace' && !input.value && stack.length) { e.preventDefault(); back(); }
        });

        // On the dialog rather than the input, so it holds while the back button has
        // the focus. Cancelling the keydown is what keeps the dialog open: its close
        // request is that key's default action.
        own.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && stack.length) { e.preventDefault(); back(); }
        });

        // Clicking the backdrop closes it. The dialog fills only part of the top
        // layer, so a click whose target IS the dialog element landed outside the
        // panel's own children.
        own.addEventListener('click', function (e) {
            if (e.target === own) { close(); return; }
            if (e.target.closest && e.target.closest('[data-palette-back]')) back();
        });

        // The app's loading signal. Redrawn in place, keeping what is typed.
        new MutationObserver(function () {
            if (own === dialog && own.open) render(input.value);
        }).observe(own, { attributes: true, attributeFilter: ['aria-busy'] });
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
            // An app loading a page's commands registers them while the page is open;
            // the reader sees them arrive under what they typed.
            if (dialog && dialog.open) render(input.value);
        },

        open: function () {
            if (!adopt()) return false;
            if (dialog.open) return true;

            stack = [];
            input.value = '';
            render('');
            dialog.showModal();
            input.focus();
            return true;
        },

        close: close,

        /* One page back; false on the root. For a control of the app's own that is
           not a `data-palette-back` button. */
        back: function () { return !!(dialog && dialog.open) && back(); },

        /* Exposed for tests and for an app that wants the same ranking in its own
           UI. Returns the matching commands on one page — the root unless `page`
           names another — best first. */
        rank: rank
    };

    document.addEventListener('keydown', function (e) {
        // metaKey for macOS, ctrlKey everywhere else. Checking both rather than
        // sniffing the platform: a Mac user on an external PC keyboard uses Ctrl.
        if (e.key !== 'k' && e.key !== 'K') return;
        if (!e.ctrlKey && !e.metaKey) return;
        // Something with the focus took the key first — the rich-text editor's link form, a
        // field of the app's own — and the palette leaves it that key.
        if (e.defaultPrevented) return;
        if (!commands.length) return;      // nothing registered: leave the browser's own binding alone
        // Nor with no palette to open: swallowing the key for a warning helps nobody.
        if (!document.querySelector('dialog[data-palette]')) return;

        e.preventDefault();
        ui.palette.open();
    });

})(window.sednaUi);

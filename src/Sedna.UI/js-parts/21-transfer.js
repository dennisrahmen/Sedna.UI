/* ── Transfer list, delegated ────────────────────────────────────────────────
   Moves rows between the two list boxes of a `.transfer`. The CSS lays the shape
   out; this is the part that cannot be CSS, because moving an <option> from one
   <select> to another is a DOM edit.

   Add data-transfer to the container, and mark the two lists and the buttons:

     <div class="transfer" data-transfer>
       <div class="form-field">
         <label class="form-label" for="t-from">Available</label>
         <select class="form-select" id="t-from" multiple size="6" data-transfer-from>…</select>
       </div>
       <div class="transfer-actions">
         <button class="btn btn--icon" type="button" data-transfer-add    aria-label="Add">…</button>
         <button class="btn btn--icon" type="button" data-transfer-remove aria-label="Remove">…</button>
       </div>
       <div class="form-field">
         <label class="form-label" for="t-to">Notified</label>
         <select class="form-select" id="t-to" multiple size="6" data-transfer-to>…</select>
       </div>
     </div>

   A double-click on a row moves it on its own, which is what people try first.

   Selection is left entirely to the platform, which already has the right contract
   for this: a plain click selects ONE row and drops the previous one, and Ctrl or
   Shift extends. That is what a staging selection wants — nothing accumulates
   between moves, and neither list ends up holding a highlight the reader does not
   remember making.

   Both lists get a `change` event after every move, bubbling and composed, so an
   app's existing handler — Blazor's `@onchange` included — sees it exactly as it
   sees a click on an option. That is the whole integration: the right-hand list is
   an ordinary `multiple` select, so a form posts it with no help from here, and
   what the reader has chosen is what that select contains.

   The lists may be `<select multiple>` or `.listbox`es (22-listbox.js): a row of a listbox is
   moved the same way, chosen is its `aria-selected`, and the destination list is
   told with the `change` and `sedna-listbox-select` events a click would have sent.
   A listbox posts nothing, so the app reads the right-hand list's `values`.

   Nothing about the CSS depends on this. With scripting blocked the two lists are
   still two list boxes, still labelled, still postable — the buttons simply do
   nothing, which is why they are buttons and not the only way to choose.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function isBox(list) { return list.classList.contains('listbox'); }

    /* A select's options, or a listbox's rows — a real array either way, taken before anything
       moves, because a live HTMLCollection would shorten underneath the loop that appends. */
    function rowsOf(list) {
        return isBox(list)
            ? Array.prototype.filter.call(list.querySelectorAll('[role="option"]'), function (o) { return o.closest('.listbox') === list; })
            : Array.prototype.slice.call(list.options);
    }
    function textOf(row) { return (row.text !== undefined ? row.text : row.textContent || '').trim(); }

    function selectedIn(list) {
        return rowsOf(list).filter(function (o) {
            return isBox(list)
                ? o.getAttribute('aria-selected') === 'true' && o.getAttribute('aria-disabled') !== 'true'
                : o.selected && !o.disabled;
        });
    }

    function unselect(list, row) {
        if (isBox(list)) { row.setAttribute('aria-selected', 'false'); row.removeAttribute('data-active'); }
        else row.selected = false;
    }

    /* Alphabetical, and only where the list was already in order. Re-sorting a list
       whose order is meaningful — a priority, a pipeline — would silently destroy
       it, so the check is whether the destination is sorted before the move, not a
       setting somebody has to know about. */
    function sorted(list) {
        var rows = rowsOf(list);
        for (var i = 1; i < rows.length; i++) {
            if (textOf(rows[i - 1]).localeCompare(textOf(rows[i])) > 0) return false;
        }
        return true;
    }

    function place(list, row) {
        var rows = rowsOf(list), at = null;
        for (var i = 0; i < rows.length; i++) {
            if (textOf(rows[i]).localeCompare(textOf(row)) > 0) { at = rows[i]; break; }
        }
        (at ? at.parentNode : list).insertBefore(row, at);
    }

    function move(from, to, rows) {
        if (!from || !to || !rows.length) return 0;

        var inOrder = sorted(to);
        for (var i = 0; i < rows.length; i++) {
            unselect(from, rows[i]);
            if (inOrder) place(to, rows[i]); else to.appendChild(rows[i]);
        }

        /* Nothing arrives selected, and the destination's own selection is cleared.
           Selection in a transfer is a STAGING act — "these are the ones I am about
           to move" — not an answer, and the answer is which list a row is in. Rows
           that landed selected accumulated: after three moves the right-hand list
           had eight rows highlighted, which reads as a multi-select the reader made
           and cannot remember making, and it made the next Remove act on all of
           them. The first row moved is scrolled into view instead, which is the part
           that was actually worth having. */
        if (isBox(to)) {
            rowsOf(to).forEach(function (o) { unselect(to, o); });
            to.removeAttribute('aria-activedescendant');
        } else to.selectedIndex = -1;
        if (rows[0].scrollIntoView) rows[0].scrollIntoView({ block: 'nearest' });

        notify(from);
        notify(to);
        return rows.length;
    }

    function notify(list) {
        list.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
        if (isBox(list)) {
            list.dispatchEvent(new CustomEvent('sedna-listbox-select', {
                bubbles: true, composed: true,
                detail: { values: ui.listbox ? ui.listbox.values(list) : [] }
            }));
        }
    }

    function lists(host) {
        return {
            from: host.querySelector('[data-transfer-from]'),
            to: host.querySelector('[data-transfer-to]')
        };
    }

    ui.transfer = {
        /* Moves the current selection in one direction. `host` is the element with
           data-transfer, or any element inside it. Returns how many rows moved, so a
           caller can tell "nothing was selected" from "it did not run". */
        add: function (host) {
            var el = host && host.closest ? host.closest('[data-transfer]') : null;
            if (!el) return 0;
            var pair = lists(el);
            return move(pair.from, pair.to, pair.from ? selectedIn(pair.from) : []);
        },
        remove: function (host) {
            var el = host && host.closest ? host.closest('[data-transfer]') : null;
            if (!el) return 0;
            var pair = lists(el);
            return move(pair.to, pair.from, pair.to ? selectedIn(pair.to) : []);
        }
    };

    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-transfer] [data-transfer-add], [data-transfer] [data-transfer-remove]');
        if (!btn || btn.disabled) return;

        e.preventDefault();
        if (btn.hasAttribute('data-transfer-add')) ui.transfer.add(btn);
        else ui.transfer.remove(btn);
    });

    /* The row under the pointer, not the whole selection: a double-click is aimed at
       one row, and taking the selection with it would move rows the reader had
       chosen minutes ago and forgotten about. */
    document.addEventListener('dblclick', function (e) {
        var option = e.target.closest('[data-transfer] option, [data-transfer] [role="option"]');
        if (!option || option.disabled || option.getAttribute('aria-disabled') === 'true') return;

        var list = option.closest('select, .listbox');
        var host = option.closest('[data-transfer]');
        if (!list || !host) return;

        var pair = lists(host);
        var to = list === pair.from ? pair.to : list === pair.to ? pair.from : null;
        if (!to) return;

        e.preventDefault();
        move(list, to, [option]);
    });

})(window.sednaUi);

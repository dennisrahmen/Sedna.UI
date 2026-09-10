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
         <button class="btn btn-icon" type="button" data-transfer-add    aria-label="Add">…</button>
         <button class="btn btn-icon" type="button" data-transfer-remove aria-label="Remove">…</button>
       </div>
       <div class="form-field">
         <label class="form-label" for="t-to">Notified</label>
         <select class="form-select" id="t-to" multiple size="6" data-transfer-to>…</select>
       </div>
     </div>

   A double-click on a row moves it on its own, which is what people try first.

   Both lists get a `change` event after every move, bubbling and composed, so an
   app's existing handler — Blazor's `@onchange` included — sees it exactly as it
   sees a click on an option. That is the whole integration: the right-hand list is
   an ordinary `multiple` select, so a form posts it with no help from here, and
   what the reader has chosen is what that select contains.

   Nothing about the CSS depends on this. With scripting blocked the two lists are
   still two list boxes, still labelled, still postable — the buttons simply do
   nothing, which is why they are buttons and not the only way to choose.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function selectedIn(list) {
        // A live HTMLCollection would shorten underneath the loop that appends from
        // it, so this is a real array taken before anything moves.
        return Array.prototype.filter.call(
            list.options, function (o) { return o.selected && !o.disabled; });
    }

    /* Alphabetical, and only where the list was already in order. Re-sorting a list
       whose order is meaningful — a priority, a pipeline — would silently destroy
       it, so the check is whether the destination is sorted before the move, not a
       setting somebody has to know about. */
    function sorted(list) {
        for (var i = 1; i < list.options.length; i++) {
            if (list.options[i - 1].text.localeCompare(list.options[i].text) > 0) return false;
        }
        return true;
    }

    function place(list, option) {
        var at = null;
        for (var i = 0; i < list.options.length; i++) {
            if (list.options[i].text.localeCompare(option.text) > 0) { at = list.options[i]; break; }
        }
        list.insertBefore(option, at);
    }

    function move(from, to, options) {
        if (!from || !to || !options.length) return 0;

        var inOrder = sorted(to);
        for (var i = 0; i < options.length; i++) {
            // Selected on arrival, so the rows just moved are the ones highlighted —
            // otherwise a move of six rows lands them somewhere in a list of forty
            // with nothing to say which they were.
            options[i].selected = true;
            if (inOrder) place(to, options[i]); else to.appendChild(options[i]);
        }

        notify(from);
        notify(to);
        return options.length;
    }

    function notify(list) {
        list.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
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
        var option = e.target.closest('[data-transfer] option');
        if (!option || option.disabled) return;

        var list = option.closest('select');
        var host = option.closest('[data-transfer]');
        if (!list || !host) return;

        var pair = lists(host);
        var to = list === pair.from ? pair.to : list === pair.to ? pair.from : null;
        if (!to) return;

        e.preventDefault();
        move(list, to, [option]);
    });

})(window.sednaUi);

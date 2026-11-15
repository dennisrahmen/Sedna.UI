/* ── Listbox, delegated ──────────────────────────────────────────────────────
   The keyboard and the selection behind a `.listbox`: an ARIA listbox the app writes as
   a box of `role="option"` rows, flat or in `role="group"`s.

     <div class="listbox" role="listbox" aria-multiselectable="true" aria-label="Notify groups" tabindex="0">
       <div role="option" aria-selected="true" data-value="fulfilment">Fulfilment</div>
       <div role="option" aria-selected="false" data-value="network">Network</div>
       <div role="option" aria-selected="false" aria-disabled="true">Finance</div>
     </div>

   `aria-selected` is chosen, `aria-disabled="true"` is passed over, `aria-multiselectable`
   allows several. `data-value` is what an event names a row by, else its text;
   `data-label` overrides the text typeahead reads.

   ONE TAB STOP, as a combo field has: the listbox takes the focus and the row the keyboard
   is on is named by `aria-activedescendant`. The script gives a row without an id one, marks
   the active row `data-active` for the paint, and starts on the first chosen row.

     ↓ ↑              next and previous row; in a listbox that takes one answer, it chooses it
     Home End         first and last row
     a letter         typeahead: the next row whose label starts with it
     Space            toggles the row in a multiselectable listbox
     Shift+↓ ↑        extends the choice from where it began
     Ctrl+A (⌘A)      chooses every row in a multiselectable listbox

   A click chooses the row and drops the others; Ctrl or ⌘ toggles it, Shift chooses from the
   row the last click or key began on. That is what a native list box does and what a staging
   choice wants.

   THE SCRIPT HOLDS THE STATE, AND SAYS SO. It writes `aria-selected`, `aria-activedescendant`
   and `data-active` on the app's elements — attributes a render leaves alone while the app's
   own value for them does not change — and after each change dispatches on the listbox, both
   bubbling:

     change                      a plain Event, so an app's `@onchange` sees it
     sedna-listbox-select        { values }           every value chosen, in document order (the target is the listbox)

   A listbox is not a form control: it posts nothing. An app that submits a choice reads
   `values` or the rows' `aria-selected`, or uses a `<select multiple>`, which a form posts.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var typed = typeof WeakMap === 'function' ? new WeakMap() : null;
    var anchors = typeof WeakMap === 'function' ? new WeakMap() : null;
    var counter = 0;

    function toArray(list) { return Array.prototype.slice.call(list); }
    /* Only a `.listbox`: a combo field's list is also `role="listbox"`, and the combo's script owns it. */
    function listboxOf(el) { return el && el.closest ? el.closest('.listbox[role="listbox"]') : null; }
    function multi(box) { return box.getAttribute('aria-multiselectable') === 'true'; }
    function disabled(box) { return box.getAttribute('aria-disabled') === 'true'; }

    function optionsOf(box) {
        return toArray(box.querySelectorAll('[role="option"]')).filter(function (o) { return listboxOf(o) === box; });
    }
    function reachable(box) {
        return optionsOf(box).filter(function (o) { return o.getAttribute('aria-disabled') !== 'true'; });
    }
    function isOn(o) { return o.getAttribute('aria-selected') === 'true'; }
    function valueOf(o) {
        var v = o.getAttribute('data-value');
        return v !== null ? v : (o.textContent || '').trim();
    }
    function labelOf(o) {
        var l = o.getAttribute('data-label');
        return (l !== null ? l : o.textContent || '').trim().toLowerCase();
    }

    function setOn(o, on) {
        var was = isOn(o);
        o.setAttribute('aria-selected', on ? 'true' : 'false');
        return was !== on;
    }

    function activeOf(box) {
        var id = box.getAttribute('aria-activedescendant');
        if (!id) return null;
        var el = document.getElementById(id);
        return el && listboxOf(el) === box ? el : null;
    }

    function activate(box, o) {
        var all = optionsOf(box);
        for (var i = 0; i < all.length; i++) if (all[i] !== o) all[i].removeAttribute('data-active');
        if (!o) { box.removeAttribute('aria-activedescendant'); return; }
        if (!o.id) o.id = 'sedna-listbox-option-' + (++counter);
        o.setAttribute('data-active', '');
        box.setAttribute('aria-activedescendant', o.id);
        if (o.scrollIntoView) o.scrollIntoView({ block: 'nearest' });
    }

    function announce(box) {
        var values = optionsOf(box).filter(isOn).map(valueOf);
        box.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
        box.dispatchEvent(new CustomEvent('sedna-listbox-select', { bubbles: true, composed: true, detail: { values: values } }));
    }

    /* Chooses exactly `chosen` (an array of rows) and says so if that changed anything. */
    function choose(box, chosen) {
        var changed = false;
        optionsOf(box).forEach(function (o) {
            var on = chosen.indexOf(o) >= 0 && o.getAttribute('aria-disabled') !== 'true';
            if (setOn(o, on)) changed = true;
        });
        if (changed) announce(box);
    }

    function rangeBetween(box, a, b) {
        var all = reachable(box);
        var i = all.indexOf(a), j = all.indexOf(b);
        if (i < 0 || j < 0) return b ? [b] : [];
        return all.slice(Math.min(i, j), Math.max(i, j) + 1);
    }

    function anchorOf(box) { return (anchors && anchors.get(box)) || activeOf(box); }
    function setAnchor(box, o) { if (anchors) anchors.set(box, o); }

    /* A move of the keyboard to `target`: in a single-answer listbox it chooses it; with Shift in a
       multiselectable one it extends from the anchor; otherwise it only moves. */
    function moveTo(box, target, extend) {
        if (!target) return;
        activate(box, target);
        if (!multi(box)) { setAnchor(box, target); choose(box, [target]); }
        else if (extend) choose(box, rangeBetween(box, anchorOf(box), target));
        else setAnchor(box, target);
    }

    function step(box, from, by) {
        var all = reachable(box);
        if (!all.length) return null;
        var i = all.indexOf(from);
        if (i < 0) return by > 0 ? all[0] : all[all.length - 1];
        return all[Math.min(all.length - 1, Math.max(0, i + by))];
    }

    function startOf(box) {
        var all = reachable(box);
        for (var i = 0; i < all.length; i++) if (isOn(all[i])) return all[i];
        return all[0] || null;
    }

    function typeahead(box, key) {
        var now = Date.now();
        var st = (typed && typed.get(box)) || { text: '', at: 0 };
        st.text = (now - st.at > 700 ? '' : st.text) + key.toLowerCase();
        st.at = now;
        if (typed) typed.set(box, st);
        var all = reachable(box);
        var from = all.indexOf(activeOf(box));
        var same = st.text.length > 1 && st.text.split('').every(function (c) { return c === st.text[0]; });
        var needle = same ? st.text[0] : st.text;
        for (var n = 1; n <= all.length; n++) {
            var o = all[(from + n + all.length) % all.length];
            if (o && labelOf(o).indexOf(needle) === 0) return o;
        }
        return null;
    }

    document.addEventListener('focusin', function (e) {
        var box = e.target && e.target.matches && e.target.matches('.listbox[role="listbox"]') ? e.target : null;
        if (!box || disabled(box) || activeOf(box)) return;
        var o = startOf(box);
        if (o) { activate(box, o); setAnchor(box, o); }
    });

    document.addEventListener('mousedown', function (e) {
        var o = e.target.closest ? e.target.closest('[role="option"]') : null;
        var box = listboxOf(o);
        if (!o || !box || disabled(box)) return;
        // Focus is the listbox's own, and a drag across rows must not select text.
        if (box.getAttribute('tabindex') !== null && document.activeElement !== box) box.focus({ preventScroll: true });
        if (e.shiftKey) e.preventDefault();
    });

    document.addEventListener('click', function (e) {
        var o = e.target.closest ? e.target.closest('[role="option"]') : null;
        var box = listboxOf(o);
        if (!o || !box || disabled(box) || o.getAttribute('aria-disabled') === 'true') return;

        activate(box, o);
        if (multi(box) && (e.ctrlKey || e.metaKey)) {
            setOn(o, !isOn(o));
            setAnchor(box, o);
            announce(box);
        } else if (multi(box) && e.shiftKey) {
            choose(box, rangeBetween(box, anchorOf(box), o));
        } else {
            setAnchor(box, o);
            choose(box, [o]);
        }
    });

    document.addEventListener('keydown', function (e) {
        var box = e.target && e.target.matches && e.target.matches('.listbox[role="listbox"]') ? e.target : null;
        if (!box || disabled(box) || e.altKey) return;

        var from = activeOf(box) || startOf(box);
        var key = e.key;
        var handled = true;

        if (key === 'ArrowDown') moveTo(box, step(box, from, 1), e.shiftKey);
        else if (key === 'ArrowUp') moveTo(box, step(box, from, -1), e.shiftKey);
        else if (key === 'Home') moveTo(box, reachable(box)[0], e.shiftKey);
        else if (key === 'End') { var all = reachable(box); moveTo(box, all[all.length - 1], e.shiftKey); }
        else if ((key === ' ' || key === 'Enter') && from) {
            if (multi(box)) { setOn(from, !isOn(from)); setAnchor(box, from); announce(box); }
            else choose(box, [from]);
        }
        else if ((e.ctrlKey || e.metaKey) && (key === 'a' || key === 'A') && multi(box)) choose(box, reachable(box));
        else if (key.length === 1 && !e.ctrlKey && !e.metaKey) {
            var hit = typeahead(box, key);
            if (hit) moveTo(box, hit, false);
            else handled = false;
        }
        else handled = false;

        if (handled) e.preventDefault();
    });

    ui.listbox = {
        /* The values chosen in a listbox, in document order — what `sedna-listbox-select` carries,
           for a caller that was not listening. */
        values: function (box) {
            return box ? optionsOf(box).filter(isOn).map(valueOf) : [];
        }
    };

})(window.sednaUi);

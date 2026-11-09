/* ── Mentions ────────────────────────────────────────────────────────────────
   "@" in a textarea opens the app's own list of who can be named, and the one chosen
   is written into the text. The list is a `.menu` the app writes beside the field —
   every person, every word — and names from the field with `data-mention`:

     <div class="mention-field">
       <textarea class="form-input" data-mention="people" aria-label="Comment"></textarea>
       <div class="menu" id="people" hidden aria-label="People">
         <button class="menu-item" type="button" tabindex="-1" data-mention-value="alex.fischer">
           <span class="avatar avatar--xs">AF</span><span>Alex Fischer</span>
         </button>
         …
       </div>
     </div>

   "@" at the start of the text or after a space opens it; what follows filters the
   items as the reader types, ranked by the palette's matcher (ui._.score) against an
   item's `data-label` or its text and against its `data-mention-value`. An item that
   does not match is `hidden`; nothing matching, a space, or the caret leaving the word
   closes the list. A list that changes while it is open — an app filling it from a
   search of its own — is filtered again at the next key.

   The focus stays in the field. Up and Down move the item marked `data-active` and
   `aria-selected`, which the field points at with `aria-activedescendant`; Enter or
   Tab writes "@" and the item's `data-mention-value` and a space in place of what was
   typed, puts the caret after it and dispatches a bubbling `input`, so a binding on
   the field hears it; Escape closes the list until another "@" is typed. While the
   list is open those keys reach no other handler — a composer's own Enter-to-send
   waits until it closes. A press on an item writes it the same way; the focus
   leaving the field, or a scroll that moves it, closes the list.

   The list is placed by the stylesheet, anchored to `.mention-field` (58-menu.css):
   nothing here measures the caret. The script writes the listbox and option roles,
   ids for items that have none, and the state above — no element and no word.
   Writing replaces the selection with setRangeText, so the browser's undo does not
   take it back.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var TRIGGER = /(^|\s)@([^\s@]{0,40})$/;
    var ids = 0;
    var open = null;    // { field, list, start, end } while a list is open
    var dismissed = typeof WeakMap === 'function' ? new WeakMap() : null;

    function fieldOf(el) {
        return el && el.nodeType === 1 && el.matches('textarea[data-mention], input[data-mention]') ? el : null;
    }

    function listOf(field) {
        var id = field.getAttribute('data-mention');
        return id ? document.getElementById(id) : null;
    }

    function itemsOf(list) {
        return Array.prototype.filter.call(list.querySelectorAll('.menu-item[data-mention-value]'), function (item) {
            return !item.disabled && item.getAttribute('aria-disabled') !== 'true';
        });
    }

    function shown(list) {
        return itemsOf(list).filter(function (item) { return !item.hidden; });
    }

    function labelOf(item) {
        return String(item.getAttribute('data-label') || item.textContent || '').trim();
    }

    function setActive(list, field, item) {
        var all = itemsOf(list);
        for (var i = 0; i < all.length; i++) {
            var on = all[i] === item;
            all[i].toggleAttribute('data-active', on);
            all[i].setAttribute('aria-selected', on ? 'true' : 'false');
        }
        if (item) {
            field.setAttribute('aria-activedescendant', item.id);
            try { item.scrollIntoView({ block: 'nearest' }); } catch (err) { /* detached */ }
        } else {
            field.removeAttribute('aria-activedescendant');
        }
    }

    function close() {
        if (!open) return;
        var was = open;
        open = null;
        was.list.hidden = true;
        setActive(was.list, was.field, null);
    }

    // The "@" the caret is after, in this field: { start, end, query } or null.
    function triggerIn(field) {
        if (field.selectionStart !== field.selectionEnd) return null;
        var end = field.selectionStart;
        var m = TRIGGER.exec(field.value.slice(0, end));
        if (!m) return null;
        return { start: end - m[2].length - 1, end: end, query: m[2] };
    }

    function update(field, typed) {
        var at = triggerIn(field);
        var list = listOf(field);
        if (!at || !list) {
            if (dismissed) dismissed.delete(field);
            if (open && open.field === field) close();
            return;
        }
        if (dismissed && dismissed.get(field) === at.start) return;
        if (!typed && !(open && open.field === field)) return;

        var all = itemsOf(list), any = false;
        for (var i = 0; i < all.length; i++) {
            var item = all[i];
            var match = !at.query
                || ui._.score(at.query, labelOf(item), 0) >= 0
                || ui._.score(at.query, item.getAttribute('data-mention-value') || '', 0) >= 0;
            item.hidden = !match;
            if (match) any = true;
        }
        if (!any) {
            if (open && open.field === field) close();
            return;
        }

        if (open && open.list !== list) close();
        list.setAttribute('role', 'listbox');
        if (!list.id) list.id = 'sedna-mention-list-' + (++ids);
        for (var k = 0; k < all.length; k++) {
            all[k].setAttribute('role', 'option');
            if (!all[k].id) all[k].id = 'sedna-mention-' + (++ids);
        }
        if (!field.hasAttribute('aria-controls')) field.setAttribute('aria-controls', list.id);
        list.hidden = false;
        open = { field: field, list: list, start: at.start, end: at.end };

        var visible = shown(list);
        var current = visible.filter(function (it) { return it.hasAttribute('data-active'); })[0];
        setActive(list, field, current || visible[0]);
    }

    function choose(item) {
        if (!open || !item) return;
        var field = open.field, start = open.start;
        var end = field.selectionStart;
        var text = '@' + item.getAttribute('data-mention-value') + ' ';
        close();
        field.setRangeText(text, start, end, 'end');
        field.dispatchEvent(new Event('input', { bubbles: true }));
    }

    function move(step) {
        var visible = shown(open.list);
        if (!visible.length) return;
        var at = -1;
        for (var i = 0; i < visible.length; i++) if (visible[i].hasAttribute('data-active')) at = i;
        setActive(open.list, open.field, visible[(at + step + visible.length) % visible.length]);
    }

    ui.mention = {
        // Closes an open list. An app calls this when it takes the field away mid-word.
        close: close
    };

    document.addEventListener('input', function (e) {
        var field = fieldOf(e.target);
        if (field) update(field, true);
    });

    // A caret moved by a key or a press re-reads the word it is in; it never opens a list.
    document.addEventListener('keyup', function (e) {
        if (!open || e.target !== open.field) return;
        if (/^(ArrowLeft|ArrowRight|Home|End)$/.test(e.key)) update(open.field, false);
    });
    document.addEventListener('click', function (e) {
        if (open && e.target === open.field) update(open.field, false);
    });

    // On the window, while capturing, so a handler of the app's on the field — Enter to
    // send — does not hear a key the list took.
    window.addEventListener('keydown', function (e) {
        if (!open || e.target !== open.field || e.isComposing) return;
        var plain = !e.altKey && !e.ctrlKey && !e.metaKey && !e.shiftKey;
        if (e.key === 'ArrowDown' && plain) move(1);
        else if (e.key === 'ArrowUp' && plain) move(-1);
        else if ((e.key === 'Enter' || e.key === 'Tab') && plain) {
            choose(shown(open.list).filter(function (it) { return it.hasAttribute('data-active'); })[0]);
        } else if (e.key === 'Escape') {
            if (dismissed) dismissed.set(open.field, open.start);
            close();
        } else return;
        e.preventDefault();
        e.stopPropagation();
    }, true);

    // A press on an item keeps the focus in the field, so the caret is still there.
    document.addEventListener('mousedown', function (e) {
        if (open && e.target instanceof Element && open.list.contains(e.target) && e.target.closest('.menu-item')) e.preventDefault();
    });
    document.addEventListener('click', function (e) {
        if (!open || !(e.target instanceof Element)) return;
        var item = e.target.closest('.menu-item[data-mention-value]');
        if (item && open.list.contains(item) && !item.hidden) choose(item);
    });
    document.addEventListener('pointermove', function (e) {
        if (!open || !(e.target instanceof Element)) return;
        var item = e.target.closest('.menu-item[data-mention-value]');
        if (item && open.list.contains(item) && !item.hidden && !item.hasAttribute('data-active')) setActive(open.list, open.field, item);
    });

    document.addEventListener('focusout', function (e) {
        if (open && e.target === open.field) close();
    });

    // The list is anchored to the field by the stylesheet, which does not follow a
    // scroll (22-anchored.js): a scroll that moves the field closes it.
    document.addEventListener('scroll', function (e) {
        if (!open) return;
        var scroller = e.target === document ? document.documentElement : e.target;
        if (!scroller || scroller.nodeType !== 1 || scroller === open.field) return;
        if (open.list === scroller || open.list.contains(scroller)) return;
        if (scroller.contains(open.field)) close();
    }, true);

})(window.sednaUi);

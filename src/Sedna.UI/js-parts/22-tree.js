/* ── Tree, delegated ─────────────────────────────────────────────────────────
   The keyboard and the state behind a `.tree`: an ARIA tree the app writes as nested
   lists.

     <ul class="tree" role="tree" aria-label="Teams" aria-multiselectable="true">
       <li role="treeitem" aria-expanded="false" aria-selected="false" data-value="ops" tabindex="0">
         <span class="tree-row">…<span class="tree-label">Operations</span></span>
         <ul role="group">…</ul>
       </li>
       <li role="treeitem" aria-expanded="false" data-tree-lazy data-value="archive" tabindex="-1">…</li>
     </ul>

   What an item can do is what it carries: `aria-expanded` opens, `aria-selected` is
   picked, `aria-checked` is ticked — `mixed` for a branch partly ticked — and
   `aria-disabled="true"` is reached but not changed. `aria-multiselectable` on the tree
   picks several. `data-value` is what an event names an item by, else its label;
   `data-label` overrides the label typeahead and a tree select read, else
   `.tree-label`, else the row's text.

   ONE TAB STOP. The tree is entered at one item, `tabindex="0"`, and every other item
   is `tabindex="-1"`; the script moves the 0 as the keyboard moves, and writes the
   -1s itself for items an app rendered without one.

     ↓ ↑                next and previous visible item
     → (← in RTL)       opens a closed branch, else goes to its first child
     ← (→ in RTL)       closes an open branch, else goes to its parent
     Home End           first and last visible item
     *                  opens every branch beside the item
     a letter           typeahead: the next item whose label starts with it
     Space              ticks or picks the item; in a multiselectable tree, toggles it
     Enter              follows the row's link, else does what Space does, else opens or
                        closes the branch
     Shift+↓ ↑          in a multiselectable tree, moves and toggles the item reached
     Ctrl+A (⌘A)        in a multiselectable tree, picks every visible item

   A click on `.tree-toggle` opens or closes; a click elsewhere on the row does what
   Space does, or opens a branch that has nothing to pick.

   THE SCRIPT HOLDS THE STATE, AND SAYS SO. It writes aria-expanded, aria-selected,
   aria-checked, aria-busy and tabindex on the app's items — attributes a render leaves
   alone while the app's own value for them does not change — and dispatches a bubbling
   event on the item after each change, so a Blazor app keeps its own copy:

     sedna-tree-expand     { item, lazy }               a branch opened
     sedna-tree-collapse   { item, lazy: false }        a branch closed
     sedna-tree-select     { item, selected, items }    picked or unpicked; `items` is
                                                        every value picked in the tree
     sedna-tree-check      { item, selected, items }    ticked or unticked; the ticks
                                                        have already gone down to its
                                                        children and up to its parents,
                                                        and `items` is every value ticked

   CHILDREN ON DEMAND. A branch whose children are not in the page yet carries
   `data-tree-lazy` and `aria-expanded="false"`. Opening it sets `aria-busy="true"` —
   the caret becomes a spinner — and sends sedna-tree-expand with `lazy: true`; the app
   renders the `role="group"` with the children and drops `data-tree-lazy`, and the busy
   state lifts as either lands.

   MOVING ITEMS is 26-drag.js, through its own attributes: `data-drag-zone` on a
   `role="group"` (or the tree), `data-drag-item` on its items. An item that drags from
   itself takes Space and Enter for the drag, as every drag item does; give it a
   `[data-drag-handle]` to keep them for picking. While a drag is under way the arrow
   keys are the drag's.

   In a combo field's panel — a tree select — the keyboard stays in the field's input
   and 23-combo.js drives the tree through `ui._.tree`.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var typed = typeof WeakMap === 'function' ? new WeakMap() : null;
    var loading = [];

    function toArray(list) { return Array.prototype.slice.call(list); }

    function isItem(el) { return !!el && el.nodeType === 1 && el.getAttribute('role') === 'treeitem'; }
    function treeOf(el) { return el && el.closest ? el.closest('[role="tree"]') : null; }
    function inCombo(tree) { return !!tree.closest('.form-combo-panel'); }

    function groupOf(item) {
        for (var c = item.firstElementChild; c; c = c.nextElementSibling) {
            if (c.getAttribute('role') === 'group') return c;
        }
        return null;
    }

    function childrenOf(item) {
        var group = groupOf(item);
        return group ? toArray(group.children).filter(isItem) : [];
    }

    function parentOf(item) {
        var group = item.parentElement;
        if (!group || group.getAttribute('role') !== 'group') return null;
        var parent = group.parentElement && group.parentElement.closest('[role="treeitem"]');
        return parent && treeOf(parent) === treeOf(item) ? parent : null;
    }

    function topItems(tree) { return toArray(tree.children).filter(isItem); }

    function itemsOf(tree) {
        return toArray(tree.querySelectorAll('[role="treeitem"]')).filter(function (i) { return treeOf(i) === tree; });
    }

    function rowOf(item) {
        for (var c = item.firstElementChild; c; c = c.nextElementSibling) {
            if (c.classList.contains('tree-row')) return c;
        }
        return null;
    }

    function labelEl(item) {
        var row = rowOf(item);
        return (row && row.querySelector('.tree-label')) || row || item;
    }

    function labelOf(item) {
        return item.hasAttribute('data-label') ? item.getAttribute('data-label') : labelEl(item).textContent.trim();
    }

    function valueOf(item) { return item.hasAttribute('data-value') ? item.getAttribute('data-value') : labelOf(item); }

    function expandable(item) { return item.hasAttribute('aria-expanded'); }
    function expanded(item) { return item.getAttribute('aria-expanded') === 'true'; }
    function disabled(item) { return item.getAttribute('aria-disabled') === 'true'; }
    function selectable(item) { return item.hasAttribute('aria-selected'); }
    function selected(item) { return item.getAttribute('aria-selected') === 'true'; }
    function checkable(item) { return item.hasAttribute('aria-checked'); }
    function multi(tree) { return tree.getAttribute('aria-multiselectable') === 'true'; }

    function rtl(tree) {
        try { return getComputedStyle(tree).direction === 'rtl'; } catch (e) { return false; }
    }

    /* Every item the reader can see, in order: not filtered out, and inside no closed
       branch. */
    function visible(tree) {
        var out = [];
        (function walk(list) {
            list.forEach(function (item) {
                if (item.hidden) return;
                out.push(item);
                if (expanded(item)) walk(childrenOf(item));
            });
        })(topItems(tree));
        return out;
    }

    function send(el, name, detail) {
        el.dispatchEvent(new CustomEvent(name, { bubbles: true, composed: true, detail: detail }));
    }

    // ── The tab stop ───────────────────────────────────────────────────────

    /* Exactly one visible item is `tabindex="0"`: the one already marked, else the
       first picked, else the first. A tree select's items are never focused. */
    function settleStop(tree) {
        if (inCombo(tree)) return;
        var all = itemsOf(tree);
        if (!all.length) return;
        var rows = visible(tree);
        var stop = rows.filter(function (i) { return i.getAttribute('tabindex') === '0'; })[0]
            || rows.filter(selected)[0] || rows[0];
        all.forEach(function (i) {
            var want = i === stop ? '0' : '-1';
            if (i.getAttribute('tabindex') !== want) i.setAttribute('tabindex', want);
        });
    }

    function focusItem(tree, item, options) {
        itemsOf(tree).forEach(function (i) {
            var want = i === item ? '0' : '-1';
            if (i.getAttribute('tabindex') !== want) i.setAttribute('tabindex', want);
        });
        item.focus(options);
    }

    // ── Opening ────────────────────────────────────────────────────────────

    function setExpanded(item, open) {
        if (!expandable(item) || expanded(item) === open) return false;
        var tree = treeOf(item);
        item.setAttribute('aria-expanded', String(open));

        var lazy = open && item.hasAttribute('data-tree-lazy');
        if (lazy && item.getAttribute('aria-busy') !== 'true') {
            item.setAttribute('aria-busy', 'true');
            loading.push(item);
        }

        // Closing the branch the keyboard is in hands the keyboard to the branch.
        if (!open && tree && !inCombo(tree)) {
            var group = groupOf(item);
            if (group && group.contains(document.activeElement)) focusItem(tree, item);
            settleStop(tree);
        }
        if (open) send(item, 'sedna-tree-expand', { item: valueOf(item), lazy: lazy });
        else send(item, 'sedna-tree-collapse', { item: valueOf(item), lazy: false });
        return true;
    }

    /* A branch's children have landed, or the app says there are none. */
    function settleLoading() {
        loading = loading.filter(function (item) {
            if (!item.isConnected) return false;
            if (item.hasAttribute('data-tree-lazy') && !childrenOf(item).length) return true;
            item.removeAttribute('aria-busy');
            return false;
        });
    }

    /* → and ← on `item`, mirrored in RTL: opens or closes it, or returns the item the
       keyboard goes to next. */
    function step(item, key) {
        var tree = treeOf(item);
        var forward = rtl(tree) ? 'ArrowLeft' : 'ArrowRight';
        if (key === forward) {
            if (expandable(item) && !expanded(item)) { setExpanded(item, true); return null; }
            if (expanded(item)) return childrenOf(item).filter(function (c) { return !c.hidden; })[0] || null;
            return null;
        }
        if (expanded(item)) { setExpanded(item, false); return null; }
        return parentOf(item);
    }

    // ── Picking ────────────────────────────────────────────────────────────

    function selectedValues(tree) {
        return itemsOf(tree).filter(selected).map(valueOf);
    }

    function checkedValues(tree) {
        return itemsOf(tree).filter(function (i) { return i.getAttribute('aria-checked') === 'true'; }).map(valueOf);
    }

    function setSelected(item, on) {
        var tree = treeOf(item);
        if (!tree || !selectable(item)) return false;
        if (on && !multi(tree)) {
            itemsOf(tree).forEach(function (i) {
                if (i !== item && selected(i)) i.setAttribute('aria-selected', 'false');
            });
        }
        if (selected(item) === on) return false;
        item.setAttribute('aria-selected', String(on));
        send(item, 'sedna-tree-select', { item: valueOf(item), selected: on, items: selectedValues(tree) });
        return true;
    }

    /* What a parent's tick is, from its children's: all ticked, none ticked, or mixed. */
    function mixOf(parent) {
        var kids = childrenOf(parent).filter(checkable);
        if (!kids.length) return parent.getAttribute('aria-checked');
        var on = 0, off = 0;
        kids.forEach(function (k) {
            var state = k.getAttribute('aria-checked');
            if (state === 'true') on++;
            else if (state === 'false') off++;
        });
        return on === kids.length ? 'true' : off === kids.length ? 'false' : 'mixed';
    }

    /* Ticks or unticks `item` and every enabled item under it, then works each parent's
       tick out again on the way up. */
    function setChecked(item, on) {
        var tree = treeOf(item);
        if (!tree || !checkable(item)) return false;
        var state = on ? 'true' : 'false';
        if (item.getAttribute('aria-checked') === state && childrenOf(item).every(function (c) {
            return !checkable(c) || disabled(c) || c.getAttribute('aria-checked') === state;
        })) return false;

        item.setAttribute('aria-checked', state);
        toArray(item.querySelectorAll('[role="treeitem"][aria-checked]')).forEach(function (d) {
            if (!disabled(d) && d.getAttribute('aria-checked') !== state) d.setAttribute('aria-checked', state);
        });
        for (var p = parentOf(item); p && checkable(p); p = parentOf(p)) {
            var mix = mixOf(p);
            if (p.getAttribute('aria-checked') !== mix) p.setAttribute('aria-checked', mix);
        }
        send(item, 'sedna-tree-check', { item: valueOf(item), selected: on, items: checkedValues(tree) });
        return true;
    }

    /* What Space does: ticks a checkable item, picks a selectable one — toggling it in a
       multiselectable tree. Returns whether the item had anything to pick. */
    function pick(item) {
        if (disabled(item)) return checkable(item) || selectable(item);
        if (checkable(item)) { setChecked(item, item.getAttribute('aria-checked') !== 'true'); return true; }
        if (selectable(item)) {
            var tree = treeOf(item);
            setSelected(item, multi(tree) ? !selected(item) : true);
            return true;
        }
        return false;
    }

    /* What a tree select shows as its picks: the items picked, or for a tree that ticks,
       each ticked item whose parent is not ticked too — a whole branch is one pick. */
    function picked(tree) {
        var items = itemsOf(tree);
        if (items.some(checkable)) {
            return items.filter(function (i) {
                if (i.getAttribute('aria-checked') !== 'true') return false;
                var p = parentOf(i);
                return !(p && p.getAttribute('aria-checked') === 'true');
            });
        }
        return items.filter(selected);
    }

    /* Where a tree select hides what does not match its query: an item stays while it
       or anything under it matches, and a branch holding a match opens. */
    function filter(tree, query) {
        var items = itemsOf(tree);
        if (!query) {
            items.forEach(function (i) { if (i.hidden) i.hidden = false; });
            return;
        }
        // Deepest first, so each item's children are settled before it is.
        for (var n = items.length - 1; n >= 0; n--) {
            var item = items[n];
            var under = childrenOf(item).some(function (c) { return !c.hidden; });
            var keep = under || ui._.score(query, labelOf(item), 0) >= 0;
            if (item.hidden === keep) item.hidden = !keep;
            if (under) setExpanded(item, true);
        }
    }

    function sameOwner(item, tree) { return !!item && treeOf(item) === tree; }

    ui._.tree = {
        items: itemsOf,
        visible: visible,
        labelEl: labelEl,
        labelOf: labelOf,
        valueOf: valueOf,
        rowOf: rowOf,
        step: step,
        pick: pick,
        picked: picked,
        setSelected: setSelected,
        setChecked: setChecked,
        filter: filter,
        disabled: disabled
    };

    // ── Delegated handlers ─────────────────────────────────────────────────

    document.addEventListener('focusin', function (e) {
        var item = e.target;
        if (!isItem(item)) return;
        var tree = treeOf(item);
        if (!tree || inCombo(tree) || item.getAttribute('tabindex') === '0') return;
        itemsOf(tree).forEach(function (i) {
            var want = i === item ? '0' : '-1';
            if (i.getAttribute('tabindex') !== want) i.setAttribute('tabindex', want);
        });
    });

    document.addEventListener('click', function (e) {
        var target = e.target;
        if (!(target instanceof Element)) return;
        var row = target.closest('.tree-row');
        var item = row && row.parentElement;
        if (!isItem(item)) return;
        var tree = treeOf(item);
        if (!tree) return;

        if (!inCombo(tree) && document.activeElement !== item) focusItem(tree, item, { preventScroll: true });
        if (target.closest('.tree-toggle')) { setExpanded(item, !expanded(item)); return; }
        if (pick(item)) return;
        if (expandable(item) && !target.closest('a[href]')) setExpanded(item, !expanded(item));
    });

    /* An item that drags from itself — no handle, or one for the pointer alone: Space and
       Enter are the drag's. */
    function dragsItself(item) {
        if (!item.hasAttribute('data-drag-item')) return false;
        var handles = item.querySelectorAll('[data-drag-handle]');
        for (var i = 0; i < handles.length; i++) {
            if (handles[i].closest('[data-drag-item]') === item && handles[i].getAttribute('data-drag-handle') !== 'pointer') return false;
        }
        return true;
    }

    document.addEventListener('keydown', function (e) {
        var item = e.target;
        if (!isItem(item) || e.isComposing || e.altKey) return;
        var tree = treeOf(item);
        if (!tree || inCombo(tree)) return;
        if (ui.drag && ui.drag.active && ui.drag.active() !== null) return;

        var rows = visible(tree), at = rows.indexOf(item), to = null;
        var key = e.key;

        if (e.ctrlKey || e.metaKey) {
            if ((key === 'a' || key === 'A') && multi(tree)) {
                e.preventDefault();
                rows.forEach(function (i) { if (!disabled(i)) setSelected(i, true); });
            }
            return;
        }

        switch (key) {
            case 'ArrowDown': to = rows[at + 1]; break;
            case 'ArrowUp': to = rows[at - 1]; break;
            case 'Home': to = rows[0]; break;
            case 'End': to = rows[rows.length - 1]; break;
            case 'ArrowRight':
            case 'ArrowLeft':
                e.preventDefault();
                to = step(item, key);
                if (!to) { settleStop(tree); return; }
                break;
            case '*':
                e.preventDefault();
                (parentOf(item) ? childrenOf(parentOf(item)) : topItems(tree)).forEach(function (i) { setExpanded(i, true); });
                return;
            case ' ':
                if (dragsItself(item)) return;
                e.preventDefault();
                pick(item);
                return;
            case 'Enter':
                if (dragsItself(item)) return;
                var row = rowOf(item);
                var link = row && (row.matches('a[href]') ? row : row.querySelector('a[href]'));
                e.preventDefault();
                // The link's own click reaches the handler above, which picks the item.
                if (link) { link.click(); return; }
                if (!pick(item) && expandable(item)) setExpanded(item, !expanded(item));
                return;
            default:
                if (key.length !== 1) return;
                var state = typed ? typed.get(tree) : null;
                if (!state) { state = {}; if (typed) typed.set(tree, state); }
                to = ui._.typeahead(state, key, rows, item, labelOf);
                if (!to) return;
        }

        if (!sameOwner(to, tree)) return;
        e.preventDefault();
        focusItem(tree, to);
        if (e.shiftKey && multi(tree) && (key === 'ArrowDown' || key === 'ArrowUp') && !disabled(to)) {
            setSelected(to, !selected(to));
        }
    });

    /* After a render: new trees get their tab stop, a stop that went with its item is
       given back, and a branch whose children have arrived stops spinning. Attributes
       are not observed but `data-tree-lazy`, so the tabindex this writes does not wake
       it again. */
    function sweep() {
        toArray(document.querySelectorAll('[role="tree"]')).forEach(settleStop);
        settleLoading();
    }

    if (typeof MutationObserver === 'function') {
        var queued = false;
        new MutationObserver(function () {
            if (queued) return;
            queued = true;
            Promise.resolve().then(function () { queued = false; sweep(); });
        }).observe(document.documentElement, {
            childList: true, subtree: true, attributes: true, attributeFilter: ['data-tree-lazy']
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', sweep);
    else sweep();

})(window.sednaUi);

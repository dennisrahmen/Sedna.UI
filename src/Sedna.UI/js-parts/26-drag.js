/* ── Drag and drop, delegated ────────────────────────────────────────────────
   Moving the app's own items: reordering a list, carrying a card to another lane,
   rearranging tiles. Not files — a file dropped from the desktop is 26-dropzone.js.

     <ul class="drag-list" data-drag-zone="queue" aria-label="Queue">
       <li class="drag-item" data-drag-item="ORD-4204">…</li>
       <li class="drag-item" data-drag-item="ORD-4209">
         <button class="drag-handle" type="button" data-drag-handle aria-label="Move ORD-4209">…</button>
         …
       </li>
     </ul>

   A zone is the element carrying data-drag-zone; its items are its DIRECT children
   carrying data-drag-item. Anything else inside the zone — a heading, a .drag-empty —
   is not counted. An item with a [data-drag-handle] of its own drags from the handle
   and nowhere else, so the rest of it stays selectable and clickable.
   `data-drag-handle="pointer"` is a handle for the pointer alone: a control that also
   does a job of its own — a header cell's sort button — drags the item under a mouse
   or a finger and stays that control for the keyboard, which picks up the item itself.

     data-drag-group="board"    zones sharing a group trade items; a zone with none
                                only reorders its own
     data-drag-accept="task"    the item types a zone takes, space-separated; an
                                empty value takes nothing, which is a source-only zone
     data-drag-type="task"      an item's type, for data-drag-accept
     data-drag-axis="grid"      how items are laid out: y (default), x or grid. A table's
                                header row is a zone along x, its header cells the items.
                                `slot` is a place with no order of its own: see below.
                                `cells` is a .panel-grid, whose panels are placed on
                                cells: 26-panels.js says what changes there
     data-drag-label="…"        what an announcement calls an item or a zone; an item
                                falls back to its text, a zone to its aria-label
     data-drag-effect="copy"    on a zone or an item: what is dropped is a copy, and the
                                original stays where it is. See COPIES below
     aria-disabled="true"       on an item, it does not lift; on a zone, nothing lifts
                                from it or lands in it

   THE LIST IS THE APP'S. Nothing here moves a node. Blazor holds references to every
   item it rendered, and a node this file inserted elsewhere would be put back — or
   worse, diffed against the wrong sibling — on the next render. So a drag changes
   attributes only, and the move itself is an event the app handles:

     sedna-dragstart   on the item, cancelable       { item, items, type, zone, index, keyboard, effect }
     sedna-drop        on the zone it landed in      { item, items, type, from, to, index, fromIndex, keyboard, fromSlot, effect }
     sedna-dragend     on the item, always, last     { item, items, type, zone, index, keyboard, dropped, effect }

   `index` in sedna-drop is where the item goes in `to` once it has left `from`, so
   the app's whole handler is a remove and an insert. A drop back where it started
   sends no sedna-drop.

   SLOTS. A zone with data-drag-axis="slot" is a place an item is put rather than a list
   it joins — a day of a month, a half hour of a week. It holds no items; the items come
   from a zone of their own, usually one that takes nothing back (data-drag-accept="").
   A slot under an item that refuses the drop is still found, so a pill dropped on top of
   another day's pill lands on that day. sedna-drop then names the slot as `to`, and
   `fromSlot` names the slot the drag started over — under the pointer where it lifted,
   or under the item's own start for the keyboard — so the app can move the item by the
   distance between the two. Dropped back on `fromSlot` it sends no sedna-drop. From the
   keyboard the item starts on that slot, and the arrows move it to the nearest slot
   that way on screen. Every other drop has `fromSlot: null`.

   COPIES. data-drag-effect="copy" on the zone an item is dragged from — a palette, a
   list of templates — or on the item itself makes every drag from it a copy: `effect` is
   "copy" in all three events, and the app adds the item to `to` and leaves `from` as it
   is. The original stays where it is, outlined, under the pointer and from the keyboard
   alike; what travels under a pointer is its name, read the way an announcement reads
   it, in a box the item's size drawn by the stylesheet from --drag-name. Nothing leaves
   the zone, so the zone it came from does not take it back, and sedna-dragend names the
   original's own place. data-drag-effect="move" on an item inside a copying zone moves
   that one. Every other drag has `effect: "move"`.

   SEVERAL AT ONCE. An item lifted while it is selected carries every other selected
   item of its zone with it. Selected is what the app already writes: aria-selected="true"
   on the item — a table row, an option — or a checked input[data-drag-select] inside
   it, which is a tile's own checkbox. `items` lists the ids carried, in document order,
   and is just [item] for an item lifted alone; `index` then counts `to` without any of
   them, so the handler removes all of `items` and inserts them there, in that order.
   A zone takes them only if it takes every one. The others stay where they are, marked
   data-drag-carried, and the lifted item carries data-drag-count for the stylesheet to
   show how many are travelling. Every attribute below is cleared BEFORE the events go out, so
   the render they cause starts from the app's own markup.

   What a drag writes, all of it on elements the app wrote:

     data-dragging="pointer|keyboard"   the item being dragged. Under a pointer it is also
                                        raised as a manual popover, with --drag-left / -top
                                        / -width / -height pinning its box and --drag-x /
                                        --drag-y carrying it; see `lift` below
     data-drop-state="valid|invalid"    every zone the item could reach
     data-drop-over                     the zone under the pointer
     data-drop-edge="before|after"      the item the insertion line is drawn against
     data-drag-slot="start|end|column"  the neighbour holding a raised item's slot open
     data-drag-carried                  the other selected items travelling with it
     data-drag-count="3"                on the dragged item, when it carries others
     data-drag-copy                     on the dragged item and those it carries, for a copy

   The lifted item, the line and the zone states are all CSS on those attributes
   (63-drag.css). Nothing is drawn: there is no ghost element and no placeholder — a
   copy's travelling name is the stylesheet's ::before on the original, from the
   --drag-name this writes on its style.

   Input, three ways:

     · mouse and pen lift after a few pixels of movement, so a click is still a click;
     · a finger lifts after the same press-and-hold as a hover hint (`ui._.hold`), and
       a finger that moves first is scrolling. On a handle it lifts at once, because a
       handle has `touch-action: none` and cannot be scrolled from anyway;
     · Space or Enter on a focused item — or its handle — picks it up, the arrows move
       it, Space or Enter drops and Escape puts it back. The words a screen reader hears
       are the app's: see `announce` below.

   Near the edge of anything that scrolls, a pointer drag scrolls it, the viewport
   included.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var SLOP = 4;          // px a mouse or a handle press moves before it is a drag
    var TOUCH_SLOP = 10;   // px a finger may drift during the hold before it is a scroll
    var EDGE = 48;         // px from a scroller's edge where auto-scroll starts
    var SPEED = 16;        // px per frame at the edge itself

    // A resize grip is a control too: a press on a column's grip resizes the column.
    var CONTROL = 'a[href], button, input, select, textarea, label, summary, [contenteditable], [data-resize]';

    var drag = null;           // the drag in progress
    var press = null;          // a press that may become one
    var swallowUntil = 0;      // the click that follows a pointer drag is not a click
    var swallowKeyup = false;  // nor is the keyup of the Space that picked an item up
    var cancelledPointer = null; // a pointer drag Escape ended, whose release is still to come

    function toArray(list) { return Array.prototype.slice.call(list); }

    function isZone(el) { return !!el && el.nodeType === 1 && el.hasAttribute('data-drag-zone'); }

    function zoneOf(item) { return isZone(item.parentElement) ? item.parentElement : null; }

    function itemsOf(zone) {
        return toArray(zone.children).filter(function (el) {
            return el.hasAttribute('data-drag-item');
        });
    }

    function disabled(el) { return el.getAttribute('aria-disabled') === 'true'; }

    /* Selected the way the app already says so: aria-selected on a row or an option, or
       the item's own checkbox — a tile's .file-tile-check — marked data-drag-select. */
    function selected(item) {
        if (item.getAttribute('aria-selected') === 'true') return true;
        var boxes = item.querySelectorAll('input[data-drag-select]');
        for (var i = 0; i < boxes.length; i++) {
            if (boxes[i].checked && boxes[i].closest('[data-drag-item]') === item) return true;
        }
        return false;
    }

    /* What lifting `item` carries: the item alone, or — when it is itself selected —
       every selected item of its zone that can be lifted, in document order. */
    function carriedWith(item, from) {
        if (!selected(item) || isCells(from)) return [item];
        return itemsOf(from).filter(function (el) {
            return el === item || (selected(el) && !disabled(el));
        });
    }

    /* A zone's items the drop is placed among: everything but what is being carried. */
    function others(zone) {
        return toArray(zone.children).filter(function (el) {
            return el.hasAttribute('data-drag-item') && drag.carried.indexOf(el) === -1;
        });
    }

    function handleOf(item) {
        var handles = item.querySelectorAll('[data-drag-handle]');
        for (var i = 0; i < handles.length; i++) {
            if (handles[i].closest('[data-drag-item]') === item) return handles[i];
        }
        return null;
    }

    /* A handle for the pointer alone; the keyboard picks up the item itself. */
    function pointerOnly(handle) { return handle.getAttribute('data-drag-handle') === 'pointer'; }

    /* What a keyboard picks the item up from: its handle, else the item. */
    function keyOwner(item) {
        var handle = handleOf(item);
        return handle && !pointerOnly(handle) ? handle : item;
    }

    /* "copy" when what is dropped is a copy and the original stays: the item's own
       data-drag-effect, else its zone's. Anything else is a move. */
    function effectOf(item, from) {
        var own = item.getAttribute('data-drag-effect');
        if (isCells(from)) return 'move';
        return (own || from.getAttribute('data-drag-effect')) === 'copy' ? 'copy' : 'move';
    }

    function axisOf(zone) { return zone.getAttribute('data-drag-axis') || 'y'; }

    /* A panel grid, `data-drag-axis="cells"`: its panels are placed on cells rather than in
       an order, and 26-panels.js works out where they go. Each takes only its own panels,
       one at a time, and lifts nothing outside its arrange mode. A cell travels through
       the code below as one `index`, row by row: y × columns + x. */
    function isCells(zone) { return !!zone && axisOf(zone) === 'cells' && !!ui._.panels; }

    function resting(zone) { return isCells(zone) && ui._.panels.resting(zone); }

    function cellOf(index) { var c = drag.cells.cols; return { x: index % c, y: Math.floor(index / c) }; }

    function indexOfCell(cell) { return cell.y * drag.cells.cols + cell.x; }

    /* Where the panel being dragged is in the arrangement shown — its reading-order
       position and its cell — or where it started. */
    function panelNow(back) {
        var s = drag.cells, list = (!back && s.result) || s.start;
        var p = ui._.panels.find(list, drag.detail.item);
        return { position: list.indexOf(p), x: p.x, y: p.y };
    }

    function isSlot(zone) { return !!zone && axisOf(zone) === 'slot'; }

    function validSlot(zone) { return isSlot(zone) && zone.getAttribute('data-drop-state') === 'valid'; }

    /* The slot that takes the item under (x, y): the first in the stack below anything the
       item itself covers, walking out from each element hit. */
    function slotAt(x, y, from) {
        var stack = document.elementsFromPoint(x, y);
        for (var i = from || 0; i < stack.length; i++) {
            if (drag.item.contains(stack[i])) continue;
            for (var z = stack[i].closest('[data-drag-zone]'); z;
                 z = z.parentElement && z.parentElement.closest('[data-drag-zone]')) {
                if (validSlot(z)) return z;
            }
        }
        return null;
    }

    function rtl(zone) {
        try { return getComputedStyle(zone).direction === 'rtl'; } catch (e) { return false; }
    }

    /* The item a press on `target` would lift, or null. A control inside the item stays a
       control, unless it is the item's own handle. */
    function liftable(target) {
        if (!(target instanceof Element)) return null;
        var item = target.closest('[data-drag-item]');
        if (!item || !zoneOf(item) || disabled(item) || disabled(zoneOf(item)) || resting(zoneOf(item))) return null;

        var handle = handleOf(item);
        if (handle) return handle.contains(target) ? item : null;

        var control = target.closest(CONTROL);
        if (control && item.contains(control) && control !== item) return null;
        return item;
    }

    function text(el) { return (el.textContent || '').replace(/\s+/g, ' ').trim(); }

    function itemName(item) { return item.getAttribute('data-drag-label') || text(item); }

    function zoneName(zone) {
        return zone.getAttribute('data-drag-label') || zone.getAttribute('aria-label')
            || zone.getAttribute('data-drag-zone');
    }

    /* Could `item`, lifted from `from`, land in `zone`? A copy never lands where it came
       from: the original is still there. */
    function accepts(zone, item, from, effect) {
        if (disabled(zone) || item.contains(zone)) return false;
        if (isCells(zone) || isCells(from)) return zone === from;
        if (effect === 'copy' && zone === from) return false;
        if (zone !== from) {
            var group = zone.getAttribute('data-drag-group');
            if (!group || group !== from.getAttribute('data-drag-group')) return false;
        }
        var accept = zone.getAttribute('data-drag-accept');
        if (accept === null) return true;
        var type = item.getAttribute('data-drag-type') || '';
        return !!type && accept.split(/\s+/).indexOf(type) !== -1;
    }

    /* ── Events ────────────────────────────────────────────────────────────── */

    function send(el, name, detail, cancelable) {
        // Bubbling, so Blazor's own event delegation — which listens at the root — and a
        // handler on any ancestor both see it.
        return el.dispatchEvent(new CustomEvent(name, {
            bubbles: true, composed: true, cancelable: !!cancelable, detail: detail
        }));
    }

    /* ── Announcements ─────────────────────────────────────────────────────────
       A keyboard drag is read out through the app's own live region, in the app's own
       words. The nearest [data-drag-live] around the item carries one sentence per step,
       with {item}, {zone}, {position} and {count} filled in:

         <p class="visually-hidden" aria-live="assertive" data-drag-live
            data-drag-pickup="Picked up {item}. Arrow keys move it, Space drops it, Escape cancels."
            data-drag-move="{item}, position {position} of {count} in {zone}."
            data-drag-drop="Dropped {item} at position {position} of {count} in {zone}."
            data-drag-cancel="{item} is back where it was."></p>

       {items} is how many are being carried, for a sentence about a selection.
       A step with no sentence says nothing. Leave the element empty in the markup: this
       writes its text.

       A zone may carry its own sentence for a step, in the same attribute, and then it is
       the one read for that zone: the zone the item moves into or is dropped in, and the
       zone it is picked up from or put back in. A place that takes one thing — a slot, a
       button an item is dropped on — words it without a position:

         <div class="drag-list" data-drag-zone="slot-3" data-drag-accept="sequence"
              aria-label="Button 3" data-drag-move="{item} onto {zone}."
              data-drag-drop="{item} put on {zone}."></div> */
    function liveFor(el) {
        for (var n = el; n && n.nodeType === 1; n = n.parentElement) {
            var live = n.matches('[data-drag-live]') ? n : n.querySelector('[data-drag-live]');
            if (live) return live;
        }
        return null;
    }

    function announce(kind) {
        if (!drag || drag.mode !== 'keyboard') return;
        var live = liveFor(drag.item);
        if (!live) return;
        var back = kind === 'cancel' || !drag.zone;
        var zone = back ? (drag.home || drag.from) : drag.zone;
        var sentence = zone.getAttribute('data-drag-' + kind) || live.getAttribute('data-drag-' + kind);
        if (!sentence) return;

        var count = others(zone).length + 1;
        var index = back ? drag.start : drag.index;
        // On a panel grid: the place in reading order, and the cell, counted from 1.
        var cell = drag.cells ? panelNow(back) : null;
        if (cell) index = cell.position;
        var said = sentence.replace(/\{(item|items|zone|position|count|column|row)\}/g, function (all, name) {
            if (name === 'item') return drag.name;
            if (name === 'items') return String(drag.carried.length);
            if (name === 'zone') return zoneName(zone);
            if (name === 'position') return String(index + 1);
            if (name === 'column') return cell ? String(cell.x + 1) : '';
            if (name === 'row') return cell ? String(cell.y + 1) : '';
            return String(count);
        });
        // The same sentence twice is not a change, so a screen reader would stay silent
        // on the second "position 1 of 1". A trailing no-break space makes it one.
        live.textContent = live.textContent === said ? said + ' ' : said;
    }

    /* ── Marking ─────────────────────────────────────────────────────────────── */

    function clearEdge() {
        if (drag.edge) drag.edge.removeAttribute('data-drop-edge');
        drag.edge = null;
    }

    function setOver(zone) {
        if (drag.over === zone) return;
        if (drag.over) drag.over.removeAttribute('data-drop-over');
        drag.over = zone;
        if (zone) zone.setAttribute('data-drop-over', '');
    }

    /* The insertion line: `before` the item the drop would land in front of, or `after`
       the last one. In a grid, a slot at the start of a row the pointer is not on is
       drawn after the tile that ends the row above, where the pointer is. */
    function markEdge(zone, index, y) {
        clearEdge();
        var items = others(zone);
        if (!items.length) return;

        var el = items[Math.min(index, items.length - 1)];
        var edge = index < items.length ? 'before' : 'after';

        if (edge === 'before' && index > 0 && axisOf(zone) === 'grid' && y !== undefined) {
            var prev = items[index - 1].getBoundingClientRect();
            if (y <= prev.bottom && el.getBoundingClientRect().top > prev.top) {
                el = items[index - 1];
                edge = 'after';
            }
        }
        el.setAttribute('data-drop-edge', edge);
        drag.edge = el;
    }

    function target(zone, index, y) {
        drag.zone = zone;
        drag.index = index;
        setOver(zone);
        var valid = !!zone && zone.getAttribute('data-drop-state') === 'valid';
        // A panel grid draws no line: the panels move aside for the one carried, and the
        // grid marks the cell it would land on. Off the grid, they go back.
        if (drag.cells) {
            if (valid) ui._.panels.moveTo(drag.cells, drag.detail.item, cellOf(index));
            else ui._.panels.moveTo(drag.cells, drag.detail.item, cellOf(drag.start));
            return;
        }
        if (valid) markEdge(zone, index, y);
        else clearEdge();
    }

    /* ── Start and finish ────────────────────────────────────────────────────── */

    function begin(item, mode, x, y) {
        var from = zoneOf(item);
        var all = itemsOf(from);
        var fromIndex = all.indexOf(item);
        var carried = carriedWith(item, from);
        // Where the carried items sit among the rest, and whether they sit together: a
        // run already together, dropped where it is, changes nothing and sends no drop.
        var first = all.indexOf(carried[0]);
        var start = first - all.slice(0, first).filter(function (el) { return carried.indexOf(el) !== -1; }).length;
        var together = all.indexOf(carried[carried.length - 1]) - first === carried.length - 1;
        var effect = effectOf(item, from);
        var detail = {
            item: item.getAttribute('data-drag-item'),
            items: carried.map(function (el) { return el.getAttribute('data-drag-item'); }),
            type: item.getAttribute('data-drag-type'),
            zone: from.getAttribute('data-drag-zone'),
            index: fromIndex,
            keyboard: mode === 'keyboard',
            effect: effect
        };
        if (!send(item, 'sedna-dragstart', detail, true)) return false;

        var rect = item.getBoundingClientRect();
        drag = {
            item: item, mode: mode, from: from, fromIndex: fromIndex, detail: detail,
            carried: carried, start: start, together: together, copy: effect === 'copy',
            name: itemName(item),
            hadStyle: item.hasAttribute('style'),
            grabX: x - rect.left, grabY: y - rect.top, x: x, y: y, x0: x, y0: y, tx: 0, ty: 0,
            left0: rect.left, top0: rect.top, width: rect.width, ref: null,
            zone: null, index: fromIndex, over: null, edge: null, zones: [], frame: 0, cells: null
        };
        if (isCells(from)) {
            // The arrangement as it is, and the panel's own cell as where it starts.
            drag.cells = ui._.panels.begin(from);
            start = drag.start = drag.index = indexOfCell(ui._.panels.find(drag.cells.start, detail.item));
        }

        // Every zone this item could reach, marked either way, so the reader sees where
        // it can go before moving at all. Zones in another group are left alone.
        toArray(document.querySelectorAll('[data-drag-zone]')).forEach(function (zone) {
            var group = zone.getAttribute('data-drag-group');
            if (zone !== from && (!group || group !== from.getAttribute('data-drag-group'))) return;
            var takes = carried.every(function (el) { return accepts(zone, el, from, effect); });
            zone.setAttribute('data-drop-state', takes ? 'valid' : 'invalid');
            drag.zones.push(zone);
        });

        // The slot the drag starts over, when it is going to slots at all: under the
        // pointer, or under the item's own start for the keyboard.
        drag.home = null;
        if (drag.zones.some(validSlot)) {
            var flipped = rtl(from);
            drag.home = mode === 'keyboard'
                ? slotAt(flipped ? rect.right - 4 : rect.left + 4, rect.top + Math.min(4, rect.height / 2))
                : slotAt(x, y);
        }

        item.setAttribute('data-dragging', mode);
        if (carried.length > 1) {
            item.setAttribute('data-drag-count', String(carried.length));
            carried.forEach(function (el) { if (el !== item) el.setAttribute('data-drag-carried', ''); });
        }
        if (drag.copy) {
            carried.forEach(function (el) { el.setAttribute('data-drag-copy', ''); });
            // The words the travelling copy shows: the app's, as a CSS string for `content`.
            item.style.setProperty('--drag-name', '"' + drag.name.replace(/["\\]/g, '\\$&') + '"');
        }
        if (mode === 'pointer') lift(item, rect);
        if (ui.tips && ui.tips.hide) ui.tips.hide();
        try { window.getSelection().removeAllRanges(); } catch (e) { /* nothing selected */ }

        if (mode === 'keyboard') {
            if (drag.home) target(drag.home, 0);
            else target(from, start);
            announce('pickup');
        } else {
            target(from, start, y);
            tick();
        }
        return true;
    }

    var BOX = ['--drag-x', '--drag-y', '--drag-left', '--drag-top', '--drag-width', '--drag-height', '--drag-name'];

    /* Raises the carried item into the top layer, as a manual popover, where it is the
       box the reader holds: nothing clips it — not a .card's overflow, not a scrolling
       lane, not the page — and no z-index outranks it. Pinned at the box it had, and
       carried from there by --drag-x / --drag-y.

       A popover leaves the flow, and a list that closed up behind it would move every
       row out from under a pointer that was aimed before the lift. So the slot is held
       open on a neighbour, measured rather than predicted — the neighbour's displacement
       is exactly what the item took up, whatever gap or margin produced it — and the
       layout the reader aimed at stays put for the whole drag. In a grid the next tile
       is pinned to its own column instead, which keeps every tile in its cell.

       All of it in one task, so neither the popover's hidden state nor the closed-up list
       is ever painted.

       A table cell is not raised: out of its row it is no longer a cell, and every cell
       after it would shift a column. A header cell is carried along its row in place. */
    function lift(item, rect) {
        drag.lifted = false;
        drag.slot = null;
        if (typeof item.showPopover !== 'function' || item.hasAttribute('popover')) return;
        if (getComputedStyle(item).display === 'table-cell') return;

        var zone = drag.from;
        var axis = axisOf(zone);
        var items = itemsOf(zone);
        var at = items.indexOf(item);
        var next = items[at + 1] || null;
        var prev = items[at - 1] || null;
        var nextBefore = next && next.getBoundingClientRect();
        var zoneBefore = zone.getBoundingClientRect();

        if (axis === 'grid' && next) {
            // The column the next tile is in now. Pinned there, it stays in its cell and
            // the item's cell stays empty; every tile after it follows on as before.
            var column = 1;
            for (var i = at; i >= 0 && Math.abs(items[i].getBoundingClientRect().top - nextBefore.top) < 2; i--) column++;
            slot(next, 'column', '--drag-slot-column', String(column));
        }

        item.style.setProperty('--drag-left', rect.left + 'px');
        item.style.setProperty('--drag-top', rect.top + 'px');
        item.style.setProperty('--drag-width', rect.width + 'px');
        item.style.setProperty('--drag-height', rect.height + 'px');
        item.setAttribute('popover', 'manual');
        try {
            item.showPopover();
            drag.lifted = true;
        } catch (e) {
            item.removeAttribute('popover');   // not connected, or inside something inert
            clearSlot();
            return;
        }

        // A panel is placed on its cells, not in a flow: nothing closes up behind it.
        if (axis === 'grid' || axis === 'cells') { follow(next || prev); return; }
        var inline = axis === 'x';
        if (next) {
            var after = next.getBoundingClientRect();
            var moved = inline ? Math.abs(nextBefore.left - after.left) : nextBefore.top - after.top;
            var own = parseFloat(getComputedStyle(next)[inline ? 'marginInlineStart' : 'marginBlockStart']) || 0;
            if (moved > 0) slot(next, 'start', '--drag-slot-size', (moved + own) + 'px');
        } else if (prev) {
            var zoneAfter = zone.getBoundingClientRect();
            var shrunk = inline ? zoneBefore.width - zoneAfter.width : zoneBefore.height - zoneAfter.height;
            var end = parseFloat(getComputedStyle(prev)[inline ? 'marginInlineEnd' : 'marginBlockEnd']) || 0;
            if (shrunk > 0) slot(prev, 'end', '--drag-slot-size', (shrunk + end) + 'px');
        }
        follow(next || prev);
    }

    /* A copy's original is pinned in the top layer over the place it never left, so it
       follows that place when anything scrolls: by how far a neighbour still in the flow
       has moved — or the zone, less its own scrolling, when it has none. A move needs
       none of this; its item is carried by the pointer. */
    function follow(ref) {
        drag.ref = null;
        if (!drag.copy) return;
        var el = ref || drag.from;
        var r = el.getBoundingClientRect();
        drag.ref = { el: el, own: !ref, left: r.left, top: r.top, sl: el.scrollLeft, st: el.scrollTop };
    }

    function shift() {
        var ref = drag.ref;
        if (!ref) return { x: 0, y: 0 };
        var r = ref.el.getBoundingClientRect();
        var x = r.left - ref.left, y = r.top - ref.top;
        if (ref.own) {
            x -= ref.el.scrollLeft - ref.sl;
            y -= ref.el.scrollTop - ref.st;
        }
        return { x: x, y: y };
    }

    function slot(el, kind, prop, value) {
        drag.slot = { el: el, prop: prop, hadStyle: el.hasAttribute('style') };
        el.style.setProperty(prop, value);
        el.setAttribute('data-drag-slot', kind);
    }

    function clearSlot() {
        var held = drag.slot;
        drag.slot = null;
        if (!held) return;
        held.el.removeAttribute('data-drag-slot');
        held.el.style.removeProperty(held.prop);
        if (!held.hadStyle && !held.el.getAttribute('style')) held.el.removeAttribute('style');
    }

    /* Clears everything a drag wrote, then says what happened. In that order: the drop
       handler may re-render, and it must render over the app's markup, not over ours. */
    function finish(commit) {
        if (!drag) return;
        var d = drag;
        cancelAnimationFrame(d.frame);

        var zone = d.zone;
        var landed = commit && zone && zone.getAttribute('data-drop-state') === 'valid' && d.item.isConnected;
        var moved = landed && (zone !== d.from || !d.together || d.index !== d.start) && zone !== d.home;
        // On a panel grid, a drop that leaves every panel where it was is no move.
        if (d.cells) moved = landed && ui._.panels.changed(d.cells);
        var landing = d.cells ? panelNow(!moved) : null;
        var effect = d.detail.effect;

        if (d.mode === 'keyboard') announce(moved ? 'drop' : 'cancel');

        clearEdge();
        setOver(null);
        d.zones.forEach(function (z) { z.removeAttribute('data-drop-state'); });
        clearSlot();
        if (d.lifted) {
            try { d.item.hidePopover(); } catch (e) { /* already closed */ }
            d.item.removeAttribute('popover');
        }
        d.item.removeAttribute('data-dragging');
        d.item.removeAttribute('data-drag-count');
        d.carried.forEach(function (el) {
            el.removeAttribute('data-drag-carried');
            el.removeAttribute('data-drag-copy');
        });
        BOX.forEach(function (name) { d.item.style.removeProperty(name); });
        if (!d.hadStyle && !d.item.getAttribute('style')) d.item.removeAttribute('style');
        drag = null;
        // The panels stay where the preview put them until the app renders the arrangement.
        var arranged = d.cells ? ui._.panels.end(d.cells, moved) : null;
        // A panel's place is its position in the arrangement's reading order.
        var index = landing ? landing.position : d.index;

        var id = d.detail.item;
        if (moved) {
            var dropped = {
                item: id,
                items: d.detail.items,
                type: d.detail.type,
                from: d.detail.zone,
                to: zone.getAttribute('data-drag-zone'),
                index: index,
                fromIndex: d.fromIndex,
                keyboard: d.mode === 'keyboard',
                fromSlot: d.home ? d.home.getAttribute('data-drag-zone') : null,
                effect: effect
            };
            if (arranged) {
                dropped.x = landing.x;
                dropped.y = landing.y;
                dropped.panels = arranged;
            }
            send(zone, 'sedna-drop', dropped);
        }

        // A copy's original never left: dragend names where it still is.
        var went = moved && !d.copy;
        var end = {
            item: id, items: d.detail.items, type: d.detail.type,
            zone: went ? zone.getAttribute('data-drag-zone') : d.detail.zone,
            index: went ? index : d.fromIndex,
            keyboard: d.mode === 'keyboard',
            dropped: !!moved,
            effect: effect
        };
        send(d.item.isConnected ? d.item : document, 'sedna-dragend', end);

        // A copy's original kept the focus; nothing the app renders moves it.
        if (went && d.mode === 'keyboard') refocus(id, keyOwner(d.item) !== d.item);
    }

    /* The app's render moves the item it was told about, and moving a focused node drops
       focus to <body> — a cross-zone move even replaces the node. So once the item with
       the same id is back, focus goes to it, or to its handle. Focus is not DOM state; a
       render cannot revert it. */
    function refocus(id, viaHandle) {
        var observer = null;
        var stop = setTimeout(done, 3000);

        function done() {
            clearTimeout(stop);
            if (observer) observer.disconnect();
        }

        function attempt() {
            var active = document.activeElement;
            if (active && active !== document.body && active !== document.documentElement) return false;
            var items = document.querySelectorAll('[data-drag-item]');
            for (var i = 0; i < items.length; i++) {
                if (items[i].getAttribute('data-drag-item') !== id) continue;
                var el = viaHandle ? keyOwner(items[i]) : items[i];
                el.focus({ preventScroll: false });
                return document.activeElement === el;
            }
            return false;
        }

        try {
            observer = new MutationObserver(function () { if (attempt()) done(); });
            observer.observe(document.body, { childList: true, subtree: true });
        } catch (e) { done(); }
        // A handler that re-rendered synchronously has already moved it.
        setTimeout(function () { if (attempt()) done(); }, 0);
    }

    /* ── Pointer ─────────────────────────────────────────────────────────────── */

    /* The zone under the pointer. The innermost zone that takes the item wins, so a card
       carried over a nested list inside another item still lands in the outer list when
       the inner one refuses it. When none takes it, the innermost is marked as refusing. */
    function zoneAt(x, y) {
        var hit = null;
        var stack = document.elementsFromPoint(x, y);
        for (var i = 0; i < stack.length; i++) {
            if (!drag.item.contains(stack[i])) { hit = stack[i]; break; }
        }
        drag.hit = hit;

        var innermost = null;
        for (var zone = hit && hit.closest('[data-drag-zone]'); zone;
             zone = zone.parentElement && zone.parentElement.closest('[data-drag-zone]')) {
            var state = zone.getAttribute('data-drop-state');
            if (!state) continue;
            if (state === 'valid') return zone;
            innermost = innermost || zone;
        }
        // Over something that refuses the item — another event lying over a day — a slot
        // underneath it still takes the drop.
        if (innermost && drag.zones.some(validSlot)) return slotAt(x, y, i + 1) || innermost;
        return innermost;
    }

    function indexAt(zone, x, y) {
        var items = others(zone);
        var axis = axisOf(zone);
        var flip = rtl(zone);
        for (var i = 0; i < items.length; i++) {
            var r = items[i].getBoundingClientRect();
            var midX = r.left + r.width / 2;
            var ahead = flip ? x > midX : x < midX;
            var before = axis === 'x' ? ahead
                : axis === 'grid' ? y < r.top || (y <= r.bottom && ahead)
                : y < r.top + r.height / 2;
            if (before) return i;
        }
        return items.length;
    }

    /* How far to scroll along one axis: nothing until the pointer is within EDGE of an
       edge, then faster towards it — and a little past it, where a drag carried just
       beyond a list is still meant for that list. */
    function speed(p, lo, hi) {
        if (hi - lo < EDGE * 3) return 0;
        if (p < lo - EDGE || p > hi + EDGE) return 0;
        if (p < lo + EDGE) return -Math.ceil(SPEED * Math.min(1, (lo + EDGE - p) / EDGE));
        if (p > hi - EDGE) return Math.ceil(SPEED * Math.min(1, (p - (hi - EDGE)) / EDGE));
        return 0;
    }

    function scrollable(el, axis) {
        var cs = getComputedStyle(el);
        var overflow = axis === 'y' ? cs.overflowY : cs.overflowX;
        if (!/(auto|scroll|overlay)/.test(overflow)) return false;
        return axis === 'y' ? el.scrollHeight > el.clientHeight : el.scrollWidth > el.clientWidth;
    }

    function tryScroll(el, x, y) {
        var r = el.getBoundingClientRect();
        var dy = x >= r.left && x <= r.right && scrollable(el, 'y') ? speed(y, r.top, r.bottom) : 0;
        var dx = y >= r.top && y <= r.bottom && scrollable(el, 'x') ? speed(x, r.left, r.right) : 0;
        if (!dx && !dy) return false;

        // Measured rather than predicted: a container already at that end does not move,
        // and the next one out gets its turn. Also right in RTL, where scrollLeft runs
        // negative.
        var top = el.scrollTop, left = el.scrollLeft;
        el.scrollTop += dy;
        el.scrollLeft += dx;
        return el.scrollTop !== top || el.scrollLeft !== left;
    }

    function autoScroll(x, y) {
        // Not until the pointer has travelled: an item picked up near the edge of a list
        // must not set the list scrolling under it before the reader has moved at all.
        if (!drag.travelled) {
            if (Math.max(Math.abs(x - drag.x0), Math.abs(y - drag.y0)) < EDGE / 3) return;
            drag.travelled = true;
        }
        var seen = [];
        var chains = [drag.hit, drag.over];
        for (var c = 0; c < chains.length; c++) {
            for (var n = chains[c]; n && n !== document.body && n !== document.documentElement; n = n.parentElement) {
                if (seen.indexOf(n) !== -1) continue;
                seen.push(n);
                if (tryScroll(n, x, y)) return;
            }
        }
        var root = document.scrollingElement || document.documentElement;
        var dy = speed(y, 0, window.innerHeight);
        var dx = speed(x, 0, window.innerWidth);
        if (dy || dx) root.scrollBy ? window.scrollBy(dx, dy) : (root.scrollTop += dy);
    }

    /* One frame of a pointer drag: follow the pointer, find the slot, scroll an edge.
       A frame loop rather than work in pointermove, because a scrolling container moves
       the slot under a pointer that is standing still. */
    function update() {
        var d = drag;
        if (d.lifted && d.copy) {
            // The original stays over its place, and the copy travels from there.
            var by = shift();
            d.item.style.setProperty('--drag-left', (d.left0 + by.x) + 'px');
            d.item.style.setProperty('--drag-top', (d.top0 + by.y) + 'px');
            d.tx = Math.round(d.x - d.x0 - by.x);
            d.ty = Math.round(d.y - d.y0 - by.y);
        } else if (d.lifted) {
            // Pinned to the viewport, so the distance the pointer has travelled is the offset.
            d.tx = Math.round(d.x - d.x0);
            d.ty = Math.round(d.y - d.y0);
        } else if (d.copy) {
            // Not raised, and not moved: the copy travels from the item's own box.
            var box = d.item.getBoundingClientRect();
            d.tx = Math.round(d.x - d.grabX - box.left);
            d.ty = Math.round(d.y - d.grabY - box.top);
        } else {
            // Still in the flow: from the untransformed box, which moves when anything
            // around it scrolls.
            var r = d.item.getBoundingClientRect();
            d.tx = Math.round(d.x - d.grabX - (r.left - d.tx));
            d.ty = Math.round(d.y - d.grabY - (r.top - d.ty));
        }
        d.item.style.setProperty('--drag-x', d.tx + 'px');
        d.item.style.setProperty('--drag-y', d.ty + 'px');

        var zone = zoneAt(d.x, d.y);
        var valid = zone && zone.getAttribute('data-drop-state') === 'valid';
        if (d.cells) {
            // The cell under the corner of the box the reader holds, not under the pointer.
            var left = d.x - d.grabX, top = d.y - d.grabY;
            var cell = valid ? indexOfCell(ui._.panels.cellAt(d.cells, d.detail.item,
                { left: left, top: top, right: left + d.width })) : d.index;
            if (zone !== d.zone || cell !== d.index) target(zone, cell);
            return;
        }
        var index = valid ? indexAt(zone, d.x, d.y) : 0;
        if (zone !== d.zone || index !== d.index || !d.edge) target(zone, index, d.y);
    }

    function tick() {
        if (!drag || drag.mode !== 'pointer') return;
        if (!drag.item.isConnected) { finish(false); return; }
        update();
        autoScroll(drag.x, drag.y);
        drag.frame = requestAnimationFrame(tick);
    }

    function clearPress() {
        if (!press) return;
        clearTimeout(press.timer);
        press = null;
    }

    document.addEventListener('pointerdown', function (e) {
        if (drag || press || !e.isPrimary) return;
        if (e.pointerType === 'mouse' && e.button !== 0) return;

        var item = liftable(e.target);
        if (!item) return;

        var onHandle = !!handleOf(item);
        press = { item: item, id: e.pointerId, x: e.clientX, y: e.clientY, touch: e.pointerType === 'touch', handle: onHandle, timer: 0 };

        if (press.touch && !onHandle) {
            // A finger has to hold still first; anything sooner is the page scrolling.
            press.timer = setTimeout(function () {
                if (!press) return;
                var p = press;
                press = null;
                if (begin(p.item, 'pointer', p.x, p.y)) drag.id = p.id;
            }, ui._.hold);
            return;
        }

        // Stops the press starting a text selection, and a touch on a handle starting
        // a scroll. Nothing inside a control reaches here, except the handle.
        e.preventDefault();
    });

    document.addEventListener('pointermove', function (e) {
        if (drag && drag.mode === 'pointer') {
            drag.x = e.clientX;
            drag.y = e.clientY;
            return;
        }
        if (!press || e.pointerId !== press.id) return;

        var moved = Math.max(Math.abs(e.clientX - press.x), Math.abs(e.clientY - press.y));
        if (press.touch && !press.handle) {
            if (moved > TOUCH_SLOP) clearPress();
            return;
        }
        if (moved <= SLOP) return;

        var p = press;
        press = null;
        if (begin(p.item, 'pointer', p.x, p.y)) {
            drag.id = p.id;
            drag.x = e.clientX;
            drag.y = e.clientY;
        }
    });

    function release(e, commit) {
        if (press && e.pointerId === press.id) clearPress();
        if (cancelledPointer === e.pointerId) {
            cancelledPointer = null;
            swallowUntil = e.timeStamp + 500;
        }
        if (!drag || drag.mode !== 'pointer' || e.pointerId !== drag.id) return;
        // Where the pointer let go, not where the last frame saw it.
        if (commit) {
            drag.x = e.clientX;
            drag.y = e.clientY;
            update();
        }
        swallowUntil = e.timeStamp + 500;
        finish(commit);
    }

    document.addEventListener('pointerup', function (e) { release(e, true); });
    document.addEventListener('pointercancel', function (e) { release(e, false); });

    // A drag that has begun owns the finger: without this the first move after the hold
    // starts a scroll, and the browser cancels the pointer to do it. Not passive, or the
    // browser ignores preventDefault.
    document.addEventListener('touchmove', function (e) {
        if ((drag && drag.mode === 'pointer') || (press && press.handle)) e.preventDefault();
    }, { passive: false });

    document.addEventListener('contextmenu', function (e) {
        if (press || (drag && drag.mode === 'pointer')) e.preventDefault();
    });

    document.addEventListener('click', function (e) {
        if (!swallowUntil || e.timeStamp > swallowUntil) return;
        swallowUntil = 0;
        e.preventDefault();
        e.stopPropagation();
    }, true);

    document.addEventListener('selectstart', function (e) {
        if (drag || press) e.preventDefault();
    });

    /* ── Keyboard ────────────────────────────────────────────────────────────── */

    /* The zones a keyboard drag may move between, in document order. */
    function reachable() {
        return drag.zones.filter(function (z) { return z.getAttribute('data-drop-state') === 'valid'; });
    }

    function columns(zone) {
        var items = itemsOf(zone);
        if (!items.length) return 1;
        var top = items[0].getBoundingClientRect().top;
        var n = 0;
        while (n < items.length && Math.abs(items[n].getBoundingClientRect().top - top) < 2) n++;
        return Math.max(1, n);
    }

    /* The nearest slot that takes the item in the arrow's direction on screen: along the
       same row for left and right, the same column for up and down. On screen rather than
       in the document, because a week's slots run down each day in turn. */
    function slotToward(zone, key) {
        var r = zone.getBoundingClientRect();
        var cx = r.left + r.width / 2, cy = r.top + r.height / 2;
        var across = key === 'ArrowLeft' || key === 'ArrowRight';
        var sign = key === 'ArrowLeft' || key === 'ArrowUp' ? -1 : 1;
        var best = null, bestDistance = Infinity;
        reachable().forEach(function (z) {
            if (z === zone || !isSlot(z)) return;
            var c = z.getBoundingClientRect();
            var inLine = across ? c.top < r.bottom - 1 && c.bottom > r.top + 1 : c.left < r.right - 1 && c.right > r.left + 1;
            if (!inLine) return;
            var distance = (across ? c.left + c.width / 2 - cx : c.top + c.height / 2 - cy) * sign;
            if (distance > 1 && distance < bestDistance) { best = z; bestDistance = distance; }
        });
        return best;
    }

    function move(key) {
        var zone = drag.zone && drag.zone.getAttribute('data-drop-state') === 'valid' ? drag.zone : drag.from;
        if (drag.cells) {
            // A cell across, or the next row where the panel lands somewhere else.
            var cell = ui._.panels.stepFrom(drag.cells, drag.detail.item, key);
            if (!cell) return;
            target(drag.from, indexOfCell(cell));
            try { drag.item.scrollIntoView({ block: 'nearest', inline: 'nearest' }); } catch (e) { /* old engine */ }
            announce('move');
            return;
        }
        if (isSlot(zone)) {
            var next = /^Arrow/.test(key) ? slotToward(zone, key) : null;
            if (!next) return;
            target(next, 0);
            try { next.scrollIntoView({ block: 'nearest', inline: 'nearest' }); } catch (e) { /* old engine */ }
            announce('move');
            return;
        }
        var max = others(zone).length;
        var axis = axisOf(zone);
        var flip = rtl(zone);
        var back = flip ? 'ArrowRight' : 'ArrowLeft';
        var fwd = flip ? 'ArrowLeft' : 'ArrowRight';
        var index = drag.index;
        var step = 0;      // along the list
        var hop = 0;       // to another zone

        if (key === 'Home') index = 0;
        else if (key === 'End') index = max;
        else if (key === 'PageUp') hop = -1;
        else if (key === 'PageDown') hop = 1;
        else if (axis === 'y') {
            if (key === 'ArrowUp') step = -1;
            else if (key === 'ArrowDown') step = 1;
            else if (key === back) hop = -1;
            else if (key === fwd) hop = 1;
        } else if (axis === 'x') {
            if (key === back) step = -1;
            else if (key === fwd) step = 1;
            else if (key === 'ArrowUp') hop = -1;
            else if (key === 'ArrowDown') hop = 1;
        } else {
            if (key === back) step = -1;
            else if (key === fwd) step = 1;
            else if (key === 'ArrowUp') step = -columns(zone);
            else if (key === 'ArrowDown') step = columns(zone);
        }

        if (hop) {
            var zones = reachable();
            var at = zones.indexOf(zone);
            // From a zone the item cannot land in — the one it was picked up from, when that
            // takes nothing back — the nearest zone before or after it in the document.
            if (at === -1) {
                var before = zones.filter(function (z) {
                    return !!(z.compareDocumentPosition(zone) & Node.DOCUMENT_POSITION_FOLLOWING);
                }).length;
                at = hop < 0 ? before : before - 1;
            }
            var next = zones[at + hop];
            if (!next) return;
            zone = next;
            index = Math.min(index, others(zone).length);
        } else {
            index = Math.max(0, Math.min(max, index + step));
        }

        target(zone, index);
        var shown = drag.edge || zone;
        try { shown.scrollIntoView({ block: 'nearest', inline: 'nearest' }); } catch (e) { /* old engine */ }
        announce('move');
    }

    function keyTarget(el) {
        if (!(el instanceof Element)) return null;
        var item = el.closest('[data-drag-item]');
        if (!item || !zoneOf(item) || disabled(item) || disabled(zoneOf(item)) || resting(zoneOf(item))) return null;
        return el === keyOwner(item) ? item : null;
    }

    var MOVES = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown'];

    document.addEventListener('keydown', function (e) {
        if (drag && drag.mode === 'keyboard') {
            if (e.key === 'Escape') { e.preventDefault(); finish(false); }
            else if (e.key === ' ' || e.key === 'Enter') {
                e.preventDefault();
                swallowKeyup = e.key === ' ';
                finish(true);
            }
            else if (MOVES.indexOf(e.key) !== -1) { e.preventDefault(); move(e.key); }
            return;
        }
        if (drag) {
            if (e.key === 'Escape') {
                e.preventDefault();
                cancelledPointer = drag.id;
                finish(false);
            }
            return;
        }

        // A key another part has already used — a keyboard grid's Enter, moving into the
        // cell's sort button — is not a pick-up.
        if ((e.key !== ' ' && e.key !== 'Enter') || e.repeat || e.altKey || e.ctrlKey || e.metaKey || e.defaultPrevented) return;
        var item = keyTarget(e.target);
        if (!item) return;
        // A handle is a button, and Space or Enter would click it as well.
        e.preventDefault();
        swallowKeyup = e.key === ' ';
        begin(item, 'keyboard', 0, 0);
    });

    document.addEventListener('keyup', function (e) {
        if (!swallowKeyup || e.key !== ' ') return;
        swallowKeyup = false;
        e.preventDefault();
    }, true);

    // Leaving the item — Tab, a click elsewhere — puts it back.
    document.addEventListener('focusout', function (e) {
        if (!drag || drag.mode !== 'keyboard') return;
        var owner = keyOwner(drag.item);
        if (e.target === owner && !owner.contains(e.relatedTarget)) finish(false);
    });

    ui.drag = {
        // Puts back the item being dragged, if any, and sends sedna-dragend. An app calls
        // this before tearing a zone down mid-drag.
        cancel: function () { finish(false); },
        // The id of the item being dragged, or null.
        active: function () { return drag ? drag.detail.item : null; }
    };

})(window.sednaUi);

/* ── Keyboard grid and cell editing ──────────────────────────────────────────
   A `<table role="grid">` is one tab stop. The cell that holds it carries `tabindex="0"`,
   every other cell `tabindex="-1"`, and the arrow keys move between cells — mirrored
   on a right-to-left page — with Home and
   End for the row's ends, Ctrl+Home and Ctrl+End (⌘ on a Mac) for the grid's, and
   Page Up and Page Down by a screenful of rows. A control inside a cell — a button,
   a link, a checkbox — is taken out of the tab order (`tabindex="-1"`), so Tab leaves
   the grid instead of walking every row; Enter or F2 on the cell moves into it, Escape
   back out, and the arrows still move on from a control that has no use for them.

     <table class="table" role="grid" aria-label="Order lines" aria-multiselectable="true">
       <thead><tr><th>Line</th><th class="col-num">Quantity</th></tr></thead>
       <tbody>
         <tr>
           <td>Desk lamp</td>
           <td class="col-num">12<input class="cell-editor" type="number" value="12" aria-label="Quantity, desk lamp"></td>
         </tr>
       </tbody>
     </table>

   With `aria-multiselectable="true"` on the table, Shift with an arrow, Home, End or
   a page key — or Shift with a click — selects the rectangle from where it started to
   the cell it reaches, as `aria-selected="true"` on each cell in it and `"false"` on
   each cell that left it. A move without Shift, or Escape, clears it. `range(grid)`
   reads it back as row and column numbers.

   A cell holding a `.cell-editor` is edited in place. Enter (all of it selected), F2
   (the caret at the end) or a double-click writes `data-editing` on the cell, which
   shows the editor over it, and focuses the editor. Enter commits and Escape cancels,
   both back on the cell; Tab commits and moves to the next cell, editing it if it can.
   Committing is the platform's own: the editor loses focus and fires `change` if its
   value changed, which `@bind` picks up. Cancelling puts back the value the editor had
   when editing began and fires `input`, so a binding on `oninput` is put back as well.
   The value and its validation are the app's; nothing here reads or judges it.

   A row the app puts in edit mode carries `data-editing` from the app's own render.
   Its controls stay in the tab order, focus moves to its first editor when the mode
   begins, Enter in a single-line editor clicks the row's `[data-row-save]` and Escape
   its `[data-row-cancel]` — the app's own buttons, which do the work.

   Blazor re-renders rows; a node that held focus can be replaced or removed. After any
   render the cell at the same row and column number takes the tab stop, and the focus
   when the grid had it and it fell to the page. What is written is attributes an app
   does not render on a cell — `tabindex`, `aria-selected`, `data-editing` — so a render
   neither reverts them nor fights over them.

   A `<table role="treegrid">` is the same grid over rows in a hierarchy: `aria-level`
   on each body row, `aria-expanded` on a branch, and a `.tree-toggle` in the cell that
   is the tree column. On that cell → (← in RTL) opens a closed branch, ← (→ in RTL)
   closes an open one or goes up to its parent's, and Enter opens or closes a branch
   whose cell has nothing to edit or press; a click on the toggle does the same. Every
   other key is the grid's. The script owns `hidden` on the body rows — a row is shown
   while every branch above it is open — and dispatches the tree's own events on the
   row, naming it by `data-value`, else its tree cell's text:

     sedna-tree-expand     { item, lazy }               a branch opened
     sedna-tree-collapse   { item, lazy: false }        a branch closed

   A branch whose rows are not in the page yet carries `data-tree-lazy`: opening it
   sets `aria-busy="true"` and sends `lazy: true`, and the busy state lifts when the
   app renders the rows under it or drops the attribute. `expanded(grid)` reads the open
   branches back.

   A cell the keyboard reaches is kept clear of a sticky `<thead>` and a sticky
   `<tfoot>` alike.

   `ui._.grid` is the core a later part extends: the selector a grid is found by, the
   row and cell walks, and `keys`, a list of `function (event, grid, cell)` consulted
   before the default keys — one that returns true has handled the key.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var state = new WeakMap();      // grid → { cell, row, col, want, anchor, picked, rowEdit }
    var tamed = new WeakMap();      // control → the tabindex attribute it had, or null
    var editing = new WeakMap();    // cell → { value, ending }
    var holder = null;              // the grid the reader is in, kept while a render takes its focus away

    var CONTROLS = 'a[href], button, input, select, textarea, summary, [tabindex], [contenteditable=""], [contenteditable="true"]';

    var core = {
        selector: 'table[role="grid"], table[role="treegrid"]',
        keys: [],
        gridOf: gridOf,
        rows: rowsOf,
        cells: cellsOf,
        colOf: colOf,
        cellAt: cellAt,
        activate: activate,
        refresh: refresh
    };
    ui._.grid = core;

    function gridOf(el) {
        var cell = el && el.closest ? el.closest('td, th') : null;
        return cell ? cell.closest('table') : (el && el.closest ? el.closest(core.selector) : null);
    }

    /* The cell of `el` in its own grid: the nearest cell whose table is a grid, so a
       plain table inside a cell belongs to the cell around it, and a grid inside a cell
       to itself. */
    function cellOf(el) {
        var cell = el && el.closest ? el.closest('td, th') : null;
        for (; cell; cell = cell.parentElement ? cell.parentElement.closest('td, th') : null) {
            var table = cell.closest('table');
            if (table && table.matches(core.selector)) return cell;
        }
        return null;
    }

    function stateOf(grid) {
        var s = state.get(grid);
        if (!s) {
            s = { cell: null, row: 0, col: 0, want: 0, anchor: null, picked: [], rowEdit: null };
            state.set(grid, s);
        }
        return s;
    }

    /* The rows a reader can reach: shown, and the grid's own. */
    function rowsOf(grid) {
        return Array.prototype.filter.call(grid.rows, function (r) {
            return r.cells.length > 0 && r.getClientRects().length > 0;
        });
    }

    function cellsOf(row) { return Array.prototype.slice.call(row.cells); }

    /* A cell's first column, counting the columns each cell before it spans. */
    function colOf(cell) {
        var col = 0;
        for (var c = cell.previousElementSibling; c; c = c.previousElementSibling) col += c.colSpan || 1;
        return col;
    }

    /* The cell that covers column `col` of `row`, else the row's last. */
    function cellAt(row, col) {
        var cells = row.cells, at = 0;
        for (var i = 0; i < cells.length; i++) {
            at += cells[i].colSpan || 1;
            if (col < at) return cells[i];
        }
        return cells[cells.length - 1] || null;
    }

    function rtl(grid) { return getComputedStyle(grid).direction === 'rtl'; }

    function editorOf(cell) { return cell.querySelector('.cell-editor'); }

    function rowEditing(cell) {
        var row = cell.parentElement;
        return !!(row && row.hasAttribute('data-editing'));
    }

    /* A control that keeps the arrow keys for itself: one typed into, a list, a radio
       group, a slider. */
    function keepsKeys(el) {
        if (el.isContentEditable) return true;
        var tag = el.tagName;
        if (tag === 'TEXTAREA' || tag === 'SELECT') return true;
        if (tag !== 'INPUT') return false;
        return !/^(button|checkbox|submit|reset|image|color|file)$/i.test(el.type);
    }

    /* Focus fell to the page — its node was removed or hidden — and not away from it. */
    function lostFocus() {
        var a = document.activeElement;
        return (a === null || a === document.body) && document.hasFocus();
    }

    /* ── The tab stop ───────────────────────────────────────────────────────── */

    function controlsIn(cell) {
        return Array.prototype.filter.call(cell.querySelectorAll(CONTROLS), function (el) {
            return !el.classList.contains('cell-editor') && cellOf(el) === cell;
        });
    }

    /* Takes a cell's controls out of the tab order, or puts back those of a row in
       edit mode, whose controls are walked with Tab like any form's. */
    function tame(grid) {
        var all = grid.querySelectorAll(CONTROLS);
        for (var i = 0; i < all.length; i++) {
            var el = all[i];
            if (el.classList.contains('cell-editor')) continue;
            var cell = cellOf(el);
            if (!cell || gridOf(cell) !== grid || el === cell) continue;
            if (rowEditing(cell)) {
                if (tamed.has(el)) {
                    var was = tamed.get(el);
                    tamed.delete(el);
                    if (was === null) el.removeAttribute('tabindex');
                    else el.setAttribute('tabindex', was);
                }
            } else if (!tamed.has(el) && el.getAttribute('tabindex') !== '-1') {
                tamed.set(el, el.getAttribute('tabindex'));
                el.setAttribute('tabindex', '-1');
            }
        }
    }

    /* Makes `cell` the grid's one tab stop, remembering where it is. */
    function activate(grid, cell, keepWant) {
        var s = stateOf(grid);
        if (s.cell && s.cell !== cell && s.cell.isConnected && gridOf(s.cell) === grid) s.cell.setAttribute('tabindex', '-1');
        if (cell.getAttribute('tabindex') !== '0') cell.setAttribute('tabindex', '0');
        var rows = rowsOf(grid);
        s.cell = cell;
        s.row = Math.max(0, rows.indexOf(cell.parentElement));
        s.col = colOf(cell);
        if (!keepWant) s.want = s.col;
    }

    /* Moves focus to a cell and keeps it clear of a pinned column or a sticky header. */
    function go(grid, cell, keepWant) {
        activate(grid, cell, keepWant);
        cell.focus();
        reveal(cell);
    }

    function scroller(el, axis) {
        for (var p = el.parentElement; p && p !== document.body; p = p.parentElement) {
            var o = getComputedStyle(p)[axis === 'x' ? 'overflowX' : 'overflowY'];
            if ((o === 'auto' || o === 'scroll') && (axis === 'x' ? p.scrollWidth > p.clientWidth : p.scrollHeight > p.clientHeight)) return p;
        }
        return null;
    }

    /* Focus scrolls a cell into its scroller, which counts a pinned column and a sticky
       header as room to show it in. The band left between them is where it has to be:
       pins are found by their computed position, so one the app wrote itself counts
       too, and the scroll is physical, as scrollLeft is in every direction. */
    function reveal(cell) {
        var x = scroller(cell, 'x');
        if (x && getComputedStyle(cell).position !== 'sticky') {
            var box = x.getBoundingClientRect();
            var left = box.left + x.clientLeft, right = left + x.clientWidth, middle = (left + right) / 2;
            var cells = cell.parentElement.cells;
            for (var i = 0; i < cells.length; i++) {
                if (cells[i] === cell || getComputedStyle(cells[i]).position !== 'sticky') continue;
                var pin = cells[i].getBoundingClientRect();
                if (pin.left + pin.width / 2 < middle) left = Math.max(left, pin.right);
                else right = Math.min(right, pin.left);
            }
            var r = cell.getBoundingClientRect();
            if (r.left < left) x.scrollLeft -= left - r.left;
            else if (r.right > right) x.scrollLeft += Math.min(r.right - right, r.left - left);
        }
        var y = scroller(cell, 'y');
        if (y) {
            var yb = y.getBoundingClientRect();
            var top = yb.top + y.clientTop, bottom = top + y.clientHeight;
            var table = cell.closest('table'), head = table.tHead, foot = table.tFoot;
            if (head && !head.contains(cell) && getComputedStyle(head).position === 'sticky') top = Math.max(top, head.getBoundingClientRect().bottom);
            if (foot && !foot.contains(cell) && getComputedStyle(foot).position === 'sticky') bottom = Math.min(bottom, foot.getBoundingClientRect().top);
            var c = cell.getBoundingClientRect();
            if (c.top < top) y.scrollTop -= top - c.top;
            else if (c.bottom > bottom) y.scrollTop += Math.min(c.bottom - bottom, c.top - top);
        }
    }

    /* After a render: the tab stop is on a cell of the grid, the controls are out of
       the tab order, a row that entered edit mode has the focus in its first editor,
       and focus that fell to the page when its node went is back on the cell where it
       was. */
    function refresh(grid) {
        if (!grid) {
            var all = document.querySelectorAll(core.selector);
            for (var g = 0; g < all.length; g++) refresh(all[g]);
            return;
        }
        var s = stateOf(grid);
        if (isTree(grid)) { showBranches(grid); settleLoading(); }
        var rows = rowsOf(grid);
        if (!rows.length) return;
        tame(grid);

        var lost = holder === grid && lostFocus();
        var cell = s.cell;
        if (!cell || !cell.isConnected || gridOf(cell) !== grid || rows.indexOf(cell.parentElement) < 0) {
            if (s.cell) {
                // Where the reader was: the same row number, the column they were keeping.
                cell = cellAt(rows[Math.min(s.row, rows.length - 1)], s.want);
                activate(grid, cell, true);
            } else {
                cell = firstStop(grid, rows);
                activate(grid, cell);
            }
            if (lost) cell.focus({ preventScroll: true });
        } else if (cell.getAttribute('tabindex') !== '0') {
            cell.setAttribute('tabindex', '0');
        }
        // Every other cell can take the focus — from a click, a screen reader's cursor or
        // the app — and none is a tab stop.
        for (var r = 0; r < rows.length; r++) {
            var cells = rows[r].cells;
            for (var i = 0; i < cells.length; i++) {
                if (cells[i] !== cell && cells[i].getAttribute('tabindex') !== '-1') cells[i].setAttribute('tabindex', '-1');
            }
        }

        var editRow = rowEditing(cell) ? cell.parentElement : null;
        if (editRow && editRow !== s.rowEdit) {
            s.rowEdit = editRow;
            if (holder === grid && (lostFocus() || editRow.contains(document.activeElement))) {
                var first = editRow.querySelector('.cell-editor');
                if (first && first !== document.activeElement) first.focus();
            }
        } else if (!editRow) {
            // The editor that had the focus is hidden or gone with the mode.
            var a = document.activeElement;
            if (s.rowEdit && holder === grid && (lostFocus() || (s.rowEdit.contains(a) && a.classList.contains('cell-editor'))))
                cell.focus({ preventScroll: true });
            s.rowEdit = null;
        }
    }

    /* The first tab stop: a cell the app marked `tabindex="0"`, else the first cell of
       the first body row, else of the first row. */
    function firstStop(grid, rows) {
        var marked = grid.querySelectorAll('td[tabindex="0"], th[tabindex="0"]');
        for (var i = 0; i < marked.length; i++) if (gridOf(marked[i]) === grid) return marked[i];
        for (var r = 0; r < rows.length; r++) if (rows[r].parentElement === grid.tBodies[0]) return rows[r].cells[0];
        return rows[0].cells[0];
    }

    /* ── Range selection ───────────────────────────────────────────────────── */

    function multi(grid) { return grid.getAttribute('aria-multiselectable') === 'true'; }

    function pick(grid, from, to) {
        var s = stateOf(grid);
        var rows = rowsOf(grid);
        var picked = [];
        if (from && to) {
            var r0 = rows.indexOf(from.parentElement), r1 = rows.indexOf(to.parentElement);
            var c0 = colOf(from), c1 = colOf(to);
            var top = Math.min(r0, r1), bottom = Math.max(r0, r1);
            var left = Math.min(c0, c1), right = Math.max(c0 + (from.colSpan || 1), c1 + (to.colSpan || 1)) - 1;
            for (var r = top; r <= bottom; r++) {
                var cells = rows[r].cells;
                for (var i = 0; i < cells.length; i++) {
                    var start = colOf(cells[i]), end = start + (cells[i].colSpan || 1) - 1;
                    if (end >= left && start <= right) picked.push(cells[i]);
                }
            }
        }
        for (var k = 0; k < s.picked.length; k++) {
            if (picked.indexOf(s.picked[k]) < 0 && s.picked[k].isConnected) s.picked[k].setAttribute('aria-selected', 'false');
        }
        for (var j = 0; j < picked.length; j++) {
            if (picked[j].getAttribute('aria-selected') !== 'true') picked[j].setAttribute('aria-selected', 'true');
        }
        s.picked = picked;
    }

    function clearPick(grid) {
        var s = stateOf(grid);
        s.anchor = null;
        if (s.picked.length) pick(grid, null, null);
    }

    /* ── Moving ─────────────────────────────────────────────────────────────── */

    function pageRows(grid, cell) {
        var y = scroller(cell, 'y');
        var height = y ? y.clientHeight : window.innerHeight;
        var row = cell.parentElement.getBoundingClientRect().height || 1;
        var head = grid.tHead && y && getComputedStyle(grid.tHead).position === 'sticky' ? grid.tHead.getBoundingClientRect().height : 0;
        var foot = grid.tFoot && y && getComputedStyle(grid.tFoot).position === 'sticky' ? grid.tFoot.getBoundingClientRect().height : 0;
        return Math.max(1, Math.floor((height - head - foot) / row) - 1);
    }

    /* The cell a navigation key leads to from `cell`, or null for a key that is no move. */
    function target(e, grid, cell) {
        var s = stateOf(grid);
        var rows = rowsOf(grid);
        var row = cell.parentElement;
        var r = rows.indexOf(row);
        if (r < 0) return null;
        var cells = row.cells;
        var i = cell.cellIndex;
        var back = rtl(grid);
        var right = back ? -1 : 1;
        var primary = e.ctrlKey || e.metaKey;
        switch (e.key) {
            case 'ArrowRight': return { cell: cells[Math.max(0, Math.min(cells.length - 1, i + right))] };
            case 'ArrowLeft': return { cell: cells[Math.max(0, Math.min(cells.length - 1, i - right))] };
            case 'ArrowDown': return { cell: cellAt(rows[Math.min(rows.length - 1, r + 1)], s.want), keepWant: true };
            case 'ArrowUp': return { cell: cellAt(rows[Math.max(0, r - 1)], s.want), keepWant: true };
            case 'Home': return primary ? { cell: rows[0].cells[0] } : { cell: cells[0] };
            case 'End':
                if (primary) { var last = rows[rows.length - 1]; return { cell: last.cells[last.cells.length - 1] }; }
                return { cell: cells[cells.length - 1] };
            case 'PageDown': return { cell: cellAt(rows[Math.min(rows.length - 1, r + pageRows(grid, cell))], s.want), keepWant: true };
            case 'PageUp': return { cell: cellAt(rows[Math.max(0, r - pageRows(grid, cell))], s.want), keepWant: true };
        }
        return null;
    }

    function move(e, grid, cell) {
        var to = target(e, grid, cell);
        if (!to || !to.cell) return false;
        e.preventDefault();
        var s = stateOf(grid);
        if (e.shiftKey && multi(grid)) {
            if (!s.anchor || !s.anchor.isConnected) s.anchor = cell;
            go(grid, to.cell, to.keepWant);
            pick(grid, s.anchor, to.cell);
        } else {
            clearPick(grid);
            go(grid, to.cell, to.keepWant);
        }
        return true;
    }

    /* ── Editing ────────────────────────────────────────────────────────────── */

    function begin(grid, cell, caretAtEnd) {
        var editor = editorOf(cell);
        if (!editor) return false;
        activate(grid, cell);
        editing.set(cell, { value: 'value' in editor ? editor.value : null, ending: false });
        cell.setAttribute('data-editing', '');
        editor.focus();
        try {
            if (caretAtEnd && editor.setSelectionRange) editor.setSelectionRange(editor.value.length, editor.value.length);
            else if (editor.select) editor.select();
        } catch (err) { /* a number or a date field has no text selection */ }
        return true;
    }

    /* Back on the cell. The editor's blur is what commits; a cancel puts its value back
       first, so the blur has nothing to send. */
    function end(grid, cell, commit) {
        var edit = editing.get(cell);
        var editor = editorOf(cell);
        if (edit) edit.ending = true;
        if (!commit && edit && editor && edit.value !== null && editor.value !== edit.value) {
            editor.value = edit.value;
            editor.dispatchEvent(new Event('input', { bubbles: true }));
        }
        if (grid && cell.isConnected) {
            activate(grid, cell, true);
            cell.focus();
        }
        cell.removeAttribute('data-editing');
        editing.delete(cell);
    }

    /* The next or previous cell in reading order, across rows. */
    function step(grid, cell, back) {
        var rows = rowsOf(grid);
        var row = cell.parentElement;
        var r = rows.indexOf(row);
        var i = cell.cellIndex + (back ? -1 : 1);
        if (i >= 0 && i < row.cells.length) return row.cells[i];
        var next = rows[r + (back ? -1 : 1)];
        if (!next) return null;
        return back ? next.cells[next.cells.length - 1] : next.cells[0];
    }

    function inEditor(e, grid, cell) {
        if (e.key === 'Escape') {
            e.preventDefault();
            end(grid, cell, false);
            return;
        }
        if (e.key === 'Enter' && !e.isComposing && (e.target.tagName !== 'TEXTAREA' || e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            end(grid, cell, true);
            return;
        }
        if (e.key === 'Tab' && !e.altKey && !e.ctrlKey && !e.metaKey) {
            var next = step(grid, cell, e.shiftKey);
            if (!next) return;
            e.preventDefault();
            end(grid, cell, true);
            if (!begin(grid, next, false)) go(grid, next);
        }
    }

    function inEditingRow(e, cell) {
        var row = cell.parentElement;
        var button = null;
        if (e.key === 'Escape') button = row.querySelector('[data-row-cancel]');
        else if (e.key === 'Enter' && !e.isComposing && e.target.classList.contains('cell-editor') && e.target.tagName !== 'TEXTAREA') button = row.querySelector('[data-row-save]');
        if (!button || button.disabled) return;
        e.preventDefault();
        button.click();
    }

    /* ── Tree grid ──────────────────────────────────────────────────────────── */

    var loading = [];           // rows opened with data-tree-lazy, waiting for their children

    function isTree(grid) { return grid.getAttribute('role') === 'treegrid'; }

    function levelOf(row) {
        var n = parseInt(row.getAttribute('aria-level'), 10);
        return n > 0 ? n : 1;
    }

    /* Every body row, shown or not, in order. */
    function bodyRows(grid) {
        var out = [];
        for (var b = 0; b < grid.tBodies.length; b++) {
            var rows = grid.tBodies[b].rows;
            for (var i = 0; i < rows.length; i++) if (rows[i].cells.length) out.push(rows[i]);
        }
        return out;
    }

    /* The cell that holds the row's `.tree-toggle`, else its first. */
    function treeCell(row) {
        var toggle = row.querySelector('.tree-toggle');
        var cell = toggle ? toggle.closest('td, th') : null;
        return cell && cell.parentElement === row ? cell : row.cells[0] || null;
    }

    function expandable(row) { return row.hasAttribute('aria-expanded'); }
    function expanded(row) { return row.getAttribute('aria-expanded') === 'true'; }

    function valueOf(row) {
        if (row.hasAttribute('data-value')) return row.getAttribute('data-value');
        var cell = treeCell(row);
        return cell ? cell.textContent.trim() : '';
    }

    /* The rows under `row`: those after it deeper than it, up to the next that is not. */
    function under(grid, row) {
        var rows = bodyRows(grid), at = rows.indexOf(row), level = levelOf(row), out = [];
        for (var i = at + 1; at >= 0 && i < rows.length && levelOf(rows[i]) > level; i++) out.push(rows[i]);
        return out;
    }

    function parentRow(grid, row) {
        var rows = bodyRows(grid), level = levelOf(row);
        for (var i = rows.indexOf(row) - 1; i >= 0; i--) if (levelOf(rows[i]) < level) return rows[i];
        return null;
    }

    /* A row is shown while every branch above it is open. The script owns `hidden` on a
       tree grid's body rows: it writes it on the rows under a closed branch and takes it
       off the rest, so markup that renders them hidden for the first paint and markup
       that does not end the same. */
    function showBranches(grid) {
        var open = [];      // the branches above the row: { level, open }
        var rows = bodyRows(grid);
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i], level = levelOf(row);
            while (open.length && open[open.length - 1].level >= level) open.pop();
            var shown = open.every(function (o) { return o.open; });
            if (row.hidden === shown) row.hidden = !shown;
            open.push({ level: level, open: expanded(row) });
        }
    }

    /* A branch's children have landed, or the app says there are none. */
    function settleLoading() {
        loading = loading.filter(function (item) {
            var row = item.row;
            if (!row.isConnected) return false;
            if (row.hasAttribute('data-tree-lazy') && !under(item.grid, row).length) return true;
            row.removeAttribute('aria-busy');
            return false;
        });
    }

    function setOpen(grid, row, open) {
        if (!expandable(row) || expanded(row) === open) return false;
        // Closing the branch the focus is in hands the focus to the branch.
        if (!open && under(grid, row).some(function (r) { return r.contains(document.activeElement); })) go(grid, treeCell(row));
        row.setAttribute('aria-expanded', String(open));
        var lazy = open && row.hasAttribute('data-tree-lazy');
        if (lazy && row.getAttribute('aria-busy') !== 'true') {
            row.setAttribute('aria-busy', 'true');
            loading.push({ grid: grid, row: row });
        }
        showBranches(grid);
        row.dispatchEvent(new CustomEvent(open ? 'sedna-tree-expand' : 'sedna-tree-collapse', {
            bubbles: true, composed: true, detail: { item: valueOf(row), lazy: lazy }
        }));
        return true;
    }

    /* → and ← on a row's tree cell, mirrored in RTL: forward opens a closed branch, back
       closes an open one or goes to the row's parent. Every other move is the grid's. */
    core.keys.push(function (e, grid, cell) {
        if (!isTree(grid) || e.target !== cell || e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return false;
        var row = cell.parentElement;
        if (!row.parentElement || row.parentElement.tagName !== 'TBODY' || treeCell(row) !== cell) return false;
        var forward = rtl(grid) ? 'ArrowLeft' : 'ArrowRight';
        var back = rtl(grid) ? 'ArrowRight' : 'ArrowLeft';
        if (e.key === forward) {
            if (!expandable(row) || expanded(row)) return false;
            e.preventDefault();
            setOpen(grid, row, true);
            return true;
        }
        if (e.key === back) {
            if (expanded(row)) { e.preventDefault(); setOpen(grid, row, false); return true; }
            var parent = parentRow(grid, row);
            if (!parent) return false;
            e.preventDefault();
            clearPick(grid);
            go(grid, treeCell(parent));
            return true;
        }
        // Enter on a branch whose cell has nothing to edit or press opens or closes it.
        if (e.key === 'Enter' && expandable(row) && !editorOf(cell) && !controlsIn(cell).length) {
            e.preventDefault();
            setOpen(grid, row, !expanded(row));
            return true;
        }
        return false;
    });

    document.addEventListener('click', function (e) {
        var toggle = e.target instanceof Element ? e.target.closest('.tree-toggle') : null;
        var row = toggle && toggle.closest('tr');
        var grid = row && gridOf(row.cells[0] || row);
        if (!grid || !isTree(grid) || !expandable(row) || treeCell(row) !== toggle.closest('td, th')) return;
        setOpen(grid, row, !expanded(row));
    });

    /* ── Events ─────────────────────────────────────────────────────────────── */

    document.addEventListener('keydown', function (e) {
        if (e.defaultPrevented || !(e.target instanceof Element)) return;
        var cell = cellOf(e.target);
        if (!cell) return;
        var grid = gridOf(cell);
        // An open menu, popover or list inside the cell has the keys.
        var layer = e.target.closest('[popover], [role="menu"], [role="listbox"], dialog');
        if (layer && cell.contains(layer)) return;

        for (var k = 0; k < core.keys.length; k++) {
            if (core.keys[k](e, grid, cell)) return;
        }

        if (e.target !== cell) {
            var editor = editorOf(cell);
            if (cell.hasAttribute('data-editing') && editor && editor.contains(e.target)) { inEditor(e, grid, cell); return; }
            if (rowEditing(cell)) { inEditingRow(e, cell); return; }
            if (e.key === 'Escape') { e.preventDefault(); go(grid, cell); return; }
            if (keepsKeys(e.target)) return;
            if (e.key === 'Enter' || e.key === ' ' || e.key === 'F2') return;
            move(e, grid, cell);
            return;
        }

        if (e.altKey) return;
        if (e.key === 'Enter' || e.key === 'F2') {
            if (begin(grid, cell, e.key === 'F2')) { e.preventDefault(); return; }
            var controls = controlsIn(cell);
            if (controls.length) { e.preventDefault(); controls[0].focus(); }
            return;
        }
        if (e.key === 'Escape') {
            if (stateOf(grid).picked.length) { e.preventDefault(); clearPick(grid); }
            return;
        }
        move(e, grid, cell);
    });

    document.addEventListener('focusin', function (e) {
        var cell = cellOf(e.target);
        if (!cell) { holder = null; return; }
        var grid = gridOf(cell);
        holder = grid;
        if (stateOf(grid).cell !== cell) activate(grid, cell);
    });

    /* An edit ends when focus leaves the cell — decided once it has landed, so a press
       on another cell, a click outside and a Tab all commit the same way. A window that
       lost focus to another keeps its edit. */
    document.addEventListener('focusout', function (e) {
        var cell = cellOf(e.target);
        if (!cell || !editing.has(cell)) return;
        Promise.resolve().then(function () {
            var edit = editing.get(cell);
            if (edit && !edit.ending && !cell.contains(document.activeElement) && document.hasFocus()) end(null, cell, true);
        });
    });

    document.addEventListener('mousedown', function (e) {
        if (!(e.target instanceof Element)) return;
        var cell = cellOf(e.target);
        if (!cell) { holder = null; return; }
        var grid = gridOf(cell);
        holder = grid;
        if (e.button !== 0) return;
        var s = stateOf(grid);
        if (e.shiftKey && multi(grid) && s.cell && s.cell !== cell) {
            e.preventDefault();
            if (!s.anchor || !s.anchor.isConnected) s.anchor = s.cell;
            go(grid, cell);
            pick(grid, s.anchor, cell);
            return;
        }
        var control = e.target.closest(CONTROLS);
        if (control && control !== cell && cell.contains(control)) return;
        clearPick(grid);
        // Focusable before the press lands, so the press focuses it.
        activate(grid, cell);
    });

    document.addEventListener('dblclick', function (e) {
        var cell = e.target instanceof Element ? cellOf(e.target) : null;
        if (!cell || cell.hasAttribute('data-editing') || rowEditing(cell)) return;
        var grid = gridOf(cell);
        begin(grid, cell, true);
    });

    ui.grid = {
        /* The selected rectangle of a grid, as row numbers in `table.rows` and column
           numbers counting each cell's span — or null when nothing is selected. */
        range: function (grid) {
            grid = typeof grid === 'string' ? document.getElementById(grid) : grid;
            var s = grid && state.get(grid);
            if (!s || !s.picked.length) return null;
            var top = Infinity, left = Infinity, bottom = -1, right = -1;
            for (var i = 0; i < s.picked.length; i++) {
                var c = s.picked[i];
                if (!c.isConnected) continue;
                var r = c.parentElement.rowIndex, col = colOf(c);
                top = Math.min(top, r); bottom = Math.max(bottom, r);
                left = Math.min(left, col); right = Math.max(right, col + (c.colSpan || 1) - 1);
            }
            return bottom < 0 ? null : { top: top, left: left, bottom: bottom, right: right };
        },
        /* The rows of a tree grid that are open, by `data-value` — else the text of their
           tree cell — in document order. */
        expanded: function (grid) {
            grid = typeof grid === 'string' ? document.getElementById(grid) : grid;
            if (!grid) return [];
            return bodyRows(grid).filter(function (r) { return expandable(r) && expanded(r); }).map(valueOf);
        },
        /* Puts the tab stop, and the focus when the grid has it, back on a cell after a
           change this part did not see. Runs by itself after every render. */
        refresh: function (grid) {
            refresh(typeof grid === 'string' ? document.getElementById(grid) : grid || null);
        }
    };

    /* Rows a render adds, removes or replaces, a row's `data-editing`, and a tree grid
       row's `aria-expanded` or `data-tree-lazy`, all arrive as mutations; the work waits
       for the frame, so a render of many rows is one pass. Only a page with a grid does
       any. */
    var scheduled = false;
    function later() {
        if (scheduled) return;
        scheduled = true;
        requestAnimationFrame(function () { scheduled = false; refresh(null); });
    }
    function watch() {
        refresh(null);
        new MutationObserver(function (records) {
            if (!document.querySelector(core.selector)) return;
            for (var i = 0; i < records.length; i++) {
                var r = records[i];
                if (r.type === 'childList' || r.target.tagName === 'TR') { later(); return; }
            }
        }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-editing', 'data-tree-lazy', 'aria-expanded'] });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();

})(window.sednaUi);

/* ── A panel grid the reader arranges ───────────────────────────────────────
   Panels placed on cells, each by its own x, y, width and height, that the reader drags
   to another place and resizes from an edge — a dashboard laid out by the person who
   reads it. The markup and the arrangement are the app's:

     <div class="panel-grid" data-drag-zone="dashboard" data-drag-axis="cells" data-arrange
          aria-label="Dashboard">
       <section class="card card--resizable" data-drag-item="orders" data-drag-label="Open orders"
                style="--panel-x: 0; --panel-y: 0; --panel-w: 8; --panel-h: 4">
         … a [data-drag-handle] in its head …
         <div class="card-resize card-resize--inline" data-resize="--panel-w" role="separator"
              aria-orientation="vertical" aria-valuemin="3" aria-valuenow="8" tabindex="0" …></div>
         <div class="card-resize" data-resize="--panel-h" role="separator"
              aria-orientation="horizontal" aria-valuemin="2" aria-valuenow="4" tabindex="0" …></div>
         <div class="card-resize card-resize--corner" data-resize="--panel-w --panel-h" aria-hidden="true"></div>
       </section>
     </div>

   The grid is a drag zone with `data-drag-axis="cells"`, so 26-drag.js picks a panel up,
   by the pointer and by the keyboard, and 26-resize.js takes its handles. This part is
   what both hand the cells to: where a panel would land, what that does to the rest, and
   the preview of it. `data-arrange` on the grid is the arrange mode; without it — and while
   the grid is too narrow and stacks its panels — nothing lifts and no handle resizes.

   ONE ARRANGEMENT. `sednaUi.panels.arrange(panels, columns, moved)` is the only layout
   there is: the preview while a panel is dragged or resized, the `panels` the events
   carry, and a stored arrangement an app checks are all its result. A panel in the way of
   the moved one goes down below it, and then every panel floats up until it rests on the
   top or on a panel above it, so nothing overlaps and nothing hangs over a gap. The moved
   panel wins its place against a panel at the same row, unless it moved down. The result
   is in reading order — by row, then by column — which is the order the app renders it
   in, so the tab order follows the screen. `SednaPanels.Arrange` is the same function in C#.

   NOTHING IS MOVED. The app writes `--panel-x`, `--panel-y`, `--panel-w` and `--panel-h`,
   and this never writes them. A preview is `--panel-preview-*` beside them on each panel,
   which the stylesheet reads first, and `--panel-drop-*` on the grid for the cell a carried
   panel would land on. After a drop or a resize the preview is held — so nothing jumps
   back while the app stores the arrangement — until the app renders a panel's style, adds
   or removes a panel, or three seconds pass; then it is cleared and the app's own values
   are all that is left. An app that keeps the arrangement shows it from then on; one that
   ignores the event shows what it had.

     sedna-drop     on the grid    as for any zone, plus { x, y, panels }: the cell the
                                   panel landed on and every panel's { id, x, y, w, h }
     sedna-resize   on the panel   { property, value, key, panels }: the property a handle
                                   names, its new value in cells, the panel's id
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    /* ── The arrangement ──────────────────────────────────────────────────────
       Pure: data in, new data out, nothing read from the page. */

    function whole(value, fallback) {
        var n = Math.round(Number(value));
        return isFinite(n) ? n : fallback;
    }

    /* A panel inside the grid: at least a cell, no wider than the grid, not past its end. */
    function fit(p, columns) {
        var w = Math.min(columns, Math.max(1, whole(p.w, 1)));
        return {
            id: String(p.id),
            x: Math.min(columns - w, Math.max(0, whole(p.x, 0))),
            y: Math.max(0, whole(p.y, 0)),
            w: w,
            h: Math.max(1, whole(p.h, 1))
        };
    }

    /* The first placed panel that `p` would overlap with its top at `y`. */
    function blocker(p, y, placed) {
        for (var i = 0; i < placed.length; i++) {
            var q = placed[i];
            if (p.x < q.x + q.w && q.x < p.x + p.w && y < q.y + q.h && q.y < y + p.h) return q;
        }
        return null;
    }

    function arrange(panels, columns, moved) {
        var cols = Math.max(1, whole(columns, 1));
        var list = [], seen = {};
        (panels || []).forEach(function (p) {
            if (!p || seen[p.id]) return;
            seen[p.id] = true;
            list.push(fit(p, cols));
        });

        var mover = null, down = false;
        if (moved) {
            var at = -1;
            for (var i = 0; i < list.length; i++) if (list[i].id === String(moved.id)) at = i;
            var was = at === -1 ? null : list[at];
            mover = fit({
                id: moved.id,
                x: moved.x === undefined ? (was ? was.x : 0) : moved.x,
                y: moved.y === undefined ? (was ? was.y : 0) : moved.y,
                w: moved.w === undefined ? (was ? was.w : 1) : moved.w,
                h: moved.h === undefined ? (was ? was.h : 1) : moved.h
            }, cols);
            down = !!was && mover.y > was.y;
            if (at === -1) list.push(mover); else list[at] = mover;
        }

        // The order panels settle in: by row, then column. The moved one goes before a
        // panel on its row — it takes that place — unless it was moving down, when it
        // goes after, or a panel could never be put below its neighbour.
        var order = list.slice().sort(function (a, b) {
            if (a.y !== b.y) return a.y - b.y;
            if (a === mover || b === mover) return (a === mover) === down ? 1 : -1;
            return a.x - b.x;
        });

        var placed = [];
        order.forEach(function (p) {
            var y = p.y, hit;
            // Down below anything already in the way…
            while ((hit = blocker(p, y, placed))) y = hit.y + hit.h;
            // …then up until it rests on the top or on a panel.
            while (y > 0 && !blocker(p, y - 1, placed)) y--;
            p.y = y;
            placed.push(p);
        });

        return placed.slice().sort(function (a, b) { return a.y - b.y || a.x - b.x; });
    }

    function find(panels, id) {
        for (var i = 0; i < panels.length; i++) if (panels[i].id === id) return panels[i];
        return null;
    }

    function same(a, b) {
        if (a.length !== b.length) return false;
        for (var i = 0; i < a.length; i++) {
            var q = find(b, a[i].id);
            if (!q || q.x !== a[i].x || q.y !== a[i].y || q.w !== a[i].w || q.h !== a[i].h) return false;
        }
        return true;
    }

    /* ── The grid on the page ─────────────────────────────────────────────── */

    var AXES = ['x', 'y', 'w', 'h'];
    var HOLD = 3000;          // ms a preview waits for the app to render the arrangement
    var held = [];            // grids whose preview waits for the app

    function isGrid(el) {
        return !!el && el.nodeType === 1 && el.hasAttribute('data-drag-zone')
            && el.getAttribute('data-drag-axis') === 'cells';
    }

    function panelsOf(grid) {
        return Array.prototype.filter.call(grid.children, function (el) {
            return el.hasAttribute('data-drag-item');
        });
    }

    /* Too narrow, the stylesheet stacks the panels in reading order, and says so. */
    function stacked(grid) {
        var first = panelsOf(grid)[0];
        return !!first && getComputedStyle(first).getPropertyValue('--panel-stacked').trim() === '1';
    }

    /* Panels are ordinary content: not in arrange mode, or stacked. */
    function resting(grid) { return !grid.hasAttribute('data-arrange') || stacked(grid); }

    /* The app's arrangement, from the properties it wrote. */
    function read(grid) {
        return panelsOf(grid).map(function (el) {
            var cs = getComputedStyle(el), p = { id: el.getAttribute('data-drag-item') };
            AXES.forEach(function (a) { p[a] = whole(parseFloat(cs.getPropertyValue('--panel-' + a)), a === 'w' || a === 'h' ? 1 : 0); });
            return p;
        });
    }

    function px(value) { var n = parseFloat(value); return isFinite(n) ? n : 0; }

    /* The cells as they are drawn: the column count, a column's and a row's pitch, and
       where the first cell starts — from the inline-end edge in a right-to-left grid. */
    function geometry(grid) {
        var cs = getComputedStyle(grid), r = grid.getBoundingClientRect();
        var cols = Math.max(1, whole(parseFloat(cs.getPropertyValue('--panel-cols')), 12));
        var gapX = px(cs.columnGap), gapY = px(cs.rowGap);
        var left = r.left + px(cs.borderLeftWidth) + px(cs.paddingLeft);
        var right = r.right - px(cs.borderRightWidth) - px(cs.paddingRight);
        var top = r.top + px(cs.borderTopWidth) + px(cs.paddingTop);
        var col = (right - left - gapX * (cols - 1)) / cols;
        var row = px(cs.gridAutoRows) || 48;
        return {
            cols: cols, col: col, row: row, gapX: gapX, gapY: gapY,
            pitchX: col + gapX, pitchY: row + gapY,
            left: left, right: right, top: top, rtl: cs.direction === 'rtl'
        };
    }

    /* Writes a result as the preview: every panel's place, and the cell `mark` names on
       the grid for the panel being carried. */
    function show(grid, result, mark) {
        panelsOf(grid).forEach(function (el) {
            var p = find(result, el.getAttribute('data-drag-item'));
            if (!p) return;
            AXES.forEach(function (a) { el.style.setProperty('--panel-preview-' + a, String(p[a])); });
        });
        if (mark) AXES.forEach(function (a) { grid.style.setProperty('--panel-drop-' + a, String(mark[a])); });
    }

    /* Clears a session's preview, leaving the markup as the app wrote it. */
    function clear(s) {
        var i = held.indexOf(s);
        if (i !== -1) {
            held.splice(i, 1);
            clearTimeout(s.timer);
            if (s.observer) s.observer.disconnect();
        }
        panelsOf(s.grid).forEach(function (el) {
            AXES.forEach(function (a) { el.style.removeProperty('--panel-preview-' + a); });
        });
        AXES.forEach(function (a) { s.grid.style.removeProperty('--panel-drop-' + a); });
        if (!s.hadStyle && !s.grid.getAttribute('style')) s.grid.removeAttribute('style');
    }

    /* Keeps the preview until the app renders: a panel's style written, or a panel added or
       removed. Started before the events go out, so a handler that renders at once is seen. */
    function hold(s) {
        var grid = s.grid;
        try {
            s.observer = new MutationObserver(function (records) {
                for (var i = 0; i < records.length; i++) {
                    var r = records[i];
                    if (r.type === 'childList' ? r.target === grid : r.target.parentElement === grid) {
                        clear(s);
                        return;
                    }
                }
            });
            s.observer.observe(grid, { childList: true, subtree: true, attributes: true, attributeFilter: ['style'] });
        } catch (e) { clear(s); return; }
        s.timer = setTimeout(function () { clear(s); }, HOLD);
        held.push(s);
    }

    /* Starts a drag or a resize on `grid`, from the arrangement it shows: the one still held
       for the app when there is one — a second key pressed before the app has rendered the
       first builds on it — else the app's own. */
    function begin(grid) {
        var before = null;
        held.slice().forEach(function (h) {
            if (h.grid !== grid) return;
            before = h.result;
            clear(h);
        });
        var g = geometry(grid);
        var s = {
            grid: grid, geo: g, cols: g.cols, hadStyle: grid.hasAttribute('style'),
            start: arrange(before || read(grid), g.cols), result: null, observer: null, timer: 0
        };
        if (before) show(grid, s.start, null);
        return s;
    }

    /* Whether the arrangement previewed differs from the one the session started from. */
    function changed(s) { return !!s.result && !same(s.result, s.start); }

    /* Ends a session: the preview is held for the app when it is kept, and cleared when it
       is not. Returns the arrangement to report, or null. */
    function end(s, keep) {
        keep = keep && changed(s);
        if (keep) hold(s); else clear(s);
        return keep ? copy(s.result) : null;
    }

    function copy(panels) {
        return panels.map(function (p) { return { id: p.id, x: p.x, y: p.y, w: p.w, h: p.h }; });
    }

    /* ── Dragging: what 26-drag.js asks ──────────────────────────────────── */

    /* Moves `id` to `cell` — { x, y } — and previews what that does. */
    function moveTo(s, id, cell) {
        var was = find(s.start, id);
        s.result = arrange(s.start, s.cols, { id: id, x: cell.x, y: cell.y, w: was.w, h: was.h });
        s.cell = cell;
        var now = find(s.result, id);
        show(s.grid, s.result, now);
        return now;
    }

    /* The cell panel `id` would land on with its box at viewport `left`, `top` and `right`:
       the cell nearest its starting corner — the top right one in a right-to-left grid —
       kept on the grid. Measured afresh, so a grid that scrolled under the box still fits. */
    function cellAt(s, id, box) {
        var g = s.geo = geometry(s.grid);
        var dx = g.rtl ? g.right - box.right : box.left - g.left;
        var w = find(s.start, id).w;
        return {
            x: Math.max(0, Math.min(s.cols - w, Math.round(dx / g.pitchX))),
            y: Math.max(0, Math.round((box.top - g.top) / g.pitchY))
        };
    }

    /* An arrow from the keyboard: across by one column — mirrored right to left — or up and
       down to the next row where the panel would land somewhere else, so a press always
       does something when anything can. Null when nothing can. */
    function stepFrom(s, id, key) {
        var now = find(s.result || s.start, id);
        var across = { ArrowLeft: -1, ArrowRight: 1 }[key];
        if (across) {
            if (s.geo.rtl) across = -across;
            var x = Math.max(0, Math.min(s.cols - now.w, now.x + across));
            return x === now.x ? null : { x: x, y: now.y };
        }
        var by = { ArrowUp: -1, ArrowDown: 1 }[key];
        if (!by) return null;
        var bottom = 0;
        s.start.forEach(function (p) { bottom = Math.max(bottom, p.y + p.h); });
        var was = find(s.start, id);
        for (var y = now.y + by; y >= 0 && y <= bottom; y += by) {
            var p = find(arrange(s.start, s.cols, { id: id, x: now.x, y: y, w: was.w, h: was.h }), id);
            if (p.y !== now.y) return { x: now.x, y: y };
        }
        return null;
    }

    /* ── Resizing: what 26-resize.js asks ────────────────────────────────── */

    /* The grid a resized element is a panel of, when it is arranged. */
    function gridOfPanel(el) {
        var grid = el && el.parentElement;
        return isGrid(grid) && el.hasAttribute('data-drag-item') ? grid : null;
    }

    /* "16:9", "16/9" or "1.78" as width over height, or null. */
    function ratioOf(el) {
        var raw = el.getAttribute('data-panel-ratio');
        if (!raw) return null;
        var parts = raw.split(/[:/]/);
        var r = parts.length === 2 ? parseFloat(parts[0]) / parseFloat(parts[1]) : parseFloat(raw);
        return isFinite(r) && r > 0 ? r : null;
    }

    /* A panel's size in cells for the size wanted: bounded by `limits` — { w: [min, max],
       h: [min, max] } — and, for a panel with `data-panel-ratio`, the height its width
       gives, to the nearest row. `lead` says which way the ratio is read: from the width
       ('w') or from the height ('h'). */
    function sized(s, panel, w, h, limits, lead) {
        var g = s.geo, ratio = ratioOf(panel);
        function clampW(v) { return Math.max(limits.w[0], Math.min(limits.w[1], v)); }
        function clampH(v) { return Math.max(limits.h[0], Math.min(limits.h[1], v)); }
        w = clampW(w);
        h = clampH(h);
        if (!ratio) return { w: w, h: h };
        function hFor(cw) { return Math.max(1, Math.round((cw * g.col + (cw - 1) * g.gapX) / ratio / g.pitchY + g.gapY / g.pitchY)); }
        function wFor(ch) { return Math.max(1, Math.round((ch * g.row + (ch - 1) * g.gapY) * ratio / g.pitchX + g.gapX / g.pitchX)); }
        // From the lead axis to the other; and back, only where a bound stopped the other.
        if (lead === 'h') {
            var wide = wFor(h);
            w = clampW(wide);
            if (w !== wide) h = clampH(hFor(w));
        } else {
            var tall = hFor(w);
            h = clampH(tall);
            if (h !== tall) w = clampW(wFor(h));
        }
        return { w: w, h: h };
    }

    /* Resizes `panel` to `size` and previews it. */
    function resizeTo(s, panel, size) {
        var id = panel.getAttribute('data-drag-item');
        var was = find(s.start, id);
        s.result = arrange(s.start, s.cols, { id: id, x: was.x, y: was.y, w: size.w, h: size.h });
        show(s.grid, s.result, null);
        return find(s.result, id);
    }

    ui.panels = {
        // The arrangement `panels` — [{ id, x, y, w, h }] in cells — settles into on a grid
        // `columns` wide, with `moved` ({ id, and any of x, y, w, h }) put where it asks
        // first. New objects, in reading order.
        arrange: function (panels, columns, moved) { return copy(arrange(panels, columns, moved)); }
    };

    ui._.panels = {
        isGrid: isGrid, resting: resting, gridOfPanel: gridOfPanel,
        begin: begin, changed: changed, end: end, find: find,
        moveTo: moveTo, cellAt: cellAt, stepFrom: stepFrom,
        sized: sized, resizeTo: resizeTo
    };

})(window.sednaUi);

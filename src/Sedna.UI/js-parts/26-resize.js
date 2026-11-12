/* ── Resizing by a handle ────────────────────────────────────────────────────
   A card the reader makes taller, an aside the reader makes wider: the app writes the
   handle, and the script moves one custom property the CSS already reads.

     <div class="card card--resizable" id="w1">
       …
       <div class="card-resize" data-resize="--card-height" role="separator"
            aria-orientation="horizontal" aria-label="Resize Open orders" tabindex="0"></div>
     </div>

   `data-resize` names the property, written on the handle's parent — or on the element
   `data-resize-on` names by id. The size measured is that element's own, or that of the
   child `data-resize-measure` selects within it (a split's aside). `aria-orientation`
   says which way it moves: horizontal resizes the block size, vertical the inline size.
   A drag snaps to `data-resize-step` pixels (8 unless set); `aria-valuemin` and
   `aria-valuemax` bound it. The arrow keys move the handle a step, Shift four steps,
   and Home or a double-click puts the size back to what the CSS gives it.

   The app writes the handle's starting `aria-valuenow` — a separator that takes the focus
   needs one — and the script keeps it current. Nothing else is written: `aria-valuenow`
   on the handle, and the property on the element. A bubbling `sedna-resize` on that element, with `property` and `value` in
   pixels (null once reset), says where it ended, so an app can keep it.

   A TABLE COLUMN. A `.col-resize` grip in a header cell of a `.table--fixed` is a handle
   on `--col-width`, written on that cell. `data-resize-key` names the column, and
   `sedna-resize` carries it as `key`, so an app keeps a width per column. Without
   `aria-valuemin` and `aria-valuemax`, the bounds are the handle's `--resize-min` and
   `--resize-max` — lengths, which a grip takes from its column's `--col-min` and
   `--col-max`. In a keyboard grid the grip is one of the cell's controls and keeps the
   arrows along its axis and Home; the rest of the keys are the grid's. On a cell that
   holds a grip, Alt+Shift with an arrow moves it a step, and Alt+Shift+Home resets it,
   without leaving the cell.

   FOLDING. `data-resize-fold="<px>"` lets the reader put a thing away by dragging it
   small: a drag that ends with the pointer below that size, or an arrow key towards
   smaller when the size is already at its minimum, folds instead of resizing. The size
   goes back to what it was before, so unfolding returns to the reader's height, and
   `sedna-resize` says `folded: true` with that size as `value`; the app folds — a card
   takes `.card--folded` — the way a drop moves nothing and the app moves the item. While
   a drag is below the threshold the handle carries `data-resize-folding`, for the app to
   show what letting go will do.

   A CORNER. `data-resize` naming two properties — the inline size, then the block size,
   `data-resize="--card-width --card-height"` — is a handle for both at once: the corner
   between a card's side and bottom handles. It is for the pointer; the keyboard has the
   two edges, so a corner is `aria-hidden="true"` and takes no focus. Each axis takes its
   bounds and step from the handle on the same element that writes that property alone,
   and keeps that handle's `aria-valuenow` current; with none, the corner's own. It sends
   one `sedna-resize` for each property it changed, and folds nothing.

   A PANEL. A handle on a panel of a panel grid — `--panel-w`, `--panel-h`, or the two —
   counts in cells, and 26-panels.js arranges the grid around the new size: the property
   is the app's and is not written, the preview is, and `sedna-resize` carries `value` in
   cells, `key` as the panel's id and `panels`, the whole arrangement. `aria-valuemin` and
   `aria-valuemax` are in cells too, and `aria-valuenow` is the app's to render with the
   arrangement, as the panel's own properties are. `data-panel-ratio="16:9"` on the panel keeps its
   shape, to the nearest row. Outside the grid's arrange mode the handles do nothing, and
   Home resets nothing: a panel has no size of its own to go back to.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function toArray(list) { return Array.prototype.slice.call(list); }

    /* The properties a handle names: one for an edge, two for a corner. */
    function properties(handle) {
        return (handle.getAttribute('data-resize') || '').trim().split(/\s+/).filter(Boolean);
    }

    function parts(handle) {
        var on = handle.getAttribute('data-resize-on');
        var target = on ? document.getElementById(on) : handle.parentElement;
        if (!target) return null;
        var sel = handle.getAttribute('data-resize-measure');
        var measured = sel ? target.querySelector(sel) : target;
        if (!measured) return null;
        var block = handle.getAttribute('aria-orientation') !== 'vertical';
        return {
            target: target, measured: measured, block: block,
            property: properties(handle)[0], key: handle.getAttribute('data-resize-key')
        };
    }

    /* The handle on the same element as `handle` that writes `property` alone: the edge
       beside a corner, whose bounds, step and value the corner shares. */
    function edgeOf(handle, target, property) {
        var found = toArray(target.querySelectorAll('[data-resize]'));
        if (target.id) {
            found = found.concat(toArray(document.querySelectorAll('[data-resize-on]')).filter(function (h) {
                return h.getAttribute('data-resize-on') === target.id;
            }));
        }
        for (var i = 0; i < found.length; i++) {
            var h = found[i], props = properties(h);
            if (h === handle || props.length !== 1 || props[0] !== property) continue;
            var q = parts(h);
            if (q && q.target === target) return h;
        }
        return null;
    }

    /* What a press on `handle` resizes, one axis each: `handle` is where the axis takes its
       bounds and step from, `aria` whether it keeps that handle's value. An edge is one
       axis; a corner is two, the inline size first. */
    function axes(handle) {
        var p = parts(handle);
        if (!p) return null;
        var props = properties(handle);
        if (props.length < 2) return [{ handle: handle, p: p, aria: true }];
        return props.slice(0, 2).map(function (property, i) {
            var edge = edgeOf(handle, p.target, property);
            var q = edge ? parts(edge) : null;
            return {
                handle: edge || handle, aria: !!edge,
                p: { target: p.target, measured: q ? q.measured : p.measured, block: i === 1, property: property, key: p.key }
            };
        });
    }

    function size(p) {
        var r = p.measured.getBoundingClientRect();
        return p.block ? r.height : r.width;
    }

    /* +1 when the handle moving down or right on screen grows the measured box: the box
       sits above or to the left of it. Measured on screen, so a right-to-left page needs
       nothing of its own here. */
    function sign(handle, p) {
        var m = p.measured.getBoundingClientRect(), h = handle.getBoundingClientRect();
        if (p.block) return (m.top + m.height / 2) <= (h.top + h.height / 2) ? 1 : -1;
        var before = (m.left + m.width / 2) <= (h.left + h.width / 2);
        return before ? 1 : -1;
    }

    /* `aria-valuemin` and `aria-valuemax`, else the handle's `--resize-min` and
       `--resize-max`: registered lengths, so the browser has already resolved a rem or a
       percentage to pixels, and `none` where nothing set one. */
    function bounds(handle) {
        var min = parseFloat(handle.getAttribute('aria-valuemin'));
        var max = parseFloat(handle.getAttribute('aria-valuemax'));
        if (isNaN(min) || isNaN(max)) {
            var cs = getComputedStyle(handle);
            if (isNaN(min)) min = parseFloat(cs.getPropertyValue('--resize-min'));
            if (isNaN(max)) max = parseFloat(cs.getPropertyValue('--resize-max'));
        }
        return { min: isNaN(min) ? 48 : min, max: isNaN(max) ? Infinity : max };
    }

    function stepOf(handle) {
        var s = parseFloat(handle.getAttribute('data-resize-step'));
        return isNaN(s) || s <= 0 ? 8 : s;
    }

    function apply(a, value) {
        var b = bounds(a.handle), step = stepOf(a.handle);
        value = Math.min(b.max, Math.max(b.min, Math.round(value / step) * step));
        a.p.target.style.setProperty(a.p.property, value + 'px');
        if (a.aria) a.handle.setAttribute('aria-valuenow', String(value));
        return value;
    }

    function done(p, value, folded, panels) {
        var event = new CustomEvent('sedna-resize', {
            bubbles: true,
            detail: { property: p.property, value: value, folded: !!folded, key: p.key }
        });
        // A panel grid's resize carries the arrangement as well.
        if (panels) event.detail.panels = panels;
        p.target.dispatchEvent(event);
    }

    function foldAt(handle) {
        var f = parseFloat(handle.getAttribute('data-resize-fold'));
        return isNaN(f) ? null : f;
    }

    /* Puts the property and the handle's value back to what they were before, and says it
       folded. `value` is what the property held, in pixels, or null when it held nothing. */
    function restore(handle, p, before, now) {
        if (before) p.target.style.setProperty(p.property, before);
        else p.target.style.removeProperty(p.property);
        if (now === null) handle.removeAttribute('aria-valuenow');
        else handle.setAttribute('aria-valuenow', now);
    }

    function fold(handle, p, before, now) {
        restore(handle, p, before, now);
        var value = parseFloat(before);
        done(p, isNaN(value) ? null : value, true);
    }

    /* The panel grid `target` is an arranged panel of, or null. */
    function panelGrid(target) { return ui._.panels ? ui._.panels.gridOfPanel(target) : null; }

    function reset(handle) {
        var p = parts(handle);
        if (!p || panelGrid(p.target)) return;
        properties(handle).forEach(function (property, i) {
            p.target.style.removeProperty(property);
            var edge = i === 0 && properties(handle).length === 1 ? handle : edgeOf(handle, p.target, property);
            if (edge) edge.removeAttribute('aria-valuenow');
            done({ target: p.target, property: property, key: p.key }, null);
        });
    }

    ui.resize = { reset: reset };

    /* ── A size in pixels ─────────────────────────────────────────────────── */

    function sizeSession(handle, list, x0, y0) {
        var single = list.length === 1, a0 = list[0];
        var threshold = single ? foldAt(handle) : null, folding = false;
        var before = a0.p.target.style.getPropertyValue(a0.p.property), now = handle.getAttribute('aria-valuenow');
        list.forEach(function (a) {
            a.start = a.value = size(a.p);
            a.dir = sign(handle, a.p);
        });
        return {
            move: function (ev) {
                list.forEach(function (a) {
                    var wanted = a.start + ((a.p.block ? ev.clientY - y0 : ev.clientX - x0) * a.dir);
                    if (single) {
                        folding = threshold !== null && wanted < threshold;
                        if (folding) handle.setAttribute('data-resize-folding', '');
                        else handle.removeAttribute('data-resize-folding');
                    }
                    a.value = apply(a, wanted);
                });
            },
            end: function (released) {
                handle.removeAttribute('data-resize-folding');
                // A cancelled drag below the threshold folds nothing: the size goes back, unsaid.
                if (folding && released) fold(handle, a0.p, before, now);
                else if (folding) restore(handle, a0.p, before, now);
                else list.forEach(function (a) { if (a.value !== a.start) done(a.p, a.value); });
            }
        };
    }

    /* ── A size in cells, on a panel grid ─────────────────────────────────── */

    function dimOf(property) { return property === '--panel-h' ? 'h' : 'w'; }

    /* Each axis's bounds, in cells, from the edge handle that writes it — `aria-valuemin`
       and `aria-valuemax` — and no wider than the grid from where the panel stands. */
    function panelLimits(handle, target, cols, was) {
        var limits = {};
        ['w', 'h'].forEach(function (dim) {
            var property = '--panel-' + dim, props = properties(handle);
            var edge = props.length === 1 && props[0] === property ? handle : edgeOf(handle, target, property);
            var min = edge ? parseFloat(edge.getAttribute('aria-valuemin')) : NaN;
            var max = edge ? parseFloat(edge.getAttribute('aria-valuemax')) : NaN;
            var widest = dim === 'w' ? cols - was.x : Infinity;
            limits[dim] = [isNaN(min) ? 1 : Math.max(1, Math.round(min)), Math.min(widest, isNaN(max) ? Infinity : Math.round(max))];
        });
        return limits;
    }

    /* Previews `target` at `want` cells — { w, h }. The edges' `aria-valuenow` is the app's
       to render with the arrangement, like the panel's own properties. */
    function panelPreview(s, target, want, limits, lead) {
        var P = ui._.panels;
        return P.resizeTo(s, target, P.sized(s, target, want.w, want.h, limits, lead));
    }

    /* Ends a panel's resize: one sedna-resize for each size that changed, carrying the
       arrangement the app stores. */
    function panelDone(s, target, was, keep) {
        var arranged = ui._.panels.end(s, keep);
        if (!arranged) return;
        var id = target.getAttribute('data-drag-item');
        var now = ui._.panels.find(arranged, id);
        ['w', 'h'].forEach(function (dim) {
            if (now[dim] === was[dim]) return;
            done({ target: target, property: '--panel-' + dim, key: id }, now[dim], false, arranged);
        });
    }

    function panelSession(handle, list, grid, x0, y0) {
        var P = ui._.panels, target = list[0].p.target, s = P.begin(grid);
        var was = P.find(s.start, target.getAttribute('data-drag-item'));
        var limits = panelLimits(handle, target, s.cols, was);
        list.forEach(function (a) {
            a.dim = dimOf(a.p.property);
            a.dir = sign(handle, { block: a.dim === 'h', measured: target });
        });
        return {
            move: function (ev) {
                var want = { w: was.w, h: was.h }, lead = list[0].dim, most = -1;
                list.forEach(function (a) {
                    var moved = (a.dim === 'h' ? ev.clientY - y0 : ev.clientX - x0) * a.dir;
                    var cells = moved / (a.dim === 'h' ? s.geo.pitchY : s.geo.pitchX);
                    want[a.dim] = was[a.dim] + Math.round(cells);
                    if (Math.abs(cells) > most) { most = Math.abs(cells); lead = a.dim; }
                });
                panelPreview(s, target, want, limits, lead);
            },
            end: function (released) { panelDone(s, target, was, released); }
        };
    }

    /* An arrow on a panel's edge: a cell, or four with Shift, and the arrangement at once. */
    function panelKey(handle, p, grid, key, steps) {
        var props = properties(handle);
        var by = arrow(handle, key);
        if (props.length !== 1 || !by) return false;
        var P = ui._.panels, s = P.begin(grid);
        var was = P.find(s.start, p.target.getAttribute('data-drag-item'));
        var dim = dimOf(props[0]);
        var want = { w: was.w, h: was.h };
        want[dim] += by * sign(handle, { block: dim === 'h', measured: p.target }) * steps;
        panelPreview(s, p.target, want, panelLimits(handle, p.target, s.cols, was), dim);
        panelDone(s, p.target, was, true);
        return true;
    }

    /* ── Input ────────────────────────────────────────────────────────────── */

    document.addEventListener('pointerdown', function (e) {
        if (e.button !== 0) return;
        var handle = e.target instanceof Element ? e.target.closest('[data-resize]') : null;
        if (!handle) return;
        var list = axes(handle);
        if (!list) return;
        var grid = panelGrid(list[0].p.target);
        // Outside the arrange mode a panel is ordinary content.
        if (grid && ui._.panels.resting(grid)) return;
        e.preventDefault();
        try { handle.setPointerCapture(e.pointerId); } catch (err) { /* already released */ }
        handle.setAttribute('data-resizing', '');
        var session = grid
            ? panelSession(handle, list, grid, e.clientX, e.clientY)
            : sizeSession(handle, list, e.clientX, e.clientY);

        function move(ev) { session.move(ev); }
        function up(ev) {
            handle.removeEventListener('pointermove', move);
            handle.removeEventListener('pointerup', up);
            handle.removeEventListener('pointercancel', up);
            handle.removeAttribute('data-resizing');
            session.end(ev.type === 'pointerup');
        }
        handle.addEventListener('pointermove', move);
        handle.addEventListener('pointerup', up);
        handle.addEventListener('pointercancel', up);
    });

    document.addEventListener('dblclick', function (e) {
        var handle = e.target instanceof Element ? e.target.closest('[data-resize]') : null;
        if (handle) reset(handle);
    });

    /* +1 or -1 for an arrow along the handle's axis — the way it moves the handle on
       screen, as a drag would — or 0 for any other key. */
    function arrow(handle, key) {
        var block = handle.getAttribute('aria-orientation') !== 'vertical';
        var forward = block ? { ArrowDown: 1, ArrowUp: -1 } : { ArrowRight: 1, ArrowLeft: -1 };
        return forward[key] || 0;
    }

    /* One key on a handle: Home resets it, an arrow along its axis moves it `steps` steps.
       False for any other key. */
    function press(handle, key, steps) {
        var p = parts(handle);
        if (!p) return false;
        var grid = panelGrid(p.target);
        if (grid) return !ui._.panels.resting(grid) && panelKey(handle, p, grid, key, steps);
        if (key === 'Home') { reset(handle); return true; }
        var by = arrow(handle, key);
        if (!by) return false;
        var a = { handle: handle, p: p, aria: true };
        var current = size(p), wanted = current + by * sign(handle, p) * stepOf(handle) * steps;
        // Already as small as it goes, and asked for less: put it away.
        if (foldAt(handle) !== null && wanted < current && current <= bounds(handle).min + 0.5) {
            fold(handle, p, p.target.style.getPropertyValue(p.property), handle.getAttribute('aria-valuenow'));
            return true;
        }
        done(p, apply(a, wanted));
        return true;
    }

    /* The grip that belongs to `el` itself — a header cell's own — not one further in. */
    function ownGrip(el) {
        for (var c = el.firstElementChild; c; c = c.nextElementSibling) {
            if (c.hasAttribute('data-resize')) return c;
        }
        return null;
    }

    document.addEventListener('keydown', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el || e.ctrlKey || e.metaKey) return;
        if (el.hasAttribute('data-resize')) {
            if (e.altKey) return;
            if (press(el, e.key, e.shiftKey ? 4 : 1)) e.preventDefault();
            return;
        }
        // Alt+Shift on the cell a grip sits in: the column resizes and the focus stays.
        if (!e.altKey || !e.shiftKey) return;
        var grip = ownGrip(el);
        if (grip && press(grip, e.key, 1)) e.preventDefault();
    });

    /* In a keyboard grid the grip is one of its cell's controls, and the grid moves on from
       a control with the arrows. Its own axis and Home are the grip's; the grid keeps the
       rest — the other axis, Escape back to the cell. */
    if (ui._.grid) {
        ui._.grid.keys.push(function (e, grid, cell) {
            var el = e.target;
            if (el === cell || !el.hasAttribute('data-resize') || e.altKey || e.ctrlKey || e.metaKey) return false;
            return e.key === 'Home' || arrow(el, e.key) !== 0;
        });
    }

})(window.sednaUi);

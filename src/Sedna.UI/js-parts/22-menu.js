/* ── Menus, delegated ────────────────────────────────────────────────────────
   Opt-in wiring for the .menu panel, so a plain HTML page (or a Razor page that
   would rather not hold the state) gets a working dropdown:

     <div class="menu-anchor">
       <button data-menu-toggle aria-expanded="false">Actions</button>
       <div class="menu" hidden>…</div>
     </div>

   The `hidden` attribute is the closed state, not a class: a panel that is
   `hidden` is out of the accessibility tree and out of the tab order, which a
   `display:none` class also achieves but an `opacity:0` one does not.

   Delegated from document, so a menu rendered by a later Blazor render works with
   nothing re-bound. The frame's own user menu does NOT use this: it holds its state
   in C#, because the frame has to work with scripting blocked.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function panelOf(toggle) {
        var anchor = toggle.closest('.menu-anchor');
        return anchor ? anchor.querySelector('.menu') : null;
    }

    function setOpen(toggle, panel, open) {
        panel.hidden = !open;
        toggle.setAttribute('aria-expanded', String(open));
        if (open) startFilter(panel);
        // A render that removes the toggle of an open menu leaves the panel pointing at
        // nothing; it closes, as a hint does when its trigger goes.
        if (open && ui._.layer) {
            ui._.layer.whenGone(toggle, function () { if (panel.isConnected) panel.hidden = true; });
        }
    }

    function closeAll(except) {
        var toggles = document.querySelectorAll('[data-menu-toggle][aria-expanded="true"]');
        for (var i = 0; i < toggles.length; i++) {
            if (toggles[i] === except) continue;
            var panel = panelOf(toggles[i]);
            if (panel) setOpen(toggles[i], panel, false);
        }
    }

    /* On a phone a dropdown is a bottom sheet (58-menu.css), and the page above it is
       the panel's own ::before: a press there is on the panel element and outside its box,
       which is a press outside the menu. */
    function outside(panel, e) {
        var r = panel.getBoundingClientRect();
        return e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom;
    }

    ui.menu = {
        // Closes every open menu. An app calls this after navigating, so a menu does
        // not survive into a back-navigation from the browser cache.
        closeAll: function () { closeAll(null); }
    };

    document.addEventListener('click', function (e) {
        var toggle = e.target.closest('[data-menu-toggle]');

        if (toggle) {
            var panel = panelOf(toggle);
            if (!panel) return;
            e.preventDefault();
            var open = toggle.getAttribute('aria-expanded') === 'true';
            // Only one menu open at a time: two panels overlapping is never wanted,
            // and the second one silently covers the first.
            closeAll(toggle);
            setOpen(toggle, panel, !open);
            return;
        }

        // A click inside a panel that is not on an item leaves it open; a click on
        // an item closes it, because the item did something.
        var item = e.target.closest('.menu-item');
        if (item && item.hasAttribute('data-menu-sub')) return;
        if (item) { closeAll(null); return; }
        var menu = e.target.closest('.menu');
        if (menu && !(e.target === menu && outside(menu, e))) return;

        closeAll(null);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var open = document.querySelector('[data-menu-toggle][aria-expanded="true"]');
        if (!open) return;
        // The Escape closed the menu, and nothing else: not a dialog or a drawer it is in.
        e.preventDefault();
        closeAll(null);
        // Focus goes back to the control that opened it. Without this, focus is left
        // on a node that has just been hidden and the next Tab starts from the top
        // of the document.
        try { open.focus(); } catch (err) { /* detached */ }
    });

    /* ── Tab leaves a menu ─────────────────────────────────────────────────
       Each item is a Tab stop of its own, and the Tab or Shift+Tab that takes the focus out
       of an open menu closes it, as the WAI-ARIA menu pattern does: a dropdown, a menu at the
       pointer and a `.menu[popover]` a popovertarget button opened. The focus goes on where
       the key sent it — nothing is held back and nothing moved. A menu held by the app — the
       user widget's — is the app's to close. */
    function outermost(el) {
        var menu = el.closest('.menu');
        for (var up = menu && menu.parentElement ? menu.parentElement.closest('.menu') : null; up;
            up = up.parentElement ? up.parentElement.closest('.menu') : null) menu = up;
        return menu;
    }

    function closeMenu(menu) {
        if (menu.hasAttribute('popover')) {
            try { if (menu.matches(':popover-open')) menu.hidePopover(); } catch (err) { /* closed */ }
            return;
        }
        var anchor = menu.closest('.menu-anchor');
        var toggle = anchor ? anchor.querySelector('[data-menu-toggle]') : null;
        if (toggle && panelOf(toggle) === menu && toggle.getAttribute('aria-expanded') === 'true') setOpen(toggle, menu, false);
    }

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Tab' || e.defaultPrevented) return;
        var menu = e.target instanceof Element ? outermost(e.target) : null;
        if (!menu) return;
        // The focus moves as the key's own default action, which has run by the next task.
        setTimeout(function () {
            if (!menu.contains(document.activeElement)) closeMenu(menu);
        }, 0);
    });

    /* ── Moving through a menu with the keyboard ───────────────────────────
       Up and Down move between the items of the open menu that has the focus, Home and
       End go to its ends; Right on a `[data-menu-sub]` item opens its submenu and enters
       it, Left in a submenu goes back to the item that opened it. Items that are
       disabled are passed over. */
    function items(menu) {
        return Array.prototype.filter.call(menu.querySelectorAll('.menu-item'), function (el) {
            return el.closest('.menu') === menu && !el.disabled && el.getAttribute('aria-disabled') !== 'true'
                && el.getClientRects().length > 0;
        });
    }

    function subOf(item) {
        var anchor = item.closest('.menu-sub-anchor');
        return anchor ? anchor.querySelector(':scope > .menu-sub') : null;
    }

    function setSub(item, open) {
        var anchor = item.closest('.menu-sub-anchor');
        if (!anchor) return;
        if (open) anchor.setAttribute('data-open', ''); else anchor.removeAttribute('data-open');
        item.setAttribute('aria-expanded', String(open));
    }

    document.addEventListener('keydown', function (e) {
        var item = e.target instanceof Element ? e.target.closest('.menu-item') : null;
        var menu = item ? item.closest('.menu') : null;
        if (!menu) return;
        var list = items(menu), at = list.indexOf(item), next = null, filter = filterOf(menu);
        if (e.key === 'ArrowDown') next = list[(at + 1) % list.length];
        else if (e.key === 'ArrowUp' && at === 0 && filter) next = filter;
        else if (e.key === 'ArrowUp') next = list[(at - 1 + list.length) % list.length];
        else if (e.key === 'Home') next = list[0];
        else if (e.key === 'End') next = list[list.length - 1];
        else if (e.key === (getComputedStyle(menu).direction === 'rtl' ? 'ArrowLeft' : 'ArrowRight') && item.hasAttribute('data-menu-sub')) {
            var sub = subOf(item);
            if (sub) { setSub(item, true); next = items(sub)[0]; }
        } else if (e.key === (getComputedStyle(menu).direction === 'rtl' ? 'ArrowRight' : 'ArrowLeft') && menu.classList.contains('menu-sub')) {
            var opener = menu.parentElement && menu.parentElement.querySelector(':scope > [data-menu-sub]');
            if (opener) { setSub(opener, false); next = opener; }
        } else if (e.key.length === 1 && e.key !== ' ' && !e.ctrlKey && !e.metaKey && !e.altKey) {
            // A letter goes to the next item whose label starts with it, as in a platform menu.
            if (!typed.has(menu)) typed.set(menu, {});
            next = ui._.typeahead(typed.get(menu), e.key, list, item, labelOf);
        }
        if (!next) return;
        e.preventDefault();
        next.focus();
    });

    var typed = new WeakMap();   // a menu -> the letters typed into it so far

    function labelOf(item) { return item.getAttribute('data-label') || item.textContent; }

    /* ── A menu that filters ───────────────────────────────────────────────
       An `<input data-menu-filter>` the app writes inside a `.menu` narrows its items as
       the reader types, ranked by the same matcher as the palette (ui._.score): an item
       that does not match is `hidden`, and so is a `.menu-label` or a `.menu-sep` with
       nothing left under it. The panel carries `data-menu-empty` while nothing matches,
       which shows the app's own `.menu-empty` row. The menu opens with the filter empty
       and focused; Down moves to the first item and Up from it comes back, Enter chooses
       the first item, and the arrows skip what is hidden. Submenus are not searched. */
    function filterOf(menu) {
        var f = menu.querySelector('[data-menu-filter]');
        return f && f.closest('.menu') === menu ? f : null;
    }

    function own(menu, el) { return el.closest('.menu') === menu; }

    function applyFilter(input) {
        var menu = input.closest('.menu');
        if (!menu) return;
        var query = input.value.trim(), shown = 0;
        var all = menu.querySelectorAll('.menu-item');
        for (var i = 0; i < all.length; i++) {
            if (!own(menu, all[i])) continue;
            var match = !query || ui._.score(query, String(labelOf(all[i]) || '').trim(), 0) >= 0;
            all[i].hidden = !match;
            if (match) shown++;
        }
        // A child that holds items — a submenu's anchor — goes with them, and a heading or
        // a rule goes with the items after it, up to the next one.
        var kids = menu.children, group = [], live = false;
        function close() {
            for (var g = 0; g < group.length; g++) {
                // A rule divides groups the filter has already mixed; a heading stays over what is left of its own.
                group[g].hidden = !!query && (group[g].classList.contains('menu-sep') || !live);
            }
            group = []; live = false;
        }
        for (var k = 0; k < kids.length; k++) {
            var kid = kids[k];
            if (kid.classList.contains('menu-label') || kid.classList.contains('menu-sep')) { close(); group.push(kid); continue; }
            if (kid.classList.contains('menu-item')) { if (!kid.hidden) live = true; continue; }
            var inner = Array.prototype.filter.call(kid.querySelectorAll('.menu-item'), function (el) { return own(menu, el); });
            if (!inner.length) continue;
            var visible = inner.some(function (el) { return !el.hidden; });
            kid.hidden = !visible;
            if (visible) live = true;
        }
        close();
        if (query && !shown) menu.setAttribute('data-menu-empty', ''); else menu.removeAttribute('data-menu-empty');
    }

    /* Opening starts from the whole list, with the reader typing. The value is cleared
       through an input event, so a Blazor binding on the filter hears it. */
    function startFilter(menu) {
        var filter = filterOf(menu);
        if (!filter) return false;
        if (filter.value) {
            filter.value = '';
            filter.dispatchEvent(new Event('input', { bubbles: true }));
        } else {
            applyFilter(filter);
        }
        try { filter.focus(); } catch (err) { /* not focusable */ }
        return true;
    }

    document.addEventListener('input', function (e) {
        var filter = e.target instanceof Element && e.target.hasAttribute('data-menu-filter') ? e.target : null;
        if (filter) applyFilter(filter);
    });

    document.addEventListener('keydown', function (e) {
        var filter = e.target instanceof Element && e.target.hasAttribute('data-menu-filter') ? e.target : null;
        var menu = filter ? filter.closest('.menu') : null;
        if (!menu || e.isComposing) return;
        var first = items(menu)[0];
        if (!first) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); first.focus(); }
        else if (e.key === 'Enter') { e.preventDefault(); first.click(); }
    });

    /* A submenu says it is open while the pointer or the focus is in it. The CSS opens it
       on :hover and :focus-within; this keeps aria-expanded true to what is shown. */
    document.addEventListener('focusout', function (e) {
        var anchor = e.target instanceof Element ? e.target.closest('.menu-sub-anchor') : null;
        if (!anchor || anchor.contains(e.relatedTarget)) return;
        var opener = anchor.querySelector(':scope > [data-menu-sub]');
        if (opener) setSub(opener, false);
    });
    document.addEventListener('pointerover', function (e) {
        var opener = e.target instanceof Element ? e.target.closest('[data-menu-sub]') : null;
        if (opener) opener.setAttribute('aria-expanded', 'true');
    });
    document.addEventListener('pointerout', function (e) {
        var anchor = e.target instanceof Element ? e.target.closest('.menu-sub-anchor') : null;
        if (!anchor || anchor.contains(e.relatedTarget) || anchor.hasAttribute('data-open')) return;
        var opener = anchor.querySelector(':scope > [data-menu-sub]');
        if (opener) opener.setAttribute('aria-expanded', 'false');
    });

    /* ── A menu at the pointer ─────────────────────────────────────────────
       `data-menu-context="<id>"` on any element opens the `.menu[popover]` of that id
       where the reader right-clicked it, or below the element for the ContextMenu key
       and Shift+F10, and moves the focus to its first item. The point is a 0×0 anchor
       nobody sees, so 58-menu.css places and flips the panel at the screen's edges with
       position-area, as it places a dropdown. The element the menu opened on carries
       `data-menu-context-target` while it is open, for the app to read.

       `openAt(panel, x, y, on)` is the same from the app's own code — for a menu the app
       decides to open, after checks of its own, which `data-menu-context` would skip. The
       panel and the element it is about are elements or ids, so C# can name them
       (ISednaUi.OpenMenuAtAsync), and it answers whether a menu opened. A scroll that
       moves the element it opened on closes it, and a menu opened at a bare point closes
       on any scroll outside it: the page has moved out from under it (22-anchored.js). */
    var point = null;
    var origins = new WeakMap();   // a menu opened at a point -> the element it is about, or null

    function pointAt(x, y) {
        if (!point) {
            point = document.createElement('span');
            point.setAttribute('aria-hidden', 'true');
            point.className = 'menu-point';
            document.body.appendChild(point);
        }
        point.style.left = Math.round(x) + 'px';
        point.style.top = Math.round(y) + 'px';
        return point;
    }

    function byId(x) { return typeof x === 'string' ? document.getElementById(x) : x; }

    function openAt(panel, x, y, on) {
        panel = byId(panel);
        on = byId(on) || null;
        if (!panel || typeof panel.showPopover !== 'function') return false;
        // Safari on macOS raises contextmenu on the press, and the release that follows would
        // light-dismiss a panel opened then; so while a secondary button is down the menu opens
        // on its release — whoever asked for it, the contextmenu handler below or the app.
        if (secondaryDown) {
            document.addEventListener('pointerup', function once(ev) {
                if (ev.button !== 2) return;
                document.removeEventListener('pointerup', once, true);
                setTimeout(function () { show(panel, x, y, on); }, 0);
            }, true);
            return true;
        }
        show(panel, x, y, on);
        return true;
    }

    function show(panel, x, y, on) {
        pointAt(x, y);
        origins.set(panel, on);
        var was = document.querySelector('[data-menu-context-target]');
        if (was) was.removeAttribute('data-menu-context-target');
        if (on) on.setAttribute('data-menu-context-target', '');
        try { if (panel.matches(':popover-open')) panel.hidePopover(); } catch (err) { /* closed */ }
        try { panel.showPopover(); } catch (err2) { return; }
        if (startFilter(panel)) return;
        var first = items(panel)[0];
        if (first) first.focus();
    }

    ui.menu.openAt = openAt;
    /* What a menu opened at a point is about: the element, null for a bare point, and
       undefined for a menu that was not opened at a point. Private, for 22-anchored.js. */
    ui._.menuOrigin = function (panel) { return origins.has(panel) ? origins.get(panel) : undefined; };

    var secondaryDown = false;
    document.addEventListener('pointerdown', function (e) { if (e.button === 2) secondaryDown = true; }, true);
    document.addEventListener('pointerup', function (e) { if (e.button === 2) secondaryDown = false; }, true);

    document.addEventListener('contextmenu', function (e) {
        var on = e.target instanceof Element ? e.target.closest('[data-menu-context]') : null;
        if (!on) return;
        var panel = document.getElementById(on.getAttribute('data-menu-context'));
        if (!panel) return;
        e.preventDefault();
        openAt(panel, e.clientX, e.clientY, on);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'ContextMenu' && !(e.key === 'F10' && e.shiftKey)) return;
        var on = e.target instanceof Element ? e.target.closest('[data-menu-context]') : null;
        if (!on) return;
        var panel = document.getElementById(on.getAttribute('data-menu-context'));
        if (!panel) return;
        e.preventDefault();
        var r = on.getBoundingClientRect();
        openAt(panel, r.left, r.bottom, on);
    });

    document.addEventListener('toggle', function (e) {
        var panel = e.target;
        if (e.newState !== 'closed' || !panel || !panel.matches || !panel.matches('.menu[popover]')) return;
        var on = origins.get(panel);
        origins.delete(panel);
        if (on && on.hasAttribute('data-menu-context-target')) on.removeAttribute('data-menu-context-target');
    }, true);

    /* An item chosen in a menu at the pointer closes it, as it closes a dropdown. */
    document.addEventListener('click', function (e) {
        var item = e.target instanceof Element ? e.target.closest('.menu-item') : null;
        if (!item || item.hasAttribute('data-menu-sub')) return;
        var panel = item.closest('.menu[popover]');
        try { if (panel && panel.matches(':popover-open')) panel.hidePopover(); } catch (err) { /* closed */ }
    });

})(window.sednaUi);

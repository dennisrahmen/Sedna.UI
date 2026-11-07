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
        if (e.target.closest('.menu')) return;

        closeAll(null);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var open = document.querySelector('[data-menu-toggle][aria-expanded="true"]');
        if (!open) return;
        closeAll(null);
        // Focus goes back to the control that opened it. Without this, focus is left
        // on a node that has just been hidden and the next Tab starts from the top
        // of the document.
        try { open.focus(); } catch (err) { /* detached */ }
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
        var list = items(menu), at = list.indexOf(item), next = null;
        if (e.key === 'ArrowDown') next = list[(at + 1) % list.length];
        else if (e.key === 'ArrowUp') next = list[(at - 1 + list.length) % list.length];
        else if (e.key === 'Home') next = list[0];
        else if (e.key === 'End') next = list[list.length - 1];
        else if (e.key === (getComputedStyle(menu).direction === 'rtl' ? 'ArrowLeft' : 'ArrowRight') && item.hasAttribute('data-menu-sub')) {
            var sub = subOf(item);
            if (sub) { setSub(item, true); next = items(sub)[0]; }
        } else if (e.key === (getComputedStyle(menu).direction === 'rtl' ? 'ArrowRight' : 'ArrowLeft') && menu.classList.contains('menu-sub')) {
            var opener = menu.parentElement && menu.parentElement.querySelector(':scope > [data-menu-sub]');
            if (opener) { setSub(opener, false); next = opener; }
        }
        if (!next) return;
        e.preventDefault();
        next.focus();
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

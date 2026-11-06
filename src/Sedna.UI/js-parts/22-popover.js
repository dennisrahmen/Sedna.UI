/* ── A popover opened from something that is not a button ───────────────────
   `popovertarget` makes its button the popover's implicit anchor, and only a
   <button> or an <input> can carry it. A count inside a link, an avatar, a chip or a
   table cell cannot be one, and `showPopover()` on its own gives the panel nothing to
   be placed against: it opens in the corner of the viewport.

     <a class="nav-link" href="/outbox">Outbox
       <span class="nav-count" data-popover="outbox-detail" tabindex="0">5</span></a>
     <div class="popover" id="outbox-detail" popover>…</div>

   `data-popover="<id>"` on any element opens and closes that popover from it, by click,
   Enter or Space, and anchors it there: the trigger gets an `anchor-name`, the panel the
   matching `position-anchor`, and 65-popover.css places it exactly as it places one
   opened by a button. `sednaUi.popover.show(panel, trigger)` does the same from the
   app's own code. Nothing is measured and no coordinate is written. The click on a
   trigger inside a link is taken, so the link does not follow it.

   The trigger gets aria-expanded and aria-controls; it needs a role and a tabindex of
   the app's own when it is not focusable already, as any control does.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var triggers = new WeakMap();
    var count = 0;

    function anchor(panel, trigger) {
        var name = trigger.style.getPropertyValue('anchor-name');
        if (!name) {
            name = '--sedna-popover-' + (++count);
            trigger.style.setProperty('anchor-name', name);
        }
        panel.style.setProperty('position-anchor', name);
    }

    function show(panel, trigger) {
        if (!panel || typeof panel.showPopover !== 'function') return;
        if (trigger) {
            triggers.set(panel, trigger);
            anchor(panel, trigger);
            if (panel.id) trigger.setAttribute('aria-controls', panel.id);
        }
        try { panel.showPopover(); } catch (err) { /* already open, or not a popover */ }
    }

    function hide(panel) {
        try { if (panel && panel.matches(':popover-open')) panel.hidePopover(); }
        catch (err) { /* already closed */ }
    }

    ui.popover = {
        show: show,
        hide: hide,
        toggle: function (panel, trigger) {
            var open = false;
            try { open = panel.matches(':popover-open'); } catch (err) { /* no :popover-open */ }
            if (open) hide(panel); else show(panel, trigger);
        },
        /* The element a popover was opened from: the one show() was given, or its
           popovertarget button. 22-anchored.js closes a panel whose trigger scrolled. */
        triggerOf: function (panel) {
            var t = triggers.get(panel);
            if (t) return t;
            return panel.id ? document.querySelector('[popovertarget="' + panel.id + '"]') : null;
        }
    };

    function target(trigger) {
        var id = trigger.getAttribute('data-popover');
        return id ? document.getElementById(id) : null;
    }

    /* A press on the trigger of an open panel light-dismisses it before the click
       arrives, and the click would open it again. The platform knows a popovertarget
       button and skips that; this remembers which panel was open when the press began. */
    var wasOpen = null;
    document.addEventListener('pointerdown', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover]') : null;
        var panel = trigger ? target(trigger) : null;
        wasOpen = null;
        try { if (panel && panel.matches(':popover-open')) wasOpen = panel; } catch (err) { /* no :popover-open */ }
    }, true);

    document.addEventListener('click', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover]') : null;
        if (!trigger) return;
        var panel = target(trigger);
        if (!panel) return;
        e.preventDefault();
        e.stopPropagation();
        // A hover card clicked is pinned: shown, and no longer closed by the pointer leaving.
        if (trigger.hasAttribute('data-popover-hover')) {
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            show(panel, trigger);
            return;
        }
        if (wasOpen === panel) { wasOpen = null; hide(panel); return; }
        ui.popover.toggle(panel, trigger);
    });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        var trigger = e.target instanceof Element ? e.target : null;
        if (!trigger || !trigger.hasAttribute('data-popover')) return;
        if (trigger.tagName === 'BUTTON') return;   // a button clicks on its own
        var panel = target(trigger);
        if (!panel) return;
        e.preventDefault();
        ui.popover.toggle(panel, trigger);
    });

    /* ── A hover card ──────────────────────────────────────────────────────
       `data-popover-hover` beside `data-popover` also opens the panel when the pointer
       rests on the trigger or the focus reaches it, after `--hover-delay`, and closes it a
       moment after the pointer has left both the trigger and the panel — so the reader
       can move onto the card to select or follow something in it. A click still pins it
       open until it is dismissed. The panel is the app's, as for any popover. */
    var hoverTimer = 0, leaveTimer = 0, hovered = null;

    function delayOf(trigger) {
        var d = parseFloat(getComputedStyle(trigger).getPropertyValue('--hover-delay'));
        return isNaN(d) ? 400 : d;
    }

    function enter(trigger) {
        var panel = target(trigger);
        if (!panel) return;
        clearTimeout(leaveTimer);
        clearTimeout(hoverTimer);
        hovered = panel;
        hoverTimer = setTimeout(function () { show(panel, trigger); }, delayOf(trigger));
    }

    function leave() {
        clearTimeout(hoverTimer);
        var panel = hovered;
        if (!panel) return;
        clearTimeout(leaveTimer);
        leaveTimer = setTimeout(function () {
            var trigger = triggers.get(panel);
            var stay = panel.matches(':hover') || panel.contains(document.activeElement)
                || (trigger && (trigger.matches(':hover') || trigger === document.activeElement));
            if (!stay) { hide(panel); hovered = null; }
        }, 200);
    }

    document.addEventListener('pointerover', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el) return;
        var trigger = el.closest('[data-popover][data-popover-hover]');
        if (trigger && !trigger.contains(e.relatedTarget)) { enter(trigger); return; }
        if (hovered && hovered.contains(el)) clearTimeout(leaveTimer);
    });
    document.addEventListener('pointerout', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el || !hovered) return;
        var from = el.closest('[data-popover][data-popover-hover]') || (hovered.contains(el) ? hovered : null);
        if (from && !from.contains(e.relatedTarget)) leave();
    });
    document.addEventListener('focusin', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover][data-popover-hover]') : null;
        if (trigger) enter(trigger);
    });
    document.addEventListener('focusout', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (el && (el.closest('[data-popover][data-popover-hover]') || (hovered && hovered.contains(el)))) leave();
    });

    /* The trigger says whether its panel is open, however the panel was closed:
       light dismiss, Escape, or the app. */
    document.addEventListener('toggle', function (e) {
        var panel = e.target;
        if (!panel || !panel.matches || !panel.matches('[popover]')) return;
        var trigger = triggers.get(panel);
        if (!trigger || !trigger.hasAttribute('data-popover')) return;
        trigger.setAttribute('aria-expanded', e.newState === 'open' ? 'true' : 'false');
    }, true);

})(window.sednaUi);

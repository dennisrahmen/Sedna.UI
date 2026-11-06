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

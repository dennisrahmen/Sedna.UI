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
    var pinned = new WeakSet();   // hover cards a click has pinned open
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
        // A hover-only card leaves the click to the trigger's own action — a button that opens
        // a dialog — and gets out of its way, so it does not sit above what the click opened.
        if (hoverOnly(trigger)) {
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            hide(panel);
            return;
        }
        e.preventDefault();
        e.stopPropagation();
        // A hover card clicked is pinned: shown, and no longer closed by the pointer leaving.
        // Clicked again once pinned, it closes, as a popovertarget button toggles its panel.
        if (trigger.hasAttribute('data-popover-hover')) {
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            var open = false;
            try { open = panel.matches(':popover-open'); } catch (err) { /* no :popover-open */ }
            if (pinned.has(panel) && (open || wasOpen === panel)) {
                wasOpen = null;
                pinned.delete(panel);
                hide(panel);
                return;
            }
            wasOpen = null;
            pinned.add(panel);
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
        if (hoverOnly(trigger)) return;             // and so does a hover-only trigger's own action
        var panel = target(trigger);
        if (!panel) return;
        e.preventDefault();
        ui.popover.toggle(panel, trigger);
    });

    /* ── A hover card ──────────────────────────────────────────────────────
       `data-popover-hover` beside `data-popover` also opens the panel when the pointer
       rests on the trigger or the focus reaches it, after `--hover-delay`, and closes it a
       moment after the pointer has left both the trigger and the panel — so the reader
       can move onto the card to select or follow something in it. A click pins it open
       until it is dismissed or the trigger is clicked again. The panel is the app's, as for
       any popover.

       `data-popover-hover="only"` is for a trigger with a click action of its own — a
       button that opens a dialog, beside a summary of what the dialog holds. Hover and
       focus open the card; a click neither opens nor pins it, is left to the trigger, and
       closes the card. */
    var hoverTimer = 0, leaveTimer = 0, hovered = null;

    function hoverOnly(trigger) { return trigger.getAttribute('data-popover-hover') === 'only'; }

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

    // A finger has no hover: a touch that lands on a trigger is a tap or a long press,
    // never a pointer resting on it.
    document.addEventListener('pointerover', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el || e.pointerType === 'touch') return;
        var trigger = el.closest('[data-popover][data-popover-hover]');
        if (trigger && !trigger.contains(e.relatedTarget)) { enter(trigger); return; }
        if (hovered && hovered.contains(el)) clearTimeout(leaveTimer);
    });
    document.addEventListener('pointerout', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el || !hovered || e.pointerType === 'touch') return;
        var from = el.closest('[data-popover][data-popover-hover]') || (hovered.contains(el) ? hovered : null);
        if (from && !from.contains(e.relatedTarget)) leave();
    });
    document.addEventListener('focusin', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover][data-popover-hover]') : null;
        // The focus a finger's press brings is not the keyboard arriving: a press is a tap
        // or a long press, below.
        if (trigger && !press) enter(trigger);
    });
    document.addEventListener('focusout', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (el && (el.closest('[data-popover][data-popover-hover]') || (hovered && hovered.contains(el)))) leave();
    });

    /* ── A long press ──────────────────────────────────────────────────────
       Touch has no hover, so a finger held on a hover card's trigger for `LONG_PRESS`
       opens the card and pins it, as a click pins it for a mouse. The press does not
       also act: the click that follows it is taken, so a link is not followed and an
       "only" trigger's own action does not run, and the platform's own long-press menu
       is refused. A finger that moves is scrolling, and a tap is unchanged.

       The release is outside the card, which is a light dismiss to the platform — the
       card opened while the finger was down — and so is the mouse press an engine makes
       up from it. So a card a long press opened opens again when the platform closes it,
       until the next press or key: that is where the reader dismisses it. */
    var LONG_PRESS = 500, SLOP = 10;
    var press = null;     // { trigger, panel, id, x, y, timer, fired } while a finger is down
    var pressed = null;   // the trigger whose long press opened its card, until its click
    var held = null;      // { trigger, panel } of that card, until the next press or key

    function cancelPress() {
        if (press) clearTimeout(press.timer);
        press = null;
    }

    document.addEventListener('pointerdown', function (e) {
        cancelPress();
        pressed = null;
        held = null;
        if (e.pointerType !== 'touch') return;
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover][data-popover-hover]') : null;
        var panel = trigger ? target(trigger) : null;
        if (!panel) return;
        var p = press = { trigger: trigger, panel: panel, id: e.pointerId, x: e.clientX, y: e.clientY, fired: false };
        p.timer = setTimeout(function () {
            p.fired = true;
            pressed = trigger;
            held = { trigger: trigger, panel: panel };
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            pinned.add(panel);
            show(panel, trigger);
        }, LONG_PRESS);
    }, true);

    document.addEventListener('pointermove', function (e) {
        if (!press || press.fired || e.pointerId !== press.id) return;
        if (Math.abs(e.clientX - press.x) > SLOP || Math.abs(e.clientY - press.y) > SLOP) cancelPress();
    }, true);

    function lift(e) {
        if (!press || e.pointerId !== press.id) return;
        var fired = press.fired;
        cancelPress();
        // A press the platform took over — a scroll — opened nothing it should keep.
        if (fired && e.type === 'pointercancel') held = null;
    }
    document.addEventListener('pointerup', lift, true);
    document.addEventListener('pointercancel', lift, true);

    // Before the trigger's own click handlers, and before the pin above: the press has acted.
    document.addEventListener('click', function (e) {
        if (!pressed || !(e.target instanceof Node) || !pressed.contains(e.target)) return;
        pressed = null;
        e.preventDefault();
        e.stopImmediatePropagation();
    }, true);

    document.addEventListener('contextmenu', function (e) {
        if ((press && press.trigger.contains(e.target)) || (pressed && pressed.contains(e.target))) e.preventDefault();
    }, true);
    document.addEventListener('keydown', function () { held = null; }, true);

    /* The trigger says whether its panel is open, however the panel was closed:
       light dismiss, Escape, or the app. */
    document.addEventListener('toggle', function (e) {
        var panel = e.target;
        if (!panel || !panel.matches || !panel.matches('[popover]')) return;
        if (e.newState === 'closed') pinned.delete(panel);
        if (e.newState === 'closed' && held && held.panel === panel && held.trigger.isConnected) {
            pinned.add(panel);
            show(panel, held.trigger);
            return;
        }
        var trigger = triggers.get(panel);
        if (!trigger || !trigger.hasAttribute('data-popover')) return;
        trigger.setAttribute('aria-expanded', e.newState === 'open' ? 'true' : 'false');
    }, true);

})(window.sednaUi);

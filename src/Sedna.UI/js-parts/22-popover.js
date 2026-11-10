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

   ONE PANEL, MANY TRIGGERS. Any number of triggers may name the same popover. Open at one
   and asked to open at another, it closes and opens again there — an anchored panel is
   placed as it is shown, so moving its anchor while it is open would leave it where it was.
   A trigger inside another open popover opens the panel as that popover's child, so the
   one it sits in stays open.

   FILLED PER TRIGGER — data-popover-for. A panel that carries `data-popover-for` holds the
   content of one trigger at a time, and its value names which: the trigger's id. Before
   such a panel opens at a trigger, a bubbling `sedna-popover-fill` is dispatched on the
   trigger, with `detail.trigger` (its id) and `detail.popover` (the panel's id). The app
   fills the panel and writes the trigger's id into `data-popover-for`, and the panel opens
   when it reads that id — at once when it already does. In Blazor that is the handler and
   one attribute, written in the same render as the content:

     <div class="popover" id="order-card" popover data-popover-for="@_cardFor">…</div>
     <tr id="@($"o-{o.Id}")" data-popover="order-card" data-popover-hover
         @onsedna-popover-fill="() => Fill(o)">…</tr>

   Nothing waits on .NET and nothing is called back: the render the app makes anyway is the
   answer. A request the reader has already left behind — the pointer gone, another
   trigger asked for — opens nothing when its answer comes. A trigger with no id cannot be
   named, and its panel opens as it is, with a warning in the console.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var triggers = new WeakMap();
    var pinned = new WeakSet();   // hover cards a click has pinned open
    var count = 0;

    function isOpen(panel) {
        try { return panel.matches(':popover-open'); } catch (err) { return false; }
    }

    // Whether showPopover() takes a `source`, which makes the trigger the panel's invoker and
    // so the popover around the trigger its ancestor. Read from the dictionary itself: the
    // argument is converted, and its getter run, before the call fails on <html>, which is
    // no popover — so nothing is shown and nothing is made.
    var takesSource = false;
    try {
        document.documentElement.showPopover({ get source() { takesSource = true; return null; } });
    } catch (err) { /* not a popover: thrown after the argument was read */ }

    // The open auto popover a trigger sits in, which opening a panel from it must not close.
    function around(trigger) {
        var host = trigger.parentElement ? trigger.parentElement.closest('[popover]') : null;
        return host && isOpen(host) ? host : null;
    }

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
        var was = triggers.get(panel);
        if (trigger) {
            triggers.set(panel, trigger);
            anchor(panel, trigger);
            if (panel.id) trigger.setAttribute('aria-controls', panel.id);
        }
        // Open at another trigger: closed and opened again, which places it at this one.
        if (trigger && was && was !== trigger && isOpen(panel)) {
            if (was.hasAttribute('data-popover')) was.setAttribute('aria-expanded', 'false');
            hide(panel);
        }
        try {
            if (trigger && takesSource && around(trigger)) panel.showPopover({ source: trigger });
            else panel.showPopover();
        } catch (err) { /* already open, or not a popover */ }
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

    /* Opens a panel at a trigger the reader reached — by click, key, hover, focus or a long
       press — asking the app to fill it first when it carries `data-popover-for`. `pin` pins a
       hover card once it opens. One request is outstanding at a time: a newer one, or the
       reader leaving, drops the last. */
    var pending = null;   // { panel, trigger, observer } while the app fills a panel

    function cancelFill() {
        if (pending) pending.observer.disconnect();
        pending = null;
    }

    function request(panel, trigger, pin) {
        cancelFill();
        var go = function () {
            if (pin) pinned.add(panel);
            show(panel, trigger);
        };
        if (!panel.hasAttribute('data-popover-for')) { go(); return; }
        var name = trigger.id;
        if (!name) {
            console.warn('sednaUi.popover: a trigger of #' + panel.id + ' has no id, so data-popover-for cannot name it; the panel opens as it is.');
            go();
            return;
        }
        trigger.dispatchEvent(new CustomEvent('sedna-popover-fill', {
            bubbles: true,
            detail: { trigger: name, popover: panel.id || null }
        }));
        if (panel.getAttribute('data-popover-for') === name) { go(); return; }
        if (typeof MutationObserver !== 'function') { go(); return; }
        var p = pending = { panel: panel, trigger: trigger };
        p.observer = new MutationObserver(function () {
            if (pending !== p || panel.getAttribute('data-popover-for') !== name) return;
            cancelFill();
            if (trigger.isConnected) go();
        });
        p.observer.observe(panel, { attributes: true, attributeFilter: ['data-popover-for'] });
    }

    /* Whether a hover card would close something the reader opened. Showing an auto popover
       closes every other open one that is not its ancestor, so a card on hover or focus waits
       while another is open: a menu, a picker, a list in a popover, a card a click pinned.
       Not one it sits inside — it opens as that one's child — and not another hover card,
       which is only a glance and gives way. */
    function blocked(panel, trigger) {
        var open;
        try { open = document.querySelectorAll(':popover-open'); } catch (err) { return false; }
        for (var i = 0; i < open.length; i++) {
            var p = open[i];
            if (p === panel || p.contains(panel)) continue;
            if (takesSource && p.contains(trigger)) continue;
            var kind = (p.getAttribute('popover') || '').toLowerCase();
            if (kind !== '' && kind !== 'auto') continue;
            var t = triggers.get(p);
            if (t && t.hasAttribute('data-popover-hover') && !pinned.has(p)) continue;
            return true;
        }
        return false;
    }

    /* A press on the trigger of an open panel light-dismisses it before the click
       arrives, and the click would open it again. The platform knows a popovertarget
       button and skips that; this remembers which panel was open when the press began. */
    var wasOpen = null, wasAt = null;
    document.addEventListener('pointerdown', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover]') : null;
        var panel = trigger ? target(trigger) : null;
        wasOpen = wasAt = null;
        if (panel && isOpen(panel)) { wasOpen = panel; wasAt = triggers.get(panel) || null; }
    }, true);

    // Whether the panel is open at this trigger, or was when the press began: a press light-
    // dismisses it before the click arrives. Open at another trigger is not open here.
    function openAt(panel, trigger) {
        var at = wasOpen === panel ? wasAt : (isOpen(panel) ? triggers.get(panel) : null);
        wasOpen = wasAt = null;
        return at === trigger;
    }

    document.addEventListener('click', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover]') : null;
        if (!trigger) return;
        var panel = target(trigger);
        if (!panel) return;
        // A hover-only card leaves the click to the trigger's own action — a button that opens
        // a dialog — and gets out of its way, so it does not sit above what the click opened.
        if (hoverOnly(trigger)) {
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            cancelFill();
            hide(panel);
            return;
        }
        e.preventDefault();
        e.stopPropagation();
        var here = openAt(panel, trigger);
        // A hover card clicked is pinned: shown, and no longer closed by the pointer leaving.
        // Clicked again once pinned, it closes, as a popovertarget button toggles its panel.
        if (trigger.hasAttribute('data-popover-hover')) {
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            if (pinned.has(panel) && here) {
                cancelFill();
                pinned.delete(panel);
                hide(panel);
                return;
            }
            request(panel, trigger, true);
            return;
        }
        if (here) { cancelFill(); hide(panel); return; }
        request(panel, trigger, false);
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
        if (isOpen(panel) && triggers.get(panel) === trigger) { cancelFill(); hide(panel); }
        else request(panel, trigger, false);
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
       closes the card.

       Any number of triggers can share one card, which follows the pointer or the focus to
       each after the same rest, filled per trigger with `data-popover-for` (above). A card a
       click pinned stays where it was pinned. A hover card does not open over another open
       popover — a menu, a picker, a list, a pinned card — that opening it would close. */
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
        if (pinned.has(panel) && isOpen(panel)) return;   // pinned where the reader left it
        hovered = panel;
        hoverTimer = setTimeout(function () {
            if (!blocked(panel, trigger)) request(panel, trigger, false);
        }, delayOf(trigger));
    }

    function engaged(el) { return !!el && (el.matches(':hover') || el === document.activeElement); }

    function leave() {
        clearTimeout(hoverTimer);
        var panel = hovered;
        if (!panel) return;
        clearTimeout(leaveTimer);
        leaveTimer = setTimeout(function () {
            var trigger = triggers.get(panel);
            var waiting = pending && pending.panel === panel ? pending.trigger : null;
            var stay = panel.matches(':hover') || panel.contains(document.activeElement)
                || engaged(trigger) || engaged(waiting);
            if (!stay) { cancelFill(); hide(panel); hovered = null; }
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
            request(panel, trigger, true);
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
        // Read from the panel as it is now: a card moved to another trigger closed and opened
        // again in one go, and an engine may report that as one event or as two.
        var open = isOpen(panel);
        if (!open) pinned.delete(panel);
        if (!open && held && held.panel === panel && held.trigger.isConnected) {
            pinned.add(panel);
            show(panel, held.trigger);
            return;
        }
        var trigger = triggers.get(panel);
        if (!trigger || !trigger.hasAttribute('data-popover')) return;
        trigger.setAttribute('aria-expanded', open ? 'true' : 'false');
    }, true);

})(window.sednaUi);

/* ── Tabs, delegated ─────────────────────────────────────────────────────────
   Opt-in wiring for .tabs, and the reason it exists is the keyboard: the CSS can
   colour a selected tab, but arrow-key movement between tabs and the single-stop
   tab order are behaviour, and a tablist without them is a tablist in name only.

   Add data-tabs to the .tabs container. Each tab needs role="tab",
   aria-controls="<panel id>" and aria-selected; each panel needs role="tabpanel"
   and a matching id.

     <div class="tabs" role="tablist" data-tabs>
       <button class="tab" role="tab" aria-selected="true"  aria-controls="p1">Open</button>
       <button class="tab" role="tab" aria-selected="false" aria-controls="p2">All</button>
     </div>
     <div class="tab-panel" role="tabpanel" id="p1">…</div>
     <div class="tab-panel" role="tabpanel" id="p2" hidden>…</div>

   A tablist whose selection the app keeps — a Blazor component rendering
   aria-selected from its own state — takes data-tabs="managed" instead. The script
   then owns only the keyboard: an arrow, Home or End moves focus to the next tab and
   CLICKS it, and the app's own click handler changes the selection and renders
   aria-selected, tabindex and the panels' hidden. Nothing here writes those, so there
   are never two things setting them. SednaTabs.Tab and SednaTabs.Panel in C# render
   the attributes. The combo field works the same way: the page owns the selection,
   and the script clicks.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function tabsIn(list) {
        return Array.prototype.filter.call(
            list.querySelectorAll('[role="tab"]'),
            function (t) { return !t.disabled && t.getAttribute('aria-disabled') !== 'true'; });
    }

    function select(list, tab) {
        var all = list.querySelectorAll('[role="tab"]');
        for (var i = 0; i < all.length; i++) {
            var isIt = all[i] === tab;
            all[i].setAttribute('aria-selected', String(isIt));
            // Roving tabindex: only the selected tab is a tab stop, so Tab moves past
            // the whole tablist rather than through every tab in it.
            all[i].tabIndex = isIt ? 0 : -1;

            var panel = document.getElementById(all[i].getAttribute('aria-controls') || '');
            if (panel) panel.hidden = !isIt;
        }
    }

    function managed(list) { return list.getAttribute('data-tabs') === 'managed'; }

    ui.tabs = {
        // Selects a tab programmatically, by element or by its aria-controls id. In a
        // managed tablist that is a click on it, which the app's handler answers.
        select: function (tabOrPanelId) {
            var tab = typeof tabOrPanelId === 'string'
                ? document.querySelector('[role="tab"][aria-controls="' + tabOrPanelId + '"]')
                : tabOrPanelId;
            var list = tab && tab.closest('[data-tabs]');
            if (!list) return;
            if (managed(list)) tab.click();
            else select(list, tab);
        }
    };

    document.addEventListener('click', function (e) {
        var tab = e.target.closest('[data-tabs] [role="tab"]');
        if (!tab || tab.disabled) return;
        var list = tab.closest('[data-tabs]');
        // The app's own handler selects a managed tab.
        if (managed(list)) return;
        e.preventDefault();
        select(list, tab);
    });

    document.addEventListener('keydown', function (e) {
        var tab = e.target.closest('[data-tabs] [role="tab"]');
        if (!tab) return;

        var list = tab.closest('[data-tabs]');
        var tabs = tabsIn(list);
        var at = tabs.indexOf(tab);
        if (at < 0) return;

        // Home/End as well as the arrows: with a dozen tabs, holding an arrow key to
        // reach the last one is the kind of thing that makes people use a mouse.
        var to = -1;
        if (e.key === 'ArrowRight' || e.key === 'ArrowDown') to = (at + 1) % tabs.length;
        else if (e.key === 'ArrowLeft' || e.key === 'ArrowUp') to = (at - 1 + tabs.length) % tabs.length;
        else if (e.key === 'Home') to = 0;
        else if (e.key === 'End') to = tabs.length - 1;
        else return;

        e.preventDefault();
        tabs[to].focus();
        if (managed(list)) tabs[to].click();
        else select(list, tabs[to]);
    });

})(window.sednaUi);

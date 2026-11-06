/* ── Groups that fold ────────────────────────────────────────────────────────
   A grouped list is flat: a `.sticky-group-head` and then its rows, then the next head,
   all siblings. Folding a group means hiding the rows up to the next head, which no
   selector can find — so this part does it.

     <li class="sticky-group-head" style="--group-head-index: 3">
       <button class="sticky-group-toggle" type="button" data-group-toggle aria-expanded="true">Later</button>
       <span class="sticky-group-count">12</span>
     </li>

   A click on the toggle flips its `aria-expanded`. Whatever the value — set by the click,
   or rendered by the app from the state it keeps — the rows of that group carry
   `data-group-hidden` while it is false, and 60-lists.css takes them out of the layout and
   the accessibility tree. `data-group-toggle="managed"` leaves the click to the app, which
   renders `aria-expanded` itself, as a managed tablist does; the rows still follow it.

   The rows are re-marked after any render that adds nodes, so a row the app renders into a
   folded group is folded too. Nothing but `data-group-hidden` is written, never `hidden`,
   which may be the app's own. */
(function (ui) {

    function rowsOf(head) {
        var rows = [];
        for (var el = head.nextElementSibling; el && !el.classList.contains('sticky-group-head'); el = el.nextElementSibling)
            rows.push(el);
        return rows;
    }

    function apply(toggle) {
        var head = toggle.closest('.sticky-group-head');
        if (!head) return;
        var folded = toggle.getAttribute('aria-expanded') === 'false';
        var rows = rowsOf(head);
        for (var i = 0; i < rows.length; i++) {
            if (folded) rows[i].setAttribute('data-group-hidden', '');
            else rows[i].removeAttribute('data-group-hidden');
        }
    }

    function applyAll(root) {
        var toggles = (root || document).querySelectorAll('[data-group-toggle]');
        for (var i = 0; i < toggles.length; i++) apply(toggles[i]);
    }

    ui.groups = {
        /* Folds or unfolds the group a toggle heads, as a click would. */
        toggle: function (toggle) {
            if (!toggle) return;
            toggle.setAttribute('aria-expanded', toggle.getAttribute('aria-expanded') === 'false' ? 'true' : 'false');
            apply(toggle);
        },
        /* Marks every group's rows from its toggle's aria-expanded, for a page that
           changed them some way this part did not see. */
        refresh: function () { applyAll(document); }
    };

    document.addEventListener('click', function (e) {
        var toggle = e.target instanceof Element ? e.target.closest('[data-group-toggle]') : null;
        if (!toggle) return;
        if (toggle.getAttribute('data-group-toggle') === 'managed') return;
        ui.groups.toggle(toggle);
    });

    /* A managed toggle's aria-expanded changes by a render, and a new row can arrive in a
       folded group; both are a mutation. Only a page with a toggle on it does any work. */
    var scheduled = false;
    function later() {
        if (scheduled) return;
        scheduled = true;
        requestAnimationFrame(function () { scheduled = false; applyAll(document); });
    }
    function watch() {
        new MutationObserver(function (records) {
            if (!document.querySelector('[data-group-toggle]')) return;
            for (var i = 0; i < records.length; i++) {
                var r = records[i];
                if (r.type === 'childList' || (r.attributeName === 'aria-expanded' && r.target.hasAttribute('data-group-toggle'))) {
                    later();
                    return;
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-expanded'] });
        applyAll(document);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();

})(window.sednaUi);

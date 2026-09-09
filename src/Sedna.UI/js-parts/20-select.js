/* ── Customizable select — the <selectedcontent> repair ──────────────────────
   One job, and it is a repair rather than a feature: filling in the clone the
   browser was supposed to make.

   A `.form-select` whose first child is `<button><selectedcontent></selectedcontent>`
   shows a copy of the chosen `<option>` in its closed box — icon, label and all.
   The browser makes that copy when the select's selectedness is set. **Blazor's
   render does not trigger it**, so under interactive rendering the copy never
   happens and the closed box comes out BLANK: no error, no warning, and a field
   that looks like it has no value. Parse the same markup with `innerHTML` and it
   is fine, which is why nothing about the markup looks wrong.

   Assigning `selectedIndex` to the value it already has is what makes the browser
   redo the copy. It dispatches no events, so no binding sees a change, and the
   repaired content survives until that subtree is rendered again — which is what
   the observer below is for.

   ── Why this is in the library and not in each app ──
   Every consuming app is Blazor, so every one of them hits it, and the failure is
   silent. The alternative was telling apps not to use `<selectedcontent>` at all,
   which costs the icon in the closed box and — worse — spills an `.option-hint`
   into it as unseparated text, because the browser's own fallback button flattens
   every text node in the option.

   Nothing in the STYLESHEET depends on this. With scripting blocked, a select
   still has its theme, its drop-down and its caret; only the copy in the closed
   box is missing, and only for markup that opted into a button.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    // Nothing to repair where there is no base appearance: the browser draws its
    // own closed box and <selectedcontent> is inert.
    var supported = (function () {
        try { return window.CSS && CSS.supports('appearance', 'base-select'); }
        catch (e) { return false; }
    })();

    // `:has()` is the whole selector. A browser without it throws on
    // querySelectorAll, which the caller catches — see repair().
    var EMPTY_CLONE = 'select:has(> button > selectedcontent:empty)';

    function repair(root) {
        if (!supported) return 0;

        var found;
        try { found = (root || document).querySelectorAll(EMPTY_CLONE); }
        catch (e) { return 0; }

        var n = 0;
        for (var i = 0; i < found.length; i++) {
            var sel = found[i];
            // A list box has no closed box and no clone, and assigning
            // selectedIndex to one would collapse a multiple selection to a single
            // row. -1 is "nothing chosen", where an empty clone is correct.
            if (sel.multiple || sel.selectedIndex < 0) continue;
            sel.selectedIndex = sel.selectedIndex;
            n++;
        }
        return n;
    }

    ui.select = {
        // Runs the repair over `root` (the document by default) and returns how
        // many selects it fixed. Called for you on load and after every render
        // that adds nodes; an app needs this only if it moves a select into place
        // without touching the DOM tree the observer is watching.
        refresh: function (root) { return repair(root); }
    };

    if (!supported) return;

    // Coalesced to one pass per task: a Blazor render produces dozens of mutation
    // records and the repair only has to run once for them.
    //
    // A TIMER, not requestAnimationFrame. rAF does not fire at all in a hidden tab,
    // so a page rendered in a background tab was never repaired — and worse, the
    // flag stayed raised, so nothing scheduled again after the reader switched to
    // it. A throttled timer is late; a paused one never runs.
    var queued = false;
    function schedule() {
        if (queued) return;
        queued = true;
        setTimeout(function () { queued = false; repair(); }, 0);
    }

    // Added nodes only. A render that just changes an attribute or a text node
    // cannot have discarded a clone, and scanning the whole document on every one
    // of those is the difference between a cheap observer and a hot one.
    function onMutations(records) {
        for (var i = 0; i < records.length; i++) {
            if (records[i].addedNodes.length) { schedule(); return; }
        }
    }

    schedule();
    // The script loads at the end of <body>, so the document is usually parsed
    // already — but not when an app moves the tag, and the pass above is cheap.
    document.addEventListener('DOMContentLoaded', schedule);
    // Background tabs throttle timers hard — a minute between them, eventually. So
    // sweep again the moment the tab is looked at, rather than letting the reader
    // arrive at a field that is still blank.
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) schedule();
    });

    try {
        new MutationObserver(onMutations)
            .observe(document.documentElement, { childList: true, subtree: true });
    } catch (e) {
        // No observer: the load-time pass still ran, and an app can call
        // sednaUi.select.refresh() itself.
    }

})(window.sednaUi);

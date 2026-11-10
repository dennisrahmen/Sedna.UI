/* ── A field of checkboxes: select all, the count, an exclusive option ───────
   Three behaviours of a `.form-check-list`, each keyed by the `name` its items post,
   among the checkboxes of the same `<fieldset>` — or `<form>`, or page:

     <input type="checkbox" data-check-all="agents">
         A "select all". It ticks every item named `agents` when it is ticked and
         clears them when it is cleared, and follows them: ticked when every item that
         can be changed is, half ticked (`indeterminate`) when some are, clear when
         none is.

     <span class="form-check-list-count" data-check-count="agents"
           data-check-format="{n} of {m} selected">2 of 12 selected</span>
         The count, in the app's words: `{n}` is how many are ticked and `{m}` how many
         there are. Render the first value yourself; it is kept from then on.

     <input type="checkbox" name="agents" value="none" data-check-exclusive>
         An option that excludes the rest — "None of these" after a `.form-check-or`.
         Ticking it clears every item named `agents`; ticking any of them clears it.

   A box this changes gets a bubbling `change`, so a form's own handler and a Blazor
   binding see it as they would a click. A disabled box, and one with
   `aria-readonly="true"`, is never changed. A value set without an event — a Blazor
   render writing `checked` — is followed at the next change, `reset` or render that
   adds markup, or by calling `sednaUi.checks.refresh()`.

   An app that renders the "select all" state itself writes `aria-checked` on it
   instead — "mixed", "true" or "false" — and leaves `data-check-all` off: the two are
   one state with two owners otherwise. The stylesheet draws "mixed" with no script;
   this part also sets the box's `indeterminate` to match, whenever the attribute is
   rendered or changes, because a native checkbox is announced from its own state and
   an `aria-checked` that disagrees with it is an error to a screen reader's checker.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var changing = false;

    function scopeOf(el) { return el.closest('fieldset, form') || document; }

    function quote(name) { return '"' + String(name).replace(/["\\]/g, '\\$&') + '"'; }

    function toArray(list) { return Array.prototype.slice.call(list); }

    function named(scope, name) {
        return toArray(scope.querySelectorAll('input[type="checkbox"][name=' + quote(name) + ']:not([data-check-all])'));
    }

    function items(scope, name) {
        return named(scope, name).filter(function (b) { return !b.hasAttribute('data-check-exclusive'); });
    }

    function exclusives(scope, name) {
        return named(scope, name).filter(function (b) { return b.hasAttribute('data-check-exclusive'); });
    }

    function changeable(box) { return !box.disabled && box.getAttribute('aria-readonly') !== 'true'; }

    /* Sets a box and says so, as a click would. */
    function set(box, on) {
        if (box.checked === on || !changeable(box)) return;
        box.checked = on;
        changing = true;
        try { box.dispatchEvent(new Event('change', { bubbles: true })); }
        finally { changing = false; }
    }

    /* Writes the count into the text node already there, so a framework that rendered
       it keeps the node it knows. */
    function write(el, text) {
        if (el.textContent === text) return;
        var node = el.firstChild;
        if (node && node.nodeType === 3 && !node.nextSibling) node.nodeValue = text;
        else el.textContent = text;
    }

    function sync(scope, name) {
        var list = items(scope, name);
        var open = list.filter(changeable);
        var ticked = list.filter(function (b) { return b.checked; }).length;
        var openTicked = open.filter(function (b) { return b.checked; }).length;
        var every = open.length > 0 && openTicked === open.length;

        toArray(scope.querySelectorAll('input[type="checkbox"][data-check-all=' + quote(name) + ']')).forEach(function (all) {
            all.checked = every;
            all.indeterminate = !every && ticked > 0;
        });
        toArray(scope.querySelectorAll('[data-check-count=' + quote(name) + ']')).forEach(function (count) {
            var format = count.getAttribute('data-check-format');
            if (format === null) return;
            write(count, format.replace(/\{n\}/g, String(ticked)).replace(/\{m\}/g, String(list.length)));
        });
    }

    /* A native box's own state, from the `aria-checked` the app rendered on it. */
    function mirror(box) {
        var mixed = box.getAttribute('aria-checked') === 'mixed';
        if (box.indeterminate !== mixed) box.indeterminate = mixed;
    }

    function refresh(root) {
        toArray((root || document).querySelectorAll('input[type="checkbox"][aria-checked]')).forEach(mirror);
        var seen = [];
        toArray((root || document).querySelectorAll('[data-check-all], [data-check-count]')).forEach(function (el) {
            var name = el.getAttribute('data-check-all') || el.getAttribute('data-check-count');
            if (!name) return;
            var scope = scopeOf(el);
            for (var i = 0; i < seen.length; i++) if (seen[i].scope === scope && seen[i].name === name) return;
            seen.push({ scope: scope, name: name });
            sync(scope, name);
        });
    }

    ui.checks = {
        /* Brings every "select all" and count in line with its items, for a page that
           changed them some way this part did not see. */
        refresh: function () { refresh(document); }
    };

    document.addEventListener('change', function (e) {
        if (changing) return;
        var box = e.target;
        if (!(box instanceof HTMLInputElement) || box.type !== 'checkbox') return;
        var scope = scopeOf(box);

        var all = box.getAttribute('data-check-all');
        if (all) {
            items(scope, all).forEach(function (b) { set(b, box.checked); });
            if (box.checked) exclusives(scope, all).forEach(function (b) { set(b, false); });
            sync(scope, all);
            return;
        }
        if (!box.name) return;
        if (box.checked) {
            var others = box.hasAttribute('data-check-exclusive') ? items(scope, box.name) : exclusives(scope, box.name);
            others.forEach(function (b) { if (b !== box) set(b, false); });
        }
        sync(scope, box.name);
    });

    var queued = false;
    function soon() {
        if (queued) return;
        queued = true;
        (window.queueMicrotask || setTimeout)(function () { queued = false; refresh(document); });
    }

    function init() {
        refresh(document);
        document.addEventListener('reset', function () { setTimeout(soon, 0); }, true);
        if (typeof MutationObserver === 'function') {
            new MutationObserver(function (records) {
                var added = false;
                for (var i = 0; i < records.length; i++) {
                    var r = records[i];
                    if (r.type === 'attributes') {
                        if (r.target instanceof HTMLInputElement && r.target.type === 'checkbox') mirror(r.target);
                    } else if (r.addedNodes.length || r.removedNodes.length) added = true;
                }
                if (added) soon();
            }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-checked'] });
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

})(window.sednaUi);

/* Catalogue chrome — READ ONLY.
   ───────────────────────────────────────────────────────────────────────────
   This file NEVER writes to the DOM. Blazor owns the document under global
   interactivity, and anything this mutated would be reverted the next time the
   subtree re-rendered — silently, and only sometimes. So every function here
   returns data, C# renders it, and the one exception below moves focus, which is
   not DOM state.

   The catalogue's shell, examples, code blocks and toggles are all Blazor, and the
   topbar search is the library's own script. What is left is the handful of things
   only the browser knows: computed token values, and what the CSS parser actually
   loaded.

   Nothing here is part of Sedna.UI. The library's own script is separate and
   loaded first.
   ─────────────────────────────────────────────────────────────────────────── */
window.sednaUiCatalogue = (function () {

    /* Computed values for a list of token names, in the theme currently applied.
       Read from the root element rather than from the stylesheet text: a token can
       be remapped by [data-theme], [data-cvd], a media query or an app override,
       and only the browser knows which won. */
    function readTokenValues(names) {
        var computed = getComputedStyle(document.documentElement);
        var out = {};
        for (var i = 0; i < names.length; i++) {
            out[names[i]] = computed.getPropertyValue(names[i]).trim();
        }
        return out;
    }

    /* Walks a stylesheet's rules, including the ones nested inside @layer, @media
       and @supports blocks. Without the recursion this reports almost nothing: the
       whole shipped sheet is inside @layer blocks. */
    function eachRule(rules, visit) {
        for (var i = 0; i < rules.length; i++) {
            var rule = rules[i];
            visit(rule);
            if (rule.cssRules) eachRule(rule.cssRules, visit);
        }
    }

    function sheetFor(fragment) {
        var sheets = document.styleSheets;
        for (var i = 0; i < sheets.length; i++) {
            if (sheets[i].href && sheets[i].href.indexOf(fragment) >= 0) return sheets[i];
        }
        return null;
    }

    /* What the browser's own CSS parser says the stylesheet declares.

       This is the landing page's figures, and it is deliberately a THIRD
       implementation: build/css-inventory.sh reads the file with sed, the tests
       read it with .NET regex, and this asks the engine that actually parses it.
       Every one of those three figures has been publicly wrong at some point while
       a single implementation agreed with itself.

       One class of bug cannot occur on this side: @layer names live on
       CSSLayerBlockRule.name and never appear in selectorText, so the "sedna.paint
       reads as .paint" phantom is structurally impossible here. */
    function readInventory() {
        var tokens = {}, classes = {}, icons = {};
        var CLASS = /\.(-?[a-zA-Z][a-zA-Z0-9-]*)/g;

        var library = sheetFor('Sedna.UI.css');
        if (library) {
            eachRule(library.cssRules, function (rule) {
                if (!rule.style || !rule.selectorText) return;
                for (var i = 0; i < rule.style.length; i++) {
                    var name = rule.style[i];
                    if (name.indexOf('--') === 0) tokens[name] = true;
                }
                var match;
                while ((match = CLASS.exec(rule.selectorText)) !== null) {
                    classes[match[1]] = true;
                }
            });
        }

        // Glyph-bearing classes only. Counting every .ri-* selector also counts the
        // sizing utilities — .ri-lg, .ri-fw, .ri-2x — which are not icons, and is
        // how the landing page came to advertise 3,245 of them.
        //
        // `::?before`, not `:before`. The vendored file writes the legacy one-colon
        // form, and the CSSOM re-serialises it as two — so a regex copied from the
        // .NET side, which reads the file's own bytes, matches nothing here and
        // reports zero icons. The two implementations are looking at genuinely
        // different text, which is the reason for having both.
        var GLYPH = /\.(ri-[a-z0-9-]+)::?before/g;
        var iconSheet = sheetFor('remixicon.css');
        if (iconSheet) {
            eachRule(iconSheet.cssRules, function (rule) {
                if (!rule.selectorText) return;
                var match;
                while ((match = GLYPH.exec(rule.selectorText)) !== null) {
                    icons[match[1]] = true;
                }
            });
        }

        return {
            tokens: Object.keys(tokens).length,
            classes: Object.keys(classes).length,
            icons: Object.keys(icons).length
        };
    }

    return {
        readTokenValues: readTokenValues,
        readInventory: readInventory
    };

})();

/* "/" focuses the topbar search. A document-level key handler is not something
   Blazor offers, and focusing an element is not a DOM mutation, so this one stays
   here. Everything the box then does is sednaUi.search.

   Guarded on `e.target instanceof Element`: a keydown dispatched at `document`
   has no closest(). */
document.addEventListener('keydown', function (e) {
    if (e.key !== '/' || e.ctrlKey || e.metaKey || e.altKey) return;
    if (!(e.target instanceof Element)) return;
    if (e.target.closest('input, textarea, select, [contenteditable]')) return;

    var box = document.getElementById('cat-search');
    if (!box) return;
    e.preventDefault();
    box.focus();
});

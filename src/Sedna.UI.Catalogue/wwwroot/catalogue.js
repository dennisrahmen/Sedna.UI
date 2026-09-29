/* Catalogue chrome — READ ONLY.
   ───────────────────────────────────────────────────────────────────────────
   This file NEVER writes to the DOM. Blazor owns the document under global
   interactivity, and anything this mutated would be reverted the next time the
   subtree re-rendered — silently, and only sometimes. So every function here
   returns data, C# renders it, and the two handlers below are the exceptions: one
   moves focus and one cancels a click, and neither is DOM state.

   The catalogue's shell, examples, code blocks and toggles are all Blazor, and the
   topbar search is the library's own script. What is left is the one thing only
   the browser knows: the computed value of a token.

   Nothing here is part of Sedna.UI. The library's own script is separate and
   loaded first.
   ─────────────────────────────────────────────────────────────────────────── */
window.sednaUiCatalogue = (function () {

    /* Computed values for a list of token names, in the theme currently applied.
       Read from the root element rather than from the stylesheet text: a token can
       be remapped by [data-theme], [data-variant], [data-cvd], a media query or an
       app override, and only the browser knows which won. */
    function readTokenValues(names) {
        var computed = getComputedStyle(document.documentElement);
        var out = {};
        for (var i = 0; i < names.length; i++) {
            out[names[i]] = computed.getPropertyValue(names[i]).trim();
        }
        return out;
    }

    return {
        readTokenValues: readTokenValues
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

/* A placeholder link in a demo goes nowhere. `href="#"` is how an example says
   "some link", and under the host page's <base href="/"> it means the landing page,
   so clicking a demo's nav item or breadcrumb left the page being read. Blazor
   ignores a click something has already cancelled, and this file loads before it. */
document.addEventListener('click', function (e) {
    if (!(e.target instanceof Element)) return;
    if (e.target.closest('.ex-demo a[href="#"]')) e.preventDefault();
});

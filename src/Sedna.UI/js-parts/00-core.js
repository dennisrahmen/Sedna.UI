/* Sedna.UI — shared browser behaviour.
   ───────────────────────────────────────────────────────────────────────────
   Load at the END of <body>:

     <script src="_content/Sedna.UI/js/Sedna.UI.js"></script>

   configure() is optional:

     <script>sednaUi.configure({ notifyIcon: '/images/logo.png' });</script>

   Everything here is generic UI behaviour. App-specific interop stays in the
   app's own script — do not grow this file with business logic.

   The global is `sednaUi` (the JS-identifier form of the package name; a
   single global cannot contain the dot).
   ─────────────────────────────────────────────────────────────────────────── */
window.sednaUi = window.sednaUi || {};

(function (ui) {

    var config = {
        // localStorage key prefix. localStorage is origin-scoped, so apps on
        // separate domains cannot collide and this needs no changing. Override it
        // only when two apps share one origin under different paths — and then set
        // the same value in data-prefix on the boot script.
        storagePrefix: 'sedna.',
        // Icon used for desktop notifications. null = browser default.
        notifyIcon: null,
        // Also mirror the language into a "<prefix>lang" cookie, so a server-
        // rendered app can prerender in the chosen language.
        langCookie: false
    };

    function key(k) { return config.storagePrefix + k; }

    function readRaw(k) {
        try { return localStorage.getItem(k); } catch (e) { return null; }
    }

    /* How well `needle` matches `haystack`. Returns a score — higher is better —
       or -1 for no match. `penalty` demotes a hit in a secondary field, so a
       command found by its keywords never outranks one found by its name.

       A hand-rolled subsequence matcher, about thirty lines. No fuse.js: this
       package loads nothing at runtime, and a fuzzy matcher good enough for a
       short label is smaller than the argument for taking a dependency.

       Ranking, in the order it matters:
         1. a prefix match            — you typed the start of the name
         2. a word-start match        — "ai" finds "Approve Item"
         3. a contiguous run inside the string
         4. any subsequence, penalised by how spread out it is

       Lives here because the palette and the header search must rank the same
       way. Two matchers would drift, and the second one would be discovered by
       someone finding the same query ordered differently in two places. */
    function score(needle, haystack, penalty) {
        if (!needle) return 1;
        if (!haystack) return -1;

        var n = needle.toLowerCase();
        var h = haystack.toLowerCase();

        var idx = h.indexOf(n);
        if (idx === 0) return 1000 - penalty;                       // prefix
        if (idx > 0) {
            // A run that starts a word beats one buried inside it.
            var wordStart = /[\s\-_/.]/.test(h[idx - 1]);
            return (wordStart ? 800 : 600) - idx - penalty;
        }

        // Subsequence. Track the span it occupies: a match spread across the whole
        // string is a worse match than a tight one, which is what stops "ae"
        // ranking "Approve … escalate" above "Archive entry".
        var first = -1, last = -1, hi = 0;
        for (var ni = 0; ni < n.length; ni++) {
            var found = h.indexOf(n[ni], hi);
            if (found < 0) return -1;
            if (first < 0) first = found;
            last = found;
            hi = found + 1;
        }
        var span = last - first + 1;
        return 400 - (span - n.length) - first - penalty;
    }

    // Shared internals for the other parts. The underscore means exactly one
    // thing: NOT part of the public contract. Nothing outside js-parts/ may read
    // it, and it may change in a patch release. Everything an app is allowed to
    // touch is a named member on `ui` itself.
    ui._ = { config: config, key: key, readRaw: readRaw, score: score };

    ui.configure = function (opts) {
        if (!opts) return;
        Object.keys(opts).forEach(function (k) {
            if (k in config) config[k] = opts[k];
        });
        // Guarded so core-plus-nothing still works for anyone using the parts
        // à la carte; in the shipped bundle settings is always present.
        if (ui.settings) ui.settings.apply();
    };

})(window.sednaUi);

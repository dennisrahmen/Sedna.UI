/* Sedna.UI — pre-paint theme boot.
   ───────────────────────────────────────────────────────────────────────────
   Load this in <head>, BEFORE the first paint, so a light-theme or compact-
   density user never sees a dark flash:

     <script src="_content/Sedna.UI/js/Sedna.UI.boot.js"></script>

   Both attributes below are optional.

   It only stamps data-theme / data-cvd / data-density / dir / lang on <html> from
   localStorage. The main Sedna.UI.js (end of <body>) keeps them current
   afterwards, and both default to the same prefix, so neither needs configuring
   unless two apps share one origin.

   dir and lang are stamped from a STORED choice only. Both are attributes the host
   page declares for itself, and deriving either from the browser would overwrite
   what the document says about itself.

   It ships as a file rather than a snippet to copy into every app on purpose:
   a copied snippet is drift waiting to happen.

   The system theme is resolved HERE, into the data-theme attribute — never as a
   `@media (prefers-color-scheme)` block in the stylesheet. A media block would
   match independently of data-theme, so every app that rebrands (all of them do:
   the light theme needs readable-on-white variants of the brand tokens) would find
   its own `[data-theme="light"]` block no longer applying while the library's media
   block did. One attribute, one source of truth, and the app's own light block keeps
   working unchanged.

   Precedence: a stored choice always wins. `prefers-color-scheme` decides only for
   somebody who has never chosen, which is what "system" means — and choosing light
   on a machine set to dark must not be silently reverted on the next load.

   data-prefix       localStorage key prefix. Default "sedna.". Only needed when
                     two apps share an origin; must match storagePrefix.
   data-lang-cookie  "true" to also write a "<prefix>lang" cookie, so a
                     server-rendered app can prerender in the chosen language
                     instead of flashing the default one.
   data-theme-default  "dark" (the default), "light", or "system" to follow
                     prefers-color-scheme until the user chooses. */
(function () {
    var el = document.currentScript;
    var prefix = (el && el.dataset.prefix) || 'sedna.';
    var wantCookie = !!(el && el.dataset.langCookie === 'true');
    var fallback = (el && el.dataset.themeDefault) || 'dark';

    try {
        var get = function (k) { return localStorage.getItem(prefix + k); };
        var root = document.documentElement;

        var stored = get('theme');
        var theme;
        if (stored === 'light' || stored === 'dark') {
            theme = stored;
        } else if (fallback === 'system') {
            // matchMedia is guarded: it is missing in some embedded webviews, and an
            // exception here would leave the page with no data-theme at all.
            theme = (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches)
                ? 'light' : 'dark';
        } else {
            theme = fallback === 'light' ? 'light' : 'dark';
        }
        root.setAttribute('data-theme', theme);
        if (get('cvd') === '1') root.setAttribute('data-cvd', '1');
        if (get('density') === 'compact') root.setAttribute('data-density', 'compact');

        // Before first paint or not at all: a document that paints left-to-right and
        // then mirrors is a worse flash than a theme change, because every box moves.
        var dir = get('dir');
        if (dir === 'rtl' || dir === 'ltr') root.dir = dir;

        // A STORED choice only. With none, <html lang> keeps whatever the host page
        // declared, and that is the correct answer: navigator.language is the
        // language of the reader's browser UI, not the language this document is
        // written in. Deriving one from the other relabels an English page as German
        // for every screen reader, translation prompt and search crawler the moment
        // somebody visits with a German browser — and it did.
        var lang = get('lang');
        if (lang) {
            root.lang = lang;
            if (wantCookie) {
                document.cookie = prefix + 'lang=' + lang + ';path=/;max-age=31536000;SameSite=Lax';
            }
        }
    } catch (e) {
        /* storage blocked — first paint falls back to the dark default */
    }
})();

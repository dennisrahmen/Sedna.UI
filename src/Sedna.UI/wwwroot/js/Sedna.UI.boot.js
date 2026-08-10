/* Sedna.UI — pre-paint theme boot.
   ───────────────────────────────────────────────────────────────────────────
   Load this in <head>, BEFORE the first paint, so a light-variant or compact-
   density user never sees a dark flash:

     <script src="_content/Sedna.UI/js/Sedna.UI.boot.js"></script>

   All the attributes below are optional.

   Two orthogonal attributes carry the theme: data-theme is WHICH theme (any name;
   "sedna" absent anything stored) and data-variant is dark or light — what a
   reader toggles. It also stamps data-cvd / data-density / dir / lang on <html>
   from localStorage. The main Sedna.UI.js (end of <body>) keeps them current
   afterwards, and both default to the same prefix, so neither needs configuring
   unless two apps share one origin.

   dir and lang are stamped from a STORED choice only. Both are attributes the host
   page declares for itself, and deriving either from the browser would overwrite
   what the document says about itself.

   It ships as a file rather than a snippet to copy into every app on purpose:
   a copied snippet is drift waiting to happen.

   The system variant is resolved HERE, into the data-variant attribute — never as
   a `@media (prefers-color-scheme)` block in the stylesheet. A media block would
   match independently of data-variant, so every app that rebrands (all of them do:
   the light variant needs readable-on-white variants of the brand tokens) would
   find its own `[data-variant="light"]` block no longer applying while the
   library's media block did. One attribute, one source of truth, and the app's own
   light block keeps working unchanged.

   Precedence: a stored choice always wins. `prefers-color-scheme` decides only for
   somebody who has never chosen — a stored "system" counts as never having chosen
   a side, so it re-checks the OS every load — and choosing light on a machine set
   to dark must not be silently reverted on the next load.

   data-prefix         localStorage key prefix. Default "sedna.". Only needed when
                       two apps share an origin; must match storagePrefix.
   data-lang-cookie    "true" to also write a "<prefix>lang" cookie, so a
                       server-rendered app can prerender in the chosen language
                       instead of flashing the default one.
   data-variant-default  "dark" (the default), "light", or "system" to follow
                       prefers-color-scheme until the user chooses. */
(function () {
    var el = document.currentScript;
    var prefix = (el && el.dataset.prefix) || 'sedna.';
    var wantCookie = !!(el && el.dataset.langCookie === 'true');
    var fallback = (el && el.dataset.variantDefault) || 'dark';

    try {
        var get = function (k) { return localStorage.getItem(prefix + k); };
        var root = document.documentElement;

        root.setAttribute('data-theme', get('theme') || 'sedna');

        var storedVariant = get('variant');
        // A recognised stored MODE (including "system") always wins over the
        // script tag's own default — that default only ever governs someone who
        // has stored nothing at all.
        var mode = (storedVariant === 'dark' || storedVariant === 'light' || storedVariant === 'system')
            ? storedVariant : fallback;
        var variant;
        if (mode === 'system') {
            // matchMedia is guarded: it is missing in some embedded webviews, and an
            // exception here would leave the page with no data-variant at all.
            variant = (window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches)
                ? 'light' : 'dark';
        } else {
            variant = mode === 'light' ? 'light' : 'dark';
        }
        root.setAttribute('data-variant', variant);
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

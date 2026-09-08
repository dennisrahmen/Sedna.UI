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
   data-tz-cookie      A cookie NAME. Writes the browser's IANA time zone to it
                       before first paint, so a server-rendered app can render UTC
                       instants on the reader's own clock. Only the browser knows
                       the zone, and in Blazor Server with prerendering off the last
                       component that can read a cookie is App.razor — so an app
                       without this ships an inline script of its own, which is the
                       one thing the consuming rules say it should not have.

                       It is a name rather than "true" because this cookie is read
                       by the app's own code, not by the library, and the app names
                       what it reads. Unlike the language cookie it takes no prefix
                       for the same reason.
   data-variant-default  "dark" (the default), "light", or "system" to follow
                       prefers-color-scheme until the user chooses. */
(function () {
    var el = document.currentScript;
    var prefix = (el && el.dataset.prefix) || 'sedna.';
    var wantCookie = !!(el && el.dataset.langCookie === 'true');
    var tzCookie = (el && el.dataset.tzCookie) || '';
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

    /* The browser's time zone, in its own try so a blocked localStorage above does
       not take it down with it — the zone comes from Intl, not from storage.

       Written only when it differs from the cookie already there. A cookie write is
       cheap, but rewriting an unchanged value on every load puts a Set-Cookie-sized
       header on every request for no reason, and a value that changes on every load
       is one an app cannot cache a formatter against.

       No reload, no event, no C# surface: an app decides what to do with the value.
       Note that a FIRST request carries no cookie yet — the app needs a configured
       fallback zone for that one render, and must not reload to get the cookie,
       because the next navigation already carries it. */
    if (tzCookie) {
        try {
            var zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
            if (zone) {
                // The leading "; " anchors the name, so a cookie whose name merely
                // ends with this one's does not match. A cookie VALUE cannot contain
                // a semicolon, so nothing else can forge the probe either.
                var probe = '; ' + tzCookie + '=' + zone;
                if (('; ' + document.cookie).indexOf(probe) === -1) {
                    document.cookie = tzCookie + '=' + zone + ';path=/;max-age=31536000;SameSite=Lax';
                }
            }
        } catch (e) {
            /* no Intl, or cookies blocked — the app's fallback zone stands */
        }
    }
})();

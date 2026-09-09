/* ── Theme / accessibility settings ──────────────────────────────────────────
   localStorage is the source of truth; the data-theme / data-variant / data-cvd /
   data-density attributes and the dir attribute on <html> drive the CSS. The boot
   script applies them before first paint; save() keeps them applied.

   data-theme and data-variant are ALWAYS written, never absent — consuming apps
   brand the light palette with `:root[data-variant="light"]`, so that selector has
   to match whenever the light palette is in use. data-theme accepts any theme
   name; "sedna" is the fallback with nothing stored.

   "system" is a real, storable variant preference — not collapsed to dark/light on
   load — so a settings UI can show "follow system" as selected rather than
   whichever side the OS happens to be on right now. matchMedia is guarded: it is
   missing in some embedded webviews, and an exception there must not stop the
   theme applying.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var core = ui._;
    var config = core.config, key = core.key, readRaw = core.readRaw;

    function systemPrefersLight() {
        return !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches);
    }

    // Who wants to know when the applied settings change. Plain functions, so this
    // part stays framework-agnostic; the Blazor bridge is in 40-interop.js.
    var listeners = [];

    function notify() {
        var current = ui.settings.load();
        // A copy of the list: a listener that unsubscribes inside its own callback
        // would otherwise shorten the array being walked and skip the next one.
        listeners.slice().forEach(function (fn) {
            try { fn(current); } catch (e) { /* a bad listener is not the theme's problem */ }
        });
    }

    ui.settings = {
        load: function () {
            var g = function (k) { return readRaw(key(k)); };
            var v = g('variant');
            return {
                // The document's own language before the browser's: boot.js leaves
                // <html lang> alone unless a choice was stored, so reporting
                // navigator.language here would tell an app's language picker
                // something different from what the page is actually marked as.
                lang:    g('lang') || document.documentElement.lang
                             || (navigator.language || 'en').slice(0, 2).toLowerCase(),
                theme:   g('theme') || 'sedna',
                variant: (v === 'light' || v === 'dark' || v === 'system') ? v : 'dark',
                cvd:     g('cvd') === '1',
                compact: g('density') === 'compact',
                // The document's own direction when nothing is stored, for the same
                // reason as lang above: the host page is the authority until the
                // reader chooses otherwise.
                dir:     g('dir') === 'rtl' ? 'rtl' : (g('dir') === 'ltr' ? 'ltr'
                             : (document.documentElement.dir || 'ltr'))
            };
        },
        save: function (k, value) {
            try { localStorage.setItem(key(k), value); } catch (e) { /* ignore */ }
            if (k === 'lang') {
                if (config.langCookie) {
                    try {
                        document.cookie = key('lang') + '=' + value + ';path=/;max-age=31536000;SameSite=Lax';
                    } catch (e) { /* ignore */ }
                }
                document.documentElement.lang = value;
            }
            this.apply();
        },
        apply: function () {
            var g = function (k) { return readRaw(key(k)); };
            var root = document.documentElement;
            root.setAttribute('data-theme', g('theme') || 'sedna');

            var v = g('variant');
            var variant;
            if (v === 'light' || v === 'dark') variant = v;
            else if (v === 'system') variant = systemPrefersLight() ? 'light' : 'dark';
            else variant = 'dark';
            root.setAttribute('data-variant', variant);

            if (g('cvd') === '1') root.setAttribute('data-cvd', '1');
            else root.removeAttribute('data-cvd');
            if (g('density') === 'compact') root.setAttribute('data-density', 'compact');
            else root.removeAttribute('data-density');
            // Only a stored choice writes dir, and "ltr" is stored explicitly rather
            // than treated as absent — otherwise switching back would delete a dir
            // the host page set for itself. An app whose document is RTL by default
            // says so in its own markup and this leaves it alone.
            var dir = g('dir');
            if (dir === 'rtl' || dir === 'ltr') root.dir = dir;

            // Last, so a listener that reads the document sees the attributes this
            // call has already written rather than the ones it is replacing.
            notify();
        },
        // Returns its own unsubscribe function, so a caller never has to keep an id
        // or hand the same function back.
        onChange: function (fn) {
            if (typeof fn !== 'function') return function () { };
            listeners.push(fn);
            return function () {
                var at = listeners.indexOf(fn);
                if (at >= 0) listeners.splice(at, 1);
            };
        }
    };

    // Live tracking: while the stored preference is literally "system", the
    // variant follows the OS without a reload. A stored "dark" or "light" is an
    // explicit choice and must not be disturbed by this listener.
    try {
        if (window.matchMedia) {
            window.matchMedia('(prefers-color-scheme: light)').addEventListener('change', function () {
                if (readRaw(key('variant')) === 'system') ui.settings.apply();
            });
        }
    } catch (e) { /* ignore */ }

})(window.sednaUi);

/* ── Theme / accessibility settings ──────────────────────────────────────────
   localStorage is the source of truth; the data-theme / data-cvd / data-density
   attributes and the dir attribute on <html> drive the CSS. The boot script applies
   them before first paint; save() keeps them applied.

   data-theme is ALWAYS written, `light` or `dark`, never absent — consuming apps
   brand the light palette with `:root[data-theme="light"]`, so that selector has
   to match whenever the light palette is in use. Any future support for the OS
   preference must resolve prefers-color-scheme into this attribute here, never
   express it as a @media block, or every app's light-theme branding stops
   applying with no app edit.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var core = ui._;
    var config = core.config, key = core.key, readRaw = core.readRaw;

    ui.settings = {
        load: function () {
            var g = function (k) { return readRaw(key(k)); };
            return {
                // The document's own language before the browser's: boot.js leaves
                // <html lang> alone unless a choice was stored, so reporting
                // navigator.language here would tell an app's language picker
                // something different from what the page is actually marked as.
                lang:    g('lang') || document.documentElement.lang
                             || (navigator.language || 'en').slice(0, 2).toLowerCase(),
                theme:   g('theme') === 'light' ? 'light' : 'dark',
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
            root.setAttribute('data-theme', g('theme') === 'light' ? 'light' : 'dark');
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
        }
    };

})(window.sednaUi);

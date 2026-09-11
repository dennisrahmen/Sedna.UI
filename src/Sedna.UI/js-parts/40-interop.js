/* ── Small interop helpers ───────────────────────────────────────────────────
   Generic browser calls a Blazor component cannot make on its own. Note that
   getItem / setItem take the RAW key and do not apply the storage prefix — they
   are a plain localStorage bridge for an app's own keys, not a view onto the
   library's settings, which live under the prefix and are reached through
   sednaUi.settings.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var readRaw = ui._.readRaw;

    ui.openTab = function (url) {
        try { window.open(url, '_blank', 'noopener'); } catch (e) { /* ignore */ }
    };

    // Returns whether the copy succeeded, so the caller can toast either way.
    // Falls back to a hidden textarea where the async Clipboard API is
    // unavailable (older browsers, insecure origins).
    ui.copyText = async function (text) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(text);
                return true;
            }
        } catch (e) { /* fall through to the legacy path */ }
        try {
            var ta = document.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.focus(); ta.select();
            var ok = document.execCommand('copy');
            document.body.removeChild(ta);
            return ok;
        } catch (e) { return false; }
    };

    ui.viewportWidth = function () {
        return window.innerWidth || document.documentElement.clientWidth || 0;
    };

    /* Scrolls the frame's page column back to the top.

       `.page` is the only scroll container in the frame, so the window's own scroll
       position is always 0 and nothing the router does moves it. Navigating therefore
       leaves the new page at the previous page's offset — halfway down, on a route the
       reader has just arrived at. Call this from a LocationChanged handler.

       Falls back to the window for a page that is not inside the frame, such as a
       bare-layout sign-in screen. */
    ui.scrollPageTop = function () {
        var page = document.querySelector('.page');
        if (page) { page.scrollTop = 0; return; }
        try { window.scrollTo(0, 0); } catch (e) { /* ignore */ }
    };

    /* The browser's own IANA time zone — "Europe/Lisbon", never an offset, because an
       offset is only true until the next transition.

       Read from Intl on every call rather than stored: it is not a preference, so it
       has no key under the storage prefix and nothing to invalidate when a reader
       travels. The boot script's data-tz-cookie is the other half of the same
       question — the cookie is what a server-rendered app's FIRST render reads,
       before there is a circuit to call this from.

       null where Intl is missing or refuses, so a caller falls back to its own
       configured zone instead of to a wrong one. */
    ui.timeZone = function () {
        try { return Intl.DateTimeFormat().resolvedOptions().timeZone || null; }
        catch (e) { return null; }
    };

    ui.getItem = function (k) { return readRaw(k); };

    ui.setItem = function (k, value) {
        try { localStorage.setItem(k, value); } catch (e) { /* ignore */ }
    };

    /* ── The settings bridge ─────────────────────────────────────────────────
       sednaUi.settings.onChange takes a function, and a .NET object reference is
       not one — so the id/handle plumbing lives here rather than making
       10-settings.js know what Blazor is.

       An id rather than the reference itself, because the reference cannot be
       compared across calls: two InvokeAsync calls carrying "the same" object
       arrive as two different objects, so an unwatch keyed on it would never
       match.

       A disposed reference throws on invoke. That is the normal end of a circuit,
       not an error, so the watcher removes itself — otherwise every navigation
       away leaves a dead listener behind for the life of the page. */
    var watchers = {};
    var nextWatcher = 1;

    ui.watchSettings = function (ref) {
        var id = nextWatcher++;

        watchers[id] = ui.settings.onChange(function (settings) {
            try {
                var call = ref.invokeMethodAsync('SettingsChanged', settings);
                if (call && call.catch) call.catch(function () { ui.unwatchSettings(id); });
            } catch (e) {
                ui.unwatchSettings(id);
            }
        });

        return id;
    };

    ui.unwatchSettings = function (id) {
        var off = watchers[id];
        if (!off) return false;

        off();
        delete watchers[id];
        return true;
    };

})(window.sednaUi);

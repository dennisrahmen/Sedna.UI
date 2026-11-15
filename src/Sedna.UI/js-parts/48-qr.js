/* ── The QR code's front door (data-qr) ───────────────────────────────────────
   Every page loads this; few of them show a QR code. So this part is small and holds
   no encoder: it finds the app's `[data-qr]` elements and imports the QR module —
   Sedna.UI.qr.js, beside this file — the first time one of them is about to be seen.
   A page without a code never downloads it, and nothing is added to the host page.

     <span class="qr" data-qr="https://example.com/pair/7Q4K" aria-label="QR code to pair the device"></span>
     <button class="btn" type="button" data-qr-download="png" data-qr-for="pair">Save as PNG</button>

   Finding codes, starting one when it comes within a screen of the viewport (or at
   once with `data-qr-eager`), waiting on a prerendered Blazor page and disposing one
   whose element has gone are the shared surface loader's (42-surfaces.js). This part
   gives a code its role the moment it is found, so the app's `aria-label` names an
   image even before the drawing arrives — the name itself is always the app's — and
   owns the buttons that save one:

     * `data-qr-download="svg|png"` on a button or a link the app wrote, delegated from
       document so one rendered later needs no wiring. Which code: the one named by
       `data-qr-for` on it or on a container around it, else the one in the nearest
       `.qr-figure` or `[data-qr-frame]`, else the one it sits inside. `data-qr-size` is
       the picture's side in pixels (1024 by default) and `data-qr-name` the file's name
       without its extension (`qr-code`).
     * `sednaUi.qr.download(target, { format, size, name })` for an app's own control.
       The handle (`get`) also has `toSvg()` and `toBlob()` — the module's, once the
       code has started.

   Everything else is the module's. A save says how it went with the plain events
   `sedna-qr-download` and `sedna-qr-error`, which bubble from the code.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var SELECTOR = '[data-qr]';

    var codes = ui._.surface({
        name: 'qr',
        selector: SELECTOR,
        module: 'Sedna.UI.qr.js',
        eager: 'data-qr-eager',
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'img');
        }
    });

    function codeOf(control) {
        var named = control.closest('[data-qr-for]');
        if (named && named.getAttribute('data-qr-for')) {
            var target = document.getElementById(named.getAttribute('data-qr-for'));
            if (target && target.matches(SELECTOR)) return target;
        }
        var frame = control.closest('.qr-figure, [data-qr-frame]');
        if (frame) {
            var inside = frame.querySelector(SELECTOR);
            if (inside) return inside;
        }
        return control.closest(SELECTOR);
    }

    function download(target, options) {
        return codes.get(target).then(function (handle) { return handle.download(options || {}); });
    }

    ui.qr = {
        /* The code's handle — an element, or its id — starting it if it has not started.
           Its members are listed in docs/architecture.md. */
        get: codes.get,

        /* Saves a code as a file: `{ format: 'svg' | 'png', size: pixels, name }`. A promise
           of `{ format, size, name }` with the name the file was saved under. */
        download: download,

        /* Starts every code in `root` (the document by default) at once. A promise of how many. */
        init: codes.init
    };

    document.addEventListener('click', function (e) {
        var control = e.target && e.target.closest ? e.target.closest('[data-qr-download]') : null;
        if (!control || control.hasAttribute('disabled') || control.getAttribute('aria-disabled') === 'true') return;
        var target = codeOf(control);
        if (!target) return;
        e.preventDefault();
        var size = parseInt(control.getAttribute('data-qr-size'), 10);
        download(target, {
            format: control.getAttribute('data-qr-download'),
            size: size > 0 ? size : undefined,
            name: control.getAttribute('data-qr-name') || undefined
        }).catch(function (err) {
            // The module has already said so on the code, as `sedna-qr-error`.
            try { console.error('Sedna.UI qr:', err); } catch (x) { /* ignore */ }
        });
    });

})(window.sednaUi);

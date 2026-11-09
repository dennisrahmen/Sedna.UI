/* ── The QR code's front door (data-qr) ───────────────────────────────────────
   Every page loads this; few of them show a QR code. So this part is small and holds
   no encoder: it finds the app's `[data-qr]` elements and imports the QR module —
   Sedna.UI.qr.js, beside this file — the first time one of them is about to be seen.
   A page without a code never downloads it, and nothing is added to the host page.

     <span class="qr" data-qr="https://example.com/pair/7Q4K" aria-label="QR code to pair the device"></span>

   Finding codes, starting one when it comes within a screen of the viewport (or at
   once with `data-qr-eager`), waiting on a prerendered Blazor page and disposing one
   whose element has gone are the shared surface loader's (42-surfaces.js). This part
   gives a code its role the moment it is found, so the app's `aria-label` names an
   image even before the drawing arrives. The name itself is always the app's.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    ui._.surface({
        name: 'qr',
        selector: '[data-qr]',
        module: 'Sedna.UI.qr.js',
        eager: 'data-qr-eager',
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'img');
        }
    });

})(window.sednaUi);

/* ── The signature pad's front door (data-signature) ──────────────────────────
   Every page loads this; few of them show a signature pad. So this part is small and
   draws nothing: it finds the app's `[data-signature]` elements and imports the pad
   module — Sedna.UI.signature.js, beside this file — the first time one of them is
   about to be seen. A page without a pad never downloads it, and nothing is added to
   the host page.

     <div class="form-signature" data-signature>
       <div class="form-signature-pad" data-signature-pad aria-label="Signature, drawn with a mouse, pen or finger">
         <span class="form-signature-hint">Sign here</span>
       </div>
       <input type="hidden" name="signature" data-signature-value>
       <div class="form-signature-actions">
         <button class="btn btn--sm btn--ghost" type="button" data-signature-undo>Undo</button>
         <button class="btn btn--sm btn--ghost" type="button" data-signature-clear>Clear</button>
       </div>
     </div>

   Finding pads, starting one when it comes within a screen of the viewport (or at once
   with `data-signature-eager`), waiting on a prerendered Blazor page and disposing one
   whose element has gone are the shared surface loader's (42-surfaces.js). This part
   gives the pad its role the moment it is found, so the app's `aria-label` names an
   image even before the canvas arrives. The name itself is always the app's.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    ui._.surface({
        name: 'signature',
        selector: '[data-signature]',
        module: 'Sedna.UI.signature.js',
        eager: 'data-signature-eager',
        found: function (el) {
            var pad = el.querySelector('[data-signature-pad]');
            if (pad && !pad.hasAttribute('role')) pad.setAttribute('role', 'img');
        }
    });

})(window.sednaUi);

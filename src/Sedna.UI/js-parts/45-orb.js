/* ── The orb's front door (data-orb) ───────────────────────────────────────────
   Every page loads this; few of them show an orb. So this part is small and draws
   nothing: it finds the app's `[data-orb]` elements and imports the orb module —
   Sedna.UI.orb.js, beside this file — the first time one of them is about to be seen.
   A page without an orb never downloads it, and nothing is added to the host page.

     <span class="orb" data-orb="thinking" role="img" aria-label="Thinking"></span>

   Finding orbs, starting one when it comes within a screen of the viewport (or at once
   with `data-orb-eager`), waiting on a prerendered Blazor page and disposing one whose
   element has gone are the shared surface loader's (42-surfaces.js). This part gives an
   orb its role the moment it is found, so the app's `aria-label` names an image even
   before the drawing arrives. The name itself is always the app's.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    ui._.surface({
        name: 'orb',
        selector: '[data-orb]',
        module: 'Sedna.UI.orb.js',
        eager: 'data-orb-eager',
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'img');
        }
    });

})(window.sednaUi);

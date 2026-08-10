/* ── Declarative copy-to-clipboard ───────────────────────────────────────────
   Put data-copy on a button and the click is handled for you:

     data-copy="literal text"      copies that text
     data-copy-target="#sel"       copies that element's textContent
     data-copy-target              (empty) copies the nearest .code-block's <pre>

   Delegated from document, so a button rendered by a later Blazor render works with
   no wiring — and nothing has to be re-bound on every render, which is how per-
   element handlers leak.

   The confirmation is swapped into the button and put back after 1.4s. The original
   HTML is stashed on the element rather than in a closure, so two rapid clicks
   cannot restore a "Copied" label as if it were the original.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var RESTORE_MS = 1400;

    function textFor(btn) {
        if (btn.hasAttribute('data-copy')) return btn.getAttribute('data-copy') || '';

        var sel = btn.getAttribute('data-copy-target');
        var node = sel
            ? document.querySelector(sel)
            // The <pre> of the code block this button belongs to.
            : (btn.closest('.code-block') || document).querySelector('pre');

        return node ? (node.innerText || node.textContent || '') : '';
    }

    function flash(btn, ok) {
        // Only stash on the first click; a second click mid-flash must not stash
        // the confirmation as the thing to restore.
        if (btn.dataset.copyOriginal === undefined) {
            btn.dataset.copyOriginal = btn.innerHTML;
        }
        clearTimeout(+btn.dataset.copyTimer || 0);

        btn.innerHTML = ok
            ? '<i class="ri-check-line"></i><span>Copied</span>'
            : '<i class="ri-error-warning-line"></i><span>Copy failed</span>';

        btn.dataset.copyTimer = setTimeout(function () {
            btn.innerHTML = btn.dataset.copyOriginal;
            delete btn.dataset.copyOriginal;
            delete btn.dataset.copyTimer;
        }, RESTORE_MS);
    }

    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-copy], [data-copy-target]');
        if (!btn) return;

        var text = textFor(btn);
        if (!text) return;

        e.preventDefault();
        // copyText resolves false rather than throwing, so the button always
        // reports what actually happened.
        ui.copyText(text).then(function (ok) { flash(btn, ok); });
    });

})(window.sednaUi);

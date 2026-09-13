/* ── Declarative copy-to-clipboard ───────────────────────────────────────────
   Put data-copy on a button and the click is handled for you:

     data-copy="literal text"      copies that text
     data-copy-target="#sel"       copies that element's textContent
     data-copy-target              (empty) copies the nearest .code-block's <pre>

   Delegated from document, so a button rendered by a later Blazor render works with
   no wiring — and nothing has to be re-bound on every render, which is how per-
   element handlers leak.

   The outcome is an attribute, never a rewrite of the button: `data-copied="ok"` or
   `data-copied="failed"` for 1.4s. The words and the icons are the app's, marked with
   what the stylesheet shows when:

     <button class="btn btn-sm" type="button" data-copy="…">
       <i class="ri-file-copy-line" data-copied-hide></i>
       <i class="ri-check-line" data-copied-show="ok"></i>
       <span data-copied-hide>Copy</span>
       <span data-copied-show="ok">Copied</span>
       <span data-copied-show="failed">Copy failed</span>
     </button>

   Rewriting the button's content was drawing markup in the app's own element — in
   English, and in a subtree a framework owns and can revert mid-flash.
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
        // One timer per button: a second click mid-flash restarts the window rather
        // than letting the first timer end the second flash early.
        clearTimeout(+btn.dataset.copyTimer || 0);
        btn.setAttribute('data-copied', ok ? 'ok' : 'failed');

        btn.dataset.copyTimer = setTimeout(function () {
            btn.removeAttribute('data-copied');
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

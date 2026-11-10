/* ── A dimmed dependent block is inert ───────────────────────────────────────
   `.choice-dependent--dim` after a `.form-check` or `.switch` fades while its option
   is off, and the stylesheet makes it inert with `interactivity: inert`. Safari has no
   such property, so a block that looks unavailable would still take focus and typing
   there. This sets the `inert` attribute to match, in every engine, so the block
   behaves the same everywhere:

     <label class="switch"><input type="checkbox" checked> Send a digest</label>
     <div class="choice-dependent choice-dependent--dim">…</div>

   `.choice-dependent--off` turns it round, and is inert while its option is ON. A
   `.form-error` or `.form-warning` between the option and the block keeps the pair.

   It follows the option on every `change` and `reset`, and after a render adds or
   swaps markup. A value set by script without either — a Blazor binding writing
   `checked` — is picked up at the next of those; the fade itself never waits, being
   CSS. An `inert` the app set itself is never touched: only what this part added is
   marked `data-sedna-inert` and taken away again.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var SELECTOR = '.choice-dependent--dim';
    var MARK = 'data-sedna-inert';

    function controlOf(block) {
        var host = block.previousElementSibling;
        if (host && host.matches('.form-error, .form-warning')) host = host.previousElementSibling;
        if (!host || !host.matches('.form-check, .switch')) return null;
        for (var i = 0; i < host.children.length; i++) {
            var c = host.children[i];
            if (c.tagName === 'INPUT' && (c.type === 'checkbox' || c.type === 'radio')) return c;
        }
        return null;
    }

    function sync(block) {
        var input = controlOf(block);
        if (!input) return;
        var off = block.classList.contains('choice-dependent--off') ? input.checked : !input.checked;
        if (off && !block.hasAttribute('inert')) {
            block.setAttribute('inert', '');
            block.setAttribute(MARK, '');
        } else if (!off && block.hasAttribute(MARK)) {
            block.removeAttribute('inert');
            block.removeAttribute(MARK);
        }
    }

    function syncAll(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var blocks = scope.querySelectorAll(SELECTOR);
        for (var i = 0; i < blocks.length; i++) sync(blocks[i]);
    }

    var queued = false;
    function soon() {
        if (queued) return;
        queued = true;
        // A radio's siblings change with it, so every block is looked at once the
        // event or the render has finished, rather than the one next to the target.
        (window.queueMicrotask || setTimeout)(function () { queued = false; syncAll(document); });
    }

    function init() {
        syncAll(document);
        document.addEventListener('change', soon, true);
        document.addEventListener('reset', function () { setTimeout(soon, 0); }, true);
        if (typeof MutationObserver === 'function') {
            new MutationObserver(soon).observe(document.documentElement, { childList: true, subtree: true });
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

})(window.sednaUi);

/* ── Code block, expand a clamped one ────────────────────────────────────────
   A `.code-block--clamped` is bounded to --code-clamp and scrolls. This is the
   control that opens it in full:

     <div class="code-block code-block--clamped">
       <pre tabindex="0"><code>…</code></pre>
       <div class="code-block-lip">
         <button class="code-block-expand" data-code-expand aria-expanded="false">
           <i class="ri-arrow-down-s-line"></i> Show all 42 lines
         </button>
       </div>
     </div>

   The clamp is CSS, so the block is bounded and scrollable with scripting blocked;
   this only removes the bound. `aria-expanded` moves with it, and the label swaps to
   the collapse wording from `data-code-collapse` if one is given.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var CLAMPED = 'code-block--clamped';

    function labelFor(button, expanded) {
        var other = expanded ? button.getAttribute('data-code-collapse')
                             : button.getAttribute('data-code-expand-label');
        if (!other) return;
        var text = button.querySelector('span');
        if (text) text.textContent = other;
    }

    ui.codeBlock = {
        /* Expands or collapses a block. Exposed so a "collapse all" control, or an app
           that renders its own lip, does not have to reproduce the class name. */
        toggle: function (block, expanded) {
            if (!block) return;
            var open = expanded === undefined ? block.classList.contains(CLAMPED) : expanded;
            block.classList.toggle(CLAMPED, !open);

            var button = block.querySelector('[data-code-expand]');
            if (button) {
                button.setAttribute('aria-expanded', String(open));
                labelFor(button, open);
            }
        }
    };

    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-code-expand]');
        if (!button) return;

        var block = button.closest('.code-block');
        if (!block) return;

        e.preventDefault();
        ui.codeBlock.toggle(block);

        // A collapse leaves the reader looking at the middle of the block. Put them
        // back at its top, which is where the collapsed view starts.
        if (block.classList.contains(CLAMPED)) {
            var pre = block.querySelector('pre');
            if (pre) pre.scrollTop = 0;
        }
    });

})(window.sednaUi);

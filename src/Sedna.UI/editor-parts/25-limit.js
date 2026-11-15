/* ── The length limit ─────────────────────────────────────────────────────────
   The textarea's `maxlength`, held by the editor: it counts the characters of the
   document's text — what `state().length` says, not the HTML — and what is typed or pasted
   beyond it is cut away at once. While typing is being stopped the frame carries
   `aria-invalid="true"`, which the next change inside the limit takes off. A document the app
   sets is its own: only what the reader types is held to the limit. Without the script the
   textarea holds it itself.
   ─────────────────────────────────────────────────────────────────────────── */

function limitOf(e) {
    const n = parseInt(e.value.getAttribute('maxlength') ?? '', 10);
    return n > 0 ? n : 0;
}

function wireLimit(e) {
    const q = e.quill;
    q.on('text-change', (delta, old, source) => {
        const max = limitOf(e);
        const length = q.getText().replace(/\n$/, '').length;
        if (source === 'user' && !e.setting && max && length > max) {
            const over = length - max;
            const caret = caretAfter(delta) ?? (q.getSelection()?.index ?? length);
            const from = Math.max(0, caret - over);
            q.deleteText(from, over, 'silent');
            q.setSelection(from, 0, 'silent');
            if (!e.el.hasAttribute('aria-invalid')) {
                e.el.setAttribute('aria-invalid', 'true');
                e.limitMarked = true;
            }
        } else if (e.limitMarked && source !== 'silent') {
            e.limitMarked = false;
            e.el.removeAttribute('aria-invalid');
        }
    });
}

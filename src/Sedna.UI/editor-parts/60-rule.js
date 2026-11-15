/* ── The horizontal rule ──────────────────────────────────────────────────────
   `data-editor-action="rule"` puts an `<hr>` at the caret — in a line of its own, as a video
   is — and the caret on the line after it. A rule is a format like any other: the editor holds
   it only when the toolbar offers the action, or `data-editor-formats` names `divider`, and
   what is pasted into one that does not is cut down without it. `.prose hr` draws it, so it
   looks the same where the HTML is shown.
   ─────────────────────────────────────────────────────────────────────────── */

const SednaRule = (() => {
    const BlockEmbed = Quill.import('blots/block/embed');
    return class SednaRule extends BlockEmbed {
        static blotName = 'divider';
        static tagName = 'HR';
    };
})();
Quill.register({ 'formats/divider': SednaRule }, true);

// Puts a rule where the caret is, or in place of the selection; the caret after it.
function insertRule(e, at) {
    const q = e.quill;
    if (!q.scroll.query('divider')) return false;
    const range = at || q.getSelection(true) || e.lastRange;
    if (!range) return false;
    q.updateContents(new Delta().retain(range.index).delete(range.length).insert({ divider: true }), 'user');
    // A rule put in the middle of a line starts a line of its own, one place on.
    const index = [range.index, range.index + 1].find(i => q.getContents(i, 1).ops[0]?.insert?.divider) ?? range.index;
    q.setSelection(index + 1, 0, 'silent');
    e.lastRange = { index: index + 1, length: 0 };
    reflect(e);
    return true;
}

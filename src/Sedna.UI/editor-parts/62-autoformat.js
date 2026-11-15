/* ── Markdown shortcuts while typing ──────────────────────────────────────────
   What a reader who knows Markdown types, turned into the format it names, for the formats
   the editor accepts and no others:

     # … ######  and a space      a heading of that level
     >  and a space                a quote
     ---  and Enter                a horizontal rule
     ```  and Enter               a code block
     **x**  *x*  `x`  ~~x~~       bold, italic, code, strike — when the closing mark is typed

   A list starts the way Quill already starts one: "- ", "1. ", "[ ] ". The change is its own
   step in the history, so Ctrl+Z puts back what was typed. Nothing happens inside a code
   block, or in a line of code.
   ─────────────────────────────────────────────────────────────────────────── */

const INLINE_MARKS = [
    { format: 'bold', pattern: /\*\*([^*\s](?:[^*]*[^*\s])?)\*\*$/, mark: 2, last: '*' },
    { format: 'strike', pattern: /~~([^~\s](?:[^~]*[^~\s])?)~~$/, mark: 2, last: '~' },
    { format: 'code', pattern: /`([^`]+)`$/, mark: 1, last: '`' },
    { format: 'italic', pattern: /(?<![*\w])\*([^*\s](?:[^*]*[^*\s])?)\*$/, mark: 1, last: '*' },
];

// Quill's keyboard bindings: the block shortcuts, which a key opens.
function autoformatBindings(e) {
    const has = f => e.formats.includes(f);
    const plain = { list: false, 'code-block': false };
    const line = (range, ctx, fn) => {
        const q = e.quill;
        q.history.cutoff();
        const start = range.index - ctx.prefix.length;
        q.deleteText(start, ctx.prefix.length, 'user');
        fn(start);
        q.history.cutoff();
        reflect(e);
        return false;
    };
    return {
        'sedna heading': {
            key: ' ', collapsed: true, format: plain, prefix: /^#{1,6}$/,
            handler(range, ctx) {
                if (!has('header')) return true;
                return line(range, ctx, start => e.quill.formatLine(start, 0, 'header', ctx.prefix.length, 'user'));
            },
        },
        'sedna quote': {
            key: ' ', collapsed: true, format: plain, prefix: /^>$/,
            handler(range, ctx) {
                if (!has('blockquote')) return true;
                return line(range, ctx, start => e.quill.formatLine(start, 0, 'blockquote', true, 'user'));
            },
        },
        'sedna rule': {
            key: 'Enter', collapsed: true, format: plain, prefix: /^---$/, suffix: /^$/,
            handler(range, ctx) {
                if (!has('divider')) return true;
                return line(range, ctx, start => insertRule(e, { index: start, length: 0 }));
            },
        },
        'sedna code block': {
            key: 'Enter', collapsed: true, format: plain, prefix: /^```$/, suffix: /^$/,
            handler(range, ctx) {
                if (!has('code-block')) return true;
                return line(range, ctx, start => e.quill.formatLine(start, 0, 'code-block', true, 'user'));
            },
        },
    };
}

// The inline marks: heard in the change that typed the closing one.
function wireAutoformat(e) {
    const q = e.quill;
    q.on('text-change', (delta, old, source) => {
        if (source !== 'user' || e.autoformatting || !e.formats.some(f => INLINE_MARKS.some(m => m.format === f))) return;
        const typed = delta.ops.find(op => typeof op.insert === 'string');
        if (!typed || typed.insert.length !== 1 || !INLINE_MARKS.some(m => m.last === typed.insert)) return;
        const caret = caretAfter(delta);
        if (caret == null) return;
        const [block, offset] = q.getLine(caret);
        if (!block) return;
        const here = q.getFormat(caret - 1, 1);
        if (here['code-block'] || here.code) return;
        const before = q.getText(caret - offset, offset);
        for (const rule of INLINE_MARKS) {
            if (!e.formats.includes(rule.format)) continue;
            const m = rule.pattern.exec(before);
            if (!m) continue;
            const start = caret - offset + m.index;
            e.autoformatting = true;
            try {
                q.history.cutoff();
                q.updateContents(new Delta().retain(start).delete(rule.mark).retain(m[1].length, { [rule.format]: true }).delete(rule.mark), 'user');
                q.setSelection(start + m[1].length, 0, 'silent');
                // What is typed next is not in the mark.
                q.format(rule.format, false, 'silent');
                q.history.cutoff();
            } finally {
                e.autoformatting = false;
            }
            break;
        }
    });
}

/* ── The value ────────────────────────────────────────────────────────────────
   The textarea is the field: it is what a form posts, what Blazor's `@bind` reads and
   writes, and what a page without the script shows. The editor loads its document from
   the textarea and writes every change back to it, as HTML, with the events a textarea
   sends while it is typed in — `input` at each change, `change` when the editor loses
   focus after one — so `@bind` and `@bind:event="oninput"` both work unchanged.

   A value written to the textarea from outside — by Blazor, when the bound field
   changes in C#, or by a script — is loaded into the editor. The textarea's `value` is
   watched for that on the element itself, which is how Blazor writes it; a value this
   editor wrote a moment ago, arriving back, is its own echo and is ignored.

   What is loaded is cut down to the formats the editor accepts: markup it cannot hold —
   a script, a style, an attribute — does not survive the load, and the value written
   back is the editor's own clean HTML. That is a convenience, not a sanitiser: what
   arrives at a server is still the server's to check.
   ─────────────────────────────────────────────────────────────────────────── */

const VALUE = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value');

/* The document as HTML. An empty document is the empty string, not an empty paragraph.
   Quill writes every space in the semantic HTML as a non-breaking one; a lone one is
   put back as a space, and a run keeps all but its first, so it still shows as a run. */
function htmlOf(quill) {
    if (quill.getLength() <= 1 && !quill.getContents().ops.some(op => typeof op.insert === 'object')) return '';
    return quill.getSemanticHTML().replace(/(?:&nbsp;)+/g, run => ' ' + '&nbsp;'.repeat(run.length / 6 - 1));
}

function stateOf(e) {
    const html = htmlOf(e.quill);
    const text = e.quill.getText().replace(/\n$/, '');
    return { html, text, length: text.length, isEmpty: html === '' };
}

function load(e, html) {
    e.loading = true;
    try {
        e.quill.setContents(e.quill.clipboard.convert({ html: html || '' }), 'silent');
        e.quill.history.clear();
    } finally {
        e.loading = false;
    }
    remember(e, htmlOf(e.quill));
    reflect(e);
}

// The last few values this editor wrote, so one arriving back is recognised as an echo.
function remember(e, html) {
    e.written.push(html);
    if (e.written.length > 20) e.written.shift();
}

function watchValue(e) {
    const ta = e.value;
    Object.defineProperty(ta, 'value', {
        configurable: true,
        get() { return VALUE.get.call(this); },
        set(v) {
            VALUE.set.call(this, v);
            if (e.disposed || e.written.includes(v ?? '')) return;
            load(e, v);
        },
    });
    e.cleanups.push(() => { delete ta.value; });

    // A form reset puts the textarea back without going through its value.
    const form = ta.form;
    if (form) {
        const onReset = () => setTimeout(() => { if (!e.disposed) load(e, VALUE.get.call(ta)); }, 0);
        form.addEventListener('reset', onReset);
        e.cleanups.push(() => form.removeEventListener('reset', onReset));
    }
}

function written(e) {
    if (e.loading) return;
    const html = htmlOf(e.quill);
    if (html === VALUE.get.call(e.value)) return;
    remember(e, html);
    VALUE.set.call(e.value, html);
    e.changed = true;
    if (html !== '' && e.el.hasAttribute('aria-invalid')) {
        e.el.removeAttribute('aria-invalid');
        e.quill.root.removeAttribute('aria-invalid');
    }
    e.value.dispatchEvent(new Event('input', { bubbles: true }));
    e.emit('sedna-editor-change', stateOf(e));
}

// `change` once the focus leaves the editor after an edit, as a textarea sends it.
function left(e) {
    if (!e.changed) return;
    e.changed = false;
    e.value.dispatchEvent(new Event('change', { bubbles: true }));
}

/* A required textarea that is hidden cannot take the browser's own check — the browser
   refuses to submit a form over a field it cannot focus. So the editor holds the
   requirement: an empty one stops the submit, is marked invalid and takes the focus. */
function watchRequired(e) {
    const ta = e.value;
    const form = ta.form;
    if (!ta.required) return;
    ta.required = false;
    e.required = true;
    e.quill.root.setAttribute('aria-required', 'true');
    if (!form) return;
    const onSubmit = event => {
        if (e.disposed || !e.required || htmlOf(e.quill) !== '') return;
        event.preventDefault();
        event.stopImmediatePropagation();
        e.el.setAttribute('aria-invalid', 'true');
        e.quill.root.setAttribute('aria-invalid', 'true');
        ta.dispatchEvent(new Event('invalid', { cancelable: true }));
        e.quill.focus();
    };
    form.addEventListener('submit', onSubmit, true);
    e.cleanups.push(() => {
        form.removeEventListener('submit', onSubmit, true);
        ta.required = true;
    });
}

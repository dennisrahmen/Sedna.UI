/* ── An editor ────────────────────────────────────────────────────────────────
   One `[data-editor]` element, from its markup to a document being edited:

     1. its textarea, body, toolbar, link form, media form and slash menu, from the markup;
     2. Quill, made in the body, accepting the formats the toolbar offers, with no toolbar
        or theme of its own — the app's toolbar is the toolbar;
     3. the document loaded from the textarea, and every change written back to it;
     4. the editable element given the textarea's name, description, placeholder and state;
     5. `data-editor-state` on the element: loading, then ready.

   Events, bubbling from the element, each with plain data in `detail`:

     sedna-editor-ready    { html, text, length, isEmpty }
     sedna-editor-change   the same, after each change
     sedna-editor-media    { kind, src, alt, width, height }, cancelable, before the media form opens
     sedna-editor-file     { files, index }, cancelable — cancelled when the app takes them
     sedna-editor-mention  { query }, cancelable — cancelled when the app fills the list itself

   and the textarea's own `input` and `change`, which is what `@bind` listens to.
   ─────────────────────────────────────────────────────────────────────────── */

const editors = new Map();

/* The editable element answers to the textarea's label, as the textarea would have: its
   `aria-labelledby`, else the `<label for>` pointing at it, else its `aria-label` — and
   its description, placeholder, required and disabled state the same way. */
function describe(e) {
    const ta = e.value;
    const root = e.quill.root;
    root.setAttribute('role', 'textbox');
    root.setAttribute('aria-multiline', 'true');
    const labelledBy = ta.getAttribute('aria-labelledby');
    const labels = ta.id ? Array.from(document.querySelectorAll(`label[for="${CSS.escape(ta.id)}"]`)) : [];
    if (labelledBy) {
        root.setAttribute('aria-labelledby', labelledBy);
    } else if (labels.length) {
        root.setAttribute('aria-labelledby', labels.map((l, i) => {
            if (!l.id) l.id = `${ta.id}-label${i ? '-' + i : ''}`;
            return l.id;
        }).join(' '));
        // A click on the label lands in the document, as it would in the textarea.
        const onLabel = ev => { ev.preventDefault(); e.quill.focus(); };
        labels.forEach(l => {
            l.addEventListener('click', onLabel);
            e.cleanups.push(() => l.removeEventListener('click', onLabel));
        });
    } else if (ta.getAttribute('aria-label')) {
        root.setAttribute('aria-label', ta.getAttribute('aria-label'));
    }
    const describedBy = ta.getAttribute('aria-describedby');
    if (describedBy) root.setAttribute('aria-describedby', describedBy);
    else root.removeAttribute('aria-describedby');
    // A value the app rejected is announced from the document, where the reader is. The
    // frame shows it from the textarea (46-form-layout.css); the editor's own required
    // check marks the frame, and is not undone here.
    if (ta.getAttribute('aria-invalid') === 'true') root.setAttribute('aria-invalid', 'true');
    else if (!e.el.hasAttribute('aria-invalid')) root.removeAttribute('aria-invalid');
    if (ta.placeholder) root.setAttribute('data-placeholder', ta.placeholder);
    const off = ta.disabled || ta.readOnly;
    e.quill.enable(!off);
    root.setAttribute('aria-readonly', off ? 'true' : 'false');
    reflect(e);
}

function start(e) {
    const { value, body, toolbar, link, media, slash, notice, bar, mention } = readMarkup(e.el);
    if (!value || !body) {
        e.el.setAttribute('data-editor-state', 'error');
        throw new Error('a [data-editor] element needs a <textarea data-editor-value> and a <div class="editor-body"> inside it.');
    }
    Object.assign(e, { value, body, toolbar, link, media, slash, notice, bar, mention, controls: controlsOf(e.el) });
    e.el.setAttribute('data-editor-state', 'loading');

    const formats = formatsOf(e.el);
    e.formats = formats;
    // Pictures kept inline (50-media.js): only on an editor that takes images.
    e.inline = null;
    if (e.el.hasAttribute('data-editor-inline-images')) {
        if (formats.includes('image')) {
            const max = Number(e.el.getAttribute('data-editor-inline-max'));
            e.inline = { max: Number.isFinite(max) && max > 0 ? max : INLINE_MAX };
        } else {
            warnOnce('inline-images', 'data-editor-inline-images needs an editor that takes images: offer data-editor-format="image" or name it in data-editor-formats.');
        }
    }
    e.quill = new Quill(body, {
        formats,
        modules: {
            toolbar: false,
            history: { delay: 800, maxStack: 200, userOnly: true },
            clipboard: { matchers: mediaMatchers(e) },
            uploader: uploaderFor(e),
            // Only an editor that takes tables loads the module, so one that does not
            // still cuts a pasted table down to its text.
            table: formats.includes('table'),
            keyboard: {
                bindings: {
                    toolbar: { key: 'F10', altKey: true, handler: () => (toBar(e) ? false : toToolbar(e)) },
                    link: { key: 'k', shortKey: true, handler: () => { if (e.link) openLink(e); return !e.link; } },
                    // Tab leaves the document, as it leaves a textarea, rather than typing a tab
                    // character. In a list or a code block Quill's own indent comes first.
                    tab: { key: 'Tab', handler: () => true },
                    ...autoformatBindings(e),
                },
            },
        },
    });

    load(e, value.value);
    watchValue(e);
    watchRequired(e);
    describe(e);
    wireToolbar(e);
    wireLink(e);
    wireMedia(e);
    wireSlash(e);
    wireBar(e);
    wireLimit(e);
    wireMention(e);
    wireAutoformat(e);

    const q = e.quill;
    q.on('text-change', (delta, old, source) => {
        written(e);
        // The reader has moved on: the notice about a file that was not taken goes.
        if (source === 'user') notify(e, false);
    });
    q.on('editor-change', () => reflect(e));
    q.on('selection-change', range => { if (range) e.lastRange = range; });
    // `change` when the focus leaves the editor after an edit — the document, its toolbar and its
    // link form together are the field. Quill cannot report this: focus moving to a button leaves
    // the page's selection where it was.
    const onFocusOut = ev => { if (!e.el.contains(ev.relatedTarget)) left(e); };
    e.el.addEventListener('focusout', onFocusOut);
    e.cleanups.push(() => e.el.removeEventListener('focusout', onFocusOut));

    // The textarea's own state, when the app changes it: disabled, read-only, its label.
    const attrs = new MutationObserver(() => { if (!e.disposed) describe(e); });
    attrs.observe(value, { attributes: true, attributeFilter: ['disabled', 'readonly', 'aria-label', 'aria-labelledby', 'aria-describedby', 'aria-invalid', 'placeholder'] });
    e.cleanups.push(() => attrs.disconnect());

    e.el.setAttribute('data-editor-state', 'ready');
    e.emit('sedna-editor-ready', stateOf(e));
}

function dispose(e) {
    if (e.disposed) return;
    e.disposed = true;
    editors.delete(e.el);
    e.cleanups.splice(0).forEach(off => { try { off(); } catch (x) { /* ignore */ } });
    e.el.removeAttribute('data-editor-state');
}

function makeApi(e) {
    return {
        get element() { return e.el; },
        /* The engine itself. Its API is Quill's and is not versioned by Sedna.UI: reach for
           it for what this module does not offer, and expect to revisit it when the
           vendored Quill changes. */
        get quill() { return e.quill; },
        state: () => stateOf(e),
        html: () => htmlOf(e.quill),
        /* Replaces the document — and the textarea's value, with its events — as a paste of
           this HTML would, cut down to the formats the editor accepts. */
        set: html => {
            // A document the app sets is its own: the length limit holds what the reader types.
            e.setting = true;
            try {
                e.quill.setContents(e.quill.clipboard.convert({ html: html || '' }), 'user');
            } finally {
                e.setting = false;
            }
            return stateOf(e);
        },
        /* Text at the caret — or at the end, if the document has not had the focus. */
        insert: text => {
            const range = e.quill.getSelection() || e.lastRange || { index: e.quill.getLength() - 1, length: 0 };
            e.quill.insertText(range.index, String(text ?? ''), 'user');
            e.quill.setSelection(range.index + String(text ?? '').length, 0, 'silent');
            return stateOf(e);
        },
        /* An image or a video where the caret was — or in place of the one it is on — with
           its description and its size in pixels, as the width and height attributes. Left
           out, the size is the one of the media it replaces; null or empty, there is none.
           Null, and nothing inserted, when the editor does not take that kind, the address is
           not http, https or relative, or a size is not a whole number above zero. */
        insertImage: (url, alt, width, height) => insertMedia(e, 'image', url, alt, takeMediaRange(e), { width, height }),
        insertVideo: (url, label, width, height) => insertMedia(e, 'video', url, label, takeMediaRange(e), { width, height }),
        /* The rows of the mention list, from the app's own search: `[{ value, label, note,
           icon }]`, cloned from the list's template. How many rows were made. */
        mentions: list => setMentions(e, list),
        focus: () => e.quill.focus(),
        enable: on => {
            e.value.disabled = on === false;
            describe(e);
        },
        /* A file pasted or dropped into the document, by the id its sedna-editor-file
           event named — a File, for the app to upload. Null once the editor has let it go. */
        file: id => fileOf(e, id),
        invoke: (method, args) => invoke(e, method, args),
        destroy: () => dispose(e),
    };
}

function invoke(e, method, args) {
    const a = args || [];
    const api = e.api;
    switch (method) {
        case 'state': return api.state();
        case 'set': return api.set(a[0]);
        case 'insert': return api.insert(a[0]);
        case 'image': return api.insertImage(a[0], a[1], a[2], a[3]);
        case 'video': return api.insertVideo(a[0], a[1], a[2], a[3]);
        case 'mentions': return api.mentions(a[0]);
        case 'focus': api.focus(); return null;
        case 'enable': api.enable(a[0] !== false); return null;
        // A File: Blazor reads it as an IJSStreamReference.
        case 'file': return api.file(a[0]);
        default:
            warnOnce('invoke:' + method, `"${method}" is not an editor method.`);
            return null;
    }
}

/* The editor behind an element, made the first time it is asked for. */
function attach(el) {
    let e = editors.get(el);
    if (!e) {
        e = { el, written: [], files: new Map(), cleanups: [], disposed: false, changed: false, loading: false, lastRange: null };
        // False when a cancelable event was cancelled.
        e.emit = (name, detail, cancelable) => el.dispatchEvent(new CustomEvent(name, { bubbles: true, cancelable: !!cancelable, detail }));
        e.api = makeApi(e);
        editors.set(el, e);
        try {
            start(e);
        } catch (x) {
            dispose(e);
            el.setAttribute('data-editor-state', 'error');
            return Promise.reject(x);
        }
    }
    return Promise.resolve(e.api);
}

/* Takes down every editor whose element has left the document. */
function sweep() {
    for (const e of [...editors.values()]) if (!e.el.isConnected) dispose(e);
}

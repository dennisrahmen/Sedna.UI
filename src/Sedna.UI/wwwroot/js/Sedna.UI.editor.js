/* ═══════════════════════════════════════════════════════════════════════════
   GENERATED FILE — DO NOT EDIT.

   Built by build/bundle-js.sh from src/Sedna.UI/editor-parts/. Edit the part
   that owns the behaviour and re-run that script; a guard test fails the build
   if this file and the parts disagree. Adding a part needs no change here —
   the directory is the source of truth.

   Contents, in load order:
     00-imports.js
     10-markup.js
     20-value.js
     30-toolbar.js
     40-link.js
     70-editor.js
     99-exports.js
   ═══════════════════════════════════════════════════════════════════════════ */

/* ── 00-imports.js ──────────────────────────────────────────────── */
/* Sedna.UI — the rich-text editor.
   ───────────────────────────────────────────────────────────────────────────
   An ES module, imported by Sedna.UI.js the first time a page shows a `[data-editor]`
   element. No app references this file: a page without an editor never downloads it,
   or the engine below it.

   Quill edits the document. Everything around the document — the toolbar and its
   buttons, the link form, the labels, the words — is the app's own markup with the
   library's classes, and the value is the app's own `<textarea>`, which the form posts
   and Blazor binds as it would any other. What this module adds is the part markup
   cannot be:

     * the document, loaded from the textarea and written back to it as HTML, with the
       events a textarea sends, so `@bind`, a form post and a page without Blazor all
       read the same field;
     * the app's toolbar driving it — every button's pressed state kept current, and one
       tab stop with the arrow keys between its buttons;
     * only the formats the toolbar offers, so what is pasted is cut down to them;
     * the link form, placed at the text it is for;
     * the editable element's name, description and state, taken from the textarea's.

   The parts in editor-parts/ are this one module cut into files. They share its
   top-level scope: only 00 imports and only 99 exports. See editor-parts/CLAUDE.md.
   ─────────────────────────────────────────────────────────────────────────── */
import Quill from '../lib/quill/quill.js';

/* ── 10-markup.js ──────────────────────────────────────────────── */
/* ── The app's markup ─────────────────────────────────────────────────────────
   One editor is one element the app wrote:

     <div class="editor" data-editor id="note">
       <div class="editor-toolbar" role="toolbar" aria-label="Formatting">
         <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="bold" aria-label="Bold"><i class="ri-bold"></i></button>
         …
       </div>
       <div class="editor-body prose" data-editor-body></div>
       <textarea name="note" data-editor-value hidden aria-label="Note">&lt;p&gt;…&lt;/p&gt;</textarea>
     </div>

   `[data-editor-body]` is where the document is edited (`.editor-body` if none is
   marked); `textarea[data-editor-value]` holds the value, as HTML. The toolbar is any
   element with `data-editor-format` or `data-editor-action` inside the editor.

   A format is one of Quill's, named on the control; a control with a `value` sets that
   value, and a `<select>` sets the one chosen:

     bold italic underline strike code script(sub|super) link
     header(1–6) list(bullet|ordered|check) blockquote code-block align(center|right|justify)

   The formats an editor accepts are the ones its toolbar offers — so what is pasted in
   is cut down to what the reader could have written — or `data-editor-formats` on the
   editor, a space-separated list, where the app wants keyboard shortcuts for formats
   it shows no button for.
   ─────────────────────────────────────────────────────────────────────────── */

const FORMATS = ['bold', 'italic', 'underline', 'strike', 'code', 'script', 'link',
    'header', 'list', 'blockquote', 'code-block', 'align', 'indent'];
const CONTROL = '[data-editor-format],[data-editor-action]';

const warned = new Set();
function warnOnce(key, message) {
    if (warned.has(key)) return;
    warned.add(key);
    try { console.warn('Sedna.UI editor: ' + message); } catch (e) { /* ignore */ }
}

// The editor's own controls: inside it, and not inside another editor nested in it.
function controlsOf(el) {
    return Array.from(el.querySelectorAll(CONTROL)).filter(c => c.closest('[data-editor]') === el);
}

function readMarkup(el) {
    const value = el.querySelector('textarea[data-editor-value]');
    const body = el.querySelector('[data-editor-body]') || el.querySelector('.editor-body');
    const toolbar = el.querySelector('[role="toolbar"]') || el.querySelector('.editor-toolbar');
    const link = el.querySelector('[data-editor-link]');
    return { value, body, toolbar, link };
}

/* The formats this editor accepts, as Quill names them. A list needs indent, for its
   nested items; `check` is Quill's `list` with a checked or unchecked value. */
function formatsOf(el) {
    const named = (el.getAttribute('data-editor-formats') || '').split(/\s+/).filter(Boolean);
    const offered = controlsOf(el).map(c => c.getAttribute('data-editor-format')).filter(Boolean);
    const acted = controlsOf(el).map(c => c.getAttribute('data-editor-action'));
    const wanted = new Set(named.length ? named : offered);
    if (acted.includes('indent') || acted.includes('outdent') || wanted.has('list')) wanted.add('indent');
    for (const f of wanted) {
        if (!FORMATS.includes(f)) {
            warnOnce('format:' + f, `"${f}" is not a format the editor offers; it is ignored.`);
            wanted.delete(f);
        }
    }
    return [...wanted];
}

/* ── 20-value.js ──────────────────────────────────────────────── */
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

/* ── 30-toolbar.js ──────────────────────────────────────────────── */
/* ── The toolbar ──────────────────────────────────────────────────────────────
   The app's buttons, driving the document. A format button is a toggle, and its
   `aria-pressed` follows the text under the caret — the attribute the stylesheet
   colours. A `<select>` shows the value under the caret. An action button — undo, redo,
   clean, indent, outdent — is `disabled` while there is nothing for it to do.

   The toolbar is one tab stop: the arrow keys, Home and End move between its buttons,
   and the one last used keeps the stop. Alt+F10 in the document goes to the toolbar and
   Escape comes back. A button pressed with the pointer leaves the focus, and the
   selection, in the document.
   ─────────────────────────────────────────────────────────────────────────── */

// What a format control says about the text under the caret.
function isOn(format, name, value) {
    const now = format[name];
    if (name === 'list' && value === 'check') return now === 'checked' || now === 'unchecked';
    if (value === null || value === '') return !!now && now !== false;
    return String(now) === value;
}

function reflect(e) {
    if (e.disposed) return;
    const q = e.quill;
    const range = q.getSelection() || e.lastRange;
    let format = {};
    try { format = range ? q.getFormat(range) : {}; } catch (x) { format = {}; }
    for (const c of e.controls) {
        const name = c.getAttribute('data-editor-format');
        if (name) {
            if (c.tagName === 'SELECT') {
                const now = format[name];
                c.value = now === undefined || now === false || now === null ? '' : String(now);
            } else {
                c.setAttribute('aria-pressed', isOn(format, name, c.getAttribute('value')) ? 'true' : 'false');
            }
            continue;
        }
        const action = c.getAttribute('data-editor-action');
        const idle = action === 'undo' ? q.history.stack.undo.length === 0
            : action === 'redo' ? q.history.stack.redo.length === 0 : false;
        c.disabled = idle || !q.isEnabled();
    }
    keepStop(e);
}

// The toolbar's one tab stop is on a button that can take it: a disabled one cannot.
function keepStop(e) {
    if (!e.toolbar) return;
    const list = e.controls.filter(c => c.tagName === 'BUTTON' && e.toolbar.contains(c));
    const stop = list.find(b => b.tabIndex === 0 && !b.disabled) || list.find(b => !b.disabled);
    for (const b of list) b.tabIndex = b === stop ? 0 : -1;
}

function applyFormat(e, c) {
    const q = e.quill;
    const name = c.getAttribute('data-editor-format');
    if (name === 'link') return openLink(e);
    const range = q.getSelection(true);
    if (!range) return;
    if (c.tagName === 'SELECT') {
        q.format(name, c.value || false, 'user');
    } else {
        const value = c.getAttribute('value');
        const on = isOn(q.getFormat(range), name, value);
        const next = name === 'list' && value === 'check' ? 'unchecked' : (value || true);
        q.format(name, on ? false : next, 'user');
    }
    reflect(e);
}

function act(e, c) {
    const q = e.quill;
    const action = c.getAttribute('data-editor-action');
    switch (action) {
        case 'undo': q.history.undo(); break;
        case 'redo': q.history.redo(); break;
        case 'indent': q.getSelection(true); q.format('indent', '+1', 'user'); break;
        case 'outdent': q.getSelection(true); q.format('indent', '-1', 'user'); break;
        case 'clean': {
            const range = q.getSelection(true);
            if (!range) break;
            if (range.length === 0) Object.keys(q.getFormat(range)).forEach(f => q.format(f, false, 'user'));
            else q.removeFormat(range.index, range.length, 'user');
            break;
        }
        default:
            warnOnce('action:' + action, `"${action}" is not an editor action.`);
    }
    reflect(e);
}

function wireToolbar(e) {
    const on = (target, type, fn) => {
        target.addEventListener(type, fn);
        e.cleanups.push(() => target.removeEventListener(type, fn));
    };
    const buttons = () => e.controls.filter(c => c.tagName === 'BUTTON' && e.toolbar && e.toolbar.contains(c));

    on(e.el, 'click', ev => {
        const c = ev.target.closest(CONTROL);
        if (!c || c.tagName === 'SELECT' || !e.controls.includes(c) || c.disabled) return;
        if (c.hasAttribute('data-editor-format')) applyFormat(e, c);
        else act(e, c);
    });
    on(e.el, 'change', ev => {
        const c = ev.target.closest('select[data-editor-format]');
        if (!c || !e.controls.includes(c)) return;
        applyFormat(e, c);
        e.quill.focus();
    });
    // Pressed with the pointer, a button leaves the focus and the selection where they were.
    on(e.el, 'mousedown', ev => {
        const c = ev.target.closest(CONTROL);
        if (c && c.tagName === 'BUTTON' && e.controls.includes(c)) ev.preventDefault();
    });

    if (!e.toolbar) return;
    // One tab stop: the first button, then whichever was last used.
    const rove = target => {
        for (const b of buttons()) b.tabIndex = b === target ? 0 : -1;
    };
    keepStop(e);
    on(e.toolbar, 'focusin', ev => {
        if (ev.target.tagName === 'BUTTON' && e.controls.includes(ev.target)) rove(ev.target);
    });
    on(e.toolbar, 'keydown', ev => {
        const list = buttons();
        const i = list.indexOf(ev.target);
        if (ev.key === 'Escape') {
            ev.preventDefault();
            e.quill.focus();
            return;
        }
        if (i < 0) return;
        const rtl = getComputedStyle(e.toolbar).direction === 'rtl';
        const step = { ArrowRight: rtl ? -1 : 1, ArrowLeft: rtl ? 1 : -1 }[ev.key];
        let next = null;
        if (step) next = list[(i + step + list.length) % list.length];
        else if (ev.key === 'Home') next = list[0];
        else if (ev.key === 'End') next = list[list.length - 1];
        if (!next) return;
        ev.preventDefault();
        rove(next);
        next.focus();
    });
}

// Alt+F10: from the document to the toolbar's stop.
function toToolbar(e) {
    if (!e.toolbar) return true;
    const stop = e.controls.find(c => e.toolbar.contains(c) && c.tabIndex === 0 && !c.disabled)
        || e.controls.find(c => e.toolbar.contains(c) && !c.disabled);
    if (!stop) return true;
    stop.focus();
    return false;
}

/* ── 40-link.js ──────────────────────────────────────────────── */
/* ── The link form ────────────────────────────────────────────────────────────
   The app's own form for a link's address, written once inside the editor, which the
   script opens at the text it is for — from a `data-editor-format="link"` button or
   Ctrl+K:

     <div class="editor-link" data-editor-link hidden>
       <input class="form-input" type="url" data-editor-link-url aria-label="Address" />
       <button class="btn btn-sm" type="button" data-editor-link-save>Save</button>
       <button class="btn btn-ghost btn-sm" type="button" data-editor-link-remove>Remove</button>
     </div>

   It is filled with the address of the link under the caret, if there is one; Remove
   shows only then. Enter saves and Escape closes, and both put the focus back in the
   document. With no text selected, the address itself is written as the link's text.
   An address Quill does not accept — anything but http, https, mailto, tel and sms — is
   written as about:blank rather than kept.
   ─────────────────────────────────────────────────────────────────────────── */

// The link the caret or the selection is in, as a range and its address — or null.
function linkAt(q, range) {
    const Link = Quill.import('formats/link');
    const [blot, offset] = q.scroll.descendant(Link, range.index);
    if (!blot) return null;
    return { index: range.index - offset, length: blot.length(), href: Link.formats(blot.domNode) };
}

function openLink(e) {
    const panel = e.link;
    if (!panel) {
        warnOnce('link', 'a link control needs the app\'s [data-editor-link] form inside the editor.');
        return;
    }
    const q = e.quill;
    const range = q.getSelection(true);
    if (!range) return;
    const existing = linkAt(q, range);
    e.linking = { range, existing };
    const url = panel.querySelector('[data-editor-link-url]');
    const remove = panel.querySelector('[data-editor-link-remove]');
    if (url) url.value = existing ? existing.href : '';
    if (remove) remove.hidden = !existing;
    panel.hidden = false;
    placeLink(e, existing || range);
    (url || panel).focus({ preventScroll: true });
}

// Beneath the text it is for, inside the editor's frame.
function placeLink(e, range) {
    const panel = e.link;
    const bounds = e.quill.getBounds(range.index, range.length);
    if (!bounds) return;
    const frame = e.el.getBoundingClientRect();
    const box = e.quill.container.getBoundingClientRect();
    panel.style.transform = 'translate(0px, 0px)';
    const own = panel.getBoundingClientRect();
    let x = box.left - frame.left + bounds.left;
    let y = box.top - frame.top + bounds.bottom + 6;
    x = Math.max(8, Math.min(x, frame.width - own.width - 8));
    if (y + own.height > frame.height - 8) y = Math.max(8, box.top - frame.top + bounds.top - own.height - 6);
    panel.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
}

function closeLink(e, refocus) {
    if (!e.link || e.link.hidden) return;
    e.link.hidden = true;
    const was = e.linking;
    e.linking = null;
    if (refocus) {
        e.quill.focus();
        if (was) e.quill.setSelection(was.range.index + was.range.length, 0, 'silent');
    }
}

function saveLink(e) {
    const was = e.linking;
    if (!was) return;
    const q = e.quill;
    const href = (e.link.querySelector('[data-editor-link-url]')?.value || '').trim();
    const { range, existing } = was;
    if (!href) {
        if (existing) q.formatText(existing.index, existing.length, 'link', false, 'user');
    } else if (existing) {
        q.formatText(existing.index, existing.length, 'link', href, 'user');
    } else if (range.length > 0) {
        q.formatText(range.index, range.length, 'link', href, 'user');
    } else {
        q.insertText(range.index, href, 'link', href, 'user');
        was.range = { index: range.index, length: href.length };
    }
    closeLink(e, true);
    reflect(e);
}

function removeLink(e) {
    const was = e.linking;
    if (was && was.existing) e.quill.formatText(was.existing.index, was.existing.length, 'link', false, 'user');
    closeLink(e, true);
    reflect(e);
}

function wireLink(e) {
    const panel = e.link;
    if (!panel) return;
    const on = (target, type, fn, opts) => {
        target.addEventListener(type, fn, opts);
        e.cleanups.push(() => target.removeEventListener(type, fn, opts));
    };
    on(panel, 'click', ev => {
        if (ev.target.closest('[data-editor-link-save]')) saveLink(e);
        else if (ev.target.closest('[data-editor-link-remove]')) removeLink(e);
        else if (ev.target.closest('[data-editor-link-cancel]')) closeLink(e, true);
    });
    on(panel, 'keydown', ev => {
        if (ev.key === 'Enter' && ev.target.matches('input')) {
            ev.preventDefault();
            saveLink(e);
        } else if (ev.key === 'Escape') {
            ev.preventDefault();
            ev.stopPropagation();
            closeLink(e, true);
        }
    });
    // A press anywhere else closes it, leaving the focus where the press put it.
    on(document, 'pointerdown', ev => {
        if (!panel.hidden && !panel.contains(ev.target) && !ev.target.closest?.('[data-editor-format="link"]')) closeLink(e, false);
    }, true);
}

/* ── 70-editor.js ──────────────────────────────────────────────── */
/* ── An editor ────────────────────────────────────────────────────────────────
   One `[data-editor]` element, from its markup to a document being edited:

     1. its textarea, body, toolbar and link form, from the markup;
     2. Quill, made in the body, accepting the formats the toolbar offers, with no toolbar
        or theme of its own — the app's toolbar is the toolbar;
     3. the document loaded from the textarea, and every change written back to it;
     4. the editable element given the textarea's name, description, placeholder and state;
     5. `data-editor-state` on the element: loading, then ready.

   Events, bubbling from the element, each with plain data in `detail`:

     sedna-editor-ready    { html, text, length, isEmpty }
     sedna-editor-change   the same, after each change

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
    if (ta.placeholder) root.setAttribute('data-placeholder', ta.placeholder);
    const off = ta.disabled || ta.readOnly;
    e.quill.enable(!off);
    root.setAttribute('aria-readonly', off ? 'true' : 'false');
    reflect(e);
}

function start(e) {
    const { value, body, toolbar, link } = readMarkup(e.el);
    if (!value || !body) {
        e.el.setAttribute('data-editor-state', 'error');
        throw new Error('a [data-editor] element needs a <textarea data-editor-value> and a <div class="editor-body"> inside it.');
    }
    Object.assign(e, { value, body, toolbar, link, controls: controlsOf(e.el) });
    e.el.setAttribute('data-editor-state', 'loading');

    e.quill = new Quill(body, {
        formats: formatsOf(e.el),
        modules: {
            toolbar: false,
            history: { delay: 800, maxStack: 200, userOnly: true },
            keyboard: {
                bindings: {
                    toolbar: { key: 'F10', altKey: true, handler: () => toToolbar(e) },
                    link: { key: 'k', shortKey: true, handler: () => { if (e.link) openLink(e); return !e.link; } },
                    // Tab leaves the document, as it leaves a textarea, rather than typing a tab
                    // character. In a list or a code block Quill's own indent comes first.
                    tab: { key: 'Tab', handler: () => true },
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

    const q = e.quill;
    q.on('text-change', () => written(e));
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
    attrs.observe(value, { attributes: true, attributeFilter: ['disabled', 'readonly', 'aria-label', 'aria-labelledby', 'aria-describedby', 'placeholder'] });
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
            e.quill.setContents(e.quill.clipboard.convert({ html: html || '' }), 'user');
            return stateOf(e);
        },
        /* Text at the caret — or at the end, if the document has not had the focus. */
        insert: text => {
            const range = e.quill.getSelection() || e.lastRange || { index: e.quill.getLength() - 1, length: 0 };
            e.quill.insertText(range.index, String(text ?? ''), 'user');
            e.quill.setSelection(range.index + String(text ?? '').length, 0, 'silent');
            return stateOf(e);
        },
        focus: () => e.quill.focus(),
        enable: on => {
            e.value.disabled = on === false;
            describe(e);
        },
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
        case 'focus': api.focus(); return null;
        case 'enable': api.enable(a[0] !== false); return null;
        default:
            warnOnce('invoke:' + method, `"${method}" is not an editor method.`);
            return null;
    }
}

/* The editor behind an element, made the first time it is asked for. */
function attach(el) {
    let e = editors.get(el);
    if (!e) {
        e = { el, written: [], cleanups: [], disposed: false, changed: false, loading: false, lastRange: null };
        e.emit = (name, detail) => el.dispatchEvent(new CustomEvent(name, { bubbles: true, detail }));
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

/* ── 99-exports.js ──────────────────────────────────────────────── */
/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two. A
   script reaches an editor through `sednaUi.editor.get(el)`, which returns the handle
   `attach` resolves — never through an import of this file.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };


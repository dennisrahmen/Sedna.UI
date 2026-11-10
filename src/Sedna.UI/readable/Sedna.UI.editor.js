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
     45-slash.js
     50-media.js
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
     * images and videos, from addresses the app supplies — through its own media form
       or a call — and only addresses the editor can show;
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
         <button class="btn btn--ghost btn--icon btn--sm" type="button" data-editor-format="bold" aria-label="Bold"><i class="ri-bold"></i></button>
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

     bold italic underline strike code script(sub|super) link image video
     header(1–6) list(bullet|ordered|check) blockquote code-block align(center|right|justify)
     color background table — opt-in: offered only when a control or data-editor-formats
     names them. A colour is the app's choice of value, kept as written; legibility in
     either variant is the app's to judge.

   The formats an editor accepts are the ones its toolbar offers — so what is pasted in
   is cut down to what the reader could have written — or `data-editor-formats` on the
   editor, a space-separated list, where the app wants keyboard shortcuts for formats
   it shows no button for.
   ─────────────────────────────────────────────────────────────────────────── */

const FORMATS = ['bold', 'italic', 'underline', 'strike', 'code', 'script', 'link', 'image', 'video',
    'header', 'list', 'blockquote', 'code-block', 'align', 'indent', 'color', 'background', 'table'];
// What a table is made of in Quill: the cell is `table`, held by these three.
const TABLE_PARTS = ['table-row', 'table-body', 'table-container'];
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
    const media = el.querySelector('[data-editor-media]');
    const slash = el.querySelector('[data-editor-slash]');
    const notice = el.querySelector('[data-editor-file-notice]');
    return { value, body, toolbar, link, media, slash, notice };
}

/* The formats this editor accepts, as Quill names them. A list needs indent, for its
   nested items; `check` is Quill's `list` with a checked or unchecked value. */
function formatsOf(el) {
    const named = (el.getAttribute('data-editor-formats') || '').split(/\s+/).filter(Boolean);
    const offered = controlsOf(el).map(c => c.getAttribute('data-editor-format')).filter(Boolean);
    const acted = controlsOf(el).map(c => c.getAttribute('data-editor-action'));
    const wanted = new Set(named.length ? named : offered);
    if (acted.includes('indent') || acted.includes('outdent') || wanted.has('list')) wanted.add('indent');
    if (acted.some(a => a && a.startsWith('table-'))) wanted.add('table');
    for (const f of wanted) {
        if (!FORMATS.includes(f)) {
            warnOnce('format:' + f, `"${f}" is not a format the editor offers; it is ignored.`);
            wanted.delete(f);
        }
    }
    if (wanted.has('table')) TABLE_PARTS.forEach(p => wanted.add(p));
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
   put back as a space, and a run keeps all but its first, so it still shows as a run.
   A document that ends in a video has an empty line after it, which Quill must hold and
   the reader did not write: it is left out, so a value ending in a video loads and is
   written back as it was. */
function htmlOf(quill) {
    if (quill.getLength() <= 1 && !quill.getContents().ops.some(op => typeof op.insert === 'object')) return '';
    return quill.getSemanticHTML()
        .replace(/(?:&nbsp;)+/g, run => ' ' + '&nbsp;'.repeat(run.length / 6 - 1))
        .replace(/(<\/video>)<p><\/p>$/, '$1');
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
    // What was loaded is not something this editor wrote: a value set from outside later
    // — '' to reset a form — must load even when it equals what was loaded first.
    e.written = [];
    reflect(e);
}

// What this editor wrote, and when, so a value Blazor echoes back a round trip late is
// recognised and not loaded over newer typing. Only a recent write counts: a value the
// app sets from outside, even one the editor held a few changes ago, always loads.
const ECHO_MS = 3000;
function remember(e, html) {
    const now = Date.now();
    e.written = e.written.filter(w => now - w.at < ECHO_MS);
    e.written.push({ html, at: now });
}
function isEcho(e, v) {
    const now = Date.now();
    return e.written.some(w => w.html === v && now - w.at < ECHO_MS);
}

function watchValue(e) {
    const ta = e.value;
    Object.defineProperty(ta, 'value', {
        configurable: true,
        get() { return VALUE.get.call(this); },
        set(v) {
            VALUE.set.call(this, v);
            const value = v ?? '';
            if (e.disposed || value === htmlOf(e.quill) || isEcho(e, value)) return;
            load(e, value);
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
            // Inserting an image or a video is an act, not a state of the text: no aria-pressed.
            if (MEDIA.includes(name)) continue;
            if (c.tagName === 'SELECT') {
                const now = format[name];
                c.value = now === undefined || now === false || now === null ? '' : String(now);
            } else if (c.tagName === 'INPUT') {
                // A colour field shows the colour under the caret, where it is one a field
                // can show; otherwise it keeps the last one chosen.
                const now = format[name];
                if (typeof now === 'string' && /^#[0-9a-f]{6}$/i.test(now)) c.value = now;
            } else {
                c.setAttribute('aria-pressed', isOn(format, name, c.getAttribute('value')) ? 'true' : 'false');
            }
            continue;
        }
        const action = c.getAttribute('data-editor-action');
        // A row or column action needs the caret in a table; inserting one, the opposite.
        const inTable = !!format.table;
        const idle = action === 'undo' ? q.history.stack.undo.length === 0
            : action === 'redo' ? q.history.stack.redo.length === 0
                : action === 'table-insert' ? inTable
                    : action && action.startsWith('table-') ? !inTable : false;
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
    if (MEDIA.includes(name)) return openMedia(e, name);
    const range = q.getSelection(true);
    if (!range) return;
    if (c.tagName === 'SELECT' || c.tagName === 'INPUT') {
        q.format(name, c.value || false, 'user');
    } else {
        const value = c.getAttribute('value');
        const on = isOn(q.getFormat(range), name, value);
        const next = name === 'list' && value === 'check' ? 'unchecked' : (value || true);
        q.format(name, on ? false : next, 'user');
    }
    reflect(e);
}

/* A table's actions, through Quill's table module — loaded only for an editor that takes
   tables. Rows and columns are added beside the cell the caret is in. */
function tableAction(q, action) {
    const table = q.getModule('table');
    if (!table) return;
    q.getSelection(true);
    switch (action) {
        case 'table-insert': table.insertTable(2, 2); break;
        case 'table-row-above': table.insertRowAbove(); break;
        case 'table-row-below': table.insertRowBelow(); break;
        case 'table-column-before': table.insertColumnLeft(); break;
        case 'table-column-after': table.insertColumnRight(); break;
        case 'table-row-delete': table.deleteRow(); break;
        case 'table-column-delete': table.deleteColumn(); break;
        case 'table-delete': table.deleteTable(); break;
        default: break;
    }
}

function act(e, c) {
    const q = e.quill;
    const action = c.getAttribute('data-editor-action');
    switch (action) {
        case 'undo': q.history.undo(); break;
        case 'redo': q.history.redo(); break;
        case 'indent': q.getSelection(true); q.format('indent', '+1', 'user'); break;
        case 'outdent': q.getSelection(true); q.format('indent', '-1', 'user'); break;
        case 'table-insert': case 'table-row-above': case 'table-row-below':
        case 'table-column-before': case 'table-column-after':
        case 'table-row-delete': case 'table-column-delete': case 'table-delete':
            tableAction(q, action); break;
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
        if (!c || c.tagName === 'SELECT' || c.tagName === 'INPUT' || !e.controls.includes(c) || c.disabled) return;
        if (c.hasAttribute('data-editor-format')) applyFormat(e, c);
        else act(e, c);
    });
    on(e.el, 'change', ev => {
        const c = ev.target.closest('select[data-editor-format], input[data-editor-format]');
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
       <button class="btn btn--sm" type="button" data-editor-link-save>Save</button>
       <button class="btn btn--ghost btn--sm" type="button" data-editor-link-remove>Remove</button>
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
    place(e, panel, existing || range);
    (url || panel).focus({ preventScroll: true });
}

// A form beneath the text it is for, inside the editor's frame: the link form, the media form.
function place(e, panel, range) {
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

/* ── 45-slash.js ──────────────────────────────────────────────── */
/* ── The slash menu ───────────────────────────────────────────────────────────
   The app's own `.menu`, written once inside the editor, which "/" opens at the caret
   — typed at the start of a line or after a space — to turn the line into another
   kind of block:

     <div class="menu editor-slash" data-editor-slash hidden aria-label="Turn the line into">
       <button class="menu-item" type="button" tabindex="-1" data-editor-block="paragraph"><i class="ri-text"></i><span>Text</span></button>
       <button class="menu-item" type="button" tabindex="-1" data-editor-block="header" value="2"><i class="ri-h-2"></i><span>Heading</span></button>
       <button class="menu-item" type="button" tabindex="-1" data-editor-block="list" value="bullet"><i class="ri-list-unordered"></i><span>Bulleted list</span></button>
       …
     </div>

   What follows the "/" filters the items as the reader types, ranked by the matcher the
   palette uses, against an item's `data-label` or its text; an item that does not
   match is `hidden`, and so is every `.menu-label` and `.menu-sep` while anything is
   typed. Nothing matching, a space after the "/", or the caret leaving the word closes
   it. The focus stays in the document: Up and Down move the item marked `data-active`
   (and `aria-selected`, which the document points at with `aria-activedescendant`),
   Enter or Tab turns the line into it, Escape closes it until another "/" is typed. A
   press on an item does the same as Enter.

   An item names a block format and its value as a toolbar control does —
   `header` 1–6, `list` bullet|ordered|check, `blockquote`, `code-block`, `align` — or
   `paragraph`, which takes every block format off the line. An item whose format the
   editor does not accept is never shown: the menu offers nothing the toolbar does not.
   The "/" and what was typed after it are removed.
   ─────────────────────────────────────────────────────────────────────────── */

const BLOCKS = ['header', 'list', 'blockquote', 'code-block', 'align'];
const SLASH = /(?:^|\s)\/([^/\n]{0,32})$/;
let slashIds = 0;

function slashItems(e) {
    if (!e.slash) return [];
    return Array.from(e.slash.querySelectorAll('.menu-item[data-editor-block]'))
        .filter(item => item.closest('[data-editor-slash]') === e.slash);
}

// An item the editor can apply: its format is one the editor accepts.
function slashUsable(e, item) {
    const name = item.getAttribute('data-editor-block');
    if (name === 'paragraph') return true;
    if (BLOCKS.includes(name) && e.formats.includes(name)) return true;
    warnOnce('block:' + name, `the slash menu offers "${name}", which this editor does not accept; the item is not shown.`);
    return false;
}

// The "/" the caret is after, and what is typed since: { start, query } or null. Just
// after typing, the caret is where the change put it: Quill can report the selection a
// step behind while it settles the first character of an empty line.
function slashAt(e, caret) {
    const q = e.quill;
    const range = caret == null ? q.getSelection() : { index: caret, length: 0 };
    if (!range || range.length) return null;
    const [line, offset] = q.getLine(range.index);
    if (!line) return null;
    let format = {};
    try { format = q.getFormat(range.index, 0); } catch (x) { format = {}; }
    if (format['code-block']) return null;
    const before = q.getText(range.index - offset, offset);
    const m = SLASH.exec(before);
    if (!m || /^\s/.test(m[1])) return null;
    return { start: range.index - m[1].length - 1, end: range.index, query: m[1] };
}

function setActive(e, item) {
    for (const it of slashItems(e)) {
        const on = it === item;
        it.toggleAttribute('data-active', on);
        it.setAttribute('aria-selected', on ? 'true' : 'false');
    }
    if (item) {
        e.quill.root.setAttribute('aria-activedescendant', item.id);
        item.scrollIntoView({ block: 'nearest' });
    } else {
        e.quill.root.removeAttribute('aria-activedescendant');
    }
}

function shownItems(e) {
    return slashItems(e).filter(item => !item.hidden);
}

function closeSlash(e) {
    if (!e.slash || e.slash.hidden) return;
    e.slash.hidden = true;
    e.slashOpen = null;
    setActive(e, null);
    e.quill.root.removeAttribute('aria-controls');
}

// The menu under the "/", or above it where the window has no room below.
function placeSlash(e, index) {
    const menu = e.slash;
    const b = e.quill.getBounds(index, 0);
    if (!b) return;
    const frame = e.el.getBoundingClientRect();
    const box = e.quill.container.getBoundingClientRect();
    menu.style.transform = 'translate(0px, 0px)';
    const own = menu.getBoundingClientRect();
    const rtl = getComputedStyle(e.quill.root).direction === 'rtl';
    let x = box.left - frame.left + (rtl ? b.right - own.width : b.left);
    x = Math.max(0, Math.min(x, frame.width - own.width));
    let y = box.top - frame.top + b.bottom + 4;
    if (frame.top + y + own.height > window.innerHeight - 8) y = box.top - frame.top + b.top - own.height - 4;
    menu.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
}

/* Opens, filters or closes the menu for where the caret is now. `typed` is true after
   the reader typed: only typing opens it, and a caret moved elsewhere only closes it. */
function updateSlash(e, typed, caret) {
    if (!e.slash || e.disposed) return;
    const at = slashAt(e, caret);
    if (!at) {
        e.slashDismissed = null;
        closeSlash(e);
        return;
    }
    if (e.slashDismissed === at.start || (!typed && e.slash.hidden)) return;
    const score = window.sednaUi?._?.score;
    let any = false;
    for (const item of slashItems(e)) {
        const label = (item.getAttribute('data-label') || item.textContent || '').trim();
        const match = slashUsable(e, item) && (!at.query || !score || score(at.query, label, 0) >= 0);
        item.hidden = !match;
        if (match) any = true;
    }
    for (const kid of e.slash.querySelectorAll('.menu-label, .menu-sep')) kid.hidden = !!at.query;
    if (!any) {
        closeSlash(e);
        return;
    }
    const wasOpen = !e.slash.hidden;
    e.slashOpen = at;
    if (!wasOpen) {
        e.slash.setAttribute('role', 'listbox');
        for (const item of slashItems(e)) {
            item.setAttribute('role', 'option');
            if (!item.id) item.id = 'sedna-slash-' + (++slashIds);
        }
        if (!e.slash.id) e.slash.id = 'sedna-slash-menu-' + (++slashIds);
        e.slash.hidden = false;
        e.quill.root.setAttribute('aria-controls', e.slash.id);
    }
    const shown = shownItems(e);
    const current = shown.find(item => item.hasAttribute('data-active'));
    setActive(e, current || shown[0]);
    placeSlash(e, at.start);
}

function applySlash(e, item) {
    const at = e.slashOpen;
    if (!at || !item) return;
    const q = e.quill;
    const caret = (q.getSelection() || { index: at.end }).index;
    const name = item.getAttribute('data-editor-block');
    const raw = item.getAttribute('value');
    closeSlash(e);
    q.deleteText(at.start, Math.max(0, caret - at.start), 'user');
    q.setSelection(at.start, 0, 'silent');
    if (name === 'paragraph') {
        for (const f of BLOCKS) if (e.formats.includes(f)) q.formatLine(at.start, 0, f, false, 'user');
    } else {
        const value = name === 'list' && raw === 'check' ? 'unchecked' : (raw || true);
        q.formatLine(at.start, 0, name, value, 'user');
    }
    q.setSelection(at.start, 0, 'silent');
    reflect(e);
}

// Where a change left the caret: after the last text it inserted, or null.
function caretAfter(delta) {
    let index = 0, after = null;
    for (const op of delta.ops || []) {
        if (typeof op.retain === 'number') index += op.retain;
        else if (typeof op.insert === 'string') { index += op.insert.length; after = index; }
        else if (op.insert) { index += 1; after = index; }
    }
    return after;
}

function moveSlash(e, step) {
    const shown = shownItems(e);
    if (!shown.length) return;
    const i = shown.findIndex(item => item.hasAttribute('data-active'));
    setActive(e, shown[(i + step + shown.length) % shown.length]);
}

function wireSlash(e) {
    if (!e.slash) return;
    e.slash.hidden = true;
    const on = (target, type, fn, opts) => {
        target.addEventListener(type, fn, opts);
        e.cleanups.push(() => target.removeEventListener(type, fn, opts));
    };
    const q = e.quill;
    q.on('text-change', (delta, old, source) => { if (source === 'user') updateSlash(e, true, caretAfter(delta)); });
    q.on('selection-change', range => {
        if (!range) closeSlash(e);
        else if (!e.slash.hidden) updateSlash(e, false);
    });

    // Before Quill's own keys, which listen on the document below this frame.
    on(e.el, 'keydown', ev => {
        if (e.slash.hidden || ev.isComposing || ev.target !== q.root) return;
        const plain = !ev.altKey && !ev.ctrlKey && !ev.metaKey && !ev.shiftKey;
        if (ev.key === 'ArrowDown' && plain) moveSlash(e, 1);
        else if (ev.key === 'ArrowUp' && plain) moveSlash(e, -1);
        else if ((ev.key === 'Enter' || ev.key === 'Tab') && plain) {
            applySlash(e, shownItems(e).find(item => item.hasAttribute('data-active')));
        } else if (ev.key === 'Escape') {
            e.slashDismissed = e.slashOpen ? e.slashOpen.start : null;
            closeSlash(e);
        } else return;
        ev.preventDefault();
        ev.stopPropagation();
    }, true);

    // A press on an item leaves the focus and the caret in the document.
    on(e.slash, 'mousedown', ev => { if (ev.target.closest('.menu-item')) ev.preventDefault(); });
    on(e.slash, 'click', ev => {
        const item = ev.target.closest('.menu-item[data-editor-block]');
        if (item && slashItems(e).includes(item) && !item.hidden) applySlash(e, item);
    });
    on(e.slash, 'pointermove', ev => {
        const item = ev.target.closest('.menu-item[data-editor-block]');
        if (item && !item.hidden && !item.hasAttribute('data-active')) setActive(e, item);
    });
}

/* ── 50-media.js ──────────────────────────────────────────────── */
/* ── Images and videos ────────────────────────────────────────────────────────
   An image is `<img src alt>` inside a line, as Quill writes it; a video is a block of
   its own, `<video controls src>` — never autoplaying, never an iframe. Both are formats
   like any other: an editor holds them only when its toolbar offers them —

     <button … data-editor-format="image" aria-label="Image"><i class="ri-image-line"></i></button>
     <button … data-editor-format="video" aria-label="Video"><i class="ri-video-line"></i></button>

   — or `data-editor-formats` names them, so a pasted picture is cut away from an editor
   that cannot take one. What is kept is an address the editor can show: http, https or
   a relative one. A `data:` image, a `javascript:` anything and a file pasted or dropped
   into the document are not kept. Uploading is the app's: the app puts the file where it
   can be fetched and hands the editor the address.

   A media control opens the app's own form, written once inside the editor and placed
   at the caret as the link form is:

     <div class="editor-media" data-editor-media hidden>
       <input class="form-input" type="url" data-editor-media-url aria-label="Address" />
       <input class="form-input" type="text" data-editor-media-alt aria-label="Description" />
       <button class="btn btn--sm" type="button" data-editor-media-save>Insert</button>
       <button class="btn btn--ghost btn--sm" type="button" data-editor-media-cancel>Cancel</button>
     </div>

   It says which it is for in `data-editor-media-kind` — image or video — and shows only
   the elements inside it whose `data-editor-media-for` names that kind, or that carry
   none: a field marked for one kind is the field for that kind. Opened on an image or a video, it is filled with its address and description and
   Insert replaces it. An address the editor would not keep marks the address field
   `aria-invalid` and leaves the form open.

   Before the form opens, the editor sends `sedna-editor-media`, cancelable, with
   `{ kind, src, alt }` — the media under the caret, or empty strings. An app that would
   rather open its own dialog — with an upload in it — cancels it and answers with
   `insertImage(url, alt)` or `insertVideo(url, label)`, which put the media where the
   caret was.

   A file pasted or dropped in sends `sedna-editor-file`, cancelable: an app that takes
   the files cancels it. One that is not taken — not cancelled, or not an image or a video
   the editor knows — shows the app's own notice, written once inside the editor, so the
   reader knows why nothing appeared:

     <p class="editor-notice form-warning" role="alert" data-editor-file-notice hidden>
       <i class="ri-image-line" aria-hidden="true"></i> Pictures cannot be pasted here. Add them as attachments.
     </p>

   It hides again at the next change to the document, or the next file that is taken.
   ─────────────────────────────────────────────────────────────────────────── */

const Delta = Quill.import('delta');
const MEDIA = ['image', 'video'];

/* The address as given, if it is one the editor keeps — http, https or relative, resolved
   the way the browser would resolve it — or null. */
function mediaUrl(url) {
    const s = String(url ?? '').trim();
    if (!s) return null;
    try {
        const { protocol } = new URL(s, document.baseURI);
        return protocol === 'http:' || protocol === 'https:' ? s : null;
    } catch (x) {
        return null;
    }
}

/* Quill's image, keeping only the addresses above: anything else is written as Quill
   writes an address it refuses, and the clipboard below has already dropped it. */
const QuillImage = Quill.import('formats/image');
class SednaImage extends QuillImage {
    static sanitize(url) { return mediaUrl(url) ?? '//:0'; }
}

/* A video the browser plays itself. Quill's own `video` is an iframe — a page from
   anywhere, framed inside the document — so this replaces it under the same name. */
const BlockEmbed = Quill.import('blots/block/embed');
const VIDEO_ATTRIBUTES = ['width', 'height', 'poster', 'aria-label'];
class SednaVideo extends BlockEmbed {
    static blotName = 'video';
    static tagName = 'VIDEO';

    static create(value) {
        const node = super.create(value);
        node.setAttribute('controls', '');
        node.setAttribute('src', mediaUrl(value) ?? '//:0');
        return node;
    }

    // `<video src>`, or the first `<source src>` inside it — which is written back as `src`.
    static value(node) {
        return node.getAttribute('src') || node.querySelector('source[src]')?.getAttribute('src') || '';
    }

    static formats(node) {
        return VIDEO_ATTRIBUTES.reduce((f, name) => {
            if (node.hasAttribute(name)) f[name] = node.getAttribute(name);
            return f;
        }, {});
    }

    format(name, value) {
        if (!VIDEO_ATTRIBUTES.includes(name)) return super.format(name, value);
        if (name === 'poster' && value) value = mediaUrl(value);
        if (value) this.domNode.setAttribute(name, value);
        else this.domNode.removeAttribute(name);
    }
}

Quill.register({ 'formats/image': SednaImage, 'formats/video': SednaVideo }, true);

/* What arrives as HTML — a paste, the textarea's value — loses an image or a video whose
   address the editor would not keep, rather than keeping a broken one. */
function dropUnsafe(kind) {
    return (node, delta) => {
        const src = kind === 'image' ? node.getAttribute('src') : SednaVideo.value(node);
        return mediaUrl(src) ? delta : new Delta();
    };
}
const MEDIA_MATCHERS = [['img', dropUnsafe('image')], ['video', dropUnsafe('video')]];

/* A file pasted or dropped into the document is not inlined: uploading is the app's. The
   editor says a file arrived — `sedna-editor-file`, with an id per file — and keeps the
   File under that id, where the app reads it (handle.file(id); from C#,
   ISednaEditors.OpenFileAsync) and answers with insertImage once it is uploaded. A paste
   that carries HTML as well — a picture copied from a page — is pasted as HTML, so its
   address is kept rather than the file.

   The app says it took them by cancelling the event. What it did not take — and a file of
   any other type, which the event never names — shows the notice. */
const FILE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp', 'image/avif', 'image/svg+xml',
    'video/mp4', 'video/webm'];
const FILES_KEPT = 20;
let fileSeq = 0;

/* Quill's uploader hands on only the types it lists, and drops every other file in
   silence. This one hands on all of them, so the handler can say which were not taken. */
const QuillUploader = Quill.import('modules/uploader');
class SednaUploader extends QuillUploader {
    upload(range, files) {
        const all = Array.from(files || []).filter(Boolean);
        if (all.length) this.options.handler.call(this, range, all);
    }
}
Quill.register({ 'modules/uploader': SednaUploader }, true);

function uploaderFor(e) {
    return {
        mimetypes: FILE_TYPES,
        handler: (range, files) => {
            const kept = files.filter(file => FILE_TYPES.includes(file.type));
            let taken = false;
            if (kept.length) {
                const listed = kept.map(file => {
                    const id = 'file-' + (++fileSeq);
                    e.files.set(id, file);
                    return { id, name: file.name || '', type: file.type || '', size: file.size || 0 };
                });
                while (e.files.size > FILES_KEPT) e.files.delete(e.files.keys().next().value);
                if (range) e.lastRange = range;
                taken = !e.emit('sedna-editor-file', { files: listed, index: range ? range.index : null }, true);
            }
            notify(e, !taken || kept.length < files.length);
        },
    };
}

/* Shows the app's notice, or hides it. Shown again while it is already showing, it is
   hidden for a frame first, so a screen reader hears the alert a second time. */
function notify(e, show) {
    const notice = e.notice;
    if (!notice) return;
    cancelAnimationFrame(e.noticeFrame);
    if (!show) {
        notice.hidden = true;
        return;
    }
    if (notice.hidden) {
        notice.hidden = false;
        return;
    }
    notice.hidden = true;
    e.noticeFrame = requestAnimationFrame(() => { if (!e.disposed) notice.hidden = false; });
}

function fileOf(e, id) {
    return e.files.get(id) ?? null;
}

function wirePaste(e) {
    const q = e.quill;
    const onPaste = ev => {
        const data = ev.clipboardData;
        const html = data?.getData('text/html');
        if (ev.defaultPrevented || !q.isEnabled() || !html || !data.files?.length) return;
        ev.preventDefault();
        const range = q.getSelection(true);
        if (range) q.clipboard.onPaste(range, { html, text: data.getData('text/plain') });
    };
    q.root.addEventListener('paste', onPaste, true);
    e.cleanups.push(() => q.root.removeEventListener('paste', onPaste, true));
}

// The image or video the selection is exactly, as its kind, index, address and description — or null.
function mediaAt(q, range) {
    if (!range || range.length !== 1) return null;
    const op = q.getContents(range.index, 1).ops[0];
    if (!op || typeof op.insert !== 'object') return null;
    const kind = MEDIA.find(k => k in op.insert);
    if (!kind) return null;
    const alt = op.attributes?.[kind === 'image' ? 'alt' : 'aria-label'] ?? '';
    return { kind, index: range.index, src: String(op.insert[kind] ?? ''), alt };
}

/* Puts an image or a video in place of the range — the caret, the media it is on — and
   the caret after it. Null when the editor does not take that kind or the address is not
   one it keeps. */
function insertMedia(e, kind, url, alt, range) {
    const q = e.quill;
    if (!q.scroll.query(kind)) {
        warnOnce('media:' + kind, `this editor does not take a ${kind}: offer data-editor-format="${kind}" or name it in data-editor-formats.`);
        return null;
    }
    const src = mediaUrl(url);
    if (!src) return null;
    const at = range || q.getSelection() || e.lastRange || { index: q.getLength() - 1, length: 0 };
    const text = String(alt ?? '').trim();
    q.updateContents(new Delta().retain(at.index).delete(at.length).insert({ [kind]: src }), 'user');
    // A video put in the middle of a line starts a line of its own, one place on — and Quill
    // formats the place it was asked for, so the description is written once it is there.
    const index = [at.index, at.index + 1].find(i => {
        const op = q.getContents(i, 1).ops[0];
        return op && typeof op.insert === 'object' && op.insert[kind] === src;
    }) ?? at.index;
    if (text) q.formatText(index, 1, kind === 'image' ? 'alt' : 'aria-label', text, 'user');
    q.setSelection(index + 1, 0, 'silent');
    e.lastRange = { index: index + 1, length: 0 };
    return stateOf(e);
}

// The form's field for this kind: the first one not set aside for the other kind.
function fieldOf(panel, name, kind) {
    return Array.from(panel.querySelectorAll(`[${name}]`)).find(f => {
        const only = f.closest('[data-editor-media-for]');
        return !only || only.getAttribute('data-editor-media-for') === kind;
    }) || null;
}

function openMedia(e, kind) {
    const q = e.quill;
    const range = q.getSelection(true) || e.lastRange;
    if (!range) return;
    const on = mediaAt(q, range);
    const existing = on && on.kind === kind ? on : null;
    const asked = e.emit('sedna-editor-media', { kind, src: existing?.src ?? '', alt: existing?.alt ?? '' }, true);
    const panel = e.media;
    if (!asked || !panel) {
        if (asked) warnOnce('media', 'an image or video control needs the app\'s [data-editor-media] form inside the editor, or a sedna-editor-media listener that cancels the event and inserts.');
        return;
    }
    e.mediaing = { kind, range };
    panel.setAttribute('data-editor-media-kind', kind);
    for (const part of panel.querySelectorAll('[data-editor-media-for]')) {
        part.hidden = part.getAttribute('data-editor-media-for') !== kind;
    }
    const url = fieldOf(panel, 'data-editor-media-url', kind);
    const alt = fieldOf(panel, 'data-editor-media-alt', kind);
    if (url) {
        url.value = existing ? existing.src : '';
        url.removeAttribute('aria-invalid');
    }
    if (alt) alt.value = existing ? existing.alt : '';
    panel.hidden = false;
    place(e, panel, range);
    (url || panel).focus({ preventScroll: true });
}

function closeMedia(e, refocus) {
    if (!e.media || e.media.hidden) return;
    e.media.hidden = true;
    const was = e.mediaing;
    e.mediaing = null;
    if (refocus) {
        e.quill.focus();
        if (was) e.quill.setSelection(was.range.index + was.range.length, 0, 'silent');
    }
}

function saveMedia(e) {
    const was = e.mediaing;
    if (!was) return;
    const url = fieldOf(e.media, 'data-editor-media-url', was.kind);
    const alt = fieldOf(e.media, 'data-editor-media-alt', was.kind);
    const given = (url?.value || '').trim();
    if (!given) return closeMedia(e, true);
    if (!mediaUrl(given)) {
        if (url) {
            url.setAttribute('aria-invalid', 'true');
            url.focus();
        }
        return;
    }
    e.mediaing = null;
    e.media.hidden = true;
    e.quill.focus();
    insertMedia(e, was.kind, given, alt?.value, was.range);
    reflect(e);
}

function wireMedia(e) {
    wirePaste(e);
    const panel = e.media;
    if (!panel) return;
    const on = (target, type, fn, opts) => {
        target.addEventListener(type, fn, opts);
        e.cleanups.push(() => target.removeEventListener(type, fn, opts));
    };
    on(panel, 'click', ev => {
        if (ev.target.closest('[data-editor-media-save]')) saveMedia(e);
        else if (ev.target.closest('[data-editor-media-cancel]')) closeMedia(e, true);
    });
    on(panel, 'keydown', ev => {
        if (ev.key === 'Enter' && ev.target.matches('input')) {
            ev.preventDefault();
            saveMedia(e);
        } else if (ev.key === 'Escape') {
            ev.preventDefault();
            ev.stopPropagation();
            closeMedia(e, true);
        }
    });
    on(panel, 'input', ev => {
        if (ev.target.matches('[data-editor-media-url]')) ev.target.removeAttribute('aria-invalid');
    });
    // A press anywhere else closes it, leaving the focus where the press put it.
    on(document, 'pointerdown', ev => {
        if (!panel.hidden && !panel.contains(ev.target) && !ev.target.closest?.('[data-editor-format="image"],[data-editor-format="video"]')) closeMedia(e, false);
    }, true);
}

/* The range an insert from the app replaces: the open media form's, which it then closes,
   or none — the caret, as insertMedia finds it. */
function takeMediaRange(e) {
    const was = e.mediaing;
    if (!was) return null;
    closeMedia(e, false);
    return was.range;
}

/* ── 70-editor.js ──────────────────────────────────────────────── */
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
     sedna-editor-media    { kind, src, alt }, cancelable, before the media form opens
     sedna-editor-file     { files, index }, cancelable — cancelled when the app takes them

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
    const { value, body, toolbar, link, media, slash, notice } = readMarkup(e.el);
    if (!value || !body) {
        e.el.setAttribute('data-editor-state', 'error');
        throw new Error('a [data-editor] element needs a <textarea data-editor-value> and a <div class="editor-body"> inside it.');
    }
    Object.assign(e, { value, body, toolbar, link, media, slash, notice, controls: controlsOf(e.el) });
    e.el.setAttribute('data-editor-state', 'loading');

    const formats = formatsOf(e.el);
    e.formats = formats;
    e.quill = new Quill(body, {
        formats,
        modules: {
            toolbar: false,
            history: { delay: 800, maxStack: 200, userOnly: true },
            clipboard: { matchers: MEDIA_MATCHERS },
            uploader: uploaderFor(e),
            // Only an editor that takes tables loads the module, so one that does not
            // still cuts a pasted table down to its text.
            table: formats.includes('table'),
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
    wireMedia(e);
    wireSlash(e);

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
        /* An image or a video where the caret was — or in place of the one it is on — with
           its description. Null, and nothing inserted, when the editor does not take that
           kind or the address is not http, https or relative. */
        insertImage: (url, alt) => insertMedia(e, 'image', url, alt, takeMediaRange(e)),
        insertVideo: (url, label) => insertMedia(e, 'video', url, label, takeMediaRange(e)),
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
        case 'image': return api.insertImage(a[0], a[1]);
        case 'video': return api.insertVideo(a[0], a[1]);
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

/* ── 99-exports.js ──────────────────────────────────────────────── */
/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two. A
   script reaches an editor through `sednaUi.editor.get(el)`, which returns the handle
   `attach` resolves — never through an import of this file.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };


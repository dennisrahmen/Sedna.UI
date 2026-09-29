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
const ACTIONS = ['undo', 'redo', 'clean', 'indent', 'outdent'];
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

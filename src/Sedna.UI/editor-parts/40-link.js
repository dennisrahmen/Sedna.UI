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

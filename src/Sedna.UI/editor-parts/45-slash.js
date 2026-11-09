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

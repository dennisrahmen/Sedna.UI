/* ── Mentions ─────────────────────────────────────────────────────────────────
   "@" in the document — at the start of a line or after a space — opens the app's own list of
   who can be named, and the one chosen is written into the text as a mention. The list is a
   `.menu` the app writes once inside the editor, as it writes the slash menu, and names
   with `data-editor-mention`:

     <div class="menu editor-mention" data-editor-mention hidden aria-label="People">
       <button class="menu-item" type="button" tabindex="-1" data-mention-value="alex.fischer" data-label="Alex Fischer">
         <span class="avatar avatar--xs">AF</span><span>Alex Fischer</span>
       </button>
       <template data-editor-mention-item>
         <button class="menu-item" type="button" tabindex="-1"><span data-label></span><span data-note></span></button>
       </template>
     </div>

   What follows the "@" filters the items as it is typed, ranked by the palette's matcher
   against an item's `data-label` or its text and its `data-mention-value`; an item that does
   not match is `hidden`. Up and Down move the item marked `data-active`, Enter or Tab writes
   "@" and its label — as a mention, `<data value="…">@Label</data>`, then a space — Escape
   closes the list until another "@" is typed, and a press on an item does the same as Enter.
   The focus stays in the document, which points at the item with `aria-activedescendant`.

   Rows the app has not written come from its search: each time the word after the "@"
   changes the editor sends `sedna-editor-mention`, cancelable, with `{ query }`. An app
   that answers from a server cancels it, fills the list with `mentions([...])` — rows cloned
   from the `<template data-editor-mention-item>`, its `data-label` and `data-note` slots
   filled with text — and the editor then shows every row it was given, unfiltered.
   ─────────────────────────────────────────────────────────────────────────── */

const MENTION = /(?:^|\s)@([^\s@]{0,40})$/;
const SednaMention = (() => {
    const Inline = Quill.import('blots/inline');
    return class SednaMention extends Inline {
        static blotName = 'mention';
        static tagName = 'DATA';

        static create(value) {
            const node = super.create();
            node.setAttribute('value', String(value).slice(0, 200));
            return node;
        }

        static formats(node) { return node.getAttribute('value'); }

        format(name, value) {
            if (name !== this.statics.blotName || !value) super.format(name, value);
            else this.domNode.setAttribute('value', String(value).slice(0, 200));
        }
    };
})();
Quill.register({ 'formats/mention': SednaMention }, true);

let mentionIds = 0;

function mentionItems(e) {
    if (!e.mention) return [];
    return Array.from(e.mention.querySelectorAll('.menu-item[data-mention-value]'))
        .filter(item => item.closest('[data-editor-mention]') === e.mention && !item.disabled && item.getAttribute('aria-disabled') !== 'true');
}

const mentionLabel = item => String(item.getAttribute('data-label') || item.textContent || '').trim();

function mentionAt(e, caret) {
    const q = e.quill;
    const range = caret == null ? q.getSelection() : { index: caret, length: 0 };
    if (!range || range.length) return null;
    const [line, offset] = q.getLine(range.index);
    if (!line) return null;
    let format = {};
    try { format = q.getFormat(range.index, 0); } catch (x) { format = {}; }
    if (format['code-block'] || format.code) return null;
    const m = MENTION.exec(q.getText(range.index - offset, offset));
    if (!m) return null;
    return { start: range.index - m[1].length - 1, end: range.index, query: m[1] };
}

function setMentionActive(e, item) {
    for (const it of mentionItems(e)) {
        const on = it === item;
        it.toggleAttribute('data-active', on);
        it.setAttribute('aria-selected', on ? 'true' : 'false');
    }
    if (item) {
        e.quill.root.setAttribute('aria-activedescendant', item.id);
        item.scrollIntoView({ block: 'nearest' });
    } else if (e.slash?.hidden !== false) {
        e.quill.root.removeAttribute('aria-activedescendant');
    }
}

const shownMentions = e => mentionItems(e).filter(item => !item.hidden);

function closeMention(e) {
    if (!e.mention || e.mention.hidden) return;
    e.mention.hidden = true;
    e.mentionOpen = null;
    setMentionActive(e, null);
    e.quill.root.removeAttribute('aria-controls');
}

function placeMention(e, index) {
    const menu = e.mention;
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

// Opens, filters or closes the list for where the caret is now; only typing opens it.
function updateMention(e, typed, caret) {
    if (!e.mention || e.disposed) return;
    const at = mentionAt(e, caret);
    if (!at || !e.formats.includes('mention')) {
        e.mentionDismissed = null;
        closeMention(e);
        return;
    }
    if (e.mentionDismissed === at.start || (!typed && e.mention.hidden)) return;
    // The app that answers from a server cancels this and fills the list itself.
    const own = e.mentionQuery !== at.query || e.mention.hidden;
    let appAnswers = e.mentionAppAnswers;
    if (own && typed) {
        e.mentionQuery = at.query;
        appAnswers = !e.emit('sedna-editor-mention', { query: at.query }, true);
        e.mentionAppAnswers = appAnswers;
    }
    const score = window.sednaUi?._?.score;
    let any = false;
    for (const item of mentionItems(e)) {
        const match = appAnswers || !at.query || !score
            || score(at.query, mentionLabel(item), 0) >= 0
            || score(at.query, item.getAttribute('data-mention-value') || '', 0) >= 0;
        item.hidden = !match;
        if (match) any = true;
    }
    if (!any) {
        closeMention(e);
        return;
    }
    const wasOpen = !e.mention.hidden;
    e.mentionOpen = at;
    if (!wasOpen) {
        e.mention.setAttribute('role', 'listbox');
        if (!e.mention.id) e.mention.id = 'sedna-mention-menu-' + (++mentionIds);
        e.mention.hidden = false;
        e.quill.root.setAttribute('aria-controls', e.mention.id);
    }
    for (const item of mentionItems(e)) {
        item.setAttribute('role', 'option');
        if (!item.id) item.id = 'sedna-editor-mention-' + (++mentionIds);
    }
    const shown = shownMentions(e);
    setMentionActive(e, shown.find(item => item.hasAttribute('data-active')) || shown[0]);
    placeMention(e, at.start);
}

function applyMention(e, item) {
    const at = e.mentionOpen;
    if (!at || !item) return;
    const q = e.quill;
    const caret = (q.getSelection() || { index: at.end }).index;
    const label = '@' + mentionLabel(item);
    const value = item.getAttribute('data-mention-value');
    closeMention(e);
    q.history.cutoff();
    q.updateContents(new Delta().retain(at.start).delete(Math.max(0, caret - at.start)).insert(label, { mention: value }).insert(' '), 'user');
    q.setSelection(at.start + label.length + 1, 0, 'silent');
    q.history.cutoff();
    reflect(e);
}

/* Rows from the app's search: replaces the rows an earlier call made with one per entry —
   { value, label, note, icon } — cloned from the app's template. Returns how many it made. */
function setMentions(e, list) {
    const menu = e.mention;
    const template = menu?.querySelector('template[data-editor-mention-item]');
    if (!menu || !template) {
        warnOnce('mentions', 'mentions() needs a [data-editor-mention] menu with a <template data-editor-mention-item> inside the editor.');
        return 0;
    }
    for (const old of menu.querySelectorAll('[data-editor-mention-made]')) old.remove();
    let made = 0;
    for (const entry of Array.isArray(list) ? list : []) {
        const value = String(entry?.value ?? '').trim();
        if (!value) continue;
        const row = window.sednaUi._.fill(template, { label: entry.label ?? value, note: entry.note, icon: entry.icon });
        if (!row) continue;
        row.setAttribute('data-mention-value', value);
        row.setAttribute('data-label', String(entry.label ?? value));
        row.setAttribute('data-editor-mention-made', '');
        menu.append(row);
        made++;
    }
    if (!menu.hidden && e.mentionOpen) updateMention(e, true);
    return made;
}

function wireMention(e) {
    if (!e.mention) return;
    e.mention.hidden = true;
    const on = (target, type, fn, opts) => {
        target.addEventListener(type, fn, opts);
        e.cleanups.push(() => target.removeEventListener(type, fn, opts));
    };
    const q = e.quill;
    q.on('text-change', (delta, old, source) => { if (source === 'user') updateMention(e, true, caretAfter(delta)); });
    q.on('selection-change', range => {
        if (!range) closeMention(e);
        else if (!e.mention.hidden) updateMention(e, false);
    });
    on(e.el, 'keydown', ev => {
        if (e.mention.hidden || ev.isComposing || ev.target !== q.root) return;
        const plain = !ev.altKey && !ev.ctrlKey && !ev.metaKey && !ev.shiftKey;
        const shown = shownMentions(e);
        const move = step => {
            const i = shown.findIndex(item => item.hasAttribute('data-active'));
            if (shown.length) setMentionActive(e, shown[(i + step + shown.length) % shown.length]);
        };
        if (ev.key === 'ArrowDown' && plain) move(1);
        else if (ev.key === 'ArrowUp' && plain) move(-1);
        else if ((ev.key === 'Enter' || ev.key === 'Tab') && plain) applyMention(e, shown.find(item => item.hasAttribute('data-active')));
        else if (ev.key === 'Escape') {
            e.mentionDismissed = e.mentionOpen ? e.mentionOpen.start : null;
            closeMention(e);
        } else return;
        ev.preventDefault();
        ev.stopPropagation();
    }, true);
    on(e.mention, 'mousedown', ev => { if (ev.target.closest('.menu-item')) ev.preventDefault(); });
    on(e.mention, 'click', ev => {
        const item = ev.target.closest('.menu-item[data-mention-value]');
        if (item && mentionItems(e).includes(item) && !item.hidden) applyMention(e, item);
    });
    on(e.mention, 'pointermove', ev => {
        const item = ev.target.closest('.menu-item[data-mention-value]');
        if (item && !item.hidden && !item.hasAttribute('data-active')) setMentionActive(e, item);
    });
}

/*! Sedna.UI — generated, comments stripped. The commented copy ships in the package as readable/Sedna.UI.editor.js. */
import Quill from '../lib/quill/quill.js';
const FORMATS = ['bold', 'italic', 'underline', 'strike', 'code', 'script', 'link', 'image', 'video',
    'header', 'list', 'blockquote', 'code-block', 'align', 'indent', 'color', 'background', 'table'];
const TABLE_PARTS = ['table-row', 'table-body', 'table-container'];
const CONTROL = '[data-editor-format],[data-editor-action]';
const warned = new Set();
function warnOnce(key, message) {
    if (warned.has(key)) return;
    warned.add(key);
    try { console.warn('Sedna.UI editor: ' + message); } catch (e) { /* ignore */ }
}
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
const VALUE = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value');
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
    e.written = [];
    reflect(e);
}
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
function left(e) {
    if (!e.changed) return;
    e.changed = false;
    e.value.dispatchEvent(new Event('change', { bubbles: true }));
}
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
            if (MEDIA.includes(name)) continue;
            if (c.tagName === 'SELECT') {
                const now = format[name];
                c.value = now === undefined || now === false || now === null ? '' : String(now);
            } else if (c.tagName === 'INPUT') {
                const now = format[name];
                if (typeof now === 'string' && /^#[0-9a-f]{6}$/i.test(now)) c.value = now;
            } else {
                c.setAttribute('aria-pressed', isOn(format, name, c.getAttribute('value')) ? 'true' : 'false');
            }
            continue;
        }
        const action = c.getAttribute('data-editor-action');
        const inTable = !!format.table;
        const idle = action === 'undo' ? q.history.stack.undo.length === 0
            : action === 'redo' ? q.history.stack.redo.length === 0
                : action === 'table-insert' ? inTable
                    : action && action.startsWith('table-') ? !inTable : false;
        c.disabled = idle || !q.isEnabled();
    }
    keepStop(e);
}
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
    on(e.el, 'mousedown', ev => {
        const c = ev.target.closest(CONTROL);
        if (c && c.tagName === 'BUTTON' && e.controls.includes(c)) ev.preventDefault();
    });
    if (!e.toolbar) return;
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
function toToolbar(e) {
    if (!e.toolbar) return true;
    const stop = e.controls.find(c => e.toolbar.contains(c) && c.tabIndex === 0 && !c.disabled)
        || e.controls.find(c => e.toolbar.contains(c) && !c.disabled);
    if (!stop) return true;
    stop.focus();
    return false;
}
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
    on(document, 'pointerdown', ev => {
        if (!panel.hidden && !panel.contains(ev.target) && !ev.target.closest?.('[data-editor-format="link"]')) closeLink(e, false);
    }, true);
}
const BLOCKS = ['header', 'list', 'blockquote', 'code-block', 'align'];
const SLASH = /(?:^|\s)\/([^/\n]{0,32})$/;
let slashIds = 0;
function slashItems(e) {
    if (!e.slash) return [];
    return Array.from(e.slash.querySelectorAll('.menu-item[data-editor-block]'))
        .filter(item => item.closest('[data-editor-slash]') === e.slash);
}
function slashUsable(e, item) {
    const name = item.getAttribute('data-editor-block');
    if (name === 'paragraph') return true;
    if (BLOCKS.includes(name) && e.formats.includes(name)) return true;
    warnOnce('block:' + name, `the slash menu offers "${name}", which this editor does not accept; the item is not shown.`);
    return false;
}
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
const Delta = Quill.import('delta');
const MEDIA = ['image', 'video'];
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
const QuillImage = Quill.import('formats/image');
class SednaImage extends QuillImage {
    static sanitize(url) { return mediaUrl(url) ?? '//:0'; }
}
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
function dropUnsafe(kind) {
    return (node, delta) => {
        const src = kind === 'image' ? node.getAttribute('src') : SednaVideo.value(node);
        return mediaUrl(src) ? delta : new Delta();
    };
}
const MEDIA_MATCHERS = [['img', dropUnsafe('image')], ['video', dropUnsafe('video')]];
const FILE_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp', 'image/avif', 'image/svg+xml',
    'video/mp4', 'video/webm'];
const FILES_KEPT = 20;
let fileSeq = 0;
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
function mediaAt(q, range) {
    if (!range || range.length !== 1) return null;
    const op = q.getContents(range.index, 1).ops[0];
    if (!op || typeof op.insert !== 'object') return null;
    const kind = MEDIA.find(k => k in op.insert);
    if (!kind) return null;
    const alt = op.attributes?.[kind === 'image' ? 'alt' : 'aria-label'] ?? '';
    return { kind, index: range.index, src: String(op.insert[kind] ?? ''), alt };
}
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
    const index = [at.index, at.index + 1].find(i => {
        const op = q.getContents(i, 1).ops[0];
        return op && typeof op.insert === 'object' && op.insert[kind] === src;
    }) ?? at.index;
    if (text) q.formatText(index, 1, kind === 'image' ? 'alt' : 'aria-label', text, 'user');
    q.setSelection(index + 1, 0, 'silent');
    e.lastRange = { index: index + 1, length: 0 };
    return stateOf(e);
}
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
    on(document, 'pointerdown', ev => {
        if (!panel.hidden && !panel.contains(ev.target) && !ev.target.closest?.('[data-editor-format="image"],[data-editor-format="video"]')) closeMedia(e, false);
    }, true);
}
function takeMediaRange(e) {
    const was = e.mediaing;
    if (!was) return null;
    closeMedia(e, false);
    return was.range;
}
const editors = new Map();
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
            table: formats.includes('table'),
            keyboard: {
                bindings: {
                    toolbar: { key: 'F10', altKey: true, handler: () => toToolbar(e) },
                    link: { key: 'k', shortKey: true, handler: () => { if (e.link) openLink(e); return !e.link; } },
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
        if (source === 'user') notify(e, false);
    });
    q.on('editor-change', () => reflect(e));
    q.on('selection-change', range => { if (range) e.lastRange = range; });
    const onFocusOut = ev => { if (!e.el.contains(ev.relatedTarget)) left(e); };
    e.el.addEventListener('focusout', onFocusOut);
    e.cleanups.push(() => e.el.removeEventListener('focusout', onFocusOut));
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
        get quill() { return e.quill; },
        state: () => stateOf(e),
        html: () => htmlOf(e.quill),
        set: html => {
            e.quill.setContents(e.quill.clipboard.convert({ html: html || '' }), 'user');
            return stateOf(e);
        },
        insert: text => {
            const range = e.quill.getSelection() || e.lastRange || { index: e.quill.getLength() - 1, length: 0 };
            e.quill.insertText(range.index, String(text ?? ''), 'user');
            e.quill.setSelection(range.index + String(text ?? '').length, 0, 'silent');
            return stateOf(e);
        },
        insertImage: (url, alt) => insertMedia(e, 'image', url, alt, takeMediaRange(e)),
        insertVideo: (url, label) => insertMedia(e, 'video', url, label, takeMediaRange(e)),
        focus: () => e.quill.focus(),
        enable: on => {
            e.value.disabled = on === false;
            describe(e);
        },
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
        case 'file': return api.file(a[0]);
        default:
            warnOnce('invoke:' + method, `"${method}" is not an editor method.`);
            return null;
    }
}
function attach(el) {
    let e = editors.get(el);
    if (!e) {
        e = { el, written: [], files: new Map(), cleanups: [], disposed: false, changed: false, loading: false, lastRange: null };
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
function sweep() {
    for (const e of [...editors.values()]) if (!e.el.isConnected) dispose(e);
}
export { attach, sweep };

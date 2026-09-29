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

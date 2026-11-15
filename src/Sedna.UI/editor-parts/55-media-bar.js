/* ── The bar on a chosen image or video ───────────────────────────────────────
   The app's own controls, written once inside the editor, which the editor shows above
   the image or video a click chose (50-media.js) and hides when it is let go:

     <div class="editor-media-bar" data-editor-media-bar role="toolbar" aria-label="Image" hidden>
       <button class="btn btn--ghost btn--sm" type="button" data-editor-media-size="240">Small</button>
       <button class="btn btn--ghost btn--sm" type="button" data-editor-media-size="">Original</button>
       <button class="btn btn--ghost btn--icon btn--sm" type="button" data-editor-media-align="start" aria-label="Float left"><i class="ri-align-left"></i></button>
       <button class="btn btn--ghost btn--sm" type="button" data-editor-media-edit>Edit</button>
       <button class="btn btn--ghost btn--sm" type="button" data-editor-media-remove>Remove</button>
     </div>

   A control is any element inside it with one of these:

     data-editor-media-size="240"   the width in pixels, the height cleared so the aspect
                                    ratio holds; "" is the natural size, both cleared
     data-editor-media-align="start"  start, end or center — "" for none: `data-align` on the media
     data-editor-media-edit         opens the media form, filled with it
     data-editor-media-remove       deletes it

   Size and align controls carry `aria-pressed` for what the media is now. An element
   marked `data-editor-media-for="image"` or `"video"` shows only for that kind. The bar is
   placed with a transform above the media — below it where there is no room above — and
   follows a scroll of the document. It is one tab stop: Alt+F10 in the document goes to it
   while a media is chosen, the arrows, Home and End move between its controls, and Escape
   comes back to the document with the media still chosen. A press with the pointer leaves
   the focus in the document.
   ─────────────────────────────────────────────────────────────────────────── */

const BAR_CONTROL = '[data-editor-media-size],[data-editor-media-align],[data-editor-media-edit],[data-editor-media-remove]';

function barControls(e) {
    if (!e.bar) return [];
    return Array.from(e.bar.querySelectorAll(BAR_CONTROL)).filter(c => !c.hidden && !c.closest('[data-editor-media-for][hidden]'));
}

// Shows, updates or hides the bar for the media now chosen. Called wherever the choice changes.
function syncBar(e) {
    const bar = e.bar;
    if (!bar || e.disposed) return;
    const node = e.pickedNode;
    const picked = pickedRange(e);
    if (!node || !picked) {
        bar.hidden = true;
        return;
    }
    for (const part of bar.querySelectorAll('[data-editor-media-for]')) {
        part.hidden = part.getAttribute('data-editor-media-for') !== e.picked.kind;
    }
    const width = node.getAttribute('width') || '';
    const align = node.getAttribute('data-align') || '';
    for (const c of bar.querySelectorAll('[data-editor-media-size]')) {
        const size = c.getAttribute('data-editor-media-size');
        c.setAttribute('aria-pressed', String(sizeOf(size) === (sizeOf(width) || '') && !node.hasAttribute('height')));
    }
    for (const c of bar.querySelectorAll('[data-editor-media-align]')) {
        c.setAttribute('aria-pressed', String(c.getAttribute('data-editor-media-align') === align));
    }
    bar.hidden = false;
    placeBar(e);
}

// Above the chosen media, or below it where the frame has no room above.
function placeBar(e) {
    const bar = e.bar;
    const node = e.pickedNode;
    if (!bar || bar.hidden || !node) return;
    const frame = e.el.getBoundingClientRect();
    const rect = node.getBoundingClientRect();
    bar.style.transform = 'translate(0px, 0px)';
    const own = bar.getBoundingClientRect();
    const rtl = getComputedStyle(e.quill.root).direction === 'rtl';
    let x = (rtl ? rect.right - own.width : rect.left) - frame.left;
    x = Math.max(8, Math.min(x, frame.width - own.width - 8));
    let y = rect.top - frame.top - own.height - 6;
    if (y < 8) y = Math.min(rect.bottom - frame.top + 6, frame.height - own.height - 8);
    bar.style.transform = `translate(${Math.round(x)}px, ${Math.round(y)}px)`;
}

function barAct(e, c) {
    const p = pickedRange(e);
    if (!p) return;
    const q = e.quill;
    const kind = e.picked.kind;
    if (c.hasAttribute('data-editor-media-edit')) {
        openMedia(e, kind);
        return;
    }
    // The document's own change lets the choice go; this one is the reader's, on the choice.
    e.keepPick = true;
    try {
        if (c.hasAttribute('data-editor-media-remove')) {
            e.keepPick = false;
            q.deleteText(p.index, 1, 'user');
            q.focus();
            return;
        }
        if (c.hasAttribute('data-editor-media-size')) {
            const width = sizeOf(c.getAttribute('data-editor-media-size'));
            if (width === null) return;
            q.formatText(p.index, 1, 'width', width || false, 'user');
            q.formatText(p.index, 1, 'height', false, 'user');
        } else if (c.hasAttribute('data-editor-media-align')) {
            q.formatText(p.index, 1, 'data-align', c.getAttribute('data-editor-media-align') || false, 'user');
        }
    } finally {
        e.keepPick = false;
    }
    markPicked(e);
}

// Alt+F10 while a media is chosen: to the bar's stop.
function toBar(e) {
    const list = barControls(e);
    if (!e.bar || e.bar.hidden || !list.length) return false;
    const stop = list.find(c => c.tabIndex === 0) || list[0];
    for (const c of list) c.tabIndex = c === stop ? 0 : -1;
    stop.focus();
    return true;
}

function wireBar(e) {
    const bar = e.bar;
    if (!bar) return;
    bar.hidden = true;
    const on = (target, type, fn, opts) => {
        target.addEventListener(type, fn, opts);
        e.cleanups.push(() => target.removeEventListener(type, fn, opts));
    };
    on(bar, 'click', ev => {
        const c = ev.target.closest(BAR_CONTROL);
        if (c && bar.contains(c) && !c.disabled) barAct(e, c);
    });
    // Pressed with the pointer, a control leaves the focus, and the media, where they were.
    on(bar, 'mousedown', ev => { if (ev.target.closest(BAR_CONTROL)) ev.preventDefault(); });
    on(bar, 'keydown', ev => {
        const list = barControls(e);
        const i = list.indexOf(ev.target);
        if (ev.key === 'Escape') {
            ev.preventDefault();
            ev.stopPropagation();
            const p = pickedRange(e);
            e.quill.focus();
            if (p && e.picked.kind === 'image') e.quill.setSelection(p.index, 1, 'silent');
            return;
        }
        if (i < 0) return;
        const rtl = getComputedStyle(bar).direction === 'rtl';
        const step = { ArrowRight: rtl ? -1 : 1, ArrowLeft: rtl ? 1 : -1 }[ev.key];
        let next = null;
        if (step) next = list[(i + step + list.length) % list.length];
        else if (ev.key === 'Home') next = list[0];
        else if (ev.key === 'End') next = list[list.length - 1];
        if (!next) return;
        ev.preventDefault();
        for (const c of list) c.tabIndex = c === next ? 0 : -1;
        next.focus();
    });
    // It follows the media as the document scrolls, and the frame as the window changes.
    on(e.quill.root, 'scroll', () => placeBar(e));
    on(window, 'resize', () => placeBar(e));
    // The one tab stop: Tab from the bar goes on in the page, as from the toolbar.
    for (const c of bar.querySelectorAll(BAR_CONTROL)) c.tabIndex = -1;
}

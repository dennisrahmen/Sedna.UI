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
       <button class="btn btn-sm" type="button" data-editor-media-save>Insert</button>
       <button class="btn btn-ghost btn-sm" type="button" data-editor-media-cancel>Cancel</button>
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

/* A file pasted or dropped into the document is not inlined: uploading is the app's. A
   paste that carries HTML as well — a picture copied from a page — is pasted as HTML, so
   its address is kept rather than the file. */
const NO_UPLOAD = { handler() { /* the app uploads; see the header */ } };

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

/* ── A signature pad ──────────────────────────────────────────────────────────
   `attach` starts one on the app's `[data-signature]`: a canvas inside its
   `[data-signature-pad]`, sized to it at the device's pixel ratio (capped at 3), and a
   probe for the ink. The app's field — `[data-signature-value]`, an input or a textarea
   — is the value, exactly as a textarea is the rich-text editor's:

     * the pad loads its strokes from the field when it starts;
     * a stroke finished, an undo and a clear write the field and send `input` then
       `change` on it, bubbling, so a form posts it and Blazor's `@bind` reads it;
     * a value written to the field from outside — by Blazor, or a script — is loaded,
       and a form reset puts the pad back with the field (a textarea; a hidden
       input keeps its value through a reset). A value this pad wrote a
       moment ago, arriving back, is its own echo and is ignored;
     * `disabled` or `readonly` on the field locks the pad: no stroke starts, and the
       undo and clear buttons are disabled.

   The app's `[data-signature-undo]` takes the last stroke away and its
   `[data-signature-clear]` every stroke; each is `disabled` while there is nothing to
   take. The element says where it is in `data-signature-state` — `ready`, or `error`
   when the markup lacks the pad or the field — and carries `data-signature-inked`
   while the pad holds a stroke.

   Drawing takes the primary pointer — a mouse's main button, a pen, a finger; a
   second finger is ignored, and the canvas's `touch-action: none` keeps a stroke
   from scrolling the page. A new size redraws the strokes, scaled to the width.
   `sweep` disposes every pad whose element has left the document.
   ─────────────────────────────────────────────────────────────────────────── */
const pads = new Set();
const MIN_STEP = 1.2;
const ECHO_MS = 3000;

function nativeValue(field) {
    const proto = field instanceof HTMLTextAreaElement ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
    return Object.getOwnPropertyDescriptor(proto, 'value');
}

function locked(p) { return p.value.disabled || p.value.hasAttribute('readonly'); }

function local(p, e) {
    const r = p.canvas.getBoundingClientRect();
    const k = WIDTH / (r.width || 1);
    const x = Math.min(WIDTH, Math.max(0, (e.clientX - r.left) * k));
    const y = Math.min(p.height, Math.max(0, (e.clientY - r.top) * k));
    return [round1(x), round1(y)];
}

/* The attributes the app's markup reads: inked, and which buttons have work to do. */
function reflect(p) {
    const any = p.strokes.length > 0, off = locked(p);
    p.el.toggleAttribute('data-signature-inked', any);
    p.el.querySelectorAll('[data-signature-undo], [data-signature-clear]').forEach(b => { b.disabled = off || !any; });
}

function remember(p, v) {
    const now = Date.now();
    p.written = p.written.filter(w => now - w.at < ECHO_MS);
    p.written.push({ v, at: now });
}

function isEcho(p, v) {
    const now = Date.now();
    return p.written.some(w => w.v === v && now - w.at < ECHO_MS);
}

/* Writes the strokes to the field and tells the page, as a reader's typing would. */
function commit(p) {
    reflect(p);
    const v = serialize(p.strokes);
    if (v === p.native.get.call(p.value)) return;
    remember(p, v);
    p.native.set.call(p.value, v);
    p.value.dispatchEvent(new Event('input', { bubbles: true }));
    p.value.dispatchEvent(new Event('change', { bubbles: true }));
}

/* Loads a value set from outside. One this pad cannot read leaves the pad empty and the
   field as it was, until the reader draws. */
function load(p, v) {
    p.drawing = null;
    p.strokes = parse(v) || [];
    paint(p);
    reflect(p);
}

function size(p) {
    // The canvas fills the pad inside its border; its own box is the drawing's.
    const r = p.canvas.getBoundingClientRect();
    const dpr = Math.min(3, window.devicePixelRatio || 1);
    p.cssWidth = r.width;
    p.height = r.width ? (WIDTH * r.height) / r.width : 0;
    p.dpr = dpr;
    p.canvas.width = Math.max(1, Math.round(r.width * dpr));
    p.canvas.height = Math.max(1, Math.round(r.height * dpr));
    paint(p);
}

function down(p, e) {
    if (p.drawing || locked(p) || !e.isPrimary || (e.pointerType === 'mouse' && e.button !== 0)) return;
    e.preventDefault();
    try { p.canvas.setPointerCapture(e.pointerId); } catch (x) { /* drawn without capture */ }
    p.drawing = { id: e.pointerId, points: [local(p, e)] };
    p.strokes.push(p.drawing.points);
    reflect(p);
    paint(p);
}

function extend(p, e) {
    const points = p.drawing.points, at = local(p, e), last = points[points.length - 1];
    if (Math.hypot(at[0] - last[0], at[1] - last[1]) >= MIN_STEP) points.push(at);
}

function move(p, e) {
    const d = p.drawing;
    if (!d || e.pointerId !== d.id) return;
    e.preventDefault();
    const coalesced = typeof e.getCoalescedEvents === 'function' ? e.getCoalescedEvents() : [];
    for (const each of coalesced.length ? coalesced : [e]) extend(p, each);
    paint(p);
}

function up(p, e) {
    const d = p.drawing;
    if (!d || e.pointerId !== d.id) return;
    if (e.type === 'pointerup') extend(p, e);
    p.drawing = null;
    paint(p);
    commit(p);
}

/* The app's undo and clear buttons, delegated once from the document. */
let delegated = false;
function delegate() {
    if (delegated) return;
    delegated = true;
    document.addEventListener('click', e => {
        const button = e.target && e.target.closest ? e.target.closest('[data-signature-undo], [data-signature-clear]') : null;
        const host = button && button.closest('[data-signature]');
        const p = host && host.__sednaSignature;
        if (!p || locked(p) || !p.strokes.length) return;
        p.drawing = null;
        if (button.hasAttribute('data-signature-clear')) p.strokes = [];
        else p.strokes.pop();
        paint(p);
        commit(p);
    });
}

function repaintAll() {
    for (const p of pads) {
        readInk(p);
        paint(p);
    }
}

let watching = false;
function watchTheWorld() {
    if (watching) return;
    watching = true;
    new MutationObserver(repaintAll).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ['class', 'style', 'data-theme', 'data-variant', 'data-cvd', 'data-contrast'],
    });
    for (const query of ['(prefers-color-scheme: dark)', '(forced-colors: active)', '(prefers-contrast: more)']) {
        matchMedia(query).addEventListener('change', repaintAll);
    }
}

const sizes = new ResizeObserver(entries => {
    for (const e of entries) {
        const p = e.target.__sednaSignaturePad;
        if (p) size(p);
    }
});

const fieldState = new MutationObserver(records => {
    for (const r of records) {
        const p = r.target.__sednaSignaturePad;
        if (!p) continue;
        if (locked(p) && p.drawing) up(p, { pointerId: p.drawing.id, type: 'pointercancel' });
        reflect(p);
    }
});

function watchValue(p) {
    const field = p.value, native = p.native;
    Object.defineProperty(field, 'value', {
        configurable: true,
        get() { return native.get.call(this); },
        set(v) {
            native.set.call(this, v);
            const value = v == null ? '' : String(v);
            if (p.disposed || value === serialize(p.strokes) || isEcho(p, value)) return;
            p.written = [];
            load(p, value);
        },
    });
    p.cleanups.push(() => { delete field.value; });

    const form = field.form;
    if (form) {
        const onReset = () => setTimeout(() => { if (!p.disposed) load(p, native.get.call(field)); }, 0);
        form.addEventListener('reset', onReset);
        p.cleanups.push(() => form.removeEventListener('reset', onReset));
    }
}

/* Starts the pad on an element the app wrote. Returns its handle, or null when the
   markup lacks the pad or the field. */
function attach(el) {
    if (el.__sednaSignature) return el.__sednaSignature.handle;
    const pad = el.querySelector('[data-signature-pad]');
    const value = el.querySelector('input[data-signature-value], textarea[data-signature-value]');
    if (!pad || !value) {
        el.setAttribute('data-signature-state', 'error');
        try { console.warn('Sedna.UI signature: a [data-signature] needs a [data-signature-pad] and an input[data-signature-value] inside it.'); } catch (e) { /* ignore */ }
        return null;
    }
    watchTheWorld();
    delegate();

    const canvas = document.createElement('canvas');
    canvas.setAttribute('aria-hidden', 'true');
    const probe = document.createElement('i');
    probe.hidden = true;
    pad.append(canvas, probe);

    const p = {
        el, pad, value, canvas, probe,
        native: nativeValue(value),
        ctx: canvas.getContext('2d'),
        strokes: [], drawing: null, written: [], cleanups: [],
        cssWidth: 0, height: 0, dpr: 1, ink: '', inkWidth: 2, disposed: false,
    };
    p.handle = {
        element: el,
        get value() { return serialize(p.strokes); },
        get strokes() { return p.strokes.length; },
        /** Takes the last stroke away, as the undo button does. */
        undo() { if (p.strokes.length) { p.strokes.pop(); paint(p); commit(p); } },
        /** Takes every stroke away, as the clear button does. */
        clear() { if (p.strokes.length) { p.strokes = []; paint(p); commit(p); } },
    };
    el.__sednaSignature = p;
    pad.__sednaSignaturePad = p;
    value.__sednaSignaturePad = p;
    pads.add(p);

    const on = (type, fn) => {
        const handler = e => fn(p, e);
        canvas.addEventListener(type, handler);
        p.cleanups.push(() => canvas.removeEventListener(type, handler));
    };
    on('pointerdown', down);
    on('pointermove', move);
    on('pointerup', up);
    on('pointercancel', up);
    on('lostpointercapture', up);

    watchValue(p);
    readInk(p);
    p.strokes = parse(p.native.get.call(value)) || [];
    size(p);
    reflect(p);
    sizes.observe(pad);
    fieldState.observe(value, { attributes: true, attributeFilter: ['disabled', 'readonly'] });
    el.setAttribute('data-signature-state', 'ready');
    return p.handle;
}

/* Disposes every pad whose element is no longer in the document. */
function sweep() {
    for (const p of pads) {
        if (p.el.isConnected) continue;
        pads.delete(p);
        p.disposed = true;
        sizes.unobserve(p.pad);
        p.cleanups.forEach(fn => fn());
        p.canvas.remove();
        p.probe.remove();
        delete p.el.__sednaSignature;
        delete p.pad.__sednaSignaturePad;
        delete p.value.__sednaSignaturePad;
    }
}

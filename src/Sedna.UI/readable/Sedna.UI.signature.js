/* ═══════════════════════════════════════════════════════════════════════════
   GENERATED FILE — DO NOT EDIT.

   Built by build/bundle-js.sh from src/Sedna.UI/signature-parts/. Edit the part
   that owns the behaviour and re-run that script; a guard test fails the build
   if this file and the parts disagree. Adding a part needs no change here —
   the directory is the source of truth.

   Contents, in load order:
     10-path.js
     20-ink.js
     30-draw.js
     50-pad.js
     99-exports.js
   ═══════════════════════════════════════════════════════════════════════════ */

/* ── 10-path.js ──────────────────────────────────────────────── */
/* ── The value: a signature as SVG path data ──────────────────────────────────
   What the field holds and a form posts is the drawing's geometry, never pixels: SVG
   path data in a box 1000 units wide, its height following the pad's ratio — 1000 × 333
   at the default 3 / 1 — y downwards from the top left. Geometry and nothing else, so
   the value is the same on every screen and in every theme, and the app draws it in
   whatever ink it likes:

     <svg viewBox="0 0 1000 333" fill="none" stroke="currentColor" stroke-width="5"
          stroke-linecap="round" stroke-linejoin="round"><path d="…"/></svg>

   One subpath per stroke, smoothed the way the pad draws it: through the midpoints
   of the points the pointer gave, the points themselves the curves' control points —

     M p0 Q p1 mid(p1,p2) Q p2 mid(p2,p3) … L pn

   — and a dot, a stroke that never moved, as `M p L p`, which round caps draw as a
   dot. Coordinates carry one decimal. `parse` reads exactly this shape back, so a value
   the app hands the field loads as the strokes it was made of, and undo still works.
   ─────────────────────────────────────────────────────────────────────────── */
const WIDTH = 1000;

function round1(v) { return Math.round(v * 10) / 10; }

function mid(a, b) { return [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2]; }

function pair(p) { return `${round1(p[0])} ${round1(p[1])}`; }

function strokePath(points) {
    const n = points.length;
    if (n === 1) return `M${pair(points[0])}L${pair(points[0])}`;
    let d = `M${pair(points[0])}`;
    for (let i = 1; i < n - 1; i++) d += `Q${pair(points[i])} ${pair(mid(points[i], points[i + 1]))}`;
    return `${d}L${pair(points[n - 1])}`;
}

function serialize(strokes) { return strokes.map(strokePath).join(''); }

/* The strokes a value holds, or null for a value this pad did not write. */
function parse(d) {
    const text = String(d || '').trim();
    if (!text) return [];
    if (!/^M[\d\s.,eE+\-MQL]*$/.test(text)) return null;
    const strokes = [];
    const commands = text.match(/[MQL][^MQL]*/g) || [];
    for (const c of commands) {
        const nums = (c.slice(1).match(/-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?/g) || []).map(Number);
        if (nums.some(v => !Number.isFinite(v))) return null;
        const stride = c[0] === 'Q' ? 4 : 2;
        if (nums.length === 0 || nums.length % stride !== 0) return null;
        for (let i = 0; i < nums.length; i += stride) {
            const p = [nums[i], nums[i + 1]];
            if (c[0] === 'M' && i === 0) strokes.push([p]);
            else if (strokes.length) strokes[strokes.length - 1].push(p);
            else return null;
        }
    }
    return strokes;
}

/* ── 20-ink.js ──────────────────────────────────────────────── */
/* ── The ink ──────────────────────────────────────────────────────────────────
   `--signature-ink` and `--signature-ink-width`, read through a probe inside the pad,
   so the pad's own tokens, the variant, colour vision, contrast, forced colours and the
   app's brand all reach the canvas. The browser resolves whatever the token holds; the
   width is read as the probe's computed width, so any length works. Read when a pad
   starts and whenever anything that colours it changes — never per stroke.
   ─────────────────────────────────────────────────────────────────────────── */
function readInk(p) {
    const probe = p.probe, style = getComputedStyle(probe);
    probe.style.color = 'var(--signature-ink)';
    probe.style.width = 'var(--signature-ink-width)';
    p.ink = style.color;
    const w = parseFloat(style.width);
    p.inkWidth = w > 0 ? w : 2;
}

/* ── 30-draw.js ──────────────────────────────────────────────── */
/* ── Drawing ──────────────────────────────────────────────────────────────────
   The whole drawing is painted from the strokes every time — a move, an undo, a new
   size, a new ink — so what is on the canvas is always exactly what the value says.
   The canvas holds the pad's size at the device's pixel ratio; the strokes are scaled
   from the value's 1000-unit box to it, and the line keeps the ink's width in CSS
   pixels whatever the pad's size. Strokes are pressure-insensitive: one width, round
   caps and joins, curved through the midpoints as the value records them.
   ─────────────────────────────────────────────────────────────────────────── */
function isDot(points) {
    const a = points[0];
    return points.every(q => q[0] === a[0] && q[1] === a[1]);
}

function trace(ctx, points, width) {
    ctx.beginPath();
    if (isDot(points)) {
        ctx.arc(points[0][0], points[0][1], width / 2, 0, Math.PI * 2);
        ctx.fill();
        return;
    }
    const n = points.length;
    ctx.moveTo(points[0][0], points[0][1]);
    for (let i = 1; i < n - 1; i++) {
        const m = mid(points[i], points[i + 1]);
        ctx.quadraticCurveTo(points[i][0], points[i][1], m[0], m[1]);
    }
    ctx.lineTo(points[n - 1][0], points[n - 1][1]);
    ctx.stroke();
}

function paint(p) {
    const ctx = p.ctx, canvas = p.canvas;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (!p.cssWidth || !p.strokes.length) return;
    const k = (p.dpr * p.cssWidth) / WIDTH;
    ctx.setTransform(k, 0, 0, k, 0, 0);
    ctx.strokeStyle = p.ink;
    ctx.fillStyle = p.ink;
    ctx.lineWidth = (p.inkWidth * WIDTH) / p.cssWidth;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    for (const s of p.strokes) trace(ctx, s, ctx.lineWidth);
}

/* ── 50-pad.js ──────────────────────────────────────────────── */
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

/* ── 99-exports.js ──────────────────────────────────────────────── */
/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two.
   The app drives a signature pad through its markup and its field, never through
   this file, whose URL is the package's business.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };


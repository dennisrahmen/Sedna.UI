/* ── Enough contrast to scan ──────────────────────────────────────────────────
   A camera reads a QR code by telling its dark modules from its light ones, so the
   ink has to be well away from the paper — and, unless the app says it knows what it
   is doing, darker than it. The colours are the stylesheet's (`--qr-ink`, `--qr-paper`,
   a tone class, a brand), so they are judged here, once they are resolved by the
   browser, rather than by the app that picked them:

     * ink lighter than the paper is an inverted code, which many readers cannot read.
       It is drawn only for `data-qr-invert`, and otherwise as the safe pair below;
     * a ratio under `MIN_RATIO`, which is what a phone camera in poor light needs,
       or a paper that is not opaque, is drawn as the safe pair.

   The safe pair is the stylesheet's, reached by `data-qr-contrast="fallback"` — dark
   ink on white paper in every variant — and the console says why. The verdict is on
   the element as `data-qr-contrast`: `ok`, `inverted` or `fallback`.

   A verdict is taken again whenever what it depends on changes: the element's own
   class and style, and the variant, the colour-vision and contrast settings on the
   document, and the browser's own light and dark.
   ─────────────────────────────────────────────────────────────────────────── */
const MIN_RATIO = 4;
let sampler = null;

/* A CSS colour as [r, g, b, a], whatever notation the browser resolved it to. */
function channels(css) {
    try {
        if (!sampler) {
            const canvas = document.createElement('canvas');
            canvas.width = canvas.height = 1;
            sampler = canvas.getContext('2d', { willReadFrequently: true });
        }
        sampler.clearRect(0, 0, 1, 1);
        sampler.fillStyle = 'transparent';
        sampler.fillStyle = css;
        sampler.fillRect(0, 0, 1, 1);
        const p = sampler.getImageData(0, 0, 1, 1).data;
        // Premultiplied by the canvas; an opaque colour is what is asked for here.
        return p[3] ? [p[0], p[1], p[2], p[3] / 255] : null;
    } catch (e) {
        return null;
    }
}

function luminance(c) {
    const f = v => { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); };
    return 0.2126 * f(c[0]) + 0.7152 * f(c[1]) + 0.0722 * f(c[2]);
}

function ratioOf(a, b) {
    const x = luminance(a), y = luminance(b);
    return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
}

/* The colours as the stylesheet resolves them for this code, without any fallback. */
function colours(q) {
    q.el.removeAttribute('data-qr-contrast');
    const ink = channels(getComputedStyle(q.path).fill);
    const paper = channels(getComputedStyle(q.el).backgroundColor);
    return { ink, paper };
}

function judge(q) {
    const el = q.el;
    const { ink, paper } = colours(q);
    q.ink = ink;
    q.paper = paper;
    if (!ink || !paper) {
        // A colour the canvas cannot read is nothing to judge by: leave the app's.
        el.setAttribute('data-qr-contrast', 'ok');
        return;
    }
    const ratio = ratioOf(ink, paper);
    const inverted = luminance(ink) > luminance(paper);
    let why = '';
    if (paper[3] < 0.99 || ink[3] < 0.99) why = 'the colours are not opaque';
    else if (inverted && !el.hasAttribute('data-qr-invert')) why = 'the ink is lighter than the paper (add data-qr-invert to allow an inverted code)';
    else if (ratio < MIN_RATIO) why = 'the contrast is ' + ratio.toFixed(1) + ':1, under ' + MIN_RATIO + ':1';
    if (why) {
        el.setAttribute('data-qr-contrast', 'fallback');
        // What is drawn is the stylesheet's safe pair.
        q.ink = channels(getComputedStyle(q.path).fill);
        q.paper = channels(getComputedStyle(el).backgroundColor);
        if (q.warned !== why) {
            q.warned = why;
            try { console.warn('Sedna.UI qr: drawn in the safe colours, ' + why + '.'); } catch (e) { /* ignore */ }
        }
    } else {
        q.warned = '';
        el.setAttribute('data-qr-contrast', inverted ? 'inverted' : 'ok');
    }
}

function rejudge() {
    for (const q of codes) if (q.symbol && q.el.isConnected) judge(q);
}

if (typeof MutationObserver === 'function') {
    new MutationObserver(rejudge).observe(document.documentElement, { attributes: true });
}
if (typeof matchMedia === 'function') {
    for (const query of ['(prefers-color-scheme: dark)', '(prefers-contrast: more)', '(forced-colors: active)']) {
        try { matchMedia(query).addEventListener('change', rejudge); } catch (e) { /* an engine without it */ }
    }
}

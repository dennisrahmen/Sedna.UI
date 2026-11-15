/* ── A QR code ────────────────────────────────────────────────────────────────
   `attach` starts one on the app's `[data-qr]` element: an `<svg>` inside it, hidden
   from assistive technology because the element's own `aria-label` names it, holding
   one path of the dark modules. The stylesheet colours it — the dark modules `--qr-ink`,
   the paper `--qr-paper` — and sets it inside the paper by the quiet zone, `--qr-quiet`
   modules wide, so a theme, a tone or an app's brand reaches it with no repaint here.

   What changes a code is watched on the element: `data-qr`, the text; `data-qr-level`,
   the error correction level; `data-qr-shape` and `data-qr-eye`, the look of its
   modules and its finder patterns; the colours, which are its class, its inline style
   and `data-qr-invert`; and a `.qr-logo` child coming or going. The element says how it
   went in `data-qr-state` — `ready`, `empty` for no text, `overflow` for more than the
   largest symbol holds at that level, which draws nothing — and describes the symbol in
   `data-qr-version`, `data-qr-modules` and `data-qr-mode`. `data-qr-contrast` says how
   the colours were judged (see 62-contrast.js).

   `sweep` disposes every code whose element has left the document.
   ─────────────────────────────────────────────────────────────────────────── */
const SVG_NS = 'http://www.w3.org/2000/svg';
const codes = new Set();

function levelOf(el) {
    const v = (el.getAttribute('data-qr-level') || 'M').trim().toUpperCase();
    return v in LEVELS ? v : 'M';
}

function choice(el, name, list) {
    const v = (el.getAttribute(name) || '').trim().toLowerCase();
    return list.includes(v) ? v : list[0];
}

/* The app's logo: a child of the code with the class `qr-logo`. */
function logoOf(el) {
    for (const c of el.children) if (c.classList.contains('qr-logo')) return c;
    return null;
}

/* How much of the symbol's side the app would like its logo's square to take. */
function logoFraction(el) {
    const v = parseFloat(getComputedStyle(el).getPropertyValue('--qr-logo-size'));
    return v > 0 && v < 1 ? v : 0.22;
}

function clear(q, text) {
    const el = q.el;
    q.svg.removeAttribute('viewBox');
    q.path.removeAttribute('d');
    q.svg.style.removeProperty('--qr-modules');
    for (const a of ['data-qr-version', 'data-qr-modules', 'data-qr-mode', 'data-qr-contrast']) el.removeAttribute(a);
    const logo = logoOf(el);
    if (logo) {
        logo.style.removeProperty('--qr-modules');
        logo.style.removeProperty('--qr-logo-modules');
    }
    el.setAttribute('data-qr-state', text ? 'overflow' : 'empty');
    if (text) {
        try { console.warn('Sedna.UI qr: the text is longer than a QR code holds at level ' + levelOf(el) + '.'); } catch (e) { /* ignore */ }
    }
    q.symbol = null;
}

function render(q) {
    const el = q.el, text = el.getAttribute('data-qr') || '';
    const logo = logoOf(el);
    const level = logo ? logoLevel(levelOf(el)) : levelOf(el);
    const s = text ? encode(text, level, logo ? LOGO_FLOOR : 1) : null;
    if (!s) {
        clear(q, text);
        return;
    }
    const n = s.size;
    if (logo) s.hole = { size: holeSide(n, level, logoFraction(el)), from: 0 };
    if (s.hole) s.hole.from = (n - s.hole.size) / 2;
    const shape = choice(el, 'data-qr-shape', SHAPES), eye = choice(el, 'data-qr-eye', EYES);
    q.shape = shape;
    q.eye = eye;
    q.svg.setAttribute('viewBox', `0 0 ${n} ${n}`);
    q.svg.setAttribute('shape-rendering', shape === 'square' && eye === 'square' ? 'crispEdges' : 'geometricPrecision');
    q.path.setAttribute('d', symbolPath(s, shape, eye));
    q.svg.style.setProperty('--qr-modules', String(n));
    if (logo) {
        // The square is the hole; the logo is two modules smaller, with a margin of paper.
        logo.style.setProperty('--qr-modules', String(n));
        logo.style.setProperty('--qr-logo-modules', String(s.hole.size - 2));
    }
    const modes = [...new Set(s.segments.map(g => g.mode))];
    el.setAttribute('data-qr-version', String(s.version));
    el.setAttribute('data-qr-modules', String(n));
    el.setAttribute('data-qr-mode', modes.length === 1 ? modes[0] : 'mixed');
    el.setAttribute('data-qr-state', 'ready');
    q.symbol = s;
    judge(q);
}

const attributes = new MutationObserver(records => {
    const seen = new Set();
    for (const r of records) {
        const q = r.target.__sednaQr;
        if (!q || seen.has(q)) continue;
        // The code's own svg arriving is not a change.
        if (r.type === 'childList' && [...r.addedNodes, ...r.removedNodes].every(x => x === q.svg)) continue;
        seen.add(q);
        render(q);
    }
});

/* Starts the code on an element the app wrote. Returns its handle. */
function attach(el) {
    if (el.__sednaQr) return el.__sednaQr.handle;
    const svg = document.createElementNS(SVG_NS, 'svg');
    svg.setAttribute('aria-hidden', 'true');
    svg.setAttribute('focusable', 'false');
    svg.setAttribute('shape-rendering', 'crispEdges');
    const path = document.createElementNS(SVG_NS, 'path');
    path.setAttribute('fill-rule', 'evenodd');
    svg.append(path);
    el.prepend(svg);
    const q = { el, svg, path, symbol: null, shape: 'square', eye: 'square' };
    q.handle = {
        element: el,
        get version() { return q.symbol ? q.symbol.version : null; },
        get level() { return q.symbol ? q.symbol.level : null; },
        get mask() { return q.symbol ? q.symbol.mask : null; },
        get modules() { return q.symbol ? q.symbol.size : null; },
        get mode() { return el.getAttribute('data-qr-mode'); },
        /** Encodes the element's text again. */
        redraw() { render(q); },
        /** The picture as the standalone SVG document it downloads as, a string. */
        toSvg(options) { return svgDocument(q, options || {}).then(d => d.text); },
        /** The picture as a Blob — `{ format: 'svg' | 'png', size: pixels }`. */
        toBlob(options) { return blobOf(q, options || {}); },
        /** Saves the picture — `{ format, size, name }`; resolves with what was saved. */
        download(options) { return saveAs(q, options || {}); },
    };
    el.__sednaQr = q;
    codes.add(q);
    render(q);
    attributes.observe(el, {
        attributes: true,
        attributeFilter: ['data-qr', 'data-qr-level', 'data-qr-shape', 'data-qr-eye', 'data-qr-invert', 'class', 'style'],
        childList: true,
    });
    return q.handle;
}

/* Disposes every code whose element is no longer in the document. */
function sweep() {
    for (const q of codes) {
        if (q.el.isConnected) continue;
        codes.delete(q);
        q.svg.remove();
        delete q.el.__sednaQr;
    }
}

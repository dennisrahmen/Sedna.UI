/* ── A QR code ────────────────────────────────────────────────────────────────
   `attach` starts one on the app's `[data-qr]` element: an `<svg>` inside it, hidden
   from assistive technology because the element's own `aria-label` names it, holding
   one path of the dark modules and the four-module quiet zone the standard requires
   around them. The stylesheet colours it — the dark modules `--qr-ink`, the paper
   `--qr-paper` — so a theme or an app's brand reaches it with no repaint here.

   What changes a code is watched on the element: `data-qr`, the text, and
   `data-qr-level`, the error correction level. The element says how it went in
   `data-qr-state` — `ready`, `empty` for no text, `overflow` for more than the largest
   symbol holds at that level, which draws nothing — and the symbol's version in
   `data-qr-version`.

   `sweep` disposes every code whose element has left the document.
   ─────────────────────────────────────────────────────────────────────────── */
const SVG_NS = 'http://www.w3.org/2000/svg';
const QUIET = 4;
const codes = new Set();

/* The dark modules as one path, a row's run of dark modules one rectangle. */
function pathOf(s) {
    const n = s.size, parts = [];
    for (let y = 0; y < n; y++) {
        let x = 0;
        while (x < n) {
            if (!s.dark[y * n + x]) { x++; continue; }
            let end = x;
            while (end < n && s.dark[y * n + end]) end++;
            parts.push(`M${x + QUIET} ${y + QUIET}h${end - x}v1h${x - end}z`);
            x = end;
        }
    }
    return parts.join('');
}

function levelOf(el) {
    const v = (el.getAttribute('data-qr-level') || 'M').trim().toUpperCase();
    return v in LEVELS ? v : 'M';
}

function render(q) {
    const el = q.el, text = el.getAttribute('data-qr') || '';
    const s = text ? encode(text, levelOf(el)) : null;
    if (!s) {
        q.svg.removeAttribute('viewBox');
        q.path.removeAttribute('d');
        el.removeAttribute('data-qr-version');
        el.setAttribute('data-qr-state', text ? 'overflow' : 'empty');
        if (text) {
            try { console.warn('Sedna.UI qr: the text is longer than a QR code holds at level ' + levelOf(el) + '.'); } catch (e) { /* ignore */ }
        }
        q.symbol = null;
        return;
    }
    const box = s.size + QUIET * 2;
    q.svg.setAttribute('viewBox', `0 0 ${box} ${box}`);
    q.path.setAttribute('d', pathOf(s));
    el.setAttribute('data-qr-version', String(s.version));
    el.setAttribute('data-qr-state', 'ready');
    q.symbol = s;
}

const attributes = new MutationObserver(records => {
    const seen = new Set();
    for (const r of records) {
        const q = r.target.__sednaQr;
        if (q && !seen.has(q)) {
            seen.add(q);
            render(q);
        }
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
    svg.append(path);
    el.append(svg);
    const q = { el, svg, path, symbol: null };
    q.handle = {
        element: el,
        get version() { return q.symbol ? q.symbol.version : null; },
        get level() { return q.symbol ? q.symbol.level : null; },
        get mask() { return q.symbol ? q.symbol.mask : null; },
        /** Encodes the element's text again. */
        redraw() { render(q); },
    };
    el.__sednaQr = q;
    codes.add(q);
    render(q);
    attributes.observe(el, { attributes: true, attributeFilter: ['data-qr', 'data-qr-level'] });
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

/* ── Saving the picture ───────────────────────────────────────────────────────
   The code as a file: an SVG, which is the symbol's path on its paper, or a PNG of that
   SVG at the size asked for. The colours are the ones on the page now — what the
   stylesheet and the contrast check settled on, read back from the browser as plain
   colours — so the file is the code the reader saw, in a viewer that has no stylesheet.
   The quiet zone is the page's `--qr-quiet`, around the symbol.

   A centre logo goes into the file as an image. An `<img>` is read through a canvas,
   which a page may not do to a picture from another origin; an inline `<svg>` is written
   out as it is and should carry its own fills, since a file has no stylesheet to give it
   any. Neither can be read from anything else, and a logo that cannot be read stops the
   download rather than leaving a hole in the code.

   Nothing here waits on the reader, and nothing reaches .NET: a button the app wrote
   with `data-qr-download`, or the handle's `download()`, does it all, and a plain event
   says how it went — `sedna-qr-download` with `{ format, size, name }`, or
   `sedna-qr-error` with `{ message }`.
   ─────────────────────────────────────────────────────────────────────────── */
const DEFAULT_SIZE = 1024;

function hex(c) {
    return '#' + c.slice(0, 3).map(v => v.toString(16).padStart(2, '0')).join('');
}

function quietOf(el) {
    const v = parseFloat(getComputedStyle(el).getPropertyValue('--qr-quiet'));
    return v >= 0 ? v : 4;
}

function pixelsOf(options) {
    const v = options.size === undefined ? DEFAULT_SIZE : Number(options.size);
    if (!Number.isInteger(v) || v < 16 || v > 8192) throw new Error('A QR code is saved at a whole number of pixels from 16 to 8192.');
    return v;
}

/* The logo's picture as a data URL, or null when the code has none. */
async function logoSource(q) {
    const logo = logoOf(q.el);
    if (!logo) return null;
    const media = logo.matches('img, svg') ? logo : logo.querySelector('img, svg');
    if (!media) return null;
    if (media instanceof SVGElement) {
        return 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(new XMLSerializer().serializeToString(media));
    }
    if (media.currentSrc.startsWith('data:')) return media.currentSrc;
    try {
        await media.decode();
        const w = media.naturalWidth || 512, h = media.naturalHeight || 512;
        const canvas = document.createElement('canvas');
        canvas.width = w;
        canvas.height = h;
        canvas.getContext('2d').drawImage(media, 0, 0, w, h);
        return canvas.toDataURL('image/png');
    } catch (e) {
        throw new Error('The logo cannot be read into the file: serve it from the same origin as the page, or write it as a data URL.');
    }
}

/* The standalone SVG: `{ text, element }`. */
async function svgDocument(q, options) {
    const s = q.symbol;
    if (!s) throw new Error('There is no code to save.');
    const px = pixelsOf(options);
    const quiet = quietOf(q.el), total = s.size + 2 * quiet;
    const ink = q.ink ? hex(q.ink) : getComputedStyle(q.path).fill;
    const paper = q.paper ? hex(q.paper) : getComputedStyle(q.el).backgroundColor;
    const svg = document.createElementNS(SVG_NS, 'svg');
    svg.setAttribute('xmlns', SVG_NS);
    svg.setAttribute('width', String(px));
    svg.setAttribute('height', String(px));
    svg.setAttribute('viewBox', `0 0 ${num(total)} ${num(total)}`);
    svg.setAttribute('shape-rendering', q.svg.getAttribute('shape-rendering'));
    const ground = document.createElementNS(SVG_NS, 'rect');
    ground.setAttribute('width', String(num(total)));
    ground.setAttribute('height', String(num(total)));
    ground.setAttribute('fill', paper);
    const path = document.createElementNS(SVG_NS, 'path');
    path.setAttribute('transform', `translate(${num(quiet)} ${num(quiet)})`);
    path.setAttribute('fill', ink);
    path.setAttribute('fill-rule', 'evenodd');
    path.setAttribute('d', q.path.getAttribute('d'));
    svg.append(ground, path);
    const href = await logoSource(q);
    if (href && s.hole) {
        const image = document.createElementNS(SVG_NS, 'image');
        const at = quiet + s.hole.from + 1, side = s.hole.size - 2;
        image.setAttribute('x', String(num(at)));
        image.setAttribute('y', String(num(at)));
        image.setAttribute('width', String(side));
        image.setAttribute('height', String(side));
        image.setAttribute('preserveAspectRatio', 'xMidYMid meet');
        image.setAttribute('href', href);
        svg.append(image);
    }
    return { text: new XMLSerializer().serializeToString(svg), element: svg };
}

async function pngBlob(q, options) {
    const px = pixelsOf(options);
    const doc = await svgDocument(q, options);
    const url = URL.createObjectURL(new Blob([doc.text], { type: 'image/svg+xml' }));
    try {
        const image = new Image();
        image.src = url;
        await image.decode();
        const canvas = document.createElement('canvas');
        canvas.width = px;
        canvas.height = px;
        canvas.getContext('2d').drawImage(image, 0, 0, px, px);
        return await new Promise((resolve, reject) => {
            canvas.toBlob(b => b ? resolve(b) : reject(new Error('The picture could not be drawn at ' + px + ' pixels.')), 'image/png');
        });
    } finally {
        URL.revokeObjectURL(url);
    }
}

function formatOf(options) {
    const f = String(options.format || 'png').toLowerCase();
    if (f !== 'svg' && f !== 'png') throw new Error('A QR code is saved as svg or png.');
    return f;
}

async function blobOf(q, options) {
    return formatOf(options) === 'svg'
        ? new Blob([(await svgDocument(q, options)).text], { type: 'image/svg+xml' })
        : pngBlob(q, options);
}

async function saveAs(q, options) {
    const el = q.el;
    try {
        const format = formatOf(options), size = pixelsOf(options);
        const name = (String(options.name || 'qr-code').replace(/[\\/:*?"<>|\s]+/g, '-') || 'qr-code');
        const blob = await blobOf(q, { format, size });
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = name + '.' + format;
        link.hidden = true;
        document.body.append(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(link.href), 4000);
        const detail = { format, size, name: link.download };
        el.dispatchEvent(new CustomEvent('sedna-qr-download', { bubbles: true, detail }));
        return detail;
    } catch (e) {
        el.dispatchEvent(new CustomEvent('sedna-qr-error', { bubbles: true, detail: { message: e.message } }));
        throw e;
    }
}

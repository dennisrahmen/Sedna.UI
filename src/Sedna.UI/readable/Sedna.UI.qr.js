/* ═══════════════════════════════════════════════════════════════════════════
   GENERATED FILE — DO NOT EDIT.

   Built by build/bundle-js.sh from src/Sedna.UI/qr-parts/. Edit the part
   that owns the behaviour and re-run that script; a guard test fails the build
   if this file and the parts disagree. Adding a part needs no change here —
   the directory is the source of truth.

   Contents, in load order:
     10-tables.js
     20-reed-solomon.js
     30-codewords.js
     40-matrix.js
     50-mask.js
     60-qr.js
     99-exports.js
   ═══════════════════════════════════════════════════════════════════════════ */

/* ── 10-tables.js ──────────────────────────────────────────────── */
/* ── The symbol's tables ──────────────────────────────────────────────────────
   What ISO/IEC 18004 fixes for each of the forty versions and four error correction
   levels: how many error correction codewords each block carries, and how many blocks
   the codewords are split into. Indexed by version; index 0 is unused. Everything else
   the encoder needs — the size, the alignment pattern centres, the data capacity — is
   computed from the version.
   ─────────────────────────────────────────────────────────────────────────── */

/* The four levels, with the two bits the format information carries for each. */
const LEVELS = {
    L: { index: 0, bits: 1 },
    M: { index: 1, bits: 0 },
    Q: { index: 2, bits: 3 },
    H: { index: 3, bits: 2 },
};

const ECC_PER_BLOCK = [
    [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
    [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
];

const BLOCKS = [
    [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
    [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
    [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
    [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
];

function sizeOf(version) { return version * 4 + 17; }

/* The modules left for data and error correction once every function pattern is drawn. */
function rawModules(version) {
    let n = (16 * version + 128) * version + 64;
    if (version >= 2) {
        const align = Math.floor(version / 7) + 2;
        n -= (25 * align - 10) * align - 55;
        if (version >= 7) n -= 36;
    }
    return n;
}

/* The data codewords a version holds at a level. */
function dataCodewords(version, level) {
    return Math.floor(rawModules(version) / 8) - ECC_PER_BLOCK[level.index][version] * BLOCKS[level.index][version];
}

/* The centres of the alignment patterns along one axis, ascending. */
function alignmentCentres(version) {
    if (version === 1) return [];
    const count = Math.floor(version / 7) + 2;
    const step = Math.floor((version * 8 + count * 3 + 5) / (count * 4 - 4)) * 2;
    const out = [6];
    for (let pos = sizeOf(version) - 7; out.length < count; pos -= step) out.splice(1, 0, pos);
    return out;
}

/* ── 20-reed-solomon.js ──────────────────────────────────────────────── */
/* ── Reed–Solomon error correction ────────────────────────────────────────────
   Arithmetic in GF(2^8) over the polynomial x^8 + x^4 + x^3 + x^2 + 1 (0x11D), and the
   remainder of a block of data divided by the generator polynomial of a given degree —
   the error correction codewords appended to that block.
   ─────────────────────────────────────────────────────────────────────────── */

function gfMultiply(x, y) {
    let z = 0;
    for (let i = 7; i >= 0; i--) {
        z = (z << 1) ^ ((z >>> 7) * 0x11d);
        z ^= ((y >>> i) & 1) * x;
    }
    return z & 0xff;
}

/* The generator polynomial of `degree`, highest coefficient first, the leading 1 left out. */
const divisors = new Map();
function rsDivisor(degree) {
    let out = divisors.get(degree);
    if (out) return out;
    out = new Array(degree).fill(0);
    out[degree - 1] = 1;
    let root = 1;
    for (let i = 0; i < degree; i++) {
        for (let j = 0; j < out.length; j++) {
            out[j] = gfMultiply(out[j], root);
            if (j + 1 < out.length) out[j] ^= out[j + 1];
        }
        root = gfMultiply(root, 0x02);
    }
    divisors.set(degree, out);
    return out;
}

function rsRemainder(data, divisor) {
    const out = divisor.map(() => 0);
    for (const b of data) {
        const factor = b ^ out.shift();
        out.push(0);
        divisor.forEach((coef, i) => { out[i] ^= gfMultiply(coef, factor); });
    }
    return out;
}

/* ── 30-codewords.js ──────────────────────────────────────────────── */
/* ── From text to codewords ───────────────────────────────────────────────────
   The app's text is encoded as UTF-8 in byte mode, the one mode every reader of a
   QR code decodes and the one that carries any text. No ECI header is written: a
   reader takes a byte-mode payload that is valid UTF-8 as UTF-8.

   The smallest version that holds the text at the level asked for is used — the level
   is the app's choice and is never raised behind its back. Then the bit stream is
   terminated, padded to a whole codeword and filled with the two pad codewords, split
   into blocks, each given its error correction, and interleaved.
   ─────────────────────────────────────────────────────────────────────────── */

const MAX_VERSION = 40;
const encoder = new TextEncoder();

/* The character count field's width in byte mode: 8 bits to version 9, 16 above. */
function countBits(version) { return version <= 9 ? 8 : 16; }

/* The smallest version holding `bytes` at `level`, or 0 when none does. */
function versionFor(bytes, level) {
    for (let v = 1; v <= MAX_VERSION; v++) {
        if (4 + countBits(v) + bytes.length * 8 <= dataCodewords(v, level) * 8) return v;
    }
    return 0;
}

function pushBits(bits, value, length) {
    for (let i = length - 1; i >= 0; i--) bits.push((value >>> i) & 1);
}

/* The data codewords: mode, count, the bytes, the terminator and the padding. */
function dataStream(bytes, version, level) {
    const capacity = dataCodewords(version, level) * 8;
    const bits = [];
    pushBits(bits, 0b0100, 4);
    pushBits(bits, bytes.length, countBits(version));
    for (const b of bytes) pushBits(bits, b, 8);
    pushBits(bits, 0, Math.min(4, capacity - bits.length));
    pushBits(bits, 0, (8 - (bits.length % 8)) % 8);
    const out = [];
    for (let i = 0; i < bits.length; i += 8) {
        let b = 0;
        for (let j = 0; j < 8; j++) b = (b << 1) | bits[i + j];
        out.push(b);
    }
    for (let pad = 0xec; out.length < capacity / 8; pad ^= 0xec ^ 0x11) out.push(pad);
    return out;
}

/* The final sequence of codewords: each block's data and error correction, interleaved. */
function interleave(data, version, level) {
    const blockCount = BLOCKS[level.index][version];
    const eccLength = ECC_PER_BLOCK[level.index][version];
    const raw = Math.floor(rawModules(version) / 8);
    const shortBlocks = blockCount - (raw % blockCount);
    const shortLength = Math.floor(raw / blockCount);
    const divisor = rsDivisor(eccLength);
    const blocks = [];
    for (let i = 0, k = 0; i < blockCount; i++) {
        const block = data.slice(k, k + shortLength - eccLength + (i < shortBlocks ? 0 : 1));
        k += block.length;
        const ecc = rsRemainder(block, divisor);
        // A short block is one data codeword shorter; a placeholder keeps the columns aligned.
        if (i < shortBlocks) block.push(0);
        blocks.push(block.concat(ecc));
    }
    const out = [];
    for (let i = 0; i < blocks[0].length; i++) {
        for (let j = 0; j < blocks.length; j++) {
            if (i !== shortLength - eccLength || j >= shortBlocks) out.push(blocks[j][i]);
        }
    }
    return out;
}

/* ── 40-matrix.js ──────────────────────────────────────────────── */
/* ── The module matrix ────────────────────────────────────────────────────────
   A symbol is a square of modules, dark or light, held row by row in one array. The
   function patterns are drawn first — the timing lines, the three finders with their
   separators, the alignment patterns, the dark module, room for the format information
   and, from version 7, the version information — and marked as such, so the codewords
   are placed around them and the mask never touches them.
   ─────────────────────────────────────────────────────────────────────────── */

function blankSymbol(version) {
    const size = sizeOf(version);
    return {
        version,
        size,
        dark: new Uint8Array(size * size),
        fixed: new Uint8Array(size * size),
    };
}

function setFixed(s, x, y, dark) {
    const i = y * s.size + x;
    s.dark[i] = dark ? 1 : 0;
    s.fixed[i] = 1;
}

function drawFinder(s, cx, cy) {
    for (let dy = -4; dy <= 4; dy++) {
        for (let dx = -4; dx <= 4; dx++) {
            const x = cx + dx, y = cy + dy;
            if (x < 0 || y < 0 || x >= s.size || y >= s.size) continue;
            const ring = Math.max(Math.abs(dx), Math.abs(dy));
            setFixed(s, x, y, ring !== 2 && ring !== 4);
        }
    }
}

function drawAlignment(s, cx, cy) {
    for (let dy = -2; dy <= 2; dy++) {
        for (let dx = -2; dx <= 2; dx++) setFixed(s, cx + dx, cy + dy, Math.max(Math.abs(dx), Math.abs(dy)) !== 1);
    }
}

/* The fifteen format bits — the level and the mask, with their BCH code — in both copies. */
function drawFormat(s, level, mask) {
    const data = (level.bits << 3) | mask;
    let rem = data;
    for (let i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >>> 9) * 0x537);
    const bits = ((data << 10) | rem) ^ 0x5412;
    const bit = i => ((bits >>> i) & 1) === 1;
    const n = s.size;
    for (let i = 0; i <= 5; i++) setFixed(s, 8, i, bit(i));
    setFixed(s, 8, 7, bit(6));
    setFixed(s, 8, 8, bit(7));
    setFixed(s, 7, 8, bit(8));
    for (let i = 9; i < 15; i++) setFixed(s, 14 - i, 8, bit(i));
    for (let i = 0; i < 8; i++) setFixed(s, n - 1 - i, 8, bit(i));
    for (let i = 8; i < 15; i++) setFixed(s, 8, n - 15 + i, bit(i));
    setFixed(s, 8, n - 8, true);
}

/* The eighteen version bits, from version 7, in both corners. */
function drawVersion(s) {
    if (s.version < 7) return;
    let rem = s.version;
    for (let i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >>> 11) * 0x1f25);
    const bits = (s.version << 12) | rem;
    for (let i = 0; i < 18; i++) {
        const dark = ((bits >>> i) & 1) === 1;
        const a = s.size - 11 + (i % 3), b = Math.floor(i / 3);
        setFixed(s, a, b, dark);
        setFixed(s, b, a, dark);
    }
}

function drawFunctionPatterns(s, level) {
    const n = s.size;
    for (let i = 0; i < n; i++) {
        setFixed(s, 6, i, i % 2 === 0);
        setFixed(s, i, 6, i % 2 === 0);
    }
    drawFinder(s, 3, 3);
    drawFinder(s, n - 4, 3);
    drawFinder(s, 3, n - 4);
    const centres = alignmentCentres(s.version);
    const last = centres.length - 1;
    for (let i = 0; i <= last; i++) {
        for (let j = 0; j <= last; j++) {
            // Not over the three finders.
            if ((i === 0 && j === 0) || (i === 0 && j === last) || (i === last && j === 0)) continue;
            drawAlignment(s, centres[i], centres[j]);
        }
    }
    // Reserved now so the codewords flow around it; drawn for real once the mask is chosen.
    drawFormat(s, level, 0);
    drawVersion(s);
}

/* The codewords, two columns at a time from the right, upwards then downwards. */
function placeCodewords(s, codewords) {
    const n = s.size, total = codewords.length * 8;
    let i = 0;
    for (let right = n - 1; right >= 1; right -= 2) {
        if (right === 6) right = 5;
        const upward = ((right + 1) & 2) === 0;
        for (let step = 0; step < n; step++) {
            const y = upward ? n - 1 - step : step;
            for (let j = 0; j < 2; j++) {
                const x = right - j, at = y * n + x;
                if (s.fixed[at]) continue;
                // The remainder bits past the last codeword stay light.
                if (i < total) s.dark[at] = (codewords[i >>> 3] >>> (7 - (i & 7))) & 1;
                i++;
            }
        }
    }
}

/* ── 50-mask.js ──────────────────────────────────────────────── */
/* ── Masking ──────────────────────────────────────────────────────────────────
   Each of the eight mask patterns is applied in turn, with its format information
   drawn, and scored by the four penalty rules of ISO/IEC 18004 §7.8.3; the pattern
   with the lowest score — the first of equals — is the one the symbol keeps. The
   rules, as scored here:

     N1  a run of five or more modules of one colour in a row or a column: 3, and 1 more
         for each module past five;
     N2  a 2×2 block of one colour: 3 for each, overlapping blocks counted separately;
     N3  dark-light-dark-dark-dark-light-dark in a row or a column with four light
         modules — or the symbol's edge — on either side: 40 each;
     N4  10 for every full 5% the share of dark modules is away from half.
   ─────────────────────────────────────────────────────────────────────────── */

const MASKS = [
    (x, y) => (x + y) % 2 === 0,
    (x, y) => y % 2 === 0,
    (x) => x % 3 === 0,
    (x, y) => (x + y) % 3 === 0,
    (x, y) => (Math.floor(x / 3) + Math.floor(y / 2)) % 2 === 0,
    (x, y) => ((x * y) % 2) + ((x * y) % 3) === 0,
    (x, y) => (((x * y) % 2) + ((x * y) % 3)) % 2 === 0,
    (x, y) => (((x + y) % 2) + ((x * y) % 3)) % 2 === 0,
];

/* Flips every data module the pattern selects. Applying it twice undoes it. */
function applyMask(s, mask) {
    const test = MASKS[mask], n = s.size;
    for (let y = 0; y < n; y++) {
        for (let x = 0; x < n; x++) {
            const at = y * n + x;
            if (!s.fixed[at] && test(x, y)) s.dark[at] ^= 1;
        }
    }
}

/* N3 in one line of modules. */
function finderLike(line, n) {
    let score = 0;
    const light = (from, to) => {
        for (let i = Math.max(from, 0); i < Math.min(to, n); i++) if (line[i]) return false;
        return true;
    };
    let at = 0;
    while (at <= n - 7) {
        if (line[at] && !line[at + 1] && line[at + 2] && line[at + 3] && line[at + 4] && !line[at + 5] && line[at + 6]) {
            if (at === 0 || at === n - 7 || light(at - 4, at) || light(at + 7, at + 11)) {
                score += 40;
                at += 7;
            } else {
                // The next pattern can start no earlier than the third dark module of this one.
                at += 4;
            }
        } else {
            at++;
        }
    }
    return score;
}

function penalty(s) {
    const n = s.size, d = s.dark;
    let score = 0, darkCount = 0;
    const row = new Uint8Array(n), column = new Uint8Array(n);
    for (let i = 0; i < n; i++) {
        let rowRun = 0, columnRun = 0, rowPrev = -1, columnPrev = -1;
        for (let j = 0; j < n; j++) {
            const r = d[i * n + j], c = d[j * n + i];
            row[j] = r;
            column[j] = c;
            darkCount += r;
            if (r === rowPrev) rowRun++;
            else {
                if (rowRun >= 5) score += rowRun - 2;
                rowRun = 1;
            }
            if (c === columnPrev) columnRun++;
            else {
                if (columnRun >= 5) score += columnRun - 2;
                columnRun = 1;
            }
            if (i > 0 && j > 0 && r === rowPrev && r === d[(i - 1) * n + j] && r === d[(i - 1) * n + j - 1]) score += 3;
            rowPrev = r;
            columnPrev = c;
        }
        if (rowRun >= 5) score += rowRun - 2;
        if (columnRun >= 5) score += columnRun - 2;
        score += finderLike(row, n) + finderLike(column, n);
    }
    score += 10 * Math.floor(Math.abs((darkCount * 100) / (n * n) - 50) / 5);
    return score;
}

/* Applies the best mask to the symbol and draws its format information. Returns the mask. */
function chooseMask(s, level) {
    let best = 0, bestScore = Infinity;
    for (let mask = 0; mask < 8; mask++) {
        applyMask(s, mask);
        drawFormat(s, level, mask);
        const score = penalty(s);
        if (score < bestScore) {
            best = mask;
            bestScore = score;
        }
        applyMask(s, mask);
    }
    applyMask(s, best);
    drawFormat(s, level, best);
    return best;
}

/* The symbol for `text` at a level — 'L', 'M', 'Q' or 'H' — or null when it does not fit. */
function encode(text, levelName) {
    const level = LEVELS[levelName] || LEVELS.M;
    const bytes = encoder.encode(text);
    const version = versionFor(bytes, level);
    if (!version) return null;
    const s = blankSymbol(version);
    drawFunctionPatterns(s, level);
    placeCodewords(s, interleave(dataStream(bytes, version, level), version, level));
    s.mask = chooseMask(s, level);
    s.level = levelName in LEVELS ? levelName : 'M';
    return s;
}

/* ── 60-qr.js ──────────────────────────────────────────────── */
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

/* ── 99-exports.js ──────────────────────────────────────────────── */
/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two.
   The app drives a QR code through its attributes and never through an import of
   this file, whose URL is the package's business.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };


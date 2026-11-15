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
     52-logo.js
     54-draw.js
     60-qr.js
     62-contrast.js
     70-export.js
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
   The app's text is cut into segments, each in the mode that holds its characters in the
   fewest bits: numeric for digits (10 bits for three), alphanumeric for digits, capitals
   and ` $%*+-./:` (11 bits for two) and byte for anything else, as UTF-8 — the one mode that
   carries any text. No ECI header is written: a reader takes a byte-mode payload that is
   valid UTF-8 as UTF-8. Kanji mode is not used; a Japanese text is byte mode, which every
   reader decodes.

   Where the text is mixed — a web address in capitals with a long number at its end — the
   segments are chosen by dynamic programming over the characters, with the cost of a
   segment's header counted, so a short run of digits is not given a header of its own when
   it costs less inside the segment beside it. The header's width depends on the version, so
   the choice is made once for each range of versions the standard sets.

   The smallest version that holds the segments at the level asked for is used — the level
   is the app's choice and is never raised behind its back (a centre logo is the app's
   choice too, and says so). Then the bit stream is terminated, padded to a whole codeword
   and filled with the two pad codewords, split into blocks, each given its error correction,
   and interleaved.
   ─────────────────────────────────────────────────────────────────────────── */

const MAX_VERSION = 40;
const encoder = new TextEncoder();

/* The mode indicator and the width of the character count field in each range of versions
   — 1 to 9, 10 to 26, 27 to 40. */
const MODES = {
    numeric: { id: 0b0001, counts: [10, 12, 14] },
    alphanumeric: { id: 0b0010, counts: [9, 11, 13] },
    byte: { id: 0b0100, counts: [8, 16, 16] },
};
const ALPHANUMERIC = '0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:';

function rangeOf(version) { return version <= 9 ? 0 : version <= 26 ? 1 : 2; }

/* The bits a segment of `count` characters takes: its header and its data. */
function segmentBits(segment, range) {
    const mode = MODES[segment.mode];
    let data;
    if (segment.mode === 'numeric') {
        const n = segment.count;
        data = Math.floor(n / 3) * 10 + (n % 3 === 0 ? 0 : (n % 3) * 3 + 1);
    } else if (segment.mode === 'alphanumeric') {
        data = Math.floor(segment.count / 2) * 11 + (segment.count % 2) * 6;
    } else {
        data = segment.count * 8;
    }
    return 4 + mode.counts[range] + data;
}

/* The cheapest cut of `text` into segments for one range of versions. Costs are in sixths
   of a bit, so the 3⅓ bits of a digit and the 5½ of an alphanumeric are whole numbers. */
function segmentsFor(chars, range) {
    const names = ['numeric', 'alphanumeric', 'byte'];
    const head = names.map(m => (4 + MODES[m].counts[range]) * 6);
    const INF = Infinity;
    const costs = [];
    let before = [0, 0, 0];
    for (let i = 0; i < chars.length; i++) {
        const c = chars[i];
        const per = [
            c >= '0' && c <= '9' ? 20 : INF,
            ALPHANUMERIC.includes(c) ? 33 : INF,
            encoder.encode(c).length * 48,
        ];
        const here = [INF, INF, INF], from = [-1, -1, -1];
        for (let m = 0; m < 3; m++) {
            if (per[m] === INF) continue;
            for (let p = 0; p < 3; p++) {
                const start = i === 0 ? (p === 0 ? head[m] : INF) : before[p] + (p === m ? 0 : head[m]);
                if (start + per[m] < here[m]) {
                    here[m] = start + per[m];
                    from[m] = p;
                }
            }
        }
        costs.push(from);
        before = here;
    }
    let m = 0;
    for (let k = 1; k < 3; k++) if (before[k] < before[m]) m = k;
    const modes = new Array(chars.length);
    for (let i = chars.length - 1; i >= 0; i--) {
        modes[i] = m;
        m = costs[i][m];
    }
    const out = [];
    for (let i = 0; i < chars.length; i++) {
        const last = out[out.length - 1];
        if (last && last.mode === names[modes[i]]) {
            last.text += chars[i];
            last.count += names[modes[i]] === 'byte' ? encoder.encode(chars[i]).length : 1;
        } else {
            out.push({
                mode: names[modes[i]],
                text: chars[i],
                count: names[modes[i]] === 'byte' ? encoder.encode(chars[i]).length : 1,
            });
        }
    }
    return out;
}

/* The smallest version, at least `floor`, that holds the text at `level`, with the segments
   it is held in — or null when none does. */
function plan(text, level, floor) {
    const chars = Array.from(text);
    const cut = [null, null, null];
    for (let v = Math.max(1, floor || 1); v <= MAX_VERSION; v++) {
        const range = rangeOf(v);
        if (!cut[range]) cut[range] = segmentsFor(chars, range);
        const segments = cut[range];
        let bits = 0;
        for (const s of segments) bits += segmentBits(s, range);
        // A count the field cannot write would be read as a shorter text.
        const fits = segments.every(s => s.count < (1 << MODES[s.mode].counts[range]));
        if (fits && bits <= dataCodewords(v, level) * 8) return { version: v, segments };
    }
    return null;
}

function pushBits(bits, value, length) {
    for (let i = length - 1; i >= 0; i--) bits.push((value >>> i) & 1);
}

/* The data codewords: each segment's mode, count and characters, the terminator and the padding. */
function dataStream(segments, version, level) {
    const capacity = dataCodewords(version, level) * 8;
    const range = rangeOf(version);
    const bits = [];
    for (const s of segments) {
        pushBits(bits, MODES[s.mode].id, 4);
        pushBits(bits, s.count, MODES[s.mode].counts[range]);
        if (s.mode === 'numeric') {
            for (let i = 0; i < s.text.length; i += 3) {
                const group = s.text.slice(i, i + 3);
                pushBits(bits, parseInt(group, 10), group.length * 3 + 1);
            }
        } else if (s.mode === 'alphanumeric') {
            for (let i = 0; i < s.text.length; i += 2) {
                const a = ALPHANUMERIC.indexOf(s.text[i]);
                if (i + 1 < s.text.length) pushBits(bits, a * 45 + ALPHANUMERIC.indexOf(s.text[i + 1]), 11);
                else pushBits(bits, a, 6);
            }
        } else {
            for (const b of encoder.encode(s.text)) pushBits(bits, b, 8);
        }
    }
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

/* The symbol for `text` at a level — 'L', 'M', 'Q' or 'H' — or null when it does not fit.
   `floor` is the smallest version it may be: a centre logo needs a symbol with room behind it. */
function encode(text, levelName, floor) {
    const level = LEVELS[levelName] || LEVELS.M;
    const planned = plan(text, level, floor);
    if (!planned) return null;
    const version = planned.version;
    const s = blankSymbol(version);
    drawFunctionPatterns(s, level);
    placeCodewords(s, interleave(dataStream(planned.segments, version, level), version, level));
    s.mask = chooseMask(s, level);
    s.level = levelName in LEVELS ? levelName : 'M';
    s.segments = planned.segments;
    return s;
}

/* ── 52-logo.js ──────────────────────────────────────────────── */
/* ── A centre logo ────────────────────────────────────────────────────────────
   The app's own `<img>` or `<svg>`, written as a child of the code with the class
   `qr-logo`, sits in the middle of the symbol over a square of modules that are not
   drawn. The error correction fills the square in again for a reader, so a logo is only
   as large as the level can pay for:

     * the level is at least Q — an app that asks for L or M gets Q, and one that asks
       for H keeps it, which is what lets the logo be larger;
     * the square takes at most a share of the symbol's modules that the level can
       recover with a margin (`LOGO_AREA`), however large `--qr-logo-size` asks;
     * the symbol is at least `LOGO_FLOOR`, so a short text still leaves room around the
       square for the three finders and the timing lines, and the logo is not a third
       of the code.

   The square is an odd number of modules, since a symbol is, so it is centred on a
   module. The logo itself is two modules smaller than the square, leaving a light
   module of its own paper all round.
   ─────────────────────────────────────────────────────────────────────────── */
const LOGO_AREA = { Q: 0.045, H: 0.075 };
const LOGO_FLOOR = 5;

/* The level a code with a logo is drawn at. */
function logoLevel(requested) { return requested === 'H' ? 'H' : 'Q'; }

/* The side of the square, in modules, for `fraction` of the symbol's side at `level`. */
function holeSide(n, level, fraction) {
    const budget = Math.floor(n * Math.sqrt(LOGO_AREA[level]));
    let m = Math.min(Math.round(fraction * n), budget);
    if (m % 2 === 0) m--;
    return Math.max(m, 3);
}

/* Whether the module at (x, y) is under the square. */
function inHole(hole, x, y) {
    return !!hole && x >= hole.from && y >= hole.from && x < hole.from + hole.size && y < hole.from + hole.size;
}

/* ── 54-draw.js ──────────────────────────────────────────────── */
/* ── The symbol as a path ─────────────────────────────────────────────────────
   One `<path>`, in module units, of everything dark: the three finder patterns in
   the shape the app chose for its eyes, then every other dark module in the shape it
   chose for modules. A path is one shape to the rasteriser, so modules that touch have
   no seam between them at any size.

   Shapes of a module:
     square   a run of dark modules in a row is one rectangle;
     rounded  a corner is rounded where neither neighbour beside it is dark, so a
              column of modules is a bar with round ends and a lone module is a disc;
     dots     a disc a little smaller than the module.
   Shapes of an eye — the ring and the stone in it, either side of the light ring:
     square, rounded, round.

   The path is drawn with the even-odd rule: a ring's hole is a second outline in it.
   ─────────────────────────────────────────────────────────────────────────── */
const SHAPES = ['square', 'rounded', 'dots'];
const EYES = ['square', 'rounded', 'round'];
const DOT = 0.4;

function num(v) { return String(Math.round(v * 1000) / 1000); }

/* A rectangle from (x, y), clockwise, each corner with its own radius. */
function corners(x, y, w, h, tl, tr, br, bl) {
    let d = `M${num(x + tl)} ${num(y)}h${num(w - tl - tr)}`;
    if (tr) d += `a${num(tr)} ${num(tr)} 0 0 1 ${num(tr)} ${num(tr)}`;
    d += `v${num(h - tr - br)}`;
    if (br) d += `a${num(br)} ${num(br)} 0 0 1 ${num(-br)} ${num(br)}`;
    d += `h${num(-(w - br - bl))}`;
    if (bl) d += `a${num(bl)} ${num(bl)} 0 0 1 ${num(-bl)} ${num(-bl)}`;
    d += `v${num(-(h - bl - tl))}`;
    if (tl) d += `a${num(tl)} ${num(tl)} 0 0 1 ${num(tl)} ${num(-tl)}`;
    return d + 'z';
}

function box(x, y, w, h, r) { return corners(x, y, w, h, r, r, r, r); }

function disc(cx, cy, r) {
    return `M${num(cx - r)} ${num(cy)}a${num(r)} ${num(r)} 0 1 0 ${num(2 * r)} 0a${num(r)} ${num(r)} 0 1 0 ${num(-2 * r)} 0z`;
}

/* A finder pattern with its top left corner at (x, y): the ring, then the stone. */
function eyePath(x, y, eye) {
    if (eye === 'round') return disc(x + 3.5, y + 3.5, 3.5) + disc(x + 3.5, y + 3.5, 2.5) + disc(x + 3.5, y + 3.5, 1.5);
    const outer = eye === 'rounded' ? 2 : 0, inner = eye === 'rounded' ? 1 : 0;
    return box(x, y, 7, 7, outer) + box(x + 1, y + 1, 5, 5, inner) + box(x + 2, y + 2, 3, 3, inner);
}

function inFinder(n, x, y) {
    return (x < 7 && y < 7) || (x >= n - 7 && y < 7) || (x < 7 && y >= n - 7);
}

function symbolPath(s, shape, eye) {
    const n = s.size;
    const on = (x, y) => x >= 0 && y >= 0 && x < n && y < n
        && s.dark[y * n + x] === 1 && !inFinder(n, x, y) && !inHole(s.hole, x, y);
    let d = eyePath(0, 0, eye) + eyePath(n - 7, 0, eye) + eyePath(0, n - 7, eye);
    for (let y = 0; y < n; y++) {
        let x = 0;
        while (x < n) {
            if (!on(x, y)) { x++; continue; }
            if (shape === 'square') {
                let end = x;
                while (on(end, y)) end++;
                d += `M${x} ${y}h${end - x}v1h${x - end}z`;
                x = end;
                continue;
            }
            if (shape === 'dots') d += disc(x + 0.5, y + 0.5, DOT);
            else {
                const r = (a, b) => (on(x + a, y) || on(x, y + b)) ? 0 : 0.5;
                d += corners(x, y, 1, 1, r(-1, -1), r(1, -1), r(1, 1), r(-1, 1));
            }
            x++;
        }
    }
    return d;
}

/* ── 60-qr.js ──────────────────────────────────────────────── */
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

/* ── 62-contrast.js ──────────────────────────────────────────────── */
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

/* ── 70-export.js ──────────────────────────────────────────────── */
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

/* ── 99-exports.js ──────────────────────────────────────────────── */
/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two.
   The app drives a QR code through its attributes and never through an import of
   this file, whose URL is the package's business.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };


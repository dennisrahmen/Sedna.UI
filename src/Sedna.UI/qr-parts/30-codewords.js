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

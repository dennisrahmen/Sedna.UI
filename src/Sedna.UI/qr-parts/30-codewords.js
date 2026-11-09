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

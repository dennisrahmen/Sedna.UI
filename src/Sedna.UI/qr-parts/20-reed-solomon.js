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

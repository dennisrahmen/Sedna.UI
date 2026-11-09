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

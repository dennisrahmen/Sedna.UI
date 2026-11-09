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

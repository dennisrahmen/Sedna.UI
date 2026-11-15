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

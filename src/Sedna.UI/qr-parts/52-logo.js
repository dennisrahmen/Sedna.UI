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

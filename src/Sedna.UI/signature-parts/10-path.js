/* ── The value: a signature as SVG path data ──────────────────────────────────
   What the field holds and a form posts is the drawing's geometry, never pixels: SVG
   path data in a box 1000 units wide, its height following the pad's ratio — 1000 × 333
   at the default 3 / 1 — y downwards from the top left. Geometry and nothing else, so
   the value is the same on every screen and in every theme, and the app draws it in
   whatever ink it likes:

     <svg viewBox="0 0 1000 333" fill="none" stroke="currentColor" stroke-width="5"
          stroke-linecap="round" stroke-linejoin="round"><path d="…"/></svg>

   One subpath per stroke, smoothed the way the pad draws it: through the midpoints
   of the points the pointer gave, the points themselves the curves' control points —

     M p0 Q p1 mid(p1,p2) Q p2 mid(p2,p3) … L pn

   — and a dot, a stroke that never moved, as `M p L p`, which round caps draw as a
   dot. Coordinates carry one decimal. `parse` reads exactly this shape back, so a value
   the app hands the field loads as the strokes it was made of, and undo still works.
   ─────────────────────────────────────────────────────────────────────────── */
const WIDTH = 1000;

function round1(v) { return Math.round(v * 10) / 10; }

function mid(a, b) { return [(a[0] + b[0]) / 2, (a[1] + b[1]) / 2]; }

function pair(p) { return `${round1(p[0])} ${round1(p[1])}`; }

function strokePath(points) {
    const n = points.length;
    if (n === 1) return `M${pair(points[0])}L${pair(points[0])}`;
    let d = `M${pair(points[0])}`;
    for (let i = 1; i < n - 1; i++) d += `Q${pair(points[i])} ${pair(mid(points[i], points[i + 1]))}`;
    return `${d}L${pair(points[n - 1])}`;
}

function serialize(strokes) { return strokes.map(strokePath).join(''); }

/* The strokes a value holds, or null for a value this pad did not write. */
function parse(d) {
    const text = String(d || '').trim();
    if (!text) return [];
    if (!/^M[\d\s.,eE+\-MQL]*$/.test(text)) return null;
    const strokes = [];
    const commands = text.match(/[MQL][^MQL]*/g) || [];
    for (const c of commands) {
        const nums = (c.slice(1).match(/-?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?/g) || []).map(Number);
        if (nums.some(v => !Number.isFinite(v))) return null;
        const stride = c[0] === 'Q' ? 4 : 2;
        if (nums.length === 0 || nums.length % stride !== 0) return null;
        for (let i = 0; i < nums.length; i += stride) {
            const p = [nums[i], nums[i + 1]];
            if (c[0] === 'M' && i === 0) strokes.push([p]);
            else if (strokes.length) strokes[strokes.length - 1].push(p);
            else return null;
        }
    }
    return strokes;
}

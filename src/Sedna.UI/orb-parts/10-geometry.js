/* ── Geometry ─────────────────────────────────────────────────────────────────
   Everything is drawn on a unit sphere and projected: x to the right, y down, z
   towards the reader. A mode turns its orbits with these and hands the renderer
   points; nothing here knows about canvas or colour.
   ─────────────────────────────────────────────────────────────────────────── */
const TAU = Math.PI * 2;

/** A repeatable 0–1 value per index, so a drawing is the same every time it starts. */
const hash = (i, s) => { const x = Math.sin(i * 127.1 + s * 311.7) * 43758.5453; return x - Math.floor(x); };
const ease = p => p < 0.5 ? 4 * p * p * p : 1 - Math.pow(-2 * p + 2, 3) / 2;
/** How near the reader a point is, 0 at the back to 1 at the front. */
const depth = z => Math.max(0, Math.min(1, (z + 1) / 2));
const lerp3 = (A, B, f) => [A[0] + (B[0] - A[0]) * f, A[1] + (B[1] - A[1]) * f, A[2] + (B[2] - A[2]) * f];
const flat = (r, a) => [Math.cos(a) * r, Math.sin(a) * r, 0];

/** Turns a point about the vertical axis, then tips it towards the reader. */
function rot(x, y, z, yaw, pitch) {
    const cy = Math.cos(yaw), sy = Math.sin(yaw);
    const x1 = x * cy + z * sy, z1 = z * cy - x * sy;
    const cp = Math.cos(pitch), sp = Math.sin(pitch);
    return [x1, y * cp - z1 * sp, y * sp + z1 * cp];
}

/** A plane through the centre, as a function from its own two axes to space. */
function plane(th, ph) {
    const n = [Math.sin(ph) * Math.cos(th), Math.cos(ph), Math.sin(ph) * Math.sin(th)];
    let u = [-n[1], n[0], 0];
    const l = Math.hypot(u[0], u[1]) || 1;
    u = [u[0] / l, u[1] / l, 0];
    const v = [n[1] * u[2] - n[2] * u[1], n[2] * u[0] - n[0] * u[2], n[0] * u[1] - n[1] * u[0]];
    return (a, b) => [u[0] * a + v[0] * b, u[1] * a + v[1] * b, u[2] * a + v[2] * b];
}

/** The eccentric anomaly for a mean anomaly: a satellite runs faster near the focal body. */
function kepler(M, e) {
    let E = M;
    for (let i = 0; i < 5; i++) E -= (E - e * Math.sin(E) - M) / (1 - e * Math.cos(E));
    return E;
}

/** A point on an ellipse with the focal body at its focus, its long axis turned by w. */
function ellipse(a, e, E, w) {
    const b = a * Math.sqrt(1 - e * e), x = a * (Math.cos(E) - e), y = b * Math.sin(E);
    const c = Math.cos(w), s = Math.sin(w);
    return [x * c - y * s, x * s + y * c];
}

const golden = Math.PI * (3 - Math.sqrt(5));
/** The i-th of n points spread evenly over a sphere of radius r. */
function sphere(i, n, r) {
    const y = 1 - (i + 0.5) / n * 2, rr = Math.sqrt(1 - y * y), th = i * golden;
    return [Math.cos(th) * rr * r, y * r, Math.sin(th) * rr * r];
}

/** A closed outline sampled at n even steps along its length, starting at the top. */
const OUTLINES = {
    triangle: [[0, -0.84], [0.73, 0.42], [-0.73, 0.42]],
    square: [[0, -0.64], [0.64, -0.64], [0.64, 0.64], [-0.64, 0.64], [-0.64, -0.64]],
};
const outlineCache = new Map();
function outline(name, n) {
    const key = name + n;
    if (outlineCache.has(key)) return outlineCache.get(key);
    let pts;
    if (name === 'circle') {
        pts = Array.from({ length: n }, (_, k) => { const a = -Math.PI / 2 + k / n * TAU; return [Math.cos(a) * 0.8, Math.sin(a) * 0.8]; });
    } else {
        const v = OUTLINES[name], segs = v.map((A, i) => [A, v[(i + 1) % v.length]]);
        const lens = segs.map(([A, B]) => Math.hypot(B[0] - A[0], B[1] - A[1]));
        const total = lens.reduce((a, b) => a + b, 0);
        pts = [];
        for (let k = 0; k < n; k++) {
            let d = k / n * total, i = 0;
            while (i < lens.length - 1 && d > lens[i]) { d -= lens[i]; i++; }
            const [A, B] = segs[i], f = Math.min(1, d / lens[i]);
            pts.push([A[0] + (B[0] - A[0]) * f, A[1] + (B[1] - A[1]) * f]);
        }
    }
    outlineCache.set(key, pts);
    return pts;
}

/** Each node of a constellation joined to its two nearest neighbours, in the order they appear. */
const edgeCache = new Map();
function edgesFor(n, pts) {
    if (edgeCache.has(n)) return edgeCache.get(n);
    const seen = new Set(), out = [];
    pts.forEach((A, i) => {
        pts.map((B, j) => [j, Math.hypot(A[0] - B[0], A[1] - B[1], A[2] - B[2])])
            .filter(([j]) => j !== i)
            .sort((a, b) => a[1] - b[1])
            .slice(0, 2)
            .forEach(([j]) => {
                const k = Math.min(i, j) + ':' + Math.max(i, j);
                if (!seen.has(k)) { seen.add(k); out.push([i, j]); }
            });
    });
    edgeCache.set(n, out);
    return out;
}

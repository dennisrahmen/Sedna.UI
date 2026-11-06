/* ═══════════════════════════════════════════════════════════════════════════
   GENERATED FILE — DO NOT EDIT.

   Built by build/bundle-js.sh from src/Sedna.UI/orb-parts/. Edit the part
   that owns the behaviour and re-run that script; a guard test fails the build
   if this file and the parts disagree. Adding a part needs no change here —
   the directory is the source of truth.

   Contents, in load order:
     10-geometry.js
     20-colour.js
     30-states.js
     31-activities.js
     40-draw.js
     50-orb.js
     99-exports.js
   ═══════════════════════════════════════════════════════════════════════════ */

/* ── 10-geometry.js ──────────────────────────────────────────────── */
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

/* ── 20-colour.js ──────────────────────────────────────────────── */
/* ── Colour ───────────────────────────────────────────────────────────────────
   Three roles — the focal body, the moving dots, the orbit paths — read through a
   probe inside the orb, so its own tone, the variant, colour vision, contrast,
   forced colours and the app's brand all reach the canvas. The browser resolves
   whatever a role holds (a ramp step, a color-mix) to a colour; a one-pixel canvas
   turns that into channels the renderer can fade.
   ─────────────────────────────────────────────────────────────────────────── */
const ROLES = ['body', 'dot', 'trail'];
let pixel = null;

function channels(css) {
    if (!pixel) {
        pixel = document.createElement('canvas').getContext('2d', { willReadFrequently: true });
        pixel.canvas.width = pixel.canvas.height = 1;
    }
    pixel.clearRect(0, 0, 1, 1);
    // A colour the canvas cannot parse is ignored, not cleared, and would paint the last role's.
    pixel.fillStyle = CSS.supports('color', css) ? css : 'transparent';
    pixel.fillRect(0, 0, 1, 1);
    const d = pixel.getImageData(0, 0, 1, 1).data;
    return [d[0], d[1], d[2], d[3] / 255];
}

/**
 * Reads the orb's three roles. Called when it starts and whenever anything that colours
 * it changes — never per frame. Each role keeps its channels, for the core's glow, and
 * the opaque colour as a string, which the renderer hands the canvas as it is: a string
 * built and parsed per dot was most of a frame's script.
 */
function readColours(o) {
    const probe = o.probe, style = getComputedStyle(probe), out = {};
    for (const role of ROLES) {
        probe.style.color = `var(--orb-${role})`;
        const c = channels(style.color);
        c.css = `rgb(${c[0]},${c[1]},${c[2]})`;
        out[role] = c;
    }
    o.colours = out;
}

/* ── 30-states.js ──────────────────────────────────────────────── */
/* ── The states ───────────────────────────────────────────────────────────────
   What condition an agent is in. Each mode is called once a frame with the scene it
   adds to, the clock, how long the orb has been in this state, and the voice level;
   it describes orbits (`path`, `line`), moving bodies (`dot`) and the focal body
   (`core`), and the renderer decides how to draw them for the size.

   Every one is an orbit round a focal body — the Sedna mark — and none ever turns an
   orbit edge-on to the reader, which at a small size reads as a stroke through the
   body: a magnifier, not an orbit.
   ─────────────────────────────────────────────────────────────────────────── */
const MODES = {
    idle(S, t) {
        const e = 0.18, a = 0.8 / (1 + e), yaw = t * 0.2, pitch = 1.1;
        const at = E => { const [p, q] = ellipse(a, e, E, 0); return rot(p, 0, q, yaw, pitch); };
        S.path(S.ring(at));
        S.dot(at(kepler((t * 0.7) % TAU, e)), 2.3);
        S.core(0.21 + 0.012 * Math.sin(t * 1.6));
    },
    thinking(S, t) {
        // Inclined orbits turning about the line of sight, so none is ever seen edge-on.
        const k = [2, 3, 4][S.lod];
        for (let i = 0; i < k; i++) {
            const e = 0.3 + 0.1 * hash(i, 4), a = 0.86 / (1 + e), open = [0.42, 0.56, 0.34, 0.62][i];
            const spin = i * Math.PI / k + t * (0.12 + 0.1 * i) * (i % 2 ? -1 : 1), w = t * 0.5 + i * 2;
            const at = E => {
                const [p, q] = ellipse(a, e, E, w), y = q * open, c = Math.cos(spin), s = Math.sin(spin);
                return [p * c - y * s, p * s + y * c, q * Math.sqrt(1 - open * open)];
            };
            S.path(S.ring(at));
            S.dot(at(kepler((t * (1 + 0.35 * hash(i, 7)) + i * 2.6) % TAU, e)), 2.1);
        }
        S.core(0.21);
    },
    searching(S, t) {
        const r = 0.86;
        if (S.lod === 0) {
            // A globe: its outline, its equator, a meridian sweeping round.
            S.path(S.ring(a => flat(r, a)), 0.9);
            S.path(S.ring(a => rot(Math.cos(a) * r, 0, Math.sin(a) * r, 0, 0.32)), 0.7);
            S.path(S.ring(a => rot(0, Math.cos(a) * r, Math.sin(a) * r, t * 1.2, 0.32)), 1, 'dot');
            S.core(0.13);
            return;
        }
        // A dotted sphere turning under a scanning meridian; now and then a dot lights as found.
        const n = S.lod === 1 ? 90 : 160, sweep = Math.sin(t * 1.3) * 1.2;
        for (let i = 0; i < n; i++) {
            const [x, y, z] = rot(...sphere(i, n, r), t * 0.35, 0.4);
            const lon = Math.atan2(x, z), near = Math.exp(-((lon - sweep) ** 2) / 0.05) * (z > -0.1 ? 1 : 0.25);
            S.dot([x, y, z], 0.9 + 1.3 * near, near > 0.2 ? 'dot' : 'trail', (0.35 + 0.65 * depth(z / r)) * (0.7 + 0.3 * near) + 0.5 * near);
            if (hash(i, 9) > 0.93 && near > 0.3) S.dot([x, y, z + 0.01], 1.6 + 1.6 * near, 'body', near);
        }
    },
    working(S, t) {
        // A gyroscope: rings tumbling on their own axes, two runners on each.
        const rings = [[0.88, 0.9, 0], [0.68, -1.25, 1.1], [0.5, 1.6, 2.2]].slice(0, S.lod ? 3 : 2);
        rings.forEach(([r, sp, ph], i) => {
            const on = plane(ph + t * sp * 0.4, 1.1 + i * 0.6 + Math.sin(t * 0.4 + i) * 0.3);
            const at = a => { const P = on(Math.cos(a) * r, Math.sin(a) * r); return rot(P[0], P[1], P[2], t * 0.2, 0.3); };
            S.path(S.ring(at));
            for (let m = 0; m < 2; m++) S.dot(at(t * sp * 2 + m * Math.PI), 2);
        });
        S.core(0.14);
    },
    listening(S, t, age, L) {
        // Rings rippling inwards, the more the louder.
        const k = S.lod ? 3 : 2;
        for (let i = 0; i < k; i++) {
            const base = 0.46 + i * (S.lod ? 0.2 : 0.34);
            S.path(S.ring(a => {
                const w = Math.sin(a * 3 + t * 3.2 - i * 0.9) * 0.5 + Math.sin(a * 5 - t * 2.1 + i) * 0.5;
                return flat(base + w * (0.025 + 0.12 * L) * (1 - i * 0.2), a);
            }), (0.6 + 0.4 * L) * (1 - i * 0.18), i === k - 1 ? 'trail' : 'dot');
        }
        S.core(0.17 + 0.06 * L);
    },
    speaking(S, t, age, L) {
        // Rings going out from a body that swells with the voice.
        const k = S.lod ? 3 : 2;
        for (let j = 0; j < k; j++) {
            const ph = (t * 0.7 + j / k) % 1, r = 0.32 + ph * 0.6;
            S.path(S.ring(a => flat(r + Math.sin(a * 4 + t * 5) * 0.03 * L, a)), (1 - ph) * (0.4 + 0.6 * L), 'dot');
        }
        S.core(0.21 + 0.09 * L);
    },
    waiting(S, t) {
        // Something running round the ring, again and again, until the reader answers.
        const r = 0.84, head = t * 0.5 * TAU - Math.PI / 2;
        S.path(S.ring(a => flat(r, a)), 0.7);
        for (let m = 0; m < 4; m++) {
            const [x, y] = flat(r, head - m * 0.34);
            S.dot([x, y, 0.01], 2.4 - m * 0.45, 'dot', 1 - m * 0.22);
        }
        S.core(0.19 + 0.02 * Math.sin(t * 3), 0.8 + 0.2 * Math.sin(t * 3));
    },
    error(S, t) {
        // Rings that hold, then glitch.
        const g = t % 2.4, glitch = g < 0.3 ? Math.sin(g / 0.3 * Math.PI) : 0;
        [0.84, 0.58].slice(0, S.lod ? 2 : 1).forEach((r, i) =>
            S.path(S.ring((a, k) => flat(r + (hash(k, i + 3) - 0.5) * 0.24 * glitch, a)), 0.9 - i * 0.3, 'dot'));
        S.core(0.18);
    },
    done(S, t, age) {
        // The ring draws itself into a tick, once, and stays.
        const p = ease(Math.min(1, Math.max(0, (age - 0.1) / 0.9)));
        const P = [[-0.44, 0.03], [-0.13, 0.34], [0.46, -0.3]];
        const l1 = Math.hypot(P[1][0] - P[0][0], P[1][1] - P[0][1]), l2 = Math.hypot(P[2][0] - P[1][0], P[2][1] - P[1][1]);
        const pts = [];
        for (let k = 0; k < S.n; k++) {
            const s = k / (S.n - 1) * (l1 + l2);
            const [A, B, f] = s <= l1 ? [P[0], P[1], s / l1] : [P[1], P[2], (s - l1) / l2];
            const cx = A[0] + (B[0] - A[0]) * f, cy = A[1] + (B[1] - A[1]) * f;
            const a = Math.PI * 0.75 + k / S.n * TAU, ox = Math.cos(a) * 0.84, oy = Math.sin(a) * 0.84;
            pts.push([ox + (cx - ox) * p, oy + (cy - oy) * p, 0]);
        }
        S.path(pts, 1, 'dot', false, 1 + 0.9 * p);
    },
};

/* ── 31-activities.js ──────────────────────────────────────────────── */
/* ── The activities ───────────────────────────────────────────────────────────
   What an agent is busy with, beyond its state. Same scene, same three drawings by
   size; a mode with nothing to say in solid lines at 16px says something simpler
   there rather than a smudge of dots.
   ─────────────────────────────────────────────────────────────────────────── */
Object.assign(MODES, {
    solving(S, t) {
        // Latitude bands scramble, then click back: the markers line up into one meridian when solved.
        const cyc = t % 4.4, mix = cyc < 3.2 ? Math.pow(Math.sin(cyc / 3.2 * Math.PI), 2) : 0;
        const flash = cyc >= 3.2 ? 1 - (cyc - 3.2) / 1.2 : 0;
        const bands = [3, 7, 9][S.lod], r = 0.86, yaw = t * 0.3, pitch = 0.38;
        for (let b = 0; b < bands; b++) {
            const lat = -Math.PI / 2 + (b + 0.5) / bands * Math.PI, y = Math.sin(lat) * r, rr = Math.cos(lat) * r;
            const off = (hash(b, 11) * 2 - 1) * Math.PI * 1.2 * mix;
            const at = a => rot(Math.cos(a + off) * rr, y, Math.sin(a + off) * rr, yaw, pitch);
            const m = S.lod === 0 ? S.n : Math.max(8, Math.round([0, 24, 36][S.lod] * Math.cos(lat)));
            S.path(S.ringN(m, at), 1);
            S.dot(at(0), 1.8 + 1.4 * flash, flash > 0 ? 'body' : 'dot', 1);
        }
    },
    writing(S, t) {
        // A sash round the sphere, tilted and turning, its width breathing; a bright pen travels along it.
        const N = [S.n, 56, 76][S.lod], across = [3, 5, 7][S.lod], pen = (t * 1.1) % TAU;
        const roll = 0.42, cr = Math.cos(roll), sr = Math.sin(roll);
        for (let j = 0; j < across; j++) {
            const v = j / (across - 1) * 2 - 1, pts = [];
            for (let k = 0; k < N; k++) {
                const u = k / N * TAU, w = 0.2 + 0.09 * Math.sin(u * 2 + t * 1.5), y = v * w + 0.1 * Math.sin(u * 3 - t * 2.2);
                const P = rot(Math.cos(u + t * 0.5) * 0.74, y, Math.sin(u + t * 0.5) * 0.74, 0, 0.5);
                const Q = [P[0] * cr - P[1] * sr, P[0] * sr + P[1] * cr, P[2]];
                if (S.lod === 0) { pts.push(Q); continue; }
                const du = ((u - pen) % TAU + TAU + Math.PI) % TAU - Math.PI, near = Math.exp(-(du ** 2) / 0.1);
                S.dot(Q, 0.85 + 0.8 * near, near > 0.25 ? 'dot' : 'trail', (0.35 + 0.65 * depth(Q[2] / 0.74)) * (0.75 + 0.25 * near) + 0.5 * near);
            }
            if (S.lod === 0) S.path(pts, j === 1 ? 1 : 0.75, j === 1 ? 'dot' : 'trail');
        }
    },
    connecting(S, t) {
        // A constellation wiring itself, edge by edge, then letting go and starting again.
        const N = [7, 14, 22][S.lod], r = 0.84, raw = [], nodes = [];
        for (let i = 0; i < N; i++) { const p = sphere(i, N, r); raw.push(p); nodes.push(rot(...p, t * 0.3, 0.35)); }
        const edges = edgesFor(N, raw), cyc = (t % 6.5) / 6.5;
        const shown = Math.min(1, cyc / 0.72) * edges.length, fade = cyc > 0.86 ? (1 - cyc) / 0.14 : 1;
        edges.forEach(([i, j], e) => {
            const part = Math.max(0, Math.min(1, shown - e));
            if (!part) return;
            const seg = S.lod ? 8 : 1, pts = [];
            for (let k = 0; k <= seg; k++) pts.push(lerp3(nodes[i], nodes[j], k / seg * part));
            S.line(pts, fade, 'trail');
        });
        nodes.forEach(P => S.dot(P, 1.7, 'dot', (0.45 + 0.55 * depth(P[2] / r)) * (0.55 + 0.45 * fade)));
    },
    planning(S, t) {
        // Three strands braiding down a spindle, each with a step moving along it.
        const n = [S.n, 30, 44][S.lod];
        for (let j = 0; j < 3; j++) {
            const at = u => {
                const y = (u - 0.5) * 1.72, rad = 0.5 * Math.sqrt(Math.max(0, 1 - (y / 0.92) ** 2)) + 0.07;
                const ang = u * TAU * 1.25 + t * 1.1 + j * TAU / 3;
                return rot(Math.cos(ang) * rad, y, Math.sin(ang) * rad, 0, 0.22);
            };
            S.line(Array.from({ length: n }, (_, k) => at(k / (n - 1))), 1, 'trail');
            S.dot(at((t * 0.22 + j / 3) % 1), 2.1, 'dot');
        }
    },
    shaping(S, t) {
        // One outline easing from circle to triangle to square and back.
        const n = [S.n, 42, 60][S.lod], seq = ['circle', 'triangle', 'square'];
        const ph = t / 2.6, i = Math.floor(ph) % 3, p = ease(Math.max(0, (ph % 1 - 0.5) / 0.5));
        const A = outline(seq[i], n), B = outline(seq[(i + 1) % 3], n), yaw = Math.sin(t * 0.7) * 0.45;
        S.path(A.map((a, k) => rot(a[0] + (B[k][0] - a[0]) * p, a[1] + (B[k][1] - a[1]) * p, 0, yaw, 0.12)), 0.9, S.lod ? 'dot' : 'trail');
        S.core(0.1);
    },
    reading(S, t) {
        // Lines of a page, read along; what has been read lights up behind the cursor.
        const L = [1, 0.82, 0.94, 0.58, 0.88], gap = 0.29, x0 = -0.7, w = 1.4, yaw = -0.32 + Math.sin(t * 0.5) * 0.12, pitch = 0.45;
        const total = L.reduce((a, b) => a + b, 0), prog = ((t * 0.17) % 1) * total;
        let acc = 0;
        L.forEach((len, i) => {
            const y = (i - 2) * gap, read = Math.max(0, Math.min(len, prog - acc));
            acc += len;
            const at = x => rot(x, y, 0, yaw, pitch);
            if (S.lod === 0) {
                S.line([at(x0), at(x0 + w * len)], 0.9, 'trail');
                if (read > 0) S.line([at(x0), at(x0 + w * read)], 1, 'dot');
            } else {
                const m = Math.round(len * [0, 14, 20][S.lod]);
                for (let k = 0; k <= m; k++) {
                    const xx = x0 + w * len * k / m, lit = xx <= x0 + w * read + 1e-6;
                    S.dot(at(xx), lit ? 1.15 : 0.9, lit ? 'dot' : 'trail', lit ? 1 : 0.95);
                }
            }
            if (read > 0 && read < len) S.dot(rot(x0 + w * read, y, 0.02, yaw, pitch), 2.4, 'body', 1);
        });
    },
    remembering(S, t) {
        // A two-armed spiral turning slowly, a memory drifting in along each arm to the core.
        const n = [28, 34, 54][S.lod], pitch = 1.05;
        for (let j = 0; j < 2; j++) {
            const at = (s, jit = 0) => {
                const ang = s * TAU * 1.1 + j * Math.PI + t * 0.6, rad = 0.12 + 0.76 * s + jit;
                return rot(Math.cos(ang) * rad, 0, Math.sin(ang) * rad, 0, pitch);
            };
            S.line(Array.from({ length: n }, (_, k) => at(k / (n - 1), S.lod ? (hash(k, j + 20) - 0.5) * 0.08 : 0)), 1, 'trail');
            S.dot(at(1 - ((t * 0.3 + j * 0.5) % 1)), 2.1, 'dot');
        }
        S.core(0.13);
    },
    syncing(S, t) {
        // A torus turning over, with three packets running round it.
        const R = 0.58, r = 0.25, yaw = t * 0.45, pitch = 1.0;
        const at = (u, v) => rot((R + r * Math.cos(v)) * Math.cos(u), r * Math.sin(v), (R + r * Math.cos(v)) * Math.sin(u), yaw, pitch);
        if (S.lod === 0) {
            S.path(S.ring(u => at(u, 0)));
            S.path(S.ring(u => at(u, Math.PI)), 0.8);
        } else {
            const U = S.lod === 1 ? 28 : 38, V = S.lod === 1 ? 7 : 10;
            for (let i = 0; i < U; i++) {
                for (let j = 0; j < V; j++) {
                    const Q = at(i / U * TAU, j / V * TAU);
                    S.dot(Q, 0.85, 'trail', 0.3 + 0.7 * depth(Q[2] / (R + r)));
                }
            }
        }
        for (let m = 0; m < 3; m++) S.dot(at(t * 1.4 + m * TAU / 3, t * 2.2 + m), 2.1, 'dot');
    },
    analyzing(S, t) {
        // A lattice turning in space while a scanning plane passes through it.
        const yaw = t * 0.35, pitch = 0.5, h = 0.5, scan = Math.sin(t * 1.1) * h;
        if (S.lod === 0) {
            const C = [];
            for (const x of [-h, h]) for (const y of [-h, h]) for (const z of [-h, h]) C.push([x, y, z]);
            for (const [a, b] of [[0, 1], [0, 2], [0, 4], [1, 3], [1, 5], [2, 3], [2, 6], [3, 7], [4, 5], [4, 6], [5, 7], [6, 7]]) {
                S.line([rot(...C[a], yaw, pitch), rot(...C[b], yaw, pitch)], 0.9, 'trail');
            }
            S.line([[scan, -h, -h], [scan, h, -h], [scan, h, h], [scan, -h, h], [scan, -h, -h]].map(p => rot(...p, yaw, pitch)), 1, 'dot');
            return;
        }
        const g = S.lod === 1 ? 4 : 5;
        for (let i = 0; i < g; i++) {
            for (let j = 0; j < g; j++) {
                for (let k = 0; k < g; k++) {
                    const x = (i / (g - 1) - 0.5) * 2 * h, y = (j / (g - 1) - 0.5) * 2 * h, z = (k / (g - 1) - 0.5) * 2 * h;
                    const near = Math.exp(-((x - scan) ** 2) / 0.012), Q = rot(x, y, z, yaw, pitch);
                    S.dot(Q, 1 + 1.3 * near, near > 0.3 ? 'dot' : 'trail', 0.35 + 0.65 * depth(Q[2]) + 0.5 * near);
                }
            }
        }
    },
});

/* ── 40-draw.js ──────────────────────────────────────────────── */
/* ── The renderer ─────────────────────────────────────────────────────────────
   One scene a frame, three drawings by size — the way the mark is drawn three times
   rather than scaled. Below 28px the orbits are solid lines, because dots that small
   read as noise; from 28px they are dots; from 72px finer and denser. The focal body
   gets a soft glow once there is room for one.

   A path is faded by depth — nearer is brighter — and at the small size in a handful
   of steps, so each run of one brightness is one stroke and the joins stay clean.
   ─────────────────────────────────────────────────────────────────────────── */
const SAMPLES = [56, 30, 44];
const CORE_SCALE = [1.18, 0.88, 0.82];
const TRAIL_BOOST = [2.3, 2.1, 1.7];

function lodFor(size) {
    return size < 28 ? 0 : size < 72 ? 1 : 2;
}

function sceneFor(lod) {
    const n = SAMPLES[lod];
    const scene = { lod, n, paths: [], dots: [], core: null };
    scene.ring = fn => { const out = []; for (let k = 0; k < n; k++) out.push(fn(k / n * TAU, k)); return out; };
    scene.ringN = (m, fn) => { const out = []; for (let k = 0; k < m; k++) out.push(fn(k / m * TAU, k)); return out; };
    scene.path = (pts, a = 1, key = 'trail', closed = true, w = 1) => scene.paths.push({ pts, a, key, closed, w });
    scene.line = (pts, a = 1, key = 'trail', w = 1) => scene.paths.push({ pts, a, key, closed: false, w });
    scene.dot = (Q, r, key = 'dot', a = 1) => scene.dots.push([Q[0], Q[1], Q[2], r, key, a]);
    scene.core = (r, a = 1) => { scene.coreAt = [r, a]; };
    return scene;
}

function draw(o, t) {
    const { ctx, size, dpr } = o;
    const P = Math.round(size * dpr);
    if (!P || !o.colours) return;
    const lod = lodFor(size);
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, P, P);

    const S = sceneFor(lod);
    (MODES[o.state] || MODES.idle)(S, t, t - o.since, levelOf(o, t));

    const R = P * 0.46, C = P / 2, colours = o.colours;
    const unit = Math.pow(size / 64, 0.6) * dpr;
    // The canvas parses a colour each time one is set, so a colour is set only when the
    // role changes, and the fade is the alpha alone.
    let fill = '', stroke = '';
    const alpha = (key, a) => { ctx.globalAlpha = Math.max(0, Math.min(1, colours[key][3] * a)); };
    const disc = (x, y, r, key, a) => {
        alpha(key, a);
        const css = colours[key].css;
        if (css !== fill) { ctx.fillStyle = css; fill = css; }
        ctx.beginPath();
        ctx.arc(C + x * R, C + y * R, r, 0, TAU);
        ctx.fill();
    };
    const core = () => {
        if (!S.coreAt) return;
        const [r, a] = S.coreAt, rr = r * R * CORE_SCALE[lod], c = colours.body;
        if (lod) {
            const glow = ctx.createRadialGradient(C, C, rr * 0.6, C, C, rr * 2.6);
            glow.addColorStop(0, `rgba(${c[0]},${c[1]},${c[2]},${0.3 * a * c[3]})`);
            glow.addColorStop(1, `rgba(${c[0]},${c[1]},${c[2]},0)`);
            ctx.globalAlpha = 1;
            ctx.fillStyle = glow;
            fill = '';
            ctx.beginPath();
            ctx.arc(C, C, rr * 2.6, 0, TAU);
            ctx.fill();
        }
        disc(0, 0, rr, 'body', a);
    };

    if (lod === 0) {
        ctx.lineJoin = 'round';
        const width = Math.max(1, size / 17) * dpr;
        for (const p of S.paths) {
            const boost = p.key === 'trail' ? TRAIL_BOOST[0] : 1, m = p.pts.length, last = p.closed ? m : m - 1;
            const step = k => Math.round(p.a * boost * (0.45 + 0.55 * depth((p.pts[k][2] + p.pts[(k + 1) % m][2]) / 2)) * 8) / 8;
            ctx.lineWidth = width * p.w;
            ctx.lineCap = p.closed ? 'butt' : 'round';
            for (let k = 0; k < last;) {
                const level = step(k);
                alpha(p.key, level);
                if (colours[p.key].css !== stroke) { stroke = colours[p.key].css; ctx.strokeStyle = stroke; }
                ctx.beginPath();
                ctx.moveTo(C + p.pts[k][0] * R, C + p.pts[k][1] * R);
                let j = k;
                while (j < last && step(j) === level) {
                    const q = p.pts[(j + 1) % m];
                    ctx.lineTo(C + q[0] * R, C + q[1] * R);
                    j++;
                }
                ctx.stroke();
                k = j;
            }
        }
        const dots = S.dots.slice().sort((a, b) => a[2] - b[2]);
        const radius = d => Math.max(1.25 * dpr, d[3] * unit * 0.8);
        for (const d of dots) if (d[2] < 0) disc(d[0], d[1], radius(d), d[4], d[5]);
        core();
        for (const d of dots) if (d[2] >= 0) disc(d[0], d[1], radius(d), d[4], d[5]);
    } else {
        const all = [], boost = TRAIL_BOOST[lod];
        for (const p of S.paths) {
            for (const q of p.pts) {
                all.push([q[0], q[1], q[2], (p.key === 'trail' ? 0.9 : 1.2) * p.w, p.key,
                    p.a * (p.key === 'trail' ? boost : 1) * (0.3 + 0.7 * depth(q[2]))]);
            }
        }
        for (const d of S.dots) all.push(d[4] === 'trail' ? [d[0], d[1], d[2], d[3], d[4], d[5] * boost] : d);
        if (S.coreAt) all.push([0, 0, 0.0001, -1]);
        all.sort((a, b) => a[2] - b[2]);
        for (const d of all) {
            if (d[3] === -1) { core(); continue; }
            disc(d[0], d[1], Math.max(0.8 * dpr, d[3] * unit), d[4], d[5]);
        }
    }
    ctx.globalAlpha = 1;
}

/** The voice level, 0–1, from `--level` on the orb or above it; a gentle stand-in without one. */
function levelOf(o, t) {
    if (o.state !== 'listening' && o.state !== 'speaking') return 0;
    const v = parseFloat(getComputedStyle(o.el).getPropertyValue('--level'));
    if (!Number.isNaN(v)) return Math.max(0, Math.min(1, v));
    return 0.4 + 0.3 * Math.sin(t * 2.3) * Math.sin(t * 5.1 + 1);
}

/* ── 50-orb.js ──────────────────────────────────────────────── */
/* ── An orb ───────────────────────────────────────────────────────────────────
   `attach` starts one: a canvas inside the element, sized to it at the device's pixel
   ratio (capped at 2), a probe for its colours, and a still frame at once. From there
   one shared clock draws every orb that is on screen, while the tab is visible and the
   reader allows motion — within a frame budget by size, below. What changes an orb is
   watched, never polled:

     * `data-orb` and `data-orb-tone` on the element — a new state restarts its clock,
       so `done` draws its tick from the start;
     * its size — a new size can be a new drawing;
     * anything that colours it — the theme, the variant, colour vision, contrast,
       forced colours — through the root's attributes and the media queries;
     * `prefers-reduced-motion` — a still frame, and nothing moves.

   `sweep` disposes every orb whose element has left the document.
   ─────────────────────────────────────────────────────────────────────────── */
const orbs = new Set();
const motion = matchMedia('(prefers-reduced-motion: reduce)');
let frame = 0;

function now() { return performance.now() / 1000; }

function still() { return motion.matches || document.visibilityState !== 'visible'; }

function stillFrame(o) {
    // A moment into the state, where every drawing shows what it is: the tick drawn,
    // the scan part way across, the constellation wired.
    draw(o, o.since + 2.2);
}

function resize(o) {
    const box = o.el.getBoundingClientRect();
    const size = Math.max(8, Math.round(Math.min(box.width, box.height)));
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    if (size === o.size && dpr === o.dpr) return;
    o.size = size;
    o.dpr = dpr;
    o.canvas.width = o.canvas.height = Math.round(size * dpr);
    stillFrame(o);
}

// A frame budget by drawing, whatever the display's rate: below 72px a dot moves well
// under a pixel a frame at sixty, so those draw thirty times a second; the finest
// drawing sixty. The slack absorbs a frame that arrives a little early.
const INTERVAL = [1 / 30, 1 / 30, 1 / 60];
const SLACK = 0.004;

function tick(ms) {
    frame = 0;
    if (still()) return;
    const t = ms / 1000;
    let any = false;
    for (const o of orbs) {
        if (!o.visible) continue;
        any = true;
        if (t - o.drawn < INTERVAL[lodFor(o.size)] - SLACK) continue;
        o.drawn = t;
        draw(o, t);
    }
    if (any) frame = requestAnimationFrame(tick);
}

function run() {
    if (!frame && !still() && orbs.size) frame = requestAnimationFrame(tick);
}

function repaintAll() {
    for (const o of orbs) {
        readColours(o);
        stillFrame(o);
    }
    run();
}

const sizes = new ResizeObserver(entries => { for (const e of entries) if (e.target.__sednaOrb) resize(e.target.__sednaOrb); });
const seen = new IntersectionObserver(entries => {
    for (const e of entries) {
        const o = e.target.__sednaOrb;
        if (o) o.visible = e.isIntersecting;
    }
    run();
}, { rootMargin: '80px' });
const attributes = new MutationObserver(records => {
    for (const r of records) {
        const o = r.target.__sednaOrb;
        if (!o) continue;
        if (r.attributeName === 'data-orb') {
            o.state = o.el.getAttribute('data-orb');
            o.since = now();
        }
        readColours(o);
        stillFrame(o);
    }
    run();
});

let watching = false;
function watchTheWorld() {
    if (watching) return;
    watching = true;
    new MutationObserver(repaintAll).observe(document.documentElement, {
        attributes: true,
        attributeFilter: ['class', 'style', 'data-theme', 'data-variant', 'data-cvd', 'data-contrast'],
    });
    for (const query of ['(prefers-color-scheme: dark)', '(forced-colors: active)', '(prefers-contrast: more)']) {
        matchMedia(query).addEventListener('change', repaintAll);
    }
    motion.addEventListener('change', repaintAll);
    document.addEventListener('visibilitychange', run);
}

/** Starts the orb on an element the app wrote. Resolves its handle. */
function attach(el) {
    if (el.__sednaOrb) return el.__sednaOrb.handle;
    watchTheWorld();
    const canvas = document.createElement('canvas');
    canvas.setAttribute('aria-hidden', 'true');
    const probe = document.createElement('i');
    probe.hidden = true;
    el.append(canvas, probe);
    const o = {
        el, canvas, probe,
        ctx: canvas.getContext('2d'),
        state: el.getAttribute('data-orb'),
        since: now(),
        size: 0, dpr: 1, visible: true, colours: null, drawn: -Infinity,
    };
    o.handle = {
        element: el,
        get state() { return o.state; },
        /** Draws one frame now, at the state's resting moment. */
        redraw() { readColours(o); stillFrame(o); },
    };
    el.__sednaOrb = o;
    orbs.add(o);
    readColours(o);
    resize(o);
    sizes.observe(el);
    seen.observe(el);
    // Not `style`: an app writing `--level` sixty times a second would repaint the
    // colours on every write. The clock reads the level itself.
    attributes.observe(el, { attributes: true, attributeFilter: ['data-orb', 'data-orb-tone'] });
    run();
    return o.handle;
}

/** Disposes every orb whose element is no longer in the document. */
function sweep() {
    for (const o of orbs) {
        if (o.el.isConnected) continue;
        orbs.delete(o);
        sizes.unobserve(o.el);
        seen.unobserve(o.el);
        o.canvas.remove();
        o.probe.remove();
        delete o.el.__sednaOrb;
    }
}

/* ── 99-exports.js ──────────────────────────────────────────────── */
/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two.
   The app drives an orb through its attributes and never through an import of this
   file, whose URL is the package's business.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };


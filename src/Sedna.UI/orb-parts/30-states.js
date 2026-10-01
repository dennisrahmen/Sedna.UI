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

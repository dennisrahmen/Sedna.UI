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

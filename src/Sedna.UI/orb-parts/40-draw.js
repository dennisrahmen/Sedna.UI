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
    const paint = (key, a) => {
        const c = colours[key];
        ctx.globalAlpha = Math.max(0, Math.min(1, c[3] * a));
        return `rgb(${c[0]},${c[1]},${c[2]})`;
    };
    const disc = (x, y, r, key, a) => {
        ctx.fillStyle = paint(key, a);
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
                ctx.strokeStyle = paint(p.key, level);
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

/* ── Drawing ──────────────────────────────────────────────────────────────────
   The whole drawing is painted from the strokes every time — a move, an undo, a new
   size, a new ink — so what is on the canvas is always exactly what the value says.
   The canvas holds the pad's size at the device's pixel ratio; the strokes are scaled
   from the value's 1000-unit box to it, and the line keeps the ink's width in CSS
   pixels whatever the pad's size. Strokes are pressure-insensitive: one width, round
   caps and joins, curved through the midpoints as the value records them.
   ─────────────────────────────────────────────────────────────────────────── */
function isDot(points) {
    const a = points[0];
    return points.every(q => q[0] === a[0] && q[1] === a[1]);
}

function trace(ctx, points, width) {
    ctx.beginPath();
    if (isDot(points)) {
        ctx.arc(points[0][0], points[0][1], width / 2, 0, Math.PI * 2);
        ctx.fill();
        return;
    }
    const n = points.length;
    ctx.moveTo(points[0][0], points[0][1]);
    for (let i = 1; i < n - 1; i++) {
        const m = mid(points[i], points[i + 1]);
        ctx.quadraticCurveTo(points[i][0], points[i][1], m[0], m[1]);
    }
    ctx.lineTo(points[n - 1][0], points[n - 1][1]);
    ctx.stroke();
}

function paint(p) {
    const ctx = p.ctx, canvas = p.canvas;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (!p.cssWidth || !p.strokes.length) return;
    const k = (p.dpr * p.cssWidth) / WIDTH;
    ctx.setTransform(k, 0, 0, k, 0, 0);
    ctx.strokeStyle = p.ink;
    ctx.fillStyle = p.ink;
    ctx.lineWidth = (p.inkWidth * WIDTH) / p.cssWidth;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    for (const s of p.strokes) trace(ctx, s, ctx.lineWidth);
}

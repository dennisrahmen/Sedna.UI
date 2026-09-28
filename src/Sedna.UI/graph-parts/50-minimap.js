/* ── The minimap ──────────────────────────────────────────────────────────────
   The whole drawing in miniature, with the part on screen framed — for a graph larger
   than its frame, where zooming in loses the reader's sense of where they are.

     <canvas class="graph-minimap" data-graph-minimap aria-hidden="true"></canvas>

   The app writes the canvas, inside the graph, and the stylesheet places it
   (`.graph-minimap--start`, `--top`); this draws into it. Pressing on it moves the view
   there, dragging pans, and the wheel zooms about the point under the pointer. It is a
   pointer convenience: the keyboard already has the arrows, + and −, and 0 to fit, so
   it is hidden from the accessibility tree.

   The records are painted once into a buffer whenever they move or change; a view that
   pans only redraws the frame over it. Colours are the canvas's own, so the minimap is
   the drawing, smaller — never a second palette.
   ─────────────────────────────────────────────────────────────────────────── */

function minimap(g, canvas) {
    const cy = g.cy;
    const buffer = document.createElement('canvas');
    let scale = 1, ox = 0, oy = 0, dirty = true, timer = 0, dragging = false;

    function measure() {
        const dpr = Math.min(window.devicePixelRatio || 1, 3);
        const w = Math.max(1, Math.round(canvas.clientWidth * dpr));
        const h = Math.max(1, Math.round(canvas.clientHeight * dpr));
        if (canvas.width !== w || canvas.height !== h) {
            canvas.width = buffer.width = w;
            canvas.height = buffer.height = h;
            dirty = true;
        }
        return dpr;
    }

    function paintRecords(dpr) {
        const ctx = buffer.getContext('2d');
        ctx.clearRect(0, 0, buffer.width, buffer.height);
        const nodes = cy.nodes().not('.hidden').filter(n => !n.isParent() && !n.hasClass('eh-ghost') && !n.hasClass('eh-handle'));
        if (nodes.empty()) return;
        const bb = nodes.boundingBox({ includeLabels: false, includeOverlays: false });
        const pad = 8 * dpr;
        scale = Math.min((buffer.width - pad * 2) / Math.max(bb.w, 1), (buffer.height - pad * 2) / Math.max(bb.h, 1));
        ox = pad + (buffer.width - pad * 2 - bb.w * scale) / 2 - bb.x1 * scale;
        oy = pad + (buffer.height - pad * 2 - bb.h * scale) / 2 - bb.y1 * scale;
        const p = palette(g.colours);

        const edges = cy.edges().not('.hidden');
        if (edges.length < 6000) {
            ctx.strokeStyle = p.line;
            ctx.globalAlpha = 0.35;
            ctx.lineWidth = Math.max(0.5, dpr * 0.6);
            ctx.beginPath();
            edges.forEach(e => {
                const s = e.source().position(), t = e.target().position();
                ctx.moveTo(ox + s.x * scale, oy + s.y * scale);
                ctx.lineTo(ox + t.x * scale, oy + t.y * scale);
            });
            ctx.stroke();
        }
        ctx.globalAlpha = 1;
        cy.nodes(':parent').not('.hidden').forEach(parent => {
            const b = parent.boundingBox({ includeLabels: false });
            ctx.fillStyle = withAlpha(parent.style('border-color'), 0.12);
            ctx.fillRect(ox + b.x1 * scale, oy + b.y1 * scale, b.w * scale, b.h * scale);
        });
        nodes.forEach(n => {
            const pos = n.position();
            const r = Math.max(1.5 * dpr, Math.min(n.width() * scale / 2, 6 * dpr));
            ctx.globalAlpha = n.hasClass('dim') ? 0.25 : n.data('muted') ? 0.5 : 1;
            ctx.fillStyle = (n.data('display') || g.options.nodes) === 'box' ? n.style('border-color') : n.style('background-color');
            ctx.beginPath();
            ctx.arc(ox + pos.x * scale, oy + pos.y * scale, r, 0, Math.PI * 2);
            ctx.fill();
        });
        ctx.globalAlpha = 1;
    }

    function draw() {
        clearTimeout(timer);
        timer = 0;
        if (!canvas.isConnected || canvas.clientWidth === 0) return;
        const dpr = measure();
        if (dirty) {
            paintRecords(dpr);
            dirty = false;
        }
        const ctx = canvas.getContext('2d');
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(buffer, 0, 0);
        const p = palette(g.colours);
        const ext = cy.extent();
        const x = ox + ext.x1 * scale, y = oy + ext.y1 * scale, w = ext.w * scale, h = ext.h * scale;
        // What is off screen is veiled; the part in view is left clear and framed. When
        // everything is in view there is nothing to veil and the frame sits on the edge.
        ctx.save();
        ctx.beginPath();
        ctx.rect(0, 0, canvas.width, canvas.height);
        ctx.rect(x, y, w, h);
        ctx.fillStyle = withAlpha(p.ground, 0.55);
        ctx.fill('evenodd');
        ctx.restore();
        const lw = 1.5 * dpr;
        const fx = Math.max(x, lw / 2), fy = Math.max(y, lw / 2);
        const fw = Math.min(x + w, canvas.width - lw / 2) - fx, fh = Math.min(y + h, canvas.height - lw / 2) - fy;
        if (fw > 0 && fh > 0) {
            ctx.strokeStyle = p.brand;
            ctx.lineWidth = lw;
            ctx.strokeRect(fx, fy, fw, fh);
        }
    }

    // A timer, not an animation frame: a frame never comes for a tab that is not painted.
    const soon = (records) => {
        if (records) dirty = true;
        if (!timer) timer = setTimeout(draw, 32);
    };

    // From the pointer to the drawing, through the canvas's content box — its border is not
    // part of what was drawn, and a pixel there is a pixel of error, magnified by the zoom.
    const toModel = e => {
        const r = canvas.getBoundingClientRect();
        const dpr = canvas.width / Math.max(canvas.clientWidth, 1);
        const px = (e.clientX - r.left - canvas.clientLeft) * dpr;
        const py = (e.clientY - r.top - canvas.clientTop) * dpr;
        return { x: (px - ox) / scale, y: (py - oy) / scale };
    };
    const centreOn = e => {
        const m = toModel(e);
        cy.stop();
        cy.pan({ x: cy.width() / 2 - m.x * cy.zoom(), y: cy.height() / 2 - m.y * cy.zoom() });
    };

    const onDown = e => {
        if (e.button !== 0) return;
        dirty = true;
        draw();
        dragging = true;
        canvas.setPointerCapture?.(e.pointerId);
        g.touched = true;
        centreOn(e);
        e.preventDefault();
    };
    const onMove = e => { if (dragging) centreOn(e); };
    const onUp = e => {
        dragging = false;
        canvas.releasePointerCapture?.(e.pointerId);
        settle(g);
    };
    const onWheel = e => {
        e.preventDefault();
        g.touched = true;
        const m = toModel(e);
        const level = Math.max(cy.minZoom(), Math.min(cy.maxZoom(), cy.zoom() * (e.deltaY < 0 ? 1.2 : 1 / 1.2)));
        cy.zoom({ level, position: m });
    };
    canvas.addEventListener('pointerdown', onDown);
    canvas.addEventListener('pointermove', onMove);
    canvas.addEventListener('pointerup', onUp);
    canvas.addEventListener('pointercancel', onUp);
    canvas.addEventListener('wheel', onWheel, { passive: false });

    const onViewport = () => soon(false);
    const onRecords = () => soon(true);
    cy.on('viewport', onViewport);
    cy.on('position add remove', onRecords);
    const size = new ResizeObserver(() => soon(true));
    size.observe(canvas);

    return {
        draw: () => soon(true),
        now: () => { dirty = true; draw(); },
        destroy() {
            clearTimeout(timer);
            size.disconnect();
            cy.off('viewport', onViewport);
            cy.off('position add remove', onRecords);
            canvas.removeEventListener('pointerdown', onDown);
            canvas.removeEventListener('pointermove', onMove);
            canvas.removeEventListener('pointerup', onUp);
            canvas.removeEventListener('pointercancel', onUp);
            canvas.removeEventListener('wheel', onWheel);
        },
    };
}

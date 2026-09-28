/* ── Export ───────────────────────────────────────────────────────────────────
   The whole drawing — every record on screen, not only the part in view — as a picture:

     data-graph-action="export-png"   cytoscape's own renderer, at twice the resolution
     data-graph-action="export-svg"   written here, from the drawing's geometry

   The SVG is Sedna.UI's own rather than the cytoscape-svg plugin, which is GPL-3.0 and
   cannot ship in an Apache-2.0 package. It is also the better file: each record is a
   group with a <title>, the text is text, and every colour is the colour the canvas
   resolved from the tokens at the moment of export — so a dark-theme export is dark.

   `data-graph-filename` on the button names the file; otherwise it is the graph's id.
   From script, graph.export('svg' | 'png') resolves a Blob.
   ─────────────────────────────────────────────────────────────────────────── */

const SVG_NS = 'http://www.w3.org/2000/svg';

async function exportImage(g, format) {
    const ground = palette(g.colours).ground;
    if (format === 'png') {
        return g.cy.png({ output: 'blob-promise', bg: ground, full: true, scale: 2, maxWidth: 8000, maxHeight: 8000 });
    }
    return new Blob([svgOf(g, ground)], { type: 'image/svg+xml' });
}

async function download(g, format, filename) {
    const blob = await exportImage(g, format === 'png' ? 'png' : 'svg');
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = (filename || g.el.id || 'graph').replace(/\.(svg|png)$/i, '') + '.' + format;
    a.click();
    setTimeout(() => URL.revokeObjectURL(url), 10000);
}

const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
const num = n => (Math.round(n * 100) / 100).toString();

// A regular polygon with `sides`, point up, fitted to the square [-1, 1].
function ngon(sides, rotation = 0) {
    const pts = [];
    for (let i = 0; i < sides; i++) {
        const a = rotation - Math.PI / 2 + (i * 2 * Math.PI) / sides;
        pts.push([Math.cos(a), Math.sin(a)]);
    }
    const xs = pts.map(p => p[0]), ys = pts.map(p => p[1]);
    const minX = Math.min(...xs), maxX = Math.max(...xs), minY = Math.min(...ys), maxY = Math.max(...ys);
    return pts.map(([x, y]) => [((x - minX) / (maxX - minX)) * 2 - 1, ((y - minY) / (maxY - minY)) * 2 - 1]);
}

function star() {
    const pts = [];
    for (let i = 0; i < 10; i++) {
        const r = i % 2 === 0 ? 1 : 0.4;
        const a = -Math.PI / 2 + (i * Math.PI) / 5;
        pts.push([Math.cos(a) * r, Math.sin(a) * r]);
    }
    return pts;
}

const POLYGONS = {
    triangle: [[0, -1], [1, 1], [-1, 1]],
    diamond: [[0, -1], [1, 0], [0, 1], [-1, 0]],
    tag: [[-1, -1], [0.25, -1], [1, 0], [0.25, 1], [-1, 1]],
    vee: [[-1, -1], [0, -0.333], [1, -1], [0, 1]],
    rhomboid: [[-1, -1], [0.333, -1], [1, 1], [-0.333, 1]],
    pentagon: ngon(5),
    hexagon: ngon(6),
    heptagon: ngon(7),
    octagon: ngon(8, Math.PI / 8),
    star: star(),
};

function shapeSvg(shape, x, y, w, h, attrs) {
    const base = shape.replace(/^round-/, '');
    const rounded = shape.startsWith('round-');
    if (base === 'ellipse') return `<ellipse cx="${num(x)}" cy="${num(y)}" rx="${num(w / 2)}" ry="${num(h / 2)}" ${attrs}/>`;
    if (POLYGONS[base]) {
        const pts = POLYGONS[base].map(([px, py]) => `${num(x + (px * w) / 2)},${num(y + (py * h) / 2)}`).join(' ');
        return `<polygon points="${pts}" stroke-linejoin="${rounded ? 'round' : 'miter'}" ${attrs}/>`;
    }
    // rectangle, round-rectangle, cut-rectangle, barrel, bottom-round-rectangle and the rest.
    const r = base === 'rectangle' && !rounded ? 0 : Math.min(w / 4, h / 4, 8);
    return `<rect x="${num(x - w / 2)}" y="${num(y - h / 2)}" width="${num(w)}" height="${num(h)}" rx="${num(r)}" ${attrs}/>`;
}

let measureCtx = null;
function wrapLines(text, maxWidth, font) {
    measureCtx = measureCtx || document.createElement('canvas').getContext('2d');
    measureCtx.font = font;
    const out = [];
    for (const para of String(text).split('\n')) {
        const words = para.split(/\s+/).filter(Boolean);
        let line = '';
        for (const w of words) {
            const next = line ? line + ' ' + w : w;
            if (line && measureCtx.measureText(next).width > maxWidth) {
                out.push(line);
                line = w;
            } else {
                line = next;
            }
        }
        out.push(line);
    }
    return out;
}

function edgePath(e) {
    const s = e.sourceEndpoint(), t = e.targetEndpoint();
    const curve = e.style('curve-style');
    let d = `M ${num(s.x)} ${num(s.y)}`;
    let before = s;
    if (/taxi|segments/.test(curve)) {
        const pts = e.segmentPoints() || [];
        pts.forEach(p => { d += ` L ${num(p.x)} ${num(p.y)}`; });
        if (pts.length) before = pts[pts.length - 1];
        d += ` L ${num(t.x)} ${num(t.y)}`;
    } else if (/bezier/.test(curve)) {
        const cps = e.controlPoints() || [];
        if (cps.length === 0) {
            d += ` L ${num(t.x)} ${num(t.y)}`;
        } else {
            for (let i = 0; i < cps.length; i++) {
                const cp = cps[i];
                const end = i < cps.length - 1 ? { x: (cp.x + cps[i + 1].x) / 2, y: (cp.y + cps[i + 1].y) / 2 } : t;
                d += ` Q ${num(cp.x)} ${num(cp.y)} ${num(end.x)} ${num(end.y)}`;
            }
            before = cps[cps.length - 1];
        }
    } else {
        d += ` L ${num(t.x)} ${num(t.y)}`;
    }
    return { d, s, t, before, after: (/bezier/.test(curve) && (e.controlPoints() || []).length) ? e.controlPoints()[0] : ((e.segmentPoints() || [])[0] || t) };
}

function arrowSvg(tip, from, size, colour) {
    const a = Math.atan2(tip.y - from.y, tip.x - from.x);
    const l = size, w = size * 0.6;
    const bx = tip.x - Math.cos(a) * l, by = tip.y - Math.sin(a) * l;
    const p1 = [bx + Math.cos(a + Math.PI / 2) * w / 2, by + Math.sin(a + Math.PI / 2) * w / 2];
    const p2 = [bx + Math.cos(a - Math.PI / 2) * w / 2, by + Math.sin(a - Math.PI / 2) * w / 2];
    return `<polygon points="${num(tip.x)},${num(tip.y)} ${num(p1[0])},${num(p1[1])} ${num(p2[0])},${num(p2[1])}" fill="${colour}"/>`;
}

function svgOf(g, ground) {
    const cy = g.cy;
    const visible = cy.elements().not('.hidden').not('.eh-ghost, .eh-ghost-edge, .eh-preview, .eh-handle');
    const bb = visible.boundingBox({ includeLabels: true, includeOverlays: false });
    const pad = 24;
    const x0 = bb.x1 - pad, y0 = bb.y1 - pad, w = bb.w + pad * 2, h = bb.h + pad * 2;
    const font = palette(g.colours).font;
    const out = [];
    const title = g.el.getAttribute('aria-label');
    out.push(`<svg xmlns="${SVG_NS}" viewBox="${num(x0)} ${num(y0)} ${num(w)} ${num(h)}" width="${Math.round(w)}" height="${Math.round(h)}" role="img"${title ? ` aria-label="${esc(title)}"` : ''} font-family="${esc(font)}">`);
    if (title) out.push(`<title>${esc(title)}</title>`);
    out.push(`<rect x="${num(x0)}" y="${num(y0)}" width="${num(w)}" height="${num(h)}" fill="${ground}"/>`);

    // The outlines around groups, underneath everything: their paths are already in the
    // drawing's own coordinates.
    if (g.hullsOn && g.bb) {
        for (const path of g.bb.getPaths()) {
            const d = path.node.getAttribute('d');
            if (!d) continue;
            const st = path.node.style;
            out.push(`<path class="graph-outline" d="${esc(d)}" fill="${st.fill || 'none'}" stroke="${st.stroke || 'none'}" stroke-width="${st.strokeWidth || 1}"/>`);
        }
    }

    // Groups underneath, outermost first.
    const parents = visible.nodes(':parent').toArray().sort((a, b) => a.ancestors().length - b.ancestors().length);
    for (const p of parents) {
        const b = p.boundingBox({ includeLabels: false });
        const fill = p.style('background-color'), stroke = p.style('border-color');
        out.push(`<g class="graph-group"><title>${esc(p.data('label') || p.id())}</title>`
            + `<rect x="${num(b.x1)}" y="${num(b.y1)}" width="${num(b.w)}" height="${num(b.h)}" rx="8" fill="${fill}" fill-opacity="${p.numericStyle('background-opacity')}" stroke="${stroke}" stroke-dasharray="6 4" stroke-width="${p.numericStyle('border-width')}"/>`
            + (p.style('label') ? `<text x="${num(b.x1 + b.w / 2)}" y="${num(b.y1 - 6)}" text-anchor="middle" font-size="${num(p.numericStyle('font-size'))}" font-weight="600" fill="${p.style('color')}">${esc(p.style('label'))}</text>` : '')
            + '</g>');
    }

    for (const e of visible.edges().toArray()) {
        const colour = e.style('line-color');
        const width = e.numericStyle('width');
        const opacity = e.numericStyle('opacity');
        const dash = e.style('line-style') === 'dashed' ? ' stroke-dasharray="6 4"' : e.style('line-style') === 'dotted' ? ' stroke-dasharray="1.5 3.5" stroke-linecap="round"' : '';
        const geo = edgePath(e);
        const size = (5 + width * 2.5) * e.numericStyle('arrow-scale');
        let arrows = '';
        if (e.style('target-arrow-shape') !== 'none') arrows += arrowSvg(geo.t, geo.before, size, colour);
        if (e.style('source-arrow-shape') !== 'none') arrows += arrowSvg(geo.s, geo.after, size, colour);
        const label = e.style('label');
        const mid = e.midpoint();
        out.push(`<g class="graph-link" opacity="${num(opacity)}"><title>${esc(e.data('label') || `${e.source().data('label')} → ${e.target().data('label')}`)}</title>`
            + `<path d="${geo.d}" fill="none" stroke="${colour}" stroke-width="${num(width)}"${dash}/>${arrows}`
            + (label ? `<text x="${num(mid.x)}" y="${num(mid.y)}" text-anchor="middle" dominant-baseline="middle" font-size="${num(e.numericStyle('font-size'))}" fill="${e.style('color')}" stroke="${ground}" stroke-width="3" paint-order="stroke">${esc(label)}</text>` : '')
            + '</g>');
    }

    for (const n of visible.nodes().not(':parent').toArray()) {
        const p = n.position();
        const nw = n.width(), nh = n.height();
        const box = (n.data('display') || g.options.nodes) === 'box';
        const fill = n.style('background-color');
        const stroke = n.style('border-color');
        const bw = n.numericStyle('border-width');
        const attrs = `fill="${fill}" fill-opacity="${num(n.numericStyle('background-opacity'))}" stroke="${stroke}" stroke-width="${num(bw)}"`;
        const parts = [`<g class="graph-record" opacity="${num(n.numericStyle('opacity'))}"><title>${esc([n.data('label'), n.data('meta')].filter(Boolean).join(' — '))}</title>`];
        parts.push(shapeSvg(n.style('shape'), p.x, p.y, nw, nh, attrs));
        const icon = n.data('icon') ? g.icons.image(n.data('icon'), box ? stroke : ground, 32) : null;
        if (icon) {
            // A box's icon sits 12px in from its left edge (background-position-x); a dot's is centred.
            const size = box ? 16 : Math.min(nw, nh) * 0.56;
            const ix = box ? p.x - nw / 2 + 12 : p.x - size / 2;
            parts.push(`<image href="${icon}" x="${num(ix)}" y="${num(p.y - size / 2)}" width="${num(size)}" height="${num(size)}"/>`);
        }
        const label = n.style('label');
        if (label) {
            const size = n.numericStyle('font-size');
            const weight = n.style('font-weight');
            const colour = n.style('color');
            if (box) {
                const maxW = n.numericStyle('text-max-width');
                const lines = wrapLines(label, maxW, `${weight} ${size}px ${font}`);
                const lh = size * 1.35;
                const top = p.y - ((lines.length - 1) * lh) / 2;
                const tx = p.x + (n.data('icon') ? 10 : 0);
                parts.push(`<text x="${num(tx)}" y="${num(top)}" text-anchor="middle" dominant-baseline="middle" font-size="${num(size)}" font-weight="${weight}" fill="${colour}">`
                    + lines.map((l, i) => `<tspan x="${num(tx)}" y="${num(top + i * lh)}">${esc(l)}</tspan>`).join('') + '</text>');
            } else {
                const ty = p.y + nh / 2 + n.numericStyle('text-margin-y') + size;
                parts.push(`<text x="${num(p.x)}" y="${num(ty)}" text-anchor="middle" font-size="${num(size)}" font-weight="${weight}" fill="${colour}" stroke="${ground}" stroke-width="${num(size * 0.22 * 2)}" stroke-linejoin="round" paint-order="stroke">${esc(label)}</text>`);
            }
        }
        parts.push('</g>');
        out.push(parts.join(''));
    }
    out.push('</svg>');
    return out.filter(Boolean).join('\n');
}

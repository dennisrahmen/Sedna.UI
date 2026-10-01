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
    pixel.fillStyle = 'transparent';
    pixel.fillStyle = css;
    pixel.fillRect(0, 0, 1, 1);
    const d = pixel.getImageData(0, 0, 1, 1).data;
    return [d[0], d[1], d[2], d[3] / 255];
}

/** Reads the orb's three roles. Called when it starts and whenever anything that colours it changes. */
function readColours(o) {
    const probe = o.probe, style = getComputedStyle(probe), out = {};
    for (const role of ROLES) {
        probe.style.color = `var(--orb-${role})`;
        out[role] = channels(style.color);
    }
    o.colours = out;
}

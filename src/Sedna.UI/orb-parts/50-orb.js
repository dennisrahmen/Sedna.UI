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

/* ── A run ────────────────────────────────────────────────────────────────────
   A workflow run, shown live on any graph. `data-state` on a record says where the run
   has got to — next, running, waiting, done, failed or skipped — and a record with none
   has not been reached yet. A link takes its target's state, so an app sets state on its
   records and the links follow; a link's own `data-state` wins. The state a link is drawn
   in is its `run` (toElements, 70-graph.js).

   The state is drawn on the border, a ring outside it and a halo (22-style.js), never on
   the fill, so a record keeps its tone. Two things move, and both are decoration: the halo
   of a running record breathes, and the dashes on a running link march from its source to
   its target. One timer per graph moves them and restyles only what is running, so a run
   on a large graph costs what is running rather than the size of the drawing. It stands
   still under prefers-reduced-motion, in a tab nobody is looking at, and when the theme
   sets its durations to 0s; the pace is the theme's own — `--progress-duration` for a dash
   to travel its length and its gap, `--pulse-duration` for one breath.
   ─────────────────────────────────────────────────────────────────────────── */

const FLOW_TICK = 50;
const FLOW_DASH = [6, 4];
const FLOW_PERIOD = FLOW_DASH[0] + FLOW_DASH[1];

// A duration token as the theme sets it — `1.1s`, `300ms` — in milliseconds; 0 if it says nothing.
function durationOf(g, token) {
    const m = /^(\d*\.?\d+)(ms|s)$/.exec(getComputedStyle(g.el).getPropertyValue(token).trim());
    return m ? Number(m[1]) * (m[2] === 's' ? 1000 : 1) : 0;
}

// What a run moves: the records running now, and the links data is flowing along.
const runningOf = g => g.cy.elements('node[state = "running"], edge[run = "running"]');

// Back at rest: the dashes where they started, the halo half lit. Only what moved is
// touched. Removing data does not always restyle what the removed data was read by, so
// the link could stand still mid-dash; what rested is restyled outright.
function rest(g, eles) {
    const moved = eles.filter(e => !e.removed() && (e.data('dash') !== undefined || e.data('breath') !== undefined));
    if (moved.empty()) return;
    g.cy.batch(() => moved.removeData('dash breath'));
    moved.updateStyle();
}

/* Starts, keeps or stops a run's motion: after new data, and when the tab, the reader's
   motion setting or the theme changes. */
function flow(g) {
    const cy = g.cy;
    if (!cy || g.disposed || cy.destroyed()) return;
    const now = runningOf(g);
    rest(g, (g.running || cy.collection()).not(now));
    g.running = now;
    const pace = { dash: durationOf(g, '--progress-duration'), breath: durationOf(g, '--pulse-duration') };
    const still = now.empty() || reducedMotion() || document.visibilityState !== 'visible' || !cy.container()
        || (pace.dash <= 0 && pace.breath <= 0);
    if (still) {
        clearTimeout(g.flowing);
        g.flowing = 0;
        rest(g, now);
        return;
    }
    g.pace = pace;
    if (g.flowing) return;
    const begun = Date.now();
    const tick = () => {
        g.flowing = 0;
        if (g.disposed || cy.destroyed()) return;
        const t = Date.now() - begun;
        const { dash, breath } = g.pace;
        // What a filter hides waits until it is shown again.
        const shown = g.running.filter(e => !e.removed() && !e.hasClass('hidden'));
        const links = shown.edges(), records = shown.nodes();
        cy.batch(() => {
            // A dash pattern runs from the source; a falling offset moves it towards the target.
            if (dash > 0 && links.nonempty()) links.data('dash', -(((t / dash) * FLOW_PERIOD) % FLOW_PERIOD));
            if (breath > 0 && records.nonempty()) records.data('breath', (1 - Math.cos((2 * Math.PI * t) / breath)) / 2);
        });
        g.flowing = setTimeout(tick, FLOW_TICK);
    };
    tick();
}

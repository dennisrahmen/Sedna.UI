/* ── The chart's front door (data-chart) ───────────────────────────────────────
   Every page loads this; few of them show a chart. So this part is small and draws
   nothing: it finds the app's `[data-chart]` elements and imports the chart module —
   Sedna.UI.chart.js, beside this file — the first time one of them is about to be
   seen. A page without a chart never downloads it, and nothing is added to the host
   page.

     <div class="chart" data-chart="column" id="orders" aria-label="Orders per month">
       <table class="chart-data" data-chart-data>…the data, and its text alternative…</table>
       <div class="chart-plot"></div>
     </div>

   Finding charts, starting one when it comes within a screen of the viewport (or at
   once with `data-chart-eager`), waiting on a prerendered Blazor page and disposing one
   whose element has gone are the shared surface loader's (42-surfaces.js). This part
   owns:
     * a chart's role and tab stop, the moment it is found;
     * the legend's buttons — `data-chart-toggle="<series>"` with `aria-pressed` —
       delegated from document, so a legend rendered after load needs no wiring. Which
       chart a button drives: the one named by `data-chart-for` on it or on a container
       around it, else the one it sits inside, else the one in the nearest
       `[data-chart-frame]` around it.

   Everything else is the module's. `sednaUi.chart.get(el)` hands a script its handle.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var SELECTOR = '[data-chart]';

    var charts = ui._.surface({
        name: 'chart',
        selector: SELECTOR,
        module: 'Sedna.UI.chart.js',
        eager: 'data-chart-eager',
        // Its role and its tab stop at once, even while held: an accessible name on an
        // element with no role is prohibited, and a chart below the fold starts late.
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'application');
            if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex', '0');
        }
    });

    function chartOf(el) {
        var named = el.closest('[data-chart-for]');
        if (named && named.getAttribute('data-chart-for')) {
            var target = document.getElementById(named.getAttribute('data-chart-for'));
            if (target && target.matches(SELECTOR)) return target;
        }
        var inside = el.closest(SELECTOR);
        if (inside) return inside;
        var frame = el.closest('[data-chart-frame]');
        return frame ? frame.querySelector(SELECTOR) : null;
    }

    ui.chart = {
        /* The chart's handle — an element, or its id — starting it if it has not
           started. Its members are listed in docs/architecture.md. */
        get: charts.get,

        /* Starts every chart in `root` (the document by default) at once, rather than
           when it scrolls near. Returns a promise of how many. */
        init: charts.init
    };

    // A legend key that switches its series. Pressed is shown, which is how a legend
    // reads before anything is switched off.
    document.addEventListener('click', function (e) {
        var key = e.target && e.target.closest ? e.target.closest('[data-chart-toggle]') : null;
        if (!key) return;
        var el = chartOf(key);
        if (!el) return;
        var shown = key.getAttribute('aria-pressed') === 'false';
        key.setAttribute('aria-pressed', shown ? 'true' : 'false');
        var name = key.getAttribute('data-chart-toggle');
        charts.get(el).then(function (chart) { chart.toggle(name, shown); }).catch(charts.report);
    });

})(window.sednaUi);

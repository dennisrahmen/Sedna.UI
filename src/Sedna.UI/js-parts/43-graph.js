/* ── The graph's front door (data-graph) ───────────────────────────────────────
   Every page loads this; almost none of them has a graph. So this part is small and
   holds no engine: it finds the app's `[data-graph]` elements, and imports the graph
   module — Sedna.UI.graph.js, beside this file — the first time one of them is about
   to be seen. A page without a graph never downloads the engine, and nothing is added
   to the host page: the module is found relative to this script, wherever the app
   serves it from.

     <div class="graph" data-graph id="deps" aria-label="Service dependencies">
       <ul class="graph-data" data-graph-data>
         <li data-node="api">Orders API</li>
         <li data-node="db">Orders database</li>
         <li data-edge data-source="api" data-target="db">Orders API reads the orders database</li>
       </ul>
       <div class="graph-canvas"></div>
     </div>

   Finding graphs, starting one when it comes within a screen of the viewport (or at
   once with `data-graph-eager`), waiting on a prerendered Blazor page and disposing one
   whose element has gone are the shared surface loader's (42-surfaces.js). This part
   owns:
     * a graph's role and tab stop, the moment it is found;
     * delegating the controls — `data-graph-action`, `data-graph-filter`,
       `data-graph-show`, `data-graph-search`, `data-graph-option` — from document, so a
       control rendered after load needs no wiring. Which graph a control drives: the
       one named by `data-graph-for` on it or on a container around it, else the one it
       sits inside, else the one in the nearest `[data-graph-frame]` around it;
     * the bridge ISednaGraph calls: `invoke(id, method, args)`.

   Everything else is the module's. `sednaUi.graph.get(el)` hands a script the engine's
   own handle, once it exists.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var SELECTOR = '[data-graph]';
    var CONTROLS = '[data-graph-action],[data-graph-filter],[data-graph-show],[data-graph-search],[data-graph-option]';

    var graphs = ui._.surface({
        name: 'graph',
        selector: SELECTOR,
        module: 'Sedna.UI.graph.js',
        eager: 'data-graph-eager',
        // Its role and its tab stop at once, even while held: an accessible name on an
        // element with no role is prohibited, and a graph below the fold starts late.
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'application');
            if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex', '0');
        }
    });

    // The graph a control drives: named by data-graph-for on it or on a container
    // around it — a panel, a toolbar — else the one it sits in, else the one in the
    // [data-graph-frame] around it.
    function graphOf(el) {
        if (!el || !el.closest) return null;
        if (el.matches(SELECTOR)) return el;
        var named = el.closest('[data-graph-for]');
        if (named && named.getAttribute('data-graph-for')) {
            var target = document.getElementById(named.getAttribute('data-graph-for'));
            if (target) return target;
        }
        var inside = el.closest(SELECTOR);
        if (inside) return inside;
        var frame = el.closest('[data-graph-frame]');
        return frame ? frame.querySelector(SELECTOR) : null;
    }

    ui.graph = {
        /* Called by Sedna.UI.lib.module.js once Blazor has started. Nothing else needs to. */
        release: graphs.release,

        /* Starts every graph in `root` (the document by default) that has not been
           started — at once, rather than when it scrolls near. Returns a promise of
           how many it started. */
        init: graphs.init,

        /* The engine's handle for a graph — an element, or its id — starting it if it
           has not started. The handle's members are listed in docs/architecture.md. */
        get: graphs.get,

        /* The ISednaGraph bridge. `method` is one of the handle's data-in, data-out
           members; the result is plain data or null. An id with no graph behind it
           warns and resolves null rather than throwing: an exception crossing the
           interop boundary from a Blazor handler tears the circuit down. */
        invoke: function (id, method, args) {
            var none = method === 'export' ? new Uint8Array(0) : null;
            var el = document.getElementById(id);
            if (!el || !el.matches(SELECTOR)) {
                try { console.warn('Sedna.UI graph: no [data-graph] element with id "' + id + '".'); } catch (e) { /* ignore */ }
                return Promise.resolve(none);
            }
            return graphs.get(el)
                .then(function (graph) { return graph.invoke(method, args || []); })
                .catch(function (e) {
                    graphs.report(e);
                    return none;
                });
        }
    };

    // Controls, delegated. Nothing happens until the module exists; a control on a page
    // whose graph has not started yet starts it.
    function control(e) {
        var c = e.target && e.target.closest ? e.target.closest(CONTROLS) : null;
        if (!c) return;
        // A text field answers to typing, everything else to its change or its click.
        var typing = c.hasAttribute('data-graph-search');
        if (e.type === 'input' && !typing) return;
        if (e.type === 'change' && typing) return;
        if (e.type === 'click' && (c.tagName === 'INPUT' || c.tagName === 'SELECT' || c.tagName === 'TEXTAREA')) return;
        var el = graphOf(c);
        if (!el) return;
        graphs.get(el).then(function (graph) { graph.control(c, e.type); }).catch(graphs.report);
    }
    document.addEventListener('click', control);
    document.addEventListener('change', control);
    document.addEventListener('input', control);

})(window.sednaUi);

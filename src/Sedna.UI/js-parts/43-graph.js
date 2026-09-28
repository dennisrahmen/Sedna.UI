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

   What it owns:
     * finding graphs — on load, after every render that adds one, and lazily: a graph
       starts when it comes within a screen of the viewport, unless it carries
       `data-graph-eager`;
     * disposing a graph whose element has left the document, so a Blazor navigation
       does not leave an engine running for a page that is gone;
     * waiting on a prerendered Blazor page. Interactive rendering replaces the markup
       the server prerendered, element for element, once the circuit or the WebAssembly
       runtime has started — so a graph drawn before that would be drawn twice, and
       flash its wait in between. When the page carries Blazor's interactive markers,
       graphs start only once Sedna.UI.lib.module.js reports that Blazor has started
       and the replaced markup has settled, or after a few seconds regardless;
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

    // Read while this script is running: currentScript is null once it has finished,
    // and the module is found relative to wherever the app serves this file from.
    var here = (function () {
        try { return document.currentScript && document.currentScript.src ? document.currentScript.src : null; }
        catch (e) { return null; }
    })();

    var loading = null;

    function load() {
        if (!loading) {
            var url = new URL('Sedna.UI.graph.js', here || document.baseURI).href;
            loading = import(url).catch(function (e) {
                loading = null;
                throw e;
            });
        }
        return loading;
    }

    var SELECTOR = '[data-graph]';
    var CONTROLS = '[data-graph-action],[data-graph-filter],[data-graph-show],[data-graph-search],[data-graph-option]';

    var watching = typeof IntersectionObserver === 'function'
        ? new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    watching.unobserve(entry.target);
                    start(entry.target);
                }
            });
        }, { rootMargin: '100% 0px' })
        : null;

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

    function start(el) {
        return load().then(function (m) { return m.attach(el); });
    }

    // Held while a prerendered Blazor page waits for its interactive render; see above.
    var held = false;
    var holdTimer = 0;

    function interactiveMarkers() {
        try {
            var walker = document.createTreeWalker(document.documentElement, NodeFilter.SHOW_COMMENT);
            for (var c = walker.nextNode(); c; c = walker.nextNode()) {
                if (c.data.indexOf('Blazor:') === 0 && /"type":"(server|webassembly|auto)"/.test(c.data)) return true;
            }
        } catch (e) { /* no walker: nothing to wait for */ }
        return false;
    }

    function release() {
        if (!held) return;
        clearTimeout(holdTimer);
        // The replaced markup is inserted in batches; start once the document has been
        // quiet for a moment, so a graph is drawn once, in the element that stays.
        var quiet = 0, since = Date.now();
        var settle = new MutationObserver(function () {
            clearTimeout(quiet);
            quiet = setTimeout(done, 60);
            if (Date.now() - since > 1000) done();
        });
        function done() {
            if (!held) return;
            held = false;
            settle.disconnect();
            clearTimeout(quiet);
            scan(document);
        }
        settle.observe(document.documentElement, { childList: true, subtree: true });
        quiet = setTimeout(done, 60);
    }

    function scan(root) {
        if (held) return;
        var found = [];
        if (root.matches && root.matches(SELECTOR)) found.push(root);
        if (root.querySelectorAll) {
            var inner = root.querySelectorAll(SELECTOR);
            for (var i = 0; i < inner.length; i++) found.push(inner[i]);
        }
        found.forEach(function (el) {
            if (el.__sednaGraphSeen) return;
            el.__sednaGraphSeen = true;
            if (watching && !el.hasAttribute('data-graph-eager')) watching.observe(el);
            else start(el).catch(report);
        });
    }

    function report(e) {
        try { console.error('Sedna.UI graph:', e); } catch (x) { /* ignore */ }
    }

    ui.graph = {
        /* Called by Sedna.UI.lib.module.js once Blazor has started. Nothing else needs to. */
        release: release,

        /* Starts every graph in `root` (the document by default) that has not been
           started — at once, rather than when it scrolls near. Returns a promise of
           how many it started. */
        init: function (root) {
            var list = [];
            var host = root || document;
            if (host.matches && host.matches(SELECTOR)) list.push(host);
            var inner = host.querySelectorAll ? host.querySelectorAll(SELECTOR) : [];
            for (var i = 0; i < inner.length; i++) list.push(inner[i]);
            return Promise.all(list.map(function (el) {
                el.__sednaGraphSeen = true;
                if (watching) watching.unobserve(el);
                return start(el);
            })).then(function (graphs) { return graphs.length; });
        },

        /* The engine's handle for a graph — an element, or its id — starting it if it
           has not started. The handle's members are listed in docs/architecture.md. */
        get: function (target) {
            var el = typeof target === 'string' ? document.getElementById(target) : target;
            if (!el || !el.matches || !el.matches(SELECTOR)) {
                return Promise.reject(new Error('No [data-graph] element: ' + target));
            }
            el.__sednaGraphSeen = true;
            if (watching) watching.unobserve(el);
            return start(el);
        },

        /* The ISednaGraph bridge. `method` is one of the handle's data-in, data-out
           members; the result is plain data or null. An id with no graph behind it
           warns and resolves null rather than throwing: an exception crossing the
           interop boundary from a Blazor handler tears the circuit down. */
        invoke: function (id, method, args) {
            var el = document.getElementById(id);
            if (!el || !el.matches(SELECTOR)) {
                try { console.warn('Sedna.UI graph: no [data-graph] element with id "' + id + '".'); } catch (e) { /* ignore */ }
                return Promise.resolve(null);
            }
            return this.get(el).then(function (graph) { return graph.invoke(method, args || []); });
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
        ui.graph.get(el).then(function (graph) { graph.control(c, e.type); }).catch(report);
    }
    document.addEventListener('click', control);
    document.addEventListener('change', control);
    document.addEventListener('input', control);

    function boot() {
        if (interactiveMarkers()) {
            held = true;
            holdTimer = setTimeout(release, 4000);
        }
        scan(document);
        if (typeof MutationObserver !== 'function') return;
        // A graph rendered later is found by the render that added it; one removed is
        // disposed by the render that removed it — checked on a timer, not at once,
        // because Blazor moves an element by removing and re-inserting it.
        var queued = false;
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    if (added[j].nodeType === 1) scan(added[j]);
                }
                if (records[i].removedNodes.length && !queued && loading) {
                    queued = true;
                    setTimeout(function () {
                        queued = false;
                        loading.then(function (m) { m.sweep(); }).catch(function () { /* nothing to sweep */ });
                    }, 0);
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else boot();

})(window.sednaUi);

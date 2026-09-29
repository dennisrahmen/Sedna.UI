/* ── Loading a tier 3 surface's module ───────────────────────────────────────
   What every shipped surface's front door needs, once. A surface with an engine — the
   graph, the rich-text editor — keeps the engine out of this script: its front door
   finds the app's elements and imports the surface's own module, beside this file, the
   first time one of them is about to be seen. A page that shows none never downloads
   the engine, and nothing is added to the host page.

     var graphs = ui._.surface({
         selector: '[data-graph]',          what the app writes
         module: 'Sedna.UI.graph.js',       the ES module beside this file; it exports attach and sweep
         eager: 'data-graph-eager',         start at once rather than when scrolled near
         found: function (el) { … }         runs as soon as an element is seen, even while held
     });
     graphs.get(el) → a promise of the module's handle, starting it if need be

   What it owns, for every surface registered:
     * finding elements — on load, after every render that adds one, and lazily: one
       starts when it comes within a screen of the viewport, unless it is eager;
     * disposing one whose element has left the document (the module's `sweep`), so a
       Blazor navigation does not leave an engine running for a page that is gone;
     * waiting on a prerendered Blazor page. Interactive rendering replaces the markup
       the server prerendered, element for element, once the circuit or the WebAssembly
       runtime has started — so a surface started before that would start twice, in
       markup about to go. When the page carries Blazor's interactive markers, surfaces
       start only once Sedna.UI.lib.module.js calls `release`, and the replaced markup
       has settled, or after a few seconds regardless.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    // Read while this script is running: currentScript is null once it has finished,
    // and a module is found relative to wherever the app serves this file from.
    var here = (function () {
        try { return document.currentScript && document.currentScript.src ? document.currentScript.src : null; }
        catch (e) { return null; }
    })();

    var surfaces = [];
    var held = false;
    var holdTimer = 0;
    var booted = false;

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
        // quiet for a moment, so a surface starts once, in the element that stays.
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
            surfaces.forEach(function (s) { s.scan(document); });
        }
        settle.observe(document.documentElement, { childList: true, subtree: true });
        quiet = setTimeout(done, 60);
    }

    function boot() {
        if (booted) return;
        booted = true;
        if (interactiveMarkers()) {
            held = true;
            holdTimer = setTimeout(release, 4000);
        }
        surfaces.forEach(function (s) { s.scan(document); });
        if (typeof MutationObserver !== 'function') return;
        // One rendered later is found by the render that added it; one removed is
        // disposed by the render that removed it — checked on a timer, not at once,
        // because Blazor moves an element by removing and re-inserting it.
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    if (added[j].nodeType === 1) surfaces.forEach(function (s) { s.scan(added[j]); });
                }
                var removed = records[i].removedNodes;
                for (var k = 0; k < removed.length; k++) {
                    if (removed[k].nodeType === 1) surfaces.forEach(function (s) { s.forget(removed[k]); });
                }
                if (removed.length) surfaces.forEach(function (s) { s.sweepSoon(); });
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }

    function surface(spec) {
        var loading = null;
        var seen = '__sednaSeen:' + spec.module;
        var started = '__sednaStarted:' + spec.module;
        var queued = false;

        function load() {
            if (!loading) {
                var url = new URL(spec.module, here || document.baseURI).href;
                loading = import(url).catch(function (e) {
                    loading = null;
                    throw e;
                });
            }
            return loading;
        }

        var watching = typeof IntersectionObserver === 'function'
            ? new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        watching.unobserve(entry.target);
                        start(entry.target).catch(report);
                    }
                });
            }, { rootMargin: '100% 0px' })
            : null;

        function start(el) {
            el[started] = true;
            return load().then(function (m) { return m.attach(el); });
        }

        function report(e) {
            try { console.error('Sedna.UI ' + (spec.name || spec.module) + ':', e); } catch (x) { /* ignore */ }
        }

        function within(root) {
            var found = [];
            if (root.matches && root.matches(spec.selector)) found.push(root);
            if (root.querySelectorAll) {
                var inner = root.querySelectorAll(spec.selector);
                for (var i = 0; i < inner.length; i++) found.push(inner[i]);
            }
            return found;
        }

        var self = {
            load: load,
            report: report,
            scan: function (root) {
                var found = within(root);
                if (spec.found) found.forEach(spec.found);
                if (held) return;
                found.forEach(function (el) {
                    if (el[seen]) return;
                    el[seen] = true;
                    if (watching && !(spec.eager && el.hasAttribute(spec.eager))) watching.observe(el);
                    else start(el).catch(report);
                });
            },
            // One removed before it ever came near the viewport is no longer watched — and
            // forgotten, so that if Blazor was only moving it, the insert that follows finds it.
            forget: function (root) {
                if (!watching) return;
                within(root).forEach(function (el) {
                    if (el[started]) return;
                    watching.unobserve(el);
                    el[seen] = false;
                });
            },
            sweepSoon: function () {
                if (queued || !loading) return;
                queued = true;
                setTimeout(function () {
                    queued = false;
                    loading.then(function (m) { m.sweep(); }).catch(function () { /* nothing to sweep */ });
                }, 0);
            },
            /* A promise of the handle for an element or an id, starting it if need be. */
            get: function (target) {
                var el = typeof target === 'string' ? document.getElementById(target) : target;
                if (!el || !el.matches || !el.matches(spec.selector)) {
                    return Promise.reject(new Error('No ' + spec.selector + ' element: ' + target));
                }
                el[seen] = true;
                if (watching) watching.unobserve(el);
                return start(el);
            },
            /* Starts every one in `root` (the document by default) at once, rather than
               when it scrolls near. A promise of how many. */
            init: function (root) {
                var list = within(root || document);
                return Promise.all(list.map(self.get)).then(function (all) { return all.length; });
            },
            release: release
        };
        surfaces.push(self);
        if (booted) self.scan(document);
        return self;
    }

    ui._.surface = surface;

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else setTimeout(boot, 0);

})(window.sednaUi);

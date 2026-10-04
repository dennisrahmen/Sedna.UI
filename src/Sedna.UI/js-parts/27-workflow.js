/* ── Workflow, follow the step at work ──────────────────────────────────────────
   Opt-in wiring for a .workflow wider than its container, so the step at work stays
   in view while a run moves on:

     <ol class="workflow" data-follow>…</ol>

   The step at work is the last step marked running, else the last one waiting, else
   the first one marked next. When that changes — a render rewrites a data-state, or
   adds steps — the diagram scrolls until the step's node is in the middle, or as near
   as the diagram can scroll: sideways across, and down as well in a diagram run top to
   bottom in a box of fixed height. Only the diagram moves, never the page.

   Following is the same mode as an output pane's, and the reader's to break. Scrolling
   the diagram until the step at work is out of view releases it; scrolling it back
   into view takes it up again. While it is released the diagram carries
   data-follow-paused, and `sedna-follow` fires on each change with
   `detail.following`. A `data-workflow-follow` button brings the step back and
   follows again — the diagram is the one its `aria-controls` names, or the nearest
   following diagram around the button.

   The app owns whether a diagram follows at all: `data-follow` is the switch, and
   adding it again jumps to the step at work.

   The scroll is smooth unless the reader asked for reduced motion. Only a scroll that
   follows the reader's own input — a wheel, a touch, a press, a key — can release
   following. The diagram also scrolls when this part centres a step, and when a render
   changes its width under the scroll position, and neither of those is the reader
   looking away.

   Out of view. Each part of a diagram — a step, a fork, a loop — that is well out of
   view carries

     data-workflow-away   68-workflow.css lets the browser skip rendering it

   until it comes back. Whenever anything in a diagram moves the browser repaints the
   whole of it, so this is what keeps a run of hundreds of steps as cheap to watch as
   a short one. The margin is half the view on every side, so a part at the edge — and
   the wire it draws into the gap before it — is always drawn. It is an attribute an
   app never renders, so a Blazor re-render leaves it be. Nothing here is a public
   member.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var STATE = '_sednaWorkflow';
    var PAUSED = 'data-follow-paused';
    // How long after the reader's input a scroll still counts as theirs.
    var READER = 1000;
    // How long a smooth scroll gets to start moving before it is landed at once.
    var LAND = 1000;

    function stepAtWork(canvas) {
        var running = canvas.querySelectorAll('.workflow-step[data-state="running"]');
        if (running.length) return running[running.length - 1];
        var waiting = canvas.querySelectorAll('.workflow-step[data-state="waiting"]');
        if (waiting.length) return waiting[waiting.length - 1];
        return canvas.querySelector('.workflow-step[data-state="next"]');
    }

    function nodeOf(step) {
        return step.querySelector(':scope > .workflow-node, :scope > .workflow-end') || step;
    }

    /* "In view" is the node's middle inside what the diagram shows: a reader who nudges
       the diagram a little is still watching the step. Both ways, because a diagram run
       top to bottom in a box of fixed height scrolls down rather than across. */
    function inView(canvas, step) {
        var box = canvas.getBoundingClientRect();
        var node = nodeOf(step).getBoundingClientRect();
        var x = node.left + node.width / 2, y = node.top + node.height / 2;
        return x >= box.left && x <= box.right && y >= box.top && y <= box.bottom;
    }

    function reducedMotion() {
        return !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    }

    function setFollowing(canvas, following) {
        var state = canvas[STATE];
        if (!state) return;
        var enabled = canvas.hasAttribute('data-follow');
        var changed = state.following !== following;
        state.following = following;
        canvas.toggleAttribute(PAUSED, enabled && !following);
        if (!changed || !enabled) return;
        try {
            canvas.dispatchEvent(new CustomEvent('sedna-follow', {
                bubbles: true,
                detail: { following: following }
            }));
        } catch (e) { /* the mark still says it */ }
    }

    function centre(canvas, smooth) {
        var state = canvas[STATE];
        var step = stepAtWork(canvas);
        if (!state || !step) return;
        var box = canvas.getBoundingClientRect();
        var node = nodeOf(step).getBoundingClientRect();
        var dx = (node.left + node.width / 2) - (box.left + box.width / 2);
        var dy = (node.top + node.height / 2) - (box.top + box.height / 2);
        var smoothly = smooth && !reducedMotion();
        var left = canvas.scrollLeft, top = canvas.scrollTop;
        // To a place, not by an amount: a smooth scrollBy adds onto one still under way, so
        // a burst of changes would overshoot by the sum of them. A diagram that cannot
        // scroll one way is simply held where it is that way.
        canvas.scrollTo({ left: left + dx, top: top + dy, behavior: smoothly ? 'smooth' : 'auto' });
        clearTimeout(state.land);
        if (!smoothly) return;
        // A smooth scroll runs on animation frames, and a tab that is not being painted
        // gets none: one that has not moved by now is landed at once.
        state.land = setTimeout(function () {
            if (canvas.scrollLeft === left && canvas.scrollTop === top) centre(canvas, false);
        }, LAND);
    }

    function bind(canvas) {
        if (canvas[STATE]) return;
        var state = canvas[STATE] = { following: true, input: 0, queued: 0, land: 0 };

        function readerInput() { state.input = Date.now(); }
        canvas.addEventListener('wheel', readerInput, { passive: true });
        canvas.addEventListener('touchstart', readerInput, { passive: true });
        canvas.addEventListener('pointerdown', readerInput);
        canvas.addEventListener('keydown', readerInput);

        canvas.addEventListener('scroll', function () {
            if (Date.now() - state.input > READER) return;
            var step = stepAtWork(canvas);
            setFollowing(canvas, !step || inView(canvas, step));
        });

        // One render rewrites several states at once — the step that finished and the
        // one that started — so the scroll waits for the last of them.
        new MutationObserver(function () {
            if (!canvas.hasAttribute('data-follow') || !state.following) return;
            clearTimeout(state.queued);
            state.queued = setTimeout(function () { centre(canvas, true); }, 0);
        }).observe(canvas, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-state'] });

        // Opens on the step at work. A diagram that fits has nowhere to scroll to.
        centre(canvas, false);
    }

    function follow(canvas) {
        if (!canvas) return;
        bind(canvas);
        setFollowing(canvas, true);
        centre(canvas, true);
    }

    ui.workflow = {
        /* Brings the step at work into view and follows again — for a button, and for
           an app that changes states the observer cannot see. */
        follow: follow,

        /* Whether the step at work is in view, or there is none. */
        isFollowing: function (canvas) {
            if (!canvas) return false;
            var step = stepAtWork(canvas);
            return !step || inView(canvas, step);
        }
    };

    function canvasFor(button) {
        var id = button.getAttribute('aria-controls');
        if (id) return document.getElementById(id);
        for (var el = button.parentElement; el; el = el.parentElement) {
            var found = el.querySelector('.workflow[data-follow]');
            if (found) return found;
        }
        return null;
    }

    document.addEventListener('click', function (e) {
        var button = e.target && e.target.closest ? e.target.closest('[data-workflow-follow]') : null;
        if (!button) return;
        var canvas = canvasFor(button);
        if (!canvas) return;
        e.preventDefault();
        follow(canvas);
    });

    function bindAll(root) {
        var found = (root || document).querySelectorAll('.workflow[data-follow]');
        for (var i = 0; i < found.length; i++) bind(found[i]);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { bindAll(document); });
    } else {
        bindAll(document);
    }

    // A diagram rendered later is picked up here, and the app switching `data-follow`
    // off and on again is seen by the same observer.
    new MutationObserver(function (records) {
        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (record.type === 'attributes') {
                var canvas = record.target;
                if (!canvas.matches || !canvas.matches('.workflow')) continue;
                var on = canvas.hasAttribute('data-follow');
                if (on && record.oldValue !== null) continue;
                if (on) follow(canvas);
                else canvas.removeAttribute(PAUSED);
                continue;
            }
            var added = record.addedNodes;
            for (var j = 0; j < added.length; j++) {
                if (added[j].nodeType !== 1) continue;
                if (added[j].matches && added[j].matches('.workflow[data-follow]')) bind(added[j]);
                bindAll(added[j]);
            }
        }
    }).observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeOldValue: true,
        attributeFilter: ['data-follow']
    });

    // ── Out of view ──
    // Swept when what is in view can change — a scroll anywhere, a resize, a change
    // inside a diagram — and never while a diagram only sits there moving its light: an
    // observer of every part is checked again on every frame that paints.
    var AWAY = 'data-workflow-away';
    var queued = false;

    function sweep(diagram) {
        var box = diagram.getBoundingClientRect();
        var vw = window.innerWidth, vh = window.innerHeight;
        // What can be seen of the diagram, with half the view again on every side.
        var left = Math.max(box.left, 0) - vw / 2, right = Math.min(box.right, vw) + vw / 2;
        var top = Math.max(box.top, 0) - vh / 2, bottom = Math.min(box.bottom, vh) + vh / 2;
        var items = diagram.children;
        for (var i = 0; i < items.length; i++) {
            var r = items[i].getBoundingClientRect();
            var away = r.right < left || r.left > right || r.bottom < top || r.top > bottom;
            if (items[i].hasAttribute(AWAY) !== away) items[i].toggleAttribute(AWAY, away);
        }
    }

    function sweepAll() {
        queued = false;
        var all = document.querySelectorAll('.workflow');
        for (var i = 0; i < all.length; i++) sweep(all[i]);
    }

    // Two frames on: a part has to be drawn once before it is let go, or the browser has
    // no size of it to keep and it folds to nothing — at load, and for a part just added.
    function soon() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(function () { requestAnimationFrame(sweepAll); });
    }

    function inDiagram(node) {
        var el = node.nodeType === 1 ? node : node.parentElement;
        return !!(el && el.closest && (el.closest('.workflow') || el.querySelector('.workflow')));
    }

    function watchAll() {
        soon();
        document.addEventListener('scroll', soon, { capture: true, passive: true });
        window.addEventListener('resize', soon, { passive: true });
        // Parts added or taken away, and a state that resizes a node. The mark itself is
        // an attribute this does not watch, or it would sweep again on its own sweep.
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                if (inDiagram(records[i].target)) { soon(); return; }
            }
        }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-state', 'class'] });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watchAll);
    else watchAll();

})(window.sednaUi);

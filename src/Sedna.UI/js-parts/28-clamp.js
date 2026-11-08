/* ── Show more, for a clamped block ──────────────────────────────────────────
   A `.text-clamp-2` / `.text-clamp-3` block is cut after its lines by CSS. This is
   the button that opens it in full and closes it again:

     <p class="text-clamp-3" id="note-4209">…</p>
     <button class="btn-bare btn-bare--link" type="button" data-clamp-toggle
             aria-controls="note-4209" aria-expanded="false">
       <span class="text-clamp-more">Show more</span>
       <span class="text-clamp-less">Show less</span>
     </button>

   The block is the element `aria-controls` names, else the element just before the
   button. A press flips the button's `aria-expanded` and writes `data-clamp-open` on
   the block, which lifts the clamp in the stylesheet; which label shows follows
   `aria-expanded`, so no word is the script's.

   While the block is closed and its text fits inside the clamp, the button carries
   `data-clamp-fits` and the stylesheet hides it. That is measured when the page loads,
   when content arrives, and whenever the block changes size — a narrower column can
   make a paragraph that fitted overflow.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var OPEN = 'data-clamp-open';
    var FITS = 'data-clamp-fits';

    function blockOf(button) {
        var id = button.getAttribute('aria-controls');
        var named = id && document.getElementById(id);
        return named || button.previousElementSibling;
    }

    function isOpen(button) { return button.getAttribute('aria-expanded') === 'true'; }

    /* Open or closed, the block follows the button: a re-render that wrote the button's
       `aria-expanded` back is a state the block takes up on the next measure. */
    function measure(button) {
        var block = blockOf(button);
        if (!block) return;
        var open = isOpen(button);
        block.toggleAttribute(OPEN, open);
        if (open) { button.removeAttribute(FITS); return; }
        button.toggleAttribute(FITS, block.scrollHeight <= block.clientHeight + 1);
    }

    var sizes = 'ResizeObserver' in window ? new ResizeObserver(function () { schedule(); }) : null;
    var watched = typeof WeakSet === 'function' ? new WeakSet() : null;
    var pending = false;

    function refresh(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var all = scope.querySelectorAll('[data-clamp-toggle]');
        for (var i = 0; i < all.length; i++) {
            measure(all[i]);
            var block = blockOf(all[i]);
            if (sizes && block && watched && !watched.has(block)) {
                watched.add(block);
                sizes.observe(block);
            }
        }
        return all.length;
    }

    function schedule() {
        if (pending) return;
        pending = true;
        requestAnimationFrame(function () { pending = false; refresh(); });
    }

    ui.clamp = {
        /* Opens or closes the block a toggle controls; `open` forces one way. Returns
           whether it is open afterwards. */
        toggle: function (button, open) {
            if (typeof button === 'string') button = document.getElementById(button);
            if (!button) return false;
            var next = open === undefined ? !isOpen(button) : !!open;
            button.setAttribute('aria-expanded', String(next));
            measure(button);

            // A collapse can leave the reader below what they were reading. Bring the
            // block's start back into view, and only when it has gone out of it.
            var block = blockOf(button);
            if (!next && block && block.getBoundingClientRect().top < 0) block.scrollIntoView({ block: 'start' });
            return next;
        },
        /* Measures every toggle in `root` again — for content an app changed in a way
           no size change follows. Returns how many there are. */
        refresh: refresh
    };

    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-clamp-toggle]');
        if (!button) return;
        e.preventDefault();
        ui.clamp.toggle(button);
    });

    function watch() {
        refresh();
        window.addEventListener('resize', schedule);
        if (document.fonts && document.fonts.ready) document.fonts.ready.then(schedule, function () {});
        if (!('MutationObserver' in window)) return;
        // Content only: the marks are attributes, and watching those would loop.
        new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true, characterData: true });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();

})(window.sednaUi);

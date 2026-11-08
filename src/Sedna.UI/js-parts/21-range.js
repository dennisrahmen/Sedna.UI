/* ── Range box: the low end stays below the high end, the fill and the bubble follow ──
   `.form-range-box` (48-form-numeric.css) draws the track and the fill of the one or
   two native ranges inside it. This keeps what CSS cannot read in custom properties on
   the app's own elements, as ratios of the track from 0 to 1:

     --range-start, --range-end   on the box: where the fill begins and ends
     --range-at                   on each `output.form-range-bubble`: where its input's thumb is

   Two inputs are the low and the high end of one span, in document order. When either
   is moved past the other it is put back level with it, before any other listener
   hears the `input` event, so a binding never reads a span whose low end is above its
   high end. Nothing new is dispatched: the inputs' own `input` and `change` stay the
   events.

   A bubble with `data-range-value`, or the element inside it that carries the
   attribute, is given its input's value as text. Leave the attribute off where the
   app writes the text itself — a binding and this would fight over the same node.

   It follows every `input` and `reset`, and a render that adds or swaps markup. A
   value set from code without either — a binding writing `value` — is picked up at
   the next of those; dispatch an `input` event on the range after setting one.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var BOX = '.form-range-box';

    function rangesOf(box) {
        var out = [];
        for (var i = 0; i < box.children.length; i++) {
            var c = box.children[i];
            if (c.tagName === 'INPUT' && c.type === 'range') out.push(c);
        }
        return out;
    }

    function ratio(input) {
        var min = parseFloat(input.min), max = parseFloat(input.max), v = parseFloat(input.value);
        if (isNaN(min)) min = 0;
        if (isNaN(max)) max = 100;
        if (!(max > min) || isNaN(v)) return 0;
        return Math.min(1, Math.max(0, (v - min) / (max - min)));
    }

    function inputFor(output, ranges) {
        var ids = (output.getAttribute('for') || '').split(/\s+/);
        for (var i = 0; i < ranges.length; i++) {
            if (ranges[i].id && ids.indexOf(ranges[i].id) >= 0) return ranges[i];
        }
        return ranges.length === 1 ? ranges[0] : null;
    }

    function sync(box, moved) {
        var ranges = rangesOf(box);
        if (!ranges.length) return;
        var lo = ranges.length > 1 ? ranges[0] : null;
        var hi = ranges[ranges.length - 1];

        if (lo && parseFloat(lo.value) > parseFloat(hi.value)) {
            if (moved === hi) hi.value = lo.value;
            else lo.value = hi.value;
        }

        box.style.setProperty('--range-start', String(lo ? ratio(lo) : 0));
        box.style.setProperty('--range-end', String(ratio(hi)));

        for (var i = 0; i < box.children.length; i++) {
            var out = box.children[i];
            if (out.tagName !== 'OUTPUT') continue;
            var input = inputFor(out, ranges);
            if (!input) continue;
            out.style.setProperty('--range-at', String(ratio(input)));
            var slot = out.hasAttribute('data-range-value') ? out : out.querySelector('[data-range-value]');
            if (slot && slot.textContent !== input.value) slot.textContent = input.value;
        }
    }

    function syncAll() {
        var boxes = document.querySelectorAll(BOX);
        for (var i = 0; i < boxes.length; i++) sync(boxes[i]);
    }

    var queued = false;
    function soon() {
        if (queued) return;
        queued = true;
        (window.queueMicrotask || setTimeout)(function () { queued = false; syncAll(); });
    }

    function onInput(e) {
        var t = e.target;
        if (!(t instanceof HTMLInputElement) || t.type !== 'range') return;
        var box = t.parentElement;
        if (box && box.matches(BOX)) sync(box, t);
    }

    function init() {
        syncAll();
        // Capturing on the document runs before a listener on the input or a delegated
        // one on the document's bubble phase, which is where a framework binds.
        document.addEventListener('input', onInput, true);
        document.addEventListener('reset', function () { setTimeout(soon, 0); }, true);
        if (typeof MutationObserver === 'function') {
            new MutationObserver(soon).observe(document.documentElement, { childList: true, subtree: true });
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

})(window.sednaUi);

/* ── A closing drawer slides out in every engine ────────────────────────────
   `dialog.drawer` — a drawer or a sheet — slides in and out with a CSS transition
   that keeps the closing dialog displayed through `display` and `overlay`, both
   `allow-discrete`. Safari has no `overlay` property and takes the dialog away the
   moment it closes, so the panel vanishes instead of leaving.

   Where the engine lacks `overlay`, this plays the same slide after the fact: the
   closed dialog is held displayed and visible for the length of its own transform
   transition, animated from where an open panel sits to where the stylesheet puts a
   closed one, and then let go. Nothing is added to the page and nothing is decided
   here — the duration, the easing and both ends are read from the stylesheet, so a
   theme, a variant and reduced motion (a zero duration) apply as they do elsewhere.
   `modal.idle` waits for it like any other animation on the dialog.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var supported = false;
    try { supported = !!(window.CSS && CSS.supports('overlay', 'auto')); } catch (e) { /* no CSS.supports */ }
    if (supported) return;

    var HELD = 'data-sedna-closing';

    // Dialogs seen open since they last slid out, so a later idle() on a dialog closed
    // long ago never replays the slide.
    var opened = new WeakSet();
    function watch(record) {
        var d = record.target;
        if (d.tagName === 'DIALOG' && d.open) opened.add(d);
    }
    if (typeof MutationObserver === 'function') {
        new MutationObserver(function (records) { records.forEach(watch); })
            .observe(document.documentElement, { attributes: true, attributeFilter: ['open'], subtree: true });
    }
    document.querySelectorAll('dialog[open]').forEach(function (d) { opened.add(d); });

    function seconds(value) {
        var n = parseFloat(value);
        if (!isFinite(n)) return 0;
        return /ms\s*$/.test(value) ? n / 1000 : n;
    }

    // The transform transition's own duration and easing, from the comma lists.
    function timing(style) {
        var props = style.transitionProperty.split(',').map(function (p) { return p.trim(); });
        var at = props.indexOf('transform');
        if (at < 0) at = props.indexOf('all');
        if (at < 0) return null;
        var pick = function (list) { var a = list.split(','); return (a[at % a.length] || '').trim(); };
        var duration = seconds(pick(style.transitionDuration)) * 1000;
        return duration > 0 ? { duration: duration, easing: pick(style.transitionTimingFunction) || 'ease' } : null;
    }

    function release(d) {
        if (!d.hasAttribute(HELD)) return;
        d.removeAttribute(HELD);
        d.style.removeProperty('display');
    }

    // Starts the slide for a dialog that has just closed, once: from the `close` event,
    // or sooner from modal.idle(), which is called straight after close() and has to
    // see the animation it is about to wait for — the event comes a task later.
    function slideOut(d) {
        if (!d || d.tagName !== 'DIALOG' || d.open || !opened.has(d)) return;
        opened.delete(d);
        if (d.hasAttribute(HELD) || !d.matches('.drawer')) return;
        var closed = getComputedStyle(d);
        var t = timing(closed);
        if (!t) return;
        var to = closed.transform;
        d.setAttribute(HELD, '');
        d.style.setProperty('display', 'flex');
        var run;
        try {
            run = d.animate(
                [{ transform: 'none', visibility: 'visible' }, { transform: to, visibility: 'visible' }],
                { duration: t.duration, easing: t.easing });
        } catch (err) {
            release(d);
            return;
        }
        var done = function () { release(d); };
        run.onfinish = done;
        run.oncancel = done;
    }

    ui._.slideOut = slideOut;

    document.addEventListener('close', function (e) { slideOut(e.target); }, true);

    // Opened again mid-slide: the slide stops where it is and the open state takes over.
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (!d || d.tagName !== 'DIALOG' || !d.open || !d.hasAttribute(HELD)) return;
        d.getAnimations().forEach(function (a) { a.cancel(); });
        release(d);
    }, true);
})(window.sednaUi);

/* ── A closing drawer slides out in every engine ────────────────────────────
   `dialog.drawer` — a drawer or a sheet — slides in and out with a CSS transition
   that keeps the closing dialog displayed through `display` and `overlay`, both
   `allow-discrete`, and its backdrop fades with it (63-drawer.css). Safari has no
   `overlay` property and takes the dialog away the moment it closes, with its
   backdrop, so the panel vanishes instead of leaving.

   Where the engine lacks `overlay`, this plays the same exit after the fact: the
   closed dialog is held displayed for the length of its own transform transition and
   slid from where an open panel sits to where the stylesheet puts a closed one, and
   then let go. Where it sat is its open transform — none, or the height a snapping
   sheet rests at (`--sheet-rest`) — or, for a sheet dragged shut, where the finger let
   go, which 29-sheet.js hands over. The backdrop is not there to fade, so a held
   modal dialog dims the page itself in 63-drawer.css, with a spread shadow that fades
   over the same duration: the script writes that duration, and the easing, from the
   transform's own. Nothing is added to the page and nothing is decided here — the
   duration, the easing and both ends are read from the stylesheet, so a theme, a
   variant and reduced motion (a zero duration) apply as they do elsewhere.
   `modal.idle` waits for it like any other animation on the dialog.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var supported = false;
    try { supported = !!(window.CSS && CSS.supports('overlay', 'auto')); } catch (e) { /* no CSS.supports */ }
    if (supported) return;

    var HELD = 'data-sedna-closing';

    // Dialogs seen open since they last slid out, so a later idle() on a dialog closed
    // long ago never replays the slide; the ones among them that were modal, which are
    // the ones with a backdrop to fade; and where a dragged sheet was let go.
    var opened = new WeakSet();
    var modal = new WeakSet();
    var dropped = new WeakMap();
    function seen(d) {
        opened.add(d);
        dropped.delete(d);
        if (d.matches(':modal')) modal.add(d); else modal.delete(d);
    }
    function watch(record) {
        var d = record.target;
        if (d.tagName === 'DIALOG' && d.open) seen(d);
    }
    if (typeof MutationObserver === 'function') {
        new MutationObserver(function (records) { records.forEach(watch); })
            .observe(document.documentElement, { attributes: true, attributeFilter: ['open'], subtree: true });
    }
    document.querySelectorAll('dialog[open]').forEach(seen);

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
        d.style.removeProperty('--sedna-exit-duration');
        d.style.removeProperty('--sedna-exit-easing');
    }

    /* Where an open panel sits: where a sheet was let go, else the transform the open
       rules give it — none, but for a snapping sheet the height it rests at. */
    function openTransform(d, style) {
        var drop = dropped.get(d);
        dropped.delete(d);
        // Only a drop that is this close: a sheet dragged shut that asked first may be
        // closed later by its button, from the rest.
        if (drop && performance.now() - drop.time < 250) return drop.transform;
        if (d.hasAttribute('data-sheet-snap')) {
            var rest = style.getPropertyValue('--sheet-rest').trim();
            if (rest) return 'translateY(' + rest + ')';
        }
        return 'none';
    }

    // Starts the exit for a dialog that has just closed, once: from the `close` event,
    // or sooner from modal.idle(), which is called straight after close() and has to
    // see the animation it is about to wait for — the event comes a task later.
    function slideOut(d) {
        if (!d || d.tagName !== 'DIALOG' || d.open || !opened.has(d)) return;
        opened.delete(d);
        if (d.hasAttribute(HELD) || !d.matches('.drawer')) return;
        var t = timing(getComputedStyle(d));
        if (!t) return;
        // A closed dialog has no box, and a percentage translation has no matrix
        // without one: displayed, it computes to where the stylesheet puts it closed.
        d.setAttribute(HELD, modal.has(d) ? 'modal' : '');
        d.style.setProperty('display', 'flex');
        d.style.setProperty('--sedna-exit-duration', t.duration + 'ms');
        d.style.setProperty('--sedna-exit-easing', t.easing);
        var style = getComputedStyle(d);
        var to = style.transform;
        var from = openTransform(d, style);
        var run;
        try {
            run = d.animate(
                [{ transform: from, visibility: 'visible' }, { transform: to, visibility: 'visible' }],
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
    // 29-sheet.js: the transform a dragged sheet is let go at, as it closes.
    ui._.slideFrom = function (d, transform) { if (transform) dropped.set(d, { transform: transform, time: performance.now() }); };

    document.addEventListener('close', function (e) { slideOut(e.target); }, true);

    // Opened again mid-slide: the slide stops where it is and the open state takes over.
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (!d || d.tagName !== 'DIALOG' || !d.open || !d.hasAttribute(HELD)) return;
        d.getAnimations().forEach(function (a) { a.cancel(); });
        release(d);
    }, true);
})(window.sednaUi);

/* ── Read-only choice controls ───────────────────────────────────────────────
   A text field has `readonly`; a select, a check, a radio, a switch, a segmented group,
   a range and a colour do not, and `aria-readonly="true"` only says so. 69-busy.css
   paints them and takes the pointer away; this stops the keyboard changing them, so the
   value stays what the app put there while it can still be focused, read and copied.

   On a check, a radio or a switch the change arrives as a click, Space included, and
   cancelling it puts the box back. Everywhere else it is a key: Tab, Escape and the
   copy shortcuts still go through, and a key that would pick or step does not. */
(function () {

    function readonly(el) {
        return el instanceof Element && !!el.closest('[aria-readonly="true"]')
            && !!el.closest('input, select, .segmented, .choice-tiles, .form-combo');
    }

    document.addEventListener('click', function (e) {
        var t = e.target;
        if (t instanceof HTMLInputElement && (t.type === 'checkbox' || t.type === 'radio') && readonly(t)) e.preventDefault();
    }, true);

    document.addEventListener('keydown', function (e) {
        var t = e.target;
        if (!readonly(t)) return;
        if (e.key === 'Tab' || e.key === 'Escape' || e.ctrlKey || e.metaKey || e.altKey) return;
        if (t instanceof HTMLInputElement && t.type === 'text') return;   // a combo's input carries `readonly` itself
        e.preventDefault();
    }, true);

    document.addEventListener('mousedown', function (e) {
        if (e.target instanceof HTMLSelectElement && readonly(e.target)) e.preventDefault();
    }, true);

})();

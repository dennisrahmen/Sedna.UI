/* ── Full screen ──────────────────────────────────────────────────────────────
   A button that puts part of the page into full screen and takes it out again — a run
   shown on a wall screen, a dashboard on a tablet, a diagram given the whole display:

     <button class="btn" type="button" data-fullscreen aria-controls="wall" aria-pressed="false">
       <i class="ri-fullscreen-line"></i> Full screen
     </button>

   It acts on the element its `aria-controls` names, else on the whole page.
   `aria-pressed` says whether that element is in full screen now, kept right when the
   reader leaves with Escape or the browser's own control as well as with the button.

   An element in full screen is shown alone on a black backdrop, so make it a surface —
   a `.card`, or anything with a background of its own. The browser asks for a press:
   full screen cannot be entered from a timer or on load, only from the reader's click.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    function targetOf(button) {
        var id = button.getAttribute('aria-controls');
        return id ? document.getElementById(id) : document.documentElement;
    }

    function sync() {
        var buttons = document.querySelectorAll('[data-fullscreen]');
        for (var i = 0; i < buttons.length; i++) {
            var target = targetOf(buttons[i]);
            buttons[i].setAttribute('aria-pressed', String(!!target && document.fullscreenElement === target));
        }
    }

    function toggle(element) {
        var target = element || document.documentElement;
        try {
            // Kept right from the promise as well as from the event, whichever comes first.
            if (document.fullscreenElement === target) {
                return document.exitFullscreen().then(function () { sync(); return false; }, function () { sync(); return true; });
            }
            if (!target.requestFullscreen) return Promise.resolve(false);
            return target.requestFullscreen().then(function () { sync(); return true; }, function () { sync(); return false; });
        } catch (e) {
            return Promise.resolve(document.fullscreenElement === target);
        }
    }

    ui.fullscreen = {
        /* Puts an element — the page when none is given — into full screen, or takes it
           out; resolves whether it is in full screen afterwards. Needs the reader's press. */
        toggle: toggle,

        /* Whether an element — the page when none is given — is in full screen now. */
        isOn: function (element) {
            return document.fullscreenElement === (element || document.documentElement);
        }
    };

    document.addEventListener('click', function (e) {
        var button = e.target && e.target.closest ? e.target.closest('[data-fullscreen]') : null;
        if (!button) return;
        var target = targetOf(button);
        if (!target) return;
        e.preventDefault();
        toggle(target);
    });

    document.addEventListener('fullscreenchange', sync);

})(window.sednaUi);

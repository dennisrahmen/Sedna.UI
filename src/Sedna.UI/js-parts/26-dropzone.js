/* ── Dropzone, delegated ─────────────────────────────────────────────────────
   Opt-in wiring for .dropzone, because there is no CSS pseudo-class for "something
   is being dragged over me" and it is the same fifteen lines in every app:

     <label class="dropzone" data-dropzone>
       <i class="ri-upload-cloud-2-line"></i>
       <span>Drop files here, or click to choose</span>
       <input type="file" multiple hidden />
     </label>

   Two things here are easy to get wrong, so they are done once.

   First, `dragleave` fires when the pointer moves onto a CHILD of the zone, so an
   "add on enter, remove on leave" pair flickers and then sticks in the wrong state as
   soon as the zone has an icon and a label inside it. The fix is a depth counter, held
   on the element so two zones on a page cannot confuse each other.

   Second, `dragover` MUST have its default prevented or the browser refuses the
   drop and then navigates to the dropped file — losing the page, which is a
   spectacular failure for a form.

   On drop the files are put into the zone's own <input type="file"> and a bubbling
   `change` event is dispatched, so the app's existing handler — including Blazor's
   InputFile — sees a dropped file exactly as it sees a chosen one, and there is
   nothing extra to bind. Nothing here calls into .NET.

   Delegated from document, so a zone rendered by a later render works unwired.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var DEPTH = '_drDropDepth';
    var OVER = 'dropzone--over';

    function zoneOf(target) {
        return target instanceof Element ? target.closest('.dropzone[data-dropzone]') : null;
    }

    function setOver(zone, on) {
        zone.classList.toggle(OVER, on);
        if (!on) zone[DEPTH] = 0;
    }

    ui.dropzone = {
        // Clears the highlight on every zone. An app calls this if it tears a zone
        // down mid-drag, when no dragleave or drop will ever arrive.
        reset: function () {
            var zones = document.querySelectorAll('.dropzone.' + OVER);
            for (var i = 0; i < zones.length; i++) setOver(zones[i], false);
        }
    };

    document.addEventListener('dragenter', function (e) {
        var zone = zoneOf(e.target);
        if (!zone) return;
        e.preventDefault();
        zone[DEPTH] = (zone[DEPTH] || 0) + 1;
        zone.classList.add(OVER);
    });

    document.addEventListener('dragover', function (e) {
        var zone = zoneOf(e.target);
        if (!zone) return;
        // Without this the drop is refused and the browser opens the file, replacing
        // the page.
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
    });

    document.addEventListener('dragleave', function (e) {
        var zone = zoneOf(e.target);
        if (!zone) return;
        zone[DEPTH] = (zone[DEPTH] || 1) - 1;
        if (zone[DEPTH] <= 0) setOver(zone, false);
    });

    document.addEventListener('drop', function (e) {
        var zone = zoneOf(e.target);
        if (!zone) return;
        e.preventDefault();
        setOver(zone, false);

        var input = zone.querySelector('input[type="file"]');
        if (!input || !e.dataTransfer || !e.dataTransfer.files.length) return;

        try {
            // Assigning a FileList is only possible through DataTransfer, and only
            // this way round: input.files = e.dataTransfer.files works in Chromium
            // and is not universally settable, so the list is rebuilt.
            var transfer = new DataTransfer();
            var files = e.dataTransfer.files;
            var many = input.multiple ? files.length : Math.min(1, files.length);
            for (var i = 0; i < many; i++) transfer.items.add(files[i]);
            input.files = transfer.files;
        } catch (err) {
            return;    // no DataTransfer constructor: the drop simply does nothing
        }

        // Bubbling, so a delegated handler and Blazor's InputFile both see it. The
        // app's change handler is the one place that knows what a file means here.
        input.dispatchEvent(new Event('change', { bubbles: true }));
    });

})(window.sednaUi);

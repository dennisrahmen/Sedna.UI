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

   ── The whole page as the target ──
   `data-dropzone="page"` marks the app's own overlay, written once in the page:

     <div class="dropzone dropzone--page" data-dropzone="page" data-dropzone-input="attach" aria-hidden="true">
       <i class="ri-upload-cloud-2-line"></i>
       <span>Drop to attach to the order</span>
     </div>

   While files are dragged over the window the overlay carries `.dropzone--over`, which
   is what shows it; leaving the window, a drop, the end of the drag or Escape takes it
   away. Only a drag carrying files counts, so a row dragged inside the page never shows
   it. A drop anywhere is delivered as a zone delivers one — to the input whose id
   `data-dropzone-input` names, else the overlay's own `input[type=file]` — except a
   drop on an ordinary zone, which keeps its own. The overlay takes no pointer, so the
   zones under it keep their highlight. The first one in the document is the target.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var DEPTH = '_drDropDepth';
    var OVER = 'dropzone--over';

    function zoneOf(target) {
        return target instanceof Element ? target.closest('.dropzone[data-dropzone]:not([data-dropzone="page"])') : null;
    }

    // The page's own target, and whether a drag carries files at all.
    function pageZone() { return document.querySelector('[data-dropzone="page"]'); }

    function carriesFiles(e) {
        var types = e.dataTransfer && e.dataTransfer.types;
        if (!types) return false;
        for (var i = 0; i < types.length; i++) if (types[i] === 'Files') return true;
        return false;
    }

    var pageDepth = 0;

    function setPage(on) {
        var page = pageZone();
        if (!on) pageDepth = 0;
        if (page) page.classList.toggle(OVER, on);
    }

    // Files into an input, and the `change` the app already listens for.
    function deliver(input, list) {
        if (!input || !list || !list.length) return;
        try {
            // Assigning a FileList is only possible through DataTransfer, and only
            // this way round: input.files = e.dataTransfer.files works in Chromium
            // and is not universally settable, so the list is rebuilt.
            var transfer = new DataTransfer();
            var many = input.multiple ? list.length : Math.min(1, list.length);
            for (var i = 0; i < many; i++) transfer.items.add(list[i]);
            input.files = transfer.files;
        } catch (err) {
            return;    // no DataTransfer constructor: the drop simply does nothing
        }

        // Bubbling, so a delegated handler and Blazor's InputFile both see it. The
        // app's change handler is the one place that knows what a file means here.
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function pageInput(page) {
        var id = page.getAttribute('data-dropzone-input');
        var named = id ? document.getElementById(id) : null;
        return named && named.matches('input[type="file"]') ? named : page.querySelector('input[type="file"]');
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
            pageDepth = 0;
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
        deliver(zone.querySelector('input[type="file"]'), e.dataTransfer && e.dataTransfer.files);
    });

    /* ── The page ─────────────────────────────────────────────────────────────
       The same depth count, held for the document: entering a child fires before
       leaving its parent, so the count stays above zero while the pointer is anywhere
       in the window and reaches zero when it leaves or the drag is called off. */
    document.addEventListener('dragenter', function (e) {
        if (!pageZone() || !carriesFiles(e)) return;
        e.preventDefault();
        pageDepth++;
        setPage(true);
    });

    document.addEventListener('dragover', function (e) {
        if (!pageZone() || !carriesFiles(e) || zoneOf(e.target)) return;
        // Without this the drop is refused and the browser opens the file.
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'copy';
        setPage(true);
    });

    document.addEventListener('dragleave', function (e) {
        if (!pageZone() || !carriesFiles(e)) return;
        pageDepth--;
        if (pageDepth <= 0) setPage(false);
    });

    document.addEventListener('drop', function (e) {
        var page = pageZone();
        if (!page || !carriesFiles(e)) return;
        setPage(false);
        if (zoneOf(e.target)) return;    // an ordinary zone took it
        e.preventDefault();
        deliver(pageInput(page), e.dataTransfer && e.dataTransfer.files);
    });

    document.addEventListener('dragend', function () { if (pageDepth) setPage(false); });

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var page = pageZone();
        if (page && page.classList.contains(OVER)) setPage(false);
    });

})(window.sednaUi);

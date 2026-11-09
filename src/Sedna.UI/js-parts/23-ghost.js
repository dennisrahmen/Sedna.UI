/* ── Ghost text ──────────────────────────────────────────────────────────────
   An inline suggestion the reader accepts with Tab. The app writes the suggestion
   on the textarea and an empty mirror beside it; this part shows the part of the
   suggestion not yet typed, after the caret, in the mirror:

     <div class="ghost-field">
       <textarea class="form-input" data-suggestion="Restart the sync worker on src-db-14"></textarea>
       <div class="ghost-text" aria-hidden="true"></div>
     </div>

   The suggestion shows while the textarea has the focus, the caret is at the end
   with nothing selected, and what is typed is the start of the suggestion, ignoring
   case. Tab writes the rest into the value, puts the caret after it and dispatches
   a bubbling `input`, so `@bind:event="oninput"` picks it up. Escape dismisses it,
   and so does typing something it does not start with; a dismissed suggestion stays
   away until the app writes a different one. The app updates `data-suggestion` as
   the reader types — removing it, or writing an empty one, shows nothing.

   No word here is the script's: the mirror shows the reader's text and the app's
   suggestion, written as its `data-ghost-typed` and `data-ghost-rest` attributes,
   which the stylesheet draws with `content: attr()` — the typed part invisible, to
   push the rest to where the caret is.

   ── What is measured ──
   The mirror has to wrap where the textarea wraps. The part copies the textarea's
   computed font, spacing, padding and border widths onto it and lays it over the
   textarea's box, adding the width of a scrollbar on the side it is on, and keeps
   its scroll position with the textarea's. That is all it measures.

   ── Limits ──
   A textarea only. Left to right and right to left both work; text mixing the two
   directions at the join is drawn by the mirror's own bidi run, which can differ
   from the textarea's. Accepting replaces the value, so the browser's undo does not
   take it back.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var COPIED = [
        'boxSizing', 'fontFamily', 'fontSize', 'fontWeight', 'fontStyle', 'fontVariant', 'fontStretch',
        'fontFeatureSettings', 'lineHeight', 'letterSpacing', 'wordSpacing', 'textIndent', 'textTransform',
        'tabSize', 'direction', 'textAlign', 'paddingTop', 'paddingRight', 'paddingBottom', 'paddingLeft',
        'borderTopWidth', 'borderRightWidth', 'borderBottomWidth', 'borderLeftWidth'
    ];

    var dismissed = typeof WeakMap === 'function' ? new WeakMap() : null;

    function field(el) {
        return el && el.nodeType === 1 && el.matches('textarea[data-suggestion]') ? el : null;
    }

    function mirrorOf(area) {
        var host = area.closest('.ghost-field');
        return host ? host.querySelector('.ghost-text') : null;
    }

    function clear(mirror) {
        if (!mirror) return;
        mirror.removeAttribute('data-ghost-typed');
        mirror.removeAttribute('data-ghost-rest');
    }

    // The rest of the suggestion to show after what is typed, or '' for none.
    function rest(area) {
        var suggestion = area.getAttribute('data-suggestion') || '';
        var value = area.value;
        if (!suggestion || suggestion.length <= value.length) return '';
        if (dismissed && dismissed.get(area) === suggestion) return '';
        if (suggestion.slice(0, value.length).toLowerCase() !== value.toLowerCase()) return '';
        return suggestion.slice(value.length);
    }

    function lay(area, mirror) {
        var cs = getComputedStyle(area);
        for (var i = 0; i < COPIED.length; i++) mirror.style[COPIED[i]] = cs[COPIED[i]];
        mirror.style.borderStyle = 'solid';
        // A scrollbar narrows the text; the mirror has none, so it gets the room back
        // as padding on the scrollbar's side.
        var bar = area.offsetWidth - area.clientWidth
            - (parseFloat(cs.borderLeftWidth) || 0) - (parseFloat(cs.borderRightWidth) || 0);
        if (bar > 0) {
            var side = cs.direction === 'rtl' ? 'paddingLeft' : 'paddingRight';
            mirror.style[side] = ((parseFloat(cs[side]) || 0) + bar) + 'px';
        }
        mirror.style.left = area.offsetLeft + 'px';
        mirror.style.top = area.offsetTop + 'px';
        mirror.style.width = area.offsetWidth + 'px';
        mirror.style.height = area.offsetHeight + 'px';
        mirror.scrollTop = area.scrollTop;
    }

    function refresh(area) {
        var mirror = mirrorOf(area);
        if (!mirror) return;
        var shown = '';
        if (document.activeElement === area && area.selectionStart === area.value.length
            && area.selectionEnd === area.value.length) {
            shown = rest(area);
        }
        if (!shown) { clear(mirror); return; }
        mirror.setAttribute('data-ghost-typed', area.value);
        mirror.setAttribute('data-ghost-rest', shown);
        lay(area, mirror);
    }

    function accept(area) {
        var mirror = mirrorOf(area);
        if (!mirror || !mirror.hasAttribute('data-ghost-rest')) return false;
        var value = area.value + mirror.getAttribute('data-ghost-rest');
        clear(mirror);
        area.value = value;
        area.setSelectionRange(value.length, value.length);
        area.dispatchEvent(new Event('input', { bubbles: true }));
        return true;
    }

    function dismiss(area) {
        if (dismissed) dismissed.set(area, area.getAttribute('data-suggestion') || '');
        clear(mirrorOf(area));
    }

    document.addEventListener('input', function (e) {
        var area = field(e.target);
        if (!area) return;
        if (e.isComposing) { clear(mirrorOf(area)); return; }
        // Typing that leaves the suggestion behind dismisses it.
        var suggestion = area.getAttribute('data-suggestion') || '';
        var value = area.value;
        if (suggestion && value && suggestion.slice(0, value.length).toLowerCase() !== value.toLowerCase()) {
            dismiss(area);
            return;
        }
        refresh(area);
    });

    document.addEventListener('keydown', function (e) {
        var area = field(e.target);
        if (!area || e.defaultPrevented || e.isComposing) return;
        var mirror = mirrorOf(area);
        if (!mirror || !mirror.hasAttribute('data-ghost-rest')) return;

        if (e.key === 'Tab' && !e.shiftKey && !e.altKey && !e.ctrlKey && !e.metaKey) {
            e.preventDefault();
            accept(area);
        } else if (e.key === 'Escape') {
            e.preventDefault();
            e.stopPropagation();
            dismiss(area);
        }
    }, true);

    // The caret leaving the end, or the field losing the focus, takes the suggestion
    // away; coming back brings it back.
    function follow() {
        var area = field(document.activeElement);
        if (area) refresh(area);
    }
    document.addEventListener('selectionchange', follow);
    document.addEventListener('keyup', follow);
    document.addEventListener('pointerup', follow);
    document.addEventListener('focusin', function (e) { var area = field(e.target); if (area) refresh(area); });
    document.addEventListener('focusout', function (e) { var area = field(e.target); if (area) clear(mirrorOf(area)); });

    // `scroll` does not bubble.
    document.addEventListener('scroll', function (e) {
        var area = field(e.target);
        var mirror = area ? mirrorOf(area) : null;
        if (mirror) mirror.scrollTop = area.scrollTop;
    }, true);
    window.addEventListener('resize', follow);

    // The app writes a new suggestion as the reader types, usually after the input
    // event that asked for it.
    if (typeof MutationObserver === 'function') {
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var area = field(records[i].target);
                if (area && area === document.activeElement) refresh(area);
                else if (area) clear(mirrorOf(area));
            }
        }).observe(document.documentElement, { attributes: true, attributeFilter: ['data-suggestion'], subtree: true });
    }

    ui.ghost = {
        /* Shows, moves or takes down the suggestion of a textarea, after the app changed
           something this part does not hear — its size, its font. */
        refresh: function (area) { area = field(area); if (area) refresh(area); },
        /* Writes the shown suggestion into the value, as Tab does. Returns whether there was one. */
        accept: function (area) { area = field(area); return area ? accept(area) : false; },
        /* Dismisses the current suggestion, as Escape does. */
        dismiss: function (area) { area = field(area); if (area) dismiss(area); }
    };

})(window.sednaUi);

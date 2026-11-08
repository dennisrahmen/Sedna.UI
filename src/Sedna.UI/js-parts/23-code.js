/* ── One-time code ──────────────────────────────────────────────────────────
   A `[data-code-group]` is a row of boxes, one character each, and one input marked
   `data-code-value` that holds the whole code — the field a form posts and Blazor binds:

     <div class="form-code" data-code-group role="group" aria-labelledby="code-label">
       <input type="hidden" name="code" data-code-value>
       <input class="form-input" inputmode="numeric" autocomplete="one-time-code" aria-label="Digit 1 of 6">
       …
     </div>

   Every other input in the group is a box. Typing a character fills the box and moves
   to the next; Backspace in an empty box clears the one before and moves there; the
   arrow keys, Home and End move between boxes. A code pasted into any box, or filled
   in by the platform from `autocomplete="one-time-code"`, is spread one character to a
   box — from the first box when it is long enough to fill them all, else from the box
   it landed in. A box with `inputmode="numeric"` keeps digits only; any other keeps
   every character but white space.

   After every change the joined code is written into the value input, which then
   dispatches `input` and `change` as a field does, so a form, a Blazor binding and an
   app's own handler each see it. A value the app writes into that input — a Blazor
   render clearing the code after a failed attempt — is spread back over the boxes:
   at once for a hidden input, whose `value` is its attribute, and otherwise the next
   time the reader comes back to the group. A form's reset empties the boxes and the
   code with them.

   This part adds no member to `sednaUi`.
   ─────────────────────────────────────────────────────────────────────────── */
(function () {

    var GROUP = '[data-code-group]';

    function groupOf(el) {
        return el instanceof Element ? el.closest(GROUP) : null;
    }

    function valueOf(group) {
        return group.querySelector('input[data-code-value]');
    }

    function boxesOf(group) {
        var all = group.querySelectorAll('input:not([data-code-value])');
        var out = [];
        for (var i = 0; i < all.length; i++) {
            if (all[i].closest(GROUP) === group && all[i].type !== 'hidden') out.push(all[i]);
        }
        return out;
    }

    function isBox(el) {
        return el instanceof HTMLInputElement && !el.hasAttribute('data-code-value')
            && el.type !== 'hidden' && !!groupOf(el) && !el.disabled && !el.readOnly;
    }

    function clean(box, text) {
        text = String(text || '');
        return box.getAttribute('inputmode') === 'numeric' ? text.replace(/\D+/g, '') : text.replace(/\s+/g, '');
    }

    function joined(boxes) {
        var code = '';
        for (var i = 0; i < boxes.length; i++) code += boxes[i].value;
        return code;
    }

    function commit(group) {
        var value = valueOf(group);
        if (!value) return;
        var code = joined(boxesOf(group));
        if (value.value === code) return;
        value.value = code;
        value.dispatchEvent(new Event('input', { bubbles: true }));
        value.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // One character to a box from `from`; returns the index after the last box written.
    function spread(boxes, from, text) {
        var i = from;
        for (var c = 0; c < text.length && i < boxes.length; c++, i++) boxes[i].value = text.charAt(c);
        return i;
    }

    function go(boxes, i) {
        var box = boxes[Math.max(0, Math.min(i, boxes.length - 1))];
        if (box && box !== document.activeElement) box.focus();
    }

    // The app's value, spread back over the boxes where they disagree.
    function fill(group) {
        var value = valueOf(group);
        if (!value) return;
        var boxes = boxesOf(group);
        var code = value.value || '';
        if (joined(boxes) === code) return;
        for (var i = 0; i < boxes.length; i++) boxes[i].value = code.charAt(i);
    }

    function fillAll() {
        var groups = document.querySelectorAll(GROUP);
        for (var i = 0; i < groups.length; i++) fill(groups[i]);
    }

    function paste(box, text) {
        var group = groupOf(box);
        var boxes = boxesOf(group);
        var from = text.length >= boxes.length ? 0 : boxes.indexOf(box);
        var next = spread(boxes, from, text);
        commit(group);
        go(boxes, next);
    }

    document.addEventListener('input', function (e) {
        var box = e.target;
        if (!isBox(box)) return;
        var group = groupOf(box);
        var boxes = boxesOf(group);
        var at = boxes.indexOf(box);

        if (e.inputType === 'insertText' && typeof e.data === 'string') {
            var typed = clean(box, e.data);
            if (typed.length === 1) {
                box.value = typed;
                commit(group);
                if (at < boxes.length - 1) go(boxes, at + 1);
                return;
            }
            if (typed.length === 0) {
                // A refused character: the box keeps what it held, which is the one
                // character left once the refused one is cleaned out.
                box.value = clean(box, box.value).charAt(0);
                return;
            }
        }

        var text = clean(box, box.value);
        if (text.length <= 1) {
            box.value = text;
            commit(group);
            if (text.length === 1 && at < boxes.length - 1) go(boxes, at + 1);
            return;
        }
        box.value = '';
        paste(box, text);
    });

    document.addEventListener('paste', function (e) {
        var box = e.target;
        if (!isBox(box)) return;
        var data = '';
        try { data = e.clipboardData ? e.clipboardData.getData('text') : ''; } catch (err) { data = ''; }
        e.preventDefault();
        var text = clean(box, data);
        if (text) paste(box, text);
    });

    document.addEventListener('keydown', function (e) {
        var box = e.target;
        if (!isBox(box) || e.ctrlKey || e.metaKey || e.altKey) return;
        var group = groupOf(box);
        var boxes = boxesOf(group);
        var at = boxes.indexOf(box);
        var rtl = getComputedStyle(group).direction === 'rtl';

        if (e.key === 'Backspace' && box.value === '' && at > 0) {
            e.preventDefault();
            boxes[at - 1].value = '';
            commit(group);
            go(boxes, at - 1);
        } else if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
            e.preventDefault();
            go(boxes, at + ((e.key === 'ArrowRight') !== rtl ? 1 : -1));
        } else if (e.key === 'Home' || e.key === 'End') {
            e.preventDefault();
            go(boxes, e.key === 'Home' ? 0 : boxes.length - 1);
        }
    });

    document.addEventListener('focusin', function (e) {
        if (!isBox(e.target)) return;
        fill(groupOf(e.target));
    });

    document.addEventListener('reset', function (e) {
        var form = e.target;
        if (!(form instanceof HTMLFormElement)) return;
        // The form resets after this event; the boxes are read once it has.
        setTimeout(function () {
            var groups = form.querySelectorAll(GROUP);
            for (var i = 0; i < groups.length; i++) commit(groups[i]);
        }, 0);
    }, true);

    var queued = false;
    function soon() {
        if (queued) return;
        queued = true;
        (window.queueMicrotask || setTimeout)(function () { queued = false; fillAll(); });
    }

    function init() {
        fillAll();
        if (typeof MutationObserver === 'function') {
            new MutationObserver(soon).observe(document.documentElement,
                { childList: true, subtree: true, attributes: true, attributeFilter: ['value'] });
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

})(window.sednaUi);

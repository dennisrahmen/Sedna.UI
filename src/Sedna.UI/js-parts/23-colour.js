/* ── Colour picker: the area, the hue and the alpha slider write the app's field ──
   `.form-color-picker` (45-colour-picker.css) is markup the app writes; this part
   keeps its pieces and the app's value field on one colour:

     [data-color-picker]       the picker
     [data-color-area]         the saturation and brightness area, a `role="slider"`
     [data-color-hue]          a native range, 0 to 360
     [data-color-alpha]        a native range, 0 to 100 — leave it out for an opaque colour
     [data-color-value]        the app's field: a text input, or an `input type="color"`,
                               which holds no opacity. More than one may be given.
     [data-color-channel]      optional number inputs, "r", "g" or "b" (0 to 255), "a" (0 to 100)

   The colour starts as the first `[data-color-value]` that parses. A text field takes
   `#rgb`, `#rgba`, `#rrggbb`, `#rrggbbaa` and `rgb()` / `rgba()`, and is written as
   lower-case hex — eight digits below full opacity — or, with `data-color-format="rgb"`,
   as `rgb(r g b)` / `rgb(r g b / a)`.

   ── The events are the field's ──
   As the reader drags over the area or presses a key on it, the field's value is written
   and an `input` is dispatched on it; a drag's end and each key press dispatch `change`.
   A slider's own `input` and `change` are passed on to the field the same way, so
   `@bind` and `@bind:event="oninput"` on the field both work and nothing else needs one.
   Typing in a field moves the area and the sliders as soon as what is typed parses, and
   on `change` the field is rewritten in its format — or back to the last colour, if it
   does not parse — before a binding reads it.

   ── The keyboard ──
   On the area the arrows move the thumb by one per cent, ten with Shift: left and right
   are saturation, up and down brightness, physical in either direction as the picture is.
   Home and End go to no and full saturation, Page Up and Page Down to full and no
   brightness. The sliders are native ranges and keep their own keys.

   The area's `aria-valuenow` is the saturation; its `aria-valuetext` is the app's
   `data-color-valuetext`, with `{h}`, `{s}`, `{v}`, `{a}` and `{hex}` filled in, or the
   hex value when the app gives none — no word here is the script's.

   It follows every `input`, `reset` and a render that adds a picker. A value a binding
   writes from code without either is picked up at the next of those; dispatch an `input`
   event on the field after setting one.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var PICKER = '[data-color-picker]';
    var states = typeof WeakMap === 'function' ? new WeakMap() : null;
    var writing = false;
    var drag = null;

    function clamp(n, lo, hi) { return n < lo ? lo : n > hi ? hi : n; }

    function hsvToRgb(h, s, v) {
        var f = function (n) {
            var k = (n + h / 60) % 6;
            return Math.round((v - v * s * Math.max(0, Math.min(k, 4 - k, 1))) * 255);
        };
        return [f(5), f(3), f(1)];
    }

    function rgbToHsv(r, g, b) {
        r /= 255; g /= 255; b /= 255;
        var max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min, h = 0;
        if (d) {
            if (max === r) h = ((g - b) / d) % 6;
            else if (max === g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h *= 60;
            if (h < 0) h += 360;
        }
        return { h: h, s: max ? d / max : 0, v: max };
    }

    function hex2(n) { var s = Math.round(n).toString(16); return s.length < 2 ? '0' + s : s; }

    function channel(text, max) {
        var t = String(text).trim(), pct = /%$/.test(t);
        var n = parseFloat(t);
        if (isNaN(n)) return NaN;
        return clamp(pct ? n / 100 * max : n, 0, max);
    }

    // { r, g, b, a } from what a reader may type, or null.
    function parse(text) {
        var t = String(text || '').trim().toLowerCase(), m;
        if ((m = /^#?([0-9a-f]{3,4}|[0-9a-f]{6}|[0-9a-f]{8})$/.exec(t))) {
            var x = m[1];
            if (x.length < 5) x = x.replace(/./g, '$&$&');
            return {
                r: parseInt(x.slice(0, 2), 16), g: parseInt(x.slice(2, 4), 16), b: parseInt(x.slice(4, 6), 16),
                a: x.length === 8 ? parseInt(x.slice(6, 8), 16) / 255 : 1
            };
        }
        if ((m = /^rgba?\(\s*([^)]*)\)$/.exec(t))) {
            var parts = m[1].split(/\s*[,/]\s*|\s+/);
            if (parts.length < 3 || parts.length > 4) return null;
            var r = channel(parts[0], 255), g = channel(parts[1], 255), b = channel(parts[2], 255);
            var a = parts.length > 3 ? channel(parts[3], 1) : 1;
            if (isNaN(r) || isNaN(g) || isNaN(b) || isNaN(a)) return null;
            return { r: Math.round(r), g: Math.round(g), b: Math.round(b), a: a };
        }
        return null;
    }

    function rgbOf(st) { return hsvToRgb(st.h, st.s, st.v); }

    function alphaText(a) { return String(Math.round(a * 100) / 100); }

    function hexOf(rgb, a, opaque) {
        var s = '#' + hex2(rgb[0]) + hex2(rgb[1]) + hex2(rgb[2]);
        return opaque || a >= 1 ? s : s + hex2(a * 255);
    }

    function format(input, st) {
        var rgb = rgbOf(st);
        if (input.type === 'color') return hexOf(rgb, 1, true);
        if (input.getAttribute('data-color-format') === 'rgb') {
            var body = rgb[0] + ' ' + rgb[1] + ' ' + rgb[2];
            return st.a >= 1 ? 'rgb(' + body + ')' : 'rgb(' + body + ' / ' + alphaText(st.a) + ')';
        }
        return hexOf(rgb, st.a);
    }

    function own(picker, sel) {
        var out = [], all = picker.querySelectorAll(sel);
        for (var i = 0; i < all.length; i++) if (all[i].closest(PICKER) === picker) out.push(all[i]);
        return out;
    }

    function stateOf(picker) {
        var st = states ? states.get(picker) : picker.__sednaColour;
        if (st) return st;
        st = { h: 0, s: 1, v: 1, a: 1 };
        var fields = own(picker, '[data-color-value]');
        for (var i = 0; i < fields.length; i++) {
            var c = parse(fields[i].value);
            if (c) { set(st, c); break; }
        }
        if (states) states.set(picker, st); else picker.__sednaColour = st;
        return st;
    }

    // Takes a parsed colour into the state, keeping the hue where the colour has none —
    // a grey or black — so dragging back out of a corner does not jump to red.
    function set(st, c) {
        var hsv = rgbToHsv(c.r, c.g, c.b);
        if (hsv.s > 0 && hsv.v > 0) st.h = hsv.h;
        st.s = hsv.s;
        st.v = hsv.v;
        st.a = clamp(c.a, 0, 1);
    }

    function fill(template, st, hex) {
        return template
            .replace(/\{h\}/g, String(Math.round(st.h)))
            .replace(/\{s\}/g, String(Math.round(st.s * 100)))
            .replace(/\{v\}/g, String(Math.round(st.v * 100)))
            .replace(/\{a\}/g, String(Math.round(st.a * 100)))
            .replace(/\{hex\}/g, hex);
    }

    // Writes the state everywhere but `skip` (the field the reader is typing in), and
    // returns the value fields and channels whose value changed.
    function render(picker, skip) {
        var st = stateOf(picker), rgb = rgbOf(st), changed = [], i;
        var opaque = 'rgb(' + rgb[0] + ' ' + rgb[1] + ' ' + rgb[2] + ')';
        var style = picker.style;
        style.setProperty('--picker-hue', String(Math.round(st.h * 10) / 10));
        style.setProperty('--picker-x', (Math.round(st.s * 1000) / 10) + '%');
        style.setProperty('--picker-y', (Math.round((1 - st.v) * 1000) / 10) + '%');
        style.setProperty('--picker-opaque', opaque);
        style.setProperty('--picker-color', 'rgb(' + rgb[0] + ' ' + rgb[1] + ' ' + rgb[2] + ' / ' + alphaText(st.a) + ')');

        var hex = hexOf(rgb, st.a);
        var areas = own(picker, '[data-color-area]');
        for (i = 0; i < areas.length; i++) {
            var area = areas[i], tpl = area.getAttribute('data-color-valuetext');
            if (!area.hasAttribute('aria-valuemin')) area.setAttribute('aria-valuemin', '0');
            if (!area.hasAttribute('aria-valuemax')) area.setAttribute('aria-valuemax', '100');
            area.setAttribute('aria-valuenow', String(Math.round(st.s * 100)));
            area.setAttribute('aria-valuetext', tpl ? fill(tpl, st, hex) : hex);
        }

        var hues = own(picker, '[data-color-hue]');
        for (i = 0; i < hues.length; i++) if (hues[i] !== skip) hues[i].value = String(Math.round(st.h));
        var alphas = own(picker, '[data-color-alpha]');
        for (i = 0; i < alphas.length; i++) if (alphas[i] !== skip) alphas[i].value = String(Math.round(st.a * 100));

        var channels = own(picker, '[data-color-channel]');
        for (i = 0; i < channels.length; i++) {
            var ch = channels[i], which = ch.getAttribute('data-color-channel');
            if (ch === skip) continue;
            var v = which === 'a' ? Math.round(st.a * 100) : rgb['rgb'.indexOf(which)];
            if (v === undefined) continue;
            if (ch.value !== String(v)) { ch.value = String(v); changed.push(ch); }
        }

        var fields = own(picker, '[data-color-value]');
        for (i = 0; i < fields.length; i++) {
            if (fields[i] === skip) continue;
            var text = format(fields[i], st);
            if (fields[i].value !== text) { fields[i].value = text; changed.push(fields[i]); }
        }
        return changed;
    }

    function dispatch(list, type) {
        writing = true;
        try {
            for (var i = 0; i < list.length; i++) list[i].dispatchEvent(new Event(type, { bubbles: true }));
        } finally { writing = false; }
    }

    function valueFields(picker) {
        return own(picker, '[data-color-value]').concat(own(picker, '[data-color-channel]'));
    }

    function disabled(el) {
        return el.getAttribute('aria-disabled') === 'true' || el.getAttribute('aria-readonly') === 'true';
    }

    // ── The area ──

    function moveTo(area, x, y) {
        var picker = area.closest(PICKER);
        if (!picker) return [];
        var r = area.getBoundingClientRect();
        if (!r.width || !r.height) return [];
        var st = stateOf(picker);
        st.s = clamp((x - r.left) / r.width, 0, 1);
        st.v = clamp(1 - (y - r.top) / r.height, 0, 1);
        return render(picker);
    }

    function onPointerDown(e) {
        if (e.button !== 0) return;
        var area = e.target.closest && e.target.closest('[data-color-area]');
        if (!area || disabled(area)) return;
        var picker = area.closest(PICKER);
        if (!picker) return;
        e.preventDefault();
        try { area.focus({ preventScroll: true }); } catch (err) { area.focus(); }
        try { area.setPointerCapture(e.pointerId); } catch (err) { /* capture is a nicety */ }
        drag = { area: area, picker: picker, id: e.pointerId, moved: false };
        var changed = moveTo(area, e.clientX, e.clientY);
        if (changed.length) { drag.moved = true; dispatch(changed, 'input'); }
    }

    function onPointerMove(e) {
        if (!drag || e.pointerId !== drag.id) return;
        var changed = moveTo(drag.area, e.clientX, e.clientY);
        if (changed.length) { drag.moved = true; dispatch(changed, 'input'); }
    }

    function onPointerUp(e) {
        if (!drag || e.pointerId !== drag.id) return;
        var d = drag;
        drag = null;
        if (d.moved) dispatch(valueFields(d.picker), 'change');
    }

    function onKeyDown(e) {
        var area = e.target.closest && e.target.closest('[data-color-area]');
        if (!area || disabled(area) || e.altKey || e.ctrlKey || e.metaKey) return;
        var picker = area.closest(PICKER);
        if (!picker) return;
        var st = stateOf(picker), step = e.shiftKey ? 0.1 : 0.01, s = st.s, v = st.v;
        switch (e.key) {
            case 'ArrowLeft': s -= step; break;
            case 'ArrowRight': s += step; break;
            case 'ArrowUp': v += step; break;
            case 'ArrowDown': v -= step; break;
            case 'Home': s = 0; break;
            case 'End': s = 1; break;
            case 'PageUp': v = 1; break;
            case 'PageDown': v = 0; break;
            default: return;
        }
        e.preventDefault();
        // Rounded to the step, so ten presses of one per cent land on a whole ten.
        st.s = clamp(Math.round(s * 100) / 100, 0, 1);
        st.v = clamp(Math.round(v * 100) / 100, 0, 1);
        var changed = render(picker);
        if (changed.length) { dispatch(changed, 'input'); dispatch(changed, 'change'); }
    }

    // ── The sliders and the fields ──

    function onInput(e) {
        if (writing) return;
        var t = e.target;
        if (!(t instanceof HTMLInputElement)) return;
        var picker = t.closest(PICKER);
        if (!picker) return;
        var st = stateOf(picker), changed;

        if (t.hasAttribute('data-color-hue')) {
            st.h = clamp(parseFloat(t.value) || 0, 0, 360);
        } else if (t.hasAttribute('data-color-alpha')) {
            st.a = clamp((parseFloat(t.value) || 0) / 100, 0, 1);
        } else if (t.hasAttribute('data-color-channel')) {
            var rgb = rgbOf(st), which = t.getAttribute('data-color-channel'), n = parseFloat(t.value);
            if (isNaN(n)) return;
            if (which === 'a') set(st, { r: rgb[0], g: rgb[1], b: rgb[2], a: clamp(n, 0, 100) / 100 });
            else {
                var at = 'rgb'.indexOf(which);
                if (at < 0) return;
                rgb[at] = Math.round(clamp(n, 0, 255));
                set(st, { r: rgb[0], g: rgb[1], b: rgb[2], a: st.a });
            }
        } else if (t.hasAttribute('data-color-value')) {
            var c = parse(t.value);
            if (!c) return;
            if (t.type === 'color') c.a = st.a;
            set(st, c);
        } else return;

        changed = render(picker, t);
        if (changed.length) dispatch(changed, 'input');
    }

    function onChange(e) {
        if (writing) return;
        var t = e.target;
        if (!(t instanceof HTMLInputElement)) return;
        var picker = t.closest(PICKER);
        if (!picker) return;
        if (t.hasAttribute('data-color-value')) {
            // Before a binding hears this change: the field in its own format, or the last
            // colour back if what was typed does not parse.
            var text = format(t, stateOf(picker));
            if (t.value !== text) t.value = text;
        } else if (!t.hasAttribute('data-color-hue') && !t.hasAttribute('data-color-alpha') && !t.hasAttribute('data-color-channel')) {
            return;
        }
        var others = [], all = valueFields(picker);
        for (var i = 0; i < all.length; i++) if (all[i] !== t) others.push(all[i]);
        dispatch(others, 'change');
    }

    function syncAll() {
        var pickers = document.querySelectorAll(PICKER);
        for (var i = 0; i < pickers.length; i++) {
            var known = states ? states.has(pickers[i]) : !!pickers[i].__sednaColour;
            if (!known || !pickers[i].style.getPropertyValue('--picker-hue')) render(pickers[i]);
        }
    }

    function resync(form) {
        var pickers = form.querySelectorAll(PICKER);
        for (var i = 0; i < pickers.length; i++) {
            if (states) states.delete(pickers[i]); else pickers[i].__sednaColour = null;
            render(pickers[i]);
        }
    }

    var queued = false;
    function soon() {
        if (queued) return;
        queued = true;
        (window.queueMicrotask || setTimeout)(function () { queued = false; syncAll(); });
    }

    function init() {
        syncAll();
        // Capturing on the document runs before a listener where a framework binds, so a
        // binding reads a field already rewritten.
        document.addEventListener('input', onInput, true);
        document.addEventListener('change', onChange, true);
        document.addEventListener('pointerdown', onPointerDown);
        document.addEventListener('pointermove', onPointerMove);
        document.addEventListener('pointerup', onPointerUp);
        document.addEventListener('pointercancel', onPointerUp);
        document.addEventListener('keydown', onKeyDown);
        // A reset restores the fields after the event, so the colour is read again then.
        document.addEventListener('reset', function (e) {
            var form = e.target;
            setTimeout(function () { if (form && form.querySelectorAll) resync(form); }, 0);
        }, true);
        if (typeof MutationObserver === 'function') {
            new MutationObserver(soon).observe(document.documentElement, { childList: true, subtree: true });
        }
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

})(window.sednaUi);

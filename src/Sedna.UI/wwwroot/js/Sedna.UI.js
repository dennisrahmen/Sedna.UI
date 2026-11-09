/*! Sedna.UI — generated, comments stripped. The commented copy ships in the package as readable/Sedna.UI.js. */
window.sednaUi = window.sednaUi || {};
(function (ui) {
    var config = {
        storagePrefix: 'sedna.',
        notifyIcon: null,
        langCookie: false,
        themeDefault: 'sedna',
        toastDismissLabel: 'Dismiss',
        toastPile: false,
        toastLimit: 0
    };
    function key(k) { return config.storagePrefix + k; }
    function readRaw(k) {
        try { return localStorage.getItem(k); } catch (e) { return null; }
    }
    function score(needle, haystack, penalty) {
        if (!needle) return 1;
        if (!haystack) return -1;
        var n = needle.toLowerCase();
        var h = haystack.toLowerCase();
        var idx = h.indexOf(n);
        if (idx === 0) return 1000 - penalty;                       // prefix
        if (idx > 0) {
            var wordStart = /[\s\-_/.]/.test(h[idx - 1]);
            return (wordStart ? 800 : 600) - idx - penalty;
        }
        var first = -1, last = -1, hi = 0;
        for (var ni = 0; ni < n.length; ni++) {
            var found = h.indexOf(n[ni], hi);
            if (found < 0) return -1;
            if (first < 0) first = found;
            last = found;
            hi = found + 1;
        }
        var span = last - first + 1;
        return 400 - (span - n.length) - first - penalty;
    }
    function fill(template, slots) {
        if (!template) return null;
        var node = (template.content && template.content.firstElementChild) || template.firstElementChild;
        if (!node) return null;
        node = node.cloneNode(true);
        Object.keys(slots || {}).forEach(function (name) {
            var value = slots[name];
            var targets = node.matches('[data-' + name + ']') ? [node] : [];
            var inner = node.querySelectorAll('[data-' + name + ']');
            for (var i = 0; i < inner.length; i++) targets.push(inner[i]);
            targets.forEach(function (t) {
                if (value === undefined || value === null || value === '') {
                    if (t !== node) t.remove();
                } else {
                    t.textContent = String(value);
                }
            });
        });
        return node;
    }
    var HOLD = 300;
    function safeHref(url) {
        if (url == null) return null;
        var s = String(url).trim();
        if (!s) return null;
        return /^(https?:|mailto:|tel:|[^:\/?#]*(?:[\/?#]|$))/i.test(s) ? s : null;
    }
    var TYPEAHEAD_PAUSE = 500;
    function typeahead(state, key, items, current, labelOf) {
        if (!key || key.length !== 1 || !items.length) return null;
        var now = Date.now();
        if (!state.text || now - state.at > TYPEAHEAD_PAUSE) state.text = '';
        state.at = now;
        state.text += key.toLowerCase();
        var text = state.text;
        var repeated = text.split('').every(function (c) { return c === text[0]; });
        var search = repeated ? text[0] : text;
        var at = items.indexOf(current);
        var from = repeated ? at + 1 : Math.max(at, 0);
        for (var i = 0; i < items.length; i++) {
            var item = items[(from + i) % items.length];
            var label = String(labelOf(item) || '').trim().toLowerCase();
            if (label.indexOf(search) === 0) return item;
        }
        return null;
    }
    ui._ = { config: config, key: key, readRaw: readRaw, score: score, fill: fill, hold: HOLD, safeHref: safeHref, typeahead: typeahead };
    ui.configure = function (opts) {
        if (!opts) return;
        Object.keys(opts).forEach(function (k) {
            if (k in config) config[k] = opts[k];
        });
        if (ui.settings) ui.settings.apply();
    };
})(window.sednaUi);
(function (ui) {
    var opened = [];     // modal dialogs, in the order they opened — the top layer's order
    var listeners = [];
    function isModal(el) {
        try { return !!el && el.isConnected && el.matches('dialog:modal'); } catch (e) { return false; }
    }
    function topModal(except) {
        for (var i = opened.length - 1; i >= 0; i--) {
            if (opened[i] !== except && isModal(opened[i])) return opened[i];
        }
        var all;
        try { all = document.querySelectorAll('dialog:modal'); } catch (e) { return null; }
        for (var j = all.length - 1; j >= 0; j--) if (all[j] !== except) return all[j];
        return null;
    }
    function raise(node, except) {
        if (!node) return;
        var parent = topModal(except) || document.body;
        if (!parent) return;
        if (node.parentNode !== parent) {
            var focused = node.contains(document.activeElement) ? document.activeElement : null;
            parent.appendChild(node);
            if (focused && focused !== document.activeElement) {
                try { focused.focus({ preventScroll: true }); } catch (e) { /* refused */ }
            }
        }
        if (!node.hasAttribute('popover')) node.setAttribute('popover', 'manual');
        try {
            if (node.matches(':popover-open')) node.hidePopover();
            node.showPopover();
        } catch (e) { /* no popover support: its z-index is the fallback */ }
    }
    function track(e) {
        var dialog = e.target;
        if (!(dialog instanceof HTMLDialogElement)) return;
        var closing = e.type === 'close' || e.newState === 'closed';
        var at = opened.indexOf(dialog);
        if (at >= 0) opened.splice(at, 1);
        if (!closing && isModal(dialog)) opened.push(dialog);
        for (var i = 0; i < listeners.length; i++) {
            try { listeners[i](closing ? dialog : null); } catch (err) { /* one listener must not stop the rest */ }
        }
    }
    document.addEventListener('beforetoggle', track, true);
    document.addEventListener('toggle', track, true);
    document.addEventListener('close', track, true);
    var watched = [];
    var observer = null;
    function check() {
        var gone = watched.filter(function (w) { return !w.el.isConnected; });
        if (!gone.length) return;
        watched = watched.filter(function (w) { return w.el.isConnected; });
        if (!watched.length && observer) { observer.disconnect(); observer = null; }
        gone.forEach(function (w) { try { w.fn(); } catch (e) { /* the next one still runs */ } });
    }
    function whenGone(el, fn) {
        if (!el || typeof MutationObserver !== 'function') return function () { };
        var entry = { el: el, fn: fn };
        watched.push(entry);
        if (!observer) {
            observer = new MutationObserver(check);
            observer.observe(document.documentElement, { childList: true, subtree: true });
        }
        return function () {
            watched = watched.filter(function (w) { return w !== entry; });
            if (!watched.length && observer) { observer.disconnect(); observer = null; }
        };
    }
    ui._.layer = {
        topModal: topModal,
        raise: raise,
        onChange: function (fn) { listeners.push(fn); },
        whenGone: whenGone
    };
})(window.sednaUi);
(function (ui) {
    var core = ui._;
    var config = core.config, key = core.key, readRaw = core.readRaw;
    function systemPrefersLight() {
        return !!(window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches);
    }
    function appNames() {
        var tag = document.querySelector('script[data-app-attrs]');
        var list = ((tag && tag.getAttribute('data-app-attrs')) || '').split(/\s+/);
        return list.filter(function (name) { return /^[a-z][a-z0-9-]*$/.test(name); });
    }
    var listeners = [];
    function notify() {
        var current = ui.settings.load();
        listeners.slice().forEach(function (fn) {
            try { fn(current); } catch (e) { /* a bad listener is not the theme's problem */ }
        });
    }
    ui.settings = {
        load: function () {
            var g = function (k) { return readRaw(key(k)); };
            var v = g('variant');
            return {
                lang:    g('lang') || document.documentElement.lang
                             || (navigator.language || 'en').slice(0, 2).toLowerCase(),
                theme:   g('theme') || config.themeDefault,
                variant: (v === 'light' || v === 'dark' || v === 'system') ? v : 'dark',
                cvd:     g('cvd') === '1',
                density: (g('density') === 'compact' || g('density') === 'touch') ? g('density') : 'comfortable',
                dir:     g('dir') === 'rtl' ? 'rtl' : (g('dir') === 'ltr' ? 'ltr'
                             : (document.documentElement.dir || 'ltr'))
            };
        },
        save: function (k, value) {
            try { localStorage.setItem(key(k), value); } catch (e) { /* ignore */ }
            if (k === 'lang') {
                if (config.langCookie) {
                    try {
                        document.cookie = key('lang') + '=' + value + ';path=/;max-age=31536000;SameSite=Lax';
                    } catch (e) { /* ignore */ }
                }
                document.documentElement.lang = value;
            }
            this.apply();
        },
        apply: function () {
            var g = function (k) { return readRaw(key(k)); };
            var root = document.documentElement;
            root.setAttribute('data-theme', g('theme') || config.themeDefault);
            var v = g('variant');
            var variant;
            if (v === 'light' || v === 'dark') variant = v;
            else if (v === 'system') variant = systemPrefersLight() ? 'light' : 'dark';
            else variant = 'dark';
            root.setAttribute('data-variant', variant);
            if (g('cvd') === '1') root.setAttribute('data-cvd', '1');
            else root.removeAttribute('data-cvd');
            var density = g('density');
            if (density === 'compact' || density === 'touch') root.setAttribute('data-density', density);
            else root.removeAttribute('data-density');
            var dir = g('dir');
            if (dir === 'rtl' || dir === 'ltr') root.dir = dir;
            appNames().forEach(function (name) {
                var value = g(name);
                if (value === null) root.removeAttribute('data-app-' + name);
                else root.setAttribute('data-app-' + name, value);
            });
            notify();
        },
        onChange: function (fn) {
            if (typeof fn !== 'function') return function () { };
            listeners.push(fn);
            return function () {
                var at = listeners.indexOf(fn);
                if (at >= 0) listeners.splice(at, 1);
            };
        }
    };
    try {
        if (window.matchMedia) {
            window.matchMedia('(prefers-color-scheme: light)').addEventListener('change', function () {
                if (readRaw(key('variant')) === 'system') ui.settings.apply();
            });
        }
    } catch (e) { /* ignore */ }
})(window.sednaUi);
(function (ui) {
    var supported = (function () {
        try { return window.CSS && CSS.supports('appearance', 'base-select'); }
        catch (e) { return false; }
    })();
    var EMPTY_CLONE = 'select:has(> button > selectedcontent:empty)';
    function repair(root) {
        if (!supported) return 0;
        var found;
        try { found = (root || document).querySelectorAll(EMPTY_CLONE); }
        catch (e) { return 0; }
        var n = 0;
        for (var i = 0; i < found.length; i++) {
            var sel = found[i];
            if (sel.multiple || sel.selectedIndex < 0) continue;
            sel.selectedIndex = sel.selectedIndex;
            n++;
        }
        return n;
    }
    ui.select = {
        refresh: function (root) { return repair(root); }
    };
    if (!supported) return;
    var queued = false;
    function schedule() {
        if (queued) return;
        queued = true;
        setTimeout(function () { queued = false; repair(); }, 0);
    }
    function onMutations(records) {
        for (var i = 0; i < records.length; i++) {
            if (records[i].addedNodes.length) { schedule(); return; }
        }
    }
    schedule();
    document.addEventListener('DOMContentLoaded', schedule);
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) schedule();
    });
    try {
        new MutationObserver(onMutations)
            .observe(document.documentElement, { childList: true, subtree: true });
    } catch (e) {
    }
})(window.sednaUi);
(function (ui) {
    ui.tips = (function () {
        var tipEl = null, showTimer = null, current = null;
        var SHOW_DELAY = 130;   // ms — long enough not to flash on a passing cursor
        var unwatch = null;
        function ensureEl() {
            if (!tipEl) {
                tipEl = document.createElement('div');
                tipEl.className = 'sedna-tip';
                tipEl.setAttribute('role', 'tooltip');
                tipEl.setAttribute('popover', 'manual');
            }
            ui._.layer.raise(tipEl);
            return tipEl;
        }
        function trigger(t) {
            if (!t || !t.closest) return null;
            var el = t.closest('[data-tip]');
            if (!el || el.closest('.sidebar')) return null;
            if (typeof api.gate === 'function' && !api.gate(el)) return null;
            if (api.enabled === false) return null;
            return el;
        }
        function clipped(el) {
            var nodes = [el].concat(Array.prototype.slice.call(el.querySelectorAll('*')));
            for (var i = 0; i < nodes.length; i++) {
                var n = nodes[i];
                if (n.scrollWidth > n.clientWidth + 1 && getComputedStyle(n).overflowX !== 'visible') return true;
            }
            return false;
        }
        function place(el) {
            var tip = el.getAttribute('data-tip');
            if (!tip) return hide();
            var vis = (el.innerText || '').trim();
            if (vis && vis.indexOf(tip) !== -1 && !clipped(el)) return hide();
            put(tip, el.getBoundingClientRect(), el.getAttribute('data-tip-pos'));
            if (unwatch) unwatch();
            unwatch = ui._.layer.whenGone(el, function () { unwatch = null; if (current === el || held === el) { held = null; hide(); } });
        }
        function put(text, r, preferred) {
            var box = ensureEl();
            box.textContent = text;
            box.style.left = '0px';
            box.style.top = '0px';
            box.classList.add('sedna-tip--visible');
            var b = box.getBoundingClientRect();
            var pos = preferred || 'top';
            var gap = 8, m = 6, vw = window.innerWidth, vh = window.innerHeight;
            if (pos === 'top' && r.top < b.height + gap + m) pos = 'bottom';
            else if (pos === 'bottom' && r.bottom + b.height + gap + m > vh) pos = 'top';
            var x, y;
            if (pos === 'left')        { x = r.left - b.width - gap; y = r.top + r.height / 2 - b.height / 2; }
            else if (pos === 'right')  { x = r.right + gap;          y = r.top + r.height / 2 - b.height / 2; }
            else if (pos === 'bottom') { x = r.left + r.width / 2 - b.width / 2; y = r.bottom + gap; }
            else                       { x = r.left + r.width / 2 - b.width / 2; y = r.top - b.height - gap; }
            x = Math.max(m, Math.min(x, vw - b.width - m));
            y = Math.max(m, Math.min(y, vh - b.height - m));
            box.style.left = Math.round(x) + 'px';
            box.style.top = Math.round(y) + 'px';
        }
        function show(el) {
            if (current === el) return;   // already showing / queued for this one
            current = el;
            clearTimeout(showTimer);
            showTimer = setTimeout(function () { if (current === el) place(el); }, SHOW_DELAY);
        }
        function hide() {
            current = null;
            clearTimeout(showTimer);
            if (unwatch) { unwatch(); unwatch = null; }
            if (tipEl) tipEl.classList.remove('sedna-tip--visible');
        }
        document.addEventListener('keydown', function (e) {
            if (e.key !== 'Escape' || !tipEl || !tipEl.classList.contains('sedna-tip--visible')) return;
            hide();
            var t = e.target instanceof Element ? e.target : null;
            if (t && t.closest('dialog[open], :popover-open')) {
                e.preventDefault();
                e.stopPropagation();
            }
        }, true);
        document.addEventListener('mouseover', function (e) {
            var el = trigger(e.target);
            if (el) show(el);
        });
        document.addEventListener('mouseout', function (e) {
            var el = trigger(e.target);
            if (!el || el !== current) return;
            if (e.relatedTarget && el.contains(e.relatedTarget)) return;
            hide();
        });
        document.addEventListener('focusin', function (e) {
            var el = trigger(e.target);
            if (el) { current = el; place(el); }   // no delay for keyboard focus
        });
        document.addEventListener('focusout', hide);
        document.addEventListener('mousedown', function () {
            if (!held) hide();   // a click dismisses its own hint; a long press is showing it
        });
        var HOLD = ui._.hold, LINGER = 1500;
        var holdTimer = null, held = null, lingerTimer = null, swallow = false;
        function release() {
            clearTimeout(holdTimer);
            holdTimer = null;
            if (!held) return;
            var el = held;
            held = null;
            clearTimeout(lingerTimer);
            lingerTimer = setTimeout(function () { if (current === el) hide(); }, LINGER);
        }
        document.addEventListener('pointerdown', function (e) {
            if (e.pointerType !== 'touch') return;
            var el = trigger(e.target);
            if (!el) return;
            clearTimeout(holdTimer);
            holdTimer = setTimeout(function () {
                held = el;
                swallow = true;
                current = el;
                place(el);
            }, HOLD);
        }, true);
        document.addEventListener('pointerup', release, true);
        document.addEventListener('pointercancel', release, true);
        document.addEventListener('pointermove', function (e) {
            if (e.pointerType === 'touch' && holdTimer && !held) { clearTimeout(holdTimer); holdTimer = null; }
        }, true);
        document.addEventListener('click', function (e) {
            if (!swallow) return;
            swallow = false;
            e.preventDefault();
            e.stopPropagation();
        }, true);
        document.addEventListener('contextmenu', function (e) {
            if (held || holdTimer) e.preventDefault();
        });
        window.addEventListener('scroll', hide, true);   // capture: any scroll container
        window.addEventListener('resize', hide);
        var api = {
            gate: null,
            enabled: true,
            hide: hide,
            at: function (rect, text, pos) {
                if (!text || !rect || api.enabled === false) return hide();
                current = null;
                clearTimeout(showTimer);
                put(String(text), rect, pos);
            },
            setEnabled: function (on) {
                api.enabled = on !== false;
                if (!api.enabled) hide();
            }
        };
        return api;
    })();
})(window.sednaUi);
(function (ui) {
    var RESTORE_MS = 1400;
    function textFor(btn) {
        if (btn.hasAttribute('data-copy')) return btn.getAttribute('data-copy') || '';
        var sel = btn.getAttribute('data-copy-target');
        var node = sel
            ? document.querySelector(sel)
            : (btn.closest('.code-block') || document).querySelector('pre');
        return node ? (node.innerText || node.textContent || '') : '';
    }
    function flash(btn, ok) {
        clearTimeout(+btn.dataset.copyTimer || 0);
        btn.setAttribute('data-copied', ok ? 'ok' : 'failed');
        btn.dataset.copyTimer = setTimeout(function () {
            btn.removeAttribute('data-copied');
            delete btn.dataset.copyTimer;
        }, RESTORE_MS);
    }
    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-copy], [data-copy-target]');
        if (!btn) return;
        var text = textFor(btn);
        if (!text) return;
        e.preventDefault();
        ui.copyText(text).then(function (ok) { flash(btn, ok); });
    });
})(window.sednaUi);
(function (ui) {
    var SELECTOR = '.choice-dependent--dim';
    var MARK = 'data-sedna-inert';
    function controlOf(block) {
        var host = block.previousElementSibling;
        if (!host || !host.matches('.form-check, .switch')) return null;
        for (var i = 0; i < host.children.length; i++) {
            var c = host.children[i];
            if (c.tagName === 'INPUT' && (c.type === 'checkbox' || c.type === 'radio')) return c;
        }
        return null;
    }
    function sync(block) {
        var input = controlOf(block);
        if (!input) return;
        var off = !input.checked;
        if (off && !block.hasAttribute('inert')) {
            block.setAttribute('inert', '');
            block.setAttribute(MARK, '');
        } else if (!off && block.hasAttribute(MARK)) {
            block.removeAttribute('inert');
            block.removeAttribute(MARK);
        }
    }
    function syncAll(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var blocks = scope.querySelectorAll(SELECTOR);
        for (var i = 0; i < blocks.length; i++) sync(blocks[i]);
    }
    var queued = false;
    function soon() {
        if (queued) return;
        queued = true;
        (window.queueMicrotask || setTimeout)(function () { queued = false; syncAll(document); });
    }
    function init() {
        syncAll(document);
        document.addEventListener('change', soon, true);
        document.addEventListener('reset', function () { setTimeout(soon, 0); }, true);
        if (typeof MutationObserver === 'function') {
            new MutationObserver(soon).observe(document.documentElement, { childList: true, subtree: true });
        }
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})(window.sednaUi);
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
        document.addEventListener('input', onInput, true);
        document.addEventListener('reset', function () { setTimeout(soon, 0); }, true);
        if (typeof MutationObserver === 'function') {
            new MutationObserver(soon).observe(document.documentElement, { childList: true, subtree: true });
        }
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})(window.sednaUi);
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
(function () {
    function fieldOf(button) {
        var id = button.getAttribute('aria-controls');
        if (id) {
            var named = document.getElementById(id.split(/\s+/)[0]);
            if (named instanceof HTMLInputElement) return named;
        }
        var group = button.parentElement;
        if (!group) return null;
        return group.querySelector('input[type="password"], input[data-sedna-revealed]');
    }
    function show(field, button, on) {
        if (on) {
            field.type = 'text';
            field.setAttribute('data-sedna-revealed', '');
        } else {
            field.type = 'password';
            field.removeAttribute('data-sedna-revealed');
        }
        if (button) button.setAttribute('aria-pressed', on ? 'true' : 'false');
    }
    document.addEventListener('click', function (e) {
        var t = e.target;
        var button = t instanceof Element ? t.closest('[data-reveal]') : null;
        if (!button || button.disabled || button.getAttribute('aria-disabled') === 'true') return;
        var id = button.getAttribute('aria-controls');
        var target = id ? document.getElementById(id.split(/\s+/)[0]) : null;
        if (target && !(target instanceof HTMLInputElement)) {
            var on = !target.hasAttribute('data-sedna-revealed');
            if (on) target.setAttribute('data-sedna-revealed', '');
            else target.removeAttribute('data-sedna-revealed');
            button.setAttribute('aria-pressed', on ? 'true' : 'false');
            return;
        }
        var field = fieldOf(button);
        if (!field) return;
        show(field, button, field.type === 'password');
    });
})();
(function (ui) {
    function selectedIn(list) {
        return Array.prototype.filter.call(
            list.options, function (o) { return o.selected && !o.disabled; });
    }
    function sorted(list) {
        for (var i = 1; i < list.options.length; i++) {
            if (list.options[i - 1].text.localeCompare(list.options[i].text) > 0) return false;
        }
        return true;
    }
    function place(list, option) {
        var at = null;
        for (var i = 0; i < list.options.length; i++) {
            if (list.options[i].text.localeCompare(option.text) > 0) { at = list.options[i]; break; }
        }
        list.insertBefore(option, at);
    }
    function move(from, to, options) {
        if (!from || !to || !options.length) return 0;
        var inOrder = sorted(to);
        for (var i = 0; i < options.length; i++) {
            options[i].selected = false;
            if (inOrder) place(to, options[i]); else to.appendChild(options[i]);
        }
        to.selectedIndex = -1;
        if (options.length && options[0].scrollIntoView) {
            options[0].scrollIntoView({ block: 'nearest' });
        }
        notify(from);
        notify(to);
        return options.length;
    }
    function notify(list) {
        list.dispatchEvent(new Event('change', { bubbles: true, composed: true }));
    }
    function lists(host) {
        return {
            from: host.querySelector('[data-transfer-from]'),
            to: host.querySelector('[data-transfer-to]')
        };
    }
    ui.transfer = {
        add: function (host) {
            var el = host && host.closest ? host.closest('[data-transfer]') : null;
            if (!el) return 0;
            var pair = lists(el);
            return move(pair.from, pair.to, pair.from ? selectedIn(pair.from) : []);
        },
        remove: function (host) {
            var el = host && host.closest ? host.closest('[data-transfer]') : null;
            if (!el) return 0;
            var pair = lists(el);
            return move(pair.to, pair.from, pair.to ? selectedIn(pair.to) : []);
        }
    };
    document.addEventListener('click', function (e) {
        var btn = e.target.closest('[data-transfer] [data-transfer-add], [data-transfer] [data-transfer-remove]');
        if (!btn || btn.disabled) return;
        e.preventDefault();
        if (btn.hasAttribute('data-transfer-add')) ui.transfer.add(btn);
        else ui.transfer.remove(btn);
    });
    document.addEventListener('dblclick', function (e) {
        var option = e.target.closest('[data-transfer] option');
        if (!option || option.disabled) return;
        var list = option.closest('select');
        var host = option.closest('[data-transfer]');
        if (!list || !host) return;
        var pair = lists(host);
        var to = list === pair.from ? pair.to : list === pair.to ? pair.from : null;
        if (!to) return;
        e.preventDefault();
        move(list, to, [option]);
    });
})(window.sednaUi);
(function (ui) {
    function scrolledElement(target) {
        if (target === document || target === window) return document.documentElement;
        return target && target.nodeType === 1 ? target : null;
    }
    function stale(scroller, panel, trigger) {
        if (!scroller || !trigger) return false;
        if (panel === scroller || panel.contains(scroller)) return false;
        return scroller !== trigger && scroller.contains(trigger);
    }
    function closeMenus(scroller) {
        var toggle = document.querySelector('[data-menu-toggle][aria-expanded="true"]');
        if (!toggle) return;
        var anchor = toggle.closest('.menu-anchor');
        var panel = anchor ? anchor.querySelector('.menu') : null;
        if (panel && stale(scroller, panel, toggle)) ui.menu.closeAll();
    }
    function closePopovers(scroller) {
        var open;
        try { open = document.querySelectorAll('.popover:popover-open'); }
        catch (err) { return; }
        for (var i = 0; i < open.length; i++) {
            var panel = open[i];
            var trigger = ui.popover ? ui.popover.triggerOf(panel)
                : (panel.id ? document.querySelector('[popovertarget="' + panel.id + '"]') : null);
            if (stale(scroller, panel, trigger)) {
                try { panel.hidePopover(); } catch (err2) { /* already closing */ }
            }
        }
    }
    function closePointMenus(scroller) {
        var open;
        try { open = document.querySelectorAll('.menu[popover]:popover-open'); }
        catch (err) { return; }
        for (var i = 0; i < open.length; i++) {
            var panel = open[i];
            var origin = ui._.menuOrigin ? ui._.menuOrigin(panel) : undefined;
            var close = origin === null
                ? !(panel === scroller || panel.contains(scroller))
                : stale(scroller, panel, origin === undefined
                    ? (panel.id ? document.querySelector('[popovertarget="' + panel.id + '"]') : null)
                    : origin);
            if (close) {
                try { panel.hidePopover(); } catch (err2) { /* already closing */ }
            }
        }
    }
    function closeCombos(scroller) {
        var open = document.querySelectorAll('[data-combo] .form-combo-input[aria-expanded="true"]');
        for (var i = 0; i < open.length; i++) {
            var host = open[i].closest('[data-combo]');
            if (host.classList.contains('form-combo--inline')) continue;
            var panel = host.querySelector('.form-combo-panel');
            var box = host.querySelector('.form-combo-box');
            if (panel && stale(scroller, panel, box)) ui.combo.close(host);
        }
    }
    document.addEventListener('scroll', function (e) {
        var scroller = scrolledElement(e.target);
        if (!scroller) return;
        closeMenus(scroller);
        closePointMenus(scroller);
        closePopovers(scroller);
        closeCombos(scroller);
    }, true);
    document.addEventListener('toggle', function (e) {
        var panel = e.target;
        if (!panel || e.newState !== 'open' || !panel.matches || !panel.matches('.popover')) return;
        var trigger = ui.popover ? ui.popover.triggerOf(panel)
            : (panel.id ? document.querySelector('[popovertarget="' + panel.id + '"]') : null);
        if (!trigger) return;
        ui._.layer.whenGone(trigger, function () {
            try { if (panel.matches(':popover-open')) panel.hidePopover(); } catch (err) { /* already closed */ }
        });
    }, true);
})(window.sednaUi);
(function (ui) {
    var state = new WeakMap();      // grid → { cell, row, col, want, anchor, picked, rowEdit }
    var tamed = new WeakMap();      // control → the tabindex attribute it had, or null
    var editing = new WeakMap();    // cell → { value, ending }
    var holder = null;              // the grid the reader is in, kept while a render takes its focus away
    var CONTROLS = 'a[href], button, input, select, textarea, summary, [tabindex], [contenteditable=""], [contenteditable="true"]';
    var core = {
        selector: 'table[role="grid"], table[role="treegrid"]',
        keys: [],
        gridOf: gridOf,
        rows: rowsOf,
        cells: cellsOf,
        colOf: colOf,
        cellAt: cellAt,
        activate: activate,
        refresh: refresh
    };
    ui._.grid = core;
    function gridOf(el) {
        var cell = el && el.closest ? el.closest('td, th') : null;
        return cell ? cell.closest('table') : (el && el.closest ? el.closest(core.selector) : null);
    }
    function cellOf(el) {
        var cell = el && el.closest ? el.closest('td, th') : null;
        for (; cell; cell = cell.parentElement ? cell.parentElement.closest('td, th') : null) {
            var table = cell.closest('table');
            if (table && table.matches(core.selector)) return cell;
        }
        return null;
    }
    function stateOf(grid) {
        var s = state.get(grid);
        if (!s) {
            s = { cell: null, row: 0, col: 0, want: 0, anchor: null, picked: [], rowEdit: null };
            state.set(grid, s);
        }
        return s;
    }
    function rowsOf(grid) {
        return Array.prototype.filter.call(grid.rows, function (r) {
            return r.cells.length > 0 && r.getClientRects().length > 0;
        });
    }
    function cellsOf(row) { return Array.prototype.slice.call(row.cells); }
    function colOf(cell) {
        var col = 0;
        for (var c = cell.previousElementSibling; c; c = c.previousElementSibling) col += c.colSpan || 1;
        return col;
    }
    function cellAt(row, col) {
        var cells = row.cells, at = 0;
        for (var i = 0; i < cells.length; i++) {
            at += cells[i].colSpan || 1;
            if (col < at) return cells[i];
        }
        return cells[cells.length - 1] || null;
    }
    function rtl(grid) { return getComputedStyle(grid).direction === 'rtl'; }
    function editorOf(cell) { return cell.querySelector('.cell-editor'); }
    function rowEditing(cell) {
        var row = cell.parentElement;
        return !!(row && row.hasAttribute('data-editing'));
    }
    function keepsKeys(el) {
        if (el.isContentEditable) return true;
        var tag = el.tagName;
        if (tag === 'TEXTAREA' || tag === 'SELECT') return true;
        if (tag !== 'INPUT') return false;
        return !/^(button|checkbox|submit|reset|image|color|file)$/i.test(el.type);
    }
    function lostFocus() {
        var a = document.activeElement;
        return (a === null || a === document.body) && document.hasFocus();
    }
    function controlsIn(cell) {
        return Array.prototype.filter.call(cell.querySelectorAll(CONTROLS), function (el) {
            return !el.classList.contains('cell-editor') && cellOf(el) === cell;
        });
    }
    function tame(grid) {
        var all = grid.querySelectorAll(CONTROLS);
        for (var i = 0; i < all.length; i++) {
            var el = all[i];
            if (el.classList.contains('cell-editor')) continue;
            var cell = cellOf(el);
            if (!cell || gridOf(cell) !== grid || el === cell) continue;
            if (rowEditing(cell)) {
                if (tamed.has(el)) {
                    var was = tamed.get(el);
                    tamed.delete(el);
                    if (was === null) el.removeAttribute('tabindex');
                    else el.setAttribute('tabindex', was);
                }
            } else if (!tamed.has(el) && el.getAttribute('tabindex') !== '-1') {
                tamed.set(el, el.getAttribute('tabindex'));
                el.setAttribute('tabindex', '-1');
            }
        }
    }
    function activate(grid, cell, keepWant) {
        var s = stateOf(grid);
        if (s.cell && s.cell !== cell && s.cell.isConnected && gridOf(s.cell) === grid) s.cell.setAttribute('tabindex', '-1');
        if (cell.getAttribute('tabindex') !== '0') cell.setAttribute('tabindex', '0');
        var rows = rowsOf(grid);
        s.cell = cell;
        s.row = Math.max(0, rows.indexOf(cell.parentElement));
        s.col = colOf(cell);
        if (!keepWant) s.want = s.col;
    }
    function go(grid, cell, keepWant) {
        activate(grid, cell, keepWant);
        cell.focus();
        reveal(cell);
    }
    function scroller(el, axis) {
        for (var p = el.parentElement; p && p !== document.body; p = p.parentElement) {
            var o = getComputedStyle(p)[axis === 'x' ? 'overflowX' : 'overflowY'];
            if ((o === 'auto' || o === 'scroll') && (axis === 'x' ? p.scrollWidth > p.clientWidth : p.scrollHeight > p.clientHeight)) return p;
        }
        return null;
    }
    function reveal(cell) {
        var x = scroller(cell, 'x');
        if (x && getComputedStyle(cell).position !== 'sticky') {
            var box = x.getBoundingClientRect();
            var left = box.left + x.clientLeft, right = left + x.clientWidth, middle = (left + right) / 2;
            var cells = cell.parentElement.cells;
            for (var i = 0; i < cells.length; i++) {
                if (cells[i] === cell || getComputedStyle(cells[i]).position !== 'sticky') continue;
                var pin = cells[i].getBoundingClientRect();
                if (pin.left + pin.width / 2 < middle) left = Math.max(left, pin.right);
                else right = Math.min(right, pin.left);
            }
            var r = cell.getBoundingClientRect();
            if (r.left < left) x.scrollLeft -= left - r.left;
            else if (r.right > right) x.scrollLeft += Math.min(r.right - right, r.left - left);
        }
        var y = scroller(cell, 'y');
        if (y) {
            var yb = y.getBoundingClientRect();
            var top = yb.top + y.clientTop, bottom = top + y.clientHeight;
            var table = cell.closest('table'), head = table.tHead, foot = table.tFoot;
            if (head && !head.contains(cell) && getComputedStyle(head).position === 'sticky') top = Math.max(top, head.getBoundingClientRect().bottom);
            if (foot && !foot.contains(cell) && getComputedStyle(foot).position === 'sticky') bottom = Math.min(bottom, foot.getBoundingClientRect().top);
            var c = cell.getBoundingClientRect();
            if (c.top < top) y.scrollTop -= top - c.top;
            else if (c.bottom > bottom) y.scrollTop += Math.min(c.bottom - bottom, c.top - top);
        }
    }
    function refresh(grid) {
        if (!grid) {
            var all = document.querySelectorAll(core.selector);
            for (var g = 0; g < all.length; g++) refresh(all[g]);
            return;
        }
        var s = stateOf(grid);
        if (isTree(grid)) { showBranches(grid); settleLoading(); }
        var rows = rowsOf(grid);
        if (!rows.length) return;
        tame(grid);
        var lost = holder === grid && lostFocus();
        var cell = s.cell;
        if (!cell || !cell.isConnected || gridOf(cell) !== grid || rows.indexOf(cell.parentElement) < 0) {
            if (s.cell) {
                cell = cellAt(rows[Math.min(s.row, rows.length - 1)], s.want);
                activate(grid, cell, true);
            } else {
                cell = firstStop(grid, rows);
                activate(grid, cell);
            }
            if (lost) cell.focus({ preventScroll: true });
        } else if (cell.getAttribute('tabindex') !== '0') {
            cell.setAttribute('tabindex', '0');
        }
        for (var r = 0; r < rows.length; r++) {
            var cells = rows[r].cells;
            for (var i = 0; i < cells.length; i++) {
                if (cells[i] !== cell && cells[i].getAttribute('tabindex') !== '-1') cells[i].setAttribute('tabindex', '-1');
            }
        }
        var editRow = rowEditing(cell) ? cell.parentElement : null;
        if (editRow && editRow !== s.rowEdit) {
            s.rowEdit = editRow;
            if (holder === grid && (lostFocus() || editRow.contains(document.activeElement))) {
                var first = editRow.querySelector('.cell-editor');
                if (first && first !== document.activeElement) first.focus();
            }
        } else if (!editRow) {
            var a = document.activeElement;
            if (s.rowEdit && holder === grid && (lostFocus() || (s.rowEdit.contains(a) && a.classList.contains('cell-editor'))))
                cell.focus({ preventScroll: true });
            s.rowEdit = null;
        }
    }
    function firstStop(grid, rows) {
        var marked = grid.querySelectorAll('td[tabindex="0"], th[tabindex="0"]');
        for (var i = 0; i < marked.length; i++) if (gridOf(marked[i]) === grid) return marked[i];
        for (var r = 0; r < rows.length; r++) if (rows[r].parentElement === grid.tBodies[0]) return rows[r].cells[0];
        return rows[0].cells[0];
    }
    function multi(grid) { return grid.getAttribute('aria-multiselectable') === 'true'; }
    function pick(grid, from, to) {
        var s = stateOf(grid);
        var rows = rowsOf(grid);
        var picked = [];
        if (from && to) {
            var r0 = rows.indexOf(from.parentElement), r1 = rows.indexOf(to.parentElement);
            var c0 = colOf(from), c1 = colOf(to);
            var top = Math.min(r0, r1), bottom = Math.max(r0, r1);
            var left = Math.min(c0, c1), right = Math.max(c0 + (from.colSpan || 1), c1 + (to.colSpan || 1)) - 1;
            for (var r = top; r <= bottom; r++) {
                var cells = rows[r].cells;
                for (var i = 0; i < cells.length; i++) {
                    var start = colOf(cells[i]), end = start + (cells[i].colSpan || 1) - 1;
                    if (end >= left && start <= right) picked.push(cells[i]);
                }
            }
        }
        for (var k = 0; k < s.picked.length; k++) {
            if (picked.indexOf(s.picked[k]) < 0 && s.picked[k].isConnected) s.picked[k].setAttribute('aria-selected', 'false');
        }
        for (var j = 0; j < picked.length; j++) {
            if (picked[j].getAttribute('aria-selected') !== 'true') picked[j].setAttribute('aria-selected', 'true');
        }
        s.picked = picked;
    }
    function clearPick(grid) {
        var s = stateOf(grid);
        s.anchor = null;
        if (s.picked.length) pick(grid, null, null);
    }
    function pageRows(grid, cell) {
        var y = scroller(cell, 'y');
        var height = y ? y.clientHeight : window.innerHeight;
        var row = cell.parentElement.getBoundingClientRect().height || 1;
        var head = grid.tHead && y && getComputedStyle(grid.tHead).position === 'sticky' ? grid.tHead.getBoundingClientRect().height : 0;
        var foot = grid.tFoot && y && getComputedStyle(grid.tFoot).position === 'sticky' ? grid.tFoot.getBoundingClientRect().height : 0;
        return Math.max(1, Math.floor((height - head - foot) / row) - 1);
    }
    function target(e, grid, cell) {
        var s = stateOf(grid);
        var rows = rowsOf(grid);
        var row = cell.parentElement;
        var r = rows.indexOf(row);
        if (r < 0) return null;
        var cells = row.cells;
        var i = cell.cellIndex;
        var back = rtl(grid);
        var right = back ? -1 : 1;
        var primary = e.ctrlKey || e.metaKey;
        switch (e.key) {
            case 'ArrowRight': return { cell: cells[Math.max(0, Math.min(cells.length - 1, i + right))] };
            case 'ArrowLeft': return { cell: cells[Math.max(0, Math.min(cells.length - 1, i - right))] };
            case 'ArrowDown': return { cell: cellAt(rows[Math.min(rows.length - 1, r + 1)], s.want), keepWant: true };
            case 'ArrowUp': return { cell: cellAt(rows[Math.max(0, r - 1)], s.want), keepWant: true };
            case 'Home': return primary ? { cell: rows[0].cells[0] } : { cell: cells[0] };
            case 'End':
                if (primary) { var last = rows[rows.length - 1]; return { cell: last.cells[last.cells.length - 1] }; }
                return { cell: cells[cells.length - 1] };
            case 'PageDown': return { cell: cellAt(rows[Math.min(rows.length - 1, r + pageRows(grid, cell))], s.want), keepWant: true };
            case 'PageUp': return { cell: cellAt(rows[Math.max(0, r - pageRows(grid, cell))], s.want), keepWant: true };
        }
        return null;
    }
    function move(e, grid, cell) {
        var to = target(e, grid, cell);
        if (!to || !to.cell) return false;
        e.preventDefault();
        var s = stateOf(grid);
        if (e.shiftKey && multi(grid)) {
            if (!s.anchor || !s.anchor.isConnected) s.anchor = cell;
            go(grid, to.cell, to.keepWant);
            pick(grid, s.anchor, to.cell);
        } else {
            clearPick(grid);
            go(grid, to.cell, to.keepWant);
        }
        return true;
    }
    function begin(grid, cell, caretAtEnd) {
        var editor = editorOf(cell);
        if (!editor) return false;
        activate(grid, cell);
        editing.set(cell, { value: 'value' in editor ? editor.value : null, ending: false });
        cell.setAttribute('data-editing', '');
        editor.focus();
        try {
            if (caretAtEnd && editor.setSelectionRange) editor.setSelectionRange(editor.value.length, editor.value.length);
            else if (editor.select) editor.select();
        } catch (err) { /* a number or a date field has no text selection */ }
        return true;
    }
    function end(grid, cell, commit) {
        var edit = editing.get(cell);
        var editor = editorOf(cell);
        if (edit) edit.ending = true;
        if (!commit && edit && editor && edit.value !== null && editor.value !== edit.value) {
            editor.value = edit.value;
            editor.dispatchEvent(new Event('input', { bubbles: true }));
        }
        if (grid && cell.isConnected) {
            activate(grid, cell, true);
            cell.focus();
        }
        cell.removeAttribute('data-editing');
        editing.delete(cell);
    }
    function step(grid, cell, back) {
        var rows = rowsOf(grid);
        var row = cell.parentElement;
        var r = rows.indexOf(row);
        var i = cell.cellIndex + (back ? -1 : 1);
        if (i >= 0 && i < row.cells.length) return row.cells[i];
        var next = rows[r + (back ? -1 : 1)];
        if (!next) return null;
        return back ? next.cells[next.cells.length - 1] : next.cells[0];
    }
    function inEditor(e, grid, cell) {
        if (e.key === 'Escape') {
            e.preventDefault();
            end(grid, cell, false);
            return;
        }
        if (e.key === 'Enter' && !e.isComposing && (e.target.tagName !== 'TEXTAREA' || e.ctrlKey || e.metaKey)) {
            e.preventDefault();
            end(grid, cell, true);
            return;
        }
        if (e.key === 'Tab' && !e.altKey && !e.ctrlKey && !e.metaKey) {
            var next = step(grid, cell, e.shiftKey);
            if (!next) return;
            e.preventDefault();
            end(grid, cell, true);
            if (!begin(grid, next, false)) go(grid, next);
        }
    }
    function inEditingRow(e, cell) {
        var row = cell.parentElement;
        var button = null;
        if (e.key === 'Escape') button = row.querySelector('[data-row-cancel]');
        else if (e.key === 'Enter' && !e.isComposing && e.target.classList.contains('cell-editor') && e.target.tagName !== 'TEXTAREA') button = row.querySelector('[data-row-save]');
        if (!button || button.disabled) return;
        e.preventDefault();
        button.click();
    }
    var loading = [];           // rows opened with data-tree-lazy, waiting for their children
    function isTree(grid) { return grid.getAttribute('role') === 'treegrid'; }
    function levelOf(row) {
        var n = parseInt(row.getAttribute('aria-level'), 10);
        return n > 0 ? n : 1;
    }
    function bodyRows(grid) {
        var out = [];
        for (var b = 0; b < grid.tBodies.length; b++) {
            var rows = grid.tBodies[b].rows;
            for (var i = 0; i < rows.length; i++) if (rows[i].cells.length) out.push(rows[i]);
        }
        return out;
    }
    function treeCell(row) {
        var toggle = row.querySelector('.tree-toggle');
        var cell = toggle ? toggle.closest('td, th') : null;
        return cell && cell.parentElement === row ? cell : row.cells[0] || null;
    }
    function expandable(row) { return row.hasAttribute('aria-expanded'); }
    function expanded(row) { return row.getAttribute('aria-expanded') === 'true'; }
    function valueOf(row) {
        if (row.hasAttribute('data-value')) return row.getAttribute('data-value');
        var cell = treeCell(row);
        return cell ? cell.textContent.trim() : '';
    }
    function under(grid, row) {
        var rows = bodyRows(grid), at = rows.indexOf(row), level = levelOf(row), out = [];
        for (var i = at + 1; at >= 0 && i < rows.length && levelOf(rows[i]) > level; i++) out.push(rows[i]);
        return out;
    }
    function parentRow(grid, row) {
        var rows = bodyRows(grid), level = levelOf(row);
        for (var i = rows.indexOf(row) - 1; i >= 0; i--) if (levelOf(rows[i]) < level) return rows[i];
        return null;
    }
    function showBranches(grid) {
        var open = [];      // the branches above the row: { level, open }
        var rows = bodyRows(grid);
        for (var i = 0; i < rows.length; i++) {
            var row = rows[i], level = levelOf(row);
            while (open.length && open[open.length - 1].level >= level) open.pop();
            var shown = open.every(function (o) { return o.open; });
            if (row.hidden === shown) row.hidden = !shown;
            open.push({ level: level, open: expanded(row) });
        }
    }
    function settleLoading() {
        loading = loading.filter(function (item) {
            var row = item.row;
            if (!row.isConnected) return false;
            if (row.hasAttribute('data-tree-lazy') && !under(item.grid, row).length) return true;
            row.removeAttribute('aria-busy');
            return false;
        });
    }
    function setOpen(grid, row, open) {
        if (!expandable(row) || expanded(row) === open) return false;
        if (!open && under(grid, row).some(function (r) { return r.contains(document.activeElement); })) go(grid, treeCell(row));
        row.setAttribute('aria-expanded', String(open));
        var lazy = open && row.hasAttribute('data-tree-lazy');
        if (lazy && row.getAttribute('aria-busy') !== 'true') {
            row.setAttribute('aria-busy', 'true');
            loading.push({ grid: grid, row: row });
        }
        showBranches(grid);
        row.dispatchEvent(new CustomEvent(open ? 'sedna-tree-expand' : 'sedna-tree-collapse', {
            bubbles: true, composed: true, detail: { item: valueOf(row), lazy: lazy }
        }));
        return true;
    }
    core.keys.push(function (e, grid, cell) {
        if (!isTree(grid) || e.target !== cell || e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return false;
        var row = cell.parentElement;
        if (!row.parentElement || row.parentElement.tagName !== 'TBODY' || treeCell(row) !== cell) return false;
        var forward = rtl(grid) ? 'ArrowLeft' : 'ArrowRight';
        var back = rtl(grid) ? 'ArrowRight' : 'ArrowLeft';
        if (e.key === forward) {
            if (!expandable(row) || expanded(row)) return false;
            e.preventDefault();
            setOpen(grid, row, true);
            return true;
        }
        if (e.key === back) {
            if (expanded(row)) { e.preventDefault(); setOpen(grid, row, false); return true; }
            var parent = parentRow(grid, row);
            if (!parent) return false;
            e.preventDefault();
            clearPick(grid);
            go(grid, treeCell(parent));
            return true;
        }
        if (e.key === 'Enter' && expandable(row) && !editorOf(cell) && !controlsIn(cell).length) {
            e.preventDefault();
            setOpen(grid, row, !expanded(row));
            return true;
        }
        return false;
    });
    document.addEventListener('click', function (e) {
        var toggle = e.target instanceof Element ? e.target.closest('.tree-toggle') : null;
        var row = toggle && toggle.closest('tr');
        var grid = row && gridOf(row.cells[0] || row);
        if (!grid || !isTree(grid) || !expandable(row) || treeCell(row) !== toggle.closest('td, th')) return;
        setOpen(grid, row, !expanded(row));
    });
    document.addEventListener('keydown', function (e) {
        if (e.defaultPrevented || !(e.target instanceof Element)) return;
        if (ui.drag && ui.drag.active && ui.drag.active() !== null) return;
        var cell = cellOf(e.target);
        if (!cell) return;
        var grid = gridOf(cell);
        var layer = e.target.closest('[popover], [role="menu"], [role="listbox"], dialog');
        if (layer && cell.contains(layer)) return;
        for (var k = 0; k < core.keys.length; k++) {
            if (core.keys[k](e, grid, cell)) return;
        }
        if (e.target !== cell) {
            var editor = editorOf(cell);
            if (cell.hasAttribute('data-editing') && editor && editor.contains(e.target)) { inEditor(e, grid, cell); return; }
            if (rowEditing(cell)) { inEditingRow(e, cell); return; }
            if (e.key === 'Escape') { e.preventDefault(); go(grid, cell); return; }
            if (keepsKeys(e.target)) return;
            if (e.key === 'Enter' || e.key === ' ' || e.key === 'F2') return;
            move(e, grid, cell);
            return;
        }
        if (e.altKey) return;
        if (e.key === 'Enter' || e.key === 'F2') {
            if (begin(grid, cell, e.key === 'F2')) { e.preventDefault(); return; }
            var controls = controlsIn(cell);
            if (controls.length) { e.preventDefault(); controls[0].focus(); }
            return;
        }
        if (e.key === 'Escape') {
            if (stateOf(grid).picked.length) { e.preventDefault(); clearPick(grid); }
            return;
        }
        move(e, grid, cell);
    });
    document.addEventListener('focusin', function (e) {
        var cell = cellOf(e.target);
        if (!cell) { holder = null; return; }
        var grid = gridOf(cell);
        holder = grid;
        if (stateOf(grid).cell !== cell) activate(grid, cell);
    });
    document.addEventListener('focusout', function (e) {
        var cell = cellOf(e.target);
        if (!cell || !editing.has(cell)) return;
        Promise.resolve().then(function () {
            var edit = editing.get(cell);
            if (edit && !edit.ending && !cell.contains(document.activeElement) && document.hasFocus()) end(null, cell, true);
        });
    });
    document.addEventListener('mousedown', function (e) {
        if (!(e.target instanceof Element)) return;
        var cell = cellOf(e.target);
        if (!cell) { holder = null; return; }
        var grid = gridOf(cell);
        holder = grid;
        if (e.button !== 0) return;
        var s = stateOf(grid);
        if (e.shiftKey && multi(grid) && s.cell && s.cell !== cell) {
            e.preventDefault();
            if (!s.anchor || !s.anchor.isConnected) s.anchor = s.cell;
            go(grid, cell);
            pick(grid, s.anchor, cell);
            return;
        }
        var control = e.target.closest(CONTROLS);
        if (control && control !== cell && cell.contains(control)) return;
        clearPick(grid);
        activate(grid, cell);
    });
    document.addEventListener('dblclick', function (e) {
        var cell = e.target instanceof Element ? cellOf(e.target) : null;
        if (!cell || cell.hasAttribute('data-editing') || rowEditing(cell)) return;
        var grid = gridOf(cell);
        begin(grid, cell, true);
    });
    ui.grid = {
        range: function (grid) {
            grid = typeof grid === 'string' ? document.getElementById(grid) : grid;
            var s = grid && state.get(grid);
            if (!s || !s.picked.length) return null;
            var top = Infinity, left = Infinity, bottom = -1, right = -1;
            for (var i = 0; i < s.picked.length; i++) {
                var c = s.picked[i];
                if (!c.isConnected) continue;
                var r = c.parentElement.rowIndex, col = colOf(c);
                top = Math.min(top, r); bottom = Math.max(bottom, r);
                left = Math.min(left, col); right = Math.max(right, col + (c.colSpan || 1) - 1);
            }
            return bottom < 0 ? null : { top: top, left: left, bottom: bottom, right: right };
        },
        expanded: function (grid) {
            grid = typeof grid === 'string' ? document.getElementById(grid) : grid;
            if (!grid) return [];
            return bodyRows(grid).filter(function (r) { return expandable(r) && expanded(r); }).map(valueOf);
        },
        refresh: function (grid) {
            refresh(typeof grid === 'string' ? document.getElementById(grid) : grid || null);
        }
    };
    var scheduled = false;
    function later() {
        if (scheduled) return;
        scheduled = true;
        requestAnimationFrame(function () { scheduled = false; refresh(null); });
    }
    function watch() {
        refresh(null);
        new MutationObserver(function (records) {
            if (!document.querySelector(core.selector)) return;
            for (var i = 0; i < records.length; i++) {
                var r = records[i];
                if (r.type === 'childList' || r.target.tagName === 'TR') { later(); return; }
            }
        }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-editing', 'data-tree-lazy', 'aria-expanded'] });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();
})(window.sednaUi);
(function (ui) {
    function rowsOf(head) {
        var rows = [];
        for (var el = head.nextElementSibling; el && !el.classList.contains('sticky-group-head'); el = el.nextElementSibling)
            rows.push(el);
        return rows;
    }
    function apply(toggle) {
        var head = toggle.closest('.sticky-group-head');
        if (!head) return;
        var folded = toggle.getAttribute('aria-expanded') === 'false';
        var rows = rowsOf(head);
        for (var i = 0; i < rows.length; i++) {
            if (folded) rows[i].setAttribute('data-group-hidden', '');
            else rows[i].removeAttribute('data-group-hidden');
        }
    }
    function applyAll(root) {
        var toggles = (root || document).querySelectorAll('[data-group-toggle]');
        for (var i = 0; i < toggles.length; i++) apply(toggles[i]);
    }
    ui.groups = {
        toggle: function (toggle) {
            if (!toggle) return;
            toggle.setAttribute('aria-expanded', toggle.getAttribute('aria-expanded') === 'false' ? 'true' : 'false');
            apply(toggle);
        },
        refresh: function () { applyAll(document); }
    };
    document.addEventListener('click', function (e) {
        var toggle = e.target instanceof Element ? e.target.closest('[data-group-toggle]') : null;
        if (!toggle) return;
        if (toggle.getAttribute('data-group-toggle') === 'managed') return;
        ui.groups.toggle(toggle);
    });
    var scheduled = false;
    function later() {
        if (scheduled) return;
        scheduled = true;
        requestAnimationFrame(function () { scheduled = false; applyAll(document); });
    }
    function watch() {
        new MutationObserver(function (records) {
            if (!document.querySelector('[data-group-toggle]')) return;
            for (var i = 0; i < records.length; i++) {
                var r = records[i];
                if (r.type === 'childList' || (r.attributeName === 'aria-expanded' && r.target.hasAttribute('data-group-toggle'))) {
                    later();
                    return;
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['aria-expanded'] });
        applyAll(document);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();
})(window.sednaUi);
(function (ui) {
    function panelOf(toggle) {
        var anchor = toggle.closest('.menu-anchor');
        return anchor ? anchor.querySelector('.menu') : null;
    }
    function setOpen(toggle, panel, open) {
        panel.hidden = !open;
        toggle.setAttribute('aria-expanded', String(open));
        if (open) startFilter(panel);
        if (open && ui._.layer) {
            ui._.layer.whenGone(toggle, function () { if (panel.isConnected) panel.hidden = true; });
        }
    }
    function closeAll(except) {
        var toggles = document.querySelectorAll('[data-menu-toggle][aria-expanded="true"]');
        for (var i = 0; i < toggles.length; i++) {
            if (toggles[i] === except) continue;
            var panel = panelOf(toggles[i]);
            if (panel) setOpen(toggles[i], panel, false);
        }
    }
    function outside(panel, e) {
        var r = panel.getBoundingClientRect();
        return e.clientX < r.left || e.clientX > r.right || e.clientY < r.top || e.clientY > r.bottom;
    }
    ui.menu = {
        closeAll: function () { closeAll(null); }
    };
    document.addEventListener('click', function (e) {
        var toggle = e.target.closest('[data-menu-toggle]');
        if (toggle) {
            var panel = panelOf(toggle);
            if (!panel) return;
            e.preventDefault();
            var open = toggle.getAttribute('aria-expanded') === 'true';
            closeAll(toggle);
            setOpen(toggle, panel, !open);
            return;
        }
        var item = e.target.closest('.menu-item');
        if (item && item.hasAttribute('data-menu-sub')) return;
        if (item) { closeAll(null); return; }
        var menu = e.target.closest('.menu');
        if (menu && !(e.target === menu && outside(menu, e))) return;
        closeAll(null);
    });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var open = document.querySelector('[data-menu-toggle][aria-expanded="true"]');
        if (!open) return;
        e.preventDefault();
        closeAll(null);
        try { open.focus(); } catch (err) { /* detached */ }
    });
    function items(menu) {
        return Array.prototype.filter.call(menu.querySelectorAll('.menu-item'), function (el) {
            return el.closest('.menu') === menu && !el.disabled && el.getAttribute('aria-disabled') !== 'true'
                && el.getClientRects().length > 0;
        });
    }
    function subOf(item) {
        var anchor = item.closest('.menu-sub-anchor');
        return anchor ? anchor.querySelector(':scope > .menu-sub') : null;
    }
    function setSub(item, open) {
        var anchor = item.closest('.menu-sub-anchor');
        if (!anchor) return;
        if (open) anchor.setAttribute('data-open', ''); else anchor.removeAttribute('data-open');
        item.setAttribute('aria-expanded', String(open));
    }
    document.addEventListener('keydown', function (e) {
        var item = e.target instanceof Element ? e.target.closest('.menu-item') : null;
        var menu = item ? item.closest('.menu') : null;
        if (!menu) return;
        var list = items(menu), at = list.indexOf(item), next = null, filter = filterOf(menu);
        if (e.key === 'ArrowDown') next = list[(at + 1) % list.length];
        else if (e.key === 'ArrowUp' && at === 0 && filter) next = filter;
        else if (e.key === 'ArrowUp') next = list[(at - 1 + list.length) % list.length];
        else if (e.key === 'Home') next = list[0];
        else if (e.key === 'End') next = list[list.length - 1];
        else if (e.key === (getComputedStyle(menu).direction === 'rtl' ? 'ArrowLeft' : 'ArrowRight') && item.hasAttribute('data-menu-sub')) {
            var sub = subOf(item);
            if (sub) { setSub(item, true); next = items(sub)[0]; }
        } else if (e.key === (getComputedStyle(menu).direction === 'rtl' ? 'ArrowRight' : 'ArrowLeft') && menu.classList.contains('menu-sub')) {
            var opener = menu.parentElement && menu.parentElement.querySelector(':scope > [data-menu-sub]');
            if (opener) { setSub(opener, false); next = opener; }
        } else if (e.key.length === 1 && e.key !== ' ' && !e.ctrlKey && !e.metaKey && !e.altKey) {
            if (!typed.has(menu)) typed.set(menu, {});
            next = ui._.typeahead(typed.get(menu), e.key, list, item, labelOf);
        }
        if (!next) return;
        e.preventDefault();
        next.focus();
    });
    var typed = new WeakMap();   // a menu -> the letters typed into it so far
    function labelOf(item) { return item.getAttribute('data-label') || item.textContent; }
    function filterOf(menu) {
        var f = menu.querySelector('[data-menu-filter]');
        return f && f.closest('.menu') === menu ? f : null;
    }
    function own(menu, el) { return el.closest('.menu') === menu; }
    function applyFilter(input) {
        var menu = input.closest('.menu');
        if (!menu) return;
        var query = input.value.trim(), shown = 0;
        var all = menu.querySelectorAll('.menu-item');
        for (var i = 0; i < all.length; i++) {
            if (!own(menu, all[i])) continue;
            var match = !query || ui._.score(query, String(labelOf(all[i]) || '').trim(), 0) >= 0;
            all[i].hidden = !match;
            if (match) shown++;
        }
        var kids = menu.children, group = [], live = false;
        function close() {
            for (var g = 0; g < group.length; g++) {
                group[g].hidden = !!query && (group[g].classList.contains('menu-sep') || !live);
            }
            group = []; live = false;
        }
        for (var k = 0; k < kids.length; k++) {
            var kid = kids[k];
            if (kid.classList.contains('menu-label') || kid.classList.contains('menu-sep')) { close(); group.push(kid); continue; }
            if (kid.classList.contains('menu-item')) { if (!kid.hidden) live = true; continue; }
            var inner = Array.prototype.filter.call(kid.querySelectorAll('.menu-item'), function (el) { return own(menu, el); });
            if (!inner.length) continue;
            var visible = inner.some(function (el) { return !el.hidden; });
            kid.hidden = !visible;
            if (visible) live = true;
        }
        close();
        if (query && !shown) menu.setAttribute('data-menu-empty', ''); else menu.removeAttribute('data-menu-empty');
    }
    function startFilter(menu) {
        var filter = filterOf(menu);
        if (!filter) return false;
        if (filter.value) {
            filter.value = '';
            filter.dispatchEvent(new Event('input', { bubbles: true }));
        } else {
            applyFilter(filter);
        }
        try { filter.focus(); } catch (err) { /* not focusable */ }
        return true;
    }
    document.addEventListener('input', function (e) {
        var filter = e.target instanceof Element && e.target.hasAttribute('data-menu-filter') ? e.target : null;
        if (filter) applyFilter(filter);
    });
    document.addEventListener('keydown', function (e) {
        var filter = e.target instanceof Element && e.target.hasAttribute('data-menu-filter') ? e.target : null;
        var menu = filter ? filter.closest('.menu') : null;
        if (!menu || e.isComposing) return;
        var first = items(menu)[0];
        if (!first) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); first.focus(); }
        else if (e.key === 'Enter') { e.preventDefault(); first.click(); }
    });
    document.addEventListener('focusout', function (e) {
        var anchor = e.target instanceof Element ? e.target.closest('.menu-sub-anchor') : null;
        if (!anchor || anchor.contains(e.relatedTarget)) return;
        var opener = anchor.querySelector(':scope > [data-menu-sub]');
        if (opener) setSub(opener, false);
    });
    document.addEventListener('pointerover', function (e) {
        var opener = e.target instanceof Element ? e.target.closest('[data-menu-sub]') : null;
        if (opener) opener.setAttribute('aria-expanded', 'true');
    });
    document.addEventListener('pointerout', function (e) {
        var anchor = e.target instanceof Element ? e.target.closest('.menu-sub-anchor') : null;
        if (!anchor || anchor.contains(e.relatedTarget) || anchor.hasAttribute('data-open')) return;
        var opener = anchor.querySelector(':scope > [data-menu-sub]');
        if (opener) opener.setAttribute('aria-expanded', 'false');
    });
    var point = null;
    var origins = new WeakMap();   // a menu opened at a point -> the element it is about, or null
    function pointAt(x, y) {
        if (!point) {
            point = document.createElement('span');
            point.setAttribute('aria-hidden', 'true');
            point.className = 'menu-point';
            document.body.appendChild(point);
        }
        point.style.left = Math.round(x) + 'px';
        point.style.top = Math.round(y) + 'px';
        return point;
    }
    function byId(x) { return typeof x === 'string' ? document.getElementById(x) : x; }
    function openAt(panel, x, y, on) {
        panel = byId(panel);
        on = byId(on) || null;
        if (!panel || typeof panel.showPopover !== 'function') return false;
        if (secondaryDown) {
            document.addEventListener('pointerup', function once(ev) {
                if (ev.button !== 2) return;
                document.removeEventListener('pointerup', once, true);
                setTimeout(function () { show(panel, x, y, on); }, 0);
            }, true);
            return true;
        }
        show(panel, x, y, on);
        return true;
    }
    function show(panel, x, y, on) {
        pointAt(x, y);
        origins.set(panel, on);
        var was = document.querySelector('[data-menu-context-target]');
        if (was) was.removeAttribute('data-menu-context-target');
        if (on) on.setAttribute('data-menu-context-target', '');
        try { if (panel.matches(':popover-open')) panel.hidePopover(); } catch (err) { /* closed */ }
        try { panel.showPopover(); } catch (err2) { return; }
        if (startFilter(panel)) return;
        var first = items(panel)[0];
        if (first) first.focus();
    }
    ui.menu.openAt = openAt;
    ui._.menuOrigin = function (panel) { return origins.has(panel) ? origins.get(panel) : undefined; };
    var secondaryDown = false;
    document.addEventListener('pointerdown', function (e) { if (e.button === 2) secondaryDown = true; }, true);
    document.addEventListener('pointerup', function (e) { if (e.button === 2) secondaryDown = false; }, true);
    document.addEventListener('contextmenu', function (e) {
        var on = e.target instanceof Element ? e.target.closest('[data-menu-context]') : null;
        if (!on) return;
        var panel = document.getElementById(on.getAttribute('data-menu-context'));
        if (!panel) return;
        e.preventDefault();
        openAt(panel, e.clientX, e.clientY, on);
    });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'ContextMenu' && !(e.key === 'F10' && e.shiftKey)) return;
        var on = e.target instanceof Element ? e.target.closest('[data-menu-context]') : null;
        if (!on) return;
        var panel = document.getElementById(on.getAttribute('data-menu-context'));
        if (!panel) return;
        e.preventDefault();
        var r = on.getBoundingClientRect();
        openAt(panel, r.left, r.bottom, on);
    });
    document.addEventListener('toggle', function (e) {
        var panel = e.target;
        if (e.newState !== 'closed' || !panel || !panel.matches || !panel.matches('.menu[popover]')) return;
        var on = origins.get(panel);
        origins.delete(panel);
        if (on && on.hasAttribute('data-menu-context-target')) on.removeAttribute('data-menu-context-target');
    }, true);
    document.addEventListener('click', function (e) {
        var item = e.target instanceof Element ? e.target.closest('.menu-item') : null;
        if (!item || item.hasAttribute('data-menu-sub')) return;
        var panel = item.closest('.menu[popover]');
        try { if (panel && panel.matches(':popover-open')) panel.hidePopover(); } catch (err) { /* closed */ }
    });
})(window.sednaUi);
(function (ui) {
    var triggers = new WeakMap();
    var pinned = new WeakSet();   // hover cards a click has pinned open
    var count = 0;
    function anchor(panel, trigger) {
        var name = trigger.style.getPropertyValue('anchor-name');
        if (!name) {
            name = '--sedna-popover-' + (++count);
            trigger.style.setProperty('anchor-name', name);
        }
        panel.style.setProperty('position-anchor', name);
    }
    function show(panel, trigger) {
        if (!panel || typeof panel.showPopover !== 'function') return;
        if (trigger) {
            triggers.set(panel, trigger);
            anchor(panel, trigger);
            if (panel.id) trigger.setAttribute('aria-controls', panel.id);
        }
        try { panel.showPopover(); } catch (err) { /* already open, or not a popover */ }
    }
    function hide(panel) {
        try { if (panel && panel.matches(':popover-open')) panel.hidePopover(); }
        catch (err) { /* already closed */ }
    }
    ui.popover = {
        show: show,
        hide: hide,
        toggle: function (panel, trigger) {
            var open = false;
            try { open = panel.matches(':popover-open'); } catch (err) { /* no :popover-open */ }
            if (open) hide(panel); else show(panel, trigger);
        },
        triggerOf: function (panel) {
            var t = triggers.get(panel);
            if (t) return t;
            return panel.id ? document.querySelector('[popovertarget="' + panel.id + '"]') : null;
        }
    };
    function target(trigger) {
        var id = trigger.getAttribute('data-popover');
        return id ? document.getElementById(id) : null;
    }
    var wasOpen = null;
    document.addEventListener('pointerdown', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover]') : null;
        var panel = trigger ? target(trigger) : null;
        wasOpen = null;
        try { if (panel && panel.matches(':popover-open')) wasOpen = panel; } catch (err) { /* no :popover-open */ }
    }, true);
    document.addEventListener('click', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover]') : null;
        if (!trigger) return;
        var panel = target(trigger);
        if (!panel) return;
        if (hoverOnly(trigger)) {
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            hide(panel);
            return;
        }
        e.preventDefault();
        e.stopPropagation();
        if (trigger.hasAttribute('data-popover-hover')) {
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            var open = false;
            try { open = panel.matches(':popover-open'); } catch (err) { /* no :popover-open */ }
            if (pinned.has(panel) && (open || wasOpen === panel)) {
                wasOpen = null;
                pinned.delete(panel);
                hide(panel);
                return;
            }
            wasOpen = null;
            pinned.add(panel);
            show(panel, trigger);
            return;
        }
        if (wasOpen === panel) { wasOpen = null; hide(panel); return; }
        ui.popover.toggle(panel, trigger);
    });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        var trigger = e.target instanceof Element ? e.target : null;
        if (!trigger || !trigger.hasAttribute('data-popover')) return;
        if (trigger.tagName === 'BUTTON') return;   // a button clicks on its own
        if (hoverOnly(trigger)) return;             // and so does a hover-only trigger's own action
        var panel = target(trigger);
        if (!panel) return;
        e.preventDefault();
        ui.popover.toggle(panel, trigger);
    });
    var hoverTimer = 0, leaveTimer = 0, hovered = null;
    function hoverOnly(trigger) { return trigger.getAttribute('data-popover-hover') === 'only'; }
    function delayOf(trigger) {
        var d = parseFloat(getComputedStyle(trigger).getPropertyValue('--hover-delay'));
        return isNaN(d) ? 400 : d;
    }
    function enter(trigger) {
        var panel = target(trigger);
        if (!panel) return;
        clearTimeout(leaveTimer);
        clearTimeout(hoverTimer);
        hovered = panel;
        hoverTimer = setTimeout(function () { show(panel, trigger); }, delayOf(trigger));
    }
    function leave() {
        clearTimeout(hoverTimer);
        var panel = hovered;
        if (!panel) return;
        clearTimeout(leaveTimer);
        leaveTimer = setTimeout(function () {
            var trigger = triggers.get(panel);
            var stay = panel.matches(':hover') || panel.contains(document.activeElement)
                || (trigger && (trigger.matches(':hover') || trigger === document.activeElement));
            if (!stay) { hide(panel); hovered = null; }
        }, 200);
    }
    document.addEventListener('pointerover', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el || e.pointerType === 'touch') return;
        var trigger = el.closest('[data-popover][data-popover-hover]');
        if (trigger && !trigger.contains(e.relatedTarget)) { enter(trigger); return; }
        if (hovered && hovered.contains(el)) clearTimeout(leaveTimer);
    });
    document.addEventListener('pointerout', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el || !hovered || e.pointerType === 'touch') return;
        var from = el.closest('[data-popover][data-popover-hover]') || (hovered.contains(el) ? hovered : null);
        if (from && !from.contains(e.relatedTarget)) leave();
    });
    document.addEventListener('focusin', function (e) {
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover][data-popover-hover]') : null;
        if (trigger && !press) enter(trigger);
    });
    document.addEventListener('focusout', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (el && (el.closest('[data-popover][data-popover-hover]') || (hovered && hovered.contains(el)))) leave();
    });
    var LONG_PRESS = 500, SLOP = 10;
    var press = null;     // { trigger, panel, id, x, y, timer, fired } while a finger is down
    var pressed = null;   // the trigger whose long press opened its card, until its click
    var held = null;      // { trigger, panel } of that card, until the next press or key
    function cancelPress() {
        if (press) clearTimeout(press.timer);
        press = null;
    }
    document.addEventListener('pointerdown', function (e) {
        cancelPress();
        pressed = null;
        held = null;
        if (e.pointerType !== 'touch') return;
        var trigger = e.target instanceof Element ? e.target.closest('[data-popover][data-popover-hover]') : null;
        var panel = trigger ? target(trigger) : null;
        if (!panel) return;
        var p = press = { trigger: trigger, panel: panel, id: e.pointerId, x: e.clientX, y: e.clientY, fired: false };
        p.timer = setTimeout(function () {
            p.fired = true;
            pressed = trigger;
            held = { trigger: trigger, panel: panel };
            clearTimeout(hoverTimer); clearTimeout(leaveTimer); hovered = null;
            pinned.add(panel);
            show(panel, trigger);
        }, LONG_PRESS);
    }, true);
    document.addEventListener('pointermove', function (e) {
        if (!press || press.fired || e.pointerId !== press.id) return;
        if (Math.abs(e.clientX - press.x) > SLOP || Math.abs(e.clientY - press.y) > SLOP) cancelPress();
    }, true);
    function lift(e) {
        if (!press || e.pointerId !== press.id) return;
        var fired = press.fired;
        cancelPress();
        if (fired && e.type === 'pointercancel') held = null;
    }
    document.addEventListener('pointerup', lift, true);
    document.addEventListener('pointercancel', lift, true);
    document.addEventListener('click', function (e) {
        if (!pressed || !(e.target instanceof Node) || !pressed.contains(e.target)) return;
        pressed = null;
        e.preventDefault();
        e.stopImmediatePropagation();
    }, true);
    document.addEventListener('contextmenu', function (e) {
        if ((press && press.trigger.contains(e.target)) || (pressed && pressed.contains(e.target))) e.preventDefault();
    }, true);
    document.addEventListener('keydown', function () { held = null; }, true);
    document.addEventListener('toggle', function (e) {
        var panel = e.target;
        if (!panel || !panel.matches || !panel.matches('[popover]')) return;
        if (e.newState === 'closed') pinned.delete(panel);
        if (e.newState === 'closed' && held && held.panel === panel && held.trigger.isConnected) {
            pinned.add(panel);
            show(panel, held.trigger);
            return;
        }
        var trigger = triggers.get(panel);
        if (!trigger || !trigger.hasAttribute('data-popover')) return;
        trigger.setAttribute('aria-expanded', e.newState === 'open' ? 'true' : 'false');
    }, true);
})(window.sednaUi);
(function (ui) {
    function mark(el) {
        var max = el.scrollWidth - el.clientWidth;
        var pos = Math.abs(el.scrollLeft);
        el.toggleAttribute('data-scroll-start', max <= 1 || pos <= 1);
        el.toggleAttribute('data-scroll-end', max <= 1 || pos >= max - 1);
    }
    function markAll(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var all = scope.querySelectorAll('.sedna-scroll-x');
        for (var i = 0; i < all.length; i++) mark(all[i]);
        if (scope !== document && scope.classList && scope.classList.contains('sedna-scroll-x')) mark(scope);
    }
    document.addEventListener('scroll', function (e) {
        var el = e.target;
        if (el && el.classList && el.classList.contains('sedna-scroll-x')) mark(el);
    }, true);
    window.addEventListener('resize', function () { markAll(); });
    function watch() {
        markAll();
        if (!('MutationObserver' in window)) return;
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    if (added[j].nodeType === 1) markAll(added[j]);
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();
})(window.sednaUi);
(function (ui) {
    var GAP = 8;            // between the selection and the toolbar
    var MARGIN = 8;         // between the toolbar and the viewport's edge
    var open = null;        // { bar, host, text, item, from, stop } while one is open
    var pressing = false;   // a pointer is down: a selection being dragged is not made yet
    function elementOf(node) {
        return node ? (node.nodeType === 1 ? node : node.parentElement) : null;
    }
    function hostOf(node) {
        var el = elementOf(node);
        return el && el.closest ? el.closest('[data-selection-toolbar]') : null;
    }
    function barOf(host) {
        var id = host.getAttribute('data-selection-toolbar');
        return id ? document.getElementById(id) : null;
    }
    function isOpen(bar) {
        try { return bar.matches(':popover-open'); } catch (e) { return false; }
    }
    function read() {
        var sel = window.getSelection ? window.getSelection() : null;
        if (!sel || !sel.rangeCount || sel.isCollapsed) return null;
        var host = hostOf(sel.anchorNode);
        if (!host || hostOf(sel.focusNode) !== host) return null;
        var active = document.activeElement;
        if (active && host.contains(active) && active.matches('input, textarea')) return null;
        var text = sel.toString();
        if (!text || !text.trim()) return null;
        var bar = barOf(host);
        if (!bar) return null;
        var range = sel.getRangeAt(0);
        var rect = range.getBoundingClientRect();
        if (!rect.width && !rect.height) {
            var rects = range.getClientRects();
            if (!rects.length) return null;
            rect = rects[0];
        }
        var common = elementOf(range.commonAncestorContainer);
        var item = common ? common.closest('[data-selection-item]') : null;
        return {
            host: host, bar: bar, text: text, rect: rect,
            item: item && host.contains(item) ? item.getAttribute('data-selection-item') : null
        };
    }
    function place(bar, rect) {
        bar.style.left = '0px';
        bar.style.top = '0px';
        var w = bar.offsetWidth, h = bar.offsetHeight;
        var vw = document.documentElement.clientWidth, vh = document.documentElement.clientHeight;
        var side = 'top';
        var top = rect.top - GAP - h;
        if (top < MARGIN) { side = 'bottom'; top = rect.bottom + GAP; }
        top = Math.max(MARGIN, Math.min(top, vh - MARGIN - h));
        var left = rect.left + rect.width / 2 - w / 2;
        left = Math.max(MARGIN, Math.min(left, vw - MARGIN - w));
        bar.style.left = Math.round(left) + 'px';
        bar.style.top = Math.round(top) + 'px';
        bar.setAttribute('data-side', side);
    }
    function show(found) {
        var bar = found.bar;
        if (open && open.bar !== bar) close(false);
        if (!bar.hasAttribute('popover')) bar.setAttribute('popover', 'manual');
        try { if (!isOpen(bar)) bar.showPopover(); } catch (e) { return; }
        place(bar, found.rect);
        var from = open && open.bar === bar ? open.from : null;
        if (open && open.stop) open.stop();
        open = {
            bar: bar, host: found.host, text: found.text, item: found.item, from: from,
            stop: ui._.layer.whenGone(found.host, function () { close(false); })
        };
    }
    function close(restore) {
        if (!open) return;
        var was = open;
        open = null;
        if (was.stop) was.stop();
        var inside = was.bar.contains(document.activeElement);
        try { if (isOpen(was.bar)) was.bar.hidePopover(); } catch (e) { /* already closed */ }
        if (restore && inside) {
            var back = was.from && was.from.isConnected && was.from !== document.body ? was.from : null;
            try {
                if (back) back.focus({ preventScroll: true });
                else if (document.activeElement && document.activeElement.blur) document.activeElement.blur();
            } catch (e) { /* refused */ }
        }
    }
    function update() {
        var found = read();
        if (found) { show(found); return; }
        if (open && !open.bar.contains(document.activeElement)) close(false);
    }
    function controls(bar) {
        var all = bar.querySelectorAll('button, [href], input, select, textarea, [tabindex]');
        var list = [];
        for (var i = 0; i < all.length; i++) {
            var c = all[i];
            if (c.disabled || c.getAttribute('tabindex') === '-1' || !c.getClientRects().length) continue;
            list.push(c);
        }
        return list;
    }
    document.addEventListener('pointerdown', function (e) {
        if (open && open.bar.contains(e.target)) return;
        pressing = true;
    }, true);
    document.addEventListener('mousedown', function (e) {
        if (open && open.bar.contains(e.target)) e.preventDefault();
    }, true);
    function released() {
        if (!pressing) return;
        pressing = false;
        setTimeout(update, 0);
    }
    document.addEventListener('pointerup', released, true);
    document.addEventListener('pointercancel', released, true);
    document.addEventListener('selectionchange', function () {
        if (pressing) {
            if (open) {
                var sel = window.getSelection();
                if (!sel || sel.isCollapsed) close(false);
            }
            return;
        }
        update();
    });
    document.addEventListener('keydown', function (e) {
        if (!open || e.defaultPrevented) return;
        var bar = open.bar;
        if (e.key === 'Escape') {
            e.preventDefault();
            e.stopPropagation();
            close(true);
            return;
        }
        if (e.key === 'F10' && e.altKey) {
            var first = controls(bar)[0];
            if (!first) return;
            e.preventDefault();
            if (!bar.contains(document.activeElement)) open.from = document.activeElement;
            try { first.focus({ preventScroll: true }); } catch (err) { /* refused */ }
            return;
        }
        if (!bar.contains(e.target)) return;
        var list = controls(bar);
        var at = list.indexOf(document.activeElement);
        if (at < 0) return;
        var rtl = getComputedStyle(bar).direction === 'rtl';
        var next = -1;
        if (e.key === 'ArrowRight') next = at + (rtl ? -1 : 1);
        else if (e.key === 'ArrowLeft') next = at + (rtl ? 1 : -1);
        else if (e.key === 'Home') next = 0;
        else if (e.key === 'End') next = list.length - 1;
        else return;
        e.preventDefault();
        next = (next + list.length) % list.length;
        list[next].focus({ preventScroll: true });
    }, true);
    document.addEventListener('focusout', function (e) {
        if (!open || !open.bar.contains(e.target)) return;
        var to = e.relatedTarget;
        if (to && !open.bar.contains(to)) close(false);
    });
    document.addEventListener('click', function (e) {
        var control = e.target && e.target.closest ? e.target.closest('[data-selection-action]') : null;
        if (!control || !open || !open.bar.contains(control)) return;
        var was = open;
        var detail = {
            action: control.getAttribute('data-selection-action'),
            text: was.text,
            item: was.item,
            source: was.host.id || null
        };
        close(true);
        was.bar.dispatchEvent(new CustomEvent('sedna-selection', { bubbles: true, detail: detail }));
    });
    document.addEventListener('scroll', function (e) {
        if (!open) return;
        var t = e.target;
        if (t && t.nodeType === 1 && open.bar.contains(t)) return;
        close(false);
    }, true);
    window.addEventListener('resize', function () { if (open) update(); });
    ui.selection = {
        current: function () {
            return open ? { text: open.text, item: open.item, source: open.host.id || null } : null;
        },
        close: function () { close(true); }
    };
})(window.sednaUi);
(function (ui) {
    var typed = typeof WeakMap === 'function' ? new WeakMap() : null;
    var loading = [];
    function toArray(list) { return Array.prototype.slice.call(list); }
    function isItem(el) { return !!el && el.nodeType === 1 && el.getAttribute('role') === 'treeitem'; }
    function treeOf(el) { return el && el.closest ? el.closest('[role="tree"]') : null; }
    function inCombo(tree) { return !!tree.closest('.form-combo-panel'); }
    function groupOf(item) {
        for (var c = item.firstElementChild; c; c = c.nextElementSibling) {
            if (c.getAttribute('role') === 'group') return c;
        }
        return null;
    }
    function childrenOf(item) {
        var group = groupOf(item);
        return group ? toArray(group.children).filter(isItem) : [];
    }
    function parentOf(item) {
        var group = item.parentElement;
        if (!group || group.getAttribute('role') !== 'group') return null;
        var parent = group.parentElement && group.parentElement.closest('[role="treeitem"]');
        return parent && treeOf(parent) === treeOf(item) ? parent : null;
    }
    function topItems(tree) { return toArray(tree.children).filter(isItem); }
    function itemsOf(tree) {
        return toArray(tree.querySelectorAll('[role="treeitem"]')).filter(function (i) { return treeOf(i) === tree; });
    }
    function rowOf(item) {
        for (var c = item.firstElementChild; c; c = c.nextElementSibling) {
            if (c.classList.contains('tree-row')) return c;
        }
        return null;
    }
    function labelEl(item) {
        var row = rowOf(item);
        return (row && row.querySelector('.tree-label')) || row || item;
    }
    function labelOf(item) {
        return item.hasAttribute('data-label') ? item.getAttribute('data-label') : labelEl(item).textContent.trim();
    }
    function valueOf(item) { return item.hasAttribute('data-value') ? item.getAttribute('data-value') : labelOf(item); }
    function expandable(item) { return item.hasAttribute('aria-expanded'); }
    function expanded(item) { return item.getAttribute('aria-expanded') === 'true'; }
    function disabled(item) { return item.getAttribute('aria-disabled') === 'true'; }
    function selectable(item) { return item.hasAttribute('aria-selected'); }
    function selected(item) { return item.getAttribute('aria-selected') === 'true'; }
    function checkable(item) { return item.hasAttribute('aria-checked'); }
    function multi(tree) { return tree.getAttribute('aria-multiselectable') === 'true'; }
    function rtl(tree) {
        try { return getComputedStyle(tree).direction === 'rtl'; } catch (e) { return false; }
    }
    function visible(tree) {
        var out = [];
        (function walk(list) {
            list.forEach(function (item) {
                if (item.hidden) return;
                out.push(item);
                if (expanded(item)) walk(childrenOf(item));
            });
        })(topItems(tree));
        return out;
    }
    function send(el, name, detail) {
        el.dispatchEvent(new CustomEvent(name, { bubbles: true, composed: true, detail: detail }));
    }
    function settleStop(tree) {
        if (inCombo(tree)) return;
        var all = itemsOf(tree);
        if (!all.length) return;
        var rows = visible(tree);
        var stop = rows.filter(function (i) { return i.getAttribute('tabindex') === '0'; })[0]
            || rows.filter(selected)[0] || rows[0];
        all.forEach(function (i) {
            var want = i === stop ? '0' : '-1';
            if (i.getAttribute('tabindex') !== want) i.setAttribute('tabindex', want);
        });
    }
    function focusItem(tree, item, options) {
        itemsOf(tree).forEach(function (i) {
            var want = i === item ? '0' : '-1';
            if (i.getAttribute('tabindex') !== want) i.setAttribute('tabindex', want);
        });
        item.focus(options);
    }
    function setExpanded(item, open) {
        if (!expandable(item) || expanded(item) === open) return false;
        var tree = treeOf(item);
        item.setAttribute('aria-expanded', String(open));
        var lazy = open && item.hasAttribute('data-tree-lazy');
        if (lazy && item.getAttribute('aria-busy') !== 'true') {
            item.setAttribute('aria-busy', 'true');
            loading.push(item);
        }
        if (!open && tree && !inCombo(tree)) {
            var group = groupOf(item);
            if (group && group.contains(document.activeElement)) focusItem(tree, item);
            settleStop(tree);
        }
        if (open) send(item, 'sedna-tree-expand', { item: valueOf(item), lazy: lazy });
        else send(item, 'sedna-tree-collapse', { item: valueOf(item), lazy: false });
        return true;
    }
    function settleLoading() {
        loading = loading.filter(function (item) {
            if (!item.isConnected) return false;
            if (item.hasAttribute('data-tree-lazy') && !childrenOf(item).length) return true;
            item.removeAttribute('aria-busy');
            return false;
        });
    }
    function step(item, key) {
        var tree = treeOf(item);
        var forward = rtl(tree) ? 'ArrowLeft' : 'ArrowRight';
        if (key === forward) {
            if (expandable(item) && !expanded(item)) { setExpanded(item, true); return null; }
            if (expanded(item)) return childrenOf(item).filter(function (c) { return !c.hidden; })[0] || null;
            return null;
        }
        if (expanded(item)) { setExpanded(item, false); return null; }
        return parentOf(item);
    }
    function selectedValues(tree) {
        return itemsOf(tree).filter(selected).map(valueOf);
    }
    function checkedValues(tree) {
        return itemsOf(tree).filter(function (i) { return i.getAttribute('aria-checked') === 'true'; }).map(valueOf);
    }
    function setSelected(item, on) {
        var tree = treeOf(item);
        if (!tree || !selectable(item)) return false;
        if (on && !multi(tree)) {
            itemsOf(tree).forEach(function (i) {
                if (i !== item && selected(i)) i.setAttribute('aria-selected', 'false');
            });
        }
        if (selected(item) === on) return false;
        item.setAttribute('aria-selected', String(on));
        send(item, 'sedna-tree-select', { item: valueOf(item), selected: on, items: selectedValues(tree) });
        return true;
    }
    function mixOf(parent) {
        var kids = childrenOf(parent).filter(checkable);
        if (!kids.length) return parent.getAttribute('aria-checked');
        var on = 0, off = 0;
        kids.forEach(function (k) {
            var state = k.getAttribute('aria-checked');
            if (state === 'true') on++;
            else if (state === 'false') off++;
        });
        return on === kids.length ? 'true' : off === kids.length ? 'false' : 'mixed';
    }
    function setChecked(item, on) {
        var tree = treeOf(item);
        if (!tree || !checkable(item)) return false;
        var state = on ? 'true' : 'false';
        if (item.getAttribute('aria-checked') === state && childrenOf(item).every(function (c) {
            return !checkable(c) || disabled(c) || c.getAttribute('aria-checked') === state;
        })) return false;
        item.setAttribute('aria-checked', state);
        toArray(item.querySelectorAll('[role="treeitem"][aria-checked]')).forEach(function (d) {
            if (!disabled(d) && d.getAttribute('aria-checked') !== state) d.setAttribute('aria-checked', state);
        });
        for (var p = parentOf(item); p && checkable(p); p = parentOf(p)) {
            var mix = mixOf(p);
            if (p.getAttribute('aria-checked') !== mix) p.setAttribute('aria-checked', mix);
        }
        send(item, 'sedna-tree-check', { item: valueOf(item), selected: on, items: checkedValues(tree) });
        return true;
    }
    function pick(item) {
        if (disabled(item)) return checkable(item) || selectable(item);
        if (checkable(item)) { setChecked(item, item.getAttribute('aria-checked') !== 'true'); return true; }
        if (selectable(item)) {
            var tree = treeOf(item);
            setSelected(item, multi(tree) ? !selected(item) : true);
            return true;
        }
        return false;
    }
    function picked(tree) {
        var items = itemsOf(tree);
        if (items.some(checkable)) {
            return items.filter(function (i) {
                if (i.getAttribute('aria-checked') !== 'true') return false;
                var p = parentOf(i);
                return !(p && p.getAttribute('aria-checked') === 'true');
            });
        }
        return items.filter(selected);
    }
    function filter(tree, query) {
        var items = itemsOf(tree);
        if (!query) {
            items.forEach(function (i) { if (i.hidden) i.hidden = false; });
            return;
        }
        for (var n = items.length - 1; n >= 0; n--) {
            var item = items[n];
            var under = childrenOf(item).some(function (c) { return !c.hidden; });
            var keep = under || ui._.score(query, labelOf(item), 0) >= 0;
            if (item.hidden === keep) item.hidden = !keep;
            if (under) setExpanded(item, true);
        }
    }
    function sameOwner(item, tree) { return !!item && treeOf(item) === tree; }
    ui._.tree = {
        items: itemsOf,
        visible: visible,
        labelEl: labelEl,
        labelOf: labelOf,
        valueOf: valueOf,
        rowOf: rowOf,
        step: step,
        pick: pick,
        picked: picked,
        setSelected: setSelected,
        setChecked: setChecked,
        filter: filter,
        disabled: disabled
    };
    document.addEventListener('focusin', function (e) {
        var item = e.target;
        if (!isItem(item)) return;
        var tree = treeOf(item);
        if (!tree || inCombo(tree) || item.getAttribute('tabindex') === '0') return;
        itemsOf(tree).forEach(function (i) {
            var want = i === item ? '0' : '-1';
            if (i.getAttribute('tabindex') !== want) i.setAttribute('tabindex', want);
        });
    });
    document.addEventListener('click', function (e) {
        var target = e.target;
        if (!(target instanceof Element)) return;
        var row = target.closest('.tree-row');
        var item = row && row.parentElement;
        if (!isItem(item)) return;
        var tree = treeOf(item);
        if (!tree) return;
        if (!inCombo(tree) && document.activeElement !== item) focusItem(tree, item, { preventScroll: true });
        if (target.closest('.tree-toggle')) { setExpanded(item, !expanded(item)); return; }
        if (pick(item)) return;
        if (expandable(item) && !target.closest('a[href]')) setExpanded(item, !expanded(item));
    });
    function dragsItself(item) {
        if (!item.hasAttribute('data-drag-item')) return false;
        var handles = item.querySelectorAll('[data-drag-handle]');
        for (var i = 0; i < handles.length; i++) {
            if (handles[i].closest('[data-drag-item]') === item && handles[i].getAttribute('data-drag-handle') !== 'pointer') return false;
        }
        return true;
    }
    document.addEventListener('keydown', function (e) {
        var item = e.target;
        if (!isItem(item) || e.isComposing || e.altKey) return;
        var tree = treeOf(item);
        if (!tree || inCombo(tree)) return;
        if (ui.drag && ui.drag.active && ui.drag.active() !== null) return;
        var rows = visible(tree), at = rows.indexOf(item), to = null;
        var key = e.key;
        if (e.ctrlKey || e.metaKey) {
            if ((key === 'a' || key === 'A') && multi(tree)) {
                e.preventDefault();
                rows.forEach(function (i) { if (!disabled(i)) setSelected(i, true); });
            }
            return;
        }
        switch (key) {
            case 'ArrowDown': to = rows[at + 1]; break;
            case 'ArrowUp': to = rows[at - 1]; break;
            case 'Home': to = rows[0]; break;
            case 'End': to = rows[rows.length - 1]; break;
            case 'ArrowRight':
            case 'ArrowLeft':
                e.preventDefault();
                to = step(item, key);
                if (!to) { settleStop(tree); return; }
                break;
            case '*':
                e.preventDefault();
                (parentOf(item) ? childrenOf(parentOf(item)) : topItems(tree)).forEach(function (i) { setExpanded(i, true); });
                return;
            case ' ':
                if (dragsItself(item)) return;
                e.preventDefault();
                pick(item);
                return;
            case 'Enter':
                if (dragsItself(item)) return;
                var row = rowOf(item);
                var link = row && (row.matches('a[href]') ? row : row.querySelector('a[href]'));
                e.preventDefault();
                if (link) { link.click(); return; }
                if (!pick(item) && expandable(item)) setExpanded(item, !expanded(item));
                return;
            default:
                if (key.length !== 1) return;
                var state = typed ? typed.get(tree) : null;
                if (!state) { state = {}; if (typed) typed.set(tree, state); }
                to = ui._.typeahead(state, key, rows, item, labelOf);
                if (!to) return;
        }
        if (!sameOwner(to, tree)) return;
        e.preventDefault();
        focusItem(tree, to);
        if (e.shiftKey && multi(tree) && (key === 'ArrowDown' || key === 'ArrowUp') && !disabled(to)) {
            setSelected(to, !selected(to));
        }
    });
    function sweep() {
        toArray(document.querySelectorAll('[role="tree"]')).forEach(settleStop);
        settleLoading();
    }
    if (typeof MutationObserver === 'function') {
        var queued = false;
        new MutationObserver(function () {
            if (queued) return;
            queued = true;
            Promise.resolve().then(function () { queued = false; sweep(); });
        }).observe(document.documentElement, {
            childList: true, subtree: true, attributes: true, attributeFilter: ['data-tree-lazy']
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', sweep);
    else sweep();
})(window.sednaUi);
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
    function spread(boxes, from, text) {
        var i = from;
        for (var c = 0; c < text.length && i < boxes.length; c++, i++) boxes[i].value = text.charAt(c);
        return i;
    }
    function go(boxes, i) {
        var box = boxes[Math.max(0, Math.min(i, boxes.length - 1))];
        if (box && box !== document.activeElement) box.focus();
    }
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
})();
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
        st.s = clamp(Math.round(s * 100) / 100, 0, 1);
        st.v = clamp(Math.round(v * 100) / 100, 0, 1);
        var changed = render(picker);
        if (changed.length) { dispatch(changed, 'input'); dispatch(changed, 'change'); }
    }
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
        document.addEventListener('input', onInput, true);
        document.addEventListener('change', onChange, true);
        document.addEventListener('pointerdown', onPointerDown);
        document.addEventListener('pointermove', onPointerMove);
        document.addEventListener('pointerup', onPointerUp);
        document.addEventListener('pointercancel', onPointerUp);
        document.addEventListener('keydown', onKeyDown);
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
(function (ui) {
    var score = ui._.score;
    var MATCH = 'sedna-combo-match';
    var EMAIL = /^[^\s@]+@[^\s@.]+(\.[^\s@.]+)+$/;
    var quiet = false;        // true while this file dispatches its own input event
    var lastMore = null;      // the .form-combo-more clicked last, and when
    var lastMoreAt = 0;
    var openedByPress = null; // a toggle whose own press opened its field through focus
    function toArray(list) { return Array.prototype.slice.call(list); }
    function hostOf(node) {
        var el = node && node.nodeType === 1 ? node : node && node.parentElement;
        return el && el.closest ? el.closest('[data-combo]') : null;
    }
    function inputOf(host) { return host.querySelector('.form-combo-input'); }
    function boxOf(host) { return host.querySelector('.form-combo-box'); }
    function listOf(host) { return host.querySelector('.form-combo-list, .form-combo-panel > [role="tree"]'); }
    function treeOf(host) { return host.querySelector('.form-combo-panel > [role="tree"]'); }
    function isTreeItem(el) { return !!el && el.getAttribute('role') === 'treeitem'; }
    function inline(host) { return host.classList.contains('form-combo--inline'); }
    function managed(host) { return host.hasAttribute('data-combo-managed'); }
    function fromServer(host) { return host.getAttribute('data-combo-source') === 'server'; }
    function modeOf(host) {
        var mode = host.getAttribute('data-combo');
        return mode === 'multi' || mode === 'free' ? mode : 'single';
    }
    function isOpen(host) {
        var input = inputOf(host);
        return !!input && input.getAttribute('aria-expanded') === 'true';
    }
    function isComboInput(el) {
        return !!(el && el.classList && el.classList.contains('form-combo-input') && hostOf(el));
    }
    function options(host) {
        var list = listOf(host);
        return list ? toArray(list.querySelectorAll('.form-combo-option')) : [];
    }
    function enabled(option) { return option.getAttribute('aria-disabled') !== 'true'; }
    function selected(option) { return option.getAttribute('aria-selected') === 'true'; }
    function reachable(host) {
        var tree = treeOf(host);
        if (tree) return ui._.tree.visible(tree).filter(enabled);
        return options(host).filter(function (o) { return !o.hidden && enabled(o); });
    }
    function stops(host) {
        if (treeOf(host)) return reachable(host);
        var list = listOf(host);
        if (!list) return [];
        return toArray(list.querySelectorAll('.form-combo-option, .form-combo-group-action')).filter(function (el) {
            if (el.classList.contains('form-combo-option')) return !el.hidden && enabled(el);
            var heading = el.closest('.form-combo-group');
            return !el.disabled && !(heading && heading.hidden);
        });
    }
    function rowsOf(heading) {
        var rows = [];
        for (var next = heading.nextElementSibling; next && !next.classList.contains('form-combo-group'); next = next.nextElementSibling) {
            if (next.classList.contains('form-combo-option')) rows.push(next);
        }
        return rows;
    }
    function multiselectable(host) {
        var list = listOf(host);
        return !!list && list.getAttribute('aria-multiselectable') === 'true';
    }
    function mark(host) {
        if (!multiselectable(host)) return;
        toArray(listOf(host).querySelectorAll('.form-combo-group')).forEach(function (heading) {
            var rows = rowsOf(heading).filter(enabled);
            var all = rows.length > 0 && rows.every(selected);
            if (heading.hasAttribute('data-all-selected') !== all) {
                if (all) heading.setAttribute('data-all-selected', '');
                else heading.removeAttribute('data-all-selected');
            }
        });
    }
    function toggleGroup(host, action) {
        var heading = action.closest('.form-combo-group');
        if (!heading || !multiselectable(host)) return;
        var rows = rowsOf(heading).filter(enabled);
        var all = rows.length > 0 && rows.every(selected);
        rows.forEach(function (o) { if (selected(o) === all) o.click(); });
        mark(host);
    }
    function chips(host) {
        var box = boxOf(host);
        return box ? toArray(box.querySelectorAll(':scope > .chip')) : [];
    }
    function textOf(option) {
        if (isTreeItem(option)) return ui._.tree.labelEl(option);
        return option.querySelector('.form-combo-option-text') || option;
    }
    function labelOf(option) {
        return option.hasAttribute('data-label')
            ? option.getAttribute('data-label')
            : textOf(option).textContent.trim();
    }
    function valueOf(option) {
        return option.hasAttribute('data-value') ? option.getAttribute('data-value') : labelOf(option);
    }
    function query(host) {
        var input = inputOf(host);
        return input ? input.value.trim() : '';
    }
    function setQuery(input, text) {
        if (input.value === text) return;
        input.value = text;
        quiet = true;
        try { input.dispatchEvent(new Event('input', { bubbles: true })); }
        finally { quiet = false; }
    }
    function filter(host) {
        var tree = treeOf(host);
        if (tree) {
            if (!fromServer(host)) ui._.tree.filter(tree, query(host));
            paint(host);
            return;
        }
        if (!fromServer(host)) {
            var q = query(host);
            options(host).forEach(function (o) {
                var hide = !!q && score(q, labelOf(o), 0) < 0;
                if (o.hidden !== hide) o.hidden = hide;
            });
            var list = listOf(host), heading = null, any = false;
            if (list) {
                toArray(list.children).forEach(function (child) {
                    if (child.classList.contains('form-combo-group')) {
                        if (heading && heading.hidden === any) heading.hidden = !any;
                        heading = child;
                        any = false;
                    } else if (child.classList.contains('form-combo-option') && !child.hidden) {
                        any = true;
                    }
                });
                if (heading && heading.hidden === any) heading.hidden = !any;
            }
        }
        mark(host);
        paint(host);
    }
    function runs(q, text) {
        var n = q.toLowerCase(), h = text.toLowerCase();
        var at = h.indexOf(n);
        if (at >= 0) return [[at, at + n.length]];
        var out = [], from = 0;
        for (var i = 0; i < n.length; i++) {
            if (n[i] === ' ') continue;
            var found = h.indexOf(n[i], from);
            if (found < 0) return [];
            var last = out[out.length - 1];
            if (last && last[1] === found) last[1] = found + 1;
            else out.push([found, found + 1]);
            from = found + 1;
        }
        return out;
    }
    function rangesIn(root, spans) {
        var walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT);
        var nodes = [], node;
        while ((node = walker.nextNode())) nodes.push(node);
        var out = [];
        spans.forEach(function (span) {
            var range = document.createRange(), offset = 0, started = false;
            for (var i = 0; i < nodes.length; i++) {
                var len = nodes[i].data.length;
                if (!started && span[0] < offset + len) {
                    range.setStart(nodes[i], span[0] - offset);
                    started = true;
                }
                if (started && span[1] <= offset + len) {
                    range.setEnd(nodes[i], span[1] - offset);
                    out.push(range);
                    return;
                }
                offset += len;
            }
        });
        return out;
    }
    function paint(host) {
        if (!window.CSS || !CSS.highlights || typeof Highlight !== 'function') return;
        var q = query(host);
        if (!q || !isOpen(host)) { CSS.highlights.delete(MATCH); return; }
        var highlight = new Highlight();
        var tree = treeOf(host);
        (tree ? ui._.tree.items(tree) : options(host)).forEach(function (o) {
            if (o.hidden) return;
            var target = textOf(o);
            rangesIn(target, runs(q, target.textContent)).forEach(function (r) { highlight.add(r); });
        });
        CSS.highlights.set(MATCH, highlight);
    }
    function activeOf(host) {
        var list = listOf(host);
        return list ? list.querySelector('.form-combo-option[data-active], .form-combo-group-action[data-active], [role="treeitem"][data-active]') : null;
    }
    function setActive(host, option, scroll) {
        var input = inputOf(host), list = listOf(host);
        if (list) toArray(list.querySelectorAll('[data-active]')).forEach(function (o) {
            if (o !== option) o.removeAttribute('data-active');
        });
        if (!option) {
            if (input) input.removeAttribute('aria-activedescendant');
            return;
        }
        if (!option.hasAttribute('data-active')) option.setAttribute('data-active', '');
        if (option.id) input.setAttribute('aria-activedescendant', option.id);
        else input.removeAttribute('aria-activedescendant');
        if (scroll && option.scrollIntoView) option.scrollIntoView({ block: 'nearest' });
    }
    function settle(host, fresh) {
        var active = activeOf(host), rows = reachable(host);
        if (!fresh && active && stops(host).indexOf(active) >= 0) { setActive(host, active); return; }
        if (modeOf(host) === 'free') { setActive(host, null); return; }
        var chosen = rows.filter(function (o) { return o.getAttribute('aria-selected') === 'true'; })[0];
        var q = query(host);
        var first = q && treeOf(host)
            ? rows.filter(function (o) { return score(q, ui._.tree.labelOf(o), 0) >= 0; })[0]
            : null;
        setActive(host, (!q && chosen) || first || rows[0] || null, true);
    }
    function move(host, step) {
        var rows = stops(host);
        if (!rows.length) return;
        var at = rows.indexOf(activeOf(host));
        var to = at < 0 ? (step > 0 ? 0 : rows.length - 1) : Math.max(0, Math.min(rows.length - 1, at + step));
        setActive(host, rows[to], true);
        requestMore(host);
    }
    function open(host) {
        var input = inputOf(host);
        if (!input || input.disabled || input.readOnly) return;
        if (!host.querySelector('.form-combo-panel')) return;
        if (!isOpen(host)) input.setAttribute('aria-expanded', 'true');
        filter(host);
        settle(host);
        requestMore(host);
    }
    function close(host) {
        var input = inputOf(host);
        if (!input) return;
        if (!inline(host) && isOpen(host)) input.setAttribute('aria-expanded', 'false');
        setActive(host, null);
        paint(host);
    }
    function disarm(host) {
        chips(host).forEach(function (c) { if (c.hasAttribute('data-armed')) c.removeAttribute('data-armed'); });
    }
    function backspace(host, e) {
        var all = chips(host), last = all[all.length - 1];
        if (!last) return;
        e.preventDefault();
        if (!last.hasAttribute('data-armed')) { last.setAttribute('data-armed', ''); return; }
        last.removeAttribute('data-armed');
        var dismiss = last.querySelector('.chip-dismiss');
        if (dismiss) dismiss.click();
    }
    function findChip(host, value) {
        var key = String(value).toLowerCase();
        return chips(host).filter(function (c) {
            return (c.getAttribute('data-value') || '').toLowerCase() === key;
        })[0] || null;
    }
    function addChip(host, value, label) {
        var template = host.querySelector('template[data-combo-chip]');
        var input = inputOf(host);
        if (!template || !input || findChip(host, value)) return;
        var chip = template.content.firstElementChild.cloneNode(true);
        chip.setAttribute('data-value', value);
        [chip].concat(toArray(chip.querySelectorAll('*'))).forEach(function (el) {
            toArray(el.attributes).forEach(function (a) {
                if (a.value.indexOf('{label}') >= 0) el.setAttribute(a.name, a.value.split('{label}').join(label));
            });
        });
        var text = chip.querySelector('.chip-label');
        if (text) text.textContent = label;
        toArray(chip.querySelectorAll('input[type="hidden"]')).forEach(function (h) { h.value = value; });
        input.parentNode.insertBefore(chip, input);
    }
    function unselect(host, value) {
        options(host).forEach(function (o) {
            if (valueOf(o) === value) o.setAttribute('aria-selected', 'false');
        });
    }
    function validate(host) {
        if (host.getAttribute('data-combo-validate') !== 'email') return;
        var input = inputOf(host), bad = false;
        chips(host).forEach(function (c) {
            var invalid = !EMAIL.test(c.getAttribute('data-value') || '');
            c.classList.toggle('chip--invalid', invalid);
            bad = bad || invalid;
        });
        if (bad) input.setAttribute('aria-invalid', 'true');
        else input.removeAttribute('aria-invalid');
    }
    function selectManaged(host, option) {
        var mode = modeOf(host), value = valueOf(option), label = labelOf(option);
        if (mode === 'single') {
            options(host).forEach(function (o) { o.setAttribute('aria-selected', String(o === option)); });
            var shown = host.querySelector('.form-combo-value');
            if (shown) shown.textContent = label;
            var field = host.querySelector('input[data-combo-field]');
            if (field) field.value = value;
            return;
        }
        var on = mode === 'free' || option.getAttribute('aria-selected') !== 'true';
        option.setAttribute('aria-selected', String(on));
        if (on) addChip(host, value, label);
        else {
            var chip = findChip(host, value);
            if (chip) chip.remove();
        }
        validate(host);
    }
    function syncTree(host) {
        var tree = treeOf(host);
        var picks = ui._.tree.picked(tree);
        if (modeOf(host) === 'single') {
            var one = picks[0];
            var shown = host.querySelector('.form-combo-value');
            if (shown) shown.textContent = one ? ui._.tree.labelOf(one) : '';
            var field = host.querySelector('input[data-combo-field]');
            if (field) field.value = one ? ui._.tree.valueOf(one) : '';
            return;
        }
        var values = picks.map(ui._.tree.valueOf);
        chips(host).forEach(function (c) {
            if (values.indexOf(c.getAttribute('data-value')) < 0) c.remove();
        });
        picks.forEach(function (item) { addChip(host, ui._.tree.valueOf(item), ui._.tree.labelOf(item)); });
        validate(host);
    }
    function untree(host, value) {
        var tree = treeOf(host);
        ui._.tree.items(tree).forEach(function (item) {
            if (ui._.tree.valueOf(item) !== value) return;
            if (item.hasAttribute('aria-checked')) ui._.tree.setChecked(item, false);
            else ui._.tree.setSelected(item, false);
        });
        syncTree(host);
    }
    function pickedTree(host, item, onToggle) {
        var input = inputOf(host);
        var pickable = (item.hasAttribute('aria-selected') || item.hasAttribute('aria-checked')) && enabled(item);
        if (managed(host) && pickable && !onToggle) syncTree(host);
        var clear = function () { if (input.value) setQuery(input, ''); };
        if (onToggle || !pickable || modeOf(host) === 'multi') {
            if (isOpen(host)) setActive(host, item);
            input.focus();
            if (pickable && !onToggle) setTimeout(clear, 0);
            return;
        }
        close(host);
        input.focus();
        setTimeout(clear, 0);
    }
    function picked(host, option) {
        if (option.getAttribute('aria-disabled') === 'true') return;
        var input = inputOf(host);
        if (managed(host)) { selectManaged(host, option); mark(host); }
        var clear = function () { if (input.value) setQuery(input, ''); };
        if (modeOf(host) === 'multi') {
            setActive(host, option);
            setTimeout(clear, 0);
            input.focus();
            return;
        }
        close(host);
        input.focus();
        setTimeout(clear, 0);
    }
    function splitter(host) {
        var input = inputOf(host);
        var email = host.getAttribute('data-combo-validate') === 'email' ||
            (input && (input.type === 'email' || input.getAttribute('inputmode') === 'email'));
        return email ? /[,;\s]+/ : /[,;\r\n\t]+/;
    }
    function commit(host, raw) {
        var input = inputOf(host);
        var seen = chips(host).map(function (c) { return (c.getAttribute('data-value') || '').toLowerCase(); });
        var entries = [];
        String(raw).split(splitter(host)).forEach(function (part) {
            var entry = part.trim(), key = entry.toLowerCase();
            if (!entry || seen.indexOf(key) >= 0) return;
            seen.push(key);
            entries.push(entry);
        });
        if (managed(host)) {
            entries.forEach(function (entry) { addChip(host, entry, entry); });
            validate(host);
        } else {
            entries.forEach(function (entry) {
                input.value = entry;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            });
        }
        setQuery(input, '');
        close(host);
    }
    function requestMore(host) {
        var list = listOf(host);
        if (!list || !isOpen(host) || list.getAttribute('aria-busy') === 'true') return;
        var more = host.querySelector('.form-combo-more');
        if (!more || more.disabled || more.hidden) return;
        var row = more.offsetHeight || list.clientHeight / 8;
        if (list.scrollHeight - list.scrollTop - list.clientHeight > row) return;
        if (more === lastMore && Date.now() - lastMoreAt < 1000) return;
        lastMore = more;
        lastMoreAt = Date.now();
        more.click();
    }
    ui.combo = {
        open: function (el) { var host = hostOf(el); if (host) open(host); },
        close: function (el) { var host = hostOf(el); if (host) close(host); }
    };
    document.addEventListener('mousedown', function (e) {
        var host = hostOf(e.target);
        if (!host) return;
        var input = inputOf(host);
        if (!input || input.disabled || e.target === input) return;
        var inPanel = e.target.closest('.form-combo-panel');
        var inBox = e.target.closest('.form-combo-box');
        if (!inPanel && !inBox) return;
        if (inPanel && e.target.closest('input, textarea, select')) return;
        e.preventDefault();
        var wasOpen = isOpen(host);
        if (document.activeElement !== input) input.focus();
        var toggle = e.target.closest('.form-combo-toggle');
        openedByPress = toggle && !wasOpen && isOpen(host) ? toggle : null;
        if (inBox && !e.target.closest('button, .chip') && modeOf(host) !== 'free') {
            if (wasOpen) close(host); else open(host);
        }
    });
    document.addEventListener('focusin', function (e) {
        var input = e.target;
        if (!isComboInput(input)) return;
        var host = hostOf(input);
        if (!host.classList.contains('form-combo--collapse') || modeOf(host) === 'free') return;
        if (e.relatedTarget && host.contains(e.relatedTarget)) return;
        open(host);
    });
    document.addEventListener('click', function (e) {
        var host = hostOf(e.target);
        if (!host) return;
        var input = inputOf(host);
        if (!input) return;
        if (e.target === input) {
            if (!isOpen(host) && modeOf(host) !== 'free') open(host);
            return;
        }
        var option = e.target.closest('.form-combo-option');
        if (option && host.contains(option)) { picked(host, option); return; }
        var item = treeOf(host) && e.target.closest('[role="treeitem"]');
        if (item && host.contains(item)) { pickedTree(host, item, !!e.target.closest('.tree-toggle')); return; }
        var action = e.target.closest('.form-combo-group-action');
        if (action && host.contains(action)) {
            if (action.hasAttribute('data-combo-toggle-group')) toggleGroup(host, action);
            if (isOpen(host)) setActive(host, action);
            input.focus();
            return;
        }
        var dismiss = e.target.closest('.chip-dismiss');
        if (dismiss && dismiss.closest('.form-combo-box')) {
            if (managed(host)) {
                var chip = dismiss.closest('.chip');
                var value = chip.getAttribute('data-value');
                chip.remove();
                if (value !== null) {
                    if (treeOf(host)) untree(host, value);
                    else unselect(host, value);
                }
                validate(host);
            }
            input.focus();
            return;
        }
        var label = e.target.closest('.chip--invalid .chip-label');
        if (label && managed(host) && modeOf(host) === 'free') {
            var bad = label.closest('.chip');
            var text = bad.getAttribute('data-value') || label.textContent;
            bad.remove();
            validate(host);
            input.value = text;
            input.focus();
            return;
        }
        if (e.target.closest('.form-combo-clear')) {
            if (managed(host)) {
                chips(host).forEach(function (c) { c.remove(); });
                options(host).forEach(function (o) { o.setAttribute('aria-selected', 'false'); });
                var tree = treeOf(host);
                if (tree) ui._.tree.items(tree).forEach(function (i) {
                    if (i.hasAttribute('aria-checked')) ui._.tree.setChecked(i, false);
                    else ui._.tree.setSelected(i, false);
                });
                var shown = host.querySelector('.form-combo-value');
                if (shown) shown.textContent = '';
                var field = host.querySelector('input[data-combo-field]');
                if (field) field.value = '';
                validate(host);
            }
            setQuery(input, '');
            input.focus();
            return;
        }
        var toggle = e.target.closest('.form-combo-toggle');
        if (toggle) {
            if (openedByPress === toggle) openedByPress = null;
            else if (isOpen(host)) close(host);
            else open(host);
            input.focus();
        }
    });
    document.addEventListener('keydown', function (e) {
        var input = e.target;
        if (!isComboInput(input) || e.isComposing) return;
        var host = hostOf(input), mode = modeOf(host), openNow = isOpen(host);
        if (e.key !== 'Backspace') disarm(host);
        switch (e.key) {
            case 'ArrowDown':
            case 'ArrowUp':
                if (!host.querySelector('.form-combo-panel')) return;
                e.preventDefault();
                if (e.altKey && e.key === 'ArrowUp') { close(host); return; }
                if (!openNow) { open(host); return; }
                move(host, e.key === 'ArrowDown' ? 1 : -1);
                return;
            case 'Enter':
                var active = openNow ? activeOf(host) : null;
                if (active) {
                    e.preventDefault();
                    (isTreeItem(active) ? ui._.tree.rowOf(active) || active : active).click();
                    return;
                }
                if (mode === 'free') {
                    if (input.value.trim()) { e.preventDefault(); commit(host, input.value); }
                    return;
                }
                e.preventDefault();
                if (!openNow) open(host);
                return;
            case 'ArrowRight':
            case 'ArrowLeft':
                var at = openNow && !input.value && treeOf(host) ? activeOf(host) : null;
                if (!at || !isTreeItem(at)) return;
                e.preventDefault();
                var to = ui._.tree.step(at, e.key);
                setActive(host, to && enabled(to) ? to : at, true);
                return;
            case 'Escape':
                if (openNow && !inline(host)) {
                    e.preventDefault();
                    close(host);
                    setQuery(input, '');
                } else if (input.value) {
                    e.preventDefault();
                    setQuery(input, '');
                }
                return;
            case 'Tab':
                if (mode !== 'free') close(host);
                return;
            case ',':
            case ';':
                if (mode === 'free') {
                    e.preventDefault();
                    if (input.value.trim()) commit(host, input.value);
                }
                return;
            case 'Backspace':
                if (mode === 'single' || input.value || input.selectionStart) { disarm(host); return; }
                backspace(host, e);
                return;
        }
    });
    document.addEventListener('input', function (e) {
        var input = e.target;
        if (!isComboInput(input)) return;
        var host = hostOf(input);
        if (quiet) {
            if (isOpen(host)) { filter(host); settle(host); }
            return;
        }
        disarm(host);
        if (modeOf(host) === 'free' && /[,;\r\n]/.test(input.value)) { commit(host, input.value); return; }
        if (!isOpen(host)) { if (input.value) open(host); return; }
        var list = listOf(host);
        if (list) list.scrollTop = 0;
        filter(host);
        settle(host, true);
    });
    document.addEventListener('paste', function (e) {
        var input = e.target;
        if (!isComboInput(input)) return;
        var host = hostOf(input);
        if (modeOf(host) !== 'free' || !e.clipboardData) return;
        var text = e.clipboardData.getData('text');
        if (!splitter(host).test(text.trim())) return;
        e.preventDefault();
        var v = input.value;
        commit(host, v.slice(0, input.selectionStart) + text + v.slice(input.selectionEnd));
    });
    window.addEventListener('change', function (e) {
        var input = e.target;
        if (!e.isTrusted || !isComboInput(input)) return;
        var host = hostOf(input);
        if (modeOf(host) !== 'free') return;
        e.stopImmediatePropagation();
        if (input.value.trim()) commit(host, input.value);
    }, true);
    document.addEventListener('focusout', function (e) {
        var host = hostOf(e.target);
        if (!host) return;
        if (e.relatedTarget && host.contains(e.relatedTarget)) return;
        disarm(host);
        if (!isComboInput(e.target)) return;
        if (modeOf(host) !== 'free' && e.target.value) setQuery(e.target, '');
        close(host);
    });
    document.addEventListener('scroll', function (e) {
        var list = e.target;
        if (!list || !list.classList || !list.classList.contains('form-combo-list')) return;
        var host = hostOf(list);
        if (host) requestMore(host);
    }, true);
    if (typeof MutationObserver === 'function') {
        new MutationObserver(function (records) {
            var focused = document.activeElement;
            if (isComboInput(focused)) {
                var own = hostOf(focused);
                if (modeOf(own) === 'free' && !isOpen(own) && focused.value.trim() && listOf(own)) open(own);
            }
            var openInputs = document.querySelectorAll('[data-combo] .form-combo-input[aria-expanded="true"]');
            for (var i = 0; i < openInputs.length; i++) {
                var host = hostOf(openInputs[i]);
                var mine = records.filter(function (r) { return host.contains(r.target); });
                if (!mine.length) continue;
                if (mine.every(function (r) { return r.type === 'attributes'; })) { mark(host); continue; }
                filter(host);
                settle(host);
                requestMore(host);
            }
        }).observe(document.documentElement, {
            childList: true, subtree: true, characterData: true,
            attributes: true, attributeFilter: ['aria-selected']
        });
    }
    function markAll() {
        toArray(document.querySelectorAll('[data-combo]')).forEach(function (host) {
            if (host.querySelector('.form-combo-group-action')) mark(host);
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', markAll);
    else markAll();
})(window.sednaUi);
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
    function follow() {
        var area = field(document.activeElement);
        if (area) refresh(area);
    }
    document.addEventListener('selectionchange', follow);
    document.addEventListener('keyup', follow);
    document.addEventListener('pointerup', follow);
    document.addEventListener('focusin', function (e) { var area = field(e.target); if (area) refresh(area); });
    document.addEventListener('focusout', function (e) { var area = field(e.target); if (area) clear(mirrorOf(area)); });
    document.addEventListener('scroll', function (e) {
        var area = field(e.target);
        var mirror = area ? mirrorOf(area) : null;
        if (mirror) mirror.scrollTop = area.scrollTop;
    }, true);
    window.addEventListener('resize', follow);
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
        refresh: function (area) { area = field(area); if (area) refresh(area); },
        accept: function (area) { area = field(area); return area ? accept(area) : false; },
        dismiss: function (area) { area = field(area); if (area) dismiss(area); }
    };
})(window.sednaUi);
(function (ui) {
    var TRIGGER = /(^|\s)@([^\s@]{0,40})$/;
    var ids = 0;
    var open = null;    // { field, list, start, end } while a list is open
    var dismissed = typeof WeakMap === 'function' ? new WeakMap() : null;
    function fieldOf(el) {
        return el && el.nodeType === 1 && el.matches('textarea[data-mention], input[data-mention]') ? el : null;
    }
    function listOf(field) {
        var id = field.getAttribute('data-mention');
        return id ? document.getElementById(id) : null;
    }
    function itemsOf(list) {
        return Array.prototype.filter.call(list.querySelectorAll('.menu-item[data-mention-value]'), function (item) {
            return !item.disabled && item.getAttribute('aria-disabled') !== 'true';
        });
    }
    function shown(list) {
        return itemsOf(list).filter(function (item) { return !item.hidden; });
    }
    function labelOf(item) {
        return String(item.getAttribute('data-label') || item.textContent || '').trim();
    }
    function setActive(list, field, item) {
        var all = itemsOf(list);
        for (var i = 0; i < all.length; i++) {
            var on = all[i] === item;
            all[i].toggleAttribute('data-active', on);
            all[i].setAttribute('aria-selected', on ? 'true' : 'false');
        }
        if (item) {
            field.setAttribute('aria-activedescendant', item.id);
            try { item.scrollIntoView({ block: 'nearest' }); } catch (err) { /* detached */ }
        } else {
            field.removeAttribute('aria-activedescendant');
        }
    }
    function close() {
        if (!open) return;
        var was = open;
        open = null;
        was.list.hidden = true;
        setActive(was.list, was.field, null);
    }
    function triggerIn(field) {
        if (field.selectionStart !== field.selectionEnd) return null;
        var end = field.selectionStart;
        var m = TRIGGER.exec(field.value.slice(0, end));
        if (!m) return null;
        return { start: end - m[2].length - 1, end: end, query: m[2] };
    }
    function update(field, typed) {
        var at = triggerIn(field);
        var list = listOf(field);
        if (!at || !list) {
            if (dismissed) dismissed.delete(field);
            if (open && open.field === field) close();
            return;
        }
        if (dismissed && dismissed.get(field) === at.start) return;
        if (!typed && !(open && open.field === field)) return;
        var all = itemsOf(list), any = false;
        for (var i = 0; i < all.length; i++) {
            var item = all[i];
            var match = !at.query
                || ui._.score(at.query, labelOf(item), 0) >= 0
                || ui._.score(at.query, item.getAttribute('data-mention-value') || '', 0) >= 0;
            item.hidden = !match;
            if (match) any = true;
        }
        if (!any) {
            if (open && open.field === field) close();
            return;
        }
        if (open && open.list !== list) close();
        list.setAttribute('role', 'listbox');
        if (!list.id) list.id = 'sedna-mention-list-' + (++ids);
        for (var k = 0; k < all.length; k++) {
            all[k].setAttribute('role', 'option');
            if (!all[k].id) all[k].id = 'sedna-mention-' + (++ids);
        }
        if (!field.hasAttribute('aria-controls')) field.setAttribute('aria-controls', list.id);
        list.hidden = false;
        open = { field: field, list: list, start: at.start, end: at.end };
        var visible = shown(list);
        var current = visible.filter(function (it) { return it.hasAttribute('data-active'); })[0];
        setActive(list, field, current || visible[0]);
    }
    function choose(item) {
        if (!open || !item) return;
        var field = open.field, start = open.start;
        var end = field.selectionStart;
        var text = '@' + item.getAttribute('data-mention-value') + ' ';
        close();
        field.setRangeText(text, start, end, 'end');
        field.dispatchEvent(new Event('input', { bubbles: true }));
    }
    function move(step) {
        var visible = shown(open.list);
        if (!visible.length) return;
        var at = -1;
        for (var i = 0; i < visible.length; i++) if (visible[i].hasAttribute('data-active')) at = i;
        setActive(open.list, open.field, visible[(at + step + visible.length) % visible.length]);
    }
    ui.mention = {
        close: close
    };
    document.addEventListener('input', function (e) {
        var field = fieldOf(e.target);
        if (field) update(field, true);
    });
    document.addEventListener('keyup', function (e) {
        if (!open || e.target !== open.field) return;
        if (/^(ArrowLeft|ArrowRight|Home|End)$/.test(e.key)) update(open.field, false);
    });
    document.addEventListener('click', function (e) {
        if (open && e.target === open.field) update(open.field, false);
    });
    window.addEventListener('keydown', function (e) {
        if (!open || e.target !== open.field || e.isComposing) return;
        var plain = !e.altKey && !e.ctrlKey && !e.metaKey && !e.shiftKey;
        if (e.key === 'ArrowDown' && plain) move(1);
        else if (e.key === 'ArrowUp' && plain) move(-1);
        else if ((e.key === 'Enter' || e.key === 'Tab') && plain) {
            choose(shown(open.list).filter(function (it) { return it.hasAttribute('data-active'); })[0]);
        } else if (e.key === 'Escape') {
            if (dismissed) dismissed.set(open.field, open.start);
            close();
        } else return;
        e.preventDefault();
        e.stopPropagation();
    }, true);
    document.addEventListener('mousedown', function (e) {
        if (open && e.target instanceof Element && open.list.contains(e.target) && e.target.closest('.menu-item')) e.preventDefault();
    });
    document.addEventListener('click', function (e) {
        if (!open || !(e.target instanceof Element)) return;
        var item = e.target.closest('.menu-item[data-mention-value]');
        if (item && open.list.contains(item) && !item.hidden) choose(item);
    });
    document.addEventListener('pointermove', function (e) {
        if (!open || !(e.target instanceof Element)) return;
        var item = e.target.closest('.menu-item[data-mention-value]');
        if (item && open.list.contains(item) && !item.hidden && !item.hasAttribute('data-active')) setActive(open.list, open.field, item);
    });
    document.addEventListener('focusout', function (e) {
        if (open && e.target === open.field) close();
    });
    document.addEventListener('scroll', function (e) {
        if (!open) return;
        var scroller = e.target === document ? document.documentElement : e.target;
        if (!scroller || scroller.nodeType !== 1 || scroller === open.field) return;
        if (open.list === scroller || open.list.contains(scroller)) return;
        if (scroller.contains(open.field)) close();
    }, true);
})(window.sednaUi);
(function (ui) {
    function words(text) {
        return text.toLowerCase().split(/\s+/).filter(function (w) { return w.length > 0; });
    }
    function links(nav) {
        return nav.querySelectorAll('.nav-scroll .nav-link');
    }
    function apply(input) {
        var nav = input.closest('.nav');
        if (!nav) return 0;
        var terms = words(input.value);
        var all = links(nav);
        var hits = 0;
        if (terms.length === 0) nav.removeAttribute('data-filtering');
        else nav.setAttribute('data-filtering', '');
        for (var i = 0; i < all.length; i++) {
            var hay = (all[i].textContent + ' ' + (all[i].getAttribute('data-keywords') || '')).toLowerCase();
            var hit = terms.every(function (t) { return hay.indexOf(t) >= 0; });
            if (hit) { hits++; all[i].removeAttribute('data-nav-miss'); }
            else all[i].setAttribute('data-nav-miss', '');
        }
        return hits;
    }
    function clear(input) {
        if (!input.value) return;
        input.value = '';
        apply(input);
    }
    function showArea(item) {
        var sidebar = item.closest('.sidebar') || document;
        var items = sidebar.querySelectorAll('[data-nav-area]');
        for (var i = 0; i < items.length; i++) {
            var isIt = items[i] === item;
            items[i].setAttribute('aria-expanded', String(isIt));
            var panel = document.getElementById(items[i].getAttribute('aria-controls') || '');
            if (panel) panel.hidden = !isIt;
        }
    }
    ui.nav = {
        filter: function (input, query) {
            if (!input) return 0;
            if (typeof query === 'string') input.value = query;
            return apply(input);
        },
        showArea: function (itemOrPanelId) {
            var item = typeof itemOrPanelId === 'string'
                ? document.querySelector('[data-nav-area][aria-controls="' + itemOrPanelId + '"]')
                : itemOrPanelId;
            if (item) showArea(item);
        },
        moveRegion: function (step) { return moveRegion(step < 0 ? -1 : 1); }
    };
    var lastY = new WeakMap();
    var SETTLE_MS = 400;
    var settledAt = new WeakMap();
    document.addEventListener('scroll', function (e) {
        var bars = document.querySelectorAll('.bottombar[data-hide-on-scroll]');
        if (bars.length === 0) return;
        var root = document.scrollingElement;
        var scroller = e.target === document ? root : e.target;
        if (!scroller || scroller.nodeType !== 1) return;
        var y = scroller.scrollTop;
        var last = lastY.has(scroller) ? lastY.get(scroller) : 0;
        if ((settledAt.get(scroller) || 0) > Date.now()) { lastY.set(scroller, y); return; }
        if (y < last && y >= scroller.scrollHeight - scroller.clientHeight - 1) { lastY.set(scroller, y); return; }
        if (Math.abs(y - last) < 8) return;
        lastY.set(scroller, y);
        var away = y > last && y > 40;
        for (var i = 0; i < bars.length; i++) {
            var shared = scroller === root || (bars[i].parentElement && bars[i].parentElement.contains(scroller));
            if (!shared || bars[i].contains(scroller)) continue;
            var was = bars[i].hasAttribute('data-away');
            if (away && !was && !bars[i].contains(document.activeElement)) bars[i].setAttribute('data-away', '');
            else if (!away && was) bars[i].removeAttribute('data-away');
            else continue;
            settledAt.set(scroller, Date.now() + SETTLE_MS);
        }
    }, true);
    document.addEventListener('focusin', function (e) {
        var bar = e.target.closest && e.target.closest('.bottombar[data-away]');
        if (bar) bar.removeAttribute('data-away');
    });
    document.addEventListener('input', function (e) {
        if (e.target.matches && e.target.matches('[data-nav-filter]')) apply(e.target);
    });
    document.addEventListener('keydown', function (e) {
        var input = e.target.closest && e.target.closest('[data-nav-filter]');
        if (!input) return;
        if (e.key === 'Escape' && input.value) {
            e.preventDefault();
            clear(input);
        } else if (e.key === 'Enter') {
            var nav = input.closest('.nav');
            var first = nav && nav.querySelector('.nav-scroll .nav-link:not([data-nav-miss])');
            if (first && nav.hasAttribute('data-filtering')) {
                e.preventDefault();
                first.click();
            }
        }
    });
    document.addEventListener('click', function (e) {
        var area = e.target.closest('[data-nav-area]');
        if (area) {
            e.preventDefault();
            showArea(area);
            return;
        }
        var link = e.target.closest('.nav[data-filtering] .nav-scroll .nav-link');
        if (link) {
            var input = link.closest('.nav').querySelector('[data-nav-filter]');
            if (input) setTimeout(function () { clear(input); }, 0);
        }
    });
    var REGION = 'header, nav, main, aside, footer, [role="banner"], [role="navigation"], [role="main"], ' +
        '[role="complementary"], [role="contentinfo"], [role="search"], [role="region"], ' +
        '.topbar, .sidebar, .bottombar, dialog[open]';
    var TABBABLE = 'a[href], area[href], button, input:not([type="hidden"]), select, textarea, summary, ' +
        'iframe, audio[controls], video[controls], [contenteditable=""], [contenteditable="true"], [tabindex]';
    function shown(el) {
        if (el.closest('[inert], [aria-hidden="true"]')) return false;
        if (el.checkVisibility && !el.checkVisibility({ visibilityProperty: true })) return false;
        if (!el.getClientRects().length) return false;
        var r = el.getBoundingClientRect();
        return r.right > 0 && r.left < window.innerWidth;
    }
    function landmark(el) {
        if (el.tagName === 'DIALOG') return !el.matches(':modal');
        if (!el.hasAttribute('role') && (el.tagName === 'HEADER' || el.tagName === 'FOOTER') &&
            !el.matches('.topbar, .bottombar') &&
            el.parentElement && el.parentElement.closest('article, aside, main, nav, section')) return false;
        return true;
    }
    function regions() {
        var all = Array.prototype.filter.call(document.querySelectorAll(REGION), landmark);
        return all.filter(function (el) {
            for (var i = 0; i < all.length; i++) {
                if (all[i] !== el && all[i].contains(el) && el.tagName !== 'DIALOG') return false;
            }
            return shown(el);
        });
    }
    function firstTabbable(region) {
        var all = region.querySelectorAll(TABBABLE);
        for (var i = 0; i < all.length; i++) {
            var el = all[i];
            if (el.tabIndex < 0 || el.disabled) continue;
            if (!shown(el)) continue;
            var closed = el.closest('details:not([open])');
            if (closed && el.tagName !== 'SUMMARY' && !el.closest('summary')) continue;
            return el;
        }
        return null;
    }
    function enterRegion(region) {
        var target = firstTabbable(region);
        if (target) { target.focus(); return; }
        if (!region.hasAttribute('tabindex')) {
            region.setAttribute('tabindex', '-1');
            region.addEventListener('blur', function lose() {
                region.removeEventListener('blur', lose);
                region.removeAttribute('tabindex');
            });
        }
        region.focus();
    }
    function moveRegion(step) {
        if (document.querySelector('dialog:modal')) return null;
        var list = regions();
        if (!list.length) return null;
        var active = document.activeElement;
        var at = -1;
        for (var i = 0; i < list.length; i++) if (active && list[i].contains(active)) at = i;
        var next = at < 0
            ? (step > 0 ? 0 : list.length - 1)
            : (at + (step > 0 ? 1 : -1) + list.length) % list.length;
        enterRegion(list[next]);
        return list[next];
    }
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'F6' || e.defaultPrevented || e.ctrlKey || e.altKey || e.metaKey) return;
        if (moveRegion(e.shiftKey ? -1 : 1)) e.preventDefault();
    });
})(window.sednaUi);
(function (ui) {
    function tabsIn(list) {
        return Array.prototype.filter.call(
            list.querySelectorAll('[role="tab"]'),
            function (t) { return !t.disabled && t.getAttribute('aria-disabled') !== 'true'; });
    }
    function select(list, tab) {
        var all = list.querySelectorAll('[role="tab"]');
        for (var i = 0; i < all.length; i++) {
            var isIt = all[i] === tab;
            all[i].setAttribute('aria-selected', String(isIt));
            all[i].tabIndex = isIt ? 0 : -1;
            var panel = document.getElementById(all[i].getAttribute('aria-controls') || '');
            if (panel) panel.hidden = !isIt;
        }
    }
    function managed(list) { return list.getAttribute('data-tabs') === 'managed'; }
    ui.tabs = {
        select: function (tabOrPanelId) {
            var tab = tabOrPanelId;
            if (typeof tabOrPanelId === 'string') {
                var own = document.getElementById(tabOrPanelId);
                tab = own && own.getAttribute('role') === 'tab'
                    ? own
                    : document.querySelector('[role="tab"][aria-controls="' + CSS.escape(tabOrPanelId) + '"]');
            }
            var list = tab && tab.closest('[data-tabs]');
            if (!list) return;
            if (managed(list)) tab.click();
            else select(list, tab);
        }
    };
    document.addEventListener('click', function (e) {
        var tab = e.target.closest('[data-tabs] [role="tab"]');
        if (!tab || tab.disabled) return;
        var list = tab.closest('[data-tabs]');
        if (managed(list)) return;
        e.preventDefault();
        select(list, tab);
    });
    document.addEventListener('keydown', function (e) {
        var tab = e.target.closest('[data-tabs] [role="tab"]');
        if (!tab) return;
        var list = tab.closest('[data-tabs]');
        var tabs = tabsIn(list);
        var at = tabs.indexOf(tab);
        if (at < 0) return;
        var rtl = getComputedStyle(list).direction === 'rtl';
        var next = rtl ? 'ArrowLeft' : 'ArrowRight', prev = rtl ? 'ArrowRight' : 'ArrowLeft';
        var to = -1;
        if (e.key === next || e.key === 'ArrowDown') to = (at + 1) % tabs.length;
        else if (e.key === prev || e.key === 'ArrowUp') to = (at - 1 + tabs.length) % tabs.length;
        else if (e.key === 'Home') to = 0;
        else if (e.key === 'End') to = tabs.length - 1;
        else return;
        e.preventDefault();
        tabs[to].focus();
        if (managed(list)) tabs[to].click();
        else select(list, tabs[to]);
    });
})(window.sednaUi);
(function (ui) {
    var score = ui._.score;
    var commands = [];
    var dialog = null;
    var input = null;
    var list = null;
    var shown = [];      // the currently visible commands, in ranked order
    var at = 0;          // index into shown
    var wired = [];      // dialogs whose listeners are attached
    var warned = false;
    var stack = [];
    function here() { return stack.length ? stack[stack.length - 1].id : null; }
    function on(c, page) { return (c.page || null) === (page || null); }
    function rank(query, page) {
        var out = [];
        for (var i = 0; i < commands.length; i++) {
            var c = commands[i];
            if (!on(c, page)) continue;
            var best = score(query, c.label, 0);
            if (c.keywords) {
                var k = score(query, c.keywords, 200);
                if (k > best) best = k;
            }
            if (best >= 0) out.push({ c: c, s: best, i: i });
        }
        out.sort(function (a, b) { return b.s - a.s || a.i - b.i; });
        return out.map(function (r) { return r.c; });
    }
    var fill = ui._.fill;
    function template(name) {
        return dialog.querySelector('template[data-palette-' + name + ']');
    }
    function busy() { return dialog.getAttribute('aria-busy') === 'true'; }
    function render(query, keep) {
        var page = here();
        var ranked = rank(query, page);
        if (query) {
            ranked = ranked.filter(function (c) { return !c.pinned; });
            shown = ranked.concat(commands.filter(function (c) { return c.pinned && on(c, page); }));
        } else {
            shown = ranked;
        }
        at = 0;
        list.textContent = '';
        trail();
        if (!ranked.length && query && !busy()) {
            var empty = fill(template('empty'), { query: query });
            if (empty) list.appendChild(empty);
        }
        var lastGroup = null;
        shown.forEach(function (c, i) {
            if (!query && c.group && c.group !== lastGroup) {
                if (lastGroup !== null) {
                    var rule = fill(template('separator'), {});
                    if (rule) list.appendChild(rule);
                }
                var head = fill(template('group'), { group: c.group });
                if (head) list.appendChild(head);
                lastGroup = c.group;
            }
            var row = fill(template('item'), { label: c.label, note: c.note });
            if (!row) return;
            var icon = row.querySelector('[data-icon]');
            if (icon && c.icon) c.icon.split(/\s+/).forEach(function (k) { if (k) icon.classList.add(k); });
            else if (icon) icon.remove();
            var option = row.matches('[role="option"]') ? row : row.querySelector('[role="option"]') || row;
            option.setAttribute('role', 'option');
            option.id = list.id + '-' + i;
            option.setAttribute('aria-selected', String(i === 0));
            option.addEventListener('click', function () { run(i); });
            list.appendChild(row);
        });
        if (busy()) {
            var loading = fill(template('loading'), {});
            if (loading) list.appendChild(loading);
        }
        if (shown.length && keep > 0) highlight(Math.min(keep, items().length - 1));
        else if (shown.length) input.setAttribute('aria-activedescendant', list.id + '-0');
        else input.removeAttribute('aria-activedescendant');
    }
    function trail() {
        var path = dialog.querySelector('[data-palette-path]');
        if (path) path.hidden = !stack.length;
        var holder = dialog.querySelector('[data-palette-trail]');
        if (!holder) return;
        holder.textContent = '';
        stack.forEach(function (p) {
            var crumb = fill(template('crumb'), { label: p.label });
            if (crumb) holder.appendChild(crumb);
        });
    }
    function enter(c) {
        stack.push({ id: c.opens, label: c.label, query: input.value, at: at });
        input.value = '';
        if (typeof c.run === 'function') c.run();
        render('');
        input.focus();
    }
    function back() {
        if (!stack.length) return false;
        var was = stack.pop();
        input.value = was.query;
        render(was.query, was.at);
        input.focus();
        return true;
    }
    function items() { return list.querySelectorAll('[role="option"]'); }
    function highlight(next) {
        var all = items();
        if (!all.length) return;
        at = (next + all.length) % all.length;
        for (var i = 0; i < all.length; i++) {
            all[i].setAttribute('aria-selected', String(i === at));
        }
        input.setAttribute('aria-activedescendant', all[at].id);
        all[at].scrollIntoView({ block: 'nearest' });
    }
    function run(i) {
        var c = shown[i];
        if (c && c.opens) { enter(c); return; }
        close();
        if (!c) return;
        if (typeof c.run === 'function') c.run();
        else if (ui._.safeHref(c.href)) go(ui._.safeHref(c.href));
    }
    function go(href) {
        var a = document.createElement('a');
        a.href = href;
        document.body.appendChild(a);
        a.click();
        a.remove();
    }
    function adopt() {
        var found = document.querySelector('dialog[data-palette]');
        if (!found) {
            if (!warned) console.warn('sednaUi.palette: no <dialog data-palette> in the document. The palette is app markup — see the Command palette page.');
            warned = true;
            return false;
        }
        dialog = found;
        input = dialog.querySelector('.palette-input, input');
        list = dialog.querySelector('.palette-list, ul');
        if (!input || !list) {
            console.warn('sednaUi.palette: the palette needs an input and a list.');
            return false;
        }
        if (!list.id) list.id = 'sedna-palette-list';
        list.setAttribute('role', 'listbox');
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-expanded', 'true');
        input.setAttribute('aria-controls', list.id);
        input.setAttribute('autocomplete', 'off');
        if (wired.indexOf(dialog) >= 0) return true;
        wired.push(dialog);
        var own = dialog;
        input.addEventListener('input', function () { render(input.value); });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); highlight(at + 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(at - 1); }
            else if (e.key === 'Home') { e.preventDefault(); highlight(0); }
            else if (e.key === 'End') { e.preventDefault(); highlight(items().length - 1); }
            else if (e.key === 'Enter') { e.preventDefault(); if (items().length) run(at); }
            else if (e.key === 'Backspace' && !input.value && stack.length) { e.preventDefault(); back(); }
        });
        own.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && stack.length) { e.preventDefault(); back(); }
        });
        own.addEventListener('click', function (e) {
            if (e.target === own) { close(); return; }
            if (e.target.closest && e.target.closest('[data-palette-back]')) back();
        });
        new MutationObserver(function () {
            if (own === dialog && own.open) render(input.value);
        }).observe(own, { attributes: true, attributeFilter: ['aria-busy'] });
        return true;
    }
    function close() {
        if (dialog && dialog.open) dialog.close();
    }
    ui.palette = {
        register: function (list_) {
            commands = Array.isArray(list_) ? list_.slice() : [];
            if (dialog && dialog.open) render(input.value);
        },
        open: function () {
            if (!adopt()) return false;
            if (dialog.open) return true;
            stack = [];
            input.value = '';
            render('');
            dialog.showModal();
            input.focus();
            return true;
        },
        close: close,
        back: function () { return !!(dialog && dialog.open) && back(); },
        rank: rank
    };
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'k' && e.key !== 'K') return;
        if (!e.ctrlKey && !e.metaKey) return;
        if (e.defaultPrevented) return;
        if (!commands.length) return;      // nothing registered: leave the browser's own binding alone
        if (!document.querySelector('dialog[data-palette]')) return;
        e.preventDefault();
        ui.palette.open();
    });
})(window.sednaUi);
(function (ui) {
    var score = ui._.score;
    var MAX = 8;
    var items = [];
    var panel = null;      // the app's [data-search-panel] for the active box
    var list = null;
    var warned = false;
    var fill = ui._.fill;
    var box = null;        // the .search the panel is currently anchored to
    var input = null;
    var shown = [];
    var at = 0;
    var total = 0;         // matches before the cut, for the "+N more" line
    function best(item, term) {
        var s = score(term, item.title, 0);
        var fields = [[item.code, 150], [item.keywords, 250], [item.meta, 350]];
        for (var i = 0; i < fields.length; i++) {
            var text = fields[i][0];
            if (!text || text.toLowerCase().indexOf(term) < 0) continue;
            var other = score(term, text, fields[i][1]);
            if (other > s) s = other;
        }
        return s;
    }
    function rank(query) {
        var q = (query || '').trim().toLowerCase();
        if (!q) return [];
        var terms = q.split(/\s+/);
        var out = [];
        for (var i = 0; i < items.length; i++) {
            var s = best(items[i], q);
            if (terms.length > 1) {
                var sum = 0, all = true;
                for (var t = 0; t < terms.length; t++) {
                    var each = best(items[i], terms[t]);
                    if (each < 0) { all = false; break; }
                    sum += each;
                }
                if (all) {
                    var mean = sum / terms.length - 50;
                    if (mean > s) s = mean;
                }
            }
            if (s >= 0) out.push({ item: items[i], s: s, i: i });
        }
        out.sort(function (a, b) { return b.s - a.s || a.i - b.i; });
        return out.map(function (r) { return r.item; });
    }
    function panelFor(target) {
        var named = target.getAttribute('data-search');
        var found = (named && document.getElementById(named)) ||
            (target.closest('.search') || document).querySelector('[data-search-panel]') ||
            document.querySelector('[data-search-panel]');
        if (!found) {
            if (!warned) console.warn('sednaUi.search: no [data-search-panel] in the document. The results panel is app markup — see the Topbar page.');
            warned = true;
            return null;
        }
        var inner = found.querySelector('[data-search-list]') || found;
        if (!inner.id) inner.id = 'sedna-search-list';
        inner.setAttribute('role', 'listbox');
        if (!found.hasAttribute('data-search-wired')) {
            found.setAttribute('data-search-wired', '');
            found.addEventListener('mousedown', function (e) { e.preventDefault(); });
        }
        panel = found;
        list = inner;
        return found;
    }
    function template(name) {
        return panel.querySelector('template[data-search-' + name + ']');
    }
    function place() {
        if (!box || !panel || panel.hidden) return;
        if (panel.classList.contains('search-panel--anchored')) return;
        var r = box.getBoundingClientRect();
        panel.style.top = (r.bottom + 6) + 'px';
        panel.style.left = r.left + 'px';
        panel.style.width = r.width + 'px';
    }
    function row(item, i) {
        var node = fill(template('item'), {
            title: item.title, code: item.code, meta: item.meta, tag: item.tag
        });
        if (!node) return null;
        var href = ui._.safeHref(item.href);
        if (href) node.setAttribute('href', href);
        else node.removeAttribute('href');
        var tag = node.querySelector('.search-tag');
        if (tag) tag.classList.toggle('search-tag--warn', item.tone === 'warn');
        var meta = node.querySelector('.search-item-meta');
        if (meta && !meta.children.length && !meta.textContent.trim()) meta.remove();
        node.setAttribute('role', 'option');
        node.setAttribute('aria-selected', String(i === 0));
        node.setAttribute('tabindex', '-1');
        node.id = list.id + '-item-' + i;
        node.classList.toggle('search-item--sel', i === 0);
        node.addEventListener('click', function (e) { pick(i, e); });
        node.addEventListener('mouseenter', function () { highlight(i); });
        return node;
    }
    function render(query) {
        if (!panel) return;
        var all = rank(query);
        total = all.length;
        shown = all.slice(0, MAX);
        at = 0;
        list.textContent = '';
        if (!shown.length) {
            var empty = fill(template('empty'), { query: query });
            if (empty) list.appendChild(empty);
            input.removeAttribute('aria-activedescendant');
        } else {
            for (var i = 0; i < shown.length; i++) {
                var node = row(shown[i], i);
                if (node) list.appendChild(node);
            }
            var cut = total - shown.length;
            if (cut > 0) {
                var more = fill(template('more'), { count: cut });
                if (more) list.appendChild(more);
            }
            input.setAttribute('aria-activedescendant', list.id + '-item-0');
        }
        open();
    }
    function open() {
        if (!panel || !panel.hidden) { place(); return; }
        panel.hidden = false;
        input.setAttribute('aria-expanded', 'true');
        place();
    }
    function close() {
        if (!panel || panel.hidden) return;
        panel.hidden = true;
        if (input) {
            input.setAttribute('aria-expanded', 'false');
            input.removeAttribute('aria-activedescendant');
        }
    }
    function rows() { return list ? list.querySelectorAll('[role="option"]') : []; }
    function highlight(next) {
        var all = rows();
        if (!all.length) return;
        at = (next + all.length) % all.length;
        for (var i = 0; i < all.length; i++) {
            all[i].setAttribute('aria-selected', String(i === at));
            all[i].classList.toggle('search-item--sel', i === at);
        }
        input.setAttribute('aria-activedescendant', all[at].id);
        all[at].scrollIntoView({ block: 'nearest' });
    }
    function pick(i, e) {
        var item = shown[i];
        var node = (!e && item && item.href) ? document.getElementById(list.id + '-item-' + i) : null;
        close();
        reset(input);
        if (!item) return;
        if (typeof item.run === 'function') {
            if (e) e.preventDefault();
            item.run();
            return;
        }
        if (node && node.click) node.click();
    }
    function reset(target) {
        if (!target || !target.value) return;
        target.value = '';
        target.dispatchEvent(new Event('input', { bubbles: true }));
    }
    function clear(target) {
        reset(target);
        target.focus();
        close();
    }
    function inside(node) {
        if (!(node instanceof Element)) return false;
        return !!(node.closest('.search') || node.closest('.search-panel, [data-search-panel]'));
    }
    ui.search = {
        register: function (list_) {
            items = Array.isArray(list_) ? list_.slice() : [];
            close();
        },
        rank: rank,
        close: close
    };
    function adopt(target) {
        if (panel && input !== target) close();
        if (!panelFor(target)) return false;
        input = target;
        box = target.closest('.search') || target;
        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-controls', list.id);
        input.setAttribute('aria-autocomplete', 'list');
        return true;
    }
    document.addEventListener('input', function (e) {
        var target = e.target;
        if (!(target instanceof Element) || !target.matches('[data-search]')) return;
        if (!items.length) return;
        if (!adopt(target)) return;
        if (!target.value.trim()) { close(); return; }
        render(target.value);
    });
    document.addEventListener('keydown', function (e) {
        var target = e.target;
        if (!(target instanceof Element) || !target.matches('[data-search]')) return;
        if (e.key === 'Escape') {
            e.preventDefault();
            if (panel && !panel.hidden) close();
            else if (target.value) clear(target);
            return;
        }
        if (!panel || panel.hidden || !rows().length) return;
        if (e.key === 'ArrowDown') { e.preventDefault(); highlight(at + 1); }
        else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(at - 1); }
        else if (e.key === 'Home') { e.preventDefault(); highlight(0); }
        else if (e.key === 'End') { e.preventDefault(); highlight(rows().length - 1); }
        else if (e.key === 'Enter') { e.preventDefault(); pick(at, null); }
    });
    document.addEventListener('click', function (e) {
        var target = e.target;
        if (!(target instanceof Element)) return;
        var button = target.closest('.search-clear');
        if (!button) return;
        var field = button.closest('.search');
        var control = field && field.querySelector('.search-input, [data-search]');
        if (control) { e.preventDefault(); clear(control); }
    });
    document.addEventListener('focusin', function (e) {
        var target = e.target;
        if (target instanceof Element && target.matches('[data-search]')) {
            if (!items.length || !target.value.trim()) return;
            if (adopt(target)) render(target.value);
            return;
        }
        if (!inside(target)) close();
    });
    document.addEventListener('pointerdown', function (e) {
        if (!inside(e.target)) close();
    });
    window.addEventListener('resize', place);
    document.addEventListener('scroll', place, true);
})(window.sednaUi);
(function (ui) {
    var SLOP = 4;          // px a mouse or a handle press moves before it is a drag
    var TOUCH_SLOP = 10;   // px a finger may drift during the hold before it is a scroll
    var EDGE = 48;         // px from a scroller's edge where auto-scroll starts
    var SPEED = 16;        // px per frame at the edge itself
    var CONTROL = 'a[href], button, input, select, textarea, label, summary, [contenteditable], [data-resize]';
    var drag = null;           // the drag in progress
    var press = null;          // a press that may become one
    var swallowUntil = 0;      // the click that follows a pointer drag is not a click
    var swallowKeyup = false;  // nor is the keyup of the Space that picked an item up
    var cancelledPointer = null; // a pointer drag Escape ended, whose release is still to come
    function toArray(list) { return Array.prototype.slice.call(list); }
    function isZone(el) { return !!el && el.nodeType === 1 && el.hasAttribute('data-drag-zone'); }
    function zoneOf(item) { return isZone(item.parentElement) ? item.parentElement : null; }
    function itemsOf(zone) {
        return toArray(zone.children).filter(function (el) {
            return el.hasAttribute('data-drag-item');
        });
    }
    function disabled(el) { return el.getAttribute('aria-disabled') === 'true'; }
    function selected(item) {
        if (item.getAttribute('aria-selected') === 'true') return true;
        var boxes = item.querySelectorAll('input[data-drag-select]');
        for (var i = 0; i < boxes.length; i++) {
            if (boxes[i].checked && boxes[i].closest('[data-drag-item]') === item) return true;
        }
        return false;
    }
    function carriedWith(item, from) {
        if (!selected(item)) return [item];
        return itemsOf(from).filter(function (el) {
            return el === item || (selected(el) && !disabled(el));
        });
    }
    function others(zone) {
        return toArray(zone.children).filter(function (el) {
            return el.hasAttribute('data-drag-item') && drag.carried.indexOf(el) === -1;
        });
    }
    function handleOf(item) {
        var handles = item.querySelectorAll('[data-drag-handle]');
        for (var i = 0; i < handles.length; i++) {
            if (handles[i].closest('[data-drag-item]') === item) return handles[i];
        }
        return null;
    }
    function pointerOnly(handle) { return handle.getAttribute('data-drag-handle') === 'pointer'; }
    function keyOwner(item) {
        var handle = handleOf(item);
        return handle && !pointerOnly(handle) ? handle : item;
    }
    function axisOf(zone) { return zone.getAttribute('data-drag-axis') || 'y'; }
    function isSlot(zone) { return !!zone && axisOf(zone) === 'slot'; }
    function validSlot(zone) { return isSlot(zone) && zone.getAttribute('data-drop-state') === 'valid'; }
    function slotAt(x, y, from) {
        var stack = document.elementsFromPoint(x, y);
        for (var i = from || 0; i < stack.length; i++) {
            if (drag.item.contains(stack[i])) continue;
            for (var z = stack[i].closest('[data-drag-zone]'); z;
                 z = z.parentElement && z.parentElement.closest('[data-drag-zone]')) {
                if (validSlot(z)) return z;
            }
        }
        return null;
    }
    function rtl(zone) {
        try { return getComputedStyle(zone).direction === 'rtl'; } catch (e) { return false; }
    }
    function liftable(target) {
        if (!(target instanceof Element)) return null;
        var item = target.closest('[data-drag-item]');
        if (!item || !zoneOf(item) || disabled(item) || disabled(zoneOf(item))) return null;
        var handle = handleOf(item);
        if (handle) return handle.contains(target) ? item : null;
        var control = target.closest(CONTROL);
        if (control && item.contains(control) && control !== item) return null;
        return item;
    }
    function text(el) { return (el.textContent || '').replace(/\s+/g, ' ').trim(); }
    function itemName(item) { return item.getAttribute('data-drag-label') || text(item); }
    function zoneName(zone) {
        return zone.getAttribute('data-drag-label') || zone.getAttribute('aria-label')
            || zone.getAttribute('data-drag-zone');
    }
    function accepts(zone, item, from) {
        if (disabled(zone) || item.contains(zone)) return false;
        if (zone !== from) {
            var group = zone.getAttribute('data-drag-group');
            if (!group || group !== from.getAttribute('data-drag-group')) return false;
        }
        var accept = zone.getAttribute('data-drag-accept');
        if (accept === null) return true;
        var type = item.getAttribute('data-drag-type') || '';
        return !!type && accept.split(/\s+/).indexOf(type) !== -1;
    }
    function send(el, name, detail, cancelable) {
        return el.dispatchEvent(new CustomEvent(name, {
            bubbles: true, composed: true, cancelable: !!cancelable, detail: detail
        }));
    }
    function liveFor(el) {
        for (var n = el; n && n.nodeType === 1; n = n.parentElement) {
            var live = n.matches('[data-drag-live]') ? n : n.querySelector('[data-drag-live]');
            if (live) return live;
        }
        return null;
    }
    function announce(kind) {
        if (!drag || drag.mode !== 'keyboard') return;
        var live = liveFor(drag.item);
        var sentence = live && live.getAttribute('data-drag-' + kind);
        if (!sentence) return;
        var back = kind === 'cancel' || !drag.zone;
        var zone = back ? (drag.home || drag.from) : drag.zone;
        var count = others(zone).length + 1;
        var index = back ? drag.start : drag.index;
        var said = sentence.replace(/\{(item|items|zone|position|count)\}/g, function (all, name) {
            if (name === 'item') return drag.name;
            if (name === 'items') return String(drag.carried.length);
            if (name === 'zone') return zoneName(zone);
            if (name === 'position') return String(index + 1);
            return String(count);
        });
        live.textContent = live.textContent === said ? said + ' ' : said;
    }
    function clearEdge() {
        if (drag.edge) drag.edge.removeAttribute('data-drop-edge');
        drag.edge = null;
    }
    function setOver(zone) {
        if (drag.over === zone) return;
        if (drag.over) drag.over.removeAttribute('data-drop-over');
        drag.over = zone;
        if (zone) zone.setAttribute('data-drop-over', '');
    }
    function markEdge(zone, index, y) {
        clearEdge();
        var items = others(zone);
        if (!items.length) return;
        var el = items[Math.min(index, items.length - 1)];
        var edge = index < items.length ? 'before' : 'after';
        if (edge === 'before' && index > 0 && axisOf(zone) === 'grid' && y !== undefined) {
            var prev = items[index - 1].getBoundingClientRect();
            if (y <= prev.bottom && el.getBoundingClientRect().top > prev.top) {
                el = items[index - 1];
                edge = 'after';
            }
        }
        el.setAttribute('data-drop-edge', edge);
        drag.edge = el;
    }
    function target(zone, index, y) {
        drag.zone = zone;
        drag.index = index;
        setOver(zone);
        if (zone && zone.getAttribute('data-drop-state') === 'valid') markEdge(zone, index, y);
        else clearEdge();
    }
    function begin(item, mode, x, y) {
        var from = zoneOf(item);
        var all = itemsOf(from);
        var fromIndex = all.indexOf(item);
        var carried = carriedWith(item, from);
        var first = all.indexOf(carried[0]);
        var start = first - all.slice(0, first).filter(function (el) { return carried.indexOf(el) !== -1; }).length;
        var together = all.indexOf(carried[carried.length - 1]) - first === carried.length - 1;
        var detail = {
            item: item.getAttribute('data-drag-item'),
            items: carried.map(function (el) { return el.getAttribute('data-drag-item'); }),
            type: item.getAttribute('data-drag-type'),
            zone: from.getAttribute('data-drag-zone'),
            index: fromIndex,
            keyboard: mode === 'keyboard'
        };
        if (!send(item, 'sedna-dragstart', detail, true)) return false;
        var rect = item.getBoundingClientRect();
        drag = {
            item: item, mode: mode, from: from, fromIndex: fromIndex, detail: detail,
            carried: carried, start: start, together: together,
            name: itemName(item),
            hadStyle: item.hasAttribute('style'),
            grabX: x - rect.left, grabY: y - rect.top, x: x, y: y, x0: x, y0: y, tx: 0, ty: 0,
            zone: null, index: fromIndex, over: null, edge: null, zones: [], frame: 0
        };
        toArray(document.querySelectorAll('[data-drag-zone]')).forEach(function (zone) {
            var group = zone.getAttribute('data-drag-group');
            if (zone !== from && (!group || group !== from.getAttribute('data-drag-group'))) return;
            var takes = carried.every(function (el) { return accepts(zone, el, from); });
            zone.setAttribute('data-drop-state', takes ? 'valid' : 'invalid');
            drag.zones.push(zone);
        });
        drag.home = null;
        if (drag.zones.some(validSlot)) {
            var flipped = rtl(from);
            drag.home = mode === 'keyboard'
                ? slotAt(flipped ? rect.right - 4 : rect.left + 4, rect.top + Math.min(4, rect.height / 2))
                : slotAt(x, y);
        }
        item.setAttribute('data-dragging', mode);
        if (carried.length > 1) {
            item.setAttribute('data-drag-count', String(carried.length));
            carried.forEach(function (el) { if (el !== item) el.setAttribute('data-drag-carried', ''); });
        }
        if (mode === 'pointer') lift(item, rect);
        if (ui.tips && ui.tips.hide) ui.tips.hide();
        try { window.getSelection().removeAllRanges(); } catch (e) { /* nothing selected */ }
        if (mode === 'keyboard') {
            if (drag.home) target(drag.home, 0);
            else target(from, start);
            announce('pickup');
        } else {
            target(from, start, y);
            tick();
        }
        return true;
    }
    var BOX = ['--drag-x', '--drag-y', '--drag-left', '--drag-top', '--drag-width', '--drag-height'];
    function lift(item, rect) {
        drag.lifted = false;
        drag.slot = null;
        if (typeof item.showPopover !== 'function' || item.hasAttribute('popover')) return;
        if (getComputedStyle(item).display === 'table-cell') return;
        var zone = drag.from;
        var axis = axisOf(zone);
        var items = itemsOf(zone);
        var at = items.indexOf(item);
        var next = items[at + 1] || null;
        var prev = items[at - 1] || null;
        var nextBefore = next && next.getBoundingClientRect();
        var zoneBefore = zone.getBoundingClientRect();
        if (axis === 'grid' && next) {
            var column = 1;
            for (var i = at; i >= 0 && Math.abs(items[i].getBoundingClientRect().top - nextBefore.top) < 2; i--) column++;
            slot(next, 'column', '--drag-slot-column', String(column));
        }
        item.style.setProperty('--drag-left', rect.left + 'px');
        item.style.setProperty('--drag-top', rect.top + 'px');
        item.style.setProperty('--drag-width', rect.width + 'px');
        item.style.setProperty('--drag-height', rect.height + 'px');
        item.setAttribute('popover', 'manual');
        try {
            item.showPopover();
            drag.lifted = true;
        } catch (e) {
            item.removeAttribute('popover');   // not connected, or inside something inert
            clearSlot();
            return;
        }
        if (axis === 'grid') return;
        var inline = axis === 'x';
        if (next) {
            var after = next.getBoundingClientRect();
            var moved = inline ? Math.abs(nextBefore.left - after.left) : nextBefore.top - after.top;
            var own = parseFloat(getComputedStyle(next)[inline ? 'marginInlineStart' : 'marginBlockStart']) || 0;
            if (moved > 0) slot(next, 'start', '--drag-slot-size', (moved + own) + 'px');
        } else if (prev) {
            var zoneAfter = zone.getBoundingClientRect();
            var shrunk = inline ? zoneBefore.width - zoneAfter.width : zoneBefore.height - zoneAfter.height;
            var end = parseFloat(getComputedStyle(prev)[inline ? 'marginInlineEnd' : 'marginBlockEnd']) || 0;
            if (shrunk > 0) slot(prev, 'end', '--drag-slot-size', (shrunk + end) + 'px');
        }
    }
    function slot(el, kind, prop, value) {
        drag.slot = { el: el, prop: prop, hadStyle: el.hasAttribute('style') };
        el.style.setProperty(prop, value);
        el.setAttribute('data-drag-slot', kind);
    }
    function clearSlot() {
        var held = drag.slot;
        drag.slot = null;
        if (!held) return;
        held.el.removeAttribute('data-drag-slot');
        held.el.style.removeProperty(held.prop);
        if (!held.hadStyle && !held.el.getAttribute('style')) held.el.removeAttribute('style');
    }
    function finish(commit) {
        if (!drag) return;
        var d = drag;
        cancelAnimationFrame(d.frame);
        var zone = d.zone;
        var landed = commit && zone && zone.getAttribute('data-drop-state') === 'valid' && d.item.isConnected;
        var moved = landed && (zone !== d.from || !d.together || d.index !== d.start) && zone !== d.home;
        if (d.mode === 'keyboard') announce(moved ? 'drop' : 'cancel');
        clearEdge();
        setOver(null);
        d.zones.forEach(function (z) { z.removeAttribute('data-drop-state'); });
        clearSlot();
        if (d.lifted) {
            try { d.item.hidePopover(); } catch (e) { /* already closed */ }
            d.item.removeAttribute('popover');
        }
        d.item.removeAttribute('data-dragging');
        d.item.removeAttribute('data-drag-count');
        d.carried.forEach(function (el) { el.removeAttribute('data-drag-carried'); });
        BOX.forEach(function (name) { d.item.style.removeProperty(name); });
        if (!d.hadStyle && !d.item.getAttribute('style')) d.item.removeAttribute('style');
        drag = null;
        var id = d.detail.item;
        if (moved) {
            send(zone, 'sedna-drop', {
                item: id,
                items: d.detail.items,
                type: d.detail.type,
                from: d.detail.zone,
                to: zone.getAttribute('data-drag-zone'),
                index: d.index,
                fromIndex: d.fromIndex,
                keyboard: d.mode === 'keyboard',
                fromSlot: d.home ? d.home.getAttribute('data-drag-zone') : null
            });
        }
        var end = {
            item: id, items: d.detail.items, type: d.detail.type,
            zone: moved ? zone.getAttribute('data-drag-zone') : d.detail.zone,
            index: moved ? d.index : d.fromIndex,
            keyboard: d.mode === 'keyboard',
            dropped: !!moved
        };
        send(d.item.isConnected ? d.item : document, 'sedna-dragend', end);
        if (moved && d.mode === 'keyboard') refocus(id, keyOwner(d.item) !== d.item);
    }
    function refocus(id, viaHandle) {
        var observer = null;
        var stop = setTimeout(done, 3000);
        function done() {
            clearTimeout(stop);
            if (observer) observer.disconnect();
        }
        function attempt() {
            var active = document.activeElement;
            if (active && active !== document.body && active !== document.documentElement) return false;
            var items = document.querySelectorAll('[data-drag-item]');
            for (var i = 0; i < items.length; i++) {
                if (items[i].getAttribute('data-drag-item') !== id) continue;
                var el = viaHandle ? keyOwner(items[i]) : items[i];
                el.focus({ preventScroll: false });
                return document.activeElement === el;
            }
            return false;
        }
        try {
            observer = new MutationObserver(function () { if (attempt()) done(); });
            observer.observe(document.body, { childList: true, subtree: true });
        } catch (e) { done(); }
        setTimeout(function () { if (attempt()) done(); }, 0);
    }
    function zoneAt(x, y) {
        var hit = null;
        var stack = document.elementsFromPoint(x, y);
        for (var i = 0; i < stack.length; i++) {
            if (!drag.item.contains(stack[i])) { hit = stack[i]; break; }
        }
        drag.hit = hit;
        var innermost = null;
        for (var zone = hit && hit.closest('[data-drag-zone]'); zone;
             zone = zone.parentElement && zone.parentElement.closest('[data-drag-zone]')) {
            var state = zone.getAttribute('data-drop-state');
            if (!state) continue;
            if (state === 'valid') return zone;
            innermost = innermost || zone;
        }
        if (innermost && drag.zones.some(validSlot)) return slotAt(x, y, i + 1) || innermost;
        return innermost;
    }
    function indexAt(zone, x, y) {
        var items = others(zone);
        var axis = axisOf(zone);
        var flip = rtl(zone);
        for (var i = 0; i < items.length; i++) {
            var r = items[i].getBoundingClientRect();
            var midX = r.left + r.width / 2;
            var ahead = flip ? x > midX : x < midX;
            var before = axis === 'x' ? ahead
                : axis === 'grid' ? y < r.top || (y <= r.bottom && ahead)
                : y < r.top + r.height / 2;
            if (before) return i;
        }
        return items.length;
    }
    function speed(p, lo, hi) {
        if (hi - lo < EDGE * 3) return 0;
        if (p < lo - EDGE || p > hi + EDGE) return 0;
        if (p < lo + EDGE) return -Math.ceil(SPEED * Math.min(1, (lo + EDGE - p) / EDGE));
        if (p > hi - EDGE) return Math.ceil(SPEED * Math.min(1, (p - (hi - EDGE)) / EDGE));
        return 0;
    }
    function scrollable(el, axis) {
        var cs = getComputedStyle(el);
        var overflow = axis === 'y' ? cs.overflowY : cs.overflowX;
        if (!/(auto|scroll|overlay)/.test(overflow)) return false;
        return axis === 'y' ? el.scrollHeight > el.clientHeight : el.scrollWidth > el.clientWidth;
    }
    function tryScroll(el, x, y) {
        var r = el.getBoundingClientRect();
        var dy = x >= r.left && x <= r.right && scrollable(el, 'y') ? speed(y, r.top, r.bottom) : 0;
        var dx = y >= r.top && y <= r.bottom && scrollable(el, 'x') ? speed(x, r.left, r.right) : 0;
        if (!dx && !dy) return false;
        var top = el.scrollTop, left = el.scrollLeft;
        el.scrollTop += dy;
        el.scrollLeft += dx;
        return el.scrollTop !== top || el.scrollLeft !== left;
    }
    function autoScroll(x, y) {
        if (!drag.travelled) {
            if (Math.max(Math.abs(x - drag.x0), Math.abs(y - drag.y0)) < EDGE / 3) return;
            drag.travelled = true;
        }
        var seen = [];
        var chains = [drag.hit, drag.over];
        for (var c = 0; c < chains.length; c++) {
            for (var n = chains[c]; n && n !== document.body && n !== document.documentElement; n = n.parentElement) {
                if (seen.indexOf(n) !== -1) continue;
                seen.push(n);
                if (tryScroll(n, x, y)) return;
            }
        }
        var root = document.scrollingElement || document.documentElement;
        var dy = speed(y, 0, window.innerHeight);
        var dx = speed(x, 0, window.innerWidth);
        if (dy || dx) root.scrollBy ? window.scrollBy(dx, dy) : (root.scrollTop += dy);
    }
    function update() {
        var d = drag;
        if (d.lifted) {
            d.tx = Math.round(d.x - d.x0);
            d.ty = Math.round(d.y - d.y0);
        } else {
            var r = d.item.getBoundingClientRect();
            d.tx = Math.round(d.x - d.grabX - (r.left - d.tx));
            d.ty = Math.round(d.y - d.grabY - (r.top - d.ty));
        }
        d.item.style.setProperty('--drag-x', d.tx + 'px');
        d.item.style.setProperty('--drag-y', d.ty + 'px');
        var zone = zoneAt(d.x, d.y);
        var index = zone && zone.getAttribute('data-drop-state') === 'valid'
            ? indexAt(zone, d.x, d.y) : 0;
        if (zone !== d.zone || index !== d.index || !d.edge) target(zone, index, d.y);
    }
    function tick() {
        if (!drag || drag.mode !== 'pointer') return;
        if (!drag.item.isConnected) { finish(false); return; }
        update();
        autoScroll(drag.x, drag.y);
        drag.frame = requestAnimationFrame(tick);
    }
    function clearPress() {
        if (!press) return;
        clearTimeout(press.timer);
        press = null;
    }
    document.addEventListener('pointerdown', function (e) {
        if (drag || press || !e.isPrimary) return;
        if (e.pointerType === 'mouse' && e.button !== 0) return;
        var item = liftable(e.target);
        if (!item) return;
        var onHandle = !!handleOf(item);
        press = { item: item, id: e.pointerId, x: e.clientX, y: e.clientY, touch: e.pointerType === 'touch', handle: onHandle, timer: 0 };
        if (press.touch && !onHandle) {
            press.timer = setTimeout(function () {
                if (!press) return;
                var p = press;
                press = null;
                if (begin(p.item, 'pointer', p.x, p.y)) drag.id = p.id;
            }, ui._.hold);
            return;
        }
        e.preventDefault();
    });
    document.addEventListener('pointermove', function (e) {
        if (drag && drag.mode === 'pointer') {
            drag.x = e.clientX;
            drag.y = e.clientY;
            return;
        }
        if (!press || e.pointerId !== press.id) return;
        var moved = Math.max(Math.abs(e.clientX - press.x), Math.abs(e.clientY - press.y));
        if (press.touch && !press.handle) {
            if (moved > TOUCH_SLOP) clearPress();
            return;
        }
        if (moved <= SLOP) return;
        var p = press;
        press = null;
        if (begin(p.item, 'pointer', p.x, p.y)) {
            drag.id = p.id;
            drag.x = e.clientX;
            drag.y = e.clientY;
        }
    });
    function release(e, commit) {
        if (press && e.pointerId === press.id) clearPress();
        if (cancelledPointer === e.pointerId) {
            cancelledPointer = null;
            swallowUntil = e.timeStamp + 500;
        }
        if (!drag || drag.mode !== 'pointer' || e.pointerId !== drag.id) return;
        if (commit) {
            drag.x = e.clientX;
            drag.y = e.clientY;
            update();
        }
        swallowUntil = e.timeStamp + 500;
        finish(commit);
    }
    document.addEventListener('pointerup', function (e) { release(e, true); });
    document.addEventListener('pointercancel', function (e) { release(e, false); });
    document.addEventListener('touchmove', function (e) {
        if ((drag && drag.mode === 'pointer') || (press && press.handle)) e.preventDefault();
    }, { passive: false });
    document.addEventListener('contextmenu', function (e) {
        if (press || (drag && drag.mode === 'pointer')) e.preventDefault();
    });
    document.addEventListener('click', function (e) {
        if (!swallowUntil || e.timeStamp > swallowUntil) return;
        swallowUntil = 0;
        e.preventDefault();
        e.stopPropagation();
    }, true);
    document.addEventListener('selectstart', function (e) {
        if (drag || press) e.preventDefault();
    });
    function reachable() {
        return drag.zones.filter(function (z) { return z.getAttribute('data-drop-state') === 'valid'; });
    }
    function columns(zone) {
        var items = itemsOf(zone);
        if (!items.length) return 1;
        var top = items[0].getBoundingClientRect().top;
        var n = 0;
        while (n < items.length && Math.abs(items[n].getBoundingClientRect().top - top) < 2) n++;
        return Math.max(1, n);
    }
    function slotToward(zone, key) {
        var r = zone.getBoundingClientRect();
        var cx = r.left + r.width / 2, cy = r.top + r.height / 2;
        var across = key === 'ArrowLeft' || key === 'ArrowRight';
        var sign = key === 'ArrowLeft' || key === 'ArrowUp' ? -1 : 1;
        var best = null, bestDistance = Infinity;
        reachable().forEach(function (z) {
            if (z === zone || !isSlot(z)) return;
            var c = z.getBoundingClientRect();
            var inLine = across ? c.top < r.bottom - 1 && c.bottom > r.top + 1 : c.left < r.right - 1 && c.right > r.left + 1;
            if (!inLine) return;
            var distance = (across ? c.left + c.width / 2 - cx : c.top + c.height / 2 - cy) * sign;
            if (distance > 1 && distance < bestDistance) { best = z; bestDistance = distance; }
        });
        return best;
    }
    function move(key) {
        var zone = drag.zone && drag.zone.getAttribute('data-drop-state') === 'valid' ? drag.zone : drag.from;
        if (isSlot(zone)) {
            var next = /^Arrow/.test(key) ? slotToward(zone, key) : null;
            if (!next) return;
            target(next, 0);
            try { next.scrollIntoView({ block: 'nearest', inline: 'nearest' }); } catch (e) { /* old engine */ }
            announce('move');
            return;
        }
        var max = others(zone).length;
        var axis = axisOf(zone);
        var flip = rtl(zone);
        var back = flip ? 'ArrowRight' : 'ArrowLeft';
        var fwd = flip ? 'ArrowLeft' : 'ArrowRight';
        var index = drag.index;
        var step = 0;      // along the list
        var hop = 0;       // to another zone
        if (key === 'Home') index = 0;
        else if (key === 'End') index = max;
        else if (key === 'PageUp') hop = -1;
        else if (key === 'PageDown') hop = 1;
        else if (axis === 'y') {
            if (key === 'ArrowUp') step = -1;
            else if (key === 'ArrowDown') step = 1;
            else if (key === back) hop = -1;
            else if (key === fwd) hop = 1;
        } else if (axis === 'x') {
            if (key === back) step = -1;
            else if (key === fwd) step = 1;
            else if (key === 'ArrowUp') hop = -1;
            else if (key === 'ArrowDown') hop = 1;
        } else {
            if (key === back) step = -1;
            else if (key === fwd) step = 1;
            else if (key === 'ArrowUp') step = -columns(zone);
            else if (key === 'ArrowDown') step = columns(zone);
        }
        if (hop) {
            var zones = reachable();
            var next = zones[zones.indexOf(zone) + hop];
            if (!next) return;
            zone = next;
            index = Math.min(index, others(zone).length);
        } else {
            index = Math.max(0, Math.min(max, index + step));
        }
        target(zone, index);
        var shown = drag.edge || zone;
        try { shown.scrollIntoView({ block: 'nearest', inline: 'nearest' }); } catch (e) { /* old engine */ }
        announce('move');
    }
    function keyTarget(el) {
        if (!(el instanceof Element)) return null;
        var item = el.closest('[data-drag-item]');
        if (!item || !zoneOf(item) || disabled(item) || disabled(zoneOf(item))) return null;
        return el === keyOwner(item) ? item : null;
    }
    var MOVES = ['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Home', 'End', 'PageUp', 'PageDown'];
    document.addEventListener('keydown', function (e) {
        if (drag && drag.mode === 'keyboard') {
            if (e.key === 'Escape') { e.preventDefault(); finish(false); }
            else if (e.key === ' ' || e.key === 'Enter') {
                e.preventDefault();
                swallowKeyup = e.key === ' ';
                finish(true);
            }
            else if (MOVES.indexOf(e.key) !== -1) { e.preventDefault(); move(e.key); }
            return;
        }
        if (drag) {
            if (e.key === 'Escape') {
                e.preventDefault();
                cancelledPointer = drag.id;
                finish(false);
            }
            return;
        }
        if ((e.key !== ' ' && e.key !== 'Enter') || e.repeat || e.altKey || e.ctrlKey || e.metaKey || e.defaultPrevented) return;
        var item = keyTarget(e.target);
        if (!item) return;
        e.preventDefault();
        swallowKeyup = e.key === ' ';
        begin(item, 'keyboard', 0, 0);
    });
    document.addEventListener('keyup', function (e) {
        if (!swallowKeyup || e.key !== ' ') return;
        swallowKeyup = false;
        e.preventDefault();
    }, true);
    document.addEventListener('focusout', function (e) {
        if (!drag || drag.mode !== 'keyboard') return;
        var owner = keyOwner(drag.item);
        if (e.target === owner && !owner.contains(e.relatedTarget)) finish(false);
    });
    ui.drag = {
        cancel: function () { finish(false); },
        active: function () { return drag ? drag.detail.item : null; }
    };
})(window.sednaUi);
(function (ui) {
    var DEPTH = '_drDropDepth';
    var OVER = 'dropzone--over';
    function zoneOf(target) {
        return target instanceof Element ? target.closest('.dropzone[data-dropzone]:not([data-dropzone="page"])') : null;
    }
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
    function deliver(input, list) {
        if (!input || !list || !list.length) return;
        try {
            var transfer = new DataTransfer();
            var many = input.multiple ? list.length : Math.min(1, list.length);
            for (var i = 0; i < many; i++) transfer.items.add(list[i]);
            input.files = transfer.files;
        } catch (err) {
            return;    // no DataTransfer constructor: the drop simply does nothing
        }
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
    document.addEventListener('dragenter', function (e) {
        if (!pageZone() || !carriesFiles(e)) return;
        e.preventDefault();
        pageDepth++;
        setPage(true);
    });
    document.addEventListener('dragover', function (e) {
        if (!pageZone() || !carriesFiles(e) || zoneOf(e.target)) return;
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
(function (ui) {
    function parts(handle) {
        var on = handle.getAttribute('data-resize-on');
        var target = on ? document.getElementById(on) : handle.parentElement;
        if (!target) return null;
        var sel = handle.getAttribute('data-resize-measure');
        var measured = sel ? target.querySelector(sel) : target;
        if (!measured) return null;
        var block = handle.getAttribute('aria-orientation') !== 'vertical';
        return {
            target: target, measured: measured, block: block,
            property: handle.getAttribute('data-resize'), key: handle.getAttribute('data-resize-key')
        };
    }
    function size(p) {
        var r = p.measured.getBoundingClientRect();
        return p.block ? r.height : r.width;
    }
    function sign(handle, p) {
        var m = p.measured.getBoundingClientRect(), h = handle.getBoundingClientRect();
        if (p.block) return (m.top + m.height / 2) <= (h.top + h.height / 2) ? 1 : -1;
        var before = (m.left + m.width / 2) <= (h.left + h.width / 2);
        return before ? 1 : -1;
    }
    function bounds(handle) {
        var min = parseFloat(handle.getAttribute('aria-valuemin'));
        var max = parseFloat(handle.getAttribute('aria-valuemax'));
        if (isNaN(min) || isNaN(max)) {
            var cs = getComputedStyle(handle);
            if (isNaN(min)) min = parseFloat(cs.getPropertyValue('--resize-min'));
            if (isNaN(max)) max = parseFloat(cs.getPropertyValue('--resize-max'));
        }
        return { min: isNaN(min) ? 48 : min, max: isNaN(max) ? Infinity : max };
    }
    function stepOf(handle) {
        var s = parseFloat(handle.getAttribute('data-resize-step'));
        return isNaN(s) || s <= 0 ? 8 : s;
    }
    function apply(handle, p, value) {
        var b = bounds(handle), step = stepOf(handle);
        value = Math.min(b.max, Math.max(b.min, Math.round(value / step) * step));
        p.target.style.setProperty(p.property, value + 'px');
        handle.setAttribute('aria-valuenow', String(value));
        return value;
    }
    function done(p, value, folded) {
        p.target.dispatchEvent(new CustomEvent('sedna-resize', {
            bubbles: true,
            detail: { property: p.property, value: value, folded: !!folded, key: p.key }
        }));
    }
    function foldAt(handle) {
        var f = parseFloat(handle.getAttribute('data-resize-fold'));
        return isNaN(f) ? null : f;
    }
    function restore(handle, p, before, now) {
        if (before) p.target.style.setProperty(p.property, before);
        else p.target.style.removeProperty(p.property);
        if (now === null) handle.removeAttribute('aria-valuenow');
        else handle.setAttribute('aria-valuenow', now);
    }
    function fold(handle, p, before, now) {
        restore(handle, p, before, now);
        var value = parseFloat(before);
        done(p, isNaN(value) ? null : value, true);
    }
    function reset(handle) {
        var p = parts(handle);
        if (!p) return;
        p.target.style.removeProperty(p.property);
        handle.removeAttribute('aria-valuenow');
        done(p, null);
    }
    ui.resize = { reset: reset };
    document.addEventListener('pointerdown', function (e) {
        if (e.button !== 0) return;
        var handle = e.target instanceof Element ? e.target.closest('[data-resize]') : null;
        if (!handle) return;
        var p = parts(handle);
        if (!p) return;
        e.preventDefault();
        var start = size(p), dir = sign(handle, p), origin = p.block ? e.clientY : e.clientX;
        var value = start, folding = false, threshold = foldAt(handle);
        var before = p.target.style.getPropertyValue(p.property), now = handle.getAttribute('aria-valuenow');
        try { handle.setPointerCapture(e.pointerId); } catch (err) { /* already released */ }
        handle.setAttribute('data-resizing', '');
        function move(ev) {
            var at = p.block ? ev.clientY : ev.clientX;
            var wanted = start + (at - origin) * dir;
            folding = threshold !== null && wanted < threshold;
            if (folding) handle.setAttribute('data-resize-folding', '');
            else handle.removeAttribute('data-resize-folding');
            value = apply(handle, p, wanted);
        }
        function up(ev) {
            handle.removeEventListener('pointermove', move);
            handle.removeEventListener('pointerup', up);
            handle.removeEventListener('pointercancel', up);
            handle.removeAttribute('data-resizing');
            handle.removeAttribute('data-resize-folding');
            if (folding && ev.type === 'pointerup') fold(handle, p, before, now);
            else if (folding) restore(handle, p, before, now);
            else if (value !== start) done(p, value);
        }
        handle.addEventListener('pointermove', move);
        handle.addEventListener('pointerup', up);
        handle.addEventListener('pointercancel', up);
    });
    document.addEventListener('dblclick', function (e) {
        var handle = e.target instanceof Element ? e.target.closest('[data-resize]') : null;
        if (handle) reset(handle);
    });
    function arrow(handle, key) {
        var block = handle.getAttribute('aria-orientation') !== 'vertical';
        var forward = block ? { ArrowDown: 1, ArrowUp: -1 } : { ArrowRight: 1, ArrowLeft: -1 };
        return forward[key] || 0;
    }
    function press(handle, key, steps) {
        var p = parts(handle);
        if (!p) return false;
        if (key === 'Home') { reset(handle); return true; }
        var by = arrow(handle, key);
        if (!by) return false;
        var current = size(p), wanted = current + by * sign(handle, p) * stepOf(handle) * steps;
        if (foldAt(handle) !== null && wanted < current && current <= bounds(handle).min + 0.5) {
            fold(handle, p, p.target.style.getPropertyValue(p.property), handle.getAttribute('aria-valuenow'));
            return true;
        }
        done(p, apply(handle, p, wanted));
        return true;
    }
    function ownGrip(el) {
        for (var c = el.firstElementChild; c; c = c.nextElementSibling) {
            if (c.hasAttribute('data-resize')) return c;
        }
        return null;
    }
    document.addEventListener('keydown', function (e) {
        var el = e.target instanceof Element ? e.target : null;
        if (!el || e.ctrlKey || e.metaKey) return;
        if (el.hasAttribute('data-resize')) {
            if (e.altKey) return;
            if (press(el, e.key, e.shiftKey ? 4 : 1)) e.preventDefault();
            return;
        }
        if (!e.altKey || !e.shiftKey) return;
        var grip = ownGrip(el);
        if (grip && press(grip, e.key, 1)) e.preventDefault();
    });
    if (ui._.grid) {
        ui._.grid.keys.push(function (e, grid, cell) {
            var el = e.target;
            if (el === cell || !el.hasAttribute('data-resize') || e.altKey || e.ctrlKey || e.metaKey) return false;
            return e.key === 'Home' || arrow(el, e.key) !== 0;
        });
    }
})(window.sednaUi);
(function (ui) {
    var STATE = '_sednaFollow';
    var TOLERANCE = 4;
    var PAUSED = 'data-follow-paused';
    function atBottom(pane) {
        return pane.scrollHeight - pane.clientHeight - pane.scrollTop <= TOLERANCE;
    }
    function toBottom(pane) {
        pane.scrollTop = pane.scrollHeight;
    }
    function menuOpen(pane) {
        return !!pane.querySelector('[data-menu-toggle][aria-expanded="true"]');
    }
    function setFollowing(pane, following) {
        var state = pane[STATE];
        if (!state) return;
        var enabled = pane.hasAttribute('data-follow');
        var changed = state.following !== following;
        state.following = following;
        pane.toggleAttribute(PAUSED, enabled && !following);
        if (!changed || !enabled) return;
        try {
            pane.dispatchEvent(new CustomEvent('sedna-follow', {
                bubbles: true,
                detail: { following: following }
            }));
        } catch (e) { /* an old engine without CustomEvent: the mark still says it */ }
    }
    function bind(pane) {
        if (pane[STATE]) return;
        pane[STATE] = { following: true };
        toBottom(pane);
        pane[STATE].top = pane.scrollTop;
        pane.addEventListener('scroll', function () {
            var state = pane[STATE];
            if (pane.scrollTop === state.top) return;
            state.top = pane.scrollTop;
            setFollowing(pane, atBottom(pane));
        });
        pane.addEventListener('toggle', function (e) {
            if (e.target && e.target.tagName === 'DETAILS' && e.target.open) setFollowing(pane, atBottom(pane));
        }, true);
        var observer = new MutationObserver(function () {
            if (!pane.hasAttribute('data-follow') || !pane[STATE].following || menuOpen(pane)) return;
            toBottom(pane);
        });
        observer.observe(pane, { childList: true, subtree: true, characterData: true });
    }
    function follow(pane) {
        if (!pane) return;
        bind(pane);
        toBottom(pane);
        setFollowing(pane, true);
    }
    ui.output = {
        follow: follow,
        isFollowing: function (pane) { return !!pane && atBottom(pane); }
    };
    function paneFor(button) {
        var id = button.getAttribute('aria-controls');
        if (id) return document.getElementById(id);
        var panel = button.closest('.output-panel');
        return panel ? panel.querySelector('.output') : null;
    }
    document.addEventListener('click', function (e) {
        var button = e.target && e.target.closest ? e.target.closest('[data-output-follow]') : null;
        if (!button) return;
        var pane = paneFor(button);
        if (!pane) return;
        e.preventDefault();
        follow(pane);
    });
    function bindAll(root) {
        var panes = (root || document).querySelectorAll('.output[data-follow]');
        for (var i = 0; i < panes.length; i++) bind(panes[i]);
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { bindAll(document); });
    } else {
        bindAll(document);
    }
    new MutationObserver(function (records) {
        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (record.type === 'attributes') {
                var pane = record.target;
                if (!pane.matches || !pane.matches('.output')) continue;
                var on = pane.hasAttribute('data-follow');
                if (on && record.oldValue !== null) continue;
                if (on) follow(pane);
                else pane.removeAttribute(PAUSED);
                continue;
            }
            var added = record.addedNodes;
            for (var j = 0; j < added.length; j++) {
                if (added[j].nodeType !== 1) continue;
                if (added[j].matches && added[j].matches('.output[data-follow]')) bind(added[j]);
                bindAll(added[j]);
            }
        }
    }).observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeOldValue: true,
        attributeFilter: ['data-follow']
    });
})(window.sednaUi);
(function (ui) {
    var STATE = '_sednaWorkflow';
    var PAUSED = 'data-follow-paused';
    var READER = 1000;
    var LAND = 1000;
    function stepAtWork(canvas) {
        var running = canvas.querySelectorAll('.workflow-step[data-state="running"]');
        if (running.length) return running[running.length - 1];
        var waiting = canvas.querySelectorAll('.workflow-step[data-state="waiting"]');
        if (waiting.length) return waiting[waiting.length - 1];
        return canvas.querySelector('.workflow-step[data-state="next"]');
    }
    function nodeOf(step) {
        return step.querySelector(':scope > .workflow-node, :scope > .workflow-end') || step;
    }
    function inView(canvas, step) {
        var box = canvas.getBoundingClientRect();
        var node = nodeOf(step).getBoundingClientRect();
        var x = node.left + node.width / 2, y = node.top + node.height / 2;
        return x >= box.left && x <= box.right && y >= box.top && y <= box.bottom;
    }
    function reducedMotion() {
        return !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    }
    function setFollowing(canvas, following) {
        var state = canvas[STATE];
        if (!state) return;
        var enabled = canvas.hasAttribute('data-follow');
        var changed = state.following !== following;
        state.following = following;
        canvas.toggleAttribute(PAUSED, enabled && !following);
        if (!changed || !enabled) return;
        try {
            canvas.dispatchEvent(new CustomEvent('sedna-follow', {
                bubbles: true,
                detail: { following: following }
            }));
        } catch (e) { /* the mark still says it */ }
    }
    function zoomOf(canvas, box) {
        var z = canvas.currentCSSZoom;
        if (z > 0) return z;
        return canvas.offsetWidth > 0 && box.width > 0 ? box.width / canvas.offsetWidth : 1;
    }
    function centre(canvas, smooth) {
        var state = canvas[STATE];
        var step = stepAtWork(canvas);
        if (!state || !step) return;
        var box = canvas.getBoundingClientRect();
        var node = nodeOf(step).getBoundingClientRect();
        var zoom = zoomOf(canvas, box);
        var dx = ((node.left + node.width / 2) - (box.left + box.width / 2)) / zoom;
        var dy = ((node.top + node.height / 2) - (box.top + box.height / 2)) / zoom;
        var smoothly = smooth && !reducedMotion();
        var left = canvas.scrollLeft, top = canvas.scrollTop;
        canvas.scrollTo({ left: left + dx, top: top + dy, behavior: smoothly ? 'smooth' : 'auto' });
        clearTimeout(state.land);
        if (!smoothly) return;
        state.land = setTimeout(function () {
            if (canvas.scrollLeft === left && canvas.scrollTop === top) centre(canvas, false);
        }, LAND);
    }
    function bind(canvas) {
        if (canvas[STATE]) return;
        var state = canvas[STATE] = { following: true, input: 0, queued: 0, land: 0 };
        function readerInput() { state.input = Date.now(); }
        canvas.addEventListener('wheel', readerInput, { passive: true });
        canvas.addEventListener('touchstart', readerInput, { passive: true });
        canvas.addEventListener('pointerdown', readerInput);
        canvas.addEventListener('keydown', readerInput);
        canvas.addEventListener('scroll', function () {
            if (Date.now() - state.input > READER) return;
            var step = stepAtWork(canvas);
            setFollowing(canvas, !step || inView(canvas, step));
        });
        new MutationObserver(function () {
            if (!canvas.hasAttribute('data-follow') || !state.following) return;
            clearTimeout(state.queued);
            state.queued = setTimeout(function () { centre(canvas, true); }, 0);
        }).observe(canvas, { subtree: true, childList: true, attributes: true, attributeFilter: ['data-state'] });
        centre(canvas, false);
    }
    function follow(canvas) {
        if (!canvas) return;
        bind(canvas);
        setFollowing(canvas, true);
        centre(canvas, true);
    }
    ui.workflow = {
        follow: follow,
        isFollowing: function (canvas) {
            if (!canvas) return false;
            var step = stepAtWork(canvas);
            return !step || inView(canvas, step);
        }
    };
    function canvasFor(button) {
        var id = button.getAttribute('aria-controls');
        if (id) return document.getElementById(id);
        for (var el = button.parentElement; el; el = el.parentElement) {
            var found = el.querySelector('.workflow[data-follow]');
            if (found) return found;
        }
        return null;
    }
    document.addEventListener('click', function (e) {
        var button = e.target && e.target.closest ? e.target.closest('[data-workflow-follow]') : null;
        if (!button) return;
        var canvas = canvasFor(button);
        if (!canvas) return;
        e.preventDefault();
        follow(canvas);
    });
    function bindAll(root) {
        var found = (root || document).querySelectorAll('.workflow[data-follow]');
        for (var i = 0; i < found.length; i++) bind(found[i]);
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { bindAll(document); });
    } else {
        bindAll(document);
    }
    new MutationObserver(function (records) {
        for (var i = 0; i < records.length; i++) {
            var record = records[i];
            if (record.type === 'attributes') {
                var canvas = record.target;
                if (!canvas.matches || !canvas.matches('.workflow')) continue;
                var on = canvas.hasAttribute('data-follow');
                if (on && record.oldValue !== null) continue;
                if (on) follow(canvas);
                else canvas.removeAttribute(PAUSED);
                continue;
            }
            var added = record.addedNodes;
            for (var j = 0; j < added.length; j++) {
                if (added[j].nodeType !== 1) continue;
                if (added[j].matches && added[j].matches('.workflow[data-follow]')) bind(added[j]);
                bindAll(added[j]);
            }
        }
    }).observe(document.documentElement, {
        childList: true,
        subtree: true,
        attributes: true,
        attributeOldValue: true,
        attributeFilter: ['data-follow']
    });
    var AWAY = 'data-workflow-away';
    var queued = false;
    function sweep(diagram) {
        var box = diagram.getBoundingClientRect();
        var vw = window.innerWidth, vh = window.innerHeight;
        var left = Math.max(box.left, 0) - vw / 2, right = Math.min(box.right, vw) + vw / 2;
        var top = Math.max(box.top, 0) - vh / 2, bottom = Math.min(box.bottom, vh) + vh / 2;
        var items = diagram.children;
        for (var i = 0; i < items.length; i++) {
            var r = items[i].getBoundingClientRect();
            var away = r.right < left || r.left > right || r.bottom < top || r.top > bottom;
            if (items[i].hasAttribute(AWAY) !== away) items[i].toggleAttribute(AWAY, away);
        }
    }
    function sweepAll() {
        queued = false;
        var all = document.querySelectorAll('.workflow');
        for (var i = 0; i < all.length; i++) sweep(all[i]);
    }
    function soon() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(function () { requestAnimationFrame(sweepAll); });
    }
    function inDiagram(node) {
        var el = node.nodeType === 1 ? node : node.parentElement;
        return !!(el && el.closest && (el.closest('.workflow') || el.querySelector('.workflow')));
    }
    function watchAll() {
        soon();
        document.addEventListener('scroll', soon, { capture: true, passive: true });
        window.addEventListener('resize', soon, { passive: true });
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                if (inDiagram(records[i].target)) { soon(); return; }
            }
        }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-state', 'class'] });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watchAll);
    else watchAll();
})(window.sednaUi);
(function (ui) {
    var OPEN = 'data-clamp-open';
    var FITS = 'data-clamp-fits';
    function blockOf(button) {
        var id = button.getAttribute('aria-controls');
        var named = id && document.getElementById(id);
        return named || button.previousElementSibling;
    }
    function isOpen(button) { return button.getAttribute('aria-expanded') === 'true'; }
    function measure(button) {
        var block = blockOf(button);
        if (!block) return;
        var open = isOpen(button);
        block.toggleAttribute(OPEN, open);
        if (open) { button.removeAttribute(FITS); return; }
        button.toggleAttribute(FITS, block.scrollHeight <= block.clientHeight + 1);
    }
    var sizes = 'ResizeObserver' in window ? new ResizeObserver(function () { schedule(); }) : null;
    var watched = typeof WeakSet === 'function' ? new WeakSet() : null;
    var pending = false;
    function refresh(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var all = scope.querySelectorAll('[data-clamp-toggle]');
        for (var i = 0; i < all.length; i++) {
            measure(all[i]);
            var block = blockOf(all[i]);
            if (sizes && block && watched && !watched.has(block)) {
                watched.add(block);
                sizes.observe(block);
            }
        }
        return all.length;
    }
    function schedule() {
        if (pending) return;
        pending = true;
        requestAnimationFrame(function () { pending = false; refresh(); });
    }
    ui.clamp = {
        toggle: function (button, open) {
            if (typeof button === 'string') button = document.getElementById(button);
            if (!button) return false;
            var next = open === undefined ? !isOpen(button) : !!open;
            button.setAttribute('aria-expanded', String(next));
            measure(button);
            var block = blockOf(button);
            if (!next && block && block.getBoundingClientRect().top < 0) block.scrollIntoView({ block: 'start' });
            return next;
        },
        refresh: refresh
    };
    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-clamp-toggle]');
        if (!button) return;
        e.preventDefault();
        ui.clamp.toggle(button);
    });
    function watch() {
        refresh();
        window.addEventListener('resize', schedule);
        if (document.fonts && document.fonts.ready) document.fonts.ready.then(schedule, function () {});
        if (!('MutationObserver' in window)) return;
        new MutationObserver(schedule).observe(document.documentElement, { childList: true, subtree: true, characterData: true });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();
})(window.sednaUi);
(function (ui) {
    var CLAMPED = 'code-block--clamped';
    function labelFor(button, expanded) {
        var other = expanded ? button.getAttribute('data-code-collapse')
                             : button.getAttribute('data-code-expand-label');
        if (!other) return;
        var text = button.querySelector('span');
        if (text) text.textContent = other;
    }
    ui.codeBlock = {
        toggle: function (block, expanded) {
            if (!block) return;
            var open = expanded === undefined ? block.classList.contains(CLAMPED) : expanded;
            block.classList.toggle(CLAMPED, !open);
            var button = block.querySelector('[data-code-expand]');
            if (button) {
                button.setAttribute('aria-expanded', String(open));
                labelFor(button, open);
            }
        }
    };
    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-code-expand]');
        if (!button) return;
        var block = button.closest('.code-block');
        if (!block) return;
        e.preventDefault();
        ui.codeBlock.toggle(block);
        if (block.classList.contains(CLAMPED)) {
            var pre = block.querySelector('pre');
            if (pre) pre.scrollTop = 0;
        }
    });
})(window.sednaUi);
(function (ui) {
    var reduce = window.matchMedia ? window.matchMedia('(prefers-reduced-motion: reduce)') : null;
    var said = typeof WeakMap === 'function' ? new WeakMap() : null;
    var settling = typeof WeakMap === 'function' ? new WeakMap() : null;
    function rootOf(el) {
        if (typeof el === 'string') el = document.getElementById(el);
        return el && el.closest ? el.closest('[data-carousel]') : null;
    }
    function trackOf(c) { return c.querySelector('.carousel-track'); }
    function slidesOf(track) {
        var out = [];
        for (var i = 0; i < track.children.length; i++) out.push(track.children[i]);
        return out;
    }
    function rtl(track) { return getComputedStyle(track).direction === 'rtl'; }
    function offset(track, slide, backwards) {
        var t = track.getBoundingClientRect(), s = slide.getBoundingClientRect();
        return backwards ? t.right - s.right : s.left - t.left;
    }
    function current(c) {
        var track = trackOf(c);
        if (!track) return -1;
        var slides = slidesOf(track), back = rtl(track), best = -1, bestAt = Infinity;
        for (var i = 0; i < slides.length; i++) {
            var at = Math.abs(offset(track, slides[i], back));
            if (at < bestAt - 0.5) { best = i; bestAt = at; }
        }
        return best;
    }
    function edges(track) {
        var max = track.scrollWidth - track.clientWidth;
        var pos = Math.abs(track.scrollLeft);
        return { start: max <= 1 || pos <= 1, end: max <= 1 || pos >= max - 1 };
    }
    function dotIndex(dot, order) {
        var n = parseInt(dot.getAttribute('data-carousel-to'), 10);
        return isNaN(n) ? order : n - 1;
    }
    function setFlag(el, name, on, value) {
        if (on) { if (el.getAttribute(name) !== value) el.setAttribute(name, value); }
        else if (el.hasAttribute(name)) el.removeAttribute(name);
    }
    function announce(c, index, slides) {
        var live = c.querySelector('[data-carousel-live]');
        if (!live || index < 0) return;
        var slide = slides[index];
        var label = slide.getAttribute('aria-label') || '';
        var sentence = live.getAttribute('data-carousel-say');
        var text = sentence
            ? sentence.replace(/\{(position|count|label)\}/g, function (all, name) {
                if (name === 'position') return String(index + 1);
                if (name === 'count') return String(slides.length);
                return label;
            })
            : label;
        if (!text) return;
        live.textContent = live.textContent === text ? text + ' ' : text;
    }
    function mark(c, speak) {
        var track = trackOf(c);
        if (!track) return;
        var slides = slidesOf(track), index = current(c), end = edges(track);
        for (var i = 0; i < slides.length; i++) setFlag(slides[i], 'aria-current', i === index, 'true');
        var dots = c.querySelectorAll('[data-carousel-to]');
        for (var d = 0; d < dots.length; d++) setFlag(dots[d], 'aria-current', dotIndex(dots[d], d) === index, 'true');
        var prev = c.querySelectorAll('[data-carousel-prev]'), next = c.querySelectorAll('[data-carousel-next]');
        for (var p = 0; p < prev.length; p++) setFlag(prev[p], 'aria-disabled', end.start, 'true');
        for (var n = 0; n < next.length; n++) setFlag(next[n], 'aria-disabled', end.end, 'true');
        if (!said) return;
        if (!said.has(c)) { said.set(c, index); return; }
        if (!speak || said.get(c) === index) return;
        said.set(c, index);
        announce(c, index, slides);
    }
    function go(el, index) {
        var c = rootOf(el);
        if (!c) return -1;
        var track = trackOf(c);
        if (!track) return -1;
        var slides = slidesOf(track);
        if (!slides.length) return -1;
        index = Math.max(0, Math.min(slides.length - 1, index | 0));
        var back = rtl(track);
        var by = offset(track, slides[index], back);
        var left = track.scrollLeft + (back ? -by : by);
        var smooth = !(reduce && reduce.matches);
        track.scrollTo({ left: left, behavior: smooth ? 'smooth' : 'auto' });
        if (!smooth) mark(c, true);
        return index;
    }
    ui.carousel = {
        go: go,
        next: function (el) { var c = rootOf(el); return c ? go(c, current(c) + 1) : -1; },
        prev: function (el) { var c = rootOf(el); return c ? go(c, current(c) - 1) : -1; },
        current: function (el) { var c = rootOf(el); return c ? current(c) : -1; }
    };
    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-carousel-prev], [data-carousel-next], [data-carousel-to]');
        if (!button) return;
        var c = rootOf(button);
        if (!c) return;
        e.preventDefault();
        if (button.getAttribute('aria-disabled') === 'true') return;
        if (button.hasAttribute('data-carousel-prev')) ui.carousel.prev(c);
        else if (button.hasAttribute('data-carousel-next')) ui.carousel.next(c);
        else {
            var dots = c.querySelectorAll('[data-carousel-to]');
            go(c, dotIndex(button, Array.prototype.indexOf.call(dots, button)));
        }
    });
    document.addEventListener('keydown', function (e) {
        if (e.altKey || e.ctrlKey || e.metaKey || e.shiftKey) return;
        var track = e.target.closest && e.target.closest('.carousel-track');
        if (!track) return;
        if (e.target !== track && (e.target.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(e.target.tagName))) return;
        var c = rootOf(track);
        if (!c) return;
        var back = rtl(track), now = current(c), to;
        if (e.key === 'ArrowRight') to = back ? now - 1 : now + 1;
        else if (e.key === 'ArrowLeft') to = back ? now + 1 : now - 1;
        else if (e.key === 'Home') to = 0;
        else if (e.key === 'End') to = slidesOf(track).length - 1;
        else return;
        e.preventDefault();
        go(c, to);
    });
    var frame = null;
    function settle(c) {
        if (!settling) return;
        clearTimeout(settling.get(c));
        settling.delete(c);
        mark(c, true);
    }
    document.addEventListener('scroll', function (e) {
        var track = e.target;
        if (!track || !track.classList || !track.classList.contains('carousel-track')) return;
        var c = rootOf(track);
        if (!c) return;
        if (!frame) frame = requestAnimationFrame(function () { frame = null; mark(c, false); });
        if (settling) {
            clearTimeout(settling.get(c));
            settling.set(c, setTimeout(function () { settle(c); }, 150));
        }
    }, true);
    document.addEventListener('scrollend', function (e) {
        var track = e.target;
        if (!track || !track.classList || !track.classList.contains('carousel-track')) return;
        var c = rootOf(track);
        if (c) settle(c);
    }, true);
    function markAll(root) {
        var scope = root && root.querySelectorAll ? root : document;
        var all = scope.querySelectorAll('[data-carousel]');
        for (var i = 0; i < all.length; i++) mark(all[i], false);
        if (scope !== document && scope.matches && scope.matches('[data-carousel]')) mark(scope, false);
    }
    function watch() {
        markAll();
        window.addEventListener('resize', function () { markAll(); });
        if (!('MutationObserver' in window)) return;
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    if (added[j].nodeType !== 1) continue;
                    var c = rootOf(added[j]);
                    if (c) mark(c, false);
                    else markAll(added[j]);
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();
})(window.sednaUi);
(function (ui) {
    function scrollerOf(el) {
        for (var p = el.parentElement; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
            var o = getComputedStyle(p).overflowY;
            if (o === 'auto' || o === 'scroll') return p;
        }
        return null;
    }
    ui._.scrollerOf = scrollerOf;
    function targetOf(link) {
        var href = link.getAttribute('href');
        if (!href || href.length < 2 || href.charAt(0) !== '#') return null;
        var id;
        try {
            id = decodeURIComponent(href.slice(1));
        } catch (e) {
            return null;
        }
        var target = document.getElementById(id);
        if (target || id.toLowerCase() !== 'top') return target;
        return { top: scrollerOf(link) };
    }
    function focusOn(target) {
        target.focus({ preventScroll: true });
        if (document.activeElement === target || target.hasAttribute('tabindex')) return;
        target.setAttribute('tabindex', '-1');
        target.addEventListener('blur', function () {
            target.removeAttribute('tabindex');
        }, { once: true });
        target.focus({ preventScroll: true });
    }
    function moveTo(target) {
        if (target.nodeType !== 1) {
            var scroller = target.top;
            if (scroller) scroller.scrollTop = 0;
            else window.scrollTo(0, 0);
            focusOn(scroller || document.querySelector('main') || document.body);
            return;
        }
        target.scrollIntoView();
        focusOn(target);
    }
    document.addEventListener('click', function (e) {
        if (e.defaultPrevented || e.button !== 0) return;
        if (e.ctrlKey || e.shiftKey || e.altKey || e.metaKey) return;
        if (!(e.target instanceof Element)) return;
        var link = e.target.closest('a[href^="#"]');
        if (!link || link.hasAttribute('download')) return;
        var frame = link.getAttribute('target');
        if (frame && frame !== '_self') return;
        var target = targetOf(link);
        if (!target) return;
        e.preventDefault();
        moveTo(target);
    });
})(window.sednaUi);
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
        toggle: toggle,
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
(function (ui) {
    var DISTANCE = 0.25;
    var FLICK = 0.5;
    var FLICK_FLOOR = 24;
    var DRAGGING = 'sheet--dragging';
    var drag = null;
    function gripped(target) {
        if (!(target instanceof Element)) return null;
        var control = target.closest('button, a, input, select, textarea, [contenteditable]');
        if (control && !control.classList.contains('sheet-handle')) return null;
        var grip = target.closest('.sheet-handle, .drawer-header');
        if (!grip) return null;
        var sheet = grip.closest('dialog.sheet[data-sheet]');
        return sheet && sheet.hasAttribute('open') && grip.parentElement === sheet ? sheet : null;
    }
    function slideOut(sheet) {
        sheet.classList.remove(DRAGGING);   // the transition comes back…
        sheet.style.transform = '';         // …and the target becomes .sheet's own
        if (typeof sheet.requestClose === 'function') sheet.requestClose();
        else sheet.close();                 //    translateY(100%), once [open] goes
    }
    function springBack(sheet) {
        sheet.classList.remove(DRAGGING);
        sheet.style.transform = '';
    }
    function snaps(sheet) {
        var raw = sheet.getAttribute('data-sheet-snap');
        if (raw === null) return null;
        var list = [], first = null;
        raw.split(/[\s,]+/).forEach(function (part) {
            var n = parseFloat(part);
            if (!isFinite(n) || n <= 0 || n > 100) return;
            n = Math.round(n * 100) / 10000;
            if (first === null) first = n;
            if (list.indexOf(n) < 0) list.push(n);
        });
        if (list.indexOf(1) < 0) list.push(1);
        list.sort(function (a, b) { return a - b; });
        return { list: list, first: first === null ? 1 : first };
    }
    function shareOf(sheet) {
        var at = parseFloat(sheet.getAttribute('data-sheet-at'));
        return isFinite(at) ? at / 100 : 1;
    }
    function format(share) { return Math.round(share * 10000) / 100 + '%'; }
    function rest(sheet, share) {
        sheet.classList.remove(DRAGGING);
        sheet.style.transform = '';
        sheet.style.setProperty('--sheet-rest', format(1 - share));
        sheet.setAttribute('data-sheet-at', format(share));
        var expand = sheet.querySelectorAll('[data-sheet-expand]');
        for (var i = 0; i < expand.length; i++) expand[i].setAttribute('aria-expanded', String(share >= 1));
    }
    function opened(sheet) {
        var s = sheet.matches && sheet.matches('dialog.sheet[data-sheet-snap]') && sheet.open ? snaps(sheet) : null;
        if (s) rest(sheet, s.first);
    }
    if (typeof MutationObserver === 'function') {
        new MutationObserver(function (records) {
            records.forEach(function (r) { opened(r.target); });
        }).observe(document.documentElement, { attributes: true, attributeFilter: ['open'], subtree: true });
    }
    document.querySelectorAll('dialog.sheet[data-sheet-snap][open]').forEach(opened);
    var dragged = false;   // the last press moved the sheet, so its click is not a step
    document.addEventListener('click', function (e) {
        var control = e.target instanceof Element ? e.target.closest('[data-sheet-expand]') : null;
        var sheet = control ? control.closest('dialog.sheet[data-sheet-snap]') : null;
        if (!sheet || !sheet.open) return;
        if (dragged && e.detail !== 0) { dragged = false; return; }
        var list = snaps(sheet).list, now = shareOf(sheet), next = list[0];
        for (var i = 0; i < list.length; i++) {
            if (list[i] > now + 0.001) { next = list[i]; break; }
        }
        rest(sheet, next);
    });
    document.addEventListener('pointerdown', function (e) {
        if (drag || (e.pointerType === 'mouse' && e.button !== 0)) return;
        dragged = false;
        var sheet = gripped(e.target);
        if (!sheet) return;
        var height = sheet.getBoundingClientRect().height;
        var snap = snaps(sheet);
        drag = {
            sheet: sheet,
            id: e.pointerId,
            y0: e.clientY,
            t0: e.timeStamp,
            dy: 0,
            moved: 0,
            captured: false,
            height: height,
            snap: snap,
            rest: snap ? (1 - shareOf(sheet)) * height : 0
        };
        sheet.classList.add(DRAGGING);
        e.preventDefault();
    });
    document.addEventListener('pointermove', function (e) {
        if (!drag || e.pointerId !== drag.id) return;
        var moved = Math.abs(e.clientY - drag.y0);
        if (moved > 3 && !drag.captured) {
            drag.captured = true;
            try { drag.sheet.setPointerCapture(e.pointerId); } catch (err) { /* carry on */ }
        }
        drag.moved = Math.max(drag.moved, moved);
        drag.dy = Math.max(-drag.rest, e.clientY - drag.y0);
        drag.sheet.style.transform = 'translateY(' + (drag.rest + drag.dy) + 'px)';
    });
    function finish(e) {
        if (!drag || e.pointerId !== drag.id) return;
        var d = drag;
        drag = null;
        try { d.sheet.releasePointerCapture(e.pointerId); } catch (err) { /* already gone */ }
        var speed = d.dy / Math.max(1, e.timeStamp - d.t0);
        if (d.moved > 3) dragged = true;
        if (d.snap) { settle(d, speed); return; }
        if (d.dy > d.height * DISTANCE || (speed > FLICK && d.dy > FLICK_FLOOR)) slideOut(d.sheet);
        else springBack(d.sheet);
    }
    function settle(d, speed) {
        var list = d.snap.list, h = Math.max(1, d.height);
        var share = 1 - (d.rest + d.dy) / h;   // what shows where the finger let go
        var from = 1 - d.rest / h;
        var flick = Math.abs(speed) > FLICK && Math.abs(d.dy) > FLICK_FLOOR;
        var lowest = list[0], i;
        if (flick && speed > 0) {
            for (i = list.length - 1; i >= 0; i--) if (list[i] < from - 0.001) { rest(d.sheet, list[i]); return; }
            slideOut(d.sheet);
            return;
        }
        if (flick) {
            for (i = 0; i < list.length; i++) if (list[i] > from + 0.001) { rest(d.sheet, list[i]); return; }
            rest(d.sheet, 1);
            return;
        }
        if (share < lowest * (1 - DISTANCE)) { slideOut(d.sheet); return; }
        var best = lowest;
        for (i = 0; i < list.length; i++) if (Math.abs(list[i] - share) < Math.abs(best - share)) best = list[i];
        rest(d.sheet, best);
    }
    document.addEventListener('pointerup', finish);
    document.addEventListener('pointercancel', finish);
})(window.sednaUi);
(function (ui) {
    if (!('IntersectionObserver' in window)) return;
    var spies = new Map();
    function idOf(link) {
        var href = link.getAttribute('href') || '';
        if (href.length < 2 || href.charAt(0) !== '#') return null;
        try { return decodeURIComponent(href.slice(1)); } catch (e) { return null; }
    }
    function linksOf(nav) {
        var all = nav.querySelectorAll('a[href^="#"]');
        var links = [], targets = [];
        for (var i = 0; i < all.length; i++) {
            var id = idOf(all[i]);
            var target = id && document.getElementById(id);
            if (!target) continue;
            links.push(all[i]);
            targets.push(target);
        }
        return { links: links, targets: targets };
    }
    function scrollerOf(el) {
        return ui._.scrollerOf ? ui._.scrollerOf(el) : null;
    }
    function scrollingElement(spy) {
        return spy.root || document.scrollingElement || document.documentElement;
    }
    function atEnd(spy) {
        var el = scrollingElement(spy);
        var max = el.scrollHeight - el.clientHeight;
        return max > 1 && el.scrollTop >= max - 1;
    }
    function mark(spy, index) {
        for (var i = 0; i < spy.links.length; i++) {
            if (i === index) {
                if (spy.links[i].getAttribute('aria-current') !== 'location') spy.links[i].setAttribute('aria-current', 'location');
            } else if (spy.links[i].getAttribute('aria-current') === 'location') {
                spy.links[i].removeAttribute('aria-current');
            }
        }
    }
    function current(spy) {
        if (spy.links.length === 0) return -1;
        if (atEnd(spy)) return spy.links.length - 1;
        var above = -1;
        for (var i = 0; i < spy.targets.length; i++) {
            var s = spy.state.get(spy.targets[i]);
            if (s === 'in') return i;
            if (s === 'above') above = i;
        }
        return above >= 0 ? above : 0;
    }
    function update(spy) {
        if (spy.pinned >= 0) mark(spy, spy.pinned);
        else mark(spy, current(spy));
    }
    function stop(nav) {
        var spy = spies.get(nav);
        if (!spy) return;
        spy.io.disconnect();
        spies.delete(nav);
    }
    function start(nav) {
        var found = linksOf(nav);
        var spy = {
            links: found.links,
            targets: found.targets,
            root: found.targets.length ? scrollerOf(found.targets[0]) : null,
            state: new Map(),
            pinned: -1,
            pinnedAt: null,
            io: null
        };
        spy.io = new IntersectionObserver(function (entries) {
            for (var i = 0; i < entries.length; i++) {
                var e = entries[i];
                var top = e.rootBounds ? e.rootBounds.top : 0;
                spy.state.set(e.target, e.isIntersecting ? 'in' : (e.boundingClientRect.top < top ? 'above' : 'below'));
            }
            update(spy);
        }, { root: spy.root, rootMargin: '0px 0px -60% 0px', threshold: 0 });
        for (var i = 0; i < spy.targets.length; i++) spy.io.observe(spy.targets[i]);
        spies.set(nav, spy);
        update(spy);
    }
    function unchanged(nav, spy) {
        var found = linksOf(nav);
        if (found.links.length !== spy.links.length) return false;
        for (var i = 0; i < found.links.length; i++) {
            if (found.links[i] !== spy.links[i] || found.targets[i] !== spy.targets[i]) return false;
        }
        return true;
    }
    function sweep() {
        spies.forEach(function (spy, nav) {
            if (!nav.isConnected || !nav.hasAttribute('data-spy')) stop(nav);
        });
        var navs = document.querySelectorAll('[data-spy]');
        for (var i = 0; i < navs.length; i++) {
            var spy = spies.get(navs[i]);
            if (spy && unchanged(navs[i], spy)) continue;
            stop(navs[i]);
            start(navs[i]);
        }
    }
    document.addEventListener('scroll', function (e) {
        var scroller = e.target === document ? null : e.target;
        spies.forEach(function (spy) {
            if (spy.root !== scroller && !(scroller === document.scrollingElement && spy.root === null)) return;
            if (spy.pinned >= 0) {
                if (Math.abs(scrollingElement(spy).scrollTop - spy.pinnedAt) < 2) return;
                spy.pinned = -1;
                spy.pinnedAt = null;
            }
            update(spy);
        });
    }, true);
    document.addEventListener('click', function (e) {
        if (!e.defaultPrevented || !(e.target instanceof Element)) return;
        var link = e.target.closest('[data-spy] a[href^="#"]');
        if (!link) return;
        var spy = spies.get(link.closest('[data-spy]'));
        if (!spy) return;
        var index = spy.links.indexOf(link);
        if (index < 0) return;
        spy.pinned = index;
        spy.pinnedAt = scrollingElement(spy).scrollTop;
        update(spy);
    });
    var queued = false;
    function queue() {
        if (queued) return;
        queued = true;
        requestAnimationFrame(function () { queued = false; sweep(); });
    }
    function watch() {
        sweep();
        if (!('MutationObserver' in window)) return;
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                if (records[i].type === 'attributes' || records[i].addedNodes.length || records[i].removedNodes.length) {
                    queue();
                    return;
                }
            }
        }).observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-spy', 'href', 'id'] });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();
})(window.sednaUi);
(function (ui) {
    var SECOND = 1000, MINUTE = 60 * SECOND, HOUR = 60 * MINUTE, DAY = 24 * HOUR;
    var UNITS = [
        { name: 'seconds', size: SECOND },
        { name: 'minutes', size: MINUTE },
        { name: 'hours', size: HOUR },
        { name: 'days', size: DAY },
        { name: 'weeks', size: 7 * DAY },
        { name: 'months', size: 30 * DAY },
        { name: 'years', size: 365 * DAY }
    ];
    var CATEGORIES = ['zero', 'one', 'two', 'few', 'many'];
    var SELECTOR = 'time[data-relative], time[data-countdown]';
    var LONGEST = HOUR;
    var timer = 0;
    var shown = new WeakMap();
    var ended = new WeakMap();
    var formats = {};
    function intl(locale) {
        var key = locale || '';
        if (formats[key]) return formats[key];
        var made = { number: null, plural: null };
        if (typeof Intl === 'object' && Intl) {
            try { made.number = new Intl.NumberFormat(locale || undefined); }
            catch (e) { try { made.number = new Intl.NumberFormat(); } catch (e2) { made.number = null; } }
            try { made.plural = new Intl.PluralRules(locale || undefined); }
            catch (e) { try { made.plural = new Intl.PluralRules(); } catch (e2) { made.plural = null; } }
        }
        formats[key] = made;
        return made;
    }
    function natural(ms) {
        var i = UNITS.length - 1;
        while (i > 0 && UNITS[i].size > ms) i--;
        return i;
    }
    function has(el, i) { return el.hasAttribute('data-' + UNITS[i].name); }
    function unitFor(el, ms) {
        var start = natural(ms), i;
        for (i = start; i >= 0; i--) if (has(el, i)) return i;
        for (i = start + 1; i < UNITS.length; i++) if (has(el, i)) return i;
        return -1;
    }
    function text(el, unit, n) {
        var host = el.closest('[lang]');
        var tools = intl(host ? host.getAttribute('lang') : '');
        var name = 'data-' + UNITS[unit].name;
        var format = null;
        if (tools.plural) {
            var category = tools.plural.select(n);
            if (CATEGORIES.indexOf(category) >= 0) format = el.getAttribute(name + '-' + category);
        }
        if (format === null) format = el.getAttribute(name);
        var number = tools.number ? tools.number.format(n) : String(n);
        return format.split('{n}').join(number);
    }
    function measure(el, now) {
        var at = Date.parse(el.getAttribute('datetime') || '');
        if (isNaN(at)) return null;
        var countdown = el.hasAttribute('data-countdown');
        var ms = countdown ? at - now : now - at;
        var ahead = 0;
        if (ms < 0) { ahead = -ms; ms = 0; }
        var unit = unitFor(el, ms);
        if (unit < 0) return null;
        var size = UNITS[unit].size;
        var n, wait;
        if (countdown) {
            n = ms > 0 ? Math.max(1, Math.floor(ms / size)) : 0;
            wait = ms <= 0 ? Infinity : Math.floor(ms / size) === 0 ? ms : ms % size + 1;
            var floor = UNITS[natural(ms)].size;
            if (ms > floor) wait = Math.min(wait, ms - floor + 1);
        } else {
            n = Math.floor(ms / size);
            wait = size - ms % size;
            var next = natural(ms) + 1;
            if (next < UNITS.length) wait = Math.min(wait, UNITS[next].size - ms);
            wait += ahead;
        }
        return { text: text(el, unit, n), wait: wait, over: countdown && ms === 0 };
    }
    function write(el, value) {
        shown.set(el, value);
        var first = null;
        for (var node = el.firstChild; node; node = node.nextSibling) {
            if (node.nodeType !== 3) continue;
            if (!first) first = node;
            else if (node.nodeValue) node.nodeValue = '';
        }
        if (!first) el.appendChild(document.createTextNode(value));
        else if (first.nodeValue !== value) first.nodeValue = value;
    }
    function update() {
        clearTimeout(timer);
        timer = 0;
        if (document.visibilityState === 'hidden') return;
        var now = Date.now(), soonest = Infinity, done = [];
        var all = document.querySelectorAll(SELECTOR);
        for (var i = 0; i < all.length; i++) {
            var el = all[i], m = measure(el, now);
            if (!m) continue;
            write(el, m.text);
            if (m.over) {
                var until = el.getAttribute('datetime');
                if (ended.get(el) !== until) { ended.set(el, until); done.push(el); }
            } else {
                ended.delete(el);
            }
            if (m.wait < soonest) soonest = m.wait;
        }
        if (soonest !== Infinity) timer = setTimeout(update, Math.min(soonest, LONGEST));
        for (var j = 0; j < done.length; j++) {
            done[j].dispatchEvent(new CustomEvent('sedna-countdown-end', {
                bubbles: true,
                detail: { until: done[j].getAttribute('datetime') }
            }));
        }
    }
    function concerns(records) {
        for (var i = 0; i < records.length; i++) {
            var r = records[i], t = r.target;
            if (r.type === 'characterData') {
                var el = t.parentNode;
                if (el && el.nodeType === 1 && el.matches(SELECTOR) && t.nodeValue !== shown.get(el)
                    && t.nodeValue !== '') return true;
            } else if (r.type === 'attributes') {
                if (r.attributeName === 'lang' || (t.matches && t.matches(SELECTOR))) return true;
            } else {
                for (var j = 0; j < r.addedNodes.length; j++) {
                    var node = r.addedNodes[j];
                    if (node.nodeType !== 1) continue;
                    if (node.matches(SELECTOR) || node.querySelector(SELECTOR)) return true;
                }
                if (t.nodeType === 1 && t.matches(SELECTOR)) return true;
            }
        }
        return false;
    }
    function watch() {
        update();
        if (!('MutationObserver' in window)) return;
        var names = ['datetime', 'data-relative', 'data-countdown', 'lang'];
        for (var i = 0; i < UNITS.length; i++) {
            names.push('data-' + UNITS[i].name);
            for (var c = 0; c < CATEGORIES.length; c++) names.push('data-' + UNITS[i].name + '-' + CATEGORIES[c]);
        }
        new MutationObserver(function (records) {
            if (concerns(records)) update();
        }).observe(document.documentElement, {
            childList: true, subtree: true, characterData: true,
            attributes: true, attributeFilter: names
        });
    }
    document.addEventListener('visibilitychange', update);
    window.addEventListener('pageshow', update);
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', watch);
    else watch();
})(window.sednaUi);
(function () {
    var timer = 0, nudged = false;
    function typing() {
        var a = document.activeElement;
        return !!a && (a.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(a.tagName));
    }
    function check() {
        var vv = window.visualViewport;
        if (!vv || window.innerWidth > 560 || !document.querySelector('.layout--flow')) return;
        var shifted = vv.offsetTop > 0.5 || window.innerHeight - vv.height > 1;
        if (!shifted) { nudged = false; return; }
        if (nudged || typing() || Math.abs(vv.scale - 1) > 0.01) return;
        nudged = true;
        window.scrollBy(0, 1);
        setTimeout(function () { window.scrollBy(0, -1); }, 50);
    }
    function settle() {
        clearTimeout(timer);
        timer = setTimeout(check, 150);
    }
    window.addEventListener('scroll', settle, { passive: true });
    window.addEventListener('resize', settle);
    document.addEventListener('focusout', settle);
    if (window.visualViewport) {
        window.visualViewport.addEventListener('resize', settle);
        window.visualViewport.addEventListener('scroll', settle);
    }
})();
(function (ui) {
    ui.md = {
        _seq: 0,
        init: function (root) {
            root = root || document;
            var editors = root.matches && root.matches('.md-editor')
                ? [root]
                : root.querySelectorAll('.md-editor');
            for (var i = 0; i < editors.length; i++) this._initOne(editors[i]);
        },
        _initOne: function (root) {
            if (!root || root.dataset.mdReady === '1') return;
            root.dataset.mdReady = '1';
            var self = this;
            var ta = root.querySelector('[data-md-input]');
            var preview = root.querySelector('[data-md-preview]');
            if (!ta) return;
            var renderPreview = function () {
                if (preview) preview.innerHTML = self.render(ta.value);
            };
            var views = root.querySelectorAll('input[data-md-tab]');
            if (views.length) {
                var group = 'sedna-md-view-' + (++ui.md._seq);
                views.forEach(function (r) { r.name = group; });
            }
            root.addEventListener('click', function (e) {
                var cmdBtn = e.target.closest('[data-md-cmd]');
                if (cmdBtn && root.contains(cmdBtn)) {
                    e.preventDefault();
                    self.apply(ta, cmdBtn.getAttribute('data-md-cmd'));
                    renderPreview();
                }
            });
            root.addEventListener('change', function (e) {
                var radio = e.target.closest('input[data-md-tab]');
                if (!radio || !root.contains(radio) || !radio.checked) return;
                var view = radio.getAttribute('data-md-tab');
                if (view === 'preview') {
                    renderPreview();
                    if (preview) preview.style.height = ta.offsetHeight + 'px';
                } else if (preview && preview.offsetHeight) {
                    ta.style.height = preview.offsetHeight + 'px';
                }
                root.setAttribute('data-md-view', view);
            });
            ta.addEventListener('input', renderPreview);
            renderPreview();
        },
        apply: function (ta, cmd) {
            var v = ta.value, s = ta.selectionStart, e = ta.selectionEnd;
            var sel = v.slice(s, e);
            var wrap = function (before, after, ph) {
                var body = sel || ph;
                ta.value = v.slice(0, s) + before + body + after + v.slice(e);
                ta.selectionStart = s + before.length;
                ta.selectionEnd = s + before.length + body.length;
            };
            var linePrefix = function (prefix) {
                var ls = v.lastIndexOf('\n', s - 1) + 1;
                var le = v.indexOf('\n', e); if (le === -1) le = v.length;
                var block = v.slice(ls, le) || prefix.trim();
                var prefixed = block.split('\n').map(function (line, i) {
                    return (cmd === 'ol' ? (i + 1) + '. ' : prefix) + line;
                }).join('\n');
                ta.value = v.slice(0, ls) + prefixed + v.slice(le);
                ta.selectionStart = ls;
                ta.selectionEnd = ls + prefixed.length;
            };
            switch (cmd) {
                case 'bold':   wrap('**', '**', 'bold text'); break;
                case 'italic': wrap('_', '_', 'italic text'); break;
                case 'code':   wrap('`', '`', 'code'); break;
                case 'h2':     linePrefix('## '); break;
                case 'ul':     linePrefix('- '); break;
                case 'ol':     linePrefix('1. '); break;
                case 'quote':  linePrefix('> '); break;
                case 'link':   wrap('[', '](https://)', 'link text'); break;
                case 'image':  wrap('![', '](https://)', 'image description'); break;
                case 'video':  wrap('![', '](https://)', 'video description'); break;
                default: return;
            }
            ta.dispatchEvent(new Event('input', { bubbles: true }));
            ta.focus();
        },
        insert: function (ta, text) {
            if (!ta || typeof ta.selectionStart !== 'number') return;
            text = text == null ? '' : String(text);
            var v = ta.value, s = ta.selectionStart, e = ta.selectionEnd;
            ta.value = v.slice(0, s) + text + v.slice(e);
            ta.selectionStart = ta.selectionEnd = s + text.length;
            ta.dispatchEvent(new Event('input', { bubbles: true }));
            ta.focus();
        },
        render: function (src) {
            if (!src) return '';
            var esc = function (s) {
                return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
                    .replace(/"/g, '&quot;');
            };
            var blocks = [];
            src = src.replace(/```([\s\S]*?)```/g, function (_, code) {
                blocks.push('<pre><code>' + esc(code.replace(/^\n/, '').replace(/\n$/, '')) + '</code></pre>');
                return '  B' + (blocks.length - 1) + ' ';
            });
            var inline = function (t) {
                t = esc(t);
                var held = [];
                var hold = function (html) {
                    held.push(html);
                    return '\u0000' + (held.length - 1) + '\u0000';
                };
                t = t.replace(/`([^`]+)`/g, function (_, code) { return hold('<code>' + code + '</code>'); });
                t = t.replace(/!\[([^\]]*)\]\(([^)\s]+)\)/g, function (_, alt, url) { return hold(mediaHtml(alt, url)); });
                t = t.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, function (_, txt, url) {
                    var safe = ui._.safeHref(url) || '#';
                    return hold('<a href="' + safe + '" target="_blank" rel="noopener">') + txt + hold('</a>');
                });
                t = t.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
                t = t.replace(/_([^_]+)_/g, '<em>$1</em>');
                return t.replace(/\u0000(\d+)\u0000/g, function (_, i) { return held[+i] || ''; });
            };
            var mediaHtml = function (alt, url) {
                if (!/^(https?:|[^:\/?#]*(?:[\/?#]|$))/i.test(url)) return alt;
                if (/\.(mp4|m4v|mov|webm|ogv)$/i.test(url.split(/[?#]/)[0])) {
                    return '<video controls preload="metadata" src="' + url + '"' +
                        (alt ? ' aria-label="' + alt + '"' : '') + '></video>';
                }
                return '<img src="' + url + '" alt="' + alt + '" loading="lazy">';
            };
            var out = [], list = null;
            var closeList = function () { if (list) { out.push('</' + list + '>'); list = null; } };
            src.split(/\r?\n/).forEach(function (line) {
                var ph = line.match(/^  B(\d+) $/);
                if (ph) { closeList(); out.push(blocks[+ph[1]]); return; }
                if (!line.trim()) { closeList(); return; }
                var m;
                if ((m = line.match(/^(#{1,6})\s+(.*)$/))) {
                    closeList();
                    var n = m[1].length;
                    out.push('<h' + n + '>' + inline(m[2]) + '</h' + n + '>');
                    return;
                }
                if (/^(---|\*\*\*|___)\s*$/.test(line)) { closeList(); out.push('<hr>'); return; }
                if ((m = line.match(/^>\s?(.*)$/))) {
                    closeList(); out.push('<blockquote>' + inline(m[1]) + '</blockquote>'); return;
                }
                if ((m = line.match(/^[-*]\s+(.*)$/))) {
                    if (list !== 'ul') { closeList(); out.push('<ul>'); list = 'ul'; }
                    out.push('<li>' + inline(m[1]) + '</li>'); return;
                }
                if ((m = line.match(/^\d+\.\s+(.*)$/))) {
                    if (list !== 'ol') { closeList(); out.push('<ol>'); list = 'ol'; }
                    out.push('<li>' + inline(m[1]) + '</li>'); return;
                }
                closeList(); out.push('<p>' + inline(line) + '</p>');
            });
            closeList();
            return out.join('');
        }
    };
})(window.sednaUi);
(function (ui) {
    var readRaw = ui._.readRaw;
    ui.openTab = function (url) {
        try { window.open(url, '_blank', 'noopener'); } catch (e) { /* ignore */ }
    };
    ui.copyText = async function (text) {
        try {
            if (navigator.clipboard && window.isSecureContext) {
                await navigator.clipboard.writeText(text);
                return true;
            }
        } catch (e) { /* fall through to the legacy path */ }
        try {
            var ta = document.createElement('textarea');
            ta.value = text;
            ta.style.position = 'fixed';
            ta.style.opacity = '0';
            document.body.appendChild(ta);
            ta.focus(); ta.select();
            var ok = document.execCommand('copy');
            document.body.removeChild(ta);
            return ok;
        } catch (e) { return false; }
    };
    ui.viewportWidth = function () {
        return window.innerWidth || document.documentElement.clientWidth || 0;
    };
    ui.scrollPageTop = function () {
        var page = document.querySelector('.page');
        if (page) page.scrollTop = 0;
        try { window.scrollTo(0, 0); } catch (e) { /* ignore */ }
    };
    ui.timeZone = function () {
        try { return Intl.DateTimeFormat().resolvedOptions().timeZone || null; }
        catch (e) { return null; }
    };
    ui.getItem = function (k) { return readRaw(k); };
    ui.setItem = function (k, value) {
        try { localStorage.setItem(k, value); } catch (e) { /* ignore */ }
    };
    var watchers = {};
    var nextWatcher = 1;
    ui.watchSettings = function (ref) {
        var id = nextWatcher++;
        watchers[id] = ui.settings.onChange(function (settings) {
            try {
                var call = ref.invokeMethodAsync('SettingsChanged', settings);
                if (call && call.catch) call.catch(function () { ui.unwatchSettings(id); });
            } catch (e) {
                ui.unwatchSettings(id);
            }
        });
        return id;
    };
    ui.unwatchSettings = function (id) {
        var off = watchers[id];
        if (!off) return false;
        off();
        delete watchers[id];
        return true;
    };
})(window.sednaUi);
(function (ui) {
    var lock = null;          // { allow, block, gate } while a lock is up, else null
    var marked = null;        // the element currently carrying .spotlight-allowed
    var ALWAYS = '.spotlight-tip, .spotlight-allowed, #components-reconnect-modal';
    function toArray(v) {
        if (!v) return [];
        if (typeof v === 'string') {
            var found = document.querySelectorAll(v), list = [];
            for (var i = 0; i < found.length; i++) list.push(found[i]);
            return list;
        }
        if (v.nodeType === 1) return [v];
        if (typeof v.length === 'number') {
            var out = [];
            for (var j = 0; j < v.length; j++) {
                var one = toArray(v[j]);
                for (var k = 0; k < one.length; k++) out.push(one[k]);
            }
            return out;
        }
        return [];
    }
    function visible(el) { return !!(el && el.getClientRects().length); }
    function resolve(target, extra) {
        return toArray(target).concat(toArray(extra)).filter(visible);
    }
    function one(v) {
        var list = toArray(v);
        return list.length ? list[0] : null;
    }
    function union(els) {
        var left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
        for (var i = 0; i < els.length; i++) {
            var b = els[i].getBoundingClientRect();
            if (b.left < left) left = b.left;
            if (b.top < top) top = b.top;
            if (b.right > right) right = b.right;
            if (b.bottom > bottom) bottom = b.bottom;
        }
        return {
            left: left, top: top, right: right, bottom: bottom,
            width: right - left, height: bottom - top
        };
    }
    function isRaised(el) {
        try { return !!el && el.matches(':popover-open'); } catch (e) { return false; }
    }
    function isModal(dialog) {
        try { return !!dialog && dialog.isConnected && dialog.matches(':modal'); }
        catch (e) { return false; }
    }
    function zoomOf(el) {
        var parent = (el && el.offsetParent) || document.body;
        if (!parent) return 1;
        var z = parent.currentCSSZoom;
        if (typeof z === 'number' && z > 0) return z;
        var drawn = parent.getBoundingClientRect().width;
        return parent.offsetWidth > 0 && drawn > 0 ? drawn / parent.offsetWidth : 1;
    }
    function offsetParentRect(el) {
        if (isRaised(el)) return { left: 0, top: 0 };
        var parent = el.offsetParent || document.body;
        return parent.getBoundingClientRect();
    }
    var raised = [];    // { el, dialog, home, attr, top, left, width }, one per raised element
    var stage = null;   // the dialog at() is placing into, so tipAt() follows it there
    function raisedEntry(el) {
        for (var i = 0; i < raised.length; i++) if (raised[i].el === el) return raised[i];
        return null;
    }
    function keepingFocus(el, move) {
        var focused = el.contains(document.activeElement) ? document.activeElement : null;
        move();
        if (focused && focused.isConnected && focused !== document.activeElement) {
            try { focused.focus({ preventScroll: true }); } catch (e) { /* refused */ }
        }
    }
    function modalDialog(el) {
        var dialog = el && el.closest ? el.closest('dialog') : null;
        return isModal(dialog) ? dialog : null;
    }
    function raise(el, dialog, move) {
        var entry = raisedEntry(el);
        if (entry && entry.dialog === dialog && isRaised(el)) return;   // already there
        if (entry) lower(entry);
        if (!el || !dialog) return;
        entry = {
            el: el, dialog: dialog, home: null,
            attr: el.getAttribute('popover'),
            top: el.style.top, left: el.style.left, width: el.style.width
        };
        if (move && !dialog.contains(el) && el.parentNode) {
            entry.home = document.createComment('sedna-spotlight');
            el.parentNode.insertBefore(entry.home, el);
            keepingFocus(el, function () { dialog.appendChild(el); });
        }
        el.setAttribute('popover', 'manual');
        try { el.showPopover(); } catch (e) { /* not connected, or already showing */ }
        raised.push(entry);
    }
    function lower(entry) {
        var at = raised.indexOf(entry);
        if (at >= 0) raised.splice(at, 1);
        var el = entry.el;
        try { if (isRaised(el)) el.hidePopover(); } catch (e) { /* already hidden */ }
        if (entry.attr === null) el.removeAttribute('popover');
        else el.setAttribute('popover', entry.attr);
        if (entry.home) {
            if (entry.dialog.contains(el)) {
                keepingFocus(el, function () {
                    if (entry.home.parentNode) entry.home.parentNode.insertBefore(el, entry.home);
                    else el.remove();
                });
            }
            if (entry.home.parentNode) entry.home.parentNode.removeChild(entry.home);
        }
        el.style.top = entry.top;
        el.style.left = entry.left;
        el.style.width = entry.width;
    }
    function lowerOne(el) {
        var entry = el ? raisedEntry(el) : null;
        if (entry) lower(entry);
    }
    function sweep() {
        for (var i = raised.length - 1; i >= 0; i--) {
            if (!isModal(raised[i].dialog)) lower(raised[i]);
        }
        if (!isModal(stage)) stage = null;
    }
    document.addEventListener('close', function () { if (raised.length) sweep(); }, true);
    function allowed(node) {
        if (!lock) return true;
        if (!(node instanceof Node)) return false;
        var regions = toArray(ALWAYS).concat(toArray(lock.allow));
        for (var i = 0; i < regions.length; i++) {
            if (!regions[i].contains(node)) continue;
            if (lock.block && node instanceof Element && node.closest(lock.block)) return false;
            return true;
        }
        return false;
    }
    function mark(el) {
        if (marked && marked !== el) marked.classList.remove('spotlight-allowed');
        marked = el || null;
        if (marked) marked.classList.add('spotlight-allowed');
    }
    function guardPointer(e) {
        if (!allowed(e.target)) { e.stopPropagation(); e.preventDefault(); }
    }
    function typing(el) {
        if (el.isContentEditable) return true;
        if (el.tagName === 'TEXTAREA') return true;
        if (el.tagName !== 'INPUT') return false;
        var type = (el.getAttribute('type') || 'text').toLowerCase();
        return type !== 'button' && type !== 'submit' && type !== 'reset'
            && type !== 'checkbox' && type !== 'radio' && type !== 'image' && type !== 'file';
    }
    function guardKey(e) {
        if (e.key !== 'Enter' && e.key !== ' ') return;
        var t = e.target;
        if (!(t instanceof Element)) return;
        if (e.key === ' ' && typing(t)) return;
        if (!t.closest('button, a, input, select, textarea, [tabindex]')) return;
        if (!allowed(t)) { e.stopPropagation(); e.preventDefault(); }
    }
    ui.spotlight = {
        at: function (hole, target, options) {
            hole = one(hole);
            if (!hole) return null;
            var opts = options || {};
            var pad = (typeof opts.pad === 'number') ? opts.pad : 4;
            var targets = resolve(target, opts.include);
            sweep();
            if (!targets.length) {
                stage = null;
                raise(hole, null, false);
                return null;
            }
            stage = null;
            for (var t = 0; !stage && t < targets.length; t++) stage = modalDialog(targets[t]);
            raise(hole, stage, false);
            if (opts.scroll) {
                var first = targets[0];
                var b0 = first.getBoundingClientRect();
                if (b0.bottom < 0 || b0.top > window.innerHeight || b0.right < 0 || b0.left > window.innerWidth
                    || opts.scroll === 'center') {
                    first.scrollIntoView({ block: opts.scroll === 'center' ? 'center' : 'nearest', inline: 'nearest' });
                }
            }
            var box = union(targets);
            var origin = offsetParentRect(hole);
            var z = zoomOf(hole);
            var rect = {
                top: (box.top - origin.top) / z - pad,
                left: (box.left - origin.left) / z - pad,
                width: box.width / z + pad * 2,
                height: box.height / z + pad * 2
            };
            hole.style.top = rect.top + 'px';
            hole.style.left = rect.left + 'px';
            hole.style.width = rect.width + 'px';
            hole.style.height = rect.height + 'px';
            hole.style.borderRadius = targets.length === 1
                ? getComputedStyle(targets[0]).borderRadius
                : '';
            return rect;
        },
        tipAt: function (tip, rect, options) {
            tip = one(tip);
            if (!tip || !rect) return null;
            sweep();
            raise(tip, stage, true);
            var opts = (typeof options === 'number') ? { gap: options } : (options || {});
            var gap = (typeof opts.gap === 'number') ? opts.gap : 12;
            var margin = (typeof opts.margin === 'number') ? opts.margin : 10;
            tip.style.width = '';
            tip.style.left = '0px';
            tip.style.top = '0px';
            var z = zoomOf(tip);
            var probe = tip.getBoundingClientRect();
            var dx = probe.left / z, dy = probe.top / z;
            var w = probe.width / z;
            tip.style.width = w + 'px';
            var h = tip.getBoundingClientRect().height / z;
            var edge = one(opts.boundary);
            var vb = edge ? edge.getBoundingClientRect()
                          : { left: 0, top: 0, right: window.innerWidth, bottom: window.innerHeight };
            var vp = { left: vb.left / z, top: vb.top / z, right: vb.right / z, bottom: vb.bottom / z };
            var r = {
                left: rect.left + dx,
                top: rect.top + dy,
                right: rect.left + dx + rect.width,
                bottom: rect.top + dy + rect.height,
                width: rect.width,
                height: rect.height
            };
            var space = {
                top: r.top - vp.top, bottom: vp.bottom - r.bottom,
                left: r.left - vp.left, right: vp.right - r.right
            };
            var opposite = { top: 'bottom', bottom: 'top', left: 'right', right: 'left' };
            var side = opts.placement || 'bottom';
            if (side === 'auto') {
                side = ['right', 'left', 'bottom', 'top'].sort(function (a, b) {
                    return space[b] - space[a];
                })[0];
            }
            var need = ((side === 'left' || side === 'right') ? w : h) + gap + margin;
            if (space[side] < need && space[opposite[side]] > space[side]) side = opposite[side];
            var x, y;
            if (side === 'left') { x = r.left - w - gap; y = r.top + r.height / 2 - h / 2; }
            else if (side === 'right') { x = r.right + gap; y = r.top + r.height / 2 - h / 2; }
            else if (side === 'top') { x = r.left + r.width / 2 - w / 2; y = r.top - h - gap; }
            else { x = r.left + r.width / 2 - w / 2; y = r.bottom + gap; }
            x = Math.max(vp.left + margin, Math.min(x, vp.right - w - margin));
            y = Math.max(vp.top + margin, Math.min(y, vp.bottom - h - margin));
            tip.style.left = Math.round(x - dx) + 'px';
            tip.style.top = Math.round(y - dy) + 'px';
            return side;
        },
        follow: function (hole, target, options) {
            var opts = options || {};
            var lockOpts = opts.lock === true ? {} : (opts.lock || null);
            var root = one(opts.root) || document.body;
            var raf = 0, stopped = false, observer = null, scrolled = false;
            function place() {
                if (stopped) return null;
                var box = one(hole);
                var rect = ui.spotlight.at(box, target, scrolled ? Object.assign({}, opts, { scroll: null }) : opts);
                scrolled = true;
                if (!rect) {
                    if (box) box.style.display = 'none';
                    if (lockOpts) mark(null);
                    handle.side = null;
                    return null;
                }
                box.style.display = '';
                if (lockOpts) mark(lockOpts.interactive ? resolve(target)[0] : null);
                handle.side = opts.tip ? ui.spotlight.tipAt(opts.tip, rect, opts) : null;
                return handle.side;
            }
            function schedule() {
                if (raf || stopped) return;
                raf = requestAnimationFrame(function () { raf = 0; place(); });
            }
            function onDialog(e) {
                if (e.target && e.target.tagName === 'DIALOG') schedule();
            }
            var handle = {
                side: null,
                update: function () { return place(); },
                stop: function () {
                    if (stopped) return;
                    stopped = true;
                    if (raf) { cancelAnimationFrame(raf); raf = 0; }
                    window.removeEventListener('scroll', schedule, true);
                    window.removeEventListener('resize', schedule);
                    document.removeEventListener('toggle', onDialog, true);
                    document.removeEventListener('close', onDialog, true);
                    document.removeEventListener('animationend', onDialog, true);
                    document.removeEventListener('transitionend', onDialog, true);
                    if (observer) { observer.disconnect(); observer = null; }
                    if (lockOpts) ui.spotlight.unlock();
                    var box = one(hole);
                    lowerOne(box);
                    lowerOne(one(opts.tip));
                    if (box) box.style.display = 'none';
                    handle.side = null;
                }
            };
            if (lockOpts) ui.spotlight.lock(lockOpts);
            window.addEventListener('scroll', schedule, true);
            window.addEventListener('resize', schedule);
            document.addEventListener('toggle', onDialog, true);
            document.addEventListener('close', onDialog, true);
            document.addEventListener('animationend', onDialog, true);
            document.addEventListener('transitionend', onDialog, true);
            observer = new MutationObserver(schedule);
            observer.observe(root, { childList: true, subtree: true });
            place();
            return handle;
        },
        lock: function (options) {
            var o = options || {};
            var already = !!lock;
            lock = {
                allow: o.allow || null,
                block: o.block === undefined ? '[data-spotlight-block]' : o.block,
                gate: already ? lock.gate : null
            };
            if (already) return;
            document.body.classList.add('spotlight-lock');
            document.addEventListener('pointerdown', guardPointer, true);
            document.addEventListener('click', guardPointer, true);
            document.addEventListener('keydown', guardKey, true);
            if (ui.tips) {
                var previous = ui.tips.gate;
                lock.gate = previous;
                ui.tips.gate = function (el) {
                    if (!allowed(el)) return false;
                    return typeof previous === 'function' ? previous(el) : true;
                };
            }
        },
        unlock: function () {
            if (!lock) return;
            if (ui.tips) ui.tips.gate = lock.gate;
            lock = null;
            mark(null);
            document.body.classList.remove('spotlight-lock');
            document.removeEventListener('pointerdown', guardPointer, true);
            document.removeEventListener('click', guardPointer, true);
            document.removeEventListener('keydown', guardKey, true);
        }
    };
})(window.sednaUi);
(function (ui) {
    var supported = false;
    try { supported = !!(window.CSS && CSS.supports('overlay', 'auto')); } catch (e) { /* no CSS.supports */ }
    if (supported) return;
    var HELD = 'data-sedna-closing';
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
    document.addEventListener('toggle', function (e) {
        var d = e.target;
        if (!d || d.tagName !== 'DIALOG' || !d.open || !d.hasAttribute(HELD)) return;
        d.getAnimations().forEach(function (a) { a.cancel(); });
        release(d);
    }, true);
})(window.sednaUi);
(function (ui) {
    var IDLE_CAP_MS = 1000;
    var pending = new WeakMap();
    function dialogById(id, verb) {
        var el = document.getElementById(id);
        if (!el) {
            console.warn('sednaUi.modal.' + verb + ': no element with id "' + id + '".');
            return null;
        }
        if (el.tagName !== 'DIALOG') {
            console.warn('sednaUi.modal.' + verb + ': #' + id + ' is a <' +
                el.tagName.toLowerCase() + '>, not a <dialog>. Only a <dialog> has showModal().');
            return null;
        }
        return el;
    }
    function watch(d) {
        return new Promise(function (resolve) {
            var done = false;
            var attrs = new MutationObserver(check);
            var tree = new MutationObserver(check);
            function check() {
                if (done || (d.open && d.isConnected)) return;
                done = true;
                attrs.disconnect();
                tree.disconnect();
                pending.delete(d);
                resolve(!d.open && d.returnValue ? d.returnValue : null);
            }
            attrs.observe(d, { attributes: true, attributeFilter: ['open'] });
            tree.observe(document, { childList: true, subtree: true });
        });
    }
    ui.modal = {
        show: function (id) {
            var d = dialogById(id, 'show');
            if (!d) return Promise.resolve(null);
            if (!d.open) {
                d.returnValue = '';
                try { if (d.hasAttribute('data-modeless')) d.show(); else d.showModal(); }
                catch (e) {
                    console.warn('sednaUi.modal.show: ' + e.message);
                    return Promise.resolve(null);
                }
            }
            var waiting = pending.get(d);
            if (!waiting) {
                waiting = watch(d);
                pending.set(d, waiting);
            }
            return waiting;
        },
        close: function (id, value) {
            var d = dialogById(id, 'close');
            if (!d || !d.open) return;
            try {
                if (value === undefined || value === null) d.close();
                else d.close(String(value));
            } catch (e) { console.warn('sednaUi.modal.close: ' + e.message); }
        },
        idle: function (id) {
            var d = document.getElementById(id);
            if (!d || d.open || typeof d.getAnimations !== 'function') return Promise.resolve();
            if (document.visibilityState === 'hidden') return Promise.resolve();
            if (ui._.slideOut) ui._.slideOut(d);
            getComputedStyle(d).display;
            var running = d.getAnimations().map(function (a) {
                return a.finished.catch(function () { /* cancelled is finished too */ });
            });
            if (!running.length) return Promise.resolve();
            return Promise.race([
                Promise.all(running),
                new Promise(function (r) { setTimeout(r, IDLE_CAP_MS); })
            ]).then(function () { });
        }
    };
    function guardedTarget(target) {
        var d = target instanceof Element ? target.closest('dialog') : null;
        if (!d) {
            var open = document.querySelectorAll('dialog:modal');
            d = open.length === 1 ? open[0] : null;
        }
        return d && d.hasAttribute('data-close-guard') && d.matches(':modal') &&
            typeof d.requestClose === 'function' ? d : null;
    }
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || e.defaultPrevented || e.isComposing) return;
        if (document.querySelector(':popover-open:not([popover="manual"])')) return;
        var d = guardedTarget(e.target);
        if (!d) return;
        e.preventDefault();
        d.requestClose();
    });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape' || e.defaultPrevented || e.isComposing) return;
        var d = e.target instanceof Element ? e.target.closest('dialog[data-modeless][open]') : null;
        if (!d || d.matches(':modal')) return;
        if (document.querySelector(':popover-open:not([popover="manual"])')) return;
        e.preventDefault();
        if (typeof d.requestClose === 'function') d.requestClose(); else d.close();
    });
    function syncToggles(d) {
        if (!d.id) return;
        var toggles = document.querySelectorAll('[aria-controls][aria-expanded]');
        for (var i = 0; i < toggles.length; i++) {
            var names = (toggles[i].getAttribute('aria-controls') || '').split(/\s+/);
            if (names.indexOf(d.id) < 0) continue;
            var value = d.open ? 'true' : 'false';
            if (toggles[i].getAttribute('aria-expanded') !== value) toggles[i].setAttribute('aria-expanded', value);
        }
    }
    if (typeof MutationObserver === 'function') {
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                if (records[i].target.tagName === 'DIALOG') syncToggles(records[i].target);
            }
        }).observe(document.documentElement, { attributes: true, attributeFilter: ['open'], subtree: true });
    }
    document.addEventListener('cancel', function (e) {
        var d = e.target;
        if (d instanceof Element && d.tagName === 'DIALOG' && d.hasAttribute('data-close-guard')) {
            e.preventDefault();
        }
    }, true);
    window.addEventListener('beforeunload', function (e) {
        if (!document.querySelector('[data-close-guard][data-dirty]')) return;
        e.preventDefault();
        e.returnValue = true;
    });
})(window.sednaUi);
(function (ui) {
    var here = (function () {
        try { return document.currentScript && document.currentScript.src ? document.currentScript.src : null; }
        catch (e) { return null; }
    })();
    var surfaces = [];
    var held = false;
    var holdTimer = 0;
    var booted = false;
    function interactiveMarkers() {
        try {
            var walker = document.createTreeWalker(document.documentElement, NodeFilter.SHOW_COMMENT);
            for (var c = walker.nextNode(); c; c = walker.nextNode()) {
                if (c.data.indexOf('Blazor:') === 0 && /"type":"(server|webassembly|auto)"/.test(c.data)) return true;
            }
        } catch (e) { /* no walker: nothing to wait for */ }
        return false;
    }
    function release() {
        if (!held) return;
        clearTimeout(holdTimer);
        var quiet = 0, since = Date.now();
        var settle = new MutationObserver(function () {
            clearTimeout(quiet);
            quiet = setTimeout(done, 60);
            if (Date.now() - since > 1000) done();
        });
        function done() {
            if (!held) return;
            held = false;
            settle.disconnect();
            clearTimeout(quiet);
            surfaces.forEach(function (s) { s.scan(document); });
        }
        settle.observe(document.documentElement, { childList: true, subtree: true });
        quiet = setTimeout(done, 60);
    }
    function boot() {
        if (booted) return;
        booted = true;
        if (interactiveMarkers()) {
            held = true;
            holdTimer = setTimeout(release, 4000);
        }
        surfaces.forEach(function (s) { s.scan(document); });
        if (typeof MutationObserver !== 'function') return;
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes;
                for (var j = 0; j < added.length; j++) {
                    if (added[j].nodeType === 1) surfaces.forEach(function (s) { s.scan(added[j]); });
                }
                var removed = records[i].removedNodes;
                for (var k = 0; k < removed.length; k++) {
                    if (removed[k].nodeType === 1) surfaces.forEach(function (s) { s.forget(removed[k]); });
                }
                if (removed.length) surfaces.forEach(function (s) { s.sweepSoon(); });
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }
    function surface(spec) {
        var loading = null;
        var seen = '__sednaSeen:' + spec.module;
        var started = '__sednaStarted:' + spec.module;
        var queued = false;
        function load() {
            if (!loading) {
                var url = new URL(spec.module, here || document.baseURI).href;
                loading = import(url).catch(function (e) {
                    loading = null;
                    throw e;
                });
            }
            return loading;
        }
        var watching = typeof IntersectionObserver === 'function'
            ? new IntersectionObserver(function (entries) {
                entries.forEach(function (entry) {
                    if (entry.isIntersecting) {
                        watching.unobserve(entry.target);
                        start(entry.target).catch(report);
                    }
                });
            }, { rootMargin: '100% 0px' })
            : null;
        function start(el) {
            el[started] = true;
            return load().then(function (m) { return m.attach(el); });
        }
        function report(e) {
            try { console.error('Sedna.UI ' + (spec.name || spec.module) + ':', e); } catch (x) { /* ignore */ }
        }
        function within(root) {
            var found = [];
            if (root.matches && root.matches(spec.selector)) found.push(root);
            if (root.querySelectorAll) {
                var inner = root.querySelectorAll(spec.selector);
                for (var i = 0; i < inner.length; i++) found.push(inner[i]);
            }
            return found;
        }
        var self = {
            load: load,
            report: report,
            scan: function (root) {
                var found = within(root);
                if (spec.found) found.forEach(spec.found);
                if (held) return;
                found.forEach(function (el) {
                    if (el[seen]) return;
                    el[seen] = true;
                    if (watching && !(spec.eager && el.hasAttribute(spec.eager))) watching.observe(el);
                    else start(el).catch(report);
                });
            },
            forget: function (root) {
                if (!watching) return;
                within(root).forEach(function (el) {
                    if (el[started]) return;
                    watching.unobserve(el);
                    el[seen] = false;
                });
            },
            sweepSoon: function () {
                if (queued || !loading) return;
                queued = true;
                setTimeout(function () {
                    queued = false;
                    loading.then(function (m) { m.sweep(); }).catch(function () { /* nothing to sweep */ });
                }, 0);
            },
            get: function (target) {
                var el = typeof target === 'string' ? document.getElementById(target) : target;
                if (!el || !el.matches || !el.matches(spec.selector)) {
                    return Promise.reject(new Error('No ' + spec.selector + ' element: ' + target));
                }
                el[seen] = true;
                if (watching) watching.unobserve(el);
                return start(el);
            },
            init: function (root) {
                var list = within(root || document);
                return Promise.all(list.map(self.get)).then(function (all) { return all.length; });
            },
            release: release
        };
        surfaces.push(self);
        if (booted) self.scan(document);
        return self;
    }
    ui._.surface = surface;
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', boot);
    else setTimeout(boot, 0);
})(window.sednaUi);
(function (ui) {
    var SELECTOR = '[data-chart]';
    var charts = ui._.surface({
        name: 'chart',
        selector: SELECTOR,
        module: 'Sedna.UI.chart.js',
        eager: 'data-chart-eager',
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'application');
            if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex', '0');
        }
    });
    function chartOf(el) {
        var named = el.closest('[data-chart-for]');
        if (named && named.getAttribute('data-chart-for')) {
            var target = document.getElementById(named.getAttribute('data-chart-for'));
            if (target && target.matches(SELECTOR)) return target;
        }
        var inside = el.closest(SELECTOR);
        if (inside) return inside;
        var frame = el.closest('[data-chart-frame]');
        return frame ? frame.querySelector(SELECTOR) : null;
    }
    ui.chart = {
        get: charts.get,
        init: charts.init
    };
    document.addEventListener('click', function (e) {
        var key = e.target && e.target.closest ? e.target.closest('[data-chart-toggle]') : null;
        if (!key) return;
        var el = chartOf(key);
        if (!el) return;
        var shown = key.getAttribute('aria-pressed') === 'false';
        key.setAttribute('aria-pressed', shown ? 'true' : 'false');
        var name = key.getAttribute('data-chart-toggle');
        charts.get(el).then(function (chart) { chart.toggle(name, shown); }).catch(charts.report);
    });
})(window.sednaUi);
(function (ui) {
    var SELECTOR = '[data-graph]';
    var CONTROLS = '[data-graph-action],[data-graph-filter],[data-graph-show],[data-graph-search],[data-graph-option]';
    var graphs = ui._.surface({
        name: 'graph',
        selector: SELECTOR,
        module: 'Sedna.UI.graph.js',
        eager: 'data-graph-eager',
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'application');
            if (!el.hasAttribute('tabindex')) el.setAttribute('tabindex', '0');
        }
    });
    function graphOf(el) {
        if (!el || !el.closest) return null;
        if (el.matches(SELECTOR)) return el;
        var named = el.closest('[data-graph-for]');
        if (named && named.getAttribute('data-graph-for')) {
            var target = document.getElementById(named.getAttribute('data-graph-for'));
            if (target) return target;
        }
        var inside = el.closest(SELECTOR);
        if (inside) return inside;
        var frame = el.closest('[data-graph-frame]');
        return frame ? frame.querySelector(SELECTOR) : null;
    }
    ui.graph = {
        release: graphs.release,
        init: graphs.init,
        get: graphs.get,
        invoke: function (id, method, args) {
            var none = method === 'export' ? new Uint8Array(0) : null;
            var el = document.getElementById(id);
            if (!el || !el.matches(SELECTOR)) {
                try { console.warn('Sedna.UI graph: no [data-graph] element with id "' + id + '".'); } catch (e) { /* ignore */ }
                return Promise.resolve(none);
            }
            return graphs.get(el)
                .then(function (graph) { return graph.invoke(method, args || []); })
                .catch(function (e) {
                    graphs.report(e);
                    return none;
                });
        }
    };
    function control(e) {
        var c = e.target && e.target.closest ? e.target.closest(CONTROLS) : null;
        if (!c) return;
        var typing = c.hasAttribute('data-graph-search');
        if (e.type === 'input' && !typing && c.type !== 'range') return;
        if (e.type === 'change' && typing) return;
        if (e.type === 'click' && (c.tagName === 'INPUT' || c.tagName === 'SELECT' || c.tagName === 'TEXTAREA')) return;
        var el = graphOf(c);
        if (!el) return;
        graphs.get(el).then(function (graph) { graph.control(c, e.type); }).catch(graphs.report);
    }
    document.addEventListener('click', control);
    document.addEventListener('change', control);
    document.addEventListener('input', control);
})(window.sednaUi);
(function (ui) {
    var SELECTOR = '[data-editor]';
    var editors = ui._.surface({
        name: 'editor',
        selector: SELECTOR,
        module: 'Sedna.UI.editor.js',
        eager: 'data-editor-eager'
    });
    ui.editor = {
        release: editors.release,
        init: editors.init,
        get: editors.get,
        invoke: function (id, method, args) {
            var el = document.getElementById(id);
            if (!el || !el.matches(SELECTOR)) {
                try { console.warn('Sedna.UI editor: no [data-editor] element with id "' + id + '".'); } catch (e) { /* ignore */ }
                return Promise.resolve(null);
            }
            return editors.get(el)
                .then(function (editor) { return editor.invoke(method, args || []); })
                .catch(function (e) {
                    editors.report(e);
                    return null;
                });
        }
    };
})(window.sednaUi);
(function (ui) {
    ui._.surface({
        name: 'orb',
        selector: '[data-orb]',
        module: 'Sedna.UI.orb.js',
        eager: 'data-orb-eager',
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'img');
        }
    });
})(window.sednaUi);
(function (ui) {
    if (typeof IntersectionObserver !== 'function') return;
    var seen = new IntersectionObserver(function (entries) {
        for (var i = 0; i < entries.length; i++) {
            entries[i].target.toggleAttribute('data-mochi-away', !entries[i].isIntersecting);
        }
    }, { rootMargin: '120px' });
    function within(root, fn) {
        if (root.classList && root.classList.contains('mochi')) fn(root);
        if (!root.querySelectorAll) return;
        var all = root.querySelectorAll('.mochi');
        for (var i = 0; i < all.length; i++) fn(all[i]);
    }
    function watch(el) {
        if (el.__sednaMochiSeen) return;
        el.__sednaMochiSeen = true;
        seen.observe(el);
    }
    function forget(el) {
        if (!el.__sednaMochiSeen || el.isConnected) return;
        el.__sednaMochiSeen = false;
        seen.unobserve(el);
    }
    function start() {
        within(document, watch);
        if (typeof MutationObserver !== 'function') return;
        new MutationObserver(function (records) {
            for (var i = 0; i < records.length; i++) {
                var added = records[i].addedNodes, removed = records[i].removedNodes;
                for (var j = 0; j < added.length; j++) if (added[j].nodeType === 1) within(added[j], watch);
                for (var k = 0; k < removed.length; k++) if (removed[k].nodeType === 1) within(removed[k], forget);
            }
        }).observe(document.documentElement, { childList: true, subtree: true });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start);
    else start();
})(window.sednaUi);
(function (ui) {
    ui._.surface({
        name: 'signature',
        selector: '[data-signature]',
        module: 'Sedna.UI.signature.js',
        eager: 'data-signature-eager',
        found: function (el) {
            var pad = el.querySelector('[data-signature-pad]');
            if (pad && !pad.hasAttribute('role')) pad.setAttribute('role', 'img');
        }
    });
})(window.sednaUi);
(function (ui) {
    ui._.surface({
        name: 'qr',
        selector: '[data-qr]',
        module: 'Sedna.UI.qr.js',
        eager: 'data-qr-eager',
        found: function (el) {
            if (!el.hasAttribute('role')) el.setAttribute('role', 'img');
        }
    });
})(window.sednaUi);
(function (ui) {
    var config = ui._.config;
    ui.requestNotify = function () {
        try {
            if (!('Notification' in window)) return Promise.resolve(false);
            if (Notification.permission !== 'default') return Promise.resolve(Notification.permission === 'granted');
            return Promise.resolve(Notification.requestPermission()).then(
                function (answer) { return answer === 'granted'; },
                function () { return false; });
        } catch (e) { return Promise.resolve(false); /* notifications unavailable */ }
    };
    ui.notify = function (title, body) {
        try {
            if ('Notification' in window && Notification.permission === 'granted') {
                var opts = { body: body };
                if (config.notifyIcon) opts.icon = config.notifyIcon;
                new Notification(title, opts);
            }
        } catch (e) { /* ignore */ }
    };
    ui.ping = function () {
        try {
            var ctx = this._audio ||
                (this._audio = new (window.AudioContext || window.webkitAudioContext)());
            if (ctx.state === 'suspended') ctx.resume();
            var t = ctx.currentTime;
            var osc = ctx.createOscillator(), gain = ctx.createGain();
            osc.type = 'sine';
            osc.frequency.setValueAtTime(880, t);
            osc.frequency.setValueAtTime(660, t + 0.12);
            gain.gain.setValueAtTime(0.0001, t);
            gain.gain.exponentialRampToValueAtTime(0.12, t + 0.02);
            gain.gain.exponentialRampToValueAtTime(0.0001, t + 0.3);
            osc.connect(gain); gain.connect(ctx.destination);
            osc.start(t); osc.stop(t + 0.32);
        } catch (e) { /* audio unavailable — the visual notification still fires */ }
    };
})(window.sednaUi);
(function (ui) {
    var ICONS = {
        go: 'ri-check-line',
        warn: 'ri-alert-line',
        danger: 'ri-error-warning-line',
        info: 'ri-information-line',
        brand: 'ri-megaphone-line',
        secret: 'ri-lock-line'
    };
    var host = null;     // our stack, while it exists
    var unwatch = null;  // stops watching for the stack being taken out with a dialog
    var layer = ui._.layer;
    function raise(except) {
        if (!host) return;
        layer.raise(host, except);
        if (unwatch) unwatch();
        unwatch = layer.whenGone(host, function () { unwatch = null; if (host && host.children.length) raise(); });
    }
    function stack() {
        if (host && !host.isConnected && host.children.length) {
            raise();     // taken out with a dialog, toasts and all: put it back
            return host;
        }
        if (!host || !host.isConnected) {
            host = document.createElement('div');
            host.className = 'toast-stack';
            host.setAttribute('data-sedna-toasts', '');
            host.setAttribute('role', 'status');
            host.setAttribute('aria-live', 'polite');
            host.setAttribute('popover', 'manual');
            watchStack(host);
        }
        host.classList.toggle('toast-stack--pile', !!ui._.config.toastPile);
        raise();
        return host;
    }
    var onStack = { hover: false, focus: false };
    function watchStack(el) {
        onStack.hover = onStack.focus = false;
        el.addEventListener('mouseenter', function () { onStack.hover = true; syncAll(); });
        el.addEventListener('mouseleave', function () { onStack.hover = false; syncAll(); });
        el.addEventListener('focusin', function () { onStack.focus = true; syncAll(); });
        el.addEventListener('focusout', function (e) {
            if (!el.contains(e.relatedTarget)) { onStack.focus = false; syncAll(); }
        });
    }
    function drop(el) {
        if (el.parentNode) el.parentNode.removeChild(el);
        if (host && !host.children.length) {
            if (unwatch) { unwatch(); unwatch = null; }
            if (host.parentNode) host.parentNode.removeChild(host);
            host = null;
        }
        limit();
    }
    function entryOf(el) {
        for (var id in live) if (live[id].el === el) return live[id];
        return null;
    }
    function limit() {
        if (!host) return;
        var max = Math.max(0, Math.floor(Number(ui._.config.toastLimit) || 0));
        var kids = host.children;
        for (var i = 0; i < kids.length; i++) {
            var over = max > 0 && i < kids.length - max;
            if (over) kids[i].setAttribute('data-toast-queued', '');
            else kids[i].removeAttribute('data-toast-queued');
            var entry = entryOf(kids[i]);
            if (entry && entry.queued !== over) { entry.queued = over; sync(entry); }
        }
    }
    layer.onChange(function (closing) { if (host && host.children.length) raise(closing); });
    var live = {};       // id -> { el, remove, timer, waiters }
    var ended = {};      // id -> how a toast that has gone ended, for a result() asked late
    var endedOrder = [];
    var REGION_SETTLE_MS = 150;
    var nextId = 1;
    function end(id, value) {
        ended[id] = value;
        endedOrder.push(id);
        if (endedOrder.length > 100) delete ended[endedOrder.shift()];
    }
    function wire(entry, el) {
        el.addEventListener('mouseenter', function () { entry.hover = true; sync(entry); });
        el.addEventListener('mouseleave', function () { entry.hover = false; sync(entry); });
        el.addEventListener('focusin', function () { entry.focus = true; sync(entry); });
        el.addEventListener('focusout', function (e) { if (!el.contains(e.relatedTarget)) { entry.focus = false; sync(entry); } });
        swipe(entry, el);
        el.addEventListener('click', function (e) {
            if (!entry.template) return;
            var t = e.target instanceof Element ? e.target.closest('[data-toast-value], .toast-close') : null;
            if (!t || !el.contains(t)) return;
            entry.remove(t.hasAttribute('data-toast-value') ? t.getAttribute('data-toast-value') : null, true);
        });
        el.addEventListener('click', function (e) {
            if (!entry.swallow) return;
            entry.swallow = false;
            e.preventDefault();
            e.stopImmediatePropagation();
        }, true);
    }
    var SLOP = 8;           // px a press travels before it is a swipe and not a click
    var FLICK = 32;         // px a quick swipe has to travel
    var FLICK_SPEED = 0.5;  // px per ms
    var FAR = 96;           // px that dismiss at any speed, or a third of a narrower toast
    function swipe(entry, el) {
        var s = null;
        function settle(e) {
            if (!s || e.pointerId !== s.id) return;
            var was = s;
            s = null;
            if (!was.swiping) return;
            entry.swallow = true;
            var speed = was.toward / Math.max(1, e.timeStamp - was.t);
            if (e.type === 'pointerup' && (was.toward >= Math.min(FAR, el.offsetWidth / 3) || (was.toward >= FLICK && speed >= FLICK_SPEED))) {
                entry.remove(null);
                return;
            }
            el.removeAttribute('data-toast-swiping');
            el.style.removeProperty('--toast-swipe');
            el.style.removeProperty('--toast-swipe-progress');
        }
        el.addEventListener('pointerdown', function (e) {
            entry.swallow = false;
            if (e.button !== 0 || !e.isPrimary || !el.querySelector('.toast-close')) return;
            s = {
                id: e.pointerId, x: e.clientX, y: e.clientY, t: e.timeStamp, toward: 0, swiping: false,
                dir: getComputedStyle(el).direction === 'rtl' ? -1 : 1
            };
        });
        el.addEventListener('pointermove', function (e) {
            if (!s || e.pointerId !== s.id) return;
            var dx = e.clientX - s.x, dy = e.clientY - s.y;
            if (!s.swiping) {
                if (Math.abs(dy) >= SLOP && Math.abs(dy) > Math.abs(dx)) { s = null; return; }   // a scroll, not a swipe
                if (Math.abs(dx) < SLOP) return;
                s.swiping = true;
                s.t = e.timeStamp;
                s.x = e.clientX;
                dx = 0;
                el.setAttribute('data-toast-swiping', '');
                try { el.setPointerCapture(e.pointerId); } catch (x) { /* a pointer already gone */ }
                try { window.getSelection().removeAllRanges(); } catch (x) { /* nothing selected */ }
            }
            var toward = dx * s.dir;
            s.toward = toward;
            var moved = toward >= 0 ? toward : Math.max(toward / 4, -SLOP * 2);
            el.style.setProperty('--toast-swipe', moved * s.dir + 'px');
            el.style.setProperty('--toast-swipe-progress', String(Math.min(1, Math.max(0, moved) / Math.max(1, el.offsetWidth))));
        });
        el.addEventListener('pointerup', settle);
        el.addEventListener('pointercancel', settle);
    }
    function holding(entry) {
        return !!(entry.hover || entry.focus || entry.queued ||
            (host && entry.el.parentNode === host && host.classList.contains('toast-stack--pile') && (onStack.hover || onStack.focus)));
    }
    function sync(entry) { if (holding(entry)) hold(entry); else release(entry); }
    function syncAll() { for (var id in live) sync(live[id]); }
    function timeOut(entry, opts) {
        clearTimeout(entry.timer);
        entry.timer = 0;
        var ms = !opts || opts.timeout === undefined ? 4000 : opts.timeout;
        entry.left = ms;
        entry.started = Date.now();
        entry.timer = ms > 0 && !entry.held ? setTimeout(function () { entry.remove(null); }, ms) : 0;
    }
    function paint(entry, message, opts) {
        opts = opts || {};
        var el = entry.el;
        var kind = ICONS[opts.kind] ? opts.kind : 'info';
        stack();
        host.setAttribute('aria-live', kind === 'danger' ? 'assertive' : 'polite');
        if (entry.template) {
            var drawn = document.createElement('div');
            wire(entry, drawn);
            entry.el.replaceWith(drawn);
            entry.el = el = drawn;
            entry.template = false;
        }
        el.className = 'toast toast--' + kind;
        el.textContent = '';
        var icon = document.createElement('i');
        icon.className = ICONS[kind];
        icon.setAttribute('aria-hidden', 'true');
        var body = document.createElement('div');
        body.className = 'toast-body';
        if (opts.title) {
            var strong = document.createElement('strong');
            strong.textContent = opts.title;
            body.appendChild(strong);
        }
        body.appendChild(document.createTextNode(message == null ? '' : String(message)));
        el.appendChild(icon);
        el.appendChild(body);
        if (opts.dismissible !== false) {
            var close = document.createElement('button');
            close.type = 'button';
            close.className = 'toast-close';
            close.setAttribute('aria-label', opts.dismissLabel || ui._.config.toastDismissLabel || 'Dismiss');
            var x = document.createElement('i');
            x.className = 'ri-close-line';
            x.setAttribute('aria-hidden', 'true');
            close.appendChild(x);
            close.addEventListener('click', function () { entry.remove(null); });
            el.appendChild(close);
        }
        timeOut(entry, opts);
    }
    function hold(entry) {
        if (entry.held) return;
        entry.held = true;
        if (!entry.timer) return;
        clearTimeout(entry.timer);
        entry.timer = 0;
        entry.left = Math.max(0, entry.left - (Date.now() - entry.started));
    }
    function release(entry) {
        if (!entry.held) return;
        entry.held = false;
        if (entry.left > 0 && !entry.timer) {
            entry.started = Date.now();
            entry.left = Math.max(entry.left, 1000);
            entry.timer = setTimeout(function () { entry.remove(null); }, entry.left);
        }
    }
    function create() {
        var id = nextId++;
        var entry = {
            id: id, el: document.createElement('div'), timer: 0, held: false, left: 0, started: 0, waiters: [], template: false,
            hover: false, focus: false, queued: false, swallow: false
        };
        wire(entry, entry.el);
        entry.remove = function (value, later) {
            if (live[id] !== entry) return;
            clearTimeout(entry.timer);
            delete live[id];
            var el = entry.el;
            if (later === true) setTimeout(function () { drop(el); }, 0);
            else drop(el);
            value = typeof value === 'string' ? value : null;
            end(id, value);
            var waiters = entry.waiters;
            entry.waiters = [];
            for (var i = 0; i < waiters.length; i++) waiters[i](value);
        };
        entry.remove.id = id;
        live[id] = entry;
        return entry;
    }
    function add(entry, fresh) {
        if (fresh) {
            entry.el.hidden = true;
            host.appendChild(entry.el);
            setTimeout(function () { entry.el.hidden = false; }, REGION_SETTLE_MS);
        } else {
            host.appendChild(entry.el);
        }
        limit();
        sync(entry);
    }
    ui.toast = function (message, opts) {
        var entry = create();
        var fresh = !host || !host.isConnected;
        paint(entry, message, opts);
        add(entry, fresh);
        return entry.remove;
    };
    ui.toast.show = function (message, opts) {
        return ui.toast(message, opts).id;
    };
    ui.toast.dismiss = function (id) {
        var entry = live[id];
        if (!entry) return false;
        entry.remove(null);
        return true;
    };
    ui.toast.replace = function (id, message, opts) {
        var entry = live[id];
        if (!entry) return ui.toast.show(message, opts);
        paint(entry, message, opts);
        limit();
        return id;
    };
    ui.toast.present = function (template, data, opts) {
        opts = opts || {};
        var t = typeof template === 'string' ? document.getElementById(template) : template;
        var slots = {}, href = null;
        Object.keys(data || {}).forEach(function (k) {
            if (k === 'href') href = data[k]; else slots[k] = data[k];
        });
        var node = t && t.tagName === 'TEMPLATE' ? ui._.fill(t, slots) : null;
        if (!node) {
            console.warn('sednaUi.toast.present: no <template> with the id ' + template + '. The toast is app markup — see the Toast page.');
            return 0;
        }
        var links = node.matches('[data-href]') ? [node] : [];
        Array.prototype.push.apply(links, node.querySelectorAll('[data-href]'));
        var safe = ui._.safeHref(href);
        links.forEach(function (a) {
            a.removeAttribute('data-href');
            if (safe) a.setAttribute('href', safe); else a.removeAttribute('href');
        });
        var entry = opts.replace ? live[opts.replace] : null;
        var fresh = !host || !host.isConnected;
        if (entry) {
            wire(entry, node);
            entry.el.replaceWith(node);
            entry.el = node;
        } else {
            entry = create();
            entry.el = node;
            wire(entry, node);
        }
        entry.template = true;
        stack();
        host.setAttribute('aria-live', node.matches('.toast--danger') ? 'assertive' : 'polite');
        if (!node.isConnected) add(entry, fresh);
        else limit();
        timeOut(entry, opts);
        return entry.id;
    };
    ui.toast.result = function (id) {
        var entry = live[id];
        if (!entry) return Promise.resolve(Object.prototype.hasOwnProperty.call(ended, id) ? ended[id] : null);
        return new Promise(function (resolve) { entry.waiters.push(resolve); });
    };
})(window.sednaUi);

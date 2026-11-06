/*! Sedna.UI — generated, comments stripped. The commented copy ships in the package as readable/Sedna.UI.js. */
window.sednaUi = window.sednaUi || {};
(function (ui) {
    var config = {
        storagePrefix: 'sedna.',
        notifyIcon: null,
        langCookie: false,
        themeDefault: 'sedna',
        toastDismissLabel: 'Dismiss'
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
    ui._ = { config: config, key: key, readRaw: readRaw, score: score, fill: fill, hold: HOLD, safeHref: safeHref };
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
                compact: g('density') === 'compact',
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
            if (g('density') === 'compact') root.setAttribute('data-density', 'compact');
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
            e.preventDefault();
            e.stopPropagation();
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
            var trigger = panel.id
                ? document.querySelector('[popovertarget="' + panel.id + '"]')
                : null;
            if (stale(scroller, panel, trigger)) {
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
        closePopovers(scroller);
        closeCombos(scroller);
    }, true);
    document.addEventListener('toggle', function (e) {
        var panel = e.target;
        if (!panel || e.newState !== 'open' || !panel.matches || !panel.matches('.popover')) return;
        var trigger = panel.id ? document.querySelector('[popovertarget="' + panel.id + '"]') : null;
        if (!trigger) return;
        ui._.layer.whenGone(trigger, function () {
            try { if (panel.matches(':popover-open')) panel.hidePopover(); } catch (err) { /* already closed */ }
        });
    }, true);
})(window.sednaUi);
(function (ui) {
    function panelOf(toggle) {
        var anchor = toggle.closest('.menu-anchor');
        return anchor ? anchor.querySelector('.menu') : null;
    }
    function setOpen(toggle, panel, open) {
        panel.hidden = !open;
        toggle.setAttribute('aria-expanded', String(open));
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
        if (item) { closeAll(null); return; }
        if (e.target.closest('.menu')) return;
        closeAll(null);
    });
    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;
        var open = document.querySelector('[data-menu-toggle][aria-expanded="true"]');
        if (!open) return;
        closeAll(null);
        try { open.focus(); } catch (err) { /* detached */ }
    });
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
    function listOf(host) { return host.querySelector('.form-combo-list'); }
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
        return options(host).filter(function (o) { return !o.hidden && enabled(o); });
    }
    function stops(host) {
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
        options(host).forEach(function (o) {
            if (o.hidden) return;
            var target = textOf(o);
            rangesIn(target, runs(q, target.textContent)).forEach(function (r) { highlight.add(r); });
        });
        CSS.highlights.set(MATCH, highlight);
    }
    function activeOf(host) {
        var list = listOf(host);
        return list ? list.querySelector('.form-combo-option[data-active], .form-combo-group-action[data-active]') : null;
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
        setActive(host, (!query(host) && chosen) || rows[0] || null, true);
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
                if (value !== null) unselect(host, value);
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
                if (active) { e.preventDefault(); active.click(); return; }
                if (mode === 'free') {
                    if (input.value.trim()) { e.preventDefault(); commit(host, input.value); }
                    return;
                }
                e.preventDefault();
                if (!openNow) open(host);
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
        }
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
            var tab = typeof tabOrPanelId === 'string'
                ? document.querySelector('[role="tab"][aria-controls="' + tabOrPanelId + '"]')
                : tabOrPanelId;
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
    function rank(query) {
        var out = [];
        for (var i = 0; i < commands.length; i++) {
            var c = commands[i];
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
    function render(query) {
        shown = rank(query);
        at = 0;
        list.textContent = '';
        if (!shown.length) {
            var empty = fill(template('empty'), { query: query });
            if (empty) list.appendChild(empty);
            input.removeAttribute('aria-activedescendant');
            return;
        }
        var lastGroup = null;
        shown.forEach(function (c, i) {
            if (!query && c.group && c.group !== lastGroup) {
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
        input.setAttribute('aria-activedescendant', list.id + '-0');
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
        input.addEventListener('input', function () { render(input.value); });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); highlight(at + 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); highlight(at - 1); }
            else if (e.key === 'Home') { e.preventDefault(); highlight(0); }
            else if (e.key === 'End') { e.preventDefault(); highlight(items().length - 1); }
            else if (e.key === 'Enter') { e.preventDefault(); if (items().length) run(at); }
        });
        dialog.addEventListener('click', function (e) { if (e.target === dialog) close(); });
        return true;
    }
    function close() {
        if (dialog && dialog.open) dialog.close();
    }
    ui.palette = {
        register: function (list_) {
            commands = Array.isArray(list_) ? list_.slice() : [];
        },
        open: function () {
            if (!adopt()) return false;
            if (dialog.open) return true;
            input.value = '';
            render('');
            dialog.showModal();
            input.focus();
            return true;
        },
        close: close,
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
    var CONTROL = 'a[href], button, input, select, textarea, label, summary, [contenteditable]';
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
    function axisOf(zone) { return zone.getAttribute('data-drag-axis') || 'y'; }
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
        var zone = back ? drag.from : drag.zone;
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
        item.setAttribute('data-dragging', mode);
        if (carried.length > 1) {
            item.setAttribute('data-drag-count', String(carried.length));
            carried.forEach(function (el) { if (el !== item) el.setAttribute('data-drag-carried', ''); });
        }
        if (mode === 'pointer') lift(item, rect);
        if (ui.tips && ui.tips.hide) ui.tips.hide();
        try { window.getSelection().removeAllRanges(); } catch (e) { /* nothing selected */ }
        if (mode === 'keyboard') {
            target(from, start);
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
        var moved = landed && (zone !== d.from || !d.together || d.index !== d.start);
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
                keyboard: d.mode === 'keyboard'
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
        if (moved && d.mode === 'keyboard') refocus(id, !!handleOf(d.item));
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
                var el = viaHandle ? handleOf(items[i]) || items[i] : items[i];
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
    function move(key) {
        var zone = drag.zone && drag.zone.getAttribute('data-drop-state') === 'valid' ? drag.zone : drag.from;
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
        var handle = handleOf(item);
        return (handle ? el === handle : el === item) ? item : null;
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
        if ((e.key !== ' ' && e.key !== 'Enter') || e.repeat || e.altKey || e.ctrlKey || e.metaKey) return;
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
        var owner = handleOf(drag.item) || drag.item;
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
        return target instanceof Element ? target.closest('.dropzone[data-dropzone]') : null;
    }
    function setOver(zone, on) {
        zone.classList.toggle(OVER, on);
        if (!on) zone[DEPTH] = 0;
    }
    ui.dropzone = {
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
            var transfer = new DataTransfer();
            var files = e.dataTransfer.files;
            var many = input.multiple ? files.length : Math.min(1, files.length);
            for (var i = 0; i < many; i++) transfer.items.add(files[i]);
            input.files = transfer.files;
        } catch (err) {
            return;    // no DataTransfer constructor: the drop simply does nothing
        }
        input.dispatchEvent(new Event('change', { bubbles: true }));
    });
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
    function targetOf(link) {
        var href = link.getAttribute('href');
        if (!href || href.length < 2 || href.charAt(0) !== '#') return null;
        try {
            return document.getElementById(decodeURIComponent(href.slice(1)));
        } catch (e) {
            return null;
        }
    }
    function moveTo(target) {
        target.scrollIntoView();
        target.focus({ preventScroll: true });
        if (document.activeElement === target || target.hasAttribute('tabindex')) return;
        target.setAttribute('tabindex', '-1');
        target.addEventListener('blur', function () {
            target.removeAttribute('tabindex');
        }, { once: true });
        target.focus({ preventScroll: true });
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
        if (target.closest('button, a, input, select, textarea, [contenteditable]')) return null;
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
    document.addEventListener('pointerdown', function (e) {
        if (drag || (e.pointerType === 'mouse' && e.button !== 0)) return;
        var sheet = gripped(e.target);
        if (!sheet) return;
        drag = {
            sheet: sheet,
            id: e.pointerId,
            y0: e.clientY,
            t0: e.timeStamp,
            dy: 0,
            height: sheet.getBoundingClientRect().height
        };
        sheet.classList.add(DRAGGING);
        try { sheet.setPointerCapture(e.pointerId); } catch (err) { /* carry on */ }
        e.preventDefault();
    });
    document.addEventListener('pointermove', function (e) {
        if (!drag || e.pointerId !== drag.id) return;
        drag.dy = Math.max(0, e.clientY - drag.y0);
        drag.sheet.style.transform = 'translateY(' + drag.dy + 'px)';
    });
    function finish(e) {
        if (!drag || e.pointerId !== drag.id) return;
        var d = drag;
        drag = null;
        try { d.sheet.releasePointerCapture(e.pointerId); } catch (err) { /* already gone */ }
        var speed = d.dy / Math.max(1, e.timeStamp - d.t0);
        if (d.dy > d.height * DISTANCE || (speed > FLICK && d.dy > FLICK_FLOOR)) slideOut(d.sheet);
        else springBack(d.sheet);
    }
    document.addEventListener('pointerup', finish);
    document.addEventListener('pointercancel', finish);
})(window.sednaUi);
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
            var box = union(targets);
            var origin = offsetParentRect(hole);
            var rect = {
                top: box.top - origin.top - pad,
                left: box.left - origin.left - pad,
                width: box.width + pad * 2,
                height: box.height + pad * 2
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
            var probe = tip.getBoundingClientRect();
            var dx = probe.left, dy = probe.top;
            var w = probe.width;
            tip.style.width = w + 'px';
            var h = tip.getBoundingClientRect().height;
            var edge = one(opts.boundary);
            var vp = edge ? edge.getBoundingClientRect()
                          : { left: 0, top: 0, right: window.innerWidth, bottom: window.innerHeight };
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
            var raf = 0, stopped = false, observer = null;
            function place() {
                if (stopped) return null;
                var box = one(hole);
                var rect = ui.spotlight.at(box, target, opts);
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
                try { d.showModal(); }
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
    document.addEventListener('cancel', function (e) {
        var d = e.target;
        if (d instanceof Element && d.tagName === 'DIALOG' && d.hasAttribute('data-close-guard')) {
            e.preventDefault();
        }
    }, true);
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
        if (e.type === 'input' && !typing) return;
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
    var config = ui._.config;
    ui.requestNotify = function () {
        try {
            if ('Notification' in window && Notification.permission === 'default') {
                Notification.requestPermission();
            }
        } catch (e) { /* notifications unavailable */ }
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
        }
        raise();
        return host;
    }
    function drop(el) {
        if (el.parentNode) el.parentNode.removeChild(el);
        if (host && !host.children.length) {
            if (unwatch) { unwatch(); unwatch = null; }
            if (host.parentNode) host.parentNode.removeChild(host);
            host = null;
        }
    }
    layer.onChange(function (closing) { if (host && host.children.length) raise(closing); });
    var live = {};       // id -> { el, remove, timer }
    var REGION_SETTLE_MS = 150;
    var nextId = 1;
    function paint(entry, message, opts) {
        opts = opts || {};
        var el = entry.el;
        var kind = ICONS[opts.kind] ? opts.kind : 'info';
        stack();
        host.setAttribute('aria-live', kind === 'danger' ? 'assertive' : 'polite');
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
            close.addEventListener('click', entry.remove);
            el.appendChild(close);
        }
        clearTimeout(entry.timer);
        entry.timer = 0;
        var ms = opts.timeout === undefined ? 4000 : opts.timeout;
        entry.left = ms;
        entry.started = Date.now();
        entry.timer = ms > 0 && !entry.held ? setTimeout(entry.remove, ms) : 0;
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
            entry.timer = setTimeout(entry.remove, entry.left);
        }
    }
    ui.toast = function (message, opts) {
        var id = nextId++;
        var entry = { el: document.createElement('div'), timer: 0, held: false, left: 0, started: 0 };
        entry.el.addEventListener('mouseenter', function () { hold(entry); });
        entry.el.addEventListener('mouseleave', function () { if (!entry.el.contains(document.activeElement)) release(entry); });
        entry.el.addEventListener('focusin', function () { hold(entry); });
        entry.el.addEventListener('focusout', function (e) { if (!entry.el.contains(e.relatedTarget) && !entry.el.matches(':hover')) release(entry); });
        entry.remove = function () {
            clearTimeout(entry.timer);
            delete live[id];
            drop(entry.el);
        };
        entry.remove.id = id;
        live[id] = entry;
        var fresh = !host || !host.isConnected;
        paint(entry, message, opts);
        if (fresh) {
            entry.el.hidden = true;
            host.appendChild(entry.el);
            setTimeout(function () { entry.el.hidden = false; }, REGION_SETTLE_MS);
        } else {
            host.appendChild(entry.el);
        }
        return entry.remove;
    };
    ui.toast.show = function (message, opts) {
        return ui.toast(message, opts).id;
    };
    ui.toast.dismiss = function (id) {
        var entry = live[id];
        if (!entry) return false;
        entry.remove();
        return true;
    };
    ui.toast.replace = function (id, message, opts) {
        var entry = live[id];
        if (!entry) return ui.toast.show(message, opts);
        paint(entry, message, opts);
        return id;
    };
})(window.sednaUi);

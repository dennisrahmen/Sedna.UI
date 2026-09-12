/* ── Combo field, delegated ──────────────────────────────────────────────────
   The keyboard, the open state and the filtering behind `.form-combo`. Add
   data-combo to the field, naming what it picks:

     data-combo="single"    one value; picking closes the list
     data-combo="multi"     chips; the list stays open while you pick
     data-combo="free"      what is typed becomes a chip; suggestions are optional

     data-combo-source="server"   the app filters, so the script does not hide rows
     data-combo-managed           the script keeps the selection itself (see below)

   THE SELECTION IS THE PAGE'S. In a Blazor app the chips and the options render
   from the app's state, and this file turns key presses into the events that app
   already handles:

     Enter on an option        clicks it
     Backspace, twice          clicks the last chip's .chip-dismiss
     free entry committed      one `change` on the input per entry, then clears it
     the list scrolled down    clicks the .form-combo-more after it

   So `@onclick` on an option and a chip's dismiss button, `@onchange` on a free
   entry's input and `@bind:event="oninput"` for the query are the whole
   integration. The script adds, removes and selects nothing in that markup —
   Blazor holds references to it, and a node this file inserted or removed would
   break its next render.

   What it DOES write is attributes the app never renders: aria-expanded and
   aria-activedescendant on the input, `hidden` on a filtered option, data-active
   on the keyboard's option, data-armed on a chip. None of those is in Blazor's
   render tree, so a diff never reverts them. The match highlight is painted
   through the CSS Custom Highlight API and touches no text node at all.

   data-combo-managed is for a page with no app behind it — a static form, this
   catalogue. The script then selects options, fills `.form-combo-value` and the
   `input[data-combo-field]` of a single select, and builds each chip from the
   `<template data-combo-chip>` in the field, putting the value into any hidden
   input the template holds so the form posts it. `{label}` in the template's
   attributes is replaced with the chip's label. Leave data-combo-managed off
   wherever C# renders the chips: both would add one.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var score = ui._.score;
    var MATCH = 'sedna-combo-match';

    /* An address has one @ and a dot in its domain. The browser's own `type=email`
       check accepts `ops@example`, which is exactly the typo a recipient field has
       to catch. */
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

    function reachable(host) {
        return options(host).filter(function (o) {
            return !o.hidden && o.getAttribute('aria-disabled') !== 'true';
        });
    }

    function chips(host) {
        var box = boxOf(host);
        return box ? toArray(box.querySelectorAll(':scope > .chip')) : [];
    }

    /* The text an option is matched on. `.form-combo-option-text` when the row has
       one, so a trailing code or count is not searched as if it were the name. */
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

    /* Sets the query and says so, so a binding on the input — Blazor's included —
       sees the same value the reader does. */
    function setQuery(input, text) {
        if (input.value === text) return;
        input.value = text;
        quiet = true;
        try { input.dispatchEvent(new Event('input', { bubbles: true })); }
        finally { quiet = false; }
    }

    // ── Filtering and the highlight ────────────────────────────────────────

    function filter(host) {
        if (!fromServer(host)) {
            var q = query(host);
            options(host).forEach(function (o) {
                var hide = !!q && score(q, labelOf(o), 0) < 0;
                if (o.hidden !== hide) o.hidden = hide;
            });

            // A group heading goes when every row under it has.
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
        paint(host);
    }

    /* Where the query sits in a label, as [start, end) runs: the substring when
       there is one, otherwise each character of the subsequence `score` found. */
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

    // ── The keyboard's option ──────────────────────────────────────────────

    function activeOf(host) {
        var list = listOf(host);
        return list ? list.querySelector('.form-combo-option[data-active]') : null;
    }

    function setActive(host, option, scroll) {
        var input = inputOf(host);
        options(host).forEach(function (o) {
            if (o !== option && o.hasAttribute('data-active')) o.removeAttribute('data-active');
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

    /* Keeps the keyboard's option valid after a filter or a render. A free entry
       has none until an arrow key picks one, so Enter adds what was typed. */
    function settle(host, fresh) {
        var active = activeOf(host), rows = reachable(host);
        if (!fresh && active && rows.indexOf(active) >= 0) { setActive(host, active); return; }
        if (modeOf(host) === 'free') { setActive(host, null); return; }

        var chosen = rows.filter(function (o) { return o.getAttribute('aria-selected') === 'true'; })[0];
        setActive(host, (!query(host) && chosen) || rows[0] || null, true);
    }

    function move(host, step) {
        var rows = reachable(host);
        if (!rows.length) return;
        var at = rows.indexOf(activeOf(host));
        var to = at < 0 ? (step > 0 ? 0 : rows.length - 1) : Math.max(0, Math.min(rows.length - 1, at + step));
        setActive(host, rows[to], true);
        requestMore(host);
    }

    // ── Open and closed ────────────────────────────────────────────────────

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

    // ── Chips ──────────────────────────────────────────────────────────────

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

    /* A managed field's own selection. Only ever reached with data-combo-managed —
       an app's markup is the app's. */
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
        if (managed(host)) selectManaged(host, option);

        if (modeOf(host) === 'multi') {
            setActive(host, option);
            if (input.value) setQuery(input, '');
            input.focus();
            return;
        }
        setQuery(input, '');
        close(host);
        input.focus();
    }

    // ── Free entry ─────────────────────────────────────────────────────────

    /* An address never holds a space, so a pasted list of them may be split on one.
       Any other entry — a tag, a place — may, so it is split on commas, semicolons
       and line breaks only. */
    function splitter(host) {
        var input = inputOf(host);
        var email = host.getAttribute('data-combo-validate') === 'email' ||
            (input && (input.type === 'email' || input.getAttribute('inputmode') === 'email'));
        return email ? /[,;\s]+/ : /[,;\r\n\t]+/;
    }

    function commit(host, raw) {
        var input = inputOf(host);
        var seen = {}, entries = [];
        chips(host).forEach(function (c) { seen[(c.getAttribute('data-value') || '').toLowerCase()] = true; });

        String(raw).split(splitter(host)).forEach(function (part) {
            var entry = part.trim(), key = entry.toLowerCase();
            if (!entry || seen[key]) return;
            seen[key] = true;
            entries.push(entry);
        });

        if (managed(host)) {
            entries.forEach(function (entry) { addChip(host, entry, entry); });
            validate(host);
        } else {
            /* One `change` per entry, carrying that entry as the input's value. The
               value cannot carry a list: a text input strips line breaks from
               whatever is assigned to it. */
            entries.forEach(function (entry) {
                input.value = entry;
                input.dispatchEvent(new Event('change', { bubbles: true }));
            });
        }

        setQuery(input, '');
        close(host);
    }

    // ── Server lists: the next page ────────────────────────────────────────

    /* Clicks the panel's .form-combo-more once the list is scrolled to within a row
       of its end — or at once, when the page is too short to scroll. The button
       sits after the list, not in it: a listbox may hold only options.

       Nothing is clicked while the list is busy, and the same button is not
       clicked twice inside a second — an app that re-renders one button for every
       page would otherwise be asked for the next page before the last arrived. */
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

    // ── Public ─────────────────────────────────────────────────────────────

    ui.combo = {
        /* Opens or closes the field `el` is, or is inside. */
        open: function (el) { var host = hostOf(el); if (host) open(host); },
        close: function (el) { var host = hostOf(el); if (host) close(host); }
    };

    // ── Delegated handlers ─────────────────────────────────────────────────

    /* Focus stays in the input for the whole interaction, which is what the
       combobox pattern asks for — so a press anywhere else in the field is
       cancelled before it can move focus and close the list under the pointer. The
       click that follows still lands. */
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
        // Read before focusing: a collapsed field opens on focus, and toggling after
        // that would shut the list the same press just opened.
        var wasOpen = isOpen(host);
        if (document.activeElement !== input) input.focus();
        var toggle = e.target.closest('.form-combo-toggle');
        openedByPress = toggle && !wasOpen && isOpen(host) ? toggle : null;

        if (inBox && !e.target.closest('button, .chip') && modeOf(host) !== 'free') {
            if (wasOpen) close(host); else open(host);
        }
    });

    /* A collapsed field shows every chip once it has focus, and the list with them —
       the reader came to change the picks, and the whole set is only readable next
       to the options. Focus only: Escape closes it and it stays closed. */
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

        /* A managed free entry's invalid chip goes back into the input to be fixed. */
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
                // A combo never submits its form on Enter, as a <select> does not.
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
                // A free entry commits through the `change` the blur fires.
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

    /* A free entry's input fires its own `change` when it loses focus with text in
       it — which would reach the app as one unsplit entry, before the commit below
       sends the same text again split. So the platform's is stopped at the window,
       ahead of every other listener, and replaced by the commit. Only a trusted
       event is: the ones commit() dispatches pass through. */
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

    // `scroll` does not bubble, hence capture.
    document.addEventListener('scroll', function (e) {
        var list = e.target;
        if (!list || !list.classList || !list.classList.contains('form-combo-list')) return;
        var host = hostOf(list);
        if (host) requestMore(host);
    }, true);

    /* After a render. An app's new rows arrive without `hidden`, the keyboard's
       option may have been replaced, and a first page shorter than the list needs
       the next one asked for with no scroll to trigger it. Only fields that are
       open are looked at, so a page with none open pays one query per mutation
       batch. `hidden` and data-active are attributes, which this does not observe,
       so nothing here re-triggers it. */
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
                var touched = records.some(function (r) { return host.contains(r.target); });
                if (!touched) continue;
                filter(host);
                settle(host);
                requestMore(host);
            }
        }).observe(document.documentElement, { childList: true, subtree: true, characterData: true });
    }

})(window.sednaUi);

/* ── Markdown editor: editing ────────────────────────────────────────────────
   What happens to the textarea's text: the toolbar's commands, the keyboard, a pasted
   address, a pasted or dropped file.

   Every edit goes through edit(), which types it into the textarea with
   document.execCommand('insertText') — so the browser's own undo steps back over it, one
   command at a time, which assigning `value` does not give. Where the browser refuses, the
   value is written and an `input` event dispatched, and the edit is then not undoable. Either
   way the textarea sees exactly one `input` event, which is what a Blazor binding listens to.

   Commands — `data-md-cmd` on any element of the editor, or sednaUi.md.apply(textarea, cmd):

     bold italic strike code codeblock link image video
     h1 h2 h3 h4 h5 h6      a heading, off again on a line that has that level
     ul ol task quote       a list or a quote, off again on lines that have it
     hr table               a block, on a line of its own
     indent outdent         the list items under the selection, one level
     undo redo              the browser's own
     fullscreen             (30-markdown.js)

   Keys, while the textarea has focus — Ctrl, or Cmd on a Mac. `data-md-keys="off"` on the
   editor turns all of it off.

     Mod+B bold   Mod+I italic   Mod+K link   Mod+E code   Mod+Shift+X strike
     Mod+Shift+7 numbered list   Mod+Shift+8 bullet list   Mod+Shift+9 quote
     Mod+Alt+1…6 heading         Mod+Alt+C code block
     Enter       goes on with the list, task or quote it ends a line of, and an empty
                 item ends it
     Tab         indents a list item (or moves to the next cell of a table), Shift+Tab the
                 other way — only where it can. Everywhere else Tab leaves the field, as
                 it does from any textarea.

   Files — a file pasted or dropped on the textarea sends `sedna-md-file`, bubbling and
   cancelable, with { files: [{ id, name, type, size }], index }. The app takes them by
   cancelling it, reads each with sednaUi.md.file(textarea, id), uploads it and answers with
   insert(). Files nobody takes show the editor's `[data-md-file-notice]`.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var md = ui.md = ui.md || {};

    var IS_MAC = /Mac|iPhone|iPad|iPod/.test(navigator.platform || navigator.userAgent || '');

    function lineStart(v, p) { return p <= 0 ? 0 : v.lastIndexOf('\n', p - 1) + 1; }
    function lineEnd(v, p) { var k = v.indexOf('\n', p); return k === -1 ? v.length : k; }

    // The whole lines a selection touches — not the one it only ends at the start of.
    function lineRange(v, s, e) {
        var last = e > s && v.charAt(e - 1) === '\n' ? e - 1 : e;
        return [lineStart(v, s), lineEnd(v, last)];
    }

    // Replaces v[from, to) with `text`, and selects [a, b) afterwards — by default the
    // caret right after the text. The one place the textarea is written.
    function edit(ta, from, to, text, a, b) {
        var v = ta.value;
        if (v.slice(from, to) !== text) {
            var want = v.slice(0, from) + text + v.slice(to), done = false;
            try {
                ta.focus();
                ta.setSelectionRange(from, to);
                done = text === '' ? document.execCommand('delete') : document.execCommand('insertText', false, text);
            } catch (x) { done = false; }
            if (!done) {
                ta.value = want;
                ta.dispatchEvent(new Event('input', { bubbles: true }));
            }
        }
        if (a === undefined) a = from + text.length;
        ta.setSelectionRange(a, b === undefined ? a : b);
    }

    // ── Inline ───────────────────────────────────────────────────────────────

    // Marks the selection, or takes the marks off a selection that has them — inside it
    // or around it. Whitespace at its ends is left outside, where emphasis can still be read.
    function wrap(ta, open, close, placeholder) {
        var v = ta.value, s = ta.selectionStart, e = ta.selectionEnd;
        while (s < e && /\s/.test(v.charAt(s))) s++;
        while (e > s && /\s/.test(v.charAt(e - 1))) e--;
        var sel = v.slice(s, e);
        if (s > 0 && sel && v.slice(s - open.length, s) === open && v.slice(e, e + close.length) === close) {
            return edit(ta, s - open.length, e + close.length, sel, s - open.length, s - open.length + sel.length);
        }
        if (sel.length > open.length + close.length && sel.slice(0, open.length) === open && sel.slice(sel.length - close.length) === close) {
            var inner = sel.slice(open.length, sel.length - close.length);
            return edit(ta, s, e, inner, s, s + inner.length);
        }
        var body = sel || placeholder;
        edit(ta, s, e, open + body + close, s + open.length, s + open.length + body.length);
    }

    var URLISH = /^(?:https?:\/\/|www\.)\S+$/i;

    // [text](address) and ![text](address): the selection is the text — or, when it is an
    // address, the address. What the reader types next is the part that is still missing.
    function link(ta, open, placeholder, selectAddress) {
        var v = ta.value, s = ta.selectionStart, e = ta.selectionEnd, sel = v.slice(s, e).trim();
        var text, address, a, b;
        if (sel && URLISH.test(sel)) { text = placeholder; address = sel; a = s + open.length; b = a + text.length; }
        else if (sel) {
            text = sel; address = 'https://';
            a = s + open.length; b = a + text.length;
            if (selectAddress) { a = b + 2; b = a + address.length; }
        } else { text = placeholder; address = 'https://'; a = s + open.length; b = a + text.length; }
        edit(ta, s, e, open + text + '](' + address + ')', a, b);
    }

    // ── Lines ────────────────────────────────────────────────────────────────

    var ANY_LIST = /^(\s*)(?:[-*+]|\d+[.)])\s+(?:\[[ xX]\]\s+)?/;
    var HAS = {
        ul: /^\s*[-*+]\s+(?!\[[ xX]\]\s)/,
        ol: /^\s*\d+[.)]\s+/,
        task: /^\s*[-*+]\s+\[[ xX]\]\s+/,
        quote: /^\s*>/
    };

    // A list or a quote, on every line the selection touches: taken off again when every
    // line has it, otherwise put on — in place of any other list marker.
    function listOf(ta, kind) {
        var v = ta.value, r = lineRange(v, ta.selectionStart, ta.selectionEnd);
        var lines = v.slice(r[0], r[1]).split('\n');
        var filled = lines.filter(function (l) { return l.trim(); });
        var off = filled.length > 0 && filled.every(function (l) { return HAS[kind].test(l); });
        var n = 0;
        var out = lines.map(function (l) {
            if (!l.trim() && lines.length > 1) return l;
            if (off) return kind === 'quote' ? l.replace(/^(\s*)>[ ]?/, '$1') : l.replace(ANY_LIST, '$1');
            n++;
            if (kind === 'quote') return '> ' + l;
            var base = l.replace(ANY_LIST, '$1');
            var indent = /^\s*/.exec(base)[0];
            var marker = kind === 'ol' ? n + '. ' : kind === 'task' ? '- [ ] ' : '- ';
            return indent + marker + base.slice(indent.length);
        });
        var text = out.join('\n');
        var empty = lines.length === 1 && !lines[0].trim();
        edit(ta, r[0], r[1], text, empty && !off ? r[0] + text.length : r[0], empty && !off ? r[0] + text.length : r[0] + text.length);
    }

    function heading(ta, level) {
        var v = ta.value, r = lineRange(v, ta.selectionStart, ta.selectionEnd);
        var lines = v.slice(r[0], r[1]).split('\n');
        var marks = new Array(level + 1).join('#');
        var filled = lines.filter(function (l) { return l.trim(); });
        var off = filled.length > 0 && filled.every(function (l) {
            var m = /^ {0,3}(#{1,6})\s+/.exec(l);
            return m && m[1].length === level;
        });
        var out = lines.map(function (l) {
            if (!l.trim() && lines.length > 1) return l;
            var text = l.replace(/^ {0,3}#{1,6}(?:\s+|$)/, '');
            return off ? text : marks + ' ' + text;
        });
        var text = out.join('\n');
        edit(ta, r[0], r[1], text, r[0] + (lines.length === 1 && !off ? text.length : 0), r[0] + text.length);
    }

    // A block of its own — a rule, a table — after the line the selection ends on, or in
    // place of that line when it is empty, with an empty line each side. Without one a
    // rule under a paragraph is the paragraph's underline.
    function block(ta, text, a, b) {
        var v = ta.value, e = ta.selectionEnd;
        var ls = lineStart(v, e), le = lineEnd(v, e);
        var from = /^\s*$/.test(v.slice(ls, le)) ? ls : le;
        var head = v.slice(0, from), tail = v.slice(le);
        var before = head === '' ? '' : /\n\n$/.test(head) ? '' : /\n$/.test(head) ? '\n' : '\n\n';
        var after = /^\n\n/.test(tail) ? '' : '\n';
        var base = from + before.length;
        edit(ta, from, le, before + text + after, a === undefined ? from + before.length + text.length + after.length : base + a,
            a === undefined ? undefined : base + b);
    }

    function codeBlock(ta) {
        var v = ta.value, s = ta.selectionStart, e = ta.selectionEnd;
        var body = v.slice(s, e).replace(/^\n+|\n+$/g, '') || 'code';
        var fence = '```';
        while (body.indexOf(fence) !== -1) fence += '`';
        var pre = s === lineStart(v, s) ? '' : '\n';
        var post = e === lineEnd(v, e) ? '' : '\n';
        edit(ta, s, e, pre + fence + '\n' + body + '\n' + fence + post, s + pre.length + fence.length + 1, s + pre.length + fence.length + 1 + body.length);
    }

    function table(ta) {
        var rows = '| Column | Column | Column |\n| --- | --- | --- |\n|  |  |  |';
        block(ta, rows, 2, 8);
    }

    // ── Lists: Enter, Tab ────────────────────────────────────────────────────

    var ITEM = /^( *)([-*+]|\d{1,9}[.)])( +)/;

    function itemOf(line) {
        var m = ITEM.exec(line);
        return m ? { indent: m[1].length, offset: m[0].length } : null;
    }

    // Whether the caret is inside a fenced code block, where a line that looks like a
    // list item is code.
    function inFence(v, pos) {
        var open = null, k = 0;
        while (k < pos) {
            var end = lineEnd(v, k);
            var m = /^ {0,3}(`{3,}|~{3,})/.exec(v.slice(k, end));
            if (m) {
                if (!open) open = m[1];
                else if (m[1].charAt(0) === open.charAt(0) && m[1].length >= open.length && /^ {0,3}[`~]+\s*$/.test(v.slice(k, end))) open = null;
            }
            k = end + 1;
        }
        return !!open;
    }

    // Enter at the end of a list item, task or quote line: the next line begins with the
    // same marker. An empty item ends the list. Returns whether it did anything.
    function newline(ta) {
        var v = ta.value, s = ta.selectionStart;
        if (s !== ta.selectionEnd) return false;
        var ls = lineStart(v, s), le = lineEnd(v, s), line = v.slice(ls, le);
        // Each ">" is a delimiter and the blanks after it are one run, so the pattern cannot
        // backtrack over a long line of them.
        var m = /^([ \t]*(?:>[ \t]*)*)(?:([-*+])|(\d{1,9})([.)]))([ \t]+)(\[[ xX]\][ \t]+)?/.exec(line);
        var q = /^([ \t]*(?:>[ \t]*)+)/.exec(line);
        if (!m && !q) return false;
        if (inFence(v, ls)) return false;
        if (m) {
            if (s - ls < m[0].length) return false;
            if (!line.slice(m[0].length).trim()) {
                var indent = /^ */.exec(m[1])[0].length;
                if (m[1].indexOf('>') === -1 && indent > 0) return outdent(ta, true);
                edit(ta, ls, le, m[1].indexOf('>') === -1 ? '' : m[1].replace(/\s+$/, ' '));
                return true;
            }
            var marker = m[2] ? m[2] : (+m[3] + 1) + m[4];
            edit(ta, s, s, '\n' + m[1] + marker + m[5] + (m[6] ? '[ ] ' : ''));
            return true;
        }
        if (s - ls < q[0].length) return false;
        if (!line.slice(q[0].length).trim()) { edit(ta, ls, le, ''); return true; }
        edit(ta, s, s, '\n' + q[1]);
        return true;
    }

    function isItem(line) { return ITEM.test(line); }

    // The list items under the selection, one level in. Only where there is a level to go
    // to: the first item of a list, or one already nested a level under the item above,
    // stays where it is, and the key does what it does everywhere else.
    function indent(ta) {
        var v = ta.value, r = lineRange(v, ta.selectionStart, ta.selectionEnd);
        var lines = v.slice(r[0], r[1]).split('\n');
        var filled = lines.filter(function (l) { return l.trim(); });
        if (!filled.length || !filled.every(isItem)) return false;
        var first = itemOf(lines[0]);
        // The nearest line above that is not empty must be an item, and the one to go under.
        var k = r[0], above = null;
        while (k > 0) {
            var ps = lineStart(v, k - 1), pl = v.slice(ps, k - 1);
            if (pl.trim()) { above = pl; break; }
            k = ps;
        }
        var parent = above && itemOf(above);
        if (!first || !parent || first.indent > parent.indent) return false;
        var step = first.indent < parent.indent ? parent.indent - first.indent : parent.offset - parent.indent;
        if (first.indent >= parent.offset) return false;
        var pad = new Array(step + 1).join(' ');
        var text = lines.map(function (l) { return l.trim() ? pad + l : l; }).join('\n');
        edit(ta, r[0], r[1], text, r[0], r[0] + text.length);
        return true;
    }

    // The list items under the selection, one level out: to the indent of the item they
    // were under, or the margin.
    function outdent(ta, caret) {
        var v = ta.value, r = lineRange(v, ta.selectionStart, ta.selectionEnd);
        var lines = v.slice(r[0], r[1]).split('\n');
        var filled = lines.filter(function (l) { return l.trim(); });
        if (!filled.length || !filled.every(isItem)) return false;
        var first = itemOf(lines[0]);
        if (!first || first.indent === 0) return false;
        var target = 0, k = r[0];
        while (k > 0) {
            var ps = lineStart(v, k - 1), pl = v.slice(ps, k - 1), it = itemOf(pl);
            if (it && it.indent < first.indent) { target = it.indent; break; }
            k = ps;
        }
        var cut = first.indent - target;
        var text = lines.map(function (l) {
            var n = Math.min(cut, indentOf(l));
            return l.slice(n);
        }).join('\n');
        if (caret) edit(ta, r[0], r[1], text, r[0] + text.length);
        else edit(ta, r[0], r[1], text, r[0], r[0] + text.length);
        return true;
    }

    function indentOf(l) { var k = 0; while (l.charAt(k) === ' ') k++; return k; }

    // ── Tables: Tab ──────────────────────────────────────────────────────────

    // The positions of a row's cell borders — the pipes that are not escaped.
    function pipes(line) {
        var at = [];
        for (var i = 0; i < line.length; i++) {
            if (line.charAt(i) === '\\') i++;
            else if (line.charAt(i) === '|') at.push(i);
        }
        return at;
    }

    function isRow(line) { return /^\s*\|.*\|\s*$/.test(line) && pipes(line).length >= 2; }
    function isRule(line) { return /^\s*\|(?:\s*:?-+:?\s*\|)+\s*$/.test(line); }

    // Puts the selection on a cell's text, or the caret in an empty cell.
    function toCell(ta, lineStartAt, line, index) {
        var p = pipes(line), from = p[index] + 1, to = p[index + 1];
        var inner = line.slice(from, to), lead = /^ */.exec(inner)[0].length, trail = /\s*$/.exec(inner)[0].length;
        if (lead === inner.length) ta.setSelectionRange(lineStartAt + from + Math.min(1, inner.length), lineStartAt + from + Math.min(1, inner.length));
        else ta.setSelectionRange(lineStartAt + from + lead, lineStartAt + to - trail);
    }

    // Tab and Shift+Tab inside a table row: the next cell, the previous one; Tab in the
    // last cell of the last row adds a row. Returns whether it did anything.
    function cell(ta, back) {
        var v = ta.value, s = ta.selectionStart;
        var ls = lineStart(v, s), le = lineEnd(v, s), line = v.slice(ls, le);
        if (!isRow(line) || isRule(line)) return false;
        var p = pipes(line), col = s - ls, i = 0;
        for (var k = 0; k < p.length; k++) if (p[k] < col) i = k;
        i = Math.min(i, p.length - 2);
        if (!back) {
            if (i + 1 < p.length - 1) { toCell(ta, ls, line, i + 1); return true; }
            var nextStart = le + 1, nextEnd = lineEnd(v, nextStart);
            // The rule under the headings is not a row to type in.
            if (le < v.length && isRule(v.slice(nextStart, nextEnd))) { nextStart = nextEnd + 1; nextEnd = lineEnd(v, nextStart); }
            if (nextStart <= v.length && isRow(v.slice(nextStart, nextEnd))) {
                toCell(ta, nextStart, v.slice(nextStart, nextEnd), 0);
                return true;
            }
            var cells = p.length - 1;
            var row = '\n|' + new Array(cells + 1).join('  |');
            edit(ta, le, le, row, le + 3);
            return true;
        }
        if (i > 0) { toCell(ta, ls, line, i - 1); return true; }
        var prevEnd = ls - 1;
        if (prevEnd < 0) return false;
        var prevStart = lineStart(v, prevEnd), prev = v.slice(prevStart, prevEnd);
        if (isRule(prev) && prevStart > 0) {
            prevEnd = prevStart - 1; prevStart = lineStart(v, prevEnd); prev = v.slice(prevStart, prevEnd);
        }
        if (!isRow(prev)) return false;
        toCell(ta, prevStart, prev, pipes(prev).length - 2);
        return true;
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    // Apply a toolbar command to the current selection. The edit reaches the textarea as
    // an `input` event, so the binding captures the new value. Returns whether `cmd` is one.
    md.apply = function (ta, cmd) {
        if (!ta || typeof ta.selectionStart !== 'number' || ta.readOnly || ta.disabled) return false;
        var level = /^h([1-6])$/.exec(cmd);
        if (level) heading(ta, +level[1]);
        else switch (cmd) {
            case 'bold':      wrap(ta, '**', '**', 'bold text'); break;
            case 'italic':    wrap(ta, '_', '_', 'italic text'); break;
            case 'strike':    wrap(ta, '~~', '~~', 'struck text'); break;
            case 'code':      wrap(ta, '`', '`', 'code'); break;
            case 'codeblock': codeBlock(ta); break;
            case 'ul':
            case 'ol':
            case 'task':
            case 'quote':     listOf(ta, cmd); break;
            case 'hr':        block(ta, '---'); break;
            case 'table':     table(ta); break;
            case 'link':      link(ta, '[', 'link text', true); break;
            case 'image':     link(ta, '![', 'image description', false); break;
            case 'video':     link(ta, '![', 'video description', false); break;
            case 'indent':    indent(ta); break;
            case 'outdent':   outdent(ta); break;
            case 'undo':
            case 'redo':
                ta.focus();
                try { document.execCommand(cmd); } catch (x) { /* the browser has no history to step */ }
                break;
            default: return false;
        }
        ta.focus();
        return true;
    };

    // Replace the current selection with `text` — a link from the app's own picker, a
    // snippet, a mention — leave the caret right after it, and fire the input event so
    // the binding captures the new value. The same edit apply() makes, without a
    // command's wrapping.
    //
    // It works while focus is elsewhere, which is the usual case: the picker is a
    // dialog opened from the toolbar, and the textarea has been blurred by the time
    // the app has its answer. A textarea keeps selectionStart/selectionEnd across a
    // blur, so the text lands where the caret was, and nothing is stored here to make
    // that true. The one thing that moves the caret is assigning a DIFFERENT value
    // while focus is away; assigning the same value, which is what a Blazor re-render
    // after the input event does, leaves it where it was.
    md.insert = function (ta, text) {
        if (!ta || typeof ta.selectionStart !== 'number') return;
        text = text == null ? '' : String(text);
        edit(ta, ta.selectionStart, ta.selectionEnd, text);
        ta.focus();
    };

    // ── Keys ─────────────────────────────────────────────────────────────────

    // The command a key chord stands for, or null.
    function chord(e) {
        var mod = IS_MAC ? e.metaKey && !e.ctrlKey : e.ctrlKey && !e.metaKey;
        if (!mod) return null;
        var code = /^(?:Key|Digit)(.)$/.exec(e.code || '');
        if (e.altKey) {
            if (e.shiftKey || !code) return null;
            if (/[1-6]/.test(code[1])) return 'h' + code[1];
            return code[1] === 'C' ? 'codeblock' : null;
        }
        if (e.shiftKey) {
            if (!code) return null;
            return { X: 'strike', 7: 'ol', 8: 'ul', 9: 'quote' }[code[1]] || null;
        }
        // A letter is the one the layout types; a layout with no Latin letters falls back
        // to the key's place on a QWERTY board.
        var letter = /^[a-z]$/i.test(e.key) ? e.key.toLowerCase() : code && /[A-Z]/.test(code[1]) ? code[1].toLowerCase() : '';
        return { b: 'bold', i: 'italic', k: 'link', e: 'code' }[letter] || null;
    }

    // ── Files ────────────────────────────────────────────────────────────────

    var FILES_KEPT = 20, fileSeq = 0, held = new WeakMap();

    // The File behind an id a `sedna-md-file` event named — for the app to upload; null once
    // the editor has let it go, which it does after the last few.
    md.file = function (ta, id) {
        var files = held.get(ta);
        return files && files.get(id) || null;
    };

    // Offers files to the app. Returns whether anything was offered.
    function offer(root, ta, list) {
        list = Array.prototype.slice.call(list || []).filter(Boolean);
        if (!list.length) return false;
        var files = held.get(ta);
        if (!files) held.set(ta, files = new Map());
        var listed = list.map(function (file) {
            var id = 'file-' + (++fileSeq);
            files.set(id, file);
            return { id: id, name: file.name || '', type: file.type || '', size: file.size || 0 };
        });
        while (files.size > FILES_KEPT) files.delete(files.keys().next().value);
        var taken = !ta.dispatchEvent(new CustomEvent('sedna-md-file', {
            bubbles: true, cancelable: true, detail: { files: listed, index: ta.selectionStart }
        }));
        var notice = root.querySelector('[data-md-file-notice]');
        if (notice) notice.hidden = taken;
        return true;
    }

    function hasFiles(dt) {
        return !!dt && Array.prototype.indexOf.call(dt.types || [], 'Files') !== -1;
    }

    /* Wires the keyboard, the paste and the drop of one editor. Private: _initOne calls it. */
    md._wire = function (root, ta) {
        var off = function () { return root.getAttribute('data-md-keys') === 'off' || ta.readOnly || ta.disabled; };

        ta.addEventListener('keydown', function (e) {
            if (e.defaultPrevented || e.isComposing || off()) return;
            var cmd = chord(e);
            if (cmd) { e.preventDefault(); md.apply(ta, cmd); return; }
            var plain = !e.ctrlKey && !e.metaKey && !e.altKey;
            if (e.key === 'Enter' && plain && !e.shiftKey) {
                if (newline(ta)) e.preventDefault();
            } else if (e.key === 'Tab' && plain) {
                var did = e.shiftKey ? (outdent(ta) || cell(ta, true)) : (indent(ta) || cell(ta, false));
                if (did) e.preventDefault();
            }
        });

        root.addEventListener('keydown', function (e) {
            if (e.key === 'Escape' && !e.defaultPrevented && root.hasAttribute('data-md-fullscreen')) {
                e.preventDefault();
                md._fullscreen(root, null, false);
            }
        });

        ta.addEventListener('paste', function (e) {
            if (e.defaultPrevented || ta.readOnly || ta.disabled) return;
            var data = e.clipboardData;
            if (!data) return;
            var text = data.getData('text/plain');
            var s = ta.selectionStart, end = ta.selectionEnd;
            // An address pasted over a selection makes the selection a link to it.
            if (end > s && !off() && /^(?:https?:\/\/|mailto:)\S+$/i.test(text.trim()) && ta.value.slice(s, end).indexOf('\n') === -1) {
                e.preventDefault();
                edit(ta, s, end, '[' + ta.value.slice(s, end) + '](' + text.trim() + ')');
                return;
            }
            // A picture copied from a screen has no text; a document copied with its pictures
            // has, and is pasted as the text.
            if (!text && data.files && data.files.length) {
                e.preventDefault();
                offer(root, ta, data.files);
            }
        });

        // A file dropped on a textarea would otherwise be opened in the tab, taking the
        // reader's draft with it.
        var drag = 0;
        var clear = function () { drag = 0; root.removeAttribute('data-md-dragging'); };
        ta.addEventListener('dragenter', function (e) {
            if (!hasFiles(e.dataTransfer)) return;
            drag++;
            root.setAttribute('data-md-dragging', '');
        });
        ta.addEventListener('dragover', function (e) {
            if (hasFiles(e.dataTransfer)) e.preventDefault();
        });
        ta.addEventListener('dragleave', function (e) {
            if (hasFiles(e.dataTransfer) && --drag <= 0) clear();
        });
        ta.addEventListener('drop', function (e) {
            if (!hasFiles(e.dataTransfer)) return;
            e.preventDefault();
            clear();
            if (!ta.readOnly && !ta.disabled) offer(root, ta, e.dataTransfer.files);
        });
    };

})(window.sednaUi);

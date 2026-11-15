/* ── Markdown → HTML ─────────────────────────────────────────────────────────
   `sednaUi.md.render(source)` — the library's own Markdown renderer, which the editor's
   preview and any page showing authored prose use. A part of its own so the editor's wiring
   (30-markdown.js) and its commands (32-markdown-edit.js) stay short; it needs nothing
   from them but the `md` object, which it creates when it comes first.

   What it reads (CommonMark with the GitHub extensions an editor's author expects):

     blocks   paragraphs, ATX and setext headings, blockquotes (nested, with anything
              inside), bullet and numbered lists (nested, tight and loose, a numbered
              list's first number), task lists, fenced code (``` and ~~~, with a
              language), indented code, horizontal rules, tables with alignment
     inline   emphasis, strong, both, ~~strikethrough~~, `code` (with backticks inside),
              links with a title, <autolinks>, bare http(s):// and www. addresses,
              images and videos, link and image references, hard and soft line breaks,
              backslash escapes, character references

   What it does not read, and why:

     raw HTML          Every `<` is text. The preview is a security boundary, and an
                       allow-list of tags is a sanitiser's job, done on the server.
     footnotes, definition lists, math, emoji codes, heading ids, front matter
                       Not CommonMark; each needs markup or an id the page may not have.
     link reference definitions that span lines

   Every string that reaches the output is built here: text and attribute values go through
   esc(), an address through ui._.safeHref(), and what is left — a tag, a class, `align`,
   `start` — is written from a fixed list. Nothing the source says is ever placed as markup.

   The parse is two phases. The blocks are found first, as plain objects holding their raw
   text; the inline syntax of each is read only when the HTML is written, so a reference
   defined below a paragraph still reaches it.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var md = ui.md = ui.md || {};

    // How deep blockquotes and lists nest before the rest is read as plain text, so a
    // line of ten thousand ">" cannot exhaust the stack.
    var MAX_DEPTH = 24;

    var NAMED = {
        amp: '&', lt: '<', gt: '>', quot: '"', apos: "'", nbsp: ' ', copy: '©', reg: '®',
        trade: '™', hellip: '…', mdash: '—', ndash: '–', lsquo: '‘', rsquo: '’',
        ldquo: '“', rdquo: '”', laquo: '«', raquo: '»', bull: '•', middot: '·',
        times: '×', divide: '÷', deg: '°', plusmn: '±', euro: '€', pound: '£',
        yen: '¥', cent: '¢', sect: '§', para: '¶', larr: '←', rarr: '→',
        uarr: '↑', darr: '↓', harr: '↔', ne: '≠', le: '≤', ge: '≥'
    };

    var ESCAPABLE = /[!-\/:-@\[-`{-~]/;
    var PUNCT = /[!-\/:-@\[-`{-~\p{P}\p{S}]/u;
    var ENTITY = /&(#[xX][0-9a-fA-F]{1,6}|#[0-9]{1,7}|[A-Za-z][A-Za-z0-9]{1,31});/;
    var BLANK = /^\s*$/;

    var RE_FENCE = /^( {0,3})(`{3,}|~{3,})(.*)$/;
    var RE_ATX = /^ {0,3}(#{1,6})(?:[ \t]+(.*?))?(?:[ \t]+#+)?[ \t]*$/;
    var RE_HR = /^ {0,3}([-*_])(?:[ \t]*\1){2,}[ \t]*$/;
    var RE_QUOTE = /^ {0,3}>/;
    var RE_SETEXT = /^ {0,3}(=+|-+)[ \t]*$/;
    var RE_LIST = /^( {0,3})([-+*]|(\d{1,9})([.)]))( +|$)(.*)$/;
    var RE_DEF = /^ {0,3}\[((?:[^\]\\]|\\.)+)\]:[ \t]*(<[^<>\n]*>|[^\s<]\S*)(?:[ \t]+("(?:[^"\\]|\\.)*"|'(?:[^'\\]|\\.)*'|\((?:[^()\\]|\\.)*\)))?[ \t]*$/;
    var RE_DELIM_CELL = /^:?-+:?$/;

    // A character reference or a backslash escape, in one pass, so `\&amp;` stays an ampersand
    // followed by "amp;" instead of being decoded after it was escaped.
    var RE_UNESC = /\\([!-\/:-@\[-`{-~])|&(#[xX][0-9a-fA-F]{1,6}|#[0-9]{1,7}|[A-Za-z][A-Za-z0-9]{1,31});/g;

    function esc(s) {
        return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    function entity(name) {
        if (name.charAt(0) === '#') {
            var hex = name.charAt(1) === 'x' || name.charAt(1) === 'X';
            var code = parseInt(name.slice(hex ? 2 : 1), hex ? 16 : 10);
            if (!code || code > 0x10ffff || (code >= 0xd800 && code <= 0xdfff)) return '�';
            return String.fromCodePoint(code);
        }
        return Object.prototype.hasOwnProperty.call(NAMED, name) ? NAMED[name] : null;
    }

    function unesc(s) {
        return s.replace(RE_UNESC, function (all, ch, ent) {
            if (ch) return ch;
            var v = entity(ent);
            return v === null ? all : v;
        });
    }

    function normLabel(s) { return s.trim().replace(/\s+/g, ' ').toLowerCase(); }

    function expandTabs(line) {
        var m = /^[ \t]+/.exec(line);
        if (!m || m[0].indexOf('\t') === -1) return line;
        var col = 0;
        for (var i = 0; i < m[0].length; i++) col += m[0].charAt(i) === '\t' ? 4 - (col % 4) : 1;
        return new Array(col + 1).join(' ') + line.slice(m[0].length);
    }

    function indentOf(line) {
        var k = 0;
        while (line.charAt(k) === ' ') k++;
        return k;
    }

    function trimStart(s) { return s.replace(/^[ \t]+/, ''); }

    // ── Media ────────────────────────────────────────────────────────────────
    // `alt`, `url` and `title` are raw here. The source is kept only when it is http, https
    // or relative — no scheme before its first / ? or # — and an image whose source is not
    // kept is its description, as text.
    function mediaHtml(alt, url, title) {
        if (!/^(https?:|[^:\/?#]*(?:[\/?#]|$))/i.test(url.trim())) return esc(alt);
        url = url.trim();
        var t = title === null ? '' : ' title="' + esc(title) + '"';
        if (/\.(mp4|m4v|mov|webm|ogv)$/i.test(url.split(/[?#]/)[0])) {
            return '<video controls preload="metadata" src="' + esc(url) + '"' +
                (alt ? ' aria-label="' + esc(alt) + '"' : '') + t + '></video>';
        }
        return '<img src="' + esc(url) + '" alt="' + esc(alt) + '"' + t + ' loading="lazy">';
    }

    function linkOpen(url, title) {
        var safe = ui._.safeHref(url) || '#';
        return '<a href="' + esc(safe) + '"' + (title === null ? '' : ' title="' + esc(title) + '"') +
            ' target="_blank" rel="noopener">';
    }

    // ── Inline ───────────────────────────────────────────────────────────────

    function isWs(c) { return c === '' || /\s/.test(c); }
    function isPunct(c) { return c !== '' && PUNCT.test(c); }

    function skipWs(src, j) {
        while (j < src.length && /\s/.test(src.charAt(j))) j++;
        return j;
    }

    // "(" destination "title"? ")" — j is at the "(". Null when it is not one.
    function parseTail(src, j) {
        var n = src.length, k, ch, buf = '', dest, title = null;
        j = skipWs(src, j + 1);
        if (src.charAt(j) === '<') {
            for (k = j + 1; k < n; k++) {
                ch = src.charAt(k);
                if (ch === '\\' && k + 1 < n) { buf += ch + src.charAt(k + 1); k++; continue; }
                if (ch === '>') break;
                if (ch === '<' || ch === '\n') return null;
                buf += ch;
            }
            if (k >= n) return null;
            dest = buf; j = k + 1;
        } else {
            var depth = 0;
            for (k = j; k < n; k++) {
                ch = src.charAt(k);
                if (ch === '\\' && k + 1 < n && ESCAPABLE.test(src.charAt(k + 1))) { buf += ch + src.charAt(k + 1); k++; continue; }
                if (ch === '(') depth++;
                else if (ch === ')') { if (depth === 0) break; depth--; }
                else if (/\s/.test(ch) || ch < ' ') break;
                buf += ch;
            }
            if (k >= n || depth !== 0) return null;
            dest = buf; j = k;
        }
        var afterDest = j;
        j = skipWs(src, j);
        var c = src.charAt(j);
        if (j > afterDest && (c === '"' || c === "'" || c === '(')) {
            var close = c === '(' ? ')' : c;
            buf = '';
            for (k = j + 1; k < n; k++) {
                ch = src.charAt(k);
                if (ch === '\\' && k + 1 < n) { buf += ch + src.charAt(k + 1); k++; continue; }
                if (ch === close) break;
                buf += ch;
            }
            if (k >= n) return null;
            title = unesc(buf);
            j = skipWs(src, k + 1);
        }
        if (src.charAt(j) !== ')') return null;
        return { dest: unesc(dest), title: title, end: j + 1 };
    }

    var AUTOLINK = /<([A-Za-z][A-Za-z0-9+.\-]{1,31}:[^\s<>]*)>/y;
    var EMAIL = /<([A-Za-z0-9.!#$%&'*+\/=?^_`{|}~\-]+@[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]{0,61}[A-Za-z0-9])?)+)>/y;
    var BARE = /(?:https?:\/\/|www\.)[^\s<]+/iy;
    var ENT_AT = new RegExp(ENTITY.source, 'y');
    var PLAIN = /[^\\`&\n*_~\[\]!<]+/y;
    var PLAIN_NO_URL = /[^\\`&\n*_~\[\]!<hHwW]+/y;

    // A bare address ends before the punctuation that ends the sentence around it, and
    // before a closing parenthesis it did not open.
    function trimBare(url) {
        for (;;) {
            var last = url.charAt(url.length - 1);
            if (/[?!.,:*_~'";]/.test(last)) { url = url.slice(0, -1); continue; }
            if (last === ')') {
                var open = (url.match(/\(/g) || []).length, close = (url.match(/\)/g) || []).length;
                if (close > open) { url = url.slice(0, -1); continue; }
            }
            return url;
        }
    }

    // The text of a run of nodes, without markup — what an image's `alt` is.
    function plainOf(nodes) {
        var s = '';
        for (var i = 0; i < nodes.length; i++) {
            var nd = nodes[i];
            if (nd.t === 'text') s += nd.v;
            else if (nd.t === 'html') s += nd.p;
            else if (nd.t === 'delim') s += new Array(nd.n + 1).join(nd.ch);
            else if (nd.t === 'link') s += plainOf(nd.kids);
            else if (nd.t === 'img') s += plainOf(nd.kids);
        }
        return s;
    }

    // Emphasis, strong and strikethrough, by the CommonMark delimiter rules: a run opens
    // or closes by what is on each side of it, "_" does not open inside a word, and a
    // closer finds the nearest opener of its own kind. A match writes its tags onto the
    // two delimiter nodes (`opens`, `closes`) and kills the delimiters between them, so
    // nothing is spliced and the indices stay valid.
    function emphasis(nodes) {
        var bottoms = {};
        for (var ci = 0; ci < nodes.length; ci++) {
            var c = nodes[ci];
            if (c.t !== 'delim' || !c.close || c.dead) continue;
            while (c.n > 0) {
                var key = c.ch + (c.orig % 3) + (c.open ? 'o' : '');
                var bottom = Object.prototype.hasOwnProperty.call(bottoms, key) ? bottoms[key] : -1;
                var o = null, oi;
                for (oi = ci - 1; oi > bottom; oi--) {
                    var d = nodes[oi];
                    if (d.t !== 'delim' || !d.open || d.dead || d.n === 0 || d.ch !== c.ch) continue;
                    if (c.ch === '~') { if (d.n === 2 && c.n === 2) { o = d; break; } continue; }
                    if ((d.close || c.open) && (d.orig + c.orig) % 3 === 0 && !(d.orig % 3 === 0 && c.orig % 3 === 0)) continue;
                    o = d; break;
                }
                if (!o) { bottoms[key] = ci - 1; break; }
                var use = c.ch === '~' ? 2 : (o.n >= 2 && c.n >= 2 ? 2 : 1);
                var tag = c.ch === '~' ? 'del' : (use === 2 ? 'strong' : 'em');
                o.n -= use; c.n -= use;
                o.opens = '<' + tag + '>' + o.opens;
                c.closes += '</' + tag + '>';
                for (var k = oi + 1; k < ci; k++) if (nodes[k].t === 'delim') nodes[k].dead = true;
            }
        }
    }

    function renderInline(nodes, depth) {
        var out = '';
        for (var i = 0; i < nodes.length; i++) {
            var nd = nodes[i];
            switch (nd.t) {
                case 'text': out += esc(nd.v); break;
                case 'html': out += nd.v; break;
                case 'delim': out += nd.closes + new Array(nd.n + 1).join(nd.ch) + nd.opens; break;
                case 'link':
                    // Past the nesting limit a link is its text, so a thousand nested
                    // brackets cannot exhaust the stack.
                    out += depth > MAX_DEPTH ? esc(plainOf(nd.kids))
                        : linkOpen(nd.dest, nd.title) + renderInline(nd.kids, depth + 1) + '</a>';
                    break;
                case 'img': out += mediaHtml(plainOf(nd.kids), nd.dest, nd.title); break;
            }
        }
        return out;
    }

    function inline(src, st) {
        var nodes = [], brackets = [], text = '', i = 0, n = src.length, m;
        var noClose = {};
        var bare = /https?:\/\/|www\./i.test(src);
        var plain = bare ? PLAIN_NO_URL : PLAIN;

        function flush() {
            if (text) { nodes.push({ t: 'text', v: text }); text = ''; }
        }
        function html(h, p) { flush(); nodes.push({ t: 'html', v: h, p: p || '' }); }
        // The spaces at the end of the text so far, before the break at `at`. They are counted
        // in the source, which holds the same characters, and never more than the text holds:
        // reading the text itself makes the engine join the pieces it was built from into one
        // string on every line break, which is time that grows with the square of the lines.
        function trailingSpaces(at) {
            var k = at;
            while (k > 0 && src.charAt(k - 1) === ' ') k--;
            return Math.min(at - k, text.length);
        }
        function dropTrailing(spaces) {
            if (spaces > 0) text = text.slice(0, text.length - spaces);
        }

        while (i < n) {
            plain.lastIndex = i;
            if ((m = plain.exec(src))) { text += m[0]; i = plain.lastIndex; continue; }
            var c = src.charAt(i);

            if (c === '\\') {
                var nx = src.charAt(i + 1);
                if (nx === '\n') { dropTrailing(trailingSpaces(i)); html('<br>', '\n'); i += 2; continue; }
                if (nx && ESCAPABLE.test(nx)) { text += nx; i += 2; continue; }
                text += c; i++; continue;
            }

            if (c === '\n') {
                var spaces = trailingSpaces(i), hard = spaces >= 2;
                dropTrailing(spaces);
                if (hard) html('<br>', '\n'); else text += '\n';
                i++; continue;
            }

            if (c === '`') {
                var k = i;
                while (src.charAt(k) === '`') k++;
                var len = k - i, found = -1, endRun = 0;
                if (!noClose[len]) {
                    var runs = /`+/g;
                    runs.lastIndex = k;
                    var r;
                    while ((r = runs.exec(src))) {
                        if (r[0].length === len) { found = r.index; endRun = found + len; break; }
                    }
                    if (found === -1) noClose[len] = true;
                }
                if (found === -1) { text += src.slice(i, k); i = k; continue; }
                var code = src.slice(k, found).replace(/\n/g, ' ');
                if (/^ .* $/.test(code) && /[^ ]/.test(code)) code = code.slice(1, -1);
                html('<code>' + esc(code) + '</code>', code);
                i = endRun; continue;
            }

            if (c === '&') {
                ENT_AT.lastIndex = i;
                var em = ENT_AT.exec(src);
                var ch = em ? entity(em[1]) : null;
                if (ch !== null) { text += ch; i += em[0].length; continue; }
                text += c; i++; continue;
            }

            if (c === '*' || c === '_' || c === '~') {
                var e = i;
                while (src.charAt(e) === c) e++;
                var run = e - i;
                if (c === '~' && run !== 2) { text += src.slice(i, e); i = e; continue; }
                var before = i > 0 ? src.charAt(i - 1) : '', after = e < n ? src.charAt(e) : '';
                var left = !isWs(after) && (!isPunct(after) || isWs(before) || isPunct(before));
                var right = !isWs(before) && (!isPunct(before) || isWs(after) || isPunct(after));
                var canOpen = c === '_' ? left && (!right || isPunct(before)) : left;
                var canClose = c === '_' ? right && (!left || isPunct(after)) : right;
                flush();
                nodes.push({ t: 'delim', ch: c, n: run, orig: run, open: canOpen, close: canClose, dead: false, opens: '', closes: '' });
                i = e; continue;
            }

            if (c === '[' || (c === '!' && src.charAt(i + 1) === '[')) {
                var img = c === '!';
                flush();
                nodes.push({ t: 'text', v: img ? '![' : '[' });
                brackets.push({ idx: nodes.length - 1, pos: i + (img ? 2 : 1), img: img, active: true });
                i += img ? 2 : 1; continue;
            }

            if (c === ']') {
                var op = brackets.length ? brackets[brackets.length - 1] : null;
                if (!op) { text += c; i++; continue; }
                if (!op.active) { brackets.pop(); text += c; i++; continue; }
                var res = null, j = i + 1, label = src.slice(op.pos, i);
                if (src.charAt(j) === '(') {
                    var tail = parseTail(src, j);
                    if (tail) res = { dest: tail.dest, title: tail.title, end: tail.end };
                }
                if (!res) {
                    var ref = null, end = j;
                    if (src.charAt(j) === '[') {
                        var close = src.indexOf(']', j + 1);
                        if (close !== -1) {
                            var l2 = src.slice(j + 1, close);
                            ref = st.refs[normLabel(l2 === '' ? label : l2)];
                            end = close + 1;
                        }
                    } else {
                        ref = st.refs[normLabel(label)];
                    }
                    if (ref) res = { dest: ref.dest, title: ref.title, end: end };
                }
                if (!res) {
                    brackets.pop(); text += c; i++; continue;
                }
                flush();
                var kids = nodes.splice(op.idx).slice(1);
                emphasis(kids);
                nodes.push({ t: op.img ? 'img' : 'link', dest: res.dest, title: res.title, kids: kids });
                brackets.pop();
                // A link cannot hold a link: every "[" still open before this one is plain text.
                if (!op.img) for (var b = 0; b < brackets.length; b++) if (!brackets[b].img) brackets[b].active = false;
                i = res.end; continue;
            }

            if (c === '<') {
                AUTOLINK.lastIndex = i;
                var am = AUTOLINK.exec(src);
                if (am) {
                    html(linkOpen(am[1], null) + esc(am[1]) + '</a>', am[1]);
                    i += am[0].length; continue;
                }
                EMAIL.lastIndex = i;
                am = EMAIL.exec(src);
                if (am) {
                    html(linkOpen('mailto:' + am[1], null) + esc(am[1]) + '</a>', am[1]);
                    i += am[0].length; continue;
                }
                text += c; i++; continue;
            }

            // h, H, w, W — only reached when the source holds an address at all.
            if (!brackets.length) {
                var prev = i > 0 ? src.charAt(i - 1) : '';
                if (prev === '' || /[\s*_~(]/.test(prev)) {
                    BARE.lastIndex = i;
                    var bm = BARE.exec(src);
                    if (bm) {
                        var url = trimBare(bm[0]);
                        if (url.replace(/^(?:https?:\/\/|www\.)/i, '').length > 0) {
                            var href = /^www\./i.test(url) ? 'http://' + url : url;
                            html(linkOpen(href, null) + esc(url) + '</a>', url);
                            i += url.length; continue;
                        }
                    }
                }
            }
            text += c; i++;
        }
        flush();
        emphasis(nodes);
        return renderInline(nodes, 0);
    }

    // ── Blocks ───────────────────────────────────────────────────────────────

    function splitRow(line) {
        var s = line.trim();
        if (s.charAt(0) === '|') s = s.slice(1);
        if (s.charAt(s.length - 1) === '|' && s.charAt(s.length - 2) !== '\\') s = s.slice(0, -1);
        var cells = [], cur = '';
        for (var i = 0; i < s.length; i++) {
            var ch = s.charAt(i);
            if (ch === '\\' && s.charAt(i + 1) === '|') { cur += '|'; i++; }
            else if (ch === '|') { cells.push(cur.trim()); cur = ''; }
            else cur += ch;
        }
        cells.push(cur.trim());
        return cells;
    }

    function isDelimRow(line) {
        if (line === undefined || line.indexOf('|') === -1 || line.indexOf('-') === -1) return false;
        var cells = splitRow(line);
        for (var i = 0; i < cells.length; i++) if (!RE_DELIM_CELL.test(cells[i])) return false;
        return true;
    }

    function isTableStart(line, next) {
        return line !== undefined && line.indexOf('|') !== -1 && indentOf(line) < 4 && isDelimRow(next) &&
            splitRow(line).length === splitRow(next).length;
    }

    // Whether a line begins a block that ends the paragraph or the list item above it.
    // `lazy`: any list marker ends it; otherwise only the ones that may interrupt a
    // paragraph — a bullet, or a number that is 1 — and only with text after them.
    function startsBlock(line, lazy) {
        if (indentOf(line) >= 4) return false;
        if (RE_FENCE.test(line) || RE_ATX.test(line) || RE_HR.test(line) || RE_QUOTE.test(line)) return true;
        var m = RE_LIST.exec(line);
        if (!m) return false;
        if (lazy) return true;
        if (!m[6].trim()) return false;
        return !m[3] || +m[3] === 1;
    }

    function parseList(lines, i, st, depth) {
        var n = lines.length, first = RE_LIST.exec(lines[i]);
        var ordered = !!first[3], kind = ordered ? first[4] : first[2];
        var list = { type: 'list', ordered: ordered, start: ordered ? +first[3] : 1, items: [], loose: false };
        while (i < n) {
            var m = RE_LIST.exec(lines[i]);
            if (!m || RE_HR.test(lines[i]) || (m[3] ? m[4] : m[2]) !== kind || !!m[3] !== ordered) break;
            var rest = m[6], spaces = m[5].length;
            var offset;
            if (!rest.trim()) offset = m[1].length + m[2].length + 1;
            else if (spaces >= 5) { offset = m[1].length + m[2].length + 1; rest = new Array(spaces).join(' ') + rest; }
            else offset = m[1].length + m[2].length + spaces;
            var item = [rest];
            i++;
            var gapAfter = false;
            while (i < n) {
                var l = lines[i];
                if (BLANK.test(l)) {
                    var j = i;
                    while (j < n && BLANK.test(lines[j])) j++;
                    if (j < n && indentOf(lines[j]) >= offset) {
                        for (; i < j; i++) item.push('');
                        continue;
                    }
                    var sib = j < n ? RE_LIST.exec(lines[j]) : null;
                    if (sib && !RE_HR.test(lines[j]) && (sib[3] ? sib[4] : sib[2]) === kind && !!sib[3] === ordered) {
                        gapAfter = true;
                        i = j;
                    }
                    break;
                }
                if (indentOf(l) >= offset) { item.push(l.slice(offset)); i++; continue; }
                var prev = item[item.length - 1];
                if (!BLANK.test(prev) && !startsBlock(l, true) && !RE_SETEXT.test(l)) { item.push(trimStart(l)); i++; continue; }
                break;
            }
            // A task is "[ ] " or "[x] " and then text, which is the checkbox's label: an item
            // that has none, or whose first block is not a paragraph, is an ordinary item.
            var task = null, plainFirst = item[0];
            var tm = /^\[([ xX])\][ \t]+(?=\S)/.exec(item[0]);
            if (tm) { task = tm[1] !== ' '; item[0] = item[0].slice(tm[0].length); }
            var kids = parseBlocks(item, st, depth + 1);
            if (task !== null && (!kids.length || kids[0].type !== 'p')) {
                task = null;
                item[0] = plainFirst;
                kids = parseBlocks(item, st, depth + 1);
            }
            var loose = false;
            for (var k = 1; k < kids.length; k++) if (kids[k].gap) loose = true;
            if (gapAfter || loose) list.loose = true;
            list.items.push({ kids: kids, task: task });
        }
        return { node: list, next: i };
    }

    function parseTable(lines, i) {
        var head = splitRow(lines[i]), delim = splitRow(lines[i + 1]);
        var aligns = delim.map(function (c) {
            var l = c.charAt(0) === ':', r = c.charAt(c.length - 1) === ':';
            return l && r ? 'center' : l ? 'left' : r ? 'right' : '';
        });
        var rows = [], j = i + 2;
        while (j < lines.length && !BLANK.test(lines[j]) && !startsBlock(lines[j], true)) {
            var cells = splitRow(lines[j]);
            while (cells.length < head.length) cells.push('');
            rows.push(cells.slice(0, head.length));
            j++;
        }
        return { node: { type: 'table', head: head, aligns: aligns, rows: rows }, next: j };
    }

    function parseBlocks(lines, st, depth) {
        var nodes = [], i = 0, n = lines.length, gap = false, m;
        function add(nd) { nd.gap = gap && nodes.length > 0; gap = false; nodes.push(nd); }

        if (depth > MAX_DEPTH) {
            var flat = lines.map(trimStart).join('\n').replace(/\s+$/, '');
            if (flat) add({ type: 'p', raw: flat });
            return nodes;
        }

        while (i < n) {
            var line = lines[i];
            if (BLANK.test(line)) { gap = true; i++; continue; }

            if (indentOf(line) >= 4) {
                var code = [];
                while (i < n && (BLANK.test(lines[i]) || indentOf(lines[i]) >= 4)) { code.push(lines[i].slice(4)); i++; }
                while (code.length && BLANK.test(code[code.length - 1])) { code.pop(); }
                add({ type: 'code', lang: '', text: code.join('\n') });
                continue;
            }

            if ((m = RE_FENCE.exec(line)) && !(m[2].charAt(0) === '`' && m[3].indexOf('`') !== -1)) {
                var fch = m[2].charAt(0), indent = m[1].length;
                var lang = unesc(m[3].trim().split(/\s+/)[0] || '');
                var closer = new RegExp('^ {0,3}' + (fch === '`' ? '`' : '~') + '{' + m[2].length + ',}[ \\t]*$');
                var body = [];
                for (i++; i < n && !closer.test(lines[i]); i++) {
                    var bl = lines[i], strip = Math.min(indent, indentOf(bl));
                    body.push(bl.slice(strip));
                }
                i++;
                add({ type: 'code', lang: lang, text: body.join('\n') });
                continue;
            }

            if ((m = RE_ATX.exec(line))) {
                add({ type: 'h', n: m[1].length, raw: m[2] || '' });
                i++; continue;
            }

            if (RE_HR.test(line)) { add({ type: 'hr' }); i++; continue; }

            if (RE_QUOTE.test(line)) {
                var q = [];
                while (i < n) {
                    var ql = lines[i], qm = /^ {0,3}>[ ]?(.*)$/.exec(ql);
                    if (qm) { q.push(qm[1]); i++; continue; }
                    if (BLANK.test(ql)) break;
                    var lastQ = q[q.length - 1];
                    if (!BLANK.test(lastQ) && !RE_FENCE.test(lastQ) && !RE_ATX.test(lastQ) && !RE_HR.test(lastQ) &&
                        !startsBlock(ql, true)) { q.push(ql); i++; continue; }
                    break;
                }
                add({ type: 'quote', kids: parseBlocks(q, st, depth + 1) });
                continue;
            }

            if (RE_LIST.test(line)) {
                var li = parseList(lines, i, st, depth);
                add(li.node); i = li.next;
                continue;
            }

            if (isTableStart(line, lines[i + 1])) {
                var tb = parseTable(lines, i);
                add(tb.node); i = tb.next;
                continue;
            }

            if ((m = RE_DEF.exec(line))) {
                var label = normLabel(m[1]);
                if (!st.refs[label]) {
                    var d = m[2].charAt(0) === '<' ? m[2].slice(1, -1) : m[2];
                    var t = m[3] ? unesc(m[3].slice(1, -1)) : null;
                    st.refs[label] = { dest: unesc(d), title: t };
                }
                i++; continue;
            }

            var para = [trimStart(line)], setext = 0;
            i++;
            while (i < n) {
                var pl = lines[i];
                if (BLANK.test(pl)) break;
                if (indentOf(pl) < 4 && (m = RE_SETEXT.exec(pl))) { setext = m[1].charAt(0) === '=' ? 1 : 2; i++; break; }
                if (startsBlock(pl, false) || isTableStart(pl, lines[i + 1])) break;
                para.push(trimStart(pl)); i++;
            }
            var raw = para.join('\n').replace(/\s+$/, '');
            add(setext ? { type: 'h', n: setext, raw: raw } : { type: 'p', raw: raw });
        }
        return nodes;
    }

    // ── Output ───────────────────────────────────────────────────────────────

    var CHECKBOX = { on: '<input type="checkbox" disabled checked> ', off: '<input type="checkbox" disabled> ' };

    function renderBlocks(nodes, tight, st, lead) {
        var out = '';
        for (var i = 0; i < nodes.length; i++) {
            var nd = nodes[i], pre = i === 0 && lead ? lead : '';
            if (pre && nd.type !== 'p') { out += pre; pre = ''; }
            switch (nd.type) {
                case 'p': {
                    // A task's checkbox and the text of its first paragraph are one label.
                    var text = pre ? '<label>' + pre + inline(nd.raw, st) + '</label>' : inline(nd.raw, st);
                    out += tight ? text : '<p>' + text + '</p>';
                    break;
                }
                case 'h':
                    out += '<h' + nd.n + '>' + inline(nd.raw.trim(), st) + '</h' + nd.n + '>';
                    break;
                case 'hr': out += '<hr>'; break;
                case 'code':
                    out += '<pre><code' + (/^[\w#+.\-]+$/.test(nd.lang) ? ' class="language-' + nd.lang + '"' : '') + '>' +
                        esc(nd.text) + '</code></pre>';
                    break;
                case 'quote':
                    out += '<blockquote>' + renderBlocks(nd.kids, false, st) + '</blockquote>';
                    break;
                case 'list': out += renderList(nd, st); break;
                case 'table': out += renderTable(nd, st); break;
            }
        }
        return out;
    }

    function renderList(list, st) {
        var tag = list.ordered ? 'ol' : 'ul', items = '';
        for (var i = 0; i < list.items.length; i++) {
            var it = list.items[i];
            if (it.task === null) {
                items += '<li>' + renderBlocks(it.kids, !list.loose, st) + '</li>';
            } else {
                var box = it.task ? CHECKBOX.on : CHECKBOX.off;
                items += '<li class="task-list-item">' + renderBlocks(it.kids, !list.loose, st, box) + '</li>';
            }
        }
        var attrs = list.ordered && list.start !== 1 ? ' start="' + list.start + '"' : '';
        return '<' + tag + attrs + '>' + items + '</' + tag + '>';
    }

    function renderTable(t, st) {
        var cell = function (tag, text, k) {
            var a = t.aligns[k] ? ' align="' + t.aligns[k] + '"' : '';
            return '<' + tag + a + '>' + inline(text, st) + '</' + tag + '>';
        };
        var out = '<table role="table" tabindex="0"><thead><tr>';
        for (var k = 0; k < t.head.length; k++) out += cell('th', t.head[k], k);
        out += '</tr></thead>';
        if (t.rows.length) {
            out += '<tbody>';
            for (var r = 0; r < t.rows.length; r++) {
                out += '<tr>';
                for (var c = 0; c < t.rows[r].length; c++) out += cell('td', t.rows[r][c], c);
                out += '</tr>';
            }
            out += '</tbody>';
        }
        return out + '</table>';
    }

    md.render = function (src) {
        if (!src) return '';
        src = String(src).replace(/\r\n?/g, '\n').replace(/\u0000/g, '�');
        var st = { refs: {} };
        var nodes = parseBlocks(src.split('\n').map(expandTabs), st, 0);
        return renderBlocks(nodes, false, st);
    };

})(window.sednaUi);

/* ── Markdown editor ─────────────────────────────────────────────────────────
   Toolbar + textarea + live preview inside one .md-editor root. Blazor owns the
   value through the textarea's two-way @bind (@bind:event="oninput"); toolbar
   edits mutate the textarea and dispatch a bubbling 'input' event so the binding
   picks them up — this code never calls back into .NET.

   init() is idempotent per editor, since Blazor re-renders its host. Call it with no
   argument to wire every .md-editor in the document, with a container to wire the ones
   inside it, or with an editor to wire exactly that one — an app renders editors and
   then calls init(), and does not have to know how many there are or hold a reference
   to each. From C#: ISednaUi.InitMarkdownAsync().
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    ui.md = {
        /* Counter for the per-editor radio group name. Private. */
        _seq: 0,

        init: function (root) {
            root = root || document;
            // An editor initialises itself; anything else initialises the editors
            // inside it. Each one is wired against its OWN root, so two editors on a
            // page get separate radio groups and separate listeners — which they would
            // not if a shared container were treated as the root.
            var editors = root.matches && root.matches('.md-editor')
                ? [root]
                : root.querySelectorAll('.md-editor');

            for (var i = 0; i < editors.length; i++) this._initOne(editors[i]);
        },

        /* One editor. Private: init() is the entry point. */
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

            // The Write/Preview switch is a .segmented control, so it is a radio
            // group: the checked state comes from the platform and CSS draws it with
            // :has(input:checked). Nothing here toggles a class.
            //
            // The radios need a shared `name` to be one group, and it has to be unique
            // per editor or two editors on a page fight over one selection. Assigned
            // here rather than in the markup, because only this code knows how many
            // roots exist.
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
                // Carry the height between panes (both are resize:vertical), reading
                // the visible one before the flip. The preview then fills the same box
                // and scrolls internally instead of ballooning its host on long text,
                // and a manual resize in either pane sticks across the switch.
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

        // Apply a toolbar command to the current selection, then fire the input
        // event so the binding captures the new value.
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
                // Expand the selection to whole lines, then prefix each.
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
                default: return;
            }
            ta.dispatchEvent(new Event('input', { bubbles: true }));
            ta.focus();
        },

        // Minimal, self-contained Markdown → HTML. HTML is escaped FIRST; only a
        // fixed set of block/inline constructs is re-introduced, and link hrefs
        // are scheme-checked. Not a spec-complete parser — enough for authored
        // prose, and safe enough that its output can be injected.
        render: function (src) {
            if (!src) return '';
            var esc = function (s) {
                return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
            };
            // Pull fenced code blocks out first so their contents are never formatted.
            var blocks = [];
            src = src.replace(/```([\s\S]*?)```/g, function (_, code) {
                blocks.push('<pre><code>' + esc(code.replace(/^\n/, '').replace(/\n$/, '')) + '</code></pre>');
                return '  B' + (blocks.length - 1) + ' ';
            });
            var inline = function (t) {
                t = esc(t);
                t = t.replace(/`([^`]+)`/g, '<code>$1</code>');
                t = t.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
                t = t.replace(/_([^_]+)_/g, '<em>$1</em>');
                t = t.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, function (_, txt, url) {
                    var safe = /^(https?:|mailto:|\/)/i.test(url) ? url : '#';
                    return '<a href="' + esc(safe) + '" target="_blank" rel="noopener">' + txt + '</a>';
                });
                return t;
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

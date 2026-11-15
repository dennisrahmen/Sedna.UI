/* ── Markdown editor ─────────────────────────────────────────────────────────
   Toolbar + textarea + live preview inside one .md-editor root. Blazor owns the
   value through the textarea's two-way @bind (@bind:event="oninput"); every edit — a
   toolbar command, a shortcut, a pasted link — reaches the textarea as an `input` event,
   which is the whole of its conversation with .NET.

   This part is the wiring: it finds the pieces of an editor, switches its view, keeps the
   preview and the counts current and runs full-screen. The commands, the keyboard and the
   paste and drop of files are 32-markdown-edit.js; the renderer is 31-markdown-render.js.

   init() is idempotent per editor, since Blazor re-renders its host. Call it with no
   argument to wire every .md-editor in the document, with a container to wire the ones
   inside it, or with an editor to wire exactly that one — an app renders editors and
   then calls init(), and does not have to know how many there are or hold a reference
   to each. From C#: ISednaUi.InitMarkdownAsync().

   The markup the app writes (all of it the app's — nothing here draws an element):

     .md-editor [data-md-view="write|preview|split"]
       toolbar     buttons with data-md-cmd="…"; radios with data-md-tab="write|preview|split"
       textarea    [data-md-input]
       preview     [data-md-preview]
       [data-md-count="words|characters|lines"]   a number, filled in as the text changes
       [data-md-file-notice]                       shown when a pasted or dropped file is not taken

   insert() puts the app's own text at the caret — from C#, ISednaUi.InsertMarkdownAsync().
   An image is `![description](address)`; the same syntax with the address of a video file
   — .mp4, .m4v, .mov, .webm or .ogv — is a video the browser plays, with controls. An app
   that uploads a picture inserts that text, with the address it uploaded to.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var md = ui.md = ui.md || {};

    /* Counter for the per-editor radio group name. Private. */
    md._seq = 0;

    md.init = function (root) {
        root = root || document;
        // An editor initialises itself; anything else initialises the editors
        // inside it. Each one is wired against its OWN root, so two editors on a
        // page get separate radio groups and separate listeners — which they would
        // not if a shared container were treated as the root.
        var editors = root.matches && root.matches('.md-editor')
            ? [root]
            : root.querySelectorAll('.md-editor');

        for (var i = 0; i < editors.length; i++) md._initOne(editors[i]);
    };

    // How many words, characters (code points) and lines a text holds.
    function counts(text) {
        var chars = 0, words = 0, inWord = false;
        for (var i = 0; i < text.length; i++) {
            var code = text.charCodeAt(i);
            if (code >= 0xdc00 && code <= 0xdfff) continue;
            chars++;
            var space = code === 32 || (code >= 9 && code <= 13) || code === 0xa0 || code === 0x2028 || code === 0x2029;
            if (space) inWord = false;
            else if (!inWord) { inWord = true; words++; }
        }
        return { words: words, characters: chars, lines: text ? text.split('\n').length : 0 };
    }

    /* One editor. Private: init() is the entry point. */
    md._initOne = function (root) {
        if (!root || root.dataset.mdReady === '1') return;
        root.dataset.mdReady = '1';
        var ta = root.querySelector('[data-md-input]');
        var preview = root.querySelector('[data-md-preview]');
        if (!ta) return;

        var timer = 0;
        var renderPreview = function () {
            if (preview) preview.innerHTML = md.render(ta.value);
        };
        var update = function () {
            // A long text is rendered once the reader pauses, so typing never waits on it.
            if (ta.value.length > 30000) {
                clearTimeout(timer);
                timer = setTimeout(renderPreview, 120);
            } else {
                renderPreview();
            }
            var numbers = root.querySelectorAll('[data-md-count]');
            if (numbers.length) {
                var c = counts(ta.value);
                for (var i = 0; i < numbers.length; i++) {
                    var kind = numbers[i].getAttribute('data-md-count');
                    if (Object.prototype.hasOwnProperty.call(c, kind)) numbers[i].textContent = String(c[kind]);
                }
            }
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
            var group = 'sedna-md-view-' + (++md._seq);
            var current = root.getAttribute('data-md-view') || 'write';
            views.forEach(function (r) {
                r.name = group;
                r.checked = r.getAttribute('data-md-tab') === current;
            });
        }

        // Switches the view. The height is carried between panes (both are
        // resize:vertical), reading the visible one before the flip. The preview then fills
        // the same box and scrolls internally instead of ballooning its host on long text,
        // and a manual resize in either pane sticks across the switch. Side by side, the
        // grid gives both the height the textarea has.
        var setView = function (view) {
            var was = root.getAttribute('data-md-view') || 'write';
            if (view === was) return;
            if (view === 'preview') {
                renderPreview();
                if (preview && was === 'write') preview.style.height = ta.offsetHeight + 'px';
            } else if (view === 'split') {
                renderPreview();
                if (preview) preview.style.height = '';
                if (was === 'preview' && preview && preview.offsetHeight) ta.style.height = preview.offsetHeight + 'px';
            } else {
                if (preview && preview.offsetHeight && was === 'preview') ta.style.height = preview.offsetHeight + 'px';
            }
            root.setAttribute('data-md-view', view);
        };

        root.addEventListener('click', function (e) {
            var cmdBtn = e.target.closest('[data-md-cmd]');
            if (!cmdBtn || !root.contains(cmdBtn)) return;
            e.preventDefault();
            var cmd = cmdBtn.getAttribute('data-md-cmd');
            if (cmd === 'fullscreen') md._fullscreen(root, cmdBtn);
            else md.apply(ta, cmd);
        });

        root.addEventListener('change', function (e) {
            var radio = e.target.closest('input[data-md-tab]');
            if (!radio || !root.contains(radio) || !radio.checked) return;
            setView(radio.getAttribute('data-md-tab'));
        });

        // Side by side, the preview follows the textarea: the same share of the way down.
        ta.addEventListener('scroll', function () {
            if (!preview || root.getAttribute('data-md-view') !== 'split') return;
            var from = ta.scrollHeight - ta.clientHeight, to = preview.scrollHeight - preview.clientHeight;
            if (from > 0 && to > 0) preview.scrollTop = ta.scrollTop / from * to;
        });

        ta.addEventListener('input', update);
        update();
        md._wire(root, ta);
    };

    // Full-screen: the editor fills the window, and the page behind it is inert, so
    // neither the keyboard nor a screen reader reaches what the editor covers. The root
    // keeps its place in the document; `data-md-fullscreen` is what the stylesheet draws.
    md._fullscreen = function (root, button, on) {
        var is = root.hasAttribute('data-md-fullscreen');
        if (on === undefined) on = !is;
        if (on === is) return;
        var ta = root.querySelector('[data-md-input]');
        var preview = root.querySelector('[data-md-preview]');
        var held = root._mdInert || [];
        if (on) {
            // The panes keep the heights they were resized to; full-screen sets its own.
            root._mdHeights = [ta && ta.style.height, preview && preview.style.height];
            if (ta) ta.style.height = '';
            if (preview) preview.style.height = '';
            for (var node = root; node.parentElement; node = node.parentElement) {
                var kids = node.parentElement.children;
                for (var i = 0; i < kids.length; i++) {
                    if (kids[i] !== node && !kids[i].inert && !/^(HEAD|SCRIPT|STYLE|LINK|TEMPLATE)$/.test(kids[i].tagName)) {
                        kids[i].inert = true;
                        held.push(kids[i]);
                    }
                }
            }
            root._mdInert = held;
            root.setAttribute('data-md-fullscreen', '');
        } else {
            root.removeAttribute('data-md-fullscreen');
            held.forEach(function (el) { el.inert = false; });
            root._mdInert = [];
            if (root._mdHeights) {
                if (ta) ta.style.height = root._mdHeights[0] || '';
                if (preview) preview.style.height = root._mdHeights[1] || '';
            }
        }
        root.querySelectorAll('[data-md-cmd="fullscreen"]').forEach(function (b) {
            b.setAttribute('aria-pressed', on ? 'true' : 'false');
        });
        if (ta) ta.focus();
    };

})(window.sednaUi);

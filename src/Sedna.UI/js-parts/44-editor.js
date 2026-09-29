/* ── The rich-text editor's front door (data-editor) ──────────────────────────
   Every page loads this; few have an editor. So this part holds no engine: the shared
   surface loader (42-surfaces.js) finds the app's `[data-editor]` elements and imports
   the editor module — Sedna.UI.editor.js, beside this file — the first time one of
   them is about to be seen, or at once with `data-editor-eager`. Until then, and on a
   page without the script, the app's textarea holds the value and the form posts it.

     <div class="editor" data-editor id="note">
       <div class="editor-toolbar" role="toolbar" aria-label="Formatting">…</div>
       <div class="editor-body prose" data-editor-body></div>
       <textarea name="note" data-editor-value hidden aria-label="Note"></textarea>
     </div>

   What it owns: `sednaUi.editor` — the handle for an editor, and the bridge
   ISednaEditors calls. Everything else is the module's.
   ─────────────────────────────────────────────────────────────────────────── */
(function (ui) {

    var SELECTOR = '[data-editor]';

    var editors = ui._.surface({
        name: 'editor',
        selector: SELECTOR,
        module: 'Sedna.UI.editor.js',
        eager: 'data-editor-eager'
    });

    ui.editor = {
        /* Called by Sedna.UI.lib.module.js once Blazor has started. Nothing else needs to. */
        release: editors.release,

        /* Starts every editor in `root` (the document by default) at once. A promise of
           how many. */
        init: editors.init,

        /* The handle for an editor — an element, or its id — starting it if need be. Its
           members are listed in docs/architecture.md. */
        get: editors.get,

        /* The ISednaEditors bridge. `method` is one of the handle's data-in, data-out
           members; the result is plain data or null. An id with no editor behind it warns
           and resolves null rather than throwing into a Blazor circuit. */
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

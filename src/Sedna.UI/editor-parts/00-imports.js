/* Sedna.UI — the rich-text editor.
   ───────────────────────────────────────────────────────────────────────────
   An ES module, imported by Sedna.UI.js the first time a page shows a `[data-editor]`
   element. No app references this file: a page without an editor never downloads it,
   or the engine below it.

   Quill edits the document. Everything around the document — the toolbar and its
   buttons, the link form, the labels, the words — is the app's own markup with the
   library's classes, and the value is the app's own `<textarea>`, which the form posts
   and Blazor binds as it would any other. What this module adds is the part markup
   cannot be:

     * the document, loaded from the textarea and written back to it as HTML, with the
       events a textarea sends, so `@bind`, a form post and a page without Blazor all
       read the same field;
     * the app's toolbar driving it — every button's pressed state kept current, and one
       tab stop with the arrow keys between its buttons;
     * only the formats the toolbar offers, so what is pasted is cut down to them;
     * the link form, placed at the text it is for;
     * the editable element's name, description and state, taken from the textarea's.

   The parts in editor-parts/ are this one module cut into files. They share its
   top-level scope: only 00 imports and only 99 exports. See editor-parts/CLAUDE.md.
   ─────────────────────────────────────────────────────────────────────────── */
import Quill from '../lib/quill/quill.js';

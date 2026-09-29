/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two. A
   script reaches an editor through `sednaUi.editor.get(el)`, which returns the handle
   `attach` resolves — never through an import of this file.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };

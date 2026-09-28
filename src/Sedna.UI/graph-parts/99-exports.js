/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these three.
   A script reaches a graph through `sednaUi.graph.get(el)`, which returns the handle
   `attach` resolves — never through an import of this file, whose URL is the package's
   business.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep, normalise };

/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two. A
   script reaches a chart through `sednaUi.chart.get()`, never through an import of
   this file, whose URL is the package's business.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };

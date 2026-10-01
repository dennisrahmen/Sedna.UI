/* ── The module's surface ─────────────────────────────────────────────────────
   Only the front door in Sedna.UI.js imports this module, and it calls these two.
   The app drives an orb through its attributes and never through an import of this
   file, whose URL is the package's business.
   ─────────────────────────────────────────────────────────────────────────── */
export { attach, sweep };

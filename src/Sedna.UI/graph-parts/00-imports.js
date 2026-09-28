/* Sedna.UI — the graph.
   ───────────────────────────────────────────────────────────────────────────
   An ES module, imported by Sedna.UI.js the first time a page shows a `[data-graph]`
   element. No app references this file: a page without a graph never downloads it,
   or the engine below it.

   cytoscape.js draws the canvas and runs the layouts. Everything a reader sees AROUND
   the canvas — the toolbar, the legend, the filters, the panel beside it, the tooltip,
   the empty state, the minimap's frame — is the app's own markup with the library's
   classes. What this module adds is the part markup cannot be:

     * the records, read from the app's markup, a URL or a call, into one model;
     * every colour on the canvas taken from a Sedna.UI token, repainted whenever the
       theme, the variant, the colour-vision setting or forced colours change;
     * layouts that read a real graph well — islands per group, rings around a record,
       hierarchies, compound groups — and names placed the way a map places them;
     * filters, search, focus, selection and the controls that drive them;
     * a keyboard and screen-reader model for a surface that is otherwise a picture;
     * the minimap, the SVG and PNG export, and the plugins, loaded when first used.

   The parts in graph-parts/ are this one module cut into files. They share its
   top-level scope: only 00 imports and only 99 exports. See graph-parts/CLAUDE.md.
   ─────────────────────────────────────────────────────────────────────────── */
import cytoscape from '../lib/cytoscape/cytoscape.js';

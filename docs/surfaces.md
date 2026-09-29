# Surfaces — tier 3

Most of Sedna.UI is markup the app writes and classes that paint it: the frame (tier 1) and the paint
(tier 2). A **surface** is the third tier: something the library draws or drives itself, because the
app has nothing to write for it — a toast's one line, the hint bubble for a `data-tip`, the canvas a
graph draws its records on, the document inside a rich-text editor or a Markdown preview.

A surface is not an exception to "markup belongs to the app". It is a tier with a stricter contract
of its own, and it is admitted to the library only by passing all of it.

## The registry

The one list of surfaces. `SurfaceRegistryTests` and `SurfacePageTests` read this table and hold every
row to the contract below, so a surface cannot exist without a row, and a row cannot exist without
what it names.

| Surface | Delivery | Reference | Stylesheet | Script | Engine | Catalogue |
|---|---|---|---|---|---|---|
| Toast | drawn | [architecture.md](architecture.md#javascript) | `38-toasts.css` | `js-parts/51-toast.js` | — | `/toast` |
| Hover hint | drawn | [architecture.md](architecture.md#javascript) | `17-frame-hover-hints.css` | `js-parts/20-tips.js` | — | `/hover-hints` |
| Markdown preview | drawn | [architecture.md](architecture.md#javascript) | `44-markdown.css` | `js-parts/30-markdown.js` | — | `/markdown` |
| Graph | shipped | [graph.md](graph.md) | `54-graph.css` | `js-parts/43-graph.js`, `graph-parts/` | `wwwroot/lib/cytoscape/` | `/graph` |
| Rich-text editor | shipped | [editor.md](editor.md) | `44-rich-text.css` | `js-parts/44-editor.js`, `editor-parts/` | `wwwroot/lib/quill/` | `/editor` |

**Delivery** is how the surface reaches a page:

- **drawn** — the library's own script draws it, with no engine behind it.
- **shipped** — the library ships an engine, vendored under `wwwroot/lib/` by a `build/vendor-*.sh`
  script, and loads it on demand through its own ES module — generated from `<name>-parts/` — which
  the shared loader in `js-parts/42-surfaces.js` imports the first time a page shows one.

A surface's **catalogue** route is its page; a page whose route extends it (`/graph-layouts`) belongs
to it too. Every one of those pages carries the tier 3 badge, and no other page does.

## The contract

Every surface meets all of these. Each is checked by a test wherever a test can check it.

1. **Nothing to author.** What a surface draws is the app's data or the app's words — records, a
   document, a message — never a form, a dialog, a control or a word of its own. The one word the
   library writes is a toast's dismiss label, and an app sets it once.
2. **Framed by app markup.** Every toolbar, legend, filter, menu, panel, tooltip and empty state around
   a surface is tier 2 markup the app writes. The script fills it — `textContent` into `data-*` slots,
   rows cloned from the app's `<template>` — and places it; it never builds it. A surface's script
   writes no markup as a string, with one exception: the document a surface renders from the app's
   own text, escaped before anything else — the Markdown preview's `innerHTML = render(…)`.
3. **Colour from tokens only.** Every colour is a token, read through the browser at runtime, so the
   variant, the colour-vision setting, the contrast setting, forced colours and an app's own brand all
   reach it — and repainted when any of them changes. No literal colour, and none handed over by the
   app.
4. **Loaded on demand.** Nothing is added to the host page. A page that never shows the surface pays
   for at most a small front door in `Sedna.UI.js`; a shipped engine is imported, relative to the
   library's own script, the first time a page shows one.
5. **Reachable without a pointer, readable without the script.** A keyboard path through everything a
   pointer can do, announcements in the app's words, a text alternative in the app's markup, and — where
   the surface is content — something readable when scripting is off.
6. **Plain events, a typed handle, no exceptions across the boundary.** What the reader does arrives
   as bubbling DOM events with plain data, bindable from Razor through `EventHandlers`. A C# service,
   when there is one, addresses the surface by its element's id and answers null for one that is not
   there, rather than throwing into a Blazor circuit.
7. **Third-party code under the vendoring rules.** Pinned to a version and to the sha512 of its
   tarball, under a permissive licence — never a copyleft one — with its licence beside it and its
   version in `THIRD-PARTY-NOTICES.md`, checked by a test. **One engine per job:** a surface is never
   added beside another that does the same job.

## Adding a surface

A new surface is the owner's decision, not a pull request's. Once agreed:

1. Add its row above. The tests fail until everything the row names exists.
2. Write its reference — a page in `docs/` for anything with a contract of its own.
3. Meet the contract; the tests hold rules 2, 3, 4 and 7, and the browser tests of the surface
   itself hold 5 and 6.
4. Give it a catalogue page with the tier 3 badge.

`CLAUDE.md` does not change: it points here.

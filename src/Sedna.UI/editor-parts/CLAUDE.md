# `editor-parts/` — how the editor module is authored

This directory **is** `wwwroot/js/Sedna.UI.editor.js`, the ES module the editor's front door
(`js-parts/44-editor.js`) hands to the shared surface loader (`js-parts/42-surfaces.js`), which imports
it the first time a page shows a `[data-editor]`. The module is generated from it and must never be
edited by hand.

```bash
build/bundle-js.sh            # regenerates Sedna.UI.js and every surface module
build/bundle-js.sh --check    # fail if any is out of date (CI runs this)
```

`The_editor_module_matches_its_parts` fails the build if the two disagree. The reference for what the
module does — the markup, the controls, the value, the events, the handle — is `docs/editor.md`; keep it
in step with any change to the surface.

## One module, cut into files

The parts are concatenated into **one** module and share its top-level scope. The build checks both:

- **Only `00-imports.js` imports** — Quill, by relative path. Nothing is imported later.
- **Only `99-exports.js` exports** — `attach` and `sweep`, for the loader. A script reaches an editor
  through `sednaUi.editor.get()`, never through an import of this file.

Everything an editor holds lives on its own object, `e`, made in `70-editor.js`.

| Range | What lives there |
|---|---|
| `00` | the import |
| `10` | reading the app's markup, and the formats it offers |
| `20` | the value — the textarea, loading, writing back, `required` |
| `30` | the toolbar |
| `40` | the link form |
| `70` | an editor: starting, its label and state, the handle, the bridge |
| `99` | the exports |

## Rules

- **The textarea is the field.** Every change is written back to it with `input`, and `change` when the
  focus leaves the editor; a value written to it from outside is loaded. Never keep the document
  anywhere a form post or a Blazor binding cannot read it.
- **The app's markup is the UI.** The toolbar, the link form and every word on them are the app's. Fill,
  toggle `hidden`, `aria-pressed` and `disabled`, set a transform — never build an element the reader
  sees, never `innerHTML`. Quill writes the document; nothing else here writes markup.
- **Quill's own toolbar, themes and stylesheet stay off.** `44-rich-text.css` dresses what Quill writes.
- **An exception crossing into Blazor tears down the circuit.** `invoke` answers null for anything it
  cannot do, and a start that fails leaves `data-editor-state="error"`.
- **Name nothing real** — see the root `CLAUDE.md`. The module ships to every app, comments included.

## Updating the engine

`build/vendor-quill.sh` owns `wwwroot/lib/quill/`. Change the pin there, run it, run the editor tests,
and update `THIRD-PARTY-NOTICES.md`. Quill's API is reached through `e.quill` everywhere in this module;
a Quill release that renames something breaks here, and nowhere in an app.

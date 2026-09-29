# Rich-text editor

A document a reader writes with formatting — a note, a description, a message — edited in place and
posted as HTML. The editor is a tier 3 surface (see [surfaces.md](surfaces.md)): the app writes the
frame, the toolbar, the link form and a `<textarea>` that holds the value; the library ships the
engine, [Quill](https://quilljs.com/) 2, and loads it the first time a page shows an editor.

## An editor

```html
<label for="note-value">Handover note</label>
<div class="editor" data-editor id="note">
  <div class="editor-toolbar" role="toolbar" aria-label="Formatting">
    <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="bold" aria-label="Bold"><i class="ri-bold"></i></button>
    <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="italic" aria-label="Italic"><i class="ri-italic"></i></button>
    <span class="editor-sep" role="separator"></span>
    <button class="btn btn-ghost btn-icon btn-sm" type="button" data-editor-format="list" value="bullet" aria-label="Bulleted list"><i class="ri-list-unordered"></i></button>
  </div>
  <div class="editor-body prose" data-editor-body></div>
  <textarea id="note-value" name="note" data-editor-value hidden placeholder="What the next shift needs to know"></textarea>
</div>
```

- **`[data-editor]`** on the `.editor` frame. `data-editor-eager` starts it at once rather than when it
  scrolls within a screen of the viewport.
- **`[data-editor-body]`** — or the `.editor-body` — is where the document is edited. Put `.prose` on it
  so what is typed looks like what is later shown.
- **`textarea[data-editor-value]`** is the field. It holds the document as HTML: the editor loads from
  it, writes every change back to it, and a form posts it. Keep it `hidden`; without the script it shows
  in the frame instead, so the page still works.
- **`data-editor-state`** on the element says where it is: `loading`, `ready`, or `error` when the
  markup lacks a textarea or a body.

The editable element takes the textarea's label — its `aria-labelledby`, a `<label for>` pointing at
it, or its `aria-label` — and its `aria-describedby`, `placeholder`, `required`, `disabled` and
`readonly`, and follows them when they change.

## The toolbar

Any element inside the editor with `data-editor-format` or `data-editor-action` is a control. A
toolbar with `role="toolbar"` is one tab stop: the arrow keys, Home and End move between its buttons.

| Control | Does |
|---|---|
| `data-editor-format="bold"`, `italic`, `underline`, `strike`, `code` | Toggles the format on the selection; `aria-pressed` follows the text under the caret. |
| `data-editor-format="script" value="sub"` or `"super"` | Subscript, superscript. |
| `data-editor-format="header" value="2"` | A heading level, 1–6. On a `<select>`, the option chosen — `""` is a paragraph. |
| `data-editor-format="list" value="bullet"`, `"ordered"`, `"check"` | A list. Tab and Shift+Tab indent an item. |
| `data-editor-format="blockquote"`, `"code-block"` | A quote, a code block. |
| `data-editor-format="align" value="center"`, `"right"`, `"justify"` | Alignment; no value is the start edge. |
| `data-editor-format="link"` | Opens the link form. |
| `data-editor-action="undo"`, `"redo"` | Undo, redo — `disabled` while there is nothing to undo or redo. |
| `data-editor-action="indent"`, `"outdent"` | One level in or out. |
| `data-editor-action="clean"` | Removes the formatting from the selection. |

**The formats the editor accepts are the ones its toolbar offers.** What is pasted in is cut down to
them, so the document never holds what the reader could not have written. `data-editor-formats="bold
italic link"` on the editor names them instead, for keyboard shortcuts without a button.

A button pressed with the pointer leaves the focus and the selection in the document.

## The link form

The app's own form, written once inside the editor and opened at the text it is for:

```html
<div class="editor-link" data-editor-link hidden>
  <input class="form-input" type="url" data-editor-link-url aria-label="Address" placeholder="https://" />
  <button class="btn btn-sm" type="button" data-editor-link-save>Save</button>
  <button class="btn btn-ghost btn-sm" type="button" data-editor-link-remove>Remove</button>
</div>
```

It is filled with the address of the link under the caret, and **Remove** shows only then.
`data-editor-link-cancel` closes it. Enter saves, Escape closes, and a press outside it closes it.
With no text selected, the address is written as the link's text. Only `http`, `https`, `mailto`,
`tel` and `sms` addresses are kept; anything else is written as `about:blank`.

## The keyboard

| Keys | In the document |
|---|---|
| Ctrl+B, Ctrl+I, Ctrl+U | Bold, italic, underline — where the editor accepts them. |
| Ctrl+K | The link form. |
| Ctrl+Z, Ctrl+Y / Ctrl+Shift+Z | Undo, redo. |
| Tab, Shift+Tab | In a list or a code block, indent and outdent; elsewhere, leave the document as from a textarea. |
| Alt+F10 | To the toolbar. Escape there comes back. |

Cmd replaces Ctrl on macOS.

## The value

The textarea is the field, so everything that reads a field reads the editor:

- **A form posts it** under the textarea's `name` — with or without the script.
- **Blazor binds it** with `@bind`, like any textarea: the editor sends the textarea's `input` at each
  change and `change` when it loses the focus after one, so `@bind` and `@bind:event="oninput"` both
  work. A value the app writes — a bound field changed in C# — is loaded into the editor.
- **A form reset** puts the document back.
- **`required`** is held by the editor: an empty one stops the submit, is marked `aria-invalid` and takes
  the focus.

The HTML written is the editor's own: lists as `<ul>` and `<ol>`, headings, paragraphs, links, and only
the formats the editor accepts. A value loaded from the textarea is cut down the same way. That is a
convenience, not a sanitiser — what arrives at the server is still the server's to check.

## Events

Bubbling from the `[data-editor]` element, with plain data in `detail`:

| Event | Detail |
|---|---|
| `sedna-editor-ready` | `{ html, text, length, isEmpty }`, once the document has loaded. |
| `sedna-editor-change` | The same, after each change. |

From Razor, with `@using Sedna.UI`: `@onsedna-editor-ready` and `@onsedna-editor-change`, each a
`SednaEditorEventArgs`.

## Script

`sednaUi.editor.get(el or id)` resolves the handle, starting the editor if need be.

| Member | |
|---|---|
| `state()` | `{ html, text, length, isEmpty }`. |
| `html()` | The document as HTML. |
| `set(html)` | Replaces the document, as a paste would; the textarea follows with its events. |
| `insert(text)` | Types text at the caret, or at the end. |
| `focus()` | Puts the focus in the document. |
| `enable(on)` | Lets the reader edit, or stops them — by the textarea's `disabled`. |
| `quill` | The engine. Quill's own API, not versioned by Sedna.UI. |
| `destroy()` | Takes the editor down; the textarea is left as it is. |

`sednaUi.editor.init(root)` starts every editor in `root` at once.

## C#

`ISednaEditors`, registered by `AddSednaUi()`, by the editor element's id:

| Method | |
|---|---|
| `GetStateAsync(id)` | A `SednaEditorState` — `Html`, `Text`, `Length`, `IsEmpty` — or null when there is no such editor. |
| `SetHtmlAsync(id, html)` | Replaces the document. |
| `InsertTextAsync(id, text)` | Types text at the caret. |
| `FocusAsync(id)` | Puts the focus in the document. |
| `SetEnabledAsync(id, enabled)` | Lets the reader edit, or stops them. |

The value itself needs none of these: bind the textarea.

```razor
<div class="editor" data-editor id="note">
  …the toolbar…
  <div class="editor-body prose" data-editor-body></div>
  <textarea data-editor-value hidden aria-label="Note" @bind="Note"></textarea>
</div>
```

## Engine

Quill 2.0.3, vendored into `wwwroot/lib/quill/` by `build/vendor-quill.sh` with the licences of what
its build bundles; see `THIRD-PARTY-NOTICES.md`. Quill's own toolbar, themes and stylesheet are not
used: the toolbar is the app's, and `44-rich-text.css` dresses the document Quill writes.

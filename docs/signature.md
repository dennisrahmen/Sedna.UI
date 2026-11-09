# Signature pad

A signature drawn with a mouse, a pen or a finger, posted with the form. A tier 3 surface — see
[surfaces.md](surfaces.md) — delivered **drawn**: the library's own module draws the strokes, with no
engine behind it. The app writes the frame, the hint, the buttons, the field that holds the value and
the typed alternative.

## Markup

```html
<div class="form-field">
  <span class="form-label" id="sign-label">Signature</span>
  <div class="form-signature" data-signature>
    <div class="form-signature-pad" data-signature-pad aria-labelledby="sign-label" aria-describedby="sign-alt">
      <span class="form-signature-hint">Sign here</span>
    </div>
    <input type="hidden" name="signature" data-signature-value>
    <div class="form-signature-actions">
      <button class="btn btn--sm btn--ghost" type="button" data-signature-undo><i class="ri-arrow-go-back-line"></i> <span>Undo</span></button>
      <button class="btn btn--sm btn--ghost" type="button" data-signature-clear><i class="ri-eraser-line"></i> <span>Clear</span></button>
    </div>
  </div>
  <label class="form-label" for="sign-name">Or type your full name</label>
  <input class="form-input" id="sign-name" name="signatureName" autocomplete="name">
  <p class="form-hint" id="sign-alt">Drawing needs a pointer. Typing your full name signs the form as well.</p>
</div>
```

| Attribute | On | Does |
|---|---|---|
| `data-signature` | the `.form-signature` frame | Starts a pad on it |
| `data-signature-pad` | the `.form-signature-pad` inside it | Where the strokes are drawn. Given `role="img"` when it has no role; its name is the app's |
| `data-signature-value` | an `<input>` or `<textarea>` inside it | The field: the value, what the form posts |
| `data-signature-undo` | a button inside it | Takes the last stroke away |
| `data-signature-clear` | a button inside it | Takes every stroke away |
| `data-signature-eager` | the frame | Start at once rather than when the pad comes within a screen of the viewport |

The script writes these on the frame:

| Attribute | Means |
|---|---|
| `data-signature-state` | `ready`, or `error` when the frame lacks the pad or the field |
| `data-signature-inked` | The pad holds at least one stroke. `.form-signature-hint` is hidden while it is set |

The undo and clear buttons are `disabled` while there is nothing to take away, and while the pad is
locked.

## The value

**The field is the value**, as the textarea is the [rich-text editor's](editor.md). The pad loads its
strokes from the field when it starts, and writes the field when a stroke is finished, on an undo and
on a clear, with an `input` and then a `change` event, both bubbling — so a form posts it, and
`@bind` reads it in Blazor. A value written to the field from outside, by Blazor or a script, is drawn;
a form reset puts the pad back with the field. A hidden input keeps its value through a reset — its value
is its default — so where a reset button should clear the signature, make the field a
`<textarea hidden>`.

The value is **SVG path data**, never pixels: one subpath per stroke, in a box 1000 units wide whose
height follows the pad's ratio — 1000 × 333 at the default 3 / 1 — with y downwards from the top left.
A stroke is curved through the midpoints of the points the pointer gave, `M p0 Q p1 mid … L pn`, and a
dot is `M p L p`. Coordinates carry one decimal. The geometry is the same on every screen and in
every theme, and carries no colour: draw it in the ink the place it is shown needs.

```html
<svg viewBox="0 0 1000 333" fill="none" stroke="currentColor" stroke-width="7"
     stroke-linecap="round" stroke-linejoin="round" role="img" aria-label="Signature of Alex Fischer">
  <path d="M120 210Q140 180 160 190L180 200" />
</svg>
```

An empty pad is the empty string. A value the pad did not write — anything but `M`, `Q` and `L`
commands with absolute coordinates — leaves the pad empty and the field as it is until the reader draws.

## States

- **Disabled** — `disabled` on the field. Nothing can be drawn, the pad is dimmed, and the form does not
  post the field.
- **Read-only** — `readonly` on the field. The signature shows at full strength and nothing can be
  drawn; the form still posts it.
- **Rejected** — `aria-invalid="true"` on the field draws the pad's edge in the danger colour, and
  **unusual**, `data-state="warn"`, in the warning colour, as on
  every control. Whether a signature is required, and what an empty one means, is the app's to check.

The pad follows `disabled` and `readonly` when they change.

## Size and ink

| Token | Default | Is |
|---|---|---|
| `--signature-ink` | `var(--fg)` | The strokes' colour, read by the script through the pad |
| `--signature-ink-width` | `2.5px` | The strokes' width, in CSS pixels at any pad size |
| `--signature-ratio` | `3 / 1` | The pad's width to its height. The value's box follows it |

The pad fills its frame's width, up to `36rem` for `.form-signature`. A new size redraws the strokes
scaled to the width, so a signature drawn on a phone and shown on a desktop is the same signature. The
canvas is drawn at the device's pixel ratio, up to 3. The ink repaints when the theme, the variant,
colour vision, contrast or forced colours change.

## Pointer input

A stroke is the primary pointer pressed and moved: a mouse's main button, a pen, a finger. A second
finger is ignored, and the canvas takes the touch rather than scrolling the page under it — except
while the pad is locked. Pressure is not read: every stroke has the ink's one width.

## Without a pointer

**A signature pad cannot be operated from the keyboard**, and a reader using a screen reader, a switch
or voice control cannot draw one. So a pad is never the only way to sign: the app writes an
alternative beside it — a typed full name in a `.form-input`, as above, which the app accepts in place of
a drawing — and says so in the hint the pad's `aria-describedby` points at. The pad's name — its
`aria-label` or `aria-labelledby` — is the app's, and says what the pad is for.

## Lifecycle

`js-parts/47-signature.js` registers `[data-signature]` with the surface loader, which imports
`Sedna.UI.signature.js` relative to the main script the first time a page shows one, starts each pad as
it comes within a screen of the viewport, and disposes one whose element has gone. On a prerendered
Blazor page pads wait for the interactive render, as every surface does.

## Without the script

The pad is an empty field with its hint and its line, and nothing can be drawn: the typed name is how
the reader signs.

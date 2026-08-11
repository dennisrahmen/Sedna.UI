# Accessibility

What the library guarantees, and what it cannot do for you.

The short version: **the CSS supplies appearance, the platform supplies behaviour, and the ARIA is
yours.** A class cannot know that three buttons are a tablist. Wherever the markup has to carry
something, it is listed below and repeated in the class's own comment.

## What the library guarantees

- **Nothing requires JavaScript.** Every class applies with scripting blocked or broken, including the
  whole frame. The collapsed rail's tooltips are CSS; the user menu's state is C#.
- **No colour-only signals.** Every state that matters carries a second channel — a leading rule, a
  filled-versus-outlined marker, an arrow, an icon. Deltas are the clearest case: `--good` / `--bad`
  set the colour, and the arrow you put inside carries the direction.
- **The colour-blind palette is a token remap**, so it composes with either theme and changes no
  layout. A browser test asserts that flipping it moves no geometry.
- **Focus is always visible.** Every interactive class has a `:focus-visible` ring. Mouse focus does
  not draw one; keyboard focus always does.
- **Forced colours works.** Every focus ring in this library is a `box-shadow`, and **forced colours
  does not paint `box-shadow` at all** — so each one is restated as an `outline`, which it does paint.
  Without that the library would be unusable by keyboard for exactly the people most likely to be in
  that mode. Selected and current states are restated as outlines for the same reason: they are
  backgrounds, and backgrounds are flattened away.
- **Reduced motion is honoured in one place.** `95-reduced-motion.css` switches off every animation and
  transition in the library, and `ReducedMotionTests` fails on any that has no off switch. Nothing there
  removes information: the spinner still reads as busy while static, and the indeterminate progress bar
  is filled rather than blanked.
- **RTL mirrors from `dir="rtl"`** with almost no rules, because every directional property is a
  logical one. A test fails on a physical one without a written justification.
- **`.visually-hidden` keeps text in the accessibility tree.** It is not `display: none` and not a
  zero-size box — both remove the element from the tree as well, which is the opposite of the point.

## What your markup has to supply

| Class | You must add |
|---|---|
| `.tabs` / `.tab` / `.tab-panel` | `role="tablist"` / `role="tab"` + `aria-selected` + `aria-controls` / `role="tabpanel"`, **and arrow-key movement with a roving tabindex**. `data-tabs` does the keyboard part for you. |
| `.menu` | `aria-expanded` on the trigger. Nothing else — see below on why there is no `role="menu"`. |
| `.form-input` when rejected | `aria-invalid="true"` (the styling keys off it, not off a class) and `aria-describedby` pointing at the `.form-error` |
| `.form-label--required` | `required` on the control. The `*` is decoration. |
| `.segmented` | one `name` shared by every radio, and a group name via `<fieldset>` or `aria-labelledby` |
| `.avatar` | the person's name in text, an `alt`, or a `.visually-hidden` span. Initials are not a name. |
| `.chip-dismiss` | `aria-label="Remove <the value>"`. `×` alone is announced as "times". |
| `.pagination` | `<nav aria-label="Pagination">`, `aria-current="page"`, and a real `href` per page |
| `.breadcrumb` | `aria-current="page"` on the last item, which stays a `<span>` |
| `.table` sorting | `aria-sort` on the `<th>` — the arrow is drawn from it, so they cannot disagree |
| `.table` selection | `aria-selected` on the `<tr>` |
| `.stat-delta` | an arrow icon. The colour is not the direction. |
| `.skip-link` | `href="#main"`, and `tabindex="-1"` on `<main id="main">` so the jump moves focus rather than only scrolling |
| any icon-only button | `aria-label`, plus `data-tip` when the purpose is not obvious — and `.btn-icon`, which squares it to the shared control height instead of leaving a wide box around one glyph |
| `.dropzone` | a real `<input type="file">` inside it. Drag and drop alone is unreachable by keyboard; `data-dropzone` adds the drag handling on top of the input, never instead of it. |

## Three deliberate omissions

**No `role="menu"`.** That role promises arrow-key navigation and a roving tabindex, and a menu that
claims it without implementing it is *worse* than one that claims nothing: the items stop being
reachable the way they appear to be. `.menu` items are ordinary links and buttons that tab. The
disclosure pattern — `aria-expanded` on the trigger, no role on the panel — is correct and complete.

**No `role="tree"`.** Same reasoning. `.tree` is built from `<details>`, which is honest about being a
set of disclosures; a real tree needs single-tab-stop navigation through a flattened view.

**No `role="listbox"` on the palette.** A combobox needs `aria-activedescendant` on the input, which is
what the palette does, rather than a listbox whose keyboard contract it would only half implement.

## Where the platform does the work

Four things are built on native elements specifically so the behaviour is not hand-rolled:

| Built on | What comes free |
|---|---|
| `<dialog>.showModal()` — `.modal` via `sednaUi.confirm()`, `.palette`, `.drawer` | top layer, focus trap, Escape, inert content behind |
| `<details>`/`<summary>` — `.accordion`, `.tree`, `.nav-group` | open state, keyboard operation, announcement; `name` makes an accordion exclusive with no JS |
| `<input type="radio">` — `.segmented` | arrow keys, group semantics, chosen state |
| `<label>` wrapping the input — `.form-check`, `.switch`, `.form-file` | the whole label is the target, and Enter works |

A hand-rolled focus trap is a lot of code that is usually subtly wrong. If you need a modal surface,
reach for `<dialog>` before reaching for `.modal-backdrop`.

## Known gaps

- **A disabled checkbox and radio are not dimmed.** Adding it would change how every released app looks
  with no app edit, which the release rules make Major. Queued for the next major.
- **No automated audit yet.** `axe-core` via Playwright is planned; today the browser tests assert
  computed styles and console cleanliness, not WCAG rules.
- **Token contrast is not verified.** The default palette was chosen by eye. A contrast audit across
  all four theme combinations is planned.
- **`.table--stack` throws away column alignment** by design, so comparing one value across rows stops
  being possible below 640px. It is opt-in for that reason.

## Browser floor

**Chromium — current Chrome and Edge.** The apps this library serves are deployed to a managed
Windows estate, and that is the whole of it.

Two consequences visible in the CSS:

- **CSS anchor positioning is used, and load-bearing.** The collapsed rail's hover flyout and
  `.popover` both depend on it. In an engine without it they do not degrade — the flyout lands at its
  static position and the popover centres itself in the viewport. `.menu` is deliberately *not*
  anchored this way: `.menu-anchor` uses `position: relative` and works anywhere.
- **No scroll-driven animations** — they fail *incorrectly*: a browser that drops `animation-timeline`
  leaves the rest of the `animation` shorthand running, so the animation plays on a timer instead of
  not at all. That is a different kind of failure from the one above, which is why it stays out.

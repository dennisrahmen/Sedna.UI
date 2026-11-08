# Accessibility

What the library guarantees, and what it cannot do for you.

The short version: **the CSS supplies appearance, the platform supplies behaviour, and the ARIA is
yours.** A class cannot know that three buttons are a tablist. Wherever the markup has to carry
something, it is listed below and repeated in the class's own comment.

## What the library guarantees

- **Nothing requires JavaScript.** Every class applies with scripting blocked or broken, including the
  whole frame. The collapsed rail's tooltips are CSS; the user menu's state is C#. The one surface that
  cannot exist without a script — a graph's canvas — is replaced by the graph's own list of records and
  links when scripting is off.
- **No colour-only signals.** Every state that matters carries a second channel — a leading rule, a
  filled-versus-outlined marker, an arrow, an icon. Deltas are the clearest case: `--good` / `--bad`
  set the colour, and the arrow you put inside carries the direction.
- **F6 moves between the frame's regions.** F6 and Shift+F6 cycle focus through the landmarks, the
  topbar, sidebar and bottom bar and an open modeless drawer, skipping hidden ones; inside a modal
  dialog the key is left to the browser. The rules are in
  [architecture](architecture.md#moving-between-regions).
- **States are ARIA attributes, not classes.** The library styles `aria-pressed`, `aria-selected`,
  `aria-current`, `aria-expanded`, `aria-invalid`, `aria-busy` and the platform's pseudo-classes, so
  the look and what a screen reader hears cannot disagree. The full list is in
  [architecture](architecture.md#interaction-states).
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
| A field when rejected | `aria-invalid="true"` on the control — on the input inside an `.input-group` or a combo box, not on the frame; the styling keys off it, not off a class — and `aria-describedby` pointing at the `.form-error` |
| A field when unusual | `data-state="warn"` on the control, and `aria-describedby` pointing at the `.form-warning`. No ARIA attribute says "unusual", so the message is what a screen reader hears. |
| `.form-label--required` | `required` on the control. The `*` is decoration. |
| `.form-actions--save-bar` | the reason in words in `.form-actions-status` — the warning icon is decoration — and `data-dirty` taken off as soon as the work is saved, so the bar and the leave prompt go with it |
| A session timeout warning | `role="alertdialog"` with `aria-labelledby` and `aria-describedby` on the app's `<dialog class="modal">`, opened at least two minutes before the end (WCAG 2.2.1), a button that extends the session, and the countdown as a `<time data-countdown>` that is not a live region |
| `.segmented` | one `name` shared by every radio, and a group name via `<fieldset>` or `aria-labelledby` |
| `.rating` | shown: `role="img"` and an `aria-label` that says the score in words. Taken: `role="radiogroup"` with a name, one `name` shared by every radio, and an `aria-label` on each radio that says its score |
| `.form-code` | a group name (`role="group"` and `aria-labelledby`), an `aria-label` per box ("Digit 2 of 6"), `autocomplete="one-time-code"` on the first box, and the `name` on the `data-code-value` input alone |
| `.input-reveal` | `aria-label="Show password"`, unchanged when pressed, and `aria-pressed="false"` to start; `aria-controls` naming the field |
| `.avatar` | the person's name in text, an `alt`, or a `.visually-hidden` span. Initials are not a name. |
| `.chip-dismiss` | `aria-label="Remove <the value>"`. `×` alone is announced as "times". |
| `.pagination` | `<nav aria-label="Pagination">`, `aria-current="page"`, and a real `href` per page |
| `.breadcrumb` | `aria-current="page"` on the last item, which stays a `<span>` |
| `.table` sorting | `aria-sort` on the `<th>` — the arrow is drawn from it, so they cannot disagree |
| `.table` selection | `aria-selected` on the `<tr>` |
| `.table[role="grid"]` | an `aria-label`; `aria-multiselectable="true"` only where a range of cells may be selected; an `aria-label` on every `.cell-editor` naming the column and the row; `aria-invalid="true"` on a rejected cell **and** its editor; and the meaning of `data-dirty` and of a rejection in words, in the cell — the corner mark is colour and shape only. The script writes the roving `tabindex`, the cells' `aria-selected` and `data-editing`. Use a grid only where the reader works in the cells: a screen reader stops reading it as a table. |
| `.table[role="treegrid"]` | everything a grid needs; `aria-level` on every body row and `aria-expanded` on every branch, rendered by the app; a `.tree-toggle` in the tree column of every row, a leaf's included; `aria-posinset` and `aria-setsize` on a row whose siblings are not all rendered. The script writes `aria-expanded` as the reader opens and closes, `aria-busy` on a `data-tree-lazy` row, and `hidden` on the rows under a closed branch. |
| `.table` with `<Virtualize>` | `aria-rowindex` on every row — the header is row 1 — and `aria-rowcount` on the table, so a screen reader announces a row's place in the whole table, not in the rows rendered. |
| `.stat-delta` | an arrow icon. The colour is not the direction. |
| `.skip-link` | `href="#main"`, and `tabindex="-1"` on `<main id="main">` so the jump moves focus rather than only scrolling. Under `<base href="/">` the jump needs `Sedna.UI.js` — see [getting started](getting-started.md#base-href) |
| any icon-only button | `aria-label`, plus `data-tip` when the purpose is not obvious — and `.btn--icon`, which squares it to the shared control height instead of leaving a wide box around one glyph |
| `data-drag-zone` | `tabindex="0"` on a whole item, or a real `<button>` as its `data-drag-handle`, with an `aria-label` naming what it moves; a `[data-drag-live]` region carrying the sentences a keyboard drag is announced in; and `aria-disabled="true"` on an item that cannot move. |
| `.graph` | an `aria-label` naming what the graph shows; the `.graph-data` list written as sentences a screen reader reads out ("Orders API reads the orders database"), with `data-label` for the short word the canvas writes; a `[data-graph-live]` polite region carrying the sentences a keyboard move is announced in; `aria-label` and `data-tip` on every icon-only tool; and colour never the only carrier — a kind gets a shape, a link a line style and a label. `aria-roledescription="graph"` is yours to add in your own language. See **The graph** below. |
| `.col-resize` | `role="separator"`, `aria-orientation="vertical"`, an `aria-label` naming the column, the starting `aria-valuenow`, and `tabindex="0"` — a keyboard grid takes it out of the tab order and Alt+Shift with ← or → on the header cell resizes instead. A reordered header row needs a `[data-drag-live]` region and a header cell the keyboard reaches: `tabindex="0"`, or a keyboard grid's own tab stop. |
| `.dropzone` | a real `<input type="file">` inside it. Drag and drop alone is unreachable by keyboard; `data-dropzone` adds the drag handling on top of the input, never instead of it. |

## The graph

A canvas is a picture to assistive technology, so the graph carries its content twice and its
interaction once more:

- **The records are text.** `.graph-data` is the graph's list, visually hidden and read in order; with
  scripting off it is what the page shows. A side panel (`data-graph-detail`) lists the selected
  record's neighbours as buttons, which is the graph's structure in a form a keyboard and a screen
  reader both reach.
- **The graph is one tab stop.** Inside it the arrow keys move a ring to the nearest record in that
  direction, Page Down and Page Up walk the links, Home goes to the root, Space selects, Enter opens,
  the context-menu key opens the app's menu, and + − 0 zoom and fit. It gets `tabindex="0"` and
  `role="application"` unless the markup has its own — `application`, because a screen reader in browse
  mode would otherwise take the arrow keys for itself.
- **Every move is said in the app's words**, through the app's `[data-graph-live]` region and its
  `data-graph-announce` template. The library writes no word of it.
- **The ring is drawn in the focus ring's colour, inside the frame**, so a card that clips its content
  does not clip it; in forced colours it is an outline, like every other ring.
- **The canvas follows the theme and forced colours.** Every colour is a token read through the browser,
  repainted when the variant, the colour-vision setting, the contrast setting or forced colours change.
- **Motion is decoration.** Layout transitions and view animations are skipped under
  `prefers-reduced-motion`, and the result is the same drawing.

## Deliberate omissions

**No `role="menu"`.** The script moves focus between a `.menu`'s items with the arrow keys, Home and
End, jumps to an item by its first letters and opens a submenu with the arrow towards it — but the
items stay ordinary links and buttons, each its own Tab stop, with no roving tabindex, and a menu may
hold a filter field. `role="menu"` promises a single tab stop and children that are all `menuitem`s,
and a screen reader stops announcing a link as a link inside it. The disclosure pattern —
`aria-expanded` on the trigger, no role on the panel — is correct and complete.

**The palette is a combobox over a listbox.** The script writes `role="combobox"` and
`aria-activedescendant` on the input and `role="listbox"` on the list, so focus stays in the input while
the arrows move the active option. Do not add these roles in the markup; the script owns them.

## The tree

`.tree` is a full ARIA tree, `role="tree"` with `role="treeitem"` items and `role="group"` children,
and the script implements the whole keyboard contract: one tab stop with a roving `tabindex`, the arrow
keys mirrored in a right-to-left page, Home and End, `*`, typeahead, Space and Enter, and Shift+arrows
and Ctrl+A in a multiselectable tree. Every state is the ARIA attribute a screen reader announces —
`aria-expanded`, `aria-selected`, `aria-checked` with `mixed`, `aria-busy`, `aria-disabled` — and the
stylesheet paints from those attributes alone. An item's level comes from the nesting, so no
`aria-level` is written. A row that navigates is a link with `tabindex="-1"`, so the tree stays one tab
stop.

In a tree select the tree is the combobox's popup: `aria-haspopup="tree"` on the input, and
`aria-activedescendant` naming the item the keyboard is on, since focus stays in the input.

## Where the platform does the work

Four things are built on native elements specifically so the behaviour is not hand-rolled:

| Built on | What comes free |
|---|---|
| `<dialog>.showModal()` — `.modal`, `.drawer` and `.sheet` via `ShowModalAsync`, `.palette` | top layer, focus trap, Escape, inert content behind |
| `<details>`/`<summary>` — `.accordion`, `.nav-group` | open state, keyboard operation, announcement; `name` makes an accordion exclusive with no JS |
| `<input type="radio">` — `.segmented`, `.rating` | arrow keys, group semantics, chosen state |
| `<label>` wrapping the input — `.form-check`, `.switch`, `.form-file` | the whole label is the target, and Enter works |

A hand-rolled focus trap is a lot of code that is usually subtly wrong. If you need a modal surface,
reach for `<dialog>` before reaching for `.modal-backdrop`.

## Known gaps

- **A disabled checkbox and radio are not dimmed.** Adding it would change how every released app looks
  with no app edit, which the release rules make Major. Queued for the next major.
- **No automated audit yet.** `axe-core` via Playwright is planned; today the browser tests assert
  computed styles and console cleanliness, not WCAG rules.
- **Token contrast is verified in parts, not wholesale.** Measured on every build: the near-floor
  pairs `docs/BRANDING.md` §7.1 names, the muted role on every elevation level, and every rule that
  paints in a brand colour — the last against each surface it can sit on, in both variants, for the
  built-in themes and a generated one. The rest of the palette has not been audited pair by pair, and
  a full audit across all four theme combinations is still planned.
- **`.table--stack` throws away column alignment** by design, so comparing one value across rows stops
  being possible below 640px. It is opt-in for that reason.

## Browser floor

**The current release of Chromium (Chrome, Edge) and of Safari on iOS and iPadOS.** Every browser test
runs in both engines.

As versions, the floor is what the tests run on, and moves with each release that updates them:

| Engine | Tested on | Which means |
|---|---|---|
| Chromium | 156 | Chrome and Edge 156 |
| WebKit | 27.2 | Safari 27.2, iOS and iPadOS 27.2 |

An older version is not tested and not supported. It is not refused either, and much of the library works
there; what fails first is what arrived last in Safari — anchor positioning with `anchor-scope`, container
style queries and `closedby`. A device that cannot update to the version above is the
one to check by hand.

Consequences visible in the CSS:

- **CSS anchor positioning is used, and load-bearing.** The collapsed rail's hover flyout, `.popover`,
  `.menu` and `.form-combo-panel` depend on it, and both engines ship it. In an engine without it they
  do not degrade — the flyout lands at its static position and the popover centres itself in the
  viewport.
- **The select is a customizable select where the engine has one.** `.form-select` sets
  `appearance: base-select`, so the drop-down is a panel in the page rather than a platform widget, and
  an `<option>` may hold markup. In an engine without it the control is the platform's own select with
  a painted caret — the two
  paths are exclusive, behind `@supports`, and neither is a script. The accessible name, the
  keyboard and typeahead are the browser's in both, because it is the same `<select>` either way.
- **Scroll-driven animations only behind `@supports (animation-timeline: scroll())`** — they fail
  *incorrectly*: a browser that drops `animation-timeline` leaves the rest of the `animation` shorthand
  running, so the animation plays on a timer instead of not at all. Behind the query an engine without
  them gets the static form: `.page-head--collapse` is an ordinary head and `.back-to-top` is always
  there. Under reduced motion the head does not fold and the button comes in without a fade.

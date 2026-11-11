# Sedna.UI design guide

How to compose a page from the library: which part answers which need, where it sits, how far it
stands from its neighbours, which colour it takes and how loudly it speaks. Every rule here is what
the stylesheet and the catalogue already do; where another document states a rule, this guide links
to it rather than saying it a second way.

## Start from the goal

Decide what the reader has to be able to do before choosing a class. The guide's goals name the
part that answers each one and the parts it is combined with — search the catalogue or the MCP
server for the goal in plain words ("let the user upload a file", "show progress of a long job"), or
browse them on [the design guide page](https://www.sedna-ui.com/design-guide#goals).

- Pick the part whose **use it for** matches the reader's job, not the one whose picture is closest.
  A list of orders to scan is a table; the same orders read one at a time on a phone are a list.
- Read its **not for** before writing markup. Each line names the part to use instead.
- Build what **combine with** names around it, in the place it says. A table is a toolbar above a
  card, the table in the card, and a pager after it — not a table alone.
- Write every word from [Words](https://www.sedna-ui.com/design-guide#words), and pass the page to
  `check_markup` before handing it over.

## Page composition

A page is the frame, then one `.page`, then a `.page-head`, then sections. The frame — layout,
sidebar, topbar, bottom bar — is markup copied from the
[Shell](https://www.sedna-ui.com/frame) and [Sidebar and nav](https://www.sedna-ui.com/nav) pages;
whole pages to start from are on [Page recipes](https://www.sedna-ui.com/page-recipes).

- **The page head comes first.** `.page-head` holds the `<h1>`, an optional one-line purpose and the
  actions that apply to the whole page. A record three levels deep puts a `.breadcrumb` above it.
- **A section is a card when it is an object in its own right** — something with its own title, its
  own actions, or its own state: a table of orders, a form, a chart with its legend. Its title row is
  `.card-head`, its content `.card-body`, its decision `.card-foot`.
- **A section is plain when it is merely the next thing on the page.** A heading and its content
  sit straight on `.page`; a `.divider` separates two related blocks; whitespace separates blocks that
  are only sequential.
- **Sub-sections of one card are `.card-section`**, never a card inside a card for layout. A card in a
  card is for an object inside an object, and steps the surface up a level each time.
- **Two regions side by side are a `.sedna-split`**: filters beside results, a list beside its record,
  a form beside its preview. The aside is the child written first or last; the two stack on their own
  when the main column would be too narrow, so no breakpoint is chosen.
- **A set of peers is a `.sedna-grid`**: stat tiles, dashboard cards, fields side by side. It
  decides the column count from its own width.
- **One job per page.** A second, unrelated job is a second page or a tab, never a second page head.

## Spacing

Spacing is the library's, at every level: the page, the card, the form, the row. An app sets none of
it with a margin. The values below are the default density; [Density](https://www.sedna-ui.com/design-guide#density)
moves them together.

| Where | What spaces it | Default |
|---|---|---|
| Between page sections | `.page`'s own rhythm, `--page-gap` (from `--page-rhythm`) above every child after the first | 16px |
| Around the page | `--page-pad` | 24px |
| Above an `<h2>` on the page | the heading's own margin | 24px |
| Inside a card | `--card-pad-block` / `--card-pad-inline`, shared by head, body and foot | 12px / 16px |
| Between fields | `.form-section-body`, and the gap of the `.sedna-grid` holding them | 16px |
| Label, control and hint | `.form-field` | 6px |
| A list row | `--list-pad-block` / `--list-pad-inline` | 10px / 12px |
| A table cell | `--cell-pad-y` / `--cell-pad-x` | 8px / 12px |
| A row of controls | `.sedna-row`, `.toolbar-filters`, `.form-actions`, `.card-foot` | 8px |

- **The page rhythm is automatic.** A card after a card, or a toolbar after the list it controls,
  needs no class and no `style="margin-top"`. `.sedna-mt-0` flushes one child; `--page-gap: 0` on
  `.page` turns the rhythm off for a page that keeps its own. The rule is stated for apps in
  [the consuming-app rules](../CLAUDE.consuming-app.md).
- **Inside a card body the rhythm is the content's.** A `.kv` stack is tight on purpose and a form is
  not. Lay several blocks out with `.sedna-col` and a gap step, never a margin per child.
- **The `sedna-gap-*` steps are their own scale**, not the `--space-*` tokens: `.sedna-gap-1` is 8px,
  `-2` 12px, `-3` 20px, `-4` 24px, `-5` 32px and `-6` 40px — `.sedna-gap-6` is `--space-11`, not
  `--space-6`. Steps 1 to 3 space controls and items; 4 to 6 space blocks of a page. `.sedna-gap-0`
  joins items that form one control.
- **Use a utility where no component spaces it**, and the component where one does. A toolbar, a form
  section, a card foot and a list already space their children; `.sedna-row`, `.sedna-col` and the gap
  steps are for arrangements of the app's own.
- **Never put a margin, a padding or a gap on a library class** in the app's stylesheet. Where a class
  has a value an app reasonably changes, it is a custom property — `--page-gap`, `--grid-min`,
  `--split-aside`, `--toolbar-input-width` — set inline on the element. A number with no such knob is a
  request for the library.

## Colour

Colour is a role first and a tone second. Every colour resolves through a token, so a theme, the
colour-blind palette, high contrast and forced colours reach every part; an app writes no colour
values of its own. Theming and rebranding are in [BRANDING](../BRANDING.md).

| Family | Means | Use it on |
|---|---|---|
| `brand` | The app itself, and the neutral primary action | the one filled button of a region that is neither sending nor destroying |
| `accent` | A link or a decorative icon | never on an action — a link is told from a button by its colour |
| `go` | Sends something outward: approve, apply, send | the primary action that does that, a state that succeeded |
| `warn` | Changes who is in control, or will fail soon | taking over, a soft limit crossed, something degraded |
| `danger` | Destructive, or failed | delete, a hard limit crossed, something broken now |
| `info` | Informational | a notice that changes nothing |
| `secret` | A sensitive value, or acting as someone else | a token, a password, impersonation |
| `cyan`, `orange`, `teal` | Nothing — categories with no ranking | a type, a queue, a source |

- **A semantic tone is a claim.** A red badge says danger whether or not the data does, so a category
  never takes a semantic family; it takes a categorical hue, and in a chart a `.series-1` to
  `.series-6`. Reach for the categorical hues only after the semantic set does not fit.
- **Soft is the default, solid is the exception.** A tone's soft tint labels and marks; `--solid`
  fills, for the one thing in a region that must be seen first — its primary action, a state that
  stops work. `--ghost` is for a quiet action beside others.
- **A filled primary and a filled destructive never sit side by side.** The destructive action takes
  the soft or ghost variant, on the far side of the row; only a confirmation's own button is filled
  danger.
- **Never colour alone.** Every tone that means something carries a second channel: an icon, a shape,
  an arrow, a word. A delta's arrow carries the direction and its colour the judgement.
- **A surface's colour comes from where it sits.** A card is `--surface-raised-1`, a card in a card
  the next level; anything opaque inside reads `--surface-here`. Do not paint a panel's background by
  hand.

## Actions

Each region has at most one primary action: the filled button, in its semantic colour, that does
what the region is for. Everything else in the region is soft or ghost. The variants are on
[Buttons](https://www.sedna-ui.com/button).

| Where | What goes there | Order |
|---|---|---|
| `.page-head-actions` | What applies to the whole page: create, export, refresh | end-aligned, the primary last |
| `.toolbar` | What narrows or acts on the list below: filters leading, the count and list actions trailing | filters first, actions at the end |
| `.card-foot` | What decides the card's content: save, approve | end-aligned, the primary last; `--split` puts a destructive or tangential action on the far side |
| `.form-actions` | What closes a form: save and cancel | end-aligned, the primary last; `--split` as above |
| `.modal-footer` | The dialog's answer | end-aligned, the confirming button last |
| A `--start` foot or form row | A work card read left to right | the primary first, its secondaries after it |

- **The primary sits where it is looked for**: at the end of an end-aligned row, first in a
  start-aligned one. Never two filled buttons in one row.
- **A button is a verb.** A confirmation repeats the action — **Delete order** and **Cancel**, never
  **Yes** and **No**.
- **An action in flight is `aria-busy="true"`** on the button, not `disabled`, so it keeps its width,
  its focus and its announcement.
- **A row of actions that outgrows its space folds** into one menu with `.actions-more`, keeping the
  primary beside it with `.actions-keep`; a toolbar and a page head both do this by their own width.
- **An action on one row of a list or table** sits at the row's end — a `.table--pin-end` column keeps
  it in reach on a wide table — and a set of them is a `.menu` behind one button.
- **A navigation is a link** (`<a href>`), an action is a `<button>`. A page of results, a record, a
  tab with an address — all links.

## Feedback

Pick the channel by how long the message stays true and whether the reader must act on it.

| The message | Channel |
|---|---|
| About one field, while it is wrong | `.form-error` under the field, with `aria-invalid` on the control |
| About one card's own data — stale, partial | `.card-warning` at the card's foot |
| A condition of the page that stays true while it is open — read-only, degraded, an empty filter | `.alert`, in place, above what it is about |
| About the whole app — an outage, a maintenance window, the phase it is in | `.page-banner` under the topbar |
| The connection to the server, the session | the status bar the frame already has |
| A receipt for something the reader just did | a toast, which goes on its own |
| Something to come back to later | the notification inbox, which a toast may announce |
| A region still loading, or with nothing in it | `.skeleton` for the first load, `.empty-state` for none, `aria-busy` on a refresh |
| A long job | a `.progress` with its figure, or a spinner where no figure exists |

- **Nothing the reader needs lives only in a toast.** A toast can be missed by a screen reader and is
  never seen by someone magnifying another part of the screen. An error the reader must fix stays on
  the page until it is fixed.
- **Feedback sits beside what it is about.** A field's error under the field, a card's caveat in the
  card, a list's empty state in the list's place — with its toolbar and its count still there.
- **The first load is a skeleton of the content's own shape**; a refresh of content already shown
  keeps the content and marks it busy.
- **A destructive action asks first**, in a modal that names what will be lost and repeats the
  action on its button; the receipt afterwards is a toast.

## Density

Density is set once, on `<html>`, as `data-density="compact"` or `data-density="touch"`, and moves
the table cells, the card padding, the list rows, the page rhythm and the control heights together.
It never moves the type, which is the reader's own zoom.

- **Choose density for the reader, not the page.** A dense operations console is compact; a tablet on
  a warehouse floor is touch. Offer it as a setting rather than fixing it per page.
- **Write nothing density-specific.** A part of the app's own that should tighten with the library
  reads the same tokens — `--card-pad-inline`, `--cell-pad-x` — and an app rule that sets padding on a
  library class stops density reaching it.
- **For more on one screen**, prefer what a part offers — `.btn--sm`, a `--sm` meter or tracker, a
  `.stat-row` — before shrinking the page.

## Motion

Motion says that something changed or is happening; it never decorates. Every animation in the
library stops for a reader who asked for reduced motion, and print shows the end state.

- **Use what a part already does.** A row that arrives (`.table--stagger`), a figure that counts up
  once (`.stat-value--roll`), a busy button, a workflow's moving light. Do not add motion of the app's
  own to a library class.
- **One moving thing at a time.** A count-up is for the one figure a page leads with, not every tile.
- **Let the reader stop it.** A display that keeps moving — a live workflow, a log tail — offers the
  control to stop it, as `.workflow--flow-off` does.
- **Speed is a token.** The `--motion-*` tokens set every transition; setting them to `0s` turns them
  all off without touching a rule. Their reference is §7.4 of [BRANDING](../BRANDING.md).

## Words

The words on the screen are the app's, and the rules for them are written once, as **Writing the
words** on [Concepts](https://www.sedna-ui.com/concepts#words) and in §9 of [BRANDING](../BRANDING.md).
In short: buttons are verbs, saving is **Save**, an empty state's headline says what to do, an error
says what went wrong and how to fix it, sentence case everywhere, no "please", numbers as digits, and
dates that cannot be read two ways.

- **Every word on a tier 3 surface's frame is the app's too** — the legend, the tooltip, the
  announcement, the empty state.
- **A status says its state in words** beside its colour: "Failed", not a red dot.

## Accessibility

What the library guarantees, and what an app's markup has to supply for each class, is in
[accessibility](../accessibility.md). Three habits cover most of it:

- **States are ARIA attributes, not classes**: `aria-current`, `aria-selected`, `aria-sort`,
  `aria-expanded`, `aria-invalid`, `aria-busy`. The look is drawn from them, so it cannot disagree
  with what is announced.
- **Every icon-only control has a name** — an `aria-label`, and a `data-tip` where its purpose is not
  obvious.
- **A picture has its words**: a chart its table, a graph its list, a bar its `role="img"` and a label
  with the numbers.

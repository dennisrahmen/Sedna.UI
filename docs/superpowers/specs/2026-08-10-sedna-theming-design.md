# Brand identity, two-tier tokens, and a branding service

**Date:** 2026-08-10
**Status:** draft — awaiting approval
**Branch:** `brand/sedna-theming` → PR #11
**Release it ships in:** `0.3.0`

Three things, in one branch: put the Sedna brand into the repository; give the
library a real token architecture; and add a branding service so a brand is data
rather than a hand-written stylesheet. Plus the catalogue page that explains it.

Measurements were taken against `main` at `c331e59` on 2026-08-10 and are snapshots.

---

## 1. Why this exists

`BRANDING.md` is 30 000 characters, most of it tables that hand-assign a value to
every colour token in the library. That length is a symptom, and the cause is
measurable:

| | |
|---|---|
| Semantic tokens declared | 159 |
| Palette / ramp tokens | **0** |
| Distinct hex literals | 88 |
| Distinct `rgba()` literals | 72 |

160 distinct colour literals against 159 tokens is effectively one literal per
token. There is no shared source, so **re-branding means re-deriving every value by
hand** — which is exactly what the manual had to do.

Dennis's own reading, which sets the direction:

> it is misleading, take the colors from there but not the assignments as static.
> Since this branding guide seems to limit itself to the tokens that are available
> for overriding.

Correct. The manual was written *inside* the constraint this spec removes. Its
§3.2–3.3 ramps are the input; its §5–6 role assignments are an artefact and are
**not** carried over verbatim.

## 2. Decisions

| Decision | Choice | Decided by |
|---|---|---|
| Token architecture | two tiers: palette → semantic | Dennis |
| Brand delivery | a branding service, brand as data | Dennis |
| CSS emission | **server-rendered into `<head>`, never JS-injected** | Claude (see §4.1) |
| API shape | a Razor component, `<SednaBrandStyle />` | Dennis relaxed the no-components rule; Claude chose (see §4.2) |
| Attribute model | `data-theme` = **which theme**, `data-variant` = `dark` \| `light` | Dennis ("maybe we need theme and variant option") |
| Every theme has both variants | **required**, enforced | Dennis |
| Breaking changes | permitted — pre-1.0 | Dennis |
| Library colours | now change (unlike PR #10) | Dennis |
| Demo themes | `forest` (green) and `cobalt` (blue) | Claude, delegated |
| Ramp generation | Sedna's ramps taken verbatim; demo themes generated from anchors; a test proves the generator reproduces Sedna's | Claude, delegated |
| The vector | `sednaui-mark-*.svg` — supplied 2026-08-10 | resolved |

Dennis chose the runtime-CSS service with the flash objection stated in front of
him. That objection is answered by §4.1 rather than by narrowing the request.

## 3. Tier 1 — the palette

New, from `BRANDING.md` §3.2–3.3. Twelve ramps, eleven steps each, values used
verbatim because they were generated in OKLCH with a shared lightness curve and
measured contrast.

```
--slate-50 … --slate-950      the neutral spine
--coral-50 … --coral-950      Sedna Red
--orbit-50 … --orbit-950      Orbit Blue
--navy-50  … --navy-950       Sedna Navy — mark and illustration only
--green-*  --amber-*  --crimson-*  --violet-*
--cyan-*   --orange-*  --teal-*    --indigo-*
```

Palette tokens hold **literals only** and never reference another token. They are
declared once, at `:root`, and are **not** remapped by mode, CVD, contrast or
density — a ramp step is a colour, not a decision.

**Naming.** Ramps are named for the hue (`--coral-*`), not the role (`--brand-*`),
because the whole point is that a role can move to a different ramp. `--navy-*` is
documented as non-interface per §3.2 of the manual.

## 4. Tier 2 — the semantic tokens

The existing 159 keep their names. Only their **values** change, from a literal to
a palette reference:

```css
--brand:        var(--coral-600);   /* #D73F1A — the one that carries white text */
--brand-soft:   var(--coral-500);   /* #FF6B4A — Sedna Red at full strength     */
--danger-solid: var(--crimson-700);
--bg:           var(--slate-900);
```

Because names do not move, an app that overrides `--brand` today keeps working.

**The three constraints from the manual are preserved as design rules, not as
copied values:**

- **§4.1 — Sedna Red cannot be a button.** White on `#FF6B4A` is 2.82:1, and
  `--on-solid` is shared by every filled control. `--brand` therefore takes the
  darkest-chroma point on the coral hue that still carries white text at AA. In
  ramp terms that is `--coral-600`; `--brand-soft` keeps `--coral-500`. The rule to
  encode is *"the brand you click is one ramp step darker than the brand you see"*.
- **§4.3 — danger cannot stay red.** `#DC2626` sits 7° from the coral in OKLCH.
  Danger moves to the crimson ramp. This is forced, not cosmetic.
- **§4.2 — accent is Orbit Blue**, so links never read as primary actions.

### 4.1 Why the emission is server-side

`CLAUDE.md` already forbids the JS route, for the parts loader:

> Do not add a runtime loader for the parts — JS-injected CSS leaves content
> unstyled until scripts run.

A brand injected after first paint flashes the wrong colours on every load, which
is the same defect. The service therefore produces **CSS text that the host page
writes into the document**, so the brand is present before the first byte of body
renders.

### 4.2 It is a component, and why that is allowed here

Dennis: *"Components are out but not forbidden, if they make sense here use them.
Especially for services things."*

So the API is a component:

```razor
<head>
    <SednaBrandStyle />
</head>
```

**Why a component rather than a static `ToCss` method.** The alternative makes the
host page do the DI plumbing:

```razor
@inject IOptions<SednaUiOptions> Options
<style>@SednaUiBrand.ToCss(Options.Value)</style>
```

Two lines and an injection, in the one file that is a **test-enforced copy-paste
artifact**: `docs/getting-started.md`'s block *is* the catalogue's host page, and
`HostPageTests` asserts they agree by executing the documented block on every CI
run. Every line added there is a line every consuming app copies forever. The
component reads its brand from DI itself and costs the host page one self-explanatory
tag.

`ToCss(SednaBrand)` still exists as a public static method — the component calls it,
and it is what makes per-request multi-tenant emission possible for an app that wants
it (§14). The component is the ergonomic default, not the only path.

**Why this does not reopen what was deleted.** `CLAUDE.md`'s prohibition is aimed at
the *frame* and at *content*: "Do not add a `<DataTable>`, a `<Card>` or an
`<AppShell>`", and "Wrapping the frame in components. They existed on `main` between
`0.1.0` and this release and were removed before shipping; do not bring them back."
A brand-style emitter is neither frame nor content — it is infrastructure that
produces a `<style>` element. The two-tier rule ("both tiers are CSS classes") is
untouched.

**Two consequences to handle, both easy to miss.**

1. **`CLAUDE.md` said "There are no components" unconditionally**, and it is
   auto-loaded into every session here — left as-is, a future agent would read that as
   licence to delete `SednaBrandStyle`. **Already fixed**, with Dennis's own reasoning:
   the objection is that a component hides CSS, HTML and JS so an agent cannot read or
   edit what it cannot see. That applies only to components containing markup a reader
   needs, so a service or helper component that emits no UI is allowed. The stated test:
   if removing it would leave a reader unable to see the markup their page renders it is
   forbidden; if it would only make them write plumbing by hand, it is fine.
2. **The library has no `.razor` files today**, so adding one re-activates the Razor
   SDK's globs. `build/verify-package.sh` has a scoped-CSS guard that currently
   reports *"no scoped CSS in use, none expected"*; it goes live the moment a
   `.razor.css` appears. Do not add one without also checking the packed
   `.bundle.scp.css` assertion.

## 5. The branding service

### 5.1 A brand is data

```csharp
var sedna = new SednaBrand("sedna")
{
    Coral  = "#FF6B4A",
    Orbit  = "#59C3FF",
    Navy   = "#17346E",
    Slate  = new SednaRamp("#F8FAFC", "#94A3B8", "#1E293B", "#0F172A"),
    // support hues fall back to the shipped ramps when omitted
};
```

A brand names its **anchors**, not 160 values. Ramps are generated from anchors, or
supplied whole when a designer has already done the work — `BRANDING.md`'s ramps go
in as supplied, unrounded, because they carry measured contrast.

### 5.2 Registration

```csharp
builder.Services.AddSednaUi(o =>
{
    o.Brands  = [sedna, forest, ember];
    o.Default = "sedna";
});
```

### 5.3 What `ToCss` emits

- `:root { … }` — the palette of the default brand
- `[data-brand="forest"] { … }` — one block per additional brand, palette only

Additional brands are emitted **alongside** the default so switching is a client-side
attribute flip with no round trip and no flash. Semantic tokens are not emitted:
they ship in the stylesheet and reference the palette, so a brand that changes only
anchors needs no semantic block at all.

### 5.4 Theme and variant

Two orthogonal attributes, matching how Dennis described it:

```html
<html data-theme="sedna"  data-variant="dark">
<html data-theme="sedna"  data-variant="light">
<html data-theme="forest" data-variant="light">
```

- **`data-theme`** — which theme. Any registered name.
- **`data-variant`** — `dark` | `light`. What the user toggles.

**Every theme must define both variants.** This is a requirement, not a
convention — a user who switches to light must never land on a theme that has no
light variant. It is enforced twice: `SednaTheme` makes both non-optional in C#, so
a one-variant theme cannot be constructed; and a guard asserts the emitted CSS
contains a block for each registered theme × variant pair.

This **breaks `data-theme`**, which currently takes `dark` | `light`. Permitted
pre-1.0, and it is the honest naming: today's `data-theme="light"` conflates "which
design" with "which variant of it", which is exactly why there is nowhere to put a
second theme.

**Migration.** `sedna.theme` in `localStorage` currently holds `dark`/`light`; it
now holds a theme name, and the new `sedna.variant` holds the variant. Old values
are **not** read and translated — `CLAUDE.md` forbids compatibility shims, and the
prefix rename set the precedent. A returning user lands on the default theme in
their device's variant once. `docs/migrating-to-sedna-ui.md` gains a section, since
it already documents the storage loss from the prefix change.

Rejected: native `light-dark()`. The Chromium floor supports it and it would
collapse the light block into the same declarations, but it ties the variant to
`color-scheme`, which cannot express "follow the device" distinctly from an explicit
choice — and `[data-cvd]` / `[data-contrast]` would still need attribute blocks.
Two competing models is worse than one.

### 5.5 The demo themes, and why these two

Two themes beyond Sedna, chosen to prove something rather than to decorate:

- **`forest`** — a green brand. Green collides with the `go` family, so the theme
  must move `go`. That is the same problem `BRANDING.md` §4.3 solves for coral
  against danger, and demonstrating it makes the constraint visible instead of
  buried in a manual.
- **`cobalt`** — a blue brand, at roughly the pre-rebrand `#2563EB`. It proves the
  architecture can **reproduce the old appearance from data**, which is both a
  strong demonstration and directly useful to anyone who preferred it.

Every brand hue collides with some semantic family; that is inherent, and these two
surface it rather than hide it.

### 5.5 The gaps this closes

Measured against `main`:

| Gap | Today | After |
|---|---|---|
| `system` as a user-selectable mode | host-page default only — `10-settings.js` collapses the stored value to `light ? light : dark` | a real third mode |
| Live OS switching | nothing subscribes to `matchMedia` change | subscribed; a stored explicit choice still wins |
| Named themes | `data-theme` is binary | `data-brand`, any number |
| C# knows the themes | `SednaUiSettings.Theme` is a string defaulting to `"dark"` | mode + brand, with the registered list |

`Sedna.UI.boot.js` already resolves `data-theme-default="system"` against
`prefers-color-scheme` **before first paint**, and deliberately does so in JS rather
than a `@media` block. That design is kept and extended to `data-brand`.

## 6. The cost, stated honestly

The variant blocks are where this gets expensive. Measured token counts per block:

| Block | Tokens remapped |
|---|---|
| `:root[data-theme="light"]` | 75 |
| `:root[data-contrast="more"]` | 58 |
| `:root[data-theme="light"][data-contrast="more"]` | 59 |
| `:root[data-cvd="1"]` | 7 |
| `:root[data-theme="light"][data-cvd="1"]` | 3 |
| `:root[data-density="compact"]` | 2 |

Every one of those must be re-expressed through the palette too, or the light and
high-contrast themes silently keep the old greys while the default theme goes navy.
**This is the bulk of the work, not the palette itself.**

`BRANDING.md` §9.2 adds a real constraint here: under deuteranopia and protanopia
coral and crimson converge, so the CVD block must push danger colder *and* darker.
That is a new remap the current CVD block does not have.

## 7. Guards to add

The existing model — variant blocks may only remap tokens, never style a selector —
is what makes CSS load order irrelevant. Two new guards keep the tiers honest:

1. **A semantic token's value may only be a palette `var()`, a `color-mix()` of one,
   or a non-colour literal.** A raw hex in tier 2 is the drift this whole change
   exists to prevent.
2. **A palette token's value must be a literal**, never a `var()`. Otherwise a ramp
   can point at a role and the tiers invert.

Plus: every `var(--x)` used is declared (exists); no `!important` (exists); the
emitted CSS parses and declares only custom properties (new, over `ToCss` output).

## 8. Brand identity in the repository

The artwork Dennis supplied on 2026-08-04, now complete:

| Supplied | Note |
|---|---|
| `sednaui-icon-{16,32,48,64,96,128,192,256,512,1024}.png` | a genuine 1024 — the gap that blocked PR #10 |
| `sednaui-icon-master.png` | 370×370 |
| `sednaui-lockup-for-{dark,light}-bg.png` | 1462×576 |
| `sednaui-lockup-tile-for-dark-bg.png` | |
| `sednaui-wordmark-{navy,white}.png` | 623×115 — the white wordmark, also a PR #10 blocker |

**No vector was supplied.** `BrandAssetTests` requires `sedna-ui-icon.svg`, and
`assets/brand/README.md` recommends the SVG as *the* scalable icon. Open — see §10.

The three quiet wirings move in the same commit as the files: the csproj
`PackageIcon` `None Include`, `README.md`'s hero URL, and `BrandAssetTests`. The
catalogue's `favicon.ico` and `logo.png` are byte-identical copies and are re-copied.

`BRANDING.md` goes to `docs/`. Note the consequence: the catalogue csproj embeds
`..\..\docs\*.md` at a single level, so it is served by the **public,
unauthenticated** MCP server as a `docs` resource. It must be written for that
audience, and its `sedna-brand.css` reference in §10.1 must resolve to something
that exists or be removed.

## 9. The catalogue branding page

`/branding`, registered in `CataloguePages`. Contents: the mark and the three
approved lockups; clear space and minimum sizes; the misuse list; the palette as
swatches read from the live stylesheet rather than typed; the three §4 decisions
stated as rules; and a **live mode × brand switcher** so the two demo brands are
demonstrated rather than described.

Every example is a file under `Examples/Branding/`, rendered *and* printed from the
same embedded bytes, per the catalogue's own rule.

## 10. Ramp generation

`BRANDING.md`'s ramps are taken **verbatim** for Sedna. They were generated in OKLCH
with measured contrast, and §9.1 names six pairs sitting within 0.3 of the AA floor —
recomputing them risks moving one below it.

The demo themes are **generated from their anchors**, on the same lightness curve and
chroma bell. That is two code paths, and the risk is that they diverge: generated
ramps that do not match the shape of the supplied ones make the themes look like
different systems rather than siblings.

**The risk becomes a test.** Generate Sedna's ramps *from Sedna's anchors* and compare
against the supplied values, asserting the perceptual difference stays under a stated
threshold. If the generator cannot reproduce the ramps a designer produced by the same
stated method, one of the two is wrong, and the test says so before a theme ships.

## 11. The vector, and the trap inside it

Resolved 2026-08-10: `sednaui-3c-assets/` supplies eleven SVGs. All artwork sits on a
120-unit grid and the SVGs are the stated source of truth. Three detail tiers, which
map exactly onto §2.2's minimum sizes:

| Tier | Stroke | Use | Satellite |
|---|---|---|---|
| `sednaui-mark-*` | 2.5 | 96px and above | yes |
| `sednaui-mark-compact-*` | 5.5 | 32–64px | yes, larger |
| `sednaui-mark-micro-*` | 8 | 16px | **dropped** |

The supplied PNG ladders already switch tier by size, so `icon/…-16.png` is the micro
form and `…-96.png` and up are the full form. `sedna-ui-icon.svg` is therefore
`sednaui-mark-navy.svg`, and `BrandAssetTests` keeps its requirement unchanged.

**The trap.** `sednaui-wordmark-*.svg` and `sednaui-lockup-*.svg` contain a `<text>`
element with `font-family="Outfit, sans-serif"`. On any machine without Outfit they
render the wordmark in a substitute face — which the asset README and `BRANDING.md`
§2.2 both forbid outright: *"Never rebuild the wordmark in another face."* Nothing
errors; the wordmark is simply wrong for almost every visitor.

So:

- **Mark and tile SVGs** — pure geometry, no text, no embedded raster. Safe for the
  favicon source, the catalogue logo, and anywhere scalable.
- **Wordmark and lockup SVGs** — design source only. Web-facing lockups use the
  **PNG** versions, where the type is already rasterised in Outfit.

**A guard for it**, because this is precisely the silent class this repository guards:
no SVG referenced by the README, the package or the catalogue may contain a `<text>`
element naming a non-system font.

## 12. Open decisions

1. **Which icon ladder becomes the package icon.** The set ships two: `icon/` is the
   navy mark on transparent, for light backgrounds; `icon-on-dark/` is the Ice White
   mark, for dark. A transparent mark is invisible on the wrong background, and
   nuget.org, GitHub and an IDE's package pane are not all the same shade.
   `sednaui-tile-for-dark-bg` carries its own Deep Space field and so reads on
   anything — which argues for the tile as the package icon and the bare mark for
   in-app use. Confirming rather than assuming, because the packed icon is permanent
   for a published version.

2. **Where the branding code lives.** `Branding/` under `src/Sedna.UI/` for
   `SednaTheme`, `SednaRamp` and `ToCss`; `Components/SednaBrandStyle.razor` for the
   component. First CSS-emitting C# in the library and the first `.razor` since
   components were removed — `CLAUDE.md` now records why both are allowed.


## 13. Sequencing

Four stages on one branch, merged as one PR, each green:

1. **Brand identity** — assets, the three wirings, `BRANDING.md`, catalogue copies.
   Independent of everything else; unblocks tagging.
2. **Tier 1 + tier 2** — the palette, the 159 re-expressions, all six variant
   blocks, the two new guards. The bulk of the work.
3. **The service** — `SednaBrand`, `SednaRamp`, `ToCss`, registration, the mode and
   brand runtime including `system` and live `matchMedia`.
4. **The catalogue page** — `/branding`, the examples, the switcher.

Stage 2 is where the visible colour change lands. Everything before it is
identity-only, which is what PR #10 deliberately stopped short of.

## 14. Not in scope

- Recolouring `catalogue.css`. It may only style `.cat-*` / `.ex-*` and defines no
  tokens; it follows the theme because every colour in it is already a library
  token.
- Per-request multi-tenant brand switching. `ToCss` makes it possible; wiring a
  tenant resolver is an application concern.
- Any change to spacing, radii, type scale, control heights, motion or shadow
  geometry. `BRANDING.md` §1 carries all of them over unchanged, and so does this.

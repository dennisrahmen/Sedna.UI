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
| Mode attribute | `data-theme` stays `dark` \| `light` | Claude, delegated |
| Brand attribute | new `data-brand` | Claude, delegated |
| Breaking changes | permitted — pre-1.0 | Dennis |
| Library colours | now change (unlike PR #10) | Dennis |

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
it (§12). The component is the ergonomic default, not the only path.

**Why this does not reopen what was deleted.** `CLAUDE.md`'s prohibition is aimed at
the *frame* and at *content*: "Do not add a `<DataTable>`, a `<Card>` or an
`<AppShell>`", and "Wrapping the frame in components. They existed on `main` between
`0.1.0` and this release and were removed before shipping; do not bring them back."
A brand-style emitter is neither frame nor content — it is infrastructure that
produces a `<style>` element. The two-tier rule ("both tiers are CSS classes") is
untouched.

**Two consequences to handle, both easy to miss.**

1. **`CLAUDE.md` currently states "There are no components" unconditionally**, and it
   is auto-loaded into every session in this repository. Left as-is, a future agent
   reads that as licence to delete `SednaBrandStyle`. It must be qualified in the same
   commit that adds the component.
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

### 5.4 Mode, and the attribute model

`data-theme` keeps `dark` | `light`. A new `data-brand` carries the brand name. They
are orthogonal: mode is a user toggle, brand is an identity. Collapsing them would
force every brand to redefine both modes — three brands × two modes is six blocks
instead of three anchor blocks plus the one shared light remap.

Rejected: native `light-dark()`. The Chromium floor supports it and it would
collapse the light block, but it ties mode to `color-scheme`, which cannot express
"system" distinctly from an explicit choice, and `[data-cvd]` / `[data-contrast]`
would still need attribute blocks. Two competing models is worse than one.

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

## 10. Open decisions

These are not implementation details and should be settled before the plan:

1. **The vector.** No SVG exists. Either author `sedna-ui-icon.svg` as a faithful
   vector reduction of the supplied mark, or drop the vectors and amend
   `BrandAssetTests` plus `assets/brand/README.md`. The 1024 raster means the
   ladder no longer *needs* a vector, so this is now a question about print and
   scalable use, not about the icon set.
2. **The two demo brands.** Names and anchor colours. They exist to prove the
   switcher works and should be visibly unlike Sedna — a cool green and a warm
   amber would read clearly against coral. Dennis's call.
3. **Ramp generation.** Accept `BRANDING.md`'s ramps verbatim for Sedna (measured
   contrast, do not recompute), and generate for the demo brands from anchors. That
   means two code paths — supplied ramps and generated ramps — and the generator
   must reproduce the supplied ones closely enough that the difference is not
   visible, or the two brands will not look like siblings.
4. **Where the branding code lives.** A new `Branding/` folder under
   `src/Sedna.UI/` for `SednaBrand`, `SednaRamp` and `ToCss`, and
   `Components/SednaBrandStyle.razor` for the component. It is the first C# in the
   library that emits CSS, and the first `.razor` since components were removed —
   both are genuine widenings of what the package does, and `CLAUDE.md` has to say
   so rather than leave a future session to infer it.

## 11. Sequencing

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

## 12. Not in scope

- Recolouring `catalogue.css`. It may only style `.cat-*` / `.ex-*` and defines no
  tokens; it follows the theme because every colour in it is already a library
  token.
- Per-request multi-tenant brand switching. `ToCss` makes it possible; wiring a
  tenant resolver is an application concern.
- Any change to spacing, radii, type scale, control heights, motion or shadow
  geometry. `BRANDING.md` §1 carries all of them over unchanged, and so does this.

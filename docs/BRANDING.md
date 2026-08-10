# Sedna.UI — brand and theming

Sedna.UI is a rebrand, not a redesign. Corner rounding, opacity, spacing, type scale, control
heights, motion durations and shadow geometry are carried over **unchanged**. What changes is hue:
the neutral ramp moves from warm grey to navy-tinted slate, and the brand moves from a generic blue
to Sedna Red.

This document is the brand *and* how the library implements it. Colour values here were generated in
OKLCH with a fixed hue and a shared lightness curve, and every contrast ratio quoted was measured
against the surface the token actually renders on.

---

## 1. Identity

### 1.1 Positioning

> A modern UI library for building beautiful digital experiences.

The identity draws on orbital motion and cosmic clarity: a deep navy field, one warm focal body, a
small cold satellite. That hierarchy — **dark field, hot focus, cool detail** — is the whole colour
strategy in miniature, and the interface reproduces it:

- **Deep navy field** → the neutral ramp, surfaces and chrome
- **Hot focal body** → Sedna Red, for the one action that matters on a screen
- **Cool satellite** → Orbit Blue, for links, information and quiet emphasis

### 1.2 The mark

An elliptical orbit resolving into an *S*, with a coral body at the focus and a small blue satellite
on the lower arc. All artwork sits on a 120-unit grid.

| Element | Value |
|---|---|
| Orbit and *S* form | `#17346E` on light, `#F8FAFC` on dark |
| Focal sphere | gradient `#FD7636` → `#F34238`; flattens to Sedna Red `#FF6B4A` |
| Satellite | `#59C3FF` |

**The mark is drawn three times, not scaled three ways.** The orbit is a hairline that fills in and
reads as noise when shrunk, so the stroke thickens and detail drops as the size falls:

| Tier | Stroke | Use at | Satellite |
|---|---|---|---|
| full | 2.5 | 96px and above | yes |
| compact | 5.5 | 32–64px | yes, larger |
| micro | 8 | 16px | **dropped** |

The PNG ladders in `assets/brand/` already switch tier by size, so picking by size gets the right
drawing. Two ladders exist because a transparent mark is invisible on the wrong surface:
`sedna-ui-icon-*` is navy for light backgrounds, `sedna-ui-icon-on-dark-*` is Ice White for dark.
Where the background is unknown, use `sedna-ui-tile-*`, which carries its own Deep Space field.

**Clear space** on all four sides equals the radius of the focal body. **Minimum sizes:** standalone
icon 20px, horizontal lockup 96px wide.

**Never** rotate, recolour or add effects. **Never** rebuild the wordmark in another face.

### 1.3 The wordmark needs its font

`sedna-ui-wordmark*.svg` and `sedna-ui-logo-*.svg` set live `<text>` in **Outfit**. Without Outfit
installed they fall back to a generic sans — which the rule above forbids, and nothing errors. The
wordmark is simply wrong.

- **Mark and tile SVGs** are pure geometry. Safe anywhere.
- **Wordmark and lockup SVGs** are design sources. For anything web-facing use the **PNG** versions,
  where the type is already rasterised in Outfit.

`assets/brand/font/` holds the variable font so the SVGs open correctly. It is a brand asset only —
the UI uses the system sans and mono stacks and never loads it. Licence in `THIRD-PARTY-NOTICES.md`.

### 1.4 Core palette

The fixed points. Everything in §2 derives from them.

| Name | Hex | OKLCH | Role |
|---|---|---|---|
| **Sedna Red** | `#FF6B4A` | L 0.707 · C 0.187 · H 34 | brand signal |
| **Orbit Blue** | `#59C3FF` | L 0.779 · C 0.130 · H 237 | accent, links, information |
| **Sedna Navy** | `#17346E` | L 0.339 · C 0.107 · H 262 | mark structure, illustration |
| **Deep Space** | `#0F172A` | L 0.208 · C 0.040 · H 266 | dark canvas |
| **Navy Slate** | `#1E293B` | L 0.279 · C 0.037 · H 260 | dark elevated surface |
| **Ice White** | `#F8FAFC` | L 0.984 · C 0.003 · H 248 | light canvas, dark-theme text |
| **Dust Gray** | `#94A3B8` | L 0.711 · C 0.035 · H 257 | muted text on dark |

Sedna Navy was sampled from the mark and added because the palette otherwise had no mid-dark blue,
leaving illustration and the mark itself with no documented value.

---

## 2. The ramps

### 2.1 How they were built

Every ramp is generated in OKLCH with a fixed hue, a lightness curve shared across all families, and
a chroma bell peaking at the anchor. Working perceptually rather than in HSL means step 600 of any
two families carries the same visual weight — a green badge and a violet badge sit at the same
optical level, which HSL cannot deliver.

Three rules govern the output:

1. **Brand anchors are exact.** They appear in the ramps at their published values, unrounded.
2. **Hue never drifts more than ~1° per step.** A ramp stays recognisably one colour end to end.
3. **Every value was contrast-checked** against the surface it actually renders on.

### 2.2 Slate — the neutral spine

Four of the seven core colours are steps on one navy-tinted neutral ramp. Core colours in **bold**.

| Step | Hex | On light `#F8FAFC` | On dark `#0F172A` |
|---|---|---|---|
| 50 | **`#F8FAFC`** | — | 17.06 |
| 100 | `#EEF4FB` | 1.06 | 16.13 |
| 200 | `#DDE7F3` | 1.19 | 14.28 |
| 300 | `#C8D5E5` | 1.42 | 11.99 |
| 400 | **`#94A3B8`** | 2.45 | 6.96 |
| 500 | `#707E93` | 3.94 | 4.33 |
| 600 | `#515E72` | 6.28 | 2.72 |
| 700 | `#354255` | 9.73 | 1.75 |
| 800 | **`#1E293B`** | 13.98 | 1.22 |
| 900 | **`#0F172A`** | 17.06 | — |
| 950 | `#04071B` | 19.09 | 1.12 |

### 2.3 Coral — the brand ramp

| Step | Hex | White on it | On light | On dark |
|---|---|---|---|---|
| 50 | `#FFF4F1` | 1.08 | 1.03 | 16.55 |
| 100 | `#FFE9E3` | 1.17 | 1.11 | 15.32 |
| 200 | `#FFD1C5` | 1.38 | 1.32 | 12.90 |
| 300 | `#FFB4A0` | 1.71 | 1.63 | 10.45 |
| 400 | `#FF8F74` | 2.23 | 2.13 | 8.02 |
| 500 | **`#FF6B4A`** | 2.82 | 2.69 | 6.34 |
| 600 | `#D73F1A` | **4.55** | 4.35 | 3.92 |
| 700 | `#BE2E06` | 5.86 | 5.60 | 3.05 |
| 800 | `#A22000` | 7.66 | 7.32 | 2.33 |
| 900 | `#7B1403` | 10.80 | 10.33 | 1.65 |
| 950 | `#580000` | 14.84 | 14.19 | 1.20 |

### 2.4 Orbit — accent and information

| Step | Hex | White on it | On light | On dark |
|---|---|---|---|---|
| 50 | `#EFF8FF` | 1.07 | 1.03 | 16.61 |
| 100 | `#E0F1FF` | 1.15 | 1.10 | 15.47 |
| 200 | `#BDE3FF` | 1.35 | 1.29 | 13.26 |
| 300 | `#90D2FF` | 1.63 | 1.56 | 10.92 |
| 400 | **`#59C3FF`** | 1.97 | 1.88 | 9.07 |
| 500 | `#3AA8E2` | 2.67 | 2.55 | 6.70 |
| 600 | `#158FC6` | 3.64 | 3.47 | 4.91 |
| 700 | `#0077A5` | **5.02** | 4.80 | 3.56 |
| 800 | `#005E82` | 7.18 | 6.87 | 2.49 |
| 900 | `#004661` | 10.25 | 9.80 | 1.74 |
| 950 | `#002C3F` | 14.66 | 14.02 | 1.22 |

### 2.5 Navy — mark and illustration only

Not consumed by interface tokens. Documented so diagrams, spot illustration and the mark have a
governed range.

| Step | Hex | | Step | Hex |
|---|---|---|---|---|
| 50 | `#F4F7FC` | | 500 | `#809DD2` |
| 100 | `#E9EFF9` | | 600 | `#6484C2` |
| 200 | `#D4DEF0` | | 700 | `#4B6CAB` |
| 300 | `#BACAE7` | | 800 | `#35548F` |
| 400 | `#9DB4DD` | | 900 | **`#17346E`** |
| | | | 950 | `#0E2450` |

### 2.6 Support ramps

Derived on the same lightness curve so they sit level with the primaries.

| Family | 200 | 300 | 400 | 500 | 600 | 700 | 800 | 900 |
|---|---|---|---|---|---|---|---|---|
| **Green** (go) | `#A2F4B6` | `#75E594` | `#44D272` | `#22C55E` | `#00A043` | `#008433` | `#006924` | `#004E16` |
| **Amber** (warn) | `#FFD5A2` | `#FEBA61` | `#F09F24` | `#F59E0B` | `#B87300` | `#995E00` | `#7A4900` | `#5D3400` |
| **Crimson** (danger) | `#FFCEDA` | `#FFAFC4` | `#FF87AB` | `#F44588` | `#D92B73` | `#C72268` | `#A60653` | `#80003D` |
| **Violet** (secret) | `#DED7FF` | `#CBBFFF` | `#B6A2FF` | `#A283FF` | `#8E61F7` | `#754BD3` | `#5D37AD` | `#452685` |
| **Cyan** (badge) | `#9CECFC` | `#6ADCF1` | `#22D3EE` | `#00B0C8` | `#0096AA` | `#007B8D` | `#00616F` | `#004853` |
| **Orange** (badge) | `#FFD4AE` | `#FEB979` | `#F59E4B` | `#DA852E` | `#BF6E10` | `#A15800` | `#814400` | `#623000` |
| **Teal** (badge) | `#A5EEE1` | `#79DFCF` | `#47CBB9` | `#14B8A6` | `#009A8B` | `#007F72` | `#00645A` | `#004B42` |
| **Indigo** (code) | `#D4DBFF` | `#BBC5FF` | `#9FABFF` | `#8590FD` | `#6F79E0` | `#5A61BF` | `#464B9B` | `#323777` |

**These eight run 200–900, not 50–950**, because those are the steps the token map
consumes. One place notices: high contrast (§7.3) wants an ultra-pale tint and an ultra-dark opaque
background, and reaches for a `100` and a `950` that do not exist. It takes the nearest available step
instead — 200 and 900 — which is marginally less extreme than the mode would like.

That is a deliberate limit rather than an oversight. Extending a ramp means generating two more steps
per family on the same lightness curve, and a value invented by eye in the *one* mode whose entire
purpose is guaranteed separation is worse than a measured step that is slightly conservative. Extend
them properly when there is a reason to, and re-measure §7.1 afterwards.

---

## 3. Three decisions that shaped the system

These are places where the palette could not be applied literally. Each is a constraint, not a
preference, and each is encoded as a **ramp relationship** rather than a hard-coded value — so it
holds for every theme, not just Sedna.

### 3.1 Sedna Red cannot be a button

`--on-solid` is `#ffffff` and is shared by every filled control. White on Sedna Red `#FF6B4A`
measures **2.82:1** — far below the 4.5:1 AA requires for the 13px/500 label a `.btn` renders. Sedna
Red is a display colour, not an action colour.

The library already solved this for its semantic families: the filled ones sit one ramp step darker
than the palette's mid tone, because `--on-solid` is white. The brand follows the same rule.

**The relationship:** `--brand` takes ramp step **600**, the darkest-chroma point on the hue that
still carries white text at AA — `#D73F1A`, at 4.55:1. This is not "coral, darkened"; it is solved
for maximum chroma at the contrast boundary, which is why it stays vivid where a naive darkening
goes brick.

Sedna Red survives everywhere it can be seen at full strength: the mark, `--brand-soft` on dark,
tints, focus glows, the sidebar indicator. **The brand you see is step 500; the brand you click is
step 600.**

### 3.2 The accent goes to Orbit Blue

`--accent` drives links and decorative icons. Giving it Orbit Blue rather than a second coral keeps
links visually distinct from primary actions — a reader should never have to guess whether coral text
is a link or a button — and gives the second brand colour real work.

A side benefit: previously `--brand` and the `--info-*` family were both blue, so a primary button
and an info banner shared a hue. With the brand coral, information owns blue outright.

### 3.3 Danger cannot stay red

The only change that is forced rather than chosen. The old `--danger-solid` `#DC2626` sits at hue 27°
in OKLCH; the brand coral sits at 34°. Seven degrees apart at similar lightness, they are effectively
the same colour — "Delete" and "Save" would be indistinguishable at a glance.

Danger therefore moves to **crimson** at hue 2°, and slightly darker than the brand, so the two
separate on two axes at once:

| | Hex | OKLCH L | OKLCH H | Δhue from brand |
|---|---|---|---|---|
| `--brand` | `#D73F1A` | 0.588 | 34° | — |
| `--danger-solid` | `#C72268` | 0.549 | 2° | **32°** |
| *(rejected)* | `#DC2626` | 0.577 | 27° | 7° |

**The rule this generalises to:** a theme whose brand hue lands within ~15° of a semantic family must
move that family. Green brands collide with `go`, blue brands with `info` and `accent`. Even with 32°
of separation, a filled primary and a filled destructive never sit adjacent — the destructive takes
the outline or ghost variant, and only a modal's confirm is filled.

---

## 4. How theming works

### 4.1 Two tiers

Colour lives in two layers, and the split is the whole point:

**Tier 1 — the palette.** The ramps in §2, as `--slate-500`, `--coral-600`, `--orbit-400` and so on.
Palette tokens hold literals only, never a reference to another token. They are not remapped by
variant, colour-vision or contrast — a ramp step is a colour, not a decision.

**Tier 2 — the semantic tokens.** The roles the stylesheet actually uses, each pointing at a palette
step:

```css
--brand:        var(--coral-600);   /* §3.1 — the one that carries white text */
--brand-soft:   var(--coral-500);   /* Sedna Red at full strength             */
--danger-solid: var(--crimson-700); /* §3.3 — forced off red                  */
--accent:       var(--orbit-400);   /* §3.2                                   */
--bg:           var(--slate-900);
```

A theme therefore redefines **ramp anchors**, not every role. That is why this document no longer
contains a table assigning a value to each of ~160 tokens: there is nothing left to assign by hand.

Names in tier 2 are unchanged from earlier releases, so an app that overrides `--brand` keeps
working.

### 4.2 Theme and variant

Two orthogonal attributes on `<html>`:

- **`data-theme`** — which theme. Any registered name.
- **`data-variant`** — `dark` or `light`. What a user toggles.

```html
<html data-theme="sedna" data-variant="dark">
```

**Every theme must define both variants.** A user who switches to light must never land on a theme
without one, so it is enforced twice: the C# type makes both non-optional, and a guard checks the
emitted CSS covers each theme × variant pair.

Dark is the default. `data-variant` is resolved **before first paint**, from a stored choice or from
`prefers-color-scheme`, and never from a `@media` block — a media block cannot be overridden by an
explicit user choice.

`data-cvd`, `data-contrast` and `data-density` compose on top, unchanged.

### 4.3 Defining a theme

A theme is data, not a stylesheet:

```csharp
builder.Services.AddSednaUi(o =>
{
    o.Themes  = [SednaTheme.Sedna, myTheme];
    o.Default = "sedna";
});
```

Its palette is emitted into `<head>` by a component, so it is present before the first byte of body
renders:

```razor
<head>
    <SednaBrandStyle />
</head>
```

Additional themes are emitted alongside the default, so switching is an attribute flip with no round
trip and no flash. Nothing is injected by JavaScript: CSS that arrives after first paint shows the
wrong colours on every load.

### 4.4 Minimum viable rebrand

Redefining the coral anchor carries every brand-tinted surface and every focus ring with it, in both
variants, because the tint, ring and glow tokens are `color-mix`ed from `--brand`. Do not pin them
unless you mean to break that relationship.

If a theme's brand hue lands near a semantic family, §3.3's rule applies — move the family.

---

## 5. Design language, carried over unchanged

### 5.1 Corner rounding

Named by role, not size. Every `border-radius` in the library except a literal circle resolves
through one of these.

| Token | Value | Applies to |
|---|---|---|
| `--radius-control` | `5px` | buttons, inputs, input groups |
| `--radius-surface` | `6px` | cards, panels, tips, flyouts, toasts |
| `--radius-panel` | `8px` | the modal — the largest surface |
| `--radius-inner` | `4px` | anything nested one border inside a control or surface |
| `--radius-small` | `3px` | inline code, the checkbox |
| `--radius-pill` | `999px` | badges, count pills, progress track, switch |

The 1px gaps between 3, 4, 5 and 6 are deliberate. A nested element at the same radius as its parent
reads as misaligned; stepping down one pixel per level is what makes corners look concentric.

### 5.2 Opacity

The palette shifted; the transparency did not.

| Purpose | Dark | Light |
|---|---|---|
| Divider | 6% | 7% |
| Surface soft / strong | 3% / 5% | 3% / 5.5% |
| Badge background | 7% | 4% |
| Table head | 4% | 3% |
| Skeleton base / sheen | 6% / 11% | 6% / 11% |
| Progress track | 8% | 8% |
| Brand tint | 14% | 10% |
| Focus ring — filled / neutral / checkbox | 50% / 40% / 35% | 50% / 40% / 35% |
| Input focus glow | 18% | 18% |
| Status tint — base / hover / active | 12% / 20% / 28% | 8–10% / 14% / 20% |
| Modal backdrop | 65% | 35% |
| In-card overlay | 66% | 66% |
| Spotlight dim | 62% | 42% |
| Scroll edge fade | 30% | 16% |

### 5.3 Spacing, type, controls, motion

| Scale | Values |
|---|---|
| Spacing `--space-1…11` | 2, 4, 6, 8, 10, 12, 16, 20, 24, 32, 40px |
| Type `--text-1…11` | 10, 11, 12, 13, 14, 15, 16, 18, 22, 26, 40px |
| Control heights | 28 / 36 / 44px (44 = the WCAG AAA target) |
| Motion | 0.12s hover · 0.18s enter/leave · 0.25s value animating |
| Loops | spin 0.6s · progress 1.1s · skeleton 1.4s · pulse 1.6s |
| Cell padding | 8px vertical, 12px horizontal |
| Page cap | 1800px · code clamp 360px |

Body copy is `--text-4` (13px) for content, `--text-5` (14px) for page chrome. Sizes are **not** on
the spacing scale — a 28px close button is a dimension, not a gap.

**Typefaces** are the system sans stack for UI and the system mono stack for code and tabular values.
The wordmark's geometric sans is a brand asset for the mark only and is never loaded as a UI face.

---

## 6. Applying colour

**One focal point per screen.** A screen has one primary action, and it is the only filled brand
element on it. Everything else that needs emphasis uses weight, border or the neutral ramp. If two
coral buttons are visible at once, one is wrong. This is not conservatism — it is what makes the
mark's logic work. The orbit has one focus.

**Colour is never the only signal.** Every status carries an icon and a label as well as a hue.
Badges pair tint with text. Table rows use tint plus a left border.

**Buttons.** Primary is filled `--brand`. Neutral is `--bg-elevated` with `--border`. Destructive is
outlined by default; only a modal's confirm may be filled. Never a filled primary beside a filled
destructive (§3.3).

**Links.** `--accent`. Underlined in prose, unstyled in navigation chrome where position carries the
affordance.

**Sidebar.** The active item takes a coral left indicator plus `--brand-tint` as a background. On the
navy field that is the mark's exact relationship.

**Focus.** Every focusable control shows a ring built from the `--brand-ring-*` family. Because those
are mixed from `--brand`, the ring follows the brand automatically. Never remove it.

**Tables.** Header takes `--table-head-bg`. Numeric columns are mono and right-aligned. Status is a
badge, never a coloured cell background.

**Empty states and errors.** Say what happened and what to do. An error names the problem and the
fix; it does not apologise.

**The reconnect banner** is Blazor's own connection UI, restyled in library classes. It stays dark in
both variants by design — it appears when the circuit is gone, and a banner that changes colour with
the theme reads as part of the page rather than as something wrong with it. Six tokens, sitting one
family apart so "retrying" and "gave up" are not the same colour:

| State | Background | Border | Text |
|---|---|---|---|
| attempting | amber 900 | amber 600 | amber 200 |
| failed | crimson 900 | crimson 700 | crimson 400 |

The failed state deliberately lands on the same steps as `--danger-solid` and `--danger-fg`: a dead
circuit *is* danger-shaped, and borrowing the family rather than inventing one keeps a theme from
having to answer for it separately.

---

## 7. Accessibility

### 7.1 Conformance

Every text token was measured against the surface it renders on. Body and label pairs meet or exceed
**WCAG 2.1 AA (4.5:1)**; icon-only and non-text tokens meet **3:1**.

The lowest passing values in the system:

| Pair | Ratio |
|---|---|
| White on `--brand` | 4.55 |
| White on `--danger-solid` (dark) | 4.61 |
| `--go-fg` on light canvas | 4.62 |
| `--teal-fg` on light canvas | 4.69 |
| `--cyan-fg` on light canvas | 4.76 |
| `--accent` on light canvas | 4.80 |

These six sit closest to the floor. **Do not lighten any of them without re-measuring.** If you need
headroom, take the next darker ramp step rather than adjusting by eye. A theme generated from anchors
inherits this constraint — the generator has to hit the same boundary, not approximate it.

### 7.2 Colour-vision deficiency

`[data-cvd="1"]` swaps the **go** family to blue, so go-versus-danger reads as blue-versus-red.

The rebrand introduces a second collision the old palette did not have: under deuteranopia and
protanopia, **coral and crimson converge** — brand and danger both desaturate toward a similar warm
brown. The CVD block therefore also pushes danger colder and darker, so the pair separates on
lightness when hue is unavailable. Any theme whose brand and danger hues sit close together needs the
same treatment.

### 7.3 High contrast

`[data-contrast="more"]` and `prefers-contrast: more` remap borders to full strength, turn translucent
tints opaque, and lift muted text to body colour. Shadows are deliberately kept — once every surface
is the same lightness, they are the only remaining depth cue.

Brand text goes to ramp step 300 on dark and 800 on light, and `--brand-tint` becomes opaque. A 14%
tint over an unknown surface has an unknown contrast, which is the one thing this mode cannot have.

### 7.4 Motion

`prefers-reduced-motion` is honoured. Setting the three `--motion-*` tokens to `0s` disables every
transition without touching a rule.

---

## 8. Verifying a theme

The catalogue's **Tokens** page reads values from the stylesheet the browser actually loaded, so it
is the fastest way to confirm a theme landed. The **Branding** page shows every theme × variant pair
side by side.

Three things worth checking on a new theme, in this order:

1. **White on `--brand`** clears 4.5:1. If it does not, the brand anchor is too light — take step 600
   of a darker ramp, not a hand-darkened value.
2. **`--brand` against `--danger-solid`** are more than ~15° apart in OKLCH hue. If not, move danger.
3. **The six near-floor pairs in §7.1** still pass. A generated ramp can drift a step and quietly
   take one below the line.

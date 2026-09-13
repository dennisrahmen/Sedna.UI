# `js-parts/` — how the browser script is authored

This directory **is** the script. `wwwroot/js/Sedna.UI.js` is generated from it and must never be
edited by hand.

```bash
build/bundle-js.sh            # regenerate the shipped script
build/bundle-js.sh --check    # fail if it is out of date (CI-friendly)
```

`The_shipped_script_matches_its_parts` fails the build if the two disagree.

`Sedna.UI.boot.js` is **not** part of this. It is a separate ~40-line file loaded in `<head>` to
apply the stored theme before first paint, and it must stay standalone — bundling it would defeat its
entire purpose.

## The shape every part takes

```js
(function (ui) {
    var core = ui._;                 // only if you need the shared internals
    ui.something = { … };            // or ui.something = function () { … };
})(window.sednaUi);
```

Each part is a self-contained IIFE that extends the one global, so **a part is a valid script on its
own** — take `00-core.js` plus that part to use one feature outside NuGet. `00-core.js` must come
first: it creates the global and the shared internals every other part reads.

## Adding a part

1. Create `NN-name.js` here. **The `NN-` prefix is the load order** — the generator discovers every
   `*.js` in the directory and concatenates them in byte-ordinal filename order. There is no manifest
   to update.
2. End the file with a terminated IIFE: `})(window.sednaUi);`. **The semicolon is required** and the
   build checks for it — without it, automatic semicolon insertion can splice your part into the next
   one as a call expression.
3. Run `build/bundle-js.sh`.
4. Document the new members in the JavaScript table in `docs/architecture.md`.
5. `dotnet test`.

### Choosing the number

| Range | What lives there |
|---|---|
| `00` | core — the global, `config`, the shared internals, `configure()` |
| `1x` | settings (theme, colour-blind palette, density, language) |
| `2x` | behaviour delegated from `document` — hover hints, copy, menus, tabs; accordion, drawer and palette land here too |
| `3x` | the Markdown editor |
| `4x` | small interop helpers |
| `5x` | things the library puts on the page itself — notifications, the audio ping, toasts |

A `2x` part may call a helper from `4x` even though it loads earlier: the call happens
inside an event handler, long after every part has run. Do not read a later part's
member at load time.

**The `2x` band is full**, so it holds two parts numbered `20`: `20-select.js` then `20-tips.js`.
Ordering still works — the generator sorts by filename, and `s` precedes `t` — and nothing in the band
depends on load order anyway. Renumbering to make room would move every part after it, which is a
change to nine files to avoid a duplicate digit.

## `ui._` is private

`00-core.js` puts `config`, `key()` and `readRaw()` on `ui._`. The underscore means exactly one thing:
**not part of the public contract.** It exists so the parts can share state that used to live in one
closure. Nothing outside this directory may read it, it is not documented for consuming apps, and it
may change in a patch release. Everything an app may touch is a named member on `sednaUi` itself.

## Rules

- **Generic UI behaviour only.** App-specific interop stays in the app's own script. If it knows about
  incidents, approvals or tours, it does not belong here.
- **The public API is a contract.** `sednaUi` is a pinned global that consuming apps call into.
  Removing or renaming a member, or changing a signature, is a **major** version change. Adding one is
  minor.
- **The script never goes looking for .NET.** The boundary is stated once, in the root `CLAUDE.md`
  under **The interop boundary**; what it means inside this directory:

  - Parts change the DOM and dispatch events that Blazor's bindings pick up — the Markdown editor is the
    reference. Never `DotNet.invokeMethod`, and never go looking for a .NET object a part was not handed.
  - **Completing an awaited call is allowed.** Return a promise and settle it; `modal.show` in
    `42-modal.js` is the reference. A promise that waits on the reader must settle on a route that does
    not depend on an event being dispatched — see that part's header for why the `close` event is not
    one.
  - **Invoking a reference the caller hands in is allowed**, and `watchSettings` / `unwatchSettings` in
    `40-interop.js` is the reference: `ISednaSettings` passes a `DotNetObjectReference`, owns and
    disposes it, and the part invokes `SettingsChanged` on it and nothing else. The script works with no
    .NET on the page at all — `settings.onChange` takes a plain function — and the coupling is visible
    in the calling C#.
  - **A function does not cross; a handle does.** When a member takes or returns a function, give the
    bridge an id and keep the function in a table keyed by it, as `watchSettings` does. Keep the
    behaviour part framework-agnostic and put the bridge in `4x`.
- **Draw no markup, except the toast and the hover-hint bubble.** A part adds classes, attributes and
  state to elements the app wrote. `51-toast.js` and `20-tips.js` are the two parts that build UI, and
  the root `CLAUDE.md` says why they are the only ones. A part may add an element nobody sees or
  reads — the palette's detached navigation anchor, a clipboard textarea — but never a panel, a
  dialog, a control, or a word of text the app did not supply.
- **Rows come from the app's `<template>`.** A part that renders data clones a template the app wrote
  and fills its `data-*` slots with `textContent`, never `innerHTML`. `24-palette.js` is the reference.
- **Fail soft.** Wrap anything a browser may refuse (`localStorage`, clipboard, `Notification`,
  `AudioContext`) in `try`/`catch` and degrade. A blocked API must not break the page.
- **Delegate from `document`, do not wire per element.** Blazor re-renders, and re-wiring on every
  render leaks handlers. The hover-hint engine is the pattern to copy.
- **Skip `.sidebar` for hover behaviour.** The collapsed rail has a CSS flyout; both firing produces a
  double tooltip.
- **ES5-compatible style, no build step beyond concatenation.** `var`, `function`, no modules — the
  file is loaded as a classic script so it can define the global synchronously. `async`/`await` is in
  use already and is fine.
- **Do not make CSS depend on this.** The stylesheet must apply with scripting disabled or blocked.

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
| `5x` | things the library puts on the page itself — notifications, the audio ping, toasts, the confirmation dialog |

A `2x` part may call a helper from `4x` even though it loads earlier: the call happens
inside an event handler, long after every part has run. Do not read a later part's
member at load time.

## `ui._` is private

`00-core.js` puts `config`, `key()` and `readRaw()` on `ui._`. The underscore means exactly one thing:
**not part of the public contract.** It exists so the parts can share state that used to live in one
closure. Nothing outside this directory may read it, it is not documented for consuming apps, and it
may change in a patch release. Everything an app may touch is a named member on `sednaUi` itself.

## Rules

- **Generic UI behaviour only.** App-specific interop stays in the app's own script. If it knows about
  incidents, approvals or tours, it does not belong here.
- **The public API is a contract.** `sednaUi` is a pinned global and four apps call into it.
  Removing or renaming a member, or changing a signature, is a **major** version change. Adding one is
  minor.
- **Never call back into .NET.** Parts manipulate the DOM and dispatch events that Blazor's bindings
  pick up — the Markdown editor is the reference for this. No `DotNet.invokeMethod`.
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

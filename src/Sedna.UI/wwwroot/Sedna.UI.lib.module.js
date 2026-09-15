/* Sedna.UI — Blazor JavaScript initializer.
   ───────────────────────────────────────────────────────────────────────────
   Blazor finds and loads this file by its name; no app references it, and nothing
   outside Blazor runs it.

   It does one thing: it tells Blazor that the drag-and-drop events Sedna.UI.js
   dispatches carry data, so `@onsedna-drop` in a component receives a
   SednaDropEventArgs rather than an empty EventArgs. The events themselves are
   ordinary bubbling DOM events, and a page with no Blazor on it listens to them with
   addEventListener. Nothing here calls into .NET.

   The C# half is the `EventHandlers` class in Sedna.UI.
   ─────────────────────────────────────────────────────────────────────────── */

const events = ['sedna-dragstart', 'sedna-drop', 'sedna-dragend'];

let registered = false;

function register(blazor) {
    if (registered || !blazor || typeof blazor.registerCustomEventType !== 'function') return;
    registered = true;
    for (const name of events) {
        blazor.registerCustomEventType(name, {
            createEventArgs: event => Object.assign({}, event.detail)
        });
    }
}

// A Blazor Web App calls the first; a standalone Blazor Server or WebAssembly app, the
// second. Both may run in one page, and a name registered twice throws.
export function afterWebStarted(blazor) { register(blazor); }
export function afterStarted(blazor) { register(blazor); }

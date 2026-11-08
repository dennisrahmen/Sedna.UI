/* Sedna.UI — Blazor JavaScript initializer.
   ───────────────────────────────────────────────────────────────────────────
   Blazor finds and loads this file by its name; no app references it, and nothing
   outside Blazor runs it.

   It does two things. It tells Blazor that the events Sedna.UI.js dispatches carry
   data, so `@onsedna-drop` in a component receives a SednaDropEventArgs, and
   `@onsedna-graph-select` a SednaGraphNodeEventArgs, rather than an empty EventArgs.
   The events themselves are ordinary bubbling DOM events, and a page with no Blazor on
   it listens to them with addEventListener. And it tells the graph and the editor when
   Blazor has started, so one on a prerendered page starts once, in the markup the
   interactive render leaves, rather than in the prerendered markup it replaces.
   Nothing here calls into .NET.

   The C# half is the `EventHandlers` class in Sedna.UI.
   ─────────────────────────────────────────────────────────────────────────── */

const events = [
    'sedna-dragstart', 'sedna-drop', 'sedna-dragend',
    'sedna-graph-ready', 'sedna-graph-change', 'sedna-graph-select', 'sedna-graph-open', 'sedna-graph-hover',
    'sedna-graph-context', 'sedna-graph-connect', 'sedna-graph-expand', 'sedna-graph-collapse',
    'sedna-editor-ready', 'sedna-editor-change', 'sedna-editor-media', 'sedna-editor-file',
    'sedna-follow', 'sedna-resize', 'sedna-countdown-end',
    'sedna-tree-expand', 'sedna-tree-collapse', 'sedna-tree-select', 'sedna-tree-check',
];

let registered = false;

function register(blazor) {
    if (registered || !blazor || typeof blazor.registerCustomEventType !== 'function') return;
    registered = true;
    for (const name of events) {
        // Blazor throws on a name registered twice, and an app may have registered one of
        // these itself before the library did — the way round a missing registration.
        // Theirs stays, the rest are still registered, and nothing stops the app starting.
        try {
            blazor.registerCustomEventType(name, {
                createEventArgs: event => Object.assign({}, event.detail)
            });
        } catch (err) { /* already registered by the app */ }
    }
}

function started() {
    // The surface loader holds graphs and editors only on a page carrying interactive
    // markers, and releases itself after a few seconds if this is never called. One hold
    // serves every surface; each front door hands out the same release.
    window.sednaUi?.graph?.release?.();
    window.sednaUi?.editor?.release?.();
}

// Every hosting model calls a different one of these: a Blazor Web App `afterWebStarted`
// and then the one for the render mode that starts; a standalone WebAssembly app
// `afterWebAssemblyStarted` instead of `afterStarted` once the module exports it; a
// standalone Server app `afterStarted`, or `afterServerStarted` the same way. So each of
// them registers, and `register` runs once however many are called — a name registered
// twice throws. EventRegistrationTests holds the four exports and the event list.
export function afterWebStarted(blazor) { register(blazor); }
export function afterStarted(blazor) { register(blazor); started(); }

// A Blazor Web App's interactive render replaces what was prerendered once its circuit,
// or its WebAssembly runtime, has started.
export function afterServerStarted(blazor) { register(blazor); started(); }
export function afterWebAssemblyStarted(blazor) { register(blazor); started(); }

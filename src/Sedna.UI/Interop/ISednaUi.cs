namespace Sedna.UI;

/// <summary>
/// Typed access to the <c>sednaUi</c> browser API from C#.
/// </summary>
/// <remarks>
/// <para>
/// Register it with <c>AddSednaUi</c> and inject it. Every member is a
/// JavaScript interop call, so <b>none of them can run during prerendering</b> —
/// call them from an event handler, or from
/// <c>OnAfterRenderAsync(firstRender: true)</c>. They deliberately do not swallow
/// the <see cref="InvalidOperationException"/> that prerendering raises: a call
/// that silently did nothing would be far harder to find than one that threw.
/// </para>
/// <para>
/// A function does not cross the interop boundary, so where the JavaScript surface
/// hands one over, the member here hands over data naming the same thing:
/// <see cref="ToastAsync"/> returns a <see cref="SednaToast"/> handle where
/// <c>toast()</c> returns a remover, and <see cref="SetTipsEnabledAsync"/> is a
/// boolean where <c>tips.gate</c> is a predicate.
/// </para>
/// </remarks>
public interface ISednaUi
{
    /// <summary>
    /// Shows a toast: a short line confirming something that already happened.
    /// </summary>
    /// <param name="message">The line to show. Inserted as text, never as markup.</param>
    /// <param name="kind">The semantic family, which chooses the icon and colour.</param>
    /// <param name="title">An optional bold first line.</param>
    /// <param name="timeoutMs">
    /// How long it stays, in milliseconds. <c>0</c> stays until dismissed, which is
    /// usually right for a failure.
    /// </param>
    /// <param name="dismissible">Whether to render the close button.</param>
    /// <param name="dismissLabel">
    /// The close button's accessible name for this toast. Unset uses
    /// <see cref="SednaUiOptions.ToastDismissLabel"/>.
    /// </param>
    /// <returns>
    /// The toast, once it is on the page: dismiss it early or replace what it says with the
    /// handle. An app with nothing more to say about it can ignore the result.
    /// </returns>
    /// <remarks>
    /// Anything the user must act on is an <c>.alert</c>, not a toast — a toast
    /// carrying a required action is an action nobody performs.
    /// </remarks>
    Task<SednaToast> ToastAsync(
        string message,
        ToastKind kind = ToastKind.Info,
        string? title = null,
        int timeoutMs = 4000,
        bool dismissible = true,
        string? dismissLabel = null);

    /// <summary>
    /// Opens a <c>&lt;dialog&gt;</c> the app wrote — a <c>.modal</c>, a <c>.drawer</c> or a
    /// <c>.sheet</c> — with the platform's own <c>showModal()</c>, and waits for it to close.
    /// </summary>
    /// <param name="elementId">The <c>id</c> of the <c>&lt;dialog&gt;</c>.</param>
    /// <param name="cancellationToken">
    /// Stops the wait and closes the dialog. Passed through to the interop call, which is
    /// what exempts it from Blazor's one-minute interop timeout — without a token, a dialog
    /// the reader left open for a minute would throw into the caller.
    /// </param>
    /// <returns>
    /// The dialog's <c>returnValue</c> once it closes — the <c>value</c> of the
    /// <c>&lt;form method="dialog"&gt;</c> button that closed it, or what
    /// <see cref="CloseModalAsync"/> passed — or <see langword="null"/> when it closed
    /// without one: <kbd>Escape</kbd>, a close with no value, or a button whose value is
    /// empty.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is what makes a dialog a dialog: the top layer, a focus trap,
    /// Escape-to-close and inert content behind it, all four from the browser. A
    /// <c>.modal-backdrop</c> div behind an <c>@if</c> has none of them.
    /// </para>
    /// <para>
    /// The markup, the classes, the wording and the styling are the app's; this only
    /// presents it. A confirmation is therefore a <c>.modal-sm</c> the app writes, with a
    /// <c>&lt;form method="dialog"&gt;</c> whose buttons carry values, and one comparison
    /// on the result. For a dialog that is its own component and hands back a typed
    /// result, use <see cref="ISednaOverlays"/>.
    /// </para>
    /// <para>
    /// A second call on a dialog that is already open joins the first wait. An id that is
    /// not a <c>&lt;dialog&gt;</c> completes with <see langword="null"/> and a console
    /// warning rather than an exception, because an exception crossing the interop
    /// boundary from a Blazor handler tears down the circuit.
    /// </para>
    /// </remarks>
    Task<string?> ShowModalAsync(string elementId, CancellationToken cancellationToken = default);

    /// <summary>Closes a <c>&lt;dialog&gt;</c> opened with <see cref="ShowModalAsync"/>.</summary>
    /// <param name="elementId">The <c>id</c> of the <c>&lt;dialog&gt;</c>.</param>
    /// <param name="returnValue">
    /// Optional value to set as the dialog's <c>returnValue</c>, which is what the
    /// waiting <see cref="ShowModalAsync"/> completes with — the same place a
    /// <c>&lt;form method="dialog"&gt;</c> puts its submitter's value.
    /// </param>
    /// <returns>A task that completes once it is closed.</returns>
    /// <remarks>
    /// Not needed for Escape or for a <c>&lt;form method="dialog"&gt;</c> submit: the
    /// platform closes the dialog itself in both cases. This is for a Close button
    /// that is not a dialog-method submitter, and for closing from C# after work
    /// completes.
    /// </remarks>
    Task CloseModalAsync(string elementId, string? returnValue = null);

    /// <summary>
    /// Starts a spotlight step: the app's <c>.spotlight-hole</c> and bubble follow the target
    /// while the page scrolls, resizes and re-renders.
    /// </summary>
    /// <param name="hole">The app's <c>.spotlight-hole</c>, as a CSS selector.</param>
    /// <param name="target">What to highlight, as a CSS selector. Several matches are unioned.</param>
    /// <param name="options">Padding, the bubble and its placement, and the lock.</param>
    /// <returns>
    /// The live step. Call <see cref="SednaSpotlight.UpdateAsync"/> after every render, and
    /// dispose it when the step ends.
    /// </returns>
    /// <remarks>
    /// A presenter: the hole, the bubble, its words and the sequence of steps are all the
    /// app's markup and state. This measures and positions them, which is the only part the
    /// browser alone knows. The hole is placed against its offset parent, which has to be
    /// positioned.
    /// </remarks>
    Task<SednaSpotlight> FollowSpotlightAsync(string hole, string target, SpotlightOptions? options = null);

    /// <summary>
    /// Turns hover hints on or off for the whole page — for a reader who switched them off in
    /// the app's settings, or while a tour owns their attention.
    /// </summary>
    /// <param name="enabled">False hides any hint showing and shows no more until set true.</param>
    /// <returns>A task that completes once the setting is applied.</returns>
    /// <remarks>
    /// The C# form of <c>sednaUi.tips.gate</c>. The gate is a predicate, and a function does
    /// not cross the interop boundary; the common reason to gate hints is a boolean the app
    /// already holds, and this is that boolean. Both apply: a hint shows only when enabled
    /// and when the gate, if any, allows it.
    /// </remarks>
    Task SetTipsEnabledAsync(bool enabled);

    /// <summary>Copies text to the clipboard.</summary>
    /// <param name="text">The text to copy.</param>
    /// <returns><see langword="true"/> if the browser allowed it.</returns>
    Task<bool> CopyTextAsync(string text);

    /// <summary>
    /// Wires every <c>.md-editor</c> in the document that is not wired yet.
    /// </summary>
    /// <returns>A task that completes once they are.</returns>
    /// <remarks>
    /// <para>
    /// The toolbar, the live preview and the Write/Preview switch are all behaviour, so
    /// an editor that nothing has called this for renders correctly and does nothing.
    /// Call it from <c>OnAfterRenderAsync</c>; it is idempotent per editor, so calling
    /// it again after a re-render only picks up editors that are new.
    /// </para>
    /// <para>
    /// It takes no element, because an app should not have to hold a reference to each
    /// editor to make it work. The textarea keeps its own value through
    /// <c>@bind:event="oninput"</c> — toolbar edits mutate it and dispatch a bubbling
    /// <c>input</c> event, which Blazor's own binding picks up.
    /// </para>
    /// </remarks>
    Task InitMarkdownAsync();

    /// <summary>Reads the stored theme, variant, colour-blind, density, direction and language settings.</summary>
    /// <returns>The settings currently applied.</returns>
    Task<SednaUiSettings> LoadSettingsAsync();

    /// <summary>
    /// Stores one setting and applies it immediately.
    /// </summary>
    /// <param name="key">One of <c>theme</c>, <c>variant</c>, <c>cvd</c>, <c>density</c>, <c>lang</c>.</param>
    /// <param name="value">
    /// <c>theme</c> takes a theme name (nothing stored falls back to
    /// <see cref="SednaUiOptions.Default"/>);
    /// <c>variant</c> takes <c>dark</c>, <c>light</c>, or <c>system</c> to follow
    /// <c>prefers-color-scheme</c> live; <c>cvd</c> takes <c>1</c> or anything else
    /// for off; <c>density</c> takes <c>compact</c> or anything else for the
    /// default; <c>lang</c> takes a two-letter code.
    /// </param>
    /// <returns>A task that completes once the setting is stored and applied.</returns>
    Task SaveSettingAsync(string key, string value);

    /// <summary>Pushes the configured options to the browser.</summary>
    /// <returns>A task that completes once the options are applied.</returns>
    /// <remarks>
    /// One of them is <see cref="SednaUiOptions.Default"/>, the theme that applies with nothing
    /// stored. The boot script runs long before this and reads its own copy from
    /// <c>data-theme-default</c>, so an app whose default is not <c>sedna</c> sets that attribute
    /// too — otherwise the first paint of a first visit carries the wrong theme name.
    /// </remarks>
    Task ConfigureAsync();

    /// <summary>Replaces the command palette's whole command list.</summary>
    /// <param name="commands">
    /// The commands now available. Call this whenever that changes — after a
    /// permission check, or on navigation.
    /// </param>
    /// <returns>A task that completes once the list is registered.</returns>
    /// <remarks>
    /// Until at least one command is registered, Ctrl/Cmd-K is left to the browser.
    /// </remarks>
    Task RegisterCommandsAsync(IReadOnlyList<PaletteCommand> commands);

    /// <summary>Ranks the registered commands against a query, best first.</summary>
    /// <param name="query">The search text. Empty returns every command in registration order.</param>
    /// <returns>The matching commands.</returns>
    /// <remarks>For an app that wants the palette's ranking in its own UI.</remarks>
    Task<IReadOnlyList<PaletteCommand>> RankCommandsAsync(string query);

    /// <summary>Opens the command palette.</summary>
    /// <returns>A task that completes once it is open.</returns>
    Task OpenPaletteAsync();

    /// <summary>Closes the command palette.</summary>
    /// <returns>A task that completes once it is closed.</returns>
    Task ClosePaletteAsync();

    /// <summary>Replaces the header search's whole index.</summary>
    /// <param name="items">Everything the search can find. Call this whenever that changes.</param>
    /// <returns>A task that completes once the index is registered.</returns>
    /// <remarks>
    /// Until at least one item is registered, an input marked <c>data-search</c>
    /// behaves as a plain text box — which is what an app rendering its own results
    /// with the <c>.search-*</c> classes wants.
    /// </remarks>
    Task RegisterSearchAsync(IReadOnlyList<SearchItem> items);

    /// <summary>Ranks the registered search index against a query, best first.</summary>
    /// <param name="query">The search text. Empty returns nothing.</param>
    /// <returns>Every match, best first. The dropdown's own cut is not applied.</returns>
    Task<IReadOnlyList<SearchItem>> RankSearchAsync(string query);

    /// <summary>Closes the header search's result panel.</summary>
    /// <returns>A task that completes once it is closed.</returns>
    /// <remarks>
    /// Call after navigating: the panel is anchored to a box the router may have
    /// just moved, and a result list for the previous page is worse than none.
    /// </remarks>
    Task CloseSearchAsync();

    /// <summary>Hides the hover hint, if one is showing.</summary>
    /// <returns>A task that completes once it is hidden.</returns>
    /// <remarks>Call after navigating, so a hint does not outlive the element it described.</remarks>
    Task HideTipsAsync();

    /// <summary>Closes every open dropdown menu.</summary>
    /// <returns>A task that completes once they are closed.</returns>
    /// <remarks>Call after navigating: a menu can otherwise survive a back-navigation.</remarks>
    Task CloseMenusAsync();

    /// <summary>Scrolls the frame's page column back to the top.</summary>
    /// <returns>A task that completes once it is scrolled.</returns>
    /// <remarks>
    /// <c>.page</c> is the only scroll container in the frame, so the window's own
    /// scroll position never moves and nothing the router does resets it. Without this,
    /// navigating leaves the new page at the offset the previous one was scrolled to.
    /// Call it from a <c>LocationChanged</c> handler.
    /// </remarks>
    Task ScrollPageTopAsync();

    /// <summary>Selects a tab by its own id or by the id of the panel it controls.</summary>
    /// <param name="tabOrPanelId">The tab's id, or its <c>aria-controls</c> target.</param>
    /// <returns>A task that completes once the tab is selected.</returns>
    Task SelectTabAsync(string tabOrPanelId);

    /// <summary>Opens a URL in a new tab, with <c>noopener</c>.</summary>
    /// <param name="url">Where to go.</param>
    /// <returns>A task that completes once the tab is requested.</returns>
    Task OpenTabAsync(string url);

    /// <summary>Reads the viewport width in CSS pixels.</summary>
    /// <returns>The width.</returns>
    /// <remarks>
    /// For a decision the server has to make. Do not use it to reimplement a media
    /// query — the stylesheet's breakpoints are the only ones that stay in step.
    /// </remarks>
    Task<int> ViewportWidthAsync();

    /// <summary>Reads the browser's time zone as an IANA id, such as <c>Europe/Lisbon</c>.</summary>
    /// <returns>The id, or <see langword="null"/> where the browser does not report one.</returns>
    /// <remarks>
    /// <para>
    /// An id, never an offset: an offset is only true until that zone's next
    /// transition. Turn it into a <see cref="TimeZoneInfo"/> with
    /// <c>TimeZoneInfo.TryFindSystemTimeZoneById</c>, which takes IANA ids on every
    /// platform, and render stored UTC instants through it.
    /// </para>
    /// <para>
    /// This and <c>data-tz-cookie</c> on the boot script answer the same question at
    /// two different moments, and an app that renders instants wants both: the cookie
    /// is what the <b>first server render</b> reads, and this is what a <b>circuit</b>
    /// reads. A first visit carries no cookie at all, and a navigation in Blazor
    /// Server is not an HTTP request — so without this a new browser stays on the
    /// fallback zone until the reader reloads.
    /// </para>
    /// <para>
    /// It is read from <c>Intl</c> at call time and stored nowhere, so a reader who
    /// travels is answered correctly on the next call.
    /// </para>
    /// </remarks>
    Task<string?> GetTimeZoneAsync();

    /// <summary>Reads a <c>localStorage</c> value.</summary>
    /// <param name="key">The raw key. The library's storage prefix is <b>not</b> applied.</param>
    /// <returns>The value, or null when absent or when storage is blocked.</returns>
    Task<string?> GetItemAsync(string key);

    /// <summary>Writes a <c>localStorage</c> value.</summary>
    /// <param name="key">The raw key. The library's storage prefix is <b>not</b> applied.</param>
    /// <param name="value">The value.</param>
    /// <returns>A task that completes once the write is attempted.</returns>
    Task SetItemAsync(string key, string value);

    /// <summary>Asks the browser for permission to show desktop notifications.</summary>
    /// <returns><see langword="true"/> if permission was granted.</returns>
    /// <remarks>Must be called from a user gesture, or browsers refuse it outright.</remarks>
    Task<bool> RequestNotifyAsync();

    /// <summary>Shows a desktop notification, if permission was granted.</summary>
    /// <param name="title">The notification's title.</param>
    /// <param name="body">Optional detail.</param>
    /// <returns>A task that completes once it is requested. Best effort.</returns>
    Task NotifyAsync(string title, string? body = null);

    /// <summary>Plays a short attention tone.</summary>
    /// <returns>A task that completes once it is requested. Best effort; ships no asset.</returns>
    Task PingAsync();
}

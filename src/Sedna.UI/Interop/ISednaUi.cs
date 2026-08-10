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
/// Two parts of the JavaScript surface have no member here, because neither can
/// cross the boundary. <c>toast()</c> returns a function that removes that toast
/// early. <c>tips.gate</c> is a predicate an app assigns to suppress hover hints
/// conditionally. Both stay JavaScript.
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
    /// <returns>A task that completes once the toast is on the page.</returns>
    /// <remarks>
    /// Anything the user must act on is an <c>.alert</c>, not a toast — a toast
    /// carrying a required action is an action nobody performs.
    /// </remarks>
    Task ToastAsync(
        string message,
        ToastKind kind = ToastKind.Info,
        string? title = null,
        int timeoutMs = 4000,
        bool dismissible = true);

    /// <summary>
    /// Asks the user to confirm, and waits for the answer.
    /// </summary>
    /// <param name="title">The question.</param>
    /// <param name="message">Optional detail: what will happen, and to what.</param>
    /// <param name="confirmLabel">The confirming button's label.</param>
    /// <param name="cancelLabel">The cancelling button's label.</param>
    /// <param name="danger">
    /// Reddens the confirming button and focuses cancel instead, for a destructive
    /// action.
    /// </param>
    /// <returns><see langword="true"/> if confirmed; false if cancelled or dismissed.</returns>
    /// <remarks>
    /// Built on <c>&lt;dialog&gt;.showModal()</c>, so it does not block the circuit
    /// the way <c>window.confirm()</c> does.
    /// </remarks>
    Task<bool> ConfirmAsync(
        string title,
        string? message = null,
        string confirmLabel = "Confirm",
        string cancelLabel = "Cancel",
        bool danger = false);

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
    /// <c>input</c> event, and nothing here calls back into .NET.
    /// </para>
    /// </remarks>
    Task InitMarkdownAsync();

    /// <summary>Reads the stored theme, colour-blind, density, direction and language settings.</summary>
    /// <returns>The settings currently applied.</returns>
    Task<SednaUiSettings> LoadSettingsAsync();

    /// <summary>
    /// Stores one setting and applies it immediately.
    /// </summary>
    /// <param name="key">One of <c>theme</c>, <c>cvd</c>, <c>density</c>, <c>lang</c>.</param>
    /// <param name="value">
    /// <c>theme</c> takes <c>dark</c> or <c>light</c>; <c>cvd</c> takes <c>1</c> or
    /// anything else for off; <c>density</c> takes <c>compact</c> or anything else
    /// for the default; <c>lang</c> takes a two-letter code.
    /// </param>
    /// <returns>A task that completes once the setting is stored and applied.</returns>
    Task SaveSettingAsync(string key, string value);

    /// <summary>Pushes the configured options to the browser.</summary>
    /// <returns>A task that completes once the options are applied.</returns>
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

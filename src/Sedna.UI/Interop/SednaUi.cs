using Microsoft.JSInterop;

namespace Sedna.UI;

/// <summary>
/// The default <see cref="ISednaUi"/>, calling the <c>sednaUi</c> global
/// through <see cref="IJSRuntime"/>.
/// </summary>
/// <remarks>
/// Registered by <c>AddSednaUi</c>. Every method is a thin, untyped call onto
/// the shipped script: this class holds no state and no logic beyond mapping
/// arguments, so the browser stays the single implementation of every behaviour.
/// </remarks>
public sealed class SednaUi : ISednaUi
{
    private readonly IJSRuntime _js;
    private readonly SednaUiOptions _options;

    /// <summary>Creates the service.</summary>
    /// <param name="jsRuntime">The app's JavaScript runtime.</param>
    /// <param name="options">The options to push in <see cref="ConfigureAsync"/>.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public SednaUi(IJSRuntime jsRuntime, SednaUiOptions options)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        ArgumentNullException.ThrowIfNull(options);

        _js = jsRuntime;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<SednaToast> ToastAsync(
        string message,
        ToastKind kind = ToastKind.Info,
        string? title = null,
        int timeoutMs = 4000,
        bool dismissible = true,
        string? dismissLabel = null)
    {
        var id = await _js.InvokeAsync<int>(
            "sednaUi.toast.show", message, ToastOptions(kind, title, timeoutMs, dismissible, dismissLabel));
        return new SednaToast(_js, id, dismissLabel);
    }

    /// <inheritdoc />
    public Task SetTipsEnabledAsync(bool enabled)
        => _js.InvokeVoidAsync("sednaUi.tips.setEnabled", enabled).AsTask();

    /// <summary>The options object the script's toast reads, shared with <see cref="SednaToast"/>.</summary>
    /// <remarks>
    /// <c>dismissLabel</c> is left out when unset rather than sent as null, so the script falls
    /// back to the configured label instead of an empty name.
    /// </remarks>
    internal static Dictionary<string, object?> ToastOptions(
        ToastKind kind, string? title, int timeoutMs, bool dismissible, string? dismissLabel)
    {
        var options = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["kind"] = Name(kind),
            ["title"] = title,
            ["timeout"] = timeoutMs,
            ["dismissible"] = dismissible,
        };
        if (!string.IsNullOrWhiteSpace(dismissLabel)) options["dismissLabel"] = dismissLabel;
        return options;
    }

    /// <inheritdoc />
    public Task<bool> CopyTextAsync(string text)
        => _js.InvokeAsync<bool>("sednaUi.copyText", text).AsTask();

    /// <inheritdoc />
    public Task InitMarkdownAsync()
        => _js.InvokeVoidAsync("sednaUi.md.init").AsTask();

    /// <inheritdoc />
    public Task<SednaUiSettings> LoadSettingsAsync()
        => _js.InvokeAsync<SednaUiSettings>("sednaUi.settings.load").AsTask();

    /// <inheritdoc />
    public Task SaveSettingAsync(string key, string value)
        => _js.InvokeVoidAsync("sednaUi.settings.save", key, value).AsTask();

    /// <inheritdoc />
    public Task ConfigureAsync()
        => _js.InvokeVoidAsync(
            "sednaUi.configure",
            new
            {
                storagePrefix = _options.StoragePrefix,
                notifyIcon = _options.NotifyIcon,
                langCookie = _options.LangCookie,
                // The theme that applies with nothing stored. It is SednaUiOptions.Default
                // rather than a literal because that is the theme SednaUiBrand.ToCss emits at
                // bare :root; the script would otherwise stamp data-theme="sedna" over an app
                // whose default is its own theme, and select a registered Sedna palette — or
                // name a theme nobody registered — on every first visit.
                themeDefault = _options.Default,
                toastDismissLabel = _options.ToastDismissLabel,
            }).AsTask();

    /// <inheritdoc />
    public Task RegisterCommandsAsync(IReadOnlyList<PaletteCommand> commands)
        => _js.InvokeVoidAsync("sednaUi.palette.register", commands).AsTask();

    /// <inheritdoc />
    public async Task<IReadOnlyList<PaletteCommand>> RankCommandsAsync(string query)
        => await _js.InvokeAsync<PaletteCommand[]>("sednaUi.palette.rank", query)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task OpenPaletteAsync() => _js.InvokeVoidAsync("sednaUi.palette.open").AsTask();

    /// <inheritdoc />
    public Task ClosePaletteAsync() => _js.InvokeVoidAsync("sednaUi.palette.close").AsTask();

    /// <inheritdoc />
    public Task RegisterSearchAsync(IReadOnlyList<SearchItem> items)
        => _js.InvokeVoidAsync("sednaUi.search.register", items).AsTask();

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchItem>> RankSearchAsync(string query)
        => await _js.InvokeAsync<SearchItem[]>("sednaUi.search.rank", query)
            .ConfigureAwait(false);

    /// <inheritdoc />
    public Task CloseSearchAsync() => _js.InvokeVoidAsync("sednaUi.search.close").AsTask();

    /// <inheritdoc />
    public Task HideTipsAsync() => _js.InvokeVoidAsync("sednaUi.tips.hide").AsTask();

    /// <inheritdoc />
    public Task CloseMenusAsync() => _js.InvokeVoidAsync("sednaUi.menu.closeAll").AsTask();

    /// <inheritdoc />
    public Task ScrollPageTopAsync() => _js.InvokeVoidAsync("sednaUi.scrollPageTop").AsTask();

    /// <inheritdoc />
    public Task SelectTabAsync(string tabOrPanelId)
        => _js.InvokeVoidAsync("sednaUi.tabs.select", tabOrPanelId).AsTask();

    /// <inheritdoc />
    public Task OpenTabAsync(string url) => _js.InvokeVoidAsync("sednaUi.openTab", url).AsTask();

    /// <inheritdoc />
    public async Task<string?> ShowModalAsync(string elementId, CancellationToken cancellationToken = default)
    {
        try
        {
            // The token overload, always: it is the one Blazor does not wrap in its
            // default one-minute timeout, and this call lasts as long as the reader takes.
            return await _js.InvokeAsync<string?>("sednaUi.modal.show", cancellationToken, [elementId])
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A caller that stopped waiting must not leave the dialog open with nobody
            // listening for its answer.
            try { await CloseModalAsync(elementId).ConfigureAwait(false); }
            catch (JSDisconnectedException) { /* the circuit is gone, and the dialog with it */ }
            throw;
        }
    }

    /// <inheritdoc />
    public Task CloseModalAsync(string elementId, string? returnValue = null)
        => _js.InvokeVoidAsync("sednaUi.modal.close", elementId, returnValue).AsTask();

    /// <inheritdoc />
    public async Task<SednaSpotlight> FollowSpotlightAsync(string hole, string target, SpotlightOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hole);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        var step = await _js.InvokeAsync<IJSObjectReference>(
            "sednaUi.spotlight.follow", hole, target, (options ?? new SpotlightOptions()).ToScript());
        return new SednaSpotlight(step);
    }

    /// <inheritdoc />
    public Task<int> ViewportWidthAsync()
        => _js.InvokeAsync<int>("sednaUi.viewportWidth").AsTask();

    /// <inheritdoc />
    public Task<string?> GetTimeZoneAsync()
        => _js.InvokeAsync<string?>("sednaUi.timeZone").AsTask();

    /// <inheritdoc />
    public Task<string?> GetItemAsync(string key)
        => _js.InvokeAsync<string?>("sednaUi.getItem", key).AsTask();

    /// <inheritdoc />
    public Task SetItemAsync(string key, string value)
        => _js.InvokeVoidAsync("sednaUi.setItem", key, value).AsTask();

    /// <inheritdoc />
    public Task<bool> RequestNotifyAsync()
        => _js.InvokeAsync<bool>("sednaUi.requestNotify").AsTask();

    /// <inheritdoc />
    public Task NotifyAsync(string title, string? body = null)
        => _js.InvokeVoidAsync("sednaUi.notify", title, body).AsTask();

    /// <inheritdoc />
    public Task PingAsync() => _js.InvokeVoidAsync("sednaUi.ping").AsTask();

    // The script's own vocabulary, which is also the CSS modifier suffix
    // (.toast-go, .toast-danger). Mapped explicitly rather than lower-casing the
    // enum name, so renaming a member here cannot silently change a CSS class.
    private static string Name(ToastKind kind) => kind switch
    {
        ToastKind.Go => "go",
        ToastKind.Warn => "warn",
        ToastKind.Danger => "danger",
        _ => "info",
    };
}

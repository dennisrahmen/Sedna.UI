using Microsoft.JSInterop;

namespace Sedna.UI;

/// <summary>
/// <see cref="ISednaSettings"/> over <c>sednaUi.settings</c>.
/// </summary>
/// <remarks>
/// The state here is a cache of what the browser applied, filled by
/// <see cref="SettingsChanged"/> — which JavaScript calls on every
/// <c>sednaUi.settings.apply()</c>, including the ones this class's own setters cause. That is
/// deliberate: one writer for the document, one path into the state, and a setter that fails in
/// the browser therefore cannot leave C# believing it succeeded.
/// </remarks>
internal sealed class SednaSettings : ISednaSettings, IAsyncDisposable
{
    private readonly ISednaUi _ui;
    private readonly IJSRuntime _js;

    private DotNetObjectReference<SednaSettings>? _self;
    private int _watcher;
    private bool _starting;

    public SednaSettings(ISednaUi ui, IJSRuntime jsRuntime, SednaUiOptions options)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _js = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
        ArgumentNullException.ThrowIfNull(options);

        // The theme with nothing stored is the app's default, not the built-in name: until
        // StartAsync this is what a settings UI draws from, and reporting "sedna" to an app
        // whose default is its own theme shows the wrong option selected for one frame.
        Current = new SednaUiSettings { Theme = options.Default };
    }

    public SednaUiSettings Current { get; private set; }

    public bool IsLive { get; private set; }

    public event Action<SednaUiSettings>? Changed;

    public async Task StartAsync()
    {
        // Idempotent in both directions: already watching, or a second call arriving while the
        // first is still awaiting interop. A component calling this from OnAfterRenderAsync and
        // a layout doing the same is the ordinary case, not a misuse.
        if (_watcher != 0 || _starting) return;
        _starting = true;

        try
        {
            Current = await _ui.LoadSettingsAsync();
            IsLive = true;

            _self = DotNetObjectReference.Create(this);
            _watcher = await _js.InvokeAsync<int>("sednaUi.watchSettings", _self);

            // After the watcher, so a change arriving between the read and the subscription is
            // not announced as if it were the initial state — and announced at all, because a
            // component that subscribed before calling this has not yet seen a single value.
            Changed?.Invoke(Current);
        }
        finally
        {
            _starting = false;
        }
    }

    /// <summary>Called from JavaScript whenever the applied settings change.</summary>
    /// <remarks>
    /// Public because <c>[JSInvokable]</c> requires it. Not part of
    /// <see cref="ISednaSettings"/>: an app has no reason to call this, and one that did would be
    /// announcing a change the browser has not made.
    /// </remarks>
    [JSInvokable]
    public void SettingsChanged(SednaUiSettings settings)
    {
        if (settings is null) return;

        Current = settings;
        IsLive = true;
        Changed?.Invoke(settings);
    }

    public Task SetThemeAsync(string theme)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(theme);
        return _ui.SaveSettingAsync("theme", theme);
    }

    public Task SetVariantAsync(string variant)
    {
        // Rejected rather than coerced. A typo silently falling back to dark is the failure this
        // exists to prevent: the reader's stored choice would be overwritten by the mistake.
        if (variant is not ("dark" or "light" or "system"))
            throw new ArgumentException(
                $"\"{variant}\" is not a variant. Use \"dark\", \"light\" or \"system\".",
                nameof(variant));

        return _ui.SaveSettingAsync("variant", variant);
    }

    public Task SetCompactAsync(bool compact) =>
        _ui.SaveSettingAsync("density", compact ? "compact" : "comfortable");

    public Task SetColourBlindAsync(bool colourBlind) =>
        _ui.SaveSettingAsync("cvd", colourBlind ? "1" : "0");

    public Task SetLanguageAsync(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        return _ui.SaveSettingAsync("lang", language);
    }

    public Task SetDirectionAsync(string direction)
    {
        if (direction is not ("ltr" or "rtl"))
            throw new ArgumentException(
                $"\"{direction}\" is not a direction. Use \"ltr\" or \"rtl\".", nameof(direction));

        return _ui.SaveSettingAsync("dir", direction);
    }

    /// <summary>Stops listening and releases the reference JavaScript holds.</summary>
    /// <remarks>
    /// Both halves are guarded: disposal runs when a circuit ends, and by then the browser may
    /// already be gone — a JSDisconnectedException there is the normal case, not a fault worth
    /// propagating out of a Dispose.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_watcher != 0)
        {
            try
            {
                await _js.InvokeVoidAsync("sednaUi.unwatchSettings", _watcher);
            }
            catch (JSDisconnectedException) { /* the circuit is gone; so is the listener */ }
            catch (TaskCanceledException) { /* shutting down mid-call */ }

            _watcher = 0;
        }

        _self?.Dispose();
        _self = null;
    }
}

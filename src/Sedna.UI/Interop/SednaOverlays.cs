using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sedna.UI;

/// <summary>
/// <see cref="ISednaOverlays"/>: the list of open overlays, which
/// <see cref="SednaOverlayHost"/> renders, and the wait on each.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here holds a <see cref="DotNetObjectReference"/> or is called from the script.
/// Every step is .NET calling JavaScript and awaiting it — render, open, wait for the close,
/// wait for the closing transition, remove — which is the whole of the interop boundary this
/// needs.
/// </para>
/// <para>
/// No <c>ConfigureAwait(false)</c>: every continuation mutates the list the host enumerates
/// while rendering, so it has to stay on the renderer's synchronisation context.
/// </para>
/// </remarks>
internal sealed class SednaOverlays : ISednaOverlays
{
    private readonly ISednaUi _ui;
    private readonly IJSRuntime _js;
    private readonly List<Entry> _open = [];
    private int _next;

    public SednaOverlays(ISednaUi ui, IJSRuntime jsRuntime)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        _js = jsRuntime ?? throw new ArgumentNullException(nameof(jsRuntime));
    }

    /// <summary>The host rendering the list, or null before one has rendered.</summary>
    internal object? Host { get; private set; }

    /// <summary>Raised when an overlay is added or removed, so the host re-renders.</summary>
    internal event Func<Task>? Changed;

    /// <summary>The overlays to render, oldest first — which is the stacking order too.</summary>
    internal IReadOnlyList<Entry> Open => _open;

    internal bool Attach(object host)
    {
        if (Host is not null) return ReferenceEquals(Host, host);
        Host = host;
        return true;
    }

    internal void Detach(object host)
    {
        if (!ReferenceEquals(Host, host)) return;
        Host = null;

        // The layout is going away, and every overlay with it. A caller still awaiting
        // gets "cancelled" rather than a call that never completes.
        foreach (var entry in _open) entry.Rendered.TrySetResult(false);
        _open.Clear();
    }

    /// <summary>Called by the host after a render that included <paramref name="entry"/>.</summary>
    internal static void MarkRendered(Entry entry) => entry.Rendered.TrySetResult(true);

    public async Task<TResult?> ShowAsync<TComponent, TResult>(
        Dictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent
    {
        var (entry, returnValue) = await PresentAsync(typeof(TComponent), parameters, cancellationToken);

        if (entry.Overlay.HasResult)
        {
            return entry.Overlay.Result switch
            {
                null => default,
                TResult typed => typed,
                var other => throw new InvalidOperationException(
                    $"{typeof(TComponent).Name} closed with a {other.GetType().Name}, " +
                    $"but ShowAsync was asked for a {typeof(TResult).Name}."),
            };
        }

        // A <form method="dialog"> button's value is a string, so a caller asking for one
        // gets it without the component needing any C# to close.
        return typeof(TResult) == typeof(string) && returnValue is TResult text
            ? text
            : default;
    }

    public async Task ShowAsync<TComponent>(
        Dictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent
        => await PresentAsync(typeof(TComponent), parameters, cancellationToken);

    private async Task<(Entry Entry, string? ReturnValue)> PresentAsync(
        Type component,
        Dictionary<string, object?>? parameters,
        CancellationToken cancellationToken)
    {
        if (Host is null)
        {
            throw new InvalidOperationException(
                $"No SednaOverlayHost is rendered, so {component.Name} would never be shown. " +
                "Place <SednaOverlayHost /> once in the layout, inside the interactive render tree.");
        }

        var id = $"sedna-overlay-{++_next}";
        var entry = new Entry(component, parameters, new SednaOverlay(id, _ui));

        _open.Add(entry);
        try
        {
            await NotifyAsync();

            // False when the host went away before the component ever rendered.
            if (!await entry.Rendered.Task.WaitAsync(cancellationToken))
                return (entry, null);

            var returnValue = await _ui.ShowModalAsync(id, cancellationToken);

            // Removing a drawer mid-slide cuts it off where it stands, so wait for the
            // closing transition before the component leaves the document.
            try { await _js.InvokeVoidAsync("sednaUi.modal.idle", id); }
            catch (JSDisconnectedException) { /* nothing left to animate */ }

            return (entry, returnValue);
        }
        finally
        {
            if (_open.Remove(entry)) await NotifyAsync();
        }
    }

    private Task NotifyAsync() => Changed?.Invoke() ?? Task.CompletedTask;

    internal sealed class Entry(Type component, Dictionary<string, object?>? parameters, SednaOverlay overlay)
    {
        public Type Component { get; } = component;

        /// <summary>
        /// Built once: a new dictionary on every host render would count as a parameter
        /// change and re-render every open overlay whenever another one opened.
        /// </summary>
        public IDictionary<string, object>? Parameters { get; } =
            parameters?.ToDictionary(p => p.Key, p => p.Value!);

        public SednaOverlay Overlay { get; } = overlay;

        /// <summary>True once rendered; false when the host was removed first.</summary>
        public TaskCompletionSource<bool> Rendered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

using Microsoft.JSInterop;

namespace Sedna.UI;

/// <summary>
/// The default <see cref="ISednaGraphs"/>, calling <c>sednaUi.graph.invoke</c> through
/// <see cref="IJSRuntime"/>.
/// </summary>
/// <remarks>
/// Registered by <c>AddSednaUi</c>. Every method is one call to the bridge with the graph's id, a
/// method name and its arguments: the browser is the single implementation of every behaviour,
/// and a graph that is not there answers null rather than throwing into the circuit.
/// </remarks>
public sealed class SednaGraphs : ISednaGraphs
{
    private const string Bridge = "sednaUi.graph.invoke";
    private readonly IJSRuntime _js;

    /// <summary>Creates the service.</summary>
    /// <param name="jsRuntime">The app's JavaScript runtime.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jsRuntime"/> is null.</exception>
    public SednaGraphs(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        _js = jsRuntime;
    }

    private ValueTask<T> Call<T>(string graphId, string method, CancellationToken cancellationToken, params object?[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphId);
        return _js.InvokeAsync<T>(Bridge, cancellationToken, [graphId, method, args]);
    }

    /// <inheritdoc />
    public async Task<SednaGraphStats?> SetDataAsync(string graphId, SednaGraphData data, bool relayout = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return await Call<SednaGraphStats?>(graphId, "set", cancellationToken, data, new { relayout });
    }

    /// <inheritdoc />
    public async Task<SednaGraphStats?> FilterAsync(string graphId, SednaGraphFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);
        return await Call<SednaGraphStats?>(graphId, "filter", cancellationToken, filter);
    }

    /// <inheritdoc />
    public async Task<SednaGraphStats?> SearchAsync(string graphId, string? text, CancellationToken cancellationToken = default)
        => await Call<SednaGraphStats?>(graphId, "search", cancellationToken, text ?? string.Empty);

    /// <inheritdoc />
    public async Task SelectAsync(string graphId, string? nodeId, CancellationToken cancellationToken = default)
        => await Call<SednaGraphStats?>(graphId, "select", cancellationToken, nodeId);

    /// <inheritdoc />
    public async Task<SednaGraphStats?> FocusAsync(string graphId, string? nodeId, int depth = 1,
        CancellationToken cancellationToken = default)
        => await Call<SednaGraphStats?>(graphId, "focus", cancellationToken, nodeId, Math.Clamp(depth, 1, 6));

    /// <inheritdoc />
    public async Task LayoutAsync(string graphId, string? layout = null, string? direction = null,
        CancellationToken cancellationToken = default)
        => await Call<SednaGraphStats?>(graphId, "layout", cancellationToken, layout, new { direction });

    /// <inheritdoc />
    public async Task SetOptionAsync(string graphId, string option, string value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(option);
        await Call<SednaGraphStats?>(graphId, "option", cancellationToken, option, value);
    }

    /// <inheritdoc />
    public async Task FitAsync(string graphId, CancellationToken cancellationToken = default)
        => await Call<object?>(graphId, "fit", cancellationToken);

    /// <inheritdoc />
    public async Task ZoomAsync(string graphId, double factor, CancellationToken cancellationToken = default)
    {
        if (!(factor > 0) || double.IsInfinity(factor))
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "A zoom factor is a positive number.");
        await Call<object?>(graphId, "zoom", cancellationToken, factor);
    }

    /// <inheritdoc />
    public async Task<string?> ExportAsync(string graphId, SednaGraphExport format = SednaGraphExport.Svg,
        CancellationToken cancellationToken = default)
        => await Call<string?>(graphId, "export", cancellationToken, Name(format));

    /// <inheritdoc />
    public async Task DownloadAsync(string graphId, SednaGraphExport format = SednaGraphExport.Svg, string? fileName = null,
        CancellationToken cancellationToken = default)
        => await Call<object?>(graphId, "download", cancellationToken, Name(format), fileName);

    /// <inheritdoc />
    public async Task<SednaGraphStats?> CollapseAsync(string graphId, string? groupId = null,
        CancellationToken cancellationToken = default)
        => await Call<SednaGraphStats?>(graphId, "collapse", cancellationToken, groupId);

    /// <inheritdoc />
    public async Task<SednaGraphStats?> ExpandAsync(string graphId, string? groupId = null,
        CancellationToken cancellationToken = default)
        => await Call<SednaGraphStats?>(graphId, "expand", cancellationToken, groupId);

    /// <inheritdoc />
    public async Task SetDrawingAsync(string graphId, bool on, CancellationToken cancellationToken = default)
        => await Call<object?>(graphId, "connect", cancellationToken, on);

    /// <inheritdoc />
    public async Task SetHullsAsync(string graphId, bool on, CancellationToken cancellationToken = default)
        => await Call<object?>(graphId, "hulls", cancellationToken, on);

    /// <inheritdoc />
    public async Task<SednaGraphStats?> ReloadAsync(string graphId, CancellationToken cancellationToken = default)
        => await Call<SednaGraphStats?>(graphId, "reload", cancellationToken);

    /// <inheritdoc />
    public async Task<SednaGraphStats?> StatsAsync(string graphId, CancellationToken cancellationToken = default)
        => await Call<SednaGraphStats?>(graphId, "stats", cancellationToken);

    private static string Name(SednaGraphExport format) => format == SednaGraphExport.Png ? "png" : "svg";
}

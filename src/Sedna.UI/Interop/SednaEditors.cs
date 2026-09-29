using Microsoft.JSInterop;

namespace Sedna.UI;

/// <summary>
/// The default <see cref="ISednaEditors"/>, calling <c>sednaUi.editor.invoke</c> through
/// <see cref="IJSRuntime"/>.
/// </summary>
/// <remarks>
/// Registered by <c>AddSednaUi</c>. Every method is one call to the bridge with the editor's id, a
/// method name and its arguments: the browser is the single implementation of every behaviour,
/// and an editor that is not there answers null rather than throwing into the circuit.
/// </remarks>
public sealed class SednaEditors : ISednaEditors
{
    private const string Bridge = "sednaUi.editor.invoke";
    private readonly IJSRuntime _js;

    /// <summary>Creates the service.</summary>
    /// <param name="jsRuntime">The app's JavaScript runtime.</param>
    /// <exception cref="ArgumentNullException"><paramref name="jsRuntime"/> is null.</exception>
    public SednaEditors(IJSRuntime jsRuntime)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        _js = jsRuntime;
    }

    private ValueTask<T> Call<T>(string editorId, string method, CancellationToken cancellationToken, params object?[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(editorId);
        return _js.InvokeAsync<T>(Bridge, cancellationToken, [editorId, method, args]);
    }

    /// <inheritdoc />
    public async Task<SednaEditorState?> GetStateAsync(string editorId, CancellationToken cancellationToken = default)
        => await Call<SednaEditorState?>(editorId, "state", cancellationToken);

    /// <inheritdoc />
    public async Task<SednaEditorState?> SetHtmlAsync(string editorId, string? html, CancellationToken cancellationToken = default)
        => await Call<SednaEditorState?>(editorId, "set", cancellationToken, html ?? string.Empty);

    /// <inheritdoc />
    public async Task<SednaEditorState?> InsertTextAsync(string editorId, string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        return await Call<SednaEditorState?>(editorId, "insert", cancellationToken, text);
    }

    /// <inheritdoc />
    public async Task FocusAsync(string editorId, CancellationToken cancellationToken = default)
        => await Call<object?>(editorId, "focus", cancellationToken);

    /// <inheritdoc />
    public async Task SetEnabledAsync(string editorId, bool enabled, CancellationToken cancellationToken = default)
        => await Call<object?>(editorId, "enable", cancellationToken, enabled);
}

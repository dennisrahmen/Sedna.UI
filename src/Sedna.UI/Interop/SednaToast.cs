using Microsoft.JSInterop;

namespace Sedna.UI;

/// <summary>
/// One toast on the page, from <see cref="ISednaUi.ToastAsync"/> or
/// <see cref="ISednaUi.PresentToastAsync"/>: dismiss it early, replace what it says in place, or
/// wait for how it ended.
/// </summary>
/// <remarks>
/// <para>
/// A handle, because a function does not cross the interop boundary: the script's
/// <c>toast()</c> returns a remover JavaScript can hold, and this holds the id that names the
/// same toast. The pattern it exists for is work that ends later:
/// </para>
/// <code>
/// var toast = await Ui.ToastAsync("Uploading…", timeoutMs: 0);
/// var ok = await UploadAsync();
/// await toast.ReplaceAsync(ok ? "Uploaded" : "Upload failed", ok ? ToastKind.Go : ToastKind.Danger);
/// </code>
/// <para>
/// Nothing here is held in the browser for C#, and nothing calls back: each member is one call
/// the app awaits — <see cref="ResultAsync"/> as long as the toast is on screen. A toast that has already gone — timed out, or closed by the reader — is not
/// an error.
/// </para>
/// </remarks>
public sealed class SednaToast
{
    private readonly IJSRuntime _js;
    private readonly string? _dismissLabel;

    internal SednaToast(IJSRuntime js, int id, string? dismissLabel)
    {
        _js = js;
        Id = id;
        _dismissLabel = dismissLabel;
    }

    /// <summary>
    /// The toast's id in the page. Changes when <see cref="ReplaceAsync"/> had to show a new
    /// toast because this one had already gone.
    /// </summary>
    public int Id { get; private set; }

    /// <summary>Removes the toast now.</summary>
    /// <returns><see langword="false"/> when it had already gone.</returns>
    public Task<bool> DismissAsync() => _js.InvokeAsync<bool>("sednaUi.toast.dismiss", Id).AsTask();

    /// <summary>
    /// Replaces what the toast says, in place, keeping its position in the stack — no flicker
    /// of one toast leaving and another arriving.
    /// </summary>
    /// <param name="message">The new line. Inserted as text, never as markup.</param>
    /// <param name="kind">The new semantic family.</param>
    /// <param name="title">An optional bold first line.</param>
    /// <param name="timeoutMs">How long it now stays, from the moment of replacing. <c>0</c> stays until dismissed.</param>
    /// <param name="dismissible">Whether to render the close button.</param>
    /// <returns>A task that completes once the toast shows the new message.</returns>
    /// <remarks>
    /// When the toast has already gone, a new one is shown instead, because the outcome of the
    /// work still has to reach the reader. <see cref="Id"/> then names the new toast.
    /// </remarks>
    public async Task ReplaceAsync(
        string message,
        ToastKind kind = ToastKind.Info,
        string? title = null,
        int timeoutMs = 4000,
        bool dismissible = true)
    {
        Id = await _js.InvokeAsync<int>(
            "sednaUi.toast.replace",
            Id,
            message,
            SednaUi.ToastOptions(kind, title, timeoutMs, dismissible, _dismissLabel));
    }

    /// <summary>
    /// Replaces the toast with one the app wrote in a <c>&lt;template&gt;</c>, in place, keeping
    /// its position, its id and a <see cref="ResultAsync"/> already waiting on it — a
    /// "+3 more" card that counts up.
    /// </summary>
    /// <param name="templateId">The <c>id</c> of the <c>&lt;template&gt;</c>.</param>
    /// <param name="data">Text for its <c>data-*</c> slots, and <c>href</c> for its <c>[data-href]</c> link.</param>
    /// <param name="timeoutMs">How long it now stays, from the moment of replacing. <c>0</c> stays.</param>
    /// <returns>
    /// <see langword="false"/> when no <c>&lt;template&gt;</c> has that id. When the toast had
    /// already gone, a new one is shown and <see cref="Id"/> names it.
    /// </returns>
    public async Task<bool> ReplaceFromTemplateAsync(
        string templateId, IReadOnlyDictionary<string, string?>? data = null, int timeoutMs = 4000)
    {
        var id = await _js.InvokeAsync<int>(
            "sednaUi.toast.present", templateId, data, SednaUi.PresentOptions(timeoutMs, Id));
        if (id == 0) return false;
        Id = id;
        return true;
    }

    /// <summary>
    /// Waits for the toast to leave and says how: the <c>data-toast-value</c> of the control the
    /// reader pressed in a toast the app wrote — <c>"open"</c>, <c>"mark-read"</c> — or
    /// <see langword="null"/> when it was closed, dismissed or timed out.
    /// </summary>
    /// <param name="cancellationToken">
    /// Stops waiting; the toast stays. Passed through to the interop call, which is what exempts
    /// it from Blazor's one-minute interop timeout, so a toast that stays until the reader deals
    /// with it can be awaited for as long as that takes.
    /// </param>
    /// <returns>The value, or <see langword="null"/>. A toast that has already gone answers at once.</returns>
    public Task<string?> ResultAsync(CancellationToken cancellationToken = default)
        => _js.InvokeAsync<string?>("sednaUi.toast.result", cancellationToken, [Id]).AsTask();
}

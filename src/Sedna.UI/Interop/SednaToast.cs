using Microsoft.JSInterop;

namespace Sedna.UI;

/// <summary>
/// One toast on the page, from <see cref="ISednaUi.ToastAsync"/>: dismiss it early, or replace
/// what it says in place.
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
/// the app awaits. A toast that has already gone — timed out, or closed by the reader — is not
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
}

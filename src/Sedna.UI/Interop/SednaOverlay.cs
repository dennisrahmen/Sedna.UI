namespace Sedna.UI;

/// <summary>
/// One overlay presented by <see cref="ISednaOverlays"/>, as its component sees it: the id
/// its <c>&lt;dialog&gt;</c> carries, and the two ways to close it.
/// </summary>
/// <remarks>
/// Cascaded to the component by <see cref="SednaOverlayHost"/>. Take it with
/// <c>[CascadingParameter] public SednaOverlay Overlay { get; set; } = default!;</c>.
/// </remarks>
public sealed class SednaOverlay
{
    private readonly ISednaUi _ui;

    internal SednaOverlay(string id, ISednaUi ui)
    {
        Id = id;
        _ui = ui;
    }

    /// <summary>
    /// The id to put on the component's <c>&lt;dialog&gt;</c>, and the prefix for any other
    /// id it needs — <c>@($"{Overlay.Id}-title")</c> for <c>aria-labelledby</c>.
    /// </summary>
    public string Id { get; }

    internal bool HasResult { get; private set; }

    internal object? Result { get; private set; }

    /// <summary>Closes the dialog, and completes the waiting call with <paramref name="result"/>.</summary>
    /// <param name="result">What <see cref="ISednaOverlays.ShowAsync{TComponent, TResult}"/> returns.</param>
    /// <returns>A task that completes once the dialog is closed.</returns>
    /// <remarks>
    /// The first call wins: a second close — a double click, or Escape arriving while the
    /// work behind a button finishes — does not replace the result.
    /// </remarks>
    public Task CloseAsync(object? result)
    {
        if (!HasResult)
        {
            HasResult = true;
            Result = result;
        }
        return _ui.CloseModalAsync(Id);
    }

    /// <summary>Closes the dialog with no result, as <kbd>Escape</kbd> does.</summary>
    /// <returns>A task that completes once the dialog is closed.</returns>
    public Task CancelAsync() => _ui.CloseModalAsync(Id);
}

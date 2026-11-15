namespace Sedna.UI;

/// <summary>
/// The choice in a <c>.listbox</c> changed — <c>sedna-listbox-select</c>, dispatched on the listbox,
/// and on the destination of a <c>data-transfer</c> move.
/// </summary>
/// <remarks>
/// The script has already written <c>aria-selected</c> on the app's rows. <see cref="Values"/> is the whole
/// choice afterwards, so an app keeps its own copy by replacing it.
/// </remarks>
public sealed class SednaListboxEventArgs : EventArgs
{
    /// <summary>Every row chosen, by its <c>data-value</c> or else its text, in document order.</summary>
    public string[] Values { get; set; } = [];
}

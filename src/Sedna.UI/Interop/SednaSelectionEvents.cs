namespace Sedna.UI;

/// <summary>
/// A control in a selection toolbar was pressed: <c>sedna-selection</c>, dispatched on the
/// toolbar after the reader selected text inside the element whose <c>data-selection-toolbar</c>
/// names it, and pressed one of its <c>[data-selection-action]</c> controls.
/// </summary>
/// <remarks>
/// The toolbar has closed by the time it fires, and the selection is left as it was. What the
/// action does — quote the text into the composer, copy it, ask about it — is the app's.
/// </remarks>
public sealed class SednaSelectionEventArgs : EventArgs
{
    /// <summary>The pressed control's <c>data-selection-action</c>, e.g. <c>quote</c>.</summary>
    public string Action { get; set; } = "";

    /// <summary>The selected text, as the browser reads it out of the selection.</summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// The <c>data-selection-item</c> of the element both ends of the selection sit in — a
    /// message's id — or null when the selection is in none, or spans two.
    /// </summary>
    public string? Item { get; set; }

    /// <summary>The id of the element carrying <c>data-selection-toolbar</c>, or null when it has none.</summary>
    public string? Source { get; set; }
}

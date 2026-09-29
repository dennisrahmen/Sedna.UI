namespace Sedna.UI;

/// <summary>What a rich-text editor's document holds, as <see cref="ISednaEditors"/> reads it.</summary>
/// <param name="Html">The document as HTML — the textarea's value — or the empty string for an empty document.</param>
/// <param name="Text">The document as plain text.</param>
/// <param name="Length">How many characters <paramref name="Text"/> has.</param>
/// <param name="IsEmpty">Whether the document is empty.</param>
public sealed record SednaEditorState(string Html, string Text, int Length, bool IsEmpty);

/// <summary>
/// An editor's document: <c>sedna-editor-ready</c>, once it has loaded, and
/// <c>sedna-editor-change</c>, after every change.
/// </summary>
/// <remarks>
/// A binding to the textarea — <c>@bind</c> — already has the value; this is for a page that wants
/// the text or the length as well, a counter or a save button that waits for something to save.
/// </remarks>
public sealed class SednaEditorEventArgs : EventArgs
{
    /// <summary>The document as HTML, or the empty string for an empty document.</summary>
    public string Html { get; set; } = string.Empty;

    /// <summary>The document as plain text.</summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>How many characters <see cref="Text"/> has.</summary>
    public int Length { get; set; }

    /// <summary>Whether the document is empty.</summary>
    public bool IsEmpty { get; set; }
}

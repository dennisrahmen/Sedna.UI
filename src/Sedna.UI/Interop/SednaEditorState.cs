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

/// <summary>
/// An image or a video control was used: <c>sedna-editor-media</c>, sent before the editor opens the
/// app's media form.
/// </summary>
/// <remarks>
/// An app that opens its own dialog instead — one with an upload in it — cancels the event with
/// <c>@onsedna-editor-media:preventDefault</c>, and answers with
/// <see cref="ISednaEditors.InsertImageAsync"/> or <see cref="ISednaEditors.InsertVideoAsync"/>, which
/// put the media where the caret was.
/// </remarks>
public sealed class SednaEditorMediaEventArgs : EventArgs
{
    /// <summary><c>image</c> or <c>video</c>.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>The address of the image or video the caret is on, or the empty string for a new one.</summary>
    public string Src { get; set; } = string.Empty;

    /// <summary>Its description — an image's <c>alt</c>, a video's <c>aria-label</c> — or the empty string.</summary>
    public string Alt { get; set; } = string.Empty;
}

/// <summary>
/// Files pasted or dropped into a rich-text editor: <c>sedna-editor-file</c>, dispatched on the
/// <c>[data-editor]</c> element.
/// </summary>
/// <remarks>
/// The editor never inlines a file — uploading is the app's. Read each one with
/// <see cref="ISednaEditors.OpenFileAsync"/>, upload it, and answer with
/// <see cref="ISednaEditors.InsertImageAsync"/> or <see cref="ISednaEditors.InsertVideoAsync"/>,
/// which put it where the caret was. The event is cancelable, and cancelling it —
/// <c>@onsedna-editor-file:preventDefault</c> — says the app took the files; files that are not
/// taken show the editor's <c>[data-editor-file-notice]</c>, when it has one.
/// </remarks>
public sealed class SednaEditorFileEventArgs : EventArgs
{
    /// <summary>Every file that arrived, in order.</summary>
    public SednaEditorFile[] Files { get; set; } = [];

    /// <summary>Where in the document they arrived, or null when the editor had no caret.</summary>
    public int? Index { get; set; }
}

/// <summary>One file pasted or dropped into an editor, as <see cref="SednaEditorFileEventArgs"/> names it.</summary>
public sealed class SednaEditorFile
{
    /// <summary>What <see cref="ISednaEditors.OpenFileAsync"/> takes to read it.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>The file's name, or the empty string — a pasted screenshot often has none.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Its media type, such as <c>image/png</c>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>Its size in bytes.</summary>
    public long Size { get; set; }
}

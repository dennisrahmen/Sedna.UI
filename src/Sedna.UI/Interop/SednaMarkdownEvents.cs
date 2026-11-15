namespace Sedna.UI;

/// <summary>
/// The data of <c>sedna-md-file</c>: files pasted or dropped on a Markdown editor's textarea.
/// </summary>
/// <remarks>
/// Nothing is inserted. Read each file by its id with
/// <see cref="ISednaUi.OpenMarkdownFileAsync"/>, upload it, and answer with
/// <see cref="ISednaUi.InsertMarkdownAsync"/>. <c>@onsedna-md-file:preventDefault</c> says the
/// app took the files; files that are not taken show the editor's <c>[data-md-file-notice]</c>,
/// when it has one.
/// </remarks>
public sealed class SednaMarkdownFileEventArgs : EventArgs
{
    /// <summary>Every file that arrived, in order.</summary>
    public SednaEditorFile[] Files { get; set; } = [];

    /// <summary>Where in the text the caret was when they arrived.</summary>
    public int? Index { get; set; }
}

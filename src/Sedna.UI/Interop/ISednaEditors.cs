namespace Sedna.UI;

/// <summary>
/// Typed access to the rich-text editors on a page — <c>sednaUi.editor</c> — by the id of their
/// <c>[data-editor]</c> element.
/// </summary>
/// <remarks>
/// <para>
/// The editor is the app's markup: a <c>&lt;div class="editor" data-editor id="…"&gt;</c> with its
/// toolbar, an <c>.editor-body</c> and a <c>&lt;textarea data-editor-value&gt;</c> — see the catalogue's
/// Rich-text editor page. The value is that textarea's, as HTML, so it binds with
/// <c>@bind</c> like any other field and needs nothing here. This service is for what a binding
/// cannot do; every method takes the editor element's id and does nothing, with a console warning,
/// when there is none.
/// </para>
/// <para>
/// Call it from <c>OnAfterRenderAsync</c>, once the element exists. An editor that has not started yet
/// is started by the call. What the reader does comes back as the textarea's own <c>input</c> and
/// <c>change</c>, and as <c>@onsedna-editor-change</c> and <c>@onsedna-editor-ready</c> on the
/// editor, with <c>@using Sedna.UI</c>. Nothing here holds a <c>DotNetObjectReference</c>.
/// </para>
/// </remarks>
public interface ISednaEditors
{
    /// <summary>What the document holds now.</summary>
    /// <param name="editorId">The <c>[data-editor]</c> element's id.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The document as HTML and as text, or null when there is no such editor.</returns>
    Task<SednaEditorState?> GetStateAsync(string editorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the document, as a paste of this HTML would — cut down to the formats the editor
    /// accepts — and writes the result to the textarea with its <c>input</c> event, so a binding follows.
    /// Undo takes it back.
    /// </summary>
    /// <returns>What the document holds afterwards, or null when there is no such editor.</returns>
    Task<SednaEditorState?> SetHtmlAsync(string editorId, string? html, CancellationToken cancellationToken = default);

    /// <summary>Types text at the caret — at the end of the document, if it has not had the focus.</summary>
    /// <returns>What the document holds afterwards, or null when there is no such editor.</returns>
    Task<SednaEditorState?> InsertTextAsync(string editorId, string text, CancellationToken cancellationToken = default);

    /// <summary>Puts the focus in the document, where the caret last was.</summary>
    Task FocusAsync(string editorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets the reader edit, or stops them. The editor follows its textarea's <c>disabled</c> and
    /// <c>readonly</c> as well, so a binding to either needs no call.
    /// </summary>
    Task SetEnabledAsync(string editorId, bool enabled, CancellationToken cancellationToken = default);
}

using Microsoft.JSInterop;

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

    /// <summary>
    /// Puts an image where the caret was — or in place of the image it is on — as
    /// <c>&lt;img src alt&gt;</c>: the answer to <c>@onsedna-editor-media</c> once the app has
    /// uploaded the file and has its address.
    /// </summary>
    /// <param name="editorId">The <c>[data-editor]</c> element's id.</param>
    /// <param name="url">The image's address: <c>http</c>, <c>https</c> or relative.</param>
    /// <param name="alt">Its description, written as <c>alt</c>; null or empty for none.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>
    /// What the document holds afterwards, or null — and nothing inserted — when there is no such
    /// editor, the editor does not take images, or the address is not one it keeps.
    /// </returns>
    Task<SednaEditorState?> InsertImageAsync(string editorId, string url, string? alt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts an image with a size where the caret was — or in place of the image it is on — as
    /// <c>&lt;img src alt width height&gt;</c>. The overload without a size keeps the size of the
    /// image it replaces; this one sets it, and a null size is none.
    /// </summary>
    /// <param name="editorId">The <c>[data-editor]</c> element's id.</param>
    /// <param name="url">The image's address: <c>http</c>, <c>https</c> or relative.</param>
    /// <param name="alt">Its description, written as <c>alt</c>; null or empty for none.</param>
    /// <param name="width">Its width in pixels, written as <c>width</c>; null for none, so the browser keeps the aspect ratio.</param>
    /// <param name="height">Its height in pixels, written as <c>height</c>; null for none.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>
    /// What the document holds afterwards, or null — and nothing inserted — when there is no such
    /// editor, the editor does not take images, the address is not one it keeps, or a size is not
    /// above zero.
    /// </returns>
    Task<SednaEditorState?> InsertImageAsync(string editorId, string url, string? alt, int? width, int? height = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a video where the caret was — or in place of the video it is on — as a block of its
    /// own, <c>&lt;video controls src&gt;</c>, which never plays by itself.
    /// </summary>
    /// <param name="editorId">The <c>[data-editor]</c> element's id.</param>
    /// <param name="url">The video file's address: <c>http</c>, <c>https</c> or relative.</param>
    /// <param name="label">Its name, written as <c>aria-label</c>; null or empty for none.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>
    /// What the document holds afterwards, or null — and nothing inserted — when there is no such
    /// editor, the editor does not take videos, or the address is not one it keeps.
    /// </returns>
    Task<SednaEditorState?> InsertVideoAsync(string editorId, string url, string? label = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts a video with a size where the caret was — or in place of the video it is on — as a block of
    /// its own, <c>&lt;video controls src width height&gt;</c>. The overload without a size keeps the size
    /// of the video it replaces; this one sets it, and a null size is none.
    /// </summary>
    /// <param name="editorId">The <c>[data-editor]</c> element's id.</param>
    /// <param name="url">The video file's address: <c>http</c>, <c>https</c> or relative.</param>
    /// <param name="label">Its name, written as <c>aria-label</c>; null or empty for none.</param>
    /// <param name="width">Its width in pixels, written as <c>width</c>; null for none.</param>
    /// <param name="height">Its height in pixels, written as <c>height</c>; null for none.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>
    /// What the document holds afterwards, or null — and nothing inserted — when there is no such
    /// editor, the editor does not take videos, the address is not one it keeps, or a size is not
    /// above zero.
    /// </returns>
    Task<SednaEditorState?> InsertVideoAsync(string editorId, string url, string? label, int? width, int? height = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file pasted or dropped into the editor, by the id its <c>sedna-editor-file</c>
    /// event named, for the app to upload.
    /// </summary>
    /// <param name="editorId">The <c>[data-editor]</c> element's id.</param>
    /// <param name="fileId">An id from <see cref="SednaEditorFileEventArgs.Files"/>.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>
    /// The file's bytes as a stream reference — <c>OpenReadStream(maxAllowedSize)</c> reads them —
    /// or null when there is no such editor or the editor has let the file go; it keeps the last
    /// few that arrived.
    /// </returns>
    Task<IJSStreamReference?> OpenFileAsync(string editorId, string fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fills the editor's mention list — the app's <c>[data-editor-mention]</c> menu — with one row per
    /// entry, cloned from the <c>&lt;template data-editor-mention-item&gt;</c> inside it: the answer to
    /// <c>@onsedna-editor-mention</c>, cancelled, once the app has searched for what the reader typed
    /// after the "@". The editor shows every row it is given. Rows an earlier call made are replaced.
    /// </summary>
    /// <param name="editorId">The <c>[data-editor]</c> element's id.</param>
    /// <param name="mentions">Who can be named; entries with an empty <see cref="SednaMention.Value"/> are skipped.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>How many rows were made — 0 for an editor with no such list or template — or null when there is no such editor.</returns>
    Task<int?> SetMentionsAsync(string editorId, IEnumerable<SednaMention> mentions, CancellationToken cancellationToken = default);

    /// <summary>Puts the focus in the document, where the caret last was.</summary>
    Task FocusAsync(string editorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lets the reader edit, or stops them. The editor follows its textarea's <c>disabled</c> and
    /// <c>readonly</c> as well, so a binding to either needs no call.
    /// </summary>
    Task SetEnabledAsync(string editorId, bool enabled, CancellationToken cancellationToken = default);
}

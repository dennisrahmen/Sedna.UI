namespace Sedna.UI;

/// <summary>
/// A <c>[data-qr]</c> code was saved as a file — <c>sedna-qr-download</c>, dispatched on the code's
/// element once the file has been handed to the browser.
/// </summary>
/// <remarks>
/// A button the app wrote with <c>data-qr-download</c> or the script's <c>sednaUi.qr.download</c> starts
/// a save. Nothing waits on the handler: it is how an app shows "Saved", and nothing more.
/// </remarks>
public sealed class SednaQrDownloadEventArgs : EventArgs
{
    /// <summary>The file's format: <c>svg</c> or <c>png</c>.</summary>
    public string Format { get; set; } = "";

    /// <summary>The picture's side in pixels.</summary>
    public int Size { get; set; }

    /// <summary>The name the file was saved under, with its extension.</summary>
    public string Name { get; set; } = "";
}

/// <summary>
/// A <c>[data-qr]</c> code could not be saved — <c>sedna-qr-error</c>, dispatched on the code's element.
/// </summary>
/// <remarks>
/// The reasons are an unsupported format, a size outside 16 to 8192 pixels, a code with nothing to
/// save, and a logo the page may not read into the file. The message names which, in English; an
/// app that shows one writes its own words.
/// </remarks>
public sealed class SednaQrErrorEventArgs : EventArgs
{
    /// <summary>What went wrong.</summary>
    public string Message { get; set; } = "";
}

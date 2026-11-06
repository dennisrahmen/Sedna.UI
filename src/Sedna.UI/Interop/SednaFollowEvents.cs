namespace Sedna.UI;

/// <summary>
/// An output pane or a workflow diagram started or stopped following what is newest:
/// <c>sedna-follow</c>, dispatched on the pane or the diagram's canvas.
/// </summary>
/// <remarks>
/// It changes when the reader scrolls away from the newest line or step, and again when they
/// come back or press a <c>data-output-follow</c> button — so an app can show "jump to latest"
/// only while it would do something, or count what arrived while the reader was away.
/// </remarks>
public sealed class SednaFollowEventArgs : EventArgs
{
    /// <summary>True when it follows again; false when the reader has scrolled away.</summary>
    public bool Following { get; set; }
}

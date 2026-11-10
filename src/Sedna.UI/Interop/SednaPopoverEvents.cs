namespace Sedna.UI;

/// <summary>
/// A popover that carries <c>data-popover-for</c> is about to open at a trigger, and asks to be
/// filled for it first: <c>sedna-popover-fill</c>, dispatched on the trigger before the panel
/// opens.
/// </summary>
/// <remarks>
/// <para>
/// One panel serves any number of <c>data-popover</c> triggers — a hover card over every
/// appointment in a calendar — and holds the content of one at a time. Render that trigger's
/// content into the panel and set its <c>data-popover-for</c> to <see cref="Trigger"/> in the
/// same render: the panel opens when it reads that id, at the trigger that asked.
/// </para>
/// <para>
/// Nothing waits on the handler and nothing is called back. A request the reader has already
/// left — the pointer gone, another trigger asked for — opens nothing when its render arrives.
/// </para>
/// </remarks>
public sealed class SednaPopoverEventArgs : EventArgs
{
    /// <summary>The id of the trigger the panel is about to open at — the value to write into <c>data-popover-for</c>.</summary>
    public string Trigger { get; set; } = "";

    /// <summary>The id of the panel, or null when it has none.</summary>
    public string? Popover { get; set; }
}

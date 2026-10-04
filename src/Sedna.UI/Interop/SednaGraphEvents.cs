namespace Sedna.UI;

/// <summary>
/// What a graph shows: <c>sedna-graph-ready</c>, once it has first drawn, and
/// <c>sedna-graph-change</c>, after every filter, search, fold and new data.
/// </summary>
public sealed class SednaGraphEventArgs : EventArgs
{
    /// <summary>Records on screen.</summary>
    public int Nodes { get; set; }

    /// <summary>Links on screen.</summary>
    public int Edges { get; set; }

    /// <summary>Records a search has marked.</summary>
    public int Matches { get; set; }

    /// <summary>Records in the data, shown or not.</summary>
    public int TotalNodes { get; set; }

    /// <summary>Links in the data, shown or not.</summary>
    public int TotalEdges { get; set; }

    /// <summary>The selected record's id, or null.</summary>
    public string? Selected { get; set; }
}

/// <summary>
/// A record: selected (<c>sedna-graph-select</c>, with <see cref="Id"/> null when the selection is
/// cleared), opened (<c>sedna-graph-open</c>), pointed at or reached by keyboard
/// (<c>sedna-graph-hover</c>), or a group folded or unfolded (<c>sedna-graph-collapse</c>,
/// <c>sedna-graph-expand</c>).
/// </summary>
/// <remarks>
/// <c>sedna-graph-open</c> follows the record's <see cref="Href"/> afterwards unless it is
/// cancelled — <c>@onsedna-graph-open:preventDefault</c> — so a page that opens a record in a drawer
/// of its own handles the event and stops it.
/// </remarks>
public class SednaGraphNodeEventArgs : EventArgs
{
    /// <summary>The record's id, or null for "none".</summary>
    public string? Id { get; set; }

    /// <summary>Its name.</summary>
    public string? Label { get; set; }

    /// <summary>Its <see cref="SednaGraphNode.Kind"/>.</summary>
    public string? Kind { get; set; }

    /// <summary>Its <see cref="SednaGraphNode.Group"/>.</summary>
    public string? Group { get; set; }

    /// <summary>Its <see cref="SednaGraphNode.Cluster"/>.</summary>
    public string? Cluster { get; set; }

    /// <summary>The group it is drawn inside.</summary>
    public string? Parent { get; set; }

    /// <summary>Its <see cref="SednaGraphNode.Meta"/>.</summary>
    public string? Meta { get; set; }

    /// <summary>Where opening it goes.</summary>
    public string? Href { get; set; }

    /// <summary>Its tone.</summary>
    public string? Tone { get; set; }

    /// <summary>Where a run has got to on it, or null for a step not reached yet.</summary>
    public SednaGraphState? State { get; set; }

    /// <summary>Its tags.</summary>
    public string[] Tags { get; set; } = [];

    /// <summary>Its own fields: every other <c>data-*</c> attribute, or <see cref="SednaGraphNode.Fields"/>.</summary>
    public Dictionary<string, string?> Fields { get; set; } = [];

    /// <summary>True when the reader did it from the keyboard.</summary>
    public bool Keyboard { get; set; }
}

/// <summary>
/// A context menu asked for — a right click, a long press, or the context-menu key — on a record,
/// or on the background with <see cref="SednaGraphNodeEventArgs.Id"/> null: <c>sedna-graph-context</c>.
/// </summary>
/// <remarks>
/// A <c>[data-graph-menu]</c> the app wrote inside the graph opens by itself. Cancel the event —
/// <c>@onsedna-graph-context:preventDefault</c> — to open something of the app's own instead, at
/// <see cref="X"/> and <see cref="Y"/>.
/// </remarks>
public sealed class SednaGraphContextEventArgs : SednaGraphNodeEventArgs
{
    /// <summary>Where, in pixels from the graph's left edge.</summary>
    public int X { get; set; }

    /// <summary>Where, in pixels from the graph's top edge.</summary>
    public int Y { get; set; }
}

/// <summary>A link the reader drew from one record to another: <c>sedna-graph-connect</c>.</summary>
/// <remarks>
/// The line drawn is removed again as the event is dispatched: the app adds the link to its own
/// data, and the graph shows it — as a drop in a drag-and-drop list is the app's to make.
/// </remarks>
public sealed class SednaGraphConnectEventArgs : EventArgs
{
    /// <summary>The record the link was drawn from.</summary>
    public string Source { get; set; } = "";

    /// <summary>The record it was drawn to.</summary>
    public string Target { get; set; } = "";
}

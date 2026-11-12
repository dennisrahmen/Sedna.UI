namespace Sedna.UI;

/// <summary>
/// An item dropped somewhere other than where it started: <c>sedna-drop</c>, dispatched on
/// the zone it landed in.
/// </summary>
/// <remarks>
/// <see cref="Index"/> counts the destination without the item, so the whole handler is a
/// remove from <see cref="From"/> and an insert into <see cref="To"/> at that index — the
/// same code whether the item changed zone or not. With a selection, it is the same handler
/// over <see cref="Items"/>: remove every one, then insert them at <see cref="Index"/> in
/// that order.
/// </remarks>
public sealed class SednaDropEventArgs : EventArgs
{
    /// <summary>The item's <c>data-drag-item</c>: the one the reader took hold of.</summary>
    public string Item { get; set; } = "";

    /// <summary>
    /// Every item carried, in document order: the selection of <see cref="From"/> when
    /// <see cref="Item"/> was part of it — <c>aria-selected="true"</c>, or a checked
    /// <c>input[data-drag-select]</c> — and otherwise <see cref="Item"/> alone.
    /// </summary>
    public string[] Items { get; set; } = [];

    /// <summary>The item's <c>data-drag-type</c>, or null when it has none.</summary>
    public string? Type { get; set; }

    /// <summary>The <c>data-drag-zone</c> the item was lifted from.</summary>
    public string From { get; set; } = "";

    /// <summary>The <c>data-drag-zone</c> it landed in; the same as <see cref="From"/> for a reorder.</summary>
    public string To { get; set; } = "";

    /// <summary>Where it goes in <see cref="To"/>, counted once it has left <see cref="From"/>.</summary>
    public int Index { get; set; }

    /// <summary>Where it was in <see cref="From"/>.</summary>
    public int FromIndex { get; set; }

    /// <summary>True when the move was made from the keyboard.</summary>
    public bool Keyboard { get; set; }

    /// <summary>
    /// For a drop on a slot — a zone with <c>data-drag-axis="slot"</c>, a day of a month or a
    /// half hour of a week — the <c>data-drag-zone</c> of the slot the drag started over: under
    /// the pointer where it was picked up, or under the item's own start from the keyboard.
    /// Move the item by the distance from here to <see cref="To"/>. Null for any other drop.
    /// </summary>
    public string? FromSlot { get; set; }

    /// <summary>
    /// <c>"copy"</c> when the item was dragged from a zone, or is an item, with
    /// <c>data-drag-effect="copy"</c>: add a copy to <see cref="To"/> and leave <see cref="From"/>
    /// as it is. <c>"move"</c> for every other drop.
    /// </summary>
    public string Effect { get; set; } = "move";

    /// <summary>
    /// For a drop on a panel grid — a <c>.panel-grid</c> with <c>data-drag-axis="cells"</c> —
    /// the column the panel landed on, from 0 at the inline start. Null for any other drop.
    /// </summary>
    public int? X { get; set; }

    /// <summary>For a drop on a panel grid, the row the panel landed on, from 0 at the top. Null for any other drop.</summary>
    public int? Y { get; set; }

    /// <summary>
    /// For a drop on a panel grid, the whole arrangement that results, in reading order: store it
    /// and render it back, and <see cref="Index"/> is the panel's place in it. Null for any other drop.
    /// </summary>
    public SednaPanel[]? Panels { get; set; }
}

/// <summary>
/// A drag starting or ending: <c>sedna-dragstart</c> and <c>sedna-dragend</c>, dispatched
/// on the item.
/// </summary>
public sealed class SednaDragEventArgs : EventArgs
{
    /// <summary>The item's <c>data-drag-item</c>.</summary>
    public string Item { get; set; } = "";

    /// <summary>Every item carried, in document order; see <see cref="SednaDropEventArgs.Items"/>.</summary>
    public string[] Items { get; set; } = [];

    /// <summary>The item's <c>data-drag-type</c>, or null when it has none.</summary>
    public string? Type { get; set; }

    /// <summary>
    /// The <c>data-drag-zone</c> the item is in: where it started, or where it was dropped — and
    /// where it started for a copy.
    /// </summary>
    public string Zone { get; set; } = "";

    /// <summary>Its position in <see cref="Zone"/>.</summary>
    public int Index { get; set; }

    /// <summary>True when the drag is from the keyboard.</summary>
    public bool Keyboard { get; set; }

    /// <summary>
    /// On <c>sedna-dragend</c>, true when the item was dropped somewhere new and a
    /// <c>sedna-drop</c> went before it. Always false on <c>sedna-dragstart</c>.
    /// </summary>
    public bool Dropped { get; set; }

    /// <summary>
    /// <c>"copy"</c> for a drag that drops a copy — <c>data-drag-effect="copy"</c> on the item
    /// or its zone — after which <see cref="Zone"/> and <see cref="Index"/> still name where
    /// the item started, because it never left; <c>"move"</c> otherwise.
    /// </summary>
    public string Effect { get; set; } = "move";
}

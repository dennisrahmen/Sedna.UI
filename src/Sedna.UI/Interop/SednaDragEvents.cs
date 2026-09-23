using Microsoft.AspNetCore.Components;

namespace Sedna.UI;

/// <summary>
/// Makes the drag-and-drop events <c>Sedna.UI.js</c> dispatches bindable from Razor, with
/// their data: <c>@onsedna-drop</c>, <c>@onsedna-dragstart</c> and <c>@onsedna-dragend</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Razor compiler finds an event only through a class of exactly this name, and only in
/// a namespace the component imports — so <c>@using Sedna.UI</c> in <c>_Imports.razor</c>.
/// <c>Sedna.UI.lib.module.js</c>, which Blazor loads by itself, registers the same three
/// names in the browser.
/// </para>
/// <para>
/// The script never moves an item. A drop is an event the app handles by moving the item in
/// its own list and re-rendering; see <see cref="SednaDropEventArgs"/>.
/// </para>
/// </remarks>
[EventHandler("onsedna-dragstart", typeof(SednaDragEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onsedna-drop", typeof(SednaDropEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onsedna-dragend", typeof(SednaDragEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
public static class EventHandlers
{
}

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

    /// <summary>The <c>data-drag-zone</c> the item is in: where it started, or where it was dropped.</summary>
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
}

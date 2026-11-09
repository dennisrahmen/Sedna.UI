namespace Sedna.UI;

/// <summary>
/// A branch of a <c>.tree</c> opened or closed: <c>sedna-tree-expand</c> and
/// <c>sedna-tree-collapse</c>, dispatched on the item.
/// </summary>
/// <remarks>
/// A branch that carries <c>data-tree-lazy</c> has no children in the page yet. Opening it sends
/// <see cref="Lazy"/> true and shows the item busy; the app renders the item's
/// <c>role="group"</c> with its children and drops <c>data-tree-lazy</c>, and the busy state lifts.
/// </remarks>
public sealed class SednaTreeEventArgs : EventArgs
{
    /// <summary>The item's <c>data-value</c>, or its label when it has none.</summary>
    public string Item { get; set; } = "";

    /// <summary>True when the branch opened with <c>data-tree-lazy</c>: the app renders its children now.</summary>
    public bool Lazy { get; set; }
}

/// <summary>
/// An item of a <c>.tree</c> was picked or unpicked — <c>sedna-tree-select</c>, for
/// <c>aria-selected</c> — or ticked or unticked — <c>sedna-tree-check</c>, for <c>aria-checked</c>.
/// Dispatched on the item.
/// </summary>
/// <remarks>
/// The script has already written the state on the app's items: for a tick, on every item under
/// it and, as ticked, unticked or mixed, on every parent above it. <see cref="Items"/> is the whole
/// state afterwards, so an app keeps its own copy by replacing it rather than by repeating the
/// propagation.
/// </remarks>
public sealed class SednaTreeSelectEventArgs : EventArgs
{
    /// <summary>The item's <c>data-value</c>, or its label when it has none.</summary>
    public string Item { get; set; } = "";

    /// <summary>True when the item is now picked, or ticked.</summary>
    public bool Selected { get; set; }

    /// <summary>
    /// Every item picked in the tree, or for <c>sedna-tree-check</c> every item ticked — not the
    /// mixed ones — in document order.
    /// </summary>
    public string[] Items { get; set; } = [];
}

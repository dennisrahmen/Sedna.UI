namespace Sedna.UI;

/// <summary>
/// How <see cref="ISednaUi.FollowSpotlightAsync"/> places the hole and the bubble.
/// </summary>
/// <remarks>
/// Every element is named by a CSS selector rather than an <c>ElementReference</c>: a
/// re-render replaces the node, and a selector is resolved again on every placement.
/// </remarks>
public sealed record SpotlightOptions
{
    /// <summary>How far the hole grows beyond the target, in pixels. 4 when unset.</summary>
    public int? Pad { get; init; }

    /// <summary>
    /// Elements that widen the hole without being the anchor — a control plus the dropdown
    /// it opened.
    /// </summary>
    public string? Include { get; init; }

    /// <summary>The app's <c>.spotlight-tip</c> bubble, placed beside the hole.</summary>
    public string? Tip { get; init; }

    /// <summary>Which side of the hole the bubble goes on. Below when unset.</summary>
    public SpotlightPlacement? Placement { get; init; }

    /// <summary>Distance between the hole and the bubble, in pixels.</summary>
    public int? Gap { get; init; }

    /// <summary>Smallest distance between the bubble and the boundary's edge, in pixels.</summary>
    public int? Margin { get; init; }

    /// <summary>What the bubble stays inside. The viewport when unset.</summary>
    public string? Boundary { get; init; }

    /// <summary>What is watched for a re-render moving the target. <c>body</c> when unset.</summary>
    public string? Root { get; init; }

    /// <summary>
    /// Makes the rest of the page inert while the step is live. Stops a person, not a
    /// script: never treat a live step as an authorization boundary.
    /// </summary>
    public SpotlightLock? Lock { get; init; }

    /// <summary>The object the script reads, with unset options left out.</summary>
    internal Dictionary<string, object> ToScript()
    {
        var o = new Dictionary<string, object>(StringComparer.Ordinal);
        if (Pad is { } pad) o["pad"] = pad;
        if (Include is not null) o["include"] = Include;
        if (Tip is not null) o["tip"] = Tip;
        if (Placement is { } placement) o["placement"] = Name(placement);
        if (Gap is { } gap) o["gap"] = gap;
        if (Margin is { } margin) o["margin"] = margin;
        if (Boundary is not null) o["boundary"] = Boundary;
        if (Root is not null) o["root"] = Root;
        if (Lock is { } l)
        {
            var lockOptions = new Dictionary<string, object>(StringComparer.Ordinal) { ["interactive"] = l.Interactive };
            if (l.Allow is not null) lockOptions["allow"] = l.Allow;
            if (l.Block is not null) lockOptions["block"] = l.Block;
            o["lock"] = lockOptions;
        }
        return o;
    }

    // The script's own vocabulary. Mapped explicitly rather than lower-casing the enum
    // name, so renaming a member here cannot silently change what the script is sent.
    private static string Name(SpotlightPlacement placement) => placement switch
    {
        SpotlightPlacement.Top => "top",
        SpotlightPlacement.Right => "right",
        SpotlightPlacement.Left => "left",
        SpotlightPlacement.Auto => "auto",
        _ => "bottom",
    };
}

/// <summary>Which side of the hole a spotlight's bubble goes on.</summary>
public enum SpotlightPlacement
{
    /// <summary>Below the hole.</summary>
    Bottom,

    /// <summary>Above the hole.</summary>
    Top,

    /// <summary>To the right of the hole.</summary>
    Right,

    /// <summary>To the left of the hole.</summary>
    Left,

    /// <summary>Whichever side has the most room.</summary>
    Auto,
}

/// <summary>What stays usable while a spotlight step locks the page.</summary>
public sealed record SpotlightLock
{
    /// <summary>The target itself stays operable, for a step the reader completes by using it.</summary>
    public bool Interactive { get; init; }

    /// <summary>Further elements that stay operable, as a CSS selector.</summary>
    public string? Allow { get; init; }

    /// <summary>
    /// Elements blocked even inside what is allowed. <c>[data-spotlight-block]</c> when unset.
    /// </summary>
    public string? Block { get; init; }
}

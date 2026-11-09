namespace Sedna.UI;

/// <summary>
/// One entry in the command palette.
/// </summary>
/// <remarks>
/// <para>
/// A command registered from C# navigates: set <see cref="Href"/> and choosing the
/// command goes there. The palette's JavaScript form also accepts a <c>run</c>
/// callback, which has no C# equivalent: a function does not cross the interop
/// boundary, so a <see cref="Delegate"/> here could not be invoked. Register commands
/// that must run arbitrary code from JavaScript instead.
/// </para>
/// </remarks>
public sealed record PaletteCommand
{
    /// <summary>The visible name, and the field the ranking scores first.</summary>
    public required string Label { get; init; }

    /// <summary>Where the command goes. Relative to the base path, or absolute.</summary>
    public string? Href { get; init; }

    /// <summary>Remix Icon class, for example <c>ri-inbox-line</c>.</summary>
    public string? Icon { get; init; }

    /// <summary>
    /// A heading the command sits under. Shown only while the list is unfiltered:
    /// a heading over reordered results is worse than no heading.
    /// </summary>
    public string? Group { get; init; }

    /// <summary>A short trailing hint — a shortcut, or where the command leads.</summary>
    public string? Note { get; init; }

    /// <summary>
    /// Extra terms the command should be findable by. A match here always ranks
    /// below the same match in <see cref="Label"/>.
    /// </summary>
    public string? Keywords { get; init; }

    /// <summary>
    /// The page the command is listed on. <see langword="null"/> lists it on the root; any
    /// other value lists it on the page a command with the same <see cref="Opens"/> opens.
    /// </summary>
    public string? Page { get; init; }

    /// <summary>
    /// The page choosing this command opens, in place of navigating. Backspace in an empty
    /// search, or Escape, goes back one page. The commands on it are those whose
    /// <see cref="Page"/> is this value.
    /// </summary>
    public string? Opens { get; init; }

    /// <summary>
    /// Whether the command is shown whatever is typed. A pinned command is never ranked:
    /// it follows the matches on its page and does not count as one.
    /// </summary>
    public bool Pinned { get; init; }
}

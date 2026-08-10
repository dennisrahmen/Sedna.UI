namespace Sedna.UI;

/// <summary>
/// One entry in the header search's index.
/// </summary>
/// <remarks>
/// <para>
/// The index is registered in the browser and ranked there, so the results appear
/// with no round trip. That is what makes it suitable for a fixed, known set — an
/// app's pages, its reports, its settings screens. It is <b>not</b> the shape for
/// searching a database: an app that needs that renders its own results with the
/// same <c>.search-*</c> classes and leaves <c>data-search</c> off the input.
/// </para>
/// <para>
/// Choosing a result navigates to <see cref="Href"/>. The JavaScript form also
/// accepts a <c>run</c> callback, which has no C# equivalent — the library never
/// calls back into .NET, so a <see cref="Delegate"/> here could not be invoked.
/// </para>
/// </remarks>
public sealed record SearchItem
{
    /// <summary>The result's first line, and the field the ranking scores first.</summary>
    public required string Title { get; init; }

    /// <summary>Where choosing the result goes. Relative to the base path, or absolute.</summary>
    public string? Href { get; init; }

    /// <summary>
    /// A short monospaced run at the start of the second line — an identifier, a
    /// route, a class name. Ranked below <see cref="Title"/>.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// The rest of the second line: what this result is. Ranked last, because a
    /// word found in a blurb is a weaker hit than the same word in a title.
    /// </summary>
    public string? Meta { get; init; }

    /// <summary>An optional pill at the end of the second line — a state, a category.</summary>
    public string? Tag { get; init; }

    /// <summary>
    /// <c>warn</c> renders <see cref="Tag"/> in the amber family. Any other value
    /// leaves it neutral.
    /// </summary>
    public string? Tone { get; init; }

    /// <summary>
    /// Extra terms the result should be findable by. A match here ranks below the
    /// same match in <see cref="Title"/>.
    /// </summary>
    public string? Keywords { get; init; }
}

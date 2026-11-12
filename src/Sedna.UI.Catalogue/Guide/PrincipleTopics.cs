namespace Sedna.UI.Catalogue.Guide;

/// <summary>
/// The principles page's topics: which of <c>principles.md</c>'s sections it lists under each,
/// in the order it shows them. A section named in no topic goes under the last one, so a new
/// section in the file is on the page before anyone files it.
/// </summary>
internal static class PrincipleTopics
{
    private static readonly (string Topic, string[] Ids)[] Map =
    [
        ("Composing a page", ["start-from-the-goal", "page-composition", "spacing", "density"]),
        ("Colour and motion", ["colour", "motion"]),
        ("Behaviour and words", ["actions", "feedback", "words", "accessibility"]),
    ];

    /// <summary>Each topic with the guide's sections under it.</summary>
    public static IReadOnlyList<(string Topic, IReadOnlyList<GuideSection> Sections)> Of(DesignGuide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);
        var filed = Map.SelectMany(t => t.Ids).ToHashSet(StringComparer.Ordinal);
        var rest = guide.Sections.Where(s => !filed.Contains(s.Id)).ToList();

        return [.. Map.Select((t, i) => (t.Topic, (IReadOnlyList<GuideSection>)
            [.. t.Ids.Select(guide.Section).OfType<GuideSection>(), .. i == Map.Length - 1 ? rest : []]))];
    }
}

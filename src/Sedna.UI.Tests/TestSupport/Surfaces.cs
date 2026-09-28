using System.Text.RegularExpressions;

namespace Sedna.UI.Tests.TestSupport;

/// <summary>
/// The tier 3 registry: the table in <c>docs/surfaces.md</c>, read as data.
/// </summary>
/// <remarks>
/// The document is the list and the tests are its readers, so a surface is added by writing its row
/// and nowhere else. Linked into the catalogue's tests, which hold the pages to the same rows.
/// </remarks>
internal static class Surfaces
{
    public sealed record Surface(
        string Name,
        string Delivery,
        string Reference,
        string? Stylesheet,
        IReadOnlyList<string> Scripts,
        string? Engine,
        string Catalogue);

    public static string DocPath => Path.Combine(Assets.RepoRoot, "docs", "surfaces.md");

    private static readonly Regex Code = new("`([^`]+)`", RegexOptions.Compiled);
    private static readonly Regex Link = new(@"\]\(([^)]+)\)", RegexOptions.Compiled);

    public static IReadOnlyList<Surface> All { get; } = Read();

    private static List<Surface> Read()
    {
        var lines = File.ReadAllLines(DocPath);
        var start = Array.FindIndex(lines, l => l.StartsWith("## The registry", StringComparison.Ordinal));
        if (start < 0) throw new InvalidOperationException("docs/surfaces.md has no \"## The registry\" section.");

        var rows = lines.Skip(start + 1)
            .SkipWhile(l => !l.StartsWith('|'))
            .TakeWhile(l => l.StartsWith('|'))
            .ToList();
        if (rows.Count < 3) throw new InvalidOperationException("The registry table has no rows.");

        var header = Cells(rows[0]);
        string[] expected = ["Surface", "Delivery", "Reference", "Stylesheet", "Script", "Engine", "Catalogue"];
        if (!header.SequenceEqual(expected))
            throw new InvalidOperationException(
                $"The registry's columns are {string.Join(", ", header)}; the tests read {string.Join(", ", expected)}.");

        return rows.Skip(2).Select(Cells).Select(c => new Surface(
            Name: c[0],
            Delivery: c[1],
            Reference: Link.Match(c[2]) is { Success: true } m ? m.Groups[1].Value : c[2],
            Stylesheet: Spans(c[3]).SingleOrDefault(),
            Scripts: Spans(c[4]),
            Engine: Spans(c[5]).SingleOrDefault(),
            Catalogue: Spans(c[6]).SingleOrDefault() ?? c[6])).ToList();
    }

    private static string[] Cells(string row) =>
        row.Trim().Trim('|').Split('|').Select(c => c.Trim()).ToArray();

    private static string[] Spans(string cell) =>
        Code.Matches(cell).Select(m => m.Groups[1].Value).ToArray();
}

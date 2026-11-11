using System.Text.Json;
using Sedna.UI.Catalogue.Guide;
using Sedna.UI.Catalogue.Navigation;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// <c>docs/design-guide/</c> as it is on disk — what the guide tests hold to their rules, and
/// what the app embeds.
/// </summary>
internal static class GuideFiles
{
    public static string Dir { get; } = Path.Combine(Assets.RepoRoot, "docs", "design-guide");

    public static string PrinciplesPath => Path.Combine(Dir, "principles.md");

    public static string Principles => Normalise(File.ReadAllText(PrinciplesPath));

    /// <summary>Each components file, as (file name, text).</summary>
    public static IReadOnlyList<(string FileName, string Json)> Components { get; } = Files("components");

    /// <summary>Each goals file, as (file name, text).</summary>
    public static IReadOnlyList<(string FileName, string Json)> GoalFiles { get; } = Files("goals");

    /// <summary>The guide built from the files on disk, by the app's own parser.</summary>
    public static DesignGuide Guide => DesignGuide.From(Principles, Components, GoalFiles);

    /// <summary>
    /// The pages that document no component, and why. They have no entry, and no goal
    /// answers with one. Every other registered page has exactly one entry.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Exempt { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["/"] = "The landing page shows the library; it documents no component.",
        ["/getting-started"] = "Installing the package and wiring the host page.",
        ["/design-guide"] = "The guide itself.",
        ["/concepts"] = "The rules an app writes its CSS against, which principles.md links to.",
        ["/tokens"] = "The token reference, read from the stylesheet.",
        ["/branding"] = "The mark, the palette and the theming decisions.",
        ["/theme-builder"] = "A tool for writing a brand.css, not a part of a page.",
        ["/mcp-server"] = "This site's MCP server.",
        ["/script"] = "The script's API reference.",
        ["/interop"] = "The C# API reference.",
        ["/tooling"] = "The app's build tooling.",
    };

    /// <summary>The groups <c>pending.json</c> lists as not yet written, for one kind of file.</summary>
    public static IReadOnlySet<string> Pending(string kind)
    {
        var path = Path.Combine(Dir, "pending.json");
        if (!File.Exists(path)) return new HashSet<string>(StringComparer.Ordinal);

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.TryGetProperty(kind, out var list)
            ? list.EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Whether a group's file of one kind is still to be written: listed in <c>pending.json</c>
    /// and not on disk. A file on disk is held to every rule whether or not the group is listed.
    /// </summary>
    public static bool IsPending(string kind, string group) =>
        Pending(kind).Contains(group.ToLowerInvariant())
        && !File.Exists(Path.Combine(Dir, kind, group.ToLowerInvariant() + ".json"));

    /// <summary>The group a registered route belongs to, or null.</summary>
    public static string? GroupOf(string route) =>
        CataloguePages.All.FirstOrDefault(p => p.Route == route)?.Group;

    private static List<(string, string)> Files(string kind)
    {
        var dir = Path.Combine(Dir, kind);
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.json").Order(StringComparer.Ordinal)
                .Select(p => (Path.GetFileName(p), Normalise(File.ReadAllText(p)))).ToList()
            : [];
    }

    private static string Normalise(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal);
}

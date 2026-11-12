using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Sedna.UI.Catalogue.Components.Docs;
using Sedna.UI.Catalogue.Navigation;

namespace Sedna.UI.Catalogue.Guide;

/// <summary>A page the entry is used together with, and how.</summary>
internal sealed record GuideLink(string Page, string How);

/// <summary>A job the page is not for, and the page that does it.</summary>
internal sealed record GuideNotFor(string Text, string Instead);

/// <summary>
/// One catalogue page's guidance: what it is for, what it is combined with, what it is not for
/// and which goals it answers. <see cref="Stamp"/> is written by <c>build/guide-stamp.sh</c>.
/// </summary>
internal sealed record GuideEntry(
    string Page,
    string UseFor,
    IReadOnlyList<GuideLink> CombineWith,
    IReadOnlyList<GuideNotFor> NotFor,
    IReadOnlyList<string> Goals,
    string Stamp)
{
    /// <summary>The catalogue group the entry's file is named after.</summary>
    [JsonIgnore] public string Group { get; init; } = string.Empty;
}

/// <summary>A page that answers a goal: the one to start from, or a part of the answer.</summary>
internal sealed record GuideAnswer(string Page, string Role)
{
    public const string Primary = "primary";
    public const string Part = "part";
}

/// <summary>Something the app wants its user to be able to do, and the parts that answer it.</summary>
internal sealed record GuideGoal(
    string Id,
    string Goal,
    IReadOnlyList<string> Phrases,
    IReadOnlyList<GuideAnswer> Answer,
    string How,
    string Avoid)
{
    /// <summary>The catalogue group the goal's file is named after: where its primary answer lives.</summary>
    [JsonIgnore] public string Group { get; init; } = string.Empty;

    /// <summary>The page to start from, with its anchor when it has one.</summary>
    public string PrimaryPage =>
        Answer.FirstOrDefault(a => a.Role == GuideAnswer.Primary)?.Page ?? Answer.FirstOrDefault()?.Page ?? "/";
}

/// <summary>One <c>## </c> section of <c>principles.md</c>.</summary>
internal sealed record GuideSection(string Id, string Title, string Markdown);

/// <summary>
/// The design guide: <c>docs/design-guide/</c>, embedded and parsed once. The web page, the
/// panel every component page shows, the topbar search and the MCP server all read this.
/// </summary>
/// <remarks>
/// The files are parsed strictly — an unknown key, a missing one or a null is an error — so a
/// mistake in a group's file fails <c>DesignGuideTests</c> with the file's name rather than
/// rendering a half-empty panel.
/// </remarks>
internal sealed class DesignGuide
{
    private static readonly JsonSerializerOptions Strict = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        ReadCommentHandling = JsonCommentHandling.Disallow,
    };

    private static readonly Lazy<DesignGuide> Loaded = new(LoadEmbedded);

    private readonly Dictionary<string, GuideEntry> _entries;
    private readonly Dictionary<string, GuideGoal> _goals;
    private readonly Dictionary<string, GuideSection> _sections;

    private DesignGuide(string intro, IReadOnlyList<GuideSection> sections,
        IReadOnlyList<GuideEntry> entries, IReadOnlyList<GuideGoal> goals)
    {
        Intro = intro;
        Sections = sections;
        Entries = entries;
        Goals = goals;
        _entries = entries.GroupBy(e => e.Page, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _goals = goals.GroupBy(g => g.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        _sections = sections.GroupBy(s => s.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    /// <summary>The guide this build embeds.</summary>
    public static DesignGuide Embedded => Loaded.Value;

    /// <summary>The text of <c>principles.md</c> before its first section.</summary>
    public string Intro { get; }

    public IReadOnlyList<GuideSection> Sections { get; }

    public IReadOnlyList<GuideEntry> Entries { get; }

    public IReadOnlyList<GuideGoal> Goals { get; }

    public GuideEntry? EntryFor(string route) => _entries.GetValueOrDefault(route);

    public GuideGoal? Goal(string id) => _goals.GetValueOrDefault(id);

    public GuideSection? Section(string id) => _sections.GetValueOrDefault(id);

    /// <summary>The goals an entry lists, in its order, skipping any that do not exist.</summary>
    public IReadOnlyList<GuideGoal> GoalsOf(GuideEntry entry) =>
        entry.Goals.Select(Goal).OfType<GuideGoal>().ToList();

    /// <summary>A page reference split into its route and its anchor, which may be empty.</summary>
    public static (string Route, string Anchor) Split(string page)
    {
        var hash = page.IndexOf('#', StringComparison.Ordinal);
        return hash < 0 ? (page, string.Empty) : (page[..hash], page[(hash + 1)..]);
    }

    /// <summary>The registered label of the page a reference names, its anchor aside.</summary>
    public static string LabelOf(string page)
    {
        var route = Split(page).Route;
        return CataloguePages.All.FirstOrDefault(p => p.Route == route)?.Label ?? route;
    }

    /// <summary>The catalogue group a guide file is named after: <c>data.json</c> is Data.</summary>
    public static string GroupOf(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return CataloguePages.Groups.FirstOrDefault(g => string.Equals(g, stem, StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidDataException(
                   $"{fileName} names no catalogue group. A guide file is named after one, in lowercase: "
                   + string.Join(", ", CataloguePages.Groups.Select(g => g.ToLowerInvariant() + ".json")) + ".");
    }

    /// <summary>One group's entries, parsed strictly.</summary>
    public static IReadOnlyList<GuideEntry> ParseEntries(string fileName, string json)
    {
        var group = GroupOf(fileName);
        return Parse<GuideEntry>(fileName, json).Select(e => e with { Group = group }).ToList();
    }

    /// <summary>One group's goals, parsed strictly.</summary>
    public static IReadOnlyList<GuideGoal> ParseGoals(string fileName, string json)
    {
        var group = GroupOf(fileName);
        return Parse<GuideGoal>(fileName, json).Select(g => g with { Group = group }).ToList();
    }

    private static List<T> Parse<T>(string fileName, string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json, Strict)
                   ?? throw new InvalidDataException($"{fileName} is null; it holds an array.");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{fileName} does not match the schema in docs/design-guide/README.md: {e.Message}", e);
        }
    }

    /// <summary>Splits <c>principles.md</c> into its intro and its <c>## </c> sections.</summary>
    public static (string Intro, IReadOnlyList<GuideSection> Sections) ParsePrinciples(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var intro = new List<string>();
        var sections = new List<GuideSection>();
        string? title = null;
        var body = new List<string>();

        void Close()
        {
            if (title is not null)
                sections.Add(new GuideSection(Slug.From(title), title, string.Join('\n', body).Trim()));
            body.Clear();
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("# ", StringComparison.Ordinal)) continue;
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                Close();
                title = line[3..].Trim();
                continue;
            }

            (title is null ? intro : body).Add(line);
        }

        Close();
        return (string.Join('\n', intro).Trim(), sections);
    }

    /// <summary>Builds a guide from its files' text — what the tests do with the files on disk.</summary>
    public static DesignGuide From(string principles,
        IEnumerable<(string FileName, string Json)> components,
        IEnumerable<(string FileName, string Json)> goals)
    {
        var (intro, sections) = ParsePrinciples(principles);
        return new DesignGuide(intro, sections,
            components.OrderBy(c => GroupOrder(c.FileName)).SelectMany(c => ParseEntries(c.FileName, c.Json)).ToList(),
            goals.OrderBy(g => GroupOrder(g.FileName)).SelectMany(g => ParseGoals(g.FileName, g.Json)).ToList());
    }

    private static int GroupOrder(string fileName)
    {
        var group = GroupOf(fileName);
        return CataloguePages.Groups.ToList().IndexOf(group);
    }

    // ── The embedded copy ──────────────────────────────────────────────────

    private const string Prefix = ".Guide.";

    /// <summary>The embedded guide files, by their path under <c>docs/design-guide/</c>.</summary>
    public static IReadOnlyDictionary<string, string> EmbeddedFiles()
    {
        var assembly = typeof(DesignGuide).Assembly;
        var files = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in assembly.GetManifestResourceNames())
        {
            var at = name.IndexOf(Prefix, StringComparison.Ordinal);
            if (at < 0) continue;

            // …Guide.components.data.json → components/data.json
            var tail = name[(at + Prefix.Length)..];
            var extension = Path.GetExtension(tail);
            var stem = tail[..^extension.Length];
            var path = stem.Replace('.', '/') + extension;
            files[path] = Read(assembly, name);
        }

        return files;
    }

    private static string Read(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static DesignGuide LoadEmbedded()
    {
        var files = EmbeddedFiles();
        if (!files.TryGetValue("principles.md", out var principles))
            throw new InvalidOperationException(
                "docs/design-guide/principles.md is not embedded. The Dockerfile has to COPY docs/, and the "
                + $"EmbeddedResource glob has to cover it. Embedded: {string.Join(", ", files.Keys)}");

        return From(principles,
            files.Where(f => f.Key.StartsWith("components/", StringComparison.Ordinal))
                .Select(f => (Path.GetFileName(f.Key), f.Value)),
            files.Where(f => f.Key.StartsWith("goals/", StringComparison.Ordinal))
                .Select(f => (Path.GetFileName(f.Key), f.Value)));
    }
}

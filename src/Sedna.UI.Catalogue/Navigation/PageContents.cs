using System.Net;
using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Components.Docs;
using Sedna.UI.Catalogue.Guide;

namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// One section a page's contents link to: the heading's id, its words, and — for a
/// <c>CatGroup</c> — the sections inside it.
/// </summary>
public sealed record PageSection(string Id, string Title, IReadOnlyList<PageSection>? Children = null)
{
    /// <summary>The sections inside a group, or none.</summary>
    public IReadOnlyList<PageSection> Children { get; } = Children ?? [];

    /// <summary>This section and every section inside it, in page order.</summary>
    public IEnumerable<PageSection> Flatten() => [this, .. Children.SelectMany(c => c.Flatten())];
}

/// <summary>
/// The sections of every page, for the contents the layout shows beside it: each
/// <c>CatGroup</c>, with the sections inside it nested under it, each <c>CatExample</c> and
/// <c>CatSnippet</c>, whose heading is its title, and each hand-written <c>&lt;h2 id&gt;</c> —
/// <c>&lt;h3 id&gt;</c> inside a group — in the order the page's source writes them.
/// </summary>
/// <remarks>
/// Read once from the page sources the assembly embeds, the same ones the MCP index reads, so a
/// section added to a page is in its contents without a list to keep. A heading a page writes in
/// a loop has no literal id to read; the two pages that do — the principles and the tokens — take
/// theirs from the data the loop renders, nested under their topics. <c>PageContentsTests</c> holds every list to the
/// headings the served page has.
/// </remarks>
internal sealed class PageContents
{
    private static readonly Regex Section = new(
        """<CatGroup\s+(?<group>[^>]*?)>|(?<close></CatGroup>)|<Cat(?:Example|Snippet)\s+(?<attrs>[^>]*?)/?>|<h[23]\s+id="(?<id>[^"@]+)"[^>]*>(?<text>.*?)(?:<CatAnchor|</h[23]>)""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex Attribute = new(
        @"(?<name>\w+)=""(?<value>[^""]*)""", RegexOptions.Compiled);

    private static readonly Regex PageDirective = new(
        @"^@page\s+""(?<route>[^""]+)""", RegexOptions.Compiled | RegexOptions.Multiline);

    private static readonly Regex Tag = new("<[^>]+>", RegexOptions.Compiled);

    private readonly Dictionary<string, IReadOnlyList<PageSection>> byRoute = new(StringComparer.Ordinal);

    public PageContents(DesignGuide guide)
    {
        ArgumentNullException.ThrowIfNull(guide);

        var assembly = typeof(PageContents).Assembly;
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.Contains(".Components.Pages.", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null) continue;
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();
            if (PageDirective.Match(source) is not { Success: true } directive) continue;

            var sections = new List<PageSection>();
            // The group being read, if any: its id, its title and what has been found inside it.
            (string Id, string Title, List<PageSection> Children)? group = null;
            void Add(PageSection section) => (group?.Children ?? sections).Add(section);

            foreach (Match m in Section.Matches(source))
            {
                if (m.Groups["group"].Success)
                {
                    var title = Attribute.Matches(m.Groups["group"].Value)
                        .First(a => a.Groups["name"].Value == "Title").Groups["value"].Value;
                    group = (Slug.From(title), title, []);
                    continue;
                }

                if (m.Groups["close"].Success)
                {
                    if (group is { } done) sections.Add(new PageSection(done.Id, done.Title, done.Children));
                    group = null;
                    continue;
                }

                if (m.Groups["id"].Success)
                {
                    // A heading whose words are a <CatTitle Text="…"> has them in that attribute.
                    var raw = m.Groups["text"].Value;
                    var named = Attribute.Matches(raw).FirstOrDefault(a => a.Groups["name"].Value == "Text");
                    var text = named is not null
                        ? CatTitle.Plain(named.Groups["value"].Value)
                        : WebUtility.HtmlDecode(Tag.Replace(raw, string.Empty)).Trim();
                    if (text.Length > 0) Add(new PageSection(m.Groups["id"].Value, text));
                    continue;
                }

                var attrs = Attribute.Matches(m.Groups["attrs"].Value)
                    .ToDictionary(a => a.Groups["name"].Value, a => a.Groups["value"].Value, StringComparer.Ordinal);
                if (!attrs.TryGetValue("Title", out var exampleTitle)) continue;
                Add(new PageSection(Slug.From(exampleTitle), exampleTitle));
            }

            byRoute[directive.Groups["route"].Value] = sections;
        }

        byRoute["/principles"] =
            [.. PrincipleTopics.Of(guide).Select(t => new PageSection(Slug.From(t.Topic), t.Topic,
                [.. t.Sections.Select(s => new PageSection(s.Id, s.Title))]))];

        byRoute["/tokens"] =
            [.. For("/tokens"), .. TokenGroups.BySection.Select(s => new PageSection(Slug.From(s.Section), s.Section,
                [.. s.Groups.Select(g => new PageSection(Slug.From(g.Name), g.Name))]))];
    }

    /// <summary>The sections of the page at a route, or none for an unknown route.</summary>
    public IReadOnlyList<PageSection> For(string route) =>
        byRoute.TryGetValue(route, out var sections) ? sections : [];
}

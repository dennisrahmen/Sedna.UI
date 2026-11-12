using System.Net;
using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Components.Docs;
using Sedna.UI.Catalogue.Guide;

namespace Sedna.UI.Catalogue.Navigation;

/// <summary>One section a page's contents link to: the heading's id and its words.</summary>
public sealed record PageSection(string Id, string Title);

/// <summary>
/// The sections of every page, for the contents the layout shows beside it: each
/// <c>CatExample</c> and <c>CatSnippet</c>, whose heading is its title, and each hand-written
/// <c>&lt;h2 id&gt;</c>, in the order the page's source writes them.
/// </summary>
/// <remarks>
/// Read once from the page sources the assembly embeds, the same ones the MCP index reads, so a
/// section added to a page is in its contents without a list to keep. A heading a page writes in
/// a loop has no literal id to read; the two pages that do — the design guide and the tokens —
/// take theirs from the data the loop renders. <c>PageContentsTests</c> holds every list to the
/// headings the served page has.
/// </remarks>
internal sealed class PageContents
{
    private static readonly Regex Section = new(
        """<Cat(?:Example|Snippet)\s+(?<attrs>[^>]*?)/?>|<h2\s+id="(?<id>[^"@]+)"[^>]*>(?<text>.*?)(?:<CatAnchor|</h2>)""",
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
            foreach (Match m in Section.Matches(source))
            {
                if (m.Groups["id"].Success)
                {
                    // A heading whose words are a <CatTitle Text="…"> has them in that attribute.
                    var raw = m.Groups["text"].Value;
                    var named = Attribute.Matches(raw).FirstOrDefault(a => a.Groups["name"].Value == "Text");
                    var text = named is not null
                        ? CatTitle.Plain(named.Groups["value"].Value)
                        : WebUtility.HtmlDecode(Tag.Replace(raw, string.Empty)).Trim();
                    if (text.Length > 0) sections.Add(new PageSection(m.Groups["id"].Value, text));
                    continue;
                }

                var attrs = Attribute.Matches(m.Groups["attrs"].Value)
                    .ToDictionary(a => a.Groups["name"].Value, a => a.Groups["value"].Value, StringComparer.Ordinal);
                if (!attrs.TryGetValue("Title", out var title)) continue;
                var plain = CatTitle.Plain(title);
                sections.Add(new PageSection(Slug.From(plain), plain));
            }

            byRoute[directive.Groups["route"].Value] = sections;
        }

        byRoute["/design-guide"] =
            [new PageSection("goals", "What the user can do"), .. guide.Sections.Select(s => new PageSection(s.Id, s.Title))];

        byRoute["/tokens"] =
            [.. For("/tokens"), .. TokenGroups.All.Select(g => new PageSection(Slug.From(g.Name), CatTitle.Plain(g.Name)))];
    }

    /// <summary>The sections of the page at a route, or none for an unknown route.</summary>
    public IReadOnlyList<PageSection> For(string route) =>
        byRoute.TryGetValue(route, out var sections) ? sections : [];
}

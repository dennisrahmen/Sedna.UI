using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Components.Docs;
using Sedna.UI.Catalogue.Navigation;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>One example, as the MCP server sees it.</summary>
internal sealed record IndexedExample(
    string Id,
    string Page,
    string Route,
    string Title,
    string Blurb,
    string Markup,
    string Language,
    bool Live,
    IReadOnlyList<string> Classes);

/// <summary>One CSS class, with what the stylesheet actually says about it.</summary>
internal sealed record IndexedClass(
    string Name,
    string Layer,
    string Declarations,
    IReadOnlyList<string> Modifiers,
    IReadOnlyList<string> UsedByExamples);

/// <summary>
/// Everything the MCP server can answer questions about, built once at startup
/// from the embedded example sources, the embedded pages, and the shipped
/// stylesheet.
/// </summary>
/// <remarks>
/// Nothing here is hand-listed. The examples come from the same embedded resources
/// the pages render, and the classes come from the stylesheet the app serves — so
/// an agent cannot be told about markup that is not on the site, or about a class
/// the sheet does not define.
/// </remarks>
internal sealed class CatalogueIndex
{
    private static readonly Regex ExampleTag = new(
        """<Cat(?<kind>Example|Snippet)\s+(?<attrs>[^>]*?)/?>""",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex Attribute = new(
        @"(?<name>\w+)=""(?<value>[^""]*)""", RegexOptions.Compiled);

    private static readonly Regex ClassAttribute = new(
        @"class=""(?<value>[^""@]*)""", RegexOptions.Compiled);

    private static readonly Regex LayerBlock = new(
        @"@layer\s+(?<layer>[\w.]+)\s*\{", RegexOptions.Compiled);

    public CatalogueIndex(IWebHostEnvironment environment)
    {
        RawStylesheet = ReadStaticAsset(environment, "css/Sedna.UI.css");
        Examples = BuildExamples();
        Classes = BuildClasses(RawStylesheet, Examples);
        Tokens = ReadTokens(environment);
    }

    /// <summary>The shipped stylesheet, verbatim — served as an MCP resource.</summary>
    public string RawStylesheet { get; }

    public IReadOnlyList<IndexedExample> Examples { get; }

    public IReadOnlyList<IndexedClass> Classes { get; }

    /// <summary>The token export, verbatim — an ordered array of blocks.</summary>
    public JsonDocument Tokens { get; }

    public IndexedExample? Example(string id) =>
        Examples.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    public IndexedClass? Class(string name)
    {
        var bare = name.TrimStart('.');
        return Classes.FirstOrDefault(c => string.Equals(c.Name, bare, StringComparison.OrdinalIgnoreCase));
    }

    // ── Examples ────────────────────────────────────────────────────────────

    private static List<IndexedExample> BuildExamples()
    {
        var metadata = PageMetadata();
        var examples = new List<IndexedExample>();

        foreach (var resource in ExampleSource.Names)
        {
            // …Examples.Badge.Semantic.razor -> ("Badge", "Semantic", "razor")
            var tail = resource[(resource.IndexOf(".Examples.", StringComparison.Ordinal)
                                 + ".Examples.".Length)..];
            var parts = tail.Split('.');
            if (parts.Length < 3) continue;

            var folder = parts[0];
            var name = parts[1];
            var extension = parts[^1];
            var id = $"{folder}/{name}";

            var markup = ExampleSource.For($"{folder}/{name}.{extension}");
            var meta = metadata.GetValueOrDefault(id);
            var route = CataloguePages.All
                .FirstOrDefault(p => string.Equals(PageId(p), meta?.Page ?? folder,
                    StringComparison.OrdinalIgnoreCase))?.Route ?? "/";

            examples.Add(new IndexedExample(
                Id: id,
                Page: meta?.Page ?? folder,
                Route: route,
                Title: meta?.Title ?? Humanise(name),
                Blurb: meta?.Blurb ?? string.Empty,
                Markup: markup,
                Language: extension == "razor" ? "html" : extension,
                Live: extension == "razor",
                Classes: ClassesIn(markup)));
        }

        return examples.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
    }

    private sealed record ExampleMeta(string Page, string Title, string Blurb);

    /// <summary>
    /// Each example's title and prose, read out of the page that renders it.
    /// </summary>
    /// <remarks>
    /// A regex over the page source rather than a second hand-kept list. It cannot
    /// silently lose an example: the index is built from the example resources, and
    /// a page tag that fails to match only costs the title, which
    /// <c>McpIndexTests</c> asserts is present for every one.
    /// </remarks>
    private static Dictionary<string, ExampleMeta> PageMetadata()
    {
        var assembly = typeof(CatalogueIndex).Assembly;
        var found = new Dictionary<string, ExampleMeta>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.Contains(".Components.Pages.", StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null) continue;
            var source = new StreamReader(stream).ReadToEnd();

            foreach (var tag in ExampleTag.Matches(source).Cast<Match>())
            {
                var attrs = Attribute.Matches(tag.Groups["attrs"].Value)
                    .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value,
                        StringComparer.Ordinal);

                string? id = null;
                if (attrs.TryGetValue("TExample", out var type))
                {
                    // "Examples.Badge.Semantic" -> "Badge/Semantic"
                    var parts = type.Split('.');
                    if (parts.Length >= 3) id = $"{parts[^2]}/{parts[^1]}";
                }
                else if (attrs.TryGetValue("Name", out var path))
                {
                    id = Path.ChangeExtension(path, null);
                }

                if (id is null || !attrs.TryGetValue("Title", out var title)) continue;

                var blurb = Blurb(source, tag.Index + tag.Length);
                found[id] = new ExampleMeta(id.Split('/')[0], CatTitle.Plain(title), blurb);
            }
        }

        return found;
    }

    /// <summary>The prose between an example's opening tag and its close.</summary>
    private static string Blurb(string source, int from)
    {
        var end = source.IndexOf("</Cat", from, StringComparison.Ordinal);
        if (end < 0) return string.Empty;

        var text = Regex.Replace(source[from..end], "<[^>]+>", " ");
        return Regex.Replace(text.Replace("@@", "@", StringComparison.Ordinal), @"\s+", " ").Trim();
    }

    private static List<string> ClassesIn(string markup) =>
        ClassAttribute.Matches(markup)
            .SelectMany(m => m.Groups["value"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(c => !c.StartsWith("ri-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();

    // ── Classes ─────────────────────────────────────────────────────────────

    private static List<IndexedClass> BuildClasses(
        string rawCss, IReadOnlyList<IndexedExample> examples)
    {
        var declarations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var layers = new Dictionary<string, string>(StringComparer.Ordinal);

        // Comments out first. The walker takes everything between one rule's closing
        // brace and the next opening brace as the selector, and this stylesheet
        // documents itself heavily — without this, `.accordion` reports a screenful
        // of box-drawing characters as its declaration.
        var css = Regex.Replace(rawCss, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        foreach (var (selector, body, layer) in Rules(css))
        {
            foreach (var name in Regex.Matches(selector, @"\.(-?[a-zA-Z][a-zA-Z0-9-]*)")
                         .Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal))
            {
                if (!declarations.TryGetValue(name, out var list))
                {
                    declarations[name] = list = [];
                    layers[name] = layer;
                }

                list.Add($"{Squash(selector)} {{ {Squash(body)} }}");
            }
        }

        var usage = examples
            .SelectMany(e => e.Classes.Select(c => (Class: c, e.Id)))
            .GroupBy(x => x.Class, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Id).ToList(),
                StringComparer.Ordinal);

        return declarations.Select(entry => new IndexedClass(
                Name: entry.Key,
                Layer: layers[entry.Key],
                // Capped: .btn appears in dozens of rules and an agent needs the
                // shape, not the whole cascade.
                Declarations: string.Join("\n", entry.Value.Take(12)),
                Modifiers: declarations.Keys
                    .Where(other => other.StartsWith(entry.Key + "-", StringComparison.Ordinal)
                                    || other.StartsWith(entry.Key + "--", StringComparison.Ordinal))
                    .OrderBy(m => m, StringComparer.Ordinal).ToList(),
                UsedByExamples: usage.GetValueOrDefault(entry.Key, [])))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Every rule as (selector, body, layer), tracking which <c>@layer</c> block it
    /// sits in.
    /// </summary>
    /// <remarks>
    /// The layer is read from the enclosing block rather than derived a second time
    /// from a part's <c>NN-</c> prefix, so it cannot disagree with what the
    /// generator wrote.
    /// </remarks>
    private static IEnumerable<(string Selector, string Body, string Layer)> Rules(string css)
    {
        var layer = string.Empty;
        var depth = 0;
        var start = 0;

        for (var i = 0; i < css.Length; i++)
        {
            if (css[i] == '{')
            {
                if (depth == 0)
                {
                    var prelude = css[start..i];
                    var match = LayerBlock.Match(prelude + "{");
                    if (match.Success) layer = match.Groups["layer"].Value;
                    start = i + 1;
                }
                else if (depth == 1)
                {
                    start = i + 1;
                }

                depth++;
            }
            else if (css[i] == '}')
            {
                depth--;
                if (depth == 1)
                {
                    var body = css[start..i];
                    var selector = css[LastBoundary(css, start)..(start - 1)];
                    if (selector.Contains('.', StringComparison.Ordinal))
                        yield return (selector.Trim(), body, layer);
                }

                start = i + 1;
            }
        }
    }

    private static int LastBoundary(string css, int bodyStart)
    {
        var i = bodyStart - 2;
        while (i > 0 && css[i] != '}' && css[i] != '{') i--;
        return i + 1;
    }

    private static string Squash(string s) =>
        Regex.Replace(s, @"\s+", " ").Trim();

    // ── Files the app already serves ────────────────────────────────────────

    private static JsonDocument ReadTokens(IWebHostEnvironment environment) =>
        JsonDocument.Parse(ReadStaticAsset(environment, "tokens/Sedna.UI.tokens.json"));

    /// <summary>
    /// Reads a file the library ships, through the same static-web-asset provider
    /// that serves it to the browser — so the MCP server and the site cannot
    /// describe different bytes.
    /// </summary>
    private static string ReadStaticAsset(IWebHostEnvironment environment, string path)
    {
        var file = environment.WebRootFileProvider.GetFileInfo($"_content/Sedna.UI/{path}");
        if (!file.Exists)
            throw new InvalidOperationException(
                $"The library asset \"{path}\" is not being served. The static web assets manifest "
                + "is missing or the project reference is broken.");

        using var reader = new StreamReader(file.CreateReadStream());
        return reader.ReadToEnd();
    }

    private static string PageId(CataloguePage page) =>
        page.Route == "/" ? "Index"
            : string.Concat(page.Route.TrimStart('/').Split('-')
                .Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

    private static string Humanise(string pascal) =>
        Regex.Replace(pascal, "(?<!^)([A-Z])", " $1");
}

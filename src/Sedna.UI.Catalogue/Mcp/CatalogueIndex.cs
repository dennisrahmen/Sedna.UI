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
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> Ids,
    IReadOnlyList<string> Members);

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

    /// <summary>A class name written in prose, e.g. <c>`.spotlight-lock` goes on body</c>.</summary>
    /// <remarks>
    /// The lookbehind is what keeps <c>sednaUi.spotlight</c> and <c>ids.Length</c> out
    /// of it; the rest is kept out by matching against what the stylesheet declares.
    /// </remarks>
    private static readonly Regex MentionedClass = new(
        @"(?<![\w-])\.(-?[a-zA-Z][a-zA-Z0-9-]*)", RegexOptions.Compiled);

    private static readonly Regex LayerBlock = new(
        @"@layer\s+(?<layer>[\w.]+)\s*\{", RegexOptions.Compiled);

    public CatalogueIndex(IWebHostEnvironment environment)
    {
        RawStylesheet = ReadStaticAsset(environment, "css/Sedna.UI.css");

        // Classes first, without their usage: an example's class list is matched
        // against what the sheet declares, and the usage is the same relation read
        // the other way round. One extraction of "what does this stylesheet declare",
        // not two.
        var (declaredClasses, declaredIds) = BuildSelectors(RawStylesheet);
        PublicApi = ReadPublicApi();
        Examples = BuildExamples(
            declaredClasses.Select(c => c.Name).ToHashSet(StringComparer.Ordinal), PublicApi);
        Classes = WithUsage(declaredClasses, Examples, e => e.Classes);
        Ids = WithUsage(declaredIds, Examples, e => e.Ids);
        Tokens = ReadTokens(environment);
    }

    /// <summary>The shipped stylesheet, verbatim — served as an MCP resource.</summary>
    public string RawStylesheet { get; }

    public IReadOnlyList<IndexedExample> Examples { get; }

    public IReadOnlyList<IndexedClass> Classes { get; }

    /// <summary>
    /// The <c>#id</c> selectors the stylesheet declares — today only Blazor's own
    /// <c>#blazor-error-ui</c>, which every host page carries.
    /// </summary>
    /// <remarks>
    /// Indexed for one reason: <c>describe_class</c> answering <c>notFound</c> to
    /// <c>#blazor-error-ui</c> read as "the library does not style that" rather than
    /// "this tool indexes classes", and an agent then wrote its own twenty lines for a
    /// rule the sheet already ships. A tool that cannot see something must say so
    /// rather than report nothing.
    /// </remarks>
    public IReadOnlyList<IndexedClass> Ids { get; }

    /// <summary>The token export, verbatim — an ordered array of blocks.</summary>
    public JsonDocument Tokens { get; }

    /// <summary>
    /// Every public C# type and member the library exports, as <c>Type</c> and
    /// <c>Type.Member</c>.
    /// </summary>
    /// <remarks>
    /// Read by reflection from the assembly this app has actually referenced, which
    /// makes it the truth about the surface — and it is deliberately a SECOND
    /// implementation of the same question <c>build/api-inventory.sh</c> answers by
    /// parsing source across every tag. Reflection cannot see an old tag and the parser
    /// cannot see the compiler's view, so <c>McpVersionTests</c> holds the two against
    /// each other at HEAD: a parse that ever drifts from the real surface is reported
    /// rather than quietly attributing a member to the wrong release.
    /// </remarks>
    public IReadOnlySet<string> PublicApi { get; }

    public IndexedExample? Example(string id) =>
        Examples.FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

    public IndexedClass? Class(string name)
    {
        // A leading '#' is never a class, however the rest of it reads: without this,
        // `#card` would answer with `.card`'s rules for a selector that matches
        // nothing of the sort.
        if (name.StartsWith('#')) return null;

        var bare = name.TrimStart('.');
        return Classes.FirstOrDefault(c => string.Equals(c.Name, bare, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The rules the stylesheet declares for an <c>#id</c>, if it declares any.</summary>
    public IndexedClass? IdSelector(string name)
    {
        var bare = name.TrimStart('#');
        return Ids.FirstOrDefault(c => string.Equals(c.Name, bare, StringComparison.OrdinalIgnoreCase));
    }

    // ── Examples ────────────────────────────────────────────────────────────

    private static List<IndexedExample> BuildExamples(
        IReadOnlySet<string> declared, IReadOnlySet<string> api)
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
                Classes: ClassesIn(markup, live: extension == "razor", declared),
                Ids: IdsIn(markup),
                Members: MembersIn(markup, api)));
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
            using var reader = new StreamReader(stream);
            var source = reader.ReadToEnd();

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

    /// <summary>
    /// The library's exported C# surface, by reflection over the referenced assembly.
    /// </summary>
    /// <remarks>
    /// Only what somebody writes in their own code is listed. Everything the compiler
    /// synthesises is skipped — property accessors, operators, records'
    /// <c>Equals</c>/<c>GetHashCode</c>/<c>ToString</c>/<c>Deconstruct</c>/<c>&lt;Clone&gt;$</c>
    /// and <c>EqualityContract</c>, and anything inherited rather than declared — because
    /// none of it appears in an example and none of it is a capability an app waits for a
    /// release to get.
    /// </remarks>
    internal static HashSet<string> ReadPublicApi()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in typeof(ISednaUi).Assembly.GetExportedTypes())
        {
            names.Add(type.Name);

            foreach (var member in type.GetMembers(
                         BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static
                         | BindingFlags.DeclaredOnly))
            {
                if (member is MethodBase { IsSpecialName: true }) continue;
                if (member is ConstructorInfo) continue;
                if (Synthesised.Contains(member.Name)) continue;
                if (member.Name.StartsWith('<')) continue;

                names.Add($"{type.Name}.{member.Name}");
            }
        }

        return names;
    }

    /// <summary>Members the compiler writes, which nobody types into an example.</summary>
    private static readonly HashSet<string> Synthesised = new(StringComparer.Ordinal)
    {
        "Equals", "GetHashCode", "ToString", "Deconstruct", "PrintMembers", "EqualityContract",
        "GetType", "Clone",
        // An enum's backing field, which reflection reports as a public instance field
        // of every enum and nobody has ever typed.
        "value__",
    };

    /// <summary>The public C# surface an example writes.</summary>
    /// <remarks>
    /// The same shape as <see cref="ClassesIn"/>, and for the same reason: a
    /// <c>.txt</c> snippet of <c>ISednaUi</c> calls declares no classes at all, so
    /// without this it had no floor and reported itself as shipping in whatever the
    /// newest release happened to be. A mention only counts when the library actually
    /// exports that name, which is what keeps <c>Task</c>, <c>await</c> and an app's own
    /// identifiers out.
    /// </remarks>
    private static List<string> MembersIn(string markup, IReadOnlySet<string> api)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var match in Identifier.Matches(markup).Cast<Match>())
        {
            var name = match.Groups[1].Value;
            // A bare `Group` or `Title` is a word before it is a member, so a member
            // name only counts qualified — `PaletteCommand.Group`, or the type itself.
            if (api.Contains(name)) found.Add(name);
        }

        foreach (var match in QualifiedIdentifier.Matches(markup).Cast<Match>())
        {
            var name = match.Groups[1].Value;
            if (api.Any(a => a.EndsWith("." + name, StringComparison.Ordinal))) found.Add(name);
        }

        return found.ToList();
    }

    /// <summary>The ids an example's markup carries.</summary>
    /// <remarks>
    /// So that an example is findable by the thing it is about. The error-bar snippet
    /// is about <c>#blazor-error-ui</c> and named it nowhere a search could see, so a
    /// search for that id returned the reconnect banner — a different mechanism, on
    /// the strength of the word "Blazor" in its blurb — and the agent that copied it
    /// styled the wrong element.
    /// </remarks>
    private static List<string> IdsIn(string markup) =>
        IdAttribute.Matches(markup)
            .Select(m => m.Groups["value"].Value)
            .Where(id => id.Length > 0 && !id.Contains('@', StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

    private static readonly Regex IdAttribute = new(
        @"\bid=""(?<value>[^""]*)""", RegexOptions.Compiled);

    /// <summary>A PascalCase identifier standing on its own — a type name.</summary>
    private static readonly Regex Identifier = new(
        @"(?<![\w.])([A-Z][A-Za-z0-9]*)(?![\w])", RegexOptions.Compiled);

    /// <summary>An identifier reached through something — <c>Ui.ToastAsync</c>, <c>x.Href</c>.</summary>
    private static readonly Regex QualifiedIdentifier = new(
        @"[\w\]\)]\.([A-Z][A-Za-z0-9]*)", RegexOptions.Compiled);

    /// <summary>The classes an example is about.</summary>
    /// <remarks>
    /// A live example applies every class it is about, so its <c>class</c>
    /// attributes are the whole answer. A code-only snippet does not: the JS
    /// snippets name the classes their API puts on the page in prose, and reading
    /// only the attributes reported <c>Spotlight/SpotlightLock</c> as shipping in
    /// the release its <c>.btn</c> markup came from rather than the one that added
    /// <c>.spotlight-lock</c> — an understated <c>since</c>, which is the direction
    /// that gets an agent to copy something its app does not have. A mention only
    /// counts when the stylesheet declares it.
    /// </remarks>
    private static List<string> ClassesIn(string markup, bool live, IReadOnlySet<string> declared)
    {
        IEnumerable<string> found = ClassAttribute.Matches(markup)
            .SelectMany(m => m.Groups["value"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        if (!live)
        {
            found = found.Concat(MentionedClass.Matches(markup)
                .Select(m => m.Groups[1].Value)
                .Where(declared.Contains));
        }

        return found
            .Where(c => !c.StartsWith("ri-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
    }

    // ── Classes ─────────────────────────────────────────────────────────────

    /// <summary>
    /// What the stylesheet declares, in one pass: the classes, and the <c>#id</c>
    /// selectors beside them.
    /// </summary>
    /// <remarks>
    /// One walk, two outputs — an id is found by the same rule walker that finds a
    /// class, so the two answers cannot disagree about what the sheet contains.
    /// </remarks>
    private static (List<IndexedClass> Classes, List<IndexedClass> Ids) BuildSelectors(string rawCss)
    {
        var declarations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var layers = new Dictionary<string, string>(StringComparer.Ordinal);
        var idDeclarations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var idLayers = new Dictionary<string, string>(StringComparer.Ordinal);

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

            foreach (var name in Regex.Matches(selector, @"#(-?[a-zA-Z][a-zA-Z0-9-]*)")
                         .Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal))
            {
                if (!idDeclarations.TryGetValue(name, out var list))
                {
                    idDeclarations[name] = list = [];
                    idLayers[name] = layer;
                }

                list.Add($"{Squash(selector)} {{ {Squash(body)} }}");
            }
        }

        return (Selectors(declarations, layers, "."), Selectors(idDeclarations, idLayers, "#"));
    }

    /// <summary>One selector family, as the index reports it.</summary>
    /// <remarks>
    /// <paramref name="sigil"/> decides what a modifier is: <c>.card-head</c> is a
    /// modifier of <c>.card</c>, while an id has no such family — nothing is a
    /// modifier of <c>#blazor-error-ui</c>, and reporting one would invent a
    /// relationship the sheet does not have.
    /// </remarks>
    private static List<IndexedClass> Selectors(
        Dictionary<string, List<string>> declarations,
        Dictionary<string, string> layers,
        string sigil) =>
        declarations.Select(entry => new IndexedClass(
                Name: entry.Key,
                Layer: layers[entry.Key],
                // Capped: .btn appears in dozens of rules and an agent needs the
                // shape, not the whole cascade.
                Declarations: string.Join("\n", entry.Value.Take(12)),
                Modifiers: sigil == "."
                    ? declarations.Keys
                        .Where(other => other.StartsWith(entry.Key + "-", StringComparison.Ordinal)
                                        || other.StartsWith(entry.Key + "--", StringComparison.Ordinal))
                        .OrderBy(m => m, StringComparer.Ordinal).ToList()
                    : [],
                UsedByExamples: []))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

    /// <summary>The same relation as an example's class list, read the other way round.</summary>
    private static List<IndexedClass> WithUsage(
        List<IndexedClass> selectors,
        IReadOnlyList<IndexedExample> examples,
        Func<IndexedExample, IReadOnlyList<string>> namesIn)
    {
        var usage = examples
            .SelectMany(e => namesIn(e).Select(c => (Name: c, e.Id)))
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Id).ToList(),
                StringComparer.Ordinal);

        return selectors
            .Select(c => c with { UsedByExamples = usage.GetValueOrDefault(c.Name, []) })
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
                    // A dot or a hash: bare element rules carry no name to index, and an
                    // id-only rule is exactly the one that was invisible — every
                    // #blazor-error-ui rule was dropped here before it reached the walker's
                    // caller, which is why the tool could only answer notFound.
                    if (selector.Contains('.', StringComparison.Ordinal)
                        || selector.Contains('#', StringComparison.Ordinal))
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

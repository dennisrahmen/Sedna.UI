using System.ComponentModel;
using System.Text.Json;
using Sedna.UI.Catalogue.Navigation;
using ModelContextProtocol.Server;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>
/// The MCP tool surface: six tools, six verbs, no overlap.
/// </summary>
/// <remarks>
/// <para>
/// Shaped around one workflow — an agent writing a page in a consuming app, knowing
/// roughly what it wants ("a filter bar above a sortable table with status badges")
/// and needing the exact markup plus enough semantics to pick the right variant.
/// <c>search</c> → <c>get_example</c> → <c>describe_class</c> is the whole loop.
/// </para>
/// <para>
/// Every tool is read-only, and <b>there must never be a seventh that writes</b>.
/// The endpoint is public and unauthenticated; a client honouring the read-only
/// hint calls these without prompting, which is only safe while that stays true.
/// </para>
/// <para>
/// <c>search</c> returns references and never markup: a search that returned markup
/// would spend the context window on the first call.
/// </para>
/// </remarks>
[McpServerToolType]
internal sealed class CatalogueTools(CatalogueIndex index, VersionEnvelope versions)
{
    private const int MaxSearchResults = 25;
    private const int MaxExamples = 5;
    private const int MaxClasses = 10;
    private const int MaxMarkupBytes = 8 * 1024;

    [McpServerTool(Name = "search", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        Search the Sedna.UI catalogue for examples, CSS classes, design tokens and pages.
        Returns references only — call get_example for the markup. This is the first call for
        any "how do I build X" question.
        """)]
    public object Search(
        [Description("What you are looking for, e.g. \"sortable table\" or \"badge-go\".")]
        string query,
        [Description("Restrict to one of: example, class, token, page. Omit for everything.")]
        string? kind = null,
        [Description("How many hits to return. Clamped to 25.")]
        int limit = 10,
        [Description("The Sedna.UI version your app has installed, e.g. \"0.2.0\". "
                     + "Supplying it flags results your version does not have.")]
        string? installedVersion = null)
    {
        var terms = CatalogueRanker.Terms(query).ToList();
        var hits = new List<Hit>();

        if (kind is null or "example")
        {
            foreach (var example in index.Examples)
            {
                var score = CatalogueRanker.Score(terms, example.Id,
                    [example.Title, example.Page, string.Join(' ', example.Classes)],
                    [example.Blurb, example.Markup]);
                if (score is null) continue;

                // Capped: the shell example names forty classes, and a search
                // result carrying all of them is the context-window cost this tool
                // exists to avoid. get_example returns the full list.
                hits.Add(new Hit("example", example.Id, example.Title, Trim(example.Blurb),
                    example.Route, example.Classes.Take(8).ToList(),
                    score.Value.Score, score.Value.MatchedOn));
            }
        }

        if (kind is null or "class")
        {
            foreach (var css in index.Classes)
            {
                var score = CatalogueRanker.Score(terms, css.Name,
                    [css.Name, string.Join(' ', css.Modifiers)], [css.Declarations]);
                if (score is null) continue;

                hits.Add(new Hit("class", "." + css.Name, "." + css.Name,
                    $"In layer {css.Layer}. Used by {css.UsedByExamples.Count} example(s).",
                    css.UsedByExamples.FirstOrDefault() ?? string.Empty,
                    css.Modifiers, score.Value.Score, score.Value.MatchedOn));
            }
        }

        if (kind is null or "token")
        {
            foreach (var token in TokenNames())
            {
                var score = CatalogueRanker.Score(terms, token, [token], []);
                if (score is null) continue;

                hits.Add(new Hit("token", token, token, "Design token.", "/tokens", [],
                    score.Value.Score, score.Value.MatchedOn));
            }
        }

        if (kind is null or "page")
        {
            foreach (var page in CataloguePages.All)
            {
                var score = CatalogueRanker.Score(terms, page.Route.TrimStart('/'),
                    [page.Label, page.Keywords], [page.Blurb]);
                if (score is null) continue;

                // A page outranks an example that merely contains the thing: "badge"
                // should find the Badges page first.
                hits.Add(new Hit("page", page.Route, page.Label, page.Blurb, page.Route, [],
                    score.Value.Score + 50, score.Value.MatchedOn));
            }
        }

        var clamped = Math.Clamp(limit, 1, MaxSearchResults);

        // Stable: ties keep source order, so two identical calls return identical JSON.
        var ranked = hits.OrderByDescending(h => h.Score).ToList();

        return new
        {
            meta = versions.For(installedVersion,
                ranked.Take(clamped).Where(h => h.Kind is "class" or "example")
                    .Select(h => (h.Ref, Since(h)))),
            hits = ranked.Take(clamped),
            total = ranked.Count,
            truncated = ranked.Count > clamped,
        };
    }

    [McpServerTool(Name = "get_example", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        The exact markup for one or more catalogue examples, byte-for-byte what the site renders.
        Paste it into a .razor page or an .html file — it is valid in both. Call after search.
        """)]
    public object GetExample(
        [Description("Example ids from search, e.g. [\"Badge/Semantic\"]. At most 5.")]
        string[] ids,
        [Description("The Sedna.UI version your app has installed.")]
        string? installedVersion = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Length > MaxExamples)
            throw new ArgumentException(
                $"At most {MaxExamples} ids per call; {ids.Length} were requested.", nameof(ids));

        var found = ids.Select(index.Example).OfType<IndexedExample>().ToList();
        var missing = ids.Where(id => index.Example(id) is null).ToList();

        return new
        {
            meta = versions.For(installedVersion,
                found.SelectMany(e => e.Classes.Select(c => ("." + c, versions.SinceClass(c))))),
            examples = found.Select(e => new
            {
                id = e.Id,
                page = e.Page,
                url = e.Route,
                title = e.Title,
                blurb = e.Blurb,
                language = e.Language,
                markup = Cap(e.Markup, out var truncated),
                truncated,
                classes = e.Classes,
                since = Since(versions.SinceAll(e.Classes)),
            }),
            notFound = missing,
        };
    }

    [McpServerTool(Name = "describe_class", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        What a CSS class actually does: the rules the shipped stylesheet declares for it, its
        cascade layer, its modifiers, and which examples use it. Use it to choose between
        variants, or to check a class an app already styles before upgrading.
        """)]
    public object DescribeClass(
        [Description("Class names, with or without the leading dot. At most 10.")]
        string[] names,
        [Description("The Sedna.UI version your app has installed.")]
        string? installedVersion = null)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Length > MaxClasses)
            throw new ArgumentException(
                $"At most {MaxClasses} names per call; {names.Length} were requested.", nameof(names));

        var found = names.Select(index.Class).OfType<IndexedClass>().ToList();

        return new
        {
            meta = versions.For(installedVersion,
                found.Select(c => ("." + c.Name, versions.SinceClass(c.Name)))),
            classes = found.Select(c => new
            {
                name = "." + c.Name,
                layer = c.Layer,
                // Read from the stylesheet the app serves, so it cannot drift from
                // what a browser would apply.
                declarations = c.Declarations,
                modifiers = c.Modifiers.Select(m => "." + m),
                usedByExamples = c.UsedByExamples,
                since = Since(versions.SinceClass(c.Name)),
            }),
            notFound = names.Where(n => index.Class(n) is null),
        };
    }

    [McpServerTool(Name = "get_page", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        With an id, every example on one catalogue page — one call instead of five. Without one,
        the list of pages, which is the fastest way to see what the library covers.
        """)]
    public object GetPage(
        [Description("A route from search, e.g. \"/table\". Omit to list every page.")]
        string? id = null)
    {
        if (id is null)
        {
            return new
            {
                meta = versions.For(null),
                pages = CataloguePages.All.Select(p => new
                {
                    id = p.Route,
                    title = p.Label,
                    group = p.Group,
                    blurb = p.Blurb,
                    exampleCount = index.Examples.Count(e => e.Route == p.Route),
                }),
            };
        }

        var route = id.StartsWith('/') ? id : "/" + id;
        var page = CataloguePages.All.FirstOrDefault(p =>
            string.Equals(p.Route, route, StringComparison.OrdinalIgnoreCase));

        if (page is null)
            return new { meta = versions.For(null), notFound = id };

        return new
        {
            meta = versions.For(null),
            id = page.Route,
            title = page.Label,
            blurb = page.Blurb,
            examples = index.Examples.Where(e => e.Route == page.Route).Select(e => new
            {
                id = e.Id,
                title = e.Title,
                blurb = e.Blurb,
                classes = e.Classes,
                since = Since(versions.SinceAll(e.Classes)),
            }),
        };
    }

    [McpServerTool(Name = "get_tokens", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        The design tokens, as an ordered list of blocks. Every colour in the library resolves
        through one of these, and redefining them in your own brand.css rebrands the whole app.
        Never declare a token name the library does not already define.
        """)]
    public object GetTokens(
        [Description("Substring filter on the token name, e.g. \"brand\". Omit for all of them.")]
        string? filter = null,
        [Description("The Sedna.UI version your app has installed.")]
        string? installedVersion = null)
    {
        var blocks = new List<object>();
        var seen = new List<(string, string?)>();

        foreach (var block in index.Tokens.RootElement.GetProperty("blocks").EnumerateArray())
        {
            var tokens = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var token in block.GetProperty("tokens").EnumerateObject())
            {
                if (filter is not null
                    && !token.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;

                tokens[token.Name] = token.Value.GetString() ?? string.Empty;
                seen.Add((token.Name, versions.SinceToken(token.Name)));
            }

            if (tokens.Count == 0) continue;

            blocks.Add(new
            {
                media = block.GetProperty("media").ValueKind == JsonValueKind.Null
                    ? null
                    : block.GetProperty("media").GetString(),
                selector = block.GetProperty("selector").GetString(),
                tokens,
            });
        }

        return new
        {
            meta = versions.For(installedVersion, seen),
            // An ordered array, not a map keyed by theme: `:root` appears three
            // times across the blocks, and a map would silently lose two of them.
            blocks,
            note = "Redefine these in your own stylesheet, loaded after the library's. "
                   + "Only names the library already declares.",
        };
    }

    [McpServerTool(Name = "get_integration_guide", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("""
        How to wire Sedna.UI into an app: the host page with its load order, the branding
        recipe, the JavaScript and C# surface, and the rules a consuming app follows. Call once
        when integrating, not per page.
        """)]
    public object GetIntegrationGuide(
        [Description("One of: host-page, branding, javascript, rules. Omit for host-page.")]
        string? section = null)
    {
        var name = section ?? "host-page";
        var markdown = name switch
        {
            "host-page" => Docs.Read("getting-started.md"),
            "branding" => Docs.Read("getting-started.md"),
            "javascript" => Docs.Read("architecture.md"),
            "rules" => Docs.Read("CLAUDE.consuming-app.md"),
            _ => throw new ArgumentException(
                $"Unknown section \"{name}\". Use host-page, branding, javascript or rules.",
                nameof(section)),
        };

        return new { meta = versions.For(null), section = name, markdown };
    }

    /// <summary>
    /// The release something first shipped in, or the literal <c>"unreleased"</c>.
    /// </summary>
    /// <remarks>
    /// Never null. The MCP SDK serialises with <c>WhenWritingNull</c>, so a null
    /// <c>since</c> vanishes from the response entirely — and "unreleased" becomes
    /// indistinguishable from "not reported", which is the one distinction this
    /// field exists to make. A word a model can read beats a missing key.
    /// </remarks>
    private const string Unreleased = "unreleased";

    private static string Since(string? version) => version ?? Unreleased;

    /// <summary>A blurb short enough to scan in a result list.</summary>
    private static string Trim(string blurb) =>
        blurb.Length <= 160 ? blurb : blurb[..blurb.LastIndexOf(' ', 160)] + "…";

    private string? Since(Hit hit) => hit.Kind switch
    {
        "class" => versions.SinceClass(hit.Ref),
        "example" => versions.SinceAll(hit.Classes),
        _ => versions.LatestRelease,
    };

    private IEnumerable<string> TokenNames() =>
        index.Tokens.RootElement.GetProperty("blocks").EnumerateArray()
            .SelectMany(b => b.GetProperty("tokens").EnumerateObject().Select(t => t.Name))
            .Distinct(StringComparer.Ordinal);

    /// <summary>
    /// Caps a markup value so one large example cannot become the whole response.
    /// </summary>
    /// <remarks>
    /// Nothing is close to the cap today. It exists so a future 300-line example
    /// cannot silently blow a context window.
    /// </remarks>
    private static string Cap(string markup, out bool truncated)
    {
        truncated = markup.Length > MaxMarkupBytes;
        if (!truncated) return markup;

        // Cut at a tag boundary, so the result is still parseable.
        var cut = markup.LastIndexOf('<', MaxMarkupBytes);
        return markup[..(cut > 0 ? cut : MaxMarkupBytes)]
               + "\n<!-- truncated; see the page for the rest -->";
    }
}

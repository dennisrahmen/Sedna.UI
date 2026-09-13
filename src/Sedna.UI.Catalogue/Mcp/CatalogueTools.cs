using System.ComponentModel;
using System.Text.Json;
using Sedna.UI.Catalogue.Navigation;
using ModelContextProtocol;
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
/// <para>
/// <b>No tool declares an output schema.</b> Every one of them returns an anonymous
/// object, which the SDK cannot describe, so <c>UseStructuredContent</c> made it
/// advertise the placeholder <c>{"type":"object","properties":{"result":true}}</c> —
/// a boolean subschema, legal JSON Schema and rejected by the Zod validator in the
/// MCP TypeScript SDK. A client that validates a tool list therefore dropped all six
/// tools while the server stayed connected and its instructions loaded, which is a
/// failure with no error anywhere. The payload is the JSON text of the response, read
/// from <c>content[0].text</c> as it always was.
/// </para>
/// <para>
/// <b>Every rejection is an <see cref="McpException"/></b>, whose message the SDK
/// propagates to the caller; any other exception type reaches it as the bare
/// "An error occurred invoking 'x'.", which tells a model nothing and leaves it
/// guessing at the limit or the spelling. So a rejection here always names the
/// limit it broke or the values it may use.
/// </para>
/// </remarks>
[McpServerToolType]
internal sealed class CatalogueTools(CatalogueIndex index, VersionEnvelope versions)
{
    private const int MaxSearchResults = 25;
    private const int MaxExamples = 5;
    private const int MaxClasses = 10;
    private const int MaxMarkupBytes = 8 * 1024;

    /// <summary>What makes a name a selector rather than a class name.</summary>
    private static readonly char[] SelectorPunctuation = [' ', '[', ':', '>', ',', '+', '~'];

    /// <summary>The values <c>kind</c> accepts. An unknown one is an error, not zero hits.</summary>
    private static readonly string[] Kinds = ["example", "class", "token", "page"];

    /// <summary>The sections <c>get_integration_guide</c> serves.</summary>
    private static readonly string[] Sections = ["host-page", "branding", "javascript", "rules"];

    [McpServerTool(Name = "search", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
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
        // An unknown kind used to return zero hits, which reads as "the catalogue has
        // none of those" rather than "that is not a kind".
        if (kind is not null && !Kinds.Contains(kind, StringComparer.OrdinalIgnoreCase))
            throw new McpException(
                $"Unknown kind \"{kind}\". Use one of: {string.Join(", ", Kinds)} — "
                + "or omit it to search everything.");

        kind = kind?.ToLowerInvariant();

        var terms = CatalogueRanker.Terms(query).ToList();
        var hits = new List<Hit>();

        if (kind is null or "example")
        {
            foreach (var example in index.Examples)
            {
                // The ids sit in the short fields beside the classes: an example is
                // asked for by the thing it is about, and for a host-page snippet that
                // thing is an id rather than a class.
                var score = CatalogueRanker.Score(terms, example.Id,
                    [example.Title, example.Page, string.Join(' ', example.Classes),
                     string.Join(' ', example.Ids)],
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
        OpenWorld = false)]
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
        if (ids is null || ids.Length == 0)
            throw new McpException(
                "ids is required: one or more example ids from search, e.g. [\"Badge/Semantic\"].");

        if (ids.Length > MaxExamples)
            throw new McpException(
                $"At most {MaxExamples} ids per call; {ids.Length} were requested. "
                + "Split them across calls, or use get_page for every example on one page.");

        var found = ids.Select(index.Example).OfType<IndexedExample>().ToList();
        var missing = ids.Where(id => index.Example(id) is null).ToList();

        return new
        {
            meta = versions.For(installedVersion, found.SelectMany(Reported)),
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
                csharp = e.Members,
                since = Since(SinceExample(e)),
            }),
            notFound = missing,
        };
    }

    [McpServerTool(Name = "describe_class", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
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
        if (names is null || names.Length == 0)
            throw new McpException(
                "names is required: one or more class names, with or without the leading dot.");

        if (names.Length > MaxClasses)
            throw new McpException(
                $"At most {MaxClasses} names per call; {names.Length} were requested. "
                + "Split them across calls.");

        var found = names.Select(index.Class).OfType<IndexedClass>().ToList();
        var missing = names.Where(n => index.Class(n) is null).ToList();

        // An id the stylesheet declares, asked of a tool that indexes classes. Answering
        // `notFound` read as "the library does not style that" and cost an app twenty
        // lines of its own CSS for a rule the sheet already ships, so the rules come
        // back — under their own key, because they are not classes.
        var others = missing
            .Select(index.IdSelector).OfType<IndexedClass>()
            .DistinctBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

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
            otherSelectors = others.Select(c => new
            {
                name = "#" + c.Name,
                layer = c.Layer,
                declarations = c.Declarations,
                usedByExamples = c.UsedByExamples,
            }),
            notFound = missing,
            notes = missing.Select(n => new { name = n, note = WhyNotAClass(n) }),
        };
    }

    /// <summary>Why a name came back in <c>notFound</c>, in one sentence.</summary>
    /// <remarks>
    /// Silence is the expensive answer here. An empty result reads as a statement about
    /// the library — "there is no such thing" — when it is a statement about the tool,
    /// and an agent acting on the first writes markup that does nothing.
    /// </remarks>
    private string WhyNotAClass(string name)
    {
        if (name.StartsWith('#'))
            return index.IdSelector(name) is not null
                ? "An id, not a class — this tool indexes classes. The stylesheet does declare it: "
                  + "its rules are under otherSelectors."
                : "An id, not a class — this tool indexes classes, and the stylesheet declares no "
                  + "rule for this id either.";

        if (name.IndexOfAny(SelectorPunctuation) >= 0)
            return "A selector, not a class name — pass one class at a time, without the dot or "
                   + "anything around it.";

        return "No class of that name is declared. Try search, which also matches a class by what "
               + "its rules say.";
    }

    [McpServerTool(Name = "get_page", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
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
                    section = p.Section,
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
                csharp = e.Members,
                since = Since(SinceExample(e)),
            }),
        };
    }

    [McpServerTool(Name = "get_tokens", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false)]
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
        Idempotent = true, OpenWorld = false)]
    [Description("""
        How to wire Sedna.UI into an app: the host page with its load order, the branding
        recipe, the JavaScript and C# surface, and the rules a consuming app follows. Call once
        when integrating, not per page.
        """)]
    public object GetIntegrationGuide(
        [Description("One of: host-page, branding, javascript, rules. Omit for host-page.")]
        string? section = null)
    {
        var name = section?.ToLowerInvariant() ?? "host-page";
        var markdown = name switch
        {
            "host-page" => Docs.Read("getting-started.md"),
            "branding" => Docs.Read("getting-started.md"),
            "javascript" => Docs.Read("architecture.md"),
            "rules" => Docs.Read("CLAUDE.consuming-app.md"),
            _ => throw new McpException(
                $"Unknown section \"{section}\". Use one of: {string.Join(", ", Sections)} — "
                + "or omit it for host-page."),
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
        // From the example itself, never from hit.Classes: those are capped at eight
        // for the result list, and a floor computed from a truncated list is an
        // understated one — the direction that gets an agent to copy something its
        // app does not have.
        "example" => index.Example(hit.Ref) is { } example ? SinceExample(example) : null,
        _ => versions.LatestRelease,
    };

    /// <summary>The release an example's content needs — its classes and its C# both.</summary>
    private string? SinceExample(IndexedExample example) =>
        versions.SinceExample(example.Id, example.Classes, example.Members);

    /// <summary>
    /// Everything one example uses, named the way a warning should name it, with the
    /// release each first shipped in.
    /// </summary>
    private IEnumerable<(string Name, string? Since)> Reported(IndexedExample example) =>
        example.Classes.Select(c => ("." + c, versions.SinceClass(c)))
            .Concat(example.Members.Select(m => (m, versions.SinceMember(m))));

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

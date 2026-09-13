using Sedna.UI;
using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// An example file is copied verbatim by readers, so what is on disk has to be
/// both valid Razor and valid HTML at the same time.
/// </summary>
/// <remarks>
/// <para>
/// The rule is one sentence: <b>an example <c>.razor</c> file contains plain HTML
/// and no Razor syntax whatsoever.</b> Then the bytes on disk are the bytes
/// compiled, the bytes rendered and the bytes printed, so nothing is escaped and
/// nothing needs unescaping.
/// </para>
/// <para>
/// The alternative — printing the source with <c>@@</c> collapsed to <c>@</c> —
/// produces a snippet that is valid HTML and invalid Razor, which fails the whole
/// point. So the escape is banned rather than undone.
/// </para>
/// <para>
/// Page prose is different and stays different: <c>&lt;code&gt;@@layer&lt;/code&gt;</c>
/// in a page is correct, renders as <c>@layer</c>, and nobody copies it. The rule
/// is that <c>@@</c> belongs in a page, never in an example.
/// </para>
/// </remarks>
public class ExampleSourceTests
{
    // An e-mail address is the one place Razor does not treat @ as a transition,
    // in content and in an attribute alike — so it is the one @ an example may
    // contain. Everything else is a directive, an escape or an expression.
    private static readonly Regex Email =
        new(@"(?<=[A-Za-z0-9._%+-])@(?=[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,})",
            RegexOptions.Compiled);

    private static readonly Regex RemoteSubresource =
        new("""(?:href|src)\s*=\s*["'](?:https?:)?//""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static TheoryData<string> LiveExamples() => Files("*.razor");
    public static TheoryData<string> AllExamples() => Files("*");

    /// <summary>
    /// The one folder where an example is allowed to be Razor.
    /// </summary>
    /// <remarks>
    /// An example that demonstrates the C# wrappers or the active-link helper
    /// cannot be plain HTML — the thing being shown <i>is</i> C#. Razor is then the
    /// correct form for a reader to copy, so the exemption is real rather than a
    /// loophole. It is a named folder so it cannot be reached for by accident, and
    /// <see cref="Only_an_interop_example_contains_Razor_syntax"/> asserts from
    /// both sides that it is used for nothing else.
    /// </remarks>
    private const string InteropFolder = "Interop";

    private static bool IsInterop(string path) =>
        path.StartsWith(InteropFolder + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    [Theory]
    [MemberData(nameof(LiveExamples))]
    public void A_live_example_contains_no_Razor_syntax(string path)
    {
        if (IsInterop(path)) return;

        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path));

        var allowed = Email.Matches(source).Select(m => m.Index).ToHashSet();
        var offenders = new List<string>();

        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] != '@' || allowed.Contains(i)) continue;
            offenders.Add($"line {Line(source, i)}: …{Around(source, i)}…");
        }

        Assert.True(offenders.Count == 0,
            $"""
             {path} contains Razor syntax. An example is copied verbatim, so it has to be
             plain HTML — an "@" that is not part of an e-mail address is a directive, an
             escape or an expression, and it would be copied literally.

             {string.Join("\n", offenders)}
             """);

        // Razor's whitespace-control element. Legal, compiles, and prints as
        // something no reader should paste.
        Assert.DoesNotContain("<text>", source, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// One remote host, and only for a photograph.
    /// </summary>
    /// <remarks>
    /// A live example's markup is fetched for real, so a URL in one is a genuine runtime
    /// dependency — and the package's guarantee is that it has none, which an example must
    /// not teach otherwise. The single exception is
    /// <see cref="CatalogueAssets.PhotoHost"/>, because a page about images cannot show how
    /// an <c>&lt;img&gt;</c> is handled without one, and no amount of CSS-drawn placeholder
    /// teaches <c>object-fit</c>. It is the catalogue's dependency, never the library's.
    /// </remarks>
    [Theory]
    [MemberData(nameof(AllExamples))]
    public void No_example_loads_from_a_remote_host_other_than_the_photo_host(string path)
    {
        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path))
            .Replace("https://" + CatalogueAssets.PhotoHost, "«photo»", StringComparison.OrdinalIgnoreCase);

        Assert.False(RemoteSubresource.IsMatch(source),
            $"{path} loads a subresource from a remote host. Everything the package needs "
            + $"ships inside it, and the one host an example may reach is {CatalogueAssets.PhotoHost}.");
    }

    [Fact]
    public void No_code_only_snippet_is_named_razor()
    {
        // Belt and braces: the SDK's own Content glob would sweep it into
        // RazorComponent long before this test ran. It is here because the failure
        // it guards — a snippet silently becoming a compiled component — reads as
        // a mysterious compile error rather than a naming mistake.
        foreach (var file in Directory.EnumerateFiles(
                     CatalogueAssets.ExamplesDir, "*.razor", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.False(text.Contains("<script", StringComparison.OrdinalIgnoreCase),
                $"{Path.GetFileName(file)} contains a <script>. A snippet that is not live "
                + "markup for its page is a .html/.css/.txt file rendered by CatSnippet.");
        }
    }

    [Theory]
    [MemberData(nameof(LiveExamples))]
    public void Only_an_interop_example_contains_Razor_syntax(string path)
    {
        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path));
        var isRazor = source.Contains("@code", StringComparison.Ordinal)
                      || source.Contains("@inject", StringComparison.Ordinal);

        // Both directions. Without the second, Examples/Interop/ becomes a place to
        // put an ordinary example that failed the scan.
        Assert.Equal(IsInterop(path), isRazor);
    }

    [Theory]
    [MemberData(nameof(AllExamples))]
    public void No_example_names_a_real_organisation_person_or_host(string path)
    {
        // An example is read by anyone browsing the site and pasted into their own
        // application, so the names in it are part of what the library teaches. The
        // demo identity is Alex Fischer at example.com (RFC 2606), hosts come from
        // the documentation ranges (RFC 5737), and no real company, account or
        // product belongs in any of it.
        //
        // Shapes, and no literal deny-list. There used to be one and it was itself a
        // published list of the names it forbade, in a public repository — the
        // disclosure it existed to prevent. The `No real names` section of the repo's
        // CLAUDE.md is the rule now; this is the part of it a test can check, and it
        // matches the FORM of a real record, host, address or company so that no
        // customer has to be named to guard against naming one.
        //
        // The library side of this is Sedna.UI.Tests/TestSupport/RealWorldShapes.cs,
        // over the shipped stylesheet and script. Two copies rather than a shared
        // project reference, because `dotnet test src/Sedna.UI.Tests` has to pass with
        // this project deleted; keep the two in step by hand when either grows.
        string[] forbiddenPatterns =
        [
            @"INC\d{4,}", @"\bCHG\d{4,}", @"\bREQ\d{4,}",
            // The separated forms of the same shapes. `INC-204471` sat in an example
            // for months because the bare pattern above wants the digits to touch the
            // prefix.
            @"\b(?:INC|CHG|REQ|SR|PRB|TASK)[-_ ]\d{3,}",
            // A German public body or a company's legal form. A shape rather than a
            // name, which is the only way to guard this class without publishing a
            // customer's: three real authorities were in the examples, and each was
            // recognisable by the word in front of the place.
            @"\b(?:Kreisverwaltung|Verbandsgemeinde|Landratsamt|Bezirksamt|Stadtwerke)\b",
            @"\b(?:GmbH|gGmbH|mbH|KGaA|OHG)\b",
            // An internal DNS label, including one hiding under a reserved domain —
            // `exch-02.corp.example` is RFC 2606 on the right and internal on the left.
            @"\.(?:internal|intern|corp|lan)\b",
            // Private and link-local ranges. A real-looking internal address in a
            // copy-pasteable field reads as a real system's address.
            @"\b10\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
            @"\b192\.168\.\d{1,3}\.\d{1,3}\b",
            @"\b172\.(1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}\b",
        ];

        // The library's own published host and repository are not somebody else's
        // naming — an MCP config example has to name the server it connects to.
        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path))
            .Replace("www.sedna-ui.com", "«site»", StringComparison.OrdinalIgnoreCase)
            .Replace("github.com/dennisrahmen", "«repo»", StringComparison.OrdinalIgnoreCase);

        var found = forbiddenPatterns
            .SelectMany(p => Regex.Matches(source, p, RegexOptions.IgnoreCase).Select(m => m.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        Assert.True(found.Count == 0,
            $"{path} names something real: {string.Join(", ", found)}. Use the demo identity "
            + "(Alex Fischer, alex.fischer@example.com) and a documentation address (192.0.2.x).");
    }

    [Theory]
    [MemberData(nameof(AllExamples))]
    public void No_example_references_an_asset_the_reader_does_not_have(string path)
    {
        // A relative src resolves against the app that pasted the markup, so
        // `logo.png` is a 404 everywhere except this site. Anything an example needs
        // to look right is drawn in CSS, comes from the bundled icon font, or — for a
        // photograph, which is neither — from CatalogueAssets.PhotoHost, an absolute
        // URL that resolves the same wherever the markup is pasted.
        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path));

        var offenders = Regex.Matches(source, """(?:src|srcset)\s*=\s*["']([^"']+)["']""",
                RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value)
            .Where(v => !v.StartsWith("_content/Sedna.UI/", StringComparison.OrdinalIgnoreCase)
                        && !v.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                        && !v.StartsWith("https://" + CatalogueAssets.PhotoHost, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"{path} loads {string.Join(", ", offenders)}, which a reader who pasted this markup "
            + "does not have. Draw the placeholder in CSS, use a Remix Icon, or — for a real "
            + $"photograph — an absolute URL on {CatalogueAssets.PhotoHost}.");
    }

    [Theory]
    [MemberData(nameof(AllExamples))]
    public void A_fragment_link_in_an_example_names_an_id_in_that_example(string path)
    {
        // A bare fragment works wherever the markup is pasted only when its target is
        // pasted with it: `#vs-email` beside `id="vs-email"`. One naming a heading of
        // the catalogue page — `#sizes` — points at nothing in the reader's app, and
        // pointed at nothing here either, so a click opened the landing page. `#`
        // alone is the placeholder.
        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path));

        var ids = Regex.Matches(source, """\bid\s*=\s*"([^"]+)""")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        // A <use> naming one of the shipped drawings is the one fragment that resolves
        // in every app, because SednaStateArt puts those ids on every page.
        foreach (Match m in Regex.Matches(SednaStateArt.Sprite, "<symbol id=\"([a-z-]+)\""))
            ids.Add(m.Groups[1].Value);

        var dangling = Regex.Matches(source, """href\s*=\s*"#([^"]+)""")
            .Select(m => m.Groups[1].Value)
            .Where(id => !ids.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(dangling.Count == 0,
            $"{path} links to #{string.Join(", #", dangling)}, which no element in the example "
            + "carries. Link to an id the example declares, or use href=\"#\" as a placeholder.");
    }

    [Fact]
    public void Every_example_folder_is_a_valid_csharp_identifier()
    {
        // MSBuild builds the manifest name from the folder path and the Razor SDK
        // builds the component namespace from the same path, by different code.
        // A hyphen desynchronises them, and the symptom is a blank code block on
        // one page rather than a build error.
        foreach (var dir in Directory.EnumerateDirectories(
                     CatalogueAssets.ExamplesDir, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(dir);
            Assert.Matches("^[A-Za-z_][A-Za-z0-9_]*$", name);
        }
    }

    private static TheoryData<string> Files(string pattern)
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.EnumerateFiles(
                     CatalogueAssets.ExamplesDir, pattern, SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            data.Add(Path.GetRelativePath(CatalogueAssets.ExamplesDir, file));
        }

        // A glob that matches nothing makes every assertion above pass vacuously.
        Assert.NotEmpty(data);
        return data;
    }

    private static int Line(string text, int index) =>
        text.Take(index).Count(c => c == '\n') + 1;

    private static string Around(string text, int index) =>
        text[Math.Max(0, index - 12)..Math.Min(text.Length, index + 14)]
            .Replace("\n", "⏎", StringComparison.Ordinal);
}

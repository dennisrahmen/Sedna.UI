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

    // One test per rule over every example file, not one result per file and rule: a rule
    // that fails names every offending file in one message, and a thousand files do not
    // become thousands of results to discover and report.
    private static IReadOnlyList<string> LiveExamples() => Files("*.razor");
    private static IReadOnlyList<string> AllExamples() => Files("*");

    private static string Read(string path) =>
        File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path));

    private static void AssertNone(IEnumerable<string> problems, string rule)
    {
        var list = problems.ToList();
        Assert.True(list.Count == 0, $"{rule}\n\n{string.Join("\n\n", list)}");
    }

    /// <summary>
    /// The one folder where an example is allowed to be Razor.
    /// </summary>
    /// <remarks>
    /// An example that demonstrates the C# surface — the services, the state helpers, a
    /// binding — cannot be plain HTML: the thing being shown <i>is</i> C#. Razor is then the
    /// correct form for a reader to copy, so the exemption is real rather than a loophole. It
    /// is a named folder so it cannot be reached for by accident, and
    /// <see cref="Only_an_interop_example_contains_Razor_syntax"/> asserts from both sides that
    /// it is used for nothing else.
    /// </remarks>
    private static readonly string[] RazorFolders = ["Interop"];

    private static bool IsInterop(string path) =>
        RazorFolders.Any(f => path.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    [Fact]
    public void A_live_example_contains_no_Razor_syntax()
    {
        var problems = new List<string>();
        foreach (var path in LiveExamples().Where(p => !IsInterop(p)))
        {
            var source = Read(path);
            var allowed = Email.Matches(source).Select(m => m.Index).ToHashSet();

            var offenders = new List<string>();
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] != '@' || allowed.Contains(i)) continue;
                offenders.Add($"  line {Line(source, i)}: …{Around(source, i)}…");
            }
            if (offenders.Count > 0)
                problems.Add($"{path}\n{string.Join("\n", offenders)}");

            // Razor's whitespace-control element. Legal, compiles, and prints as
            // something no reader should paste.
            if (source.Contains("<text>", StringComparison.OrdinalIgnoreCase))
                problems.Add($"{path} contains <text>, Razor's whitespace-control element.");
        }

        AssertNone(problems,
            """
            An example is copied verbatim, so it has to be plain HTML — an "@" that is not part
            of an e-mail address is a directive, an escape or an expression, and it would be
            copied literally. Remove it from:
            """);
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
    [Fact]
    public void No_example_loads_from_a_remote_host_other_than_the_photo_host()
    {
        var offenders = AllExamples().Where(path => RemoteSubresource.IsMatch(
            Read(path).Replace("https://" + CatalogueAssets.PhotoHost, "«photo»", StringComparison.OrdinalIgnoreCase)));

        AssertNone(offenders.Select(p => "  " + p),
            "An example loads a subresource from a remote host. Everything the package needs "
            + $"ships inside it, and the one host an example may reach is {CatalogueAssets.PhotoHost}. Offending files:");
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

    [Fact]
    public void Only_an_interop_example_contains_Razor_syntax()
    {
        // Both directions. Without the second, Examples/Interop/ becomes a place to
        // put an ordinary example that failed the scan.
        var wrong = LiveExamples().Where(path =>
        {
            var source = Read(path);
            var isRazor = source.Contains("@code", StringComparison.Ordinal)
                          || source.Contains("@inject", StringComparison.Ordinal)
                          || source.Contains("@bind", StringComparison.Ordinal);
            return IsInterop(path) != isRazor;
        });

        AssertNone(wrong.Select(p => "  " + p),
            "Razor syntax (@code, @inject, @bind) belongs in Examples/Interop/ and nowhere else, "
            + "and every file there uses it. Wrong place or wrong form:");
    }

    [Fact]
    public void No_example_names_a_real_organisation_person_or_host()
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

        var problems = new List<string>();
        foreach (var path in AllExamples())
        {
            // The library's own published host and repository are not somebody else's
            // naming — an MCP config example has to name the server it connects to.
            var source = Read(path)
                .Replace("www.sedna-ui.com", "«site»", StringComparison.OrdinalIgnoreCase)
                .Replace("github.com/dennisrahmen", "«repo»", StringComparison.OrdinalIgnoreCase);

            var found = forbiddenPatterns
                .SelectMany(p => Regex.Matches(source, p, RegexOptions.IgnoreCase).Select(m => m.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (found.Count > 0) problems.Add($"  {path}: {string.Join(", ", found)}");
        }

        AssertNone(problems,
            "An example names something real. Use the demo identity (Alex Fischer, "
            + "alex.fischer@example.com) and a documentation address (192.0.2.x). Found in:");
    }

    [Fact]
    public void No_example_references_an_asset_the_reader_does_not_have()
    {
        // A relative src resolves against the app that pasted the markup, so
        // `logo.png` is a 404 everywhere except this site. Anything an example needs
        // to look right is drawn in CSS, comes from the bundled icon font, or — for a
        // photograph, which is neither — from CatalogueAssets.PhotoHost, an absolute
        // URL that resolves the same wherever the markup is pasted.
        // The src attribute itself, not one whose name ends in it: `data-graph-src` names the
        // app's own endpoint, which the reader writes, and is no asset of this site's. In a C#
        // example a value that is a Razor expression — `src="@_picture"` — is whatever the
        // component computes, a data: URL from ExportAsync, and names no file either. A PDF a
        // frame shows (/pdf) is the app's own document, which the reader names, like the endpoint.
        var problems = new List<string>();
        foreach (var path in AllExamples())
        {
            var offenders = Regex.Matches(Read(path), """(?<![\w-])(?:src|srcset)\s*=\s*["']([^"']+)["']""",
                    RegexOptions.IgnoreCase)
                .Select(m => m.Groups[1].Value)
                .Where(v => !v.StartsWith("_content/Sedna.UI/", StringComparison.OrdinalIgnoreCase)
                            && !v.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                            && !v.StartsWith("https://" + CatalogueAssets.PhotoHost, StringComparison.OrdinalIgnoreCase)
                            && !(IsInterop(path) && v.StartsWith('@'))
                            && !Regex.IsMatch(v, @"\.pdf(?:#.*)?$", RegexOptions.IgnoreCase))
                .ToList();

            if (offenders.Count > 0) problems.Add($"  {path}: {string.Join(", ", offenders)}");
        }

        AssertNone(problems,
            "An example loads a file a reader who pasted the markup does not have. Draw the "
            + "placeholder in CSS, use a Remix Icon, or — for a real photograph — an absolute URL "
            + $"on {CatalogueAssets.PhotoHost}. Found in:");
    }

    [Fact]
    public void A_fragment_link_in_an_example_names_an_id_in_that_example()
    {
        // A bare fragment works wherever the markup is pasted only when its target is
        // pasted with it: `#vs-email` beside `id="vs-email"`. One naming a heading of
        // the catalogue page — `#sizes` — points at nothing in the reader's app, and
        // pointed at nothing here either, so a click opened the landing page. `#`
        // alone is the placeholder.
        // A <use> naming one of the shipped drawings is the one fragment that resolves
        // in every app, because SednaStateArt and SednaMochi put those ids on the page.
        // `#top` with no element of that id is the top of the page, to a browser and to
        // 29-fragment.js alike, so it resolves wherever the markup is pasted.
        var shipped = new HashSet<string>(StringComparer.Ordinal) { "top" };
        foreach (var sprite in new[] { SednaStateArt.Sprite, SednaMochi.Sprite })
            foreach (Match m in Regex.Matches(sprite, "<symbol id=\"([a-z-]+)\""))
                shipped.Add(m.Groups[1].Value);

        var problems = new List<string>();
        foreach (var path in AllExamples())
        {
            var source = Read(path);
            var ids = Regex.Matches(source, """\bid\s*=\s*"([^"]+)""")
                .Select(m => m.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);

            // A <use> naming one of the shipped drawings is the one fragment that resolves
            // in every app, because SednaStateArt and SednaMochi put those ids on the page.
            ids.UnionWith(shipped);

            var dangling = Regex.Matches(source, """href\s*=\s*"#([^"]+)""")
                .Select(m => m.Groups[1].Value)
                .Where(id => !ids.Contains(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            if (dangling.Count > 0) problems.Add($"  {path}: #{string.Join(", #", dangling)}");
        }

        AssertNone(problems,
            "A fragment link names an id no element in its example carries. Link to an id the "
            + "example declares, or use href=\"#\" as a placeholder. Found in:");
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

    private static IReadOnlyList<string> Files(string pattern)
    {
        var data = Directory.EnumerateFiles(CatalogueAssets.ExamplesDir, pattern, SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(CatalogueAssets.ExamplesDir, file))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

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

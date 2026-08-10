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

    [Theory]
    [MemberData(nameof(AllExamples))]
    public void No_example_loads_anything_from_a_remote_host(string path)
    {
        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path));

        // A live example's markup is fetched for real, so a CDN URL there is a
        // genuine runtime dependency. A code-only one is held to the same rule
        // because documenting a remote asset contradicts the package's own
        // guarantee that nothing is loaded from a remote host.
        Assert.False(RemoteSubresource.IsMatch(source),
            $"{path} loads a subresource from a remote host. Everything the package needs "
            + "ships inside it, and an example must not teach otherwise.");
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
        string[] forbidden =
        [
            "athene", "netpoint", "servicenow", "SD-Network", "gsearch", "n8n",
            "rahmen", "np-console",
        ];
        string[] forbiddenPatterns =
        [
            @"INC\d{4,}", @"\bCHG\d{4,}", @"\bREQ\d{4,}",
            // Private and link-local ranges. A real-looking internal address in a
            // copy-pasteable field reads as a real system's address.
            @"\b10\.\d{1,3}\.\d{1,3}\.\d{1,3}\b",
            @"\b192\.168\.\d{1,3}\.\d{1,3}\b",
            @"\b172\.(1[6-9]|2\d|3[01])\.\d{1,3}\.\d{1,3}\b",
        ];

        // The library's own published host and repository are not app-specific
        // naming — an MCP config example has to name the server it connects to.
        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path))
            .Replace("www.sedna-ui.com", "«site»", StringComparison.OrdinalIgnoreCase)
            .Replace("github.com/dennisrahmen", "«repo»", StringComparison.OrdinalIgnoreCase);

        var found = forbidden
            .Where(f => source.Contains(f, StringComparison.OrdinalIgnoreCase))
            .Concat(forbiddenPatterns
                .SelectMany(p => Regex.Matches(source, p, RegexOptions.IgnoreCase).Select(m => m.Value)))
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
        // to look right is drawn in CSS or comes from the bundled icon font.
        var source = File.ReadAllText(Path.Combine(CatalogueAssets.ExamplesDir, path));

        var offenders = Regex.Matches(source, """(?:src|srcset)\s*=\s*["']([^"']+)["']""",
                RegexOptions.IgnoreCase)
            .Select(m => m.Groups[1].Value)
            .Where(v => !v.StartsWith("_content/Sedna.UI/", StringComparison.OrdinalIgnoreCase)
                        && !v.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(offenders.Count == 0,
            $"{path} loads {string.Join(", ", offenders)}, which a reader who pasted this markup "
            + "does not have. Draw the placeholder in CSS or use a Remix Icon.");
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

using System.Text.RegularExpressions;

using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// Every rendered code block carries text, and every live example rendered
/// something.
/// </summary>
/// <remarks>
/// <para>
/// This is the guard for the failure mode the whole example mechanism is exposed
/// to: the manifest resource name is derived by MSBuild from <c>RootNamespace</c>
/// plus a folder path, the component namespace is derived by the Razor SDK from
/// the same inputs by different code, and <c>ExampleSource</c> assumes they agree.
/// When they do not, the symptom is a blank code block on one page — not a build
/// error, and not something any source scan can see.
/// </para>
/// <para>
/// Mutation-tested by breaking the <c>EmbeddedResource</c> glob. The failure
/// surfaces as a <b>500 on the affected route</b> rather than as a blank block,
/// because <c>ExampleSource</c> throws with a diagnostic naming the glob and the
/// folder-name rule — which is the better failure, and is why <c>Get</c> asserts
/// the status. The empty-body assertions below cover the remaining case: a source
/// that is present but empty.
/// </para>
/// <para>
/// No browser: the examples and their code blocks are server-rendered, which is
/// the invariant the app is built to. Asserting it over plain HTTP is both cheaper
/// and a stronger statement than asserting it in a browser would be.
/// </para>
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class ExampleRenderTests(CatalogueAppFixture app)
{
    // `code-block[^"]*`, not `code-block`: a long snippet also carries
    // `code-block--clamped`, and an exact class match silently stopped seeing every
    // block that had one — which reads as "the catalogue renders no code" rather than
    // as a brittle regex.
    private static readonly Regex CodeBlock =
        new("""<div class="code-block[^"]*">.*?<pre[^>]*><code>(?<body>.*?)</code></pre>""",
            RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex Demo =
        new("""<div class="ex-demo[^"]*">(?<body>.*?)</div>\s*<div class="code-block">""",
            RegexOptions.Compiled | RegexOptions.Singleline);

    // The routes the app serves, not the registry — NavigationTests is what asserts
    // those two agree, and this test should say something true while they do not.
    public static TheoryData<string> Routes() => RoutedPages.AsTheoryData();

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Every_code_block_on_the_page_has_a_body(string route)
    {
        var html = await Get(route);

        var blocks = CodeBlock.Matches(html);
        foreach (var block in blocks.Cast<Match>())
        {
            Assert.False(string.IsNullOrWhiteSpace(block.Groups["body"].Value),
                $"{route} rendered an empty code block. Either an example's embedded source is "
                + "missing — check the EmbeddedResource glob and the folder names under "
                + "Examples/ — or a CatSnippet Name does not match a file.");
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task Every_live_example_rendered_something(string route)
    {
        var html = await Get(route);

        foreach (var demo in Demo.Matches(html).Cast<Match>())
        {
            Assert.False(string.IsNullOrWhiteSpace(demo.Groups["body"].Value),
                $"{route} rendered an empty demo. The example component produced no markup.");
        }
    }

    [Fact]
    public async Task The_catalogue_renders_code_blocks_at_all()
    {
        // The vacuity guard. Every assertion above passes on a page with no code
        // blocks, and a regex that silently stops matching would look identical to
        // a clean run.
        var found = 0;
        foreach (var route in RoutedPages.All)
        {
            found += CodeBlock.Matches(await Get(route)).Count;
        }

        Assert.True(found >= 5, $"Only {found} code blocks rendered across the whole catalogue.");
    }

    private async Task<string> Get(string route)
    {
        var response = await app.Client.GetAsync(new Uri(route, UriKind.Relative));
        Assert.True(response.IsSuccessStatusCode, $"{route} returned {(int)response.StatusCode}.");
        return await response.Content.ReadAsStringAsync();
    }
}

using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// An example belongs to the page that renders it. The C# examples are filed in
/// <c>Examples/Interop/</c> and rendered on several pages, and the MCP server's snippets in
/// <c>Examples/Mcp/</c> for a page routed <c>/mcp-server</c>; filed by folder, those pages
/// answered <c>get_page</c> with no examples at all.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class McpPageTests(CatalogueAppFixture app)
{
    private McpClient Mcp => new(app.Client);

    private static readonly Regex Tag = new(@"<Cat(Example|Snippet)\s[^>]*\bTitle=""", RegexOptions.Compiled);
    private static readonly Regex Route = new(@"^@page\s+""(?<route>[^""]+)""", RegexOptions.Multiline | RegexOptions.Compiled);

    public static TheoryData<string> PagesWithExamples()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Assets.RepoRoot, "src", "Sedna.UI.Catalogue", "Components", "Pages"), "*.razor"))
        {
            var source = File.ReadAllText(file);
            if (Route.Match(source) is { Success: true } m && Tag.IsMatch(source)) data.Add(m.Groups["route"].Value);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(PagesWithExamples))]
    public async Task A_page_that_renders_examples_reports_them(string route)
    {
        var page = await Mcp.Tool("get_page", new { id = route });
        Assert.True(page.GetProperty("examples").GetArrayLength() > 0, $"get_page(\"{route}\") reports no examples.");
    }

    [Theory]
    [InlineData("/graph-blazor", "Interop/GraphFromCsharp")]
    [InlineData("/mcp-server", "Mcp/Config")]
    [InlineData("/card", "Interop/CardFoldFromCsharp")]
    public async Task An_example_is_filed_under_the_page_that_renders_it(string route, string id)
    {
        var page = await Mcp.Tool("get_page", new { id = route });
        Assert.Contains(page.GetProperty("examples").EnumerateArray(), e => e.GetProperty("id").GetString() == id);

        var example = (await Mcp.Tool("get_example", new { ids = new[] { id } })).GetProperty("examples")[0];
        Assert.Equal(route, example.GetProperty("url").GetString());
    }
}

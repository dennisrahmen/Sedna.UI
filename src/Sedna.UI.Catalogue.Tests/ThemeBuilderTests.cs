using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Catalogue.Theming;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// <c>/theme-builder</c>: the <c>brand.css</c> it generates, and the page that shows it.
/// </summary>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public class ThemeBuilderTests(CatalogueAppFixture app)
{
    private static readonly Regex Declared = new(@"(?:^|[{;])\s*(--[\w-]+)\s*:", RegexOptions.Compiled | RegexOptions.Multiline);

    private static HashSet<string> Names(string css) =>
        Declared.Matches(Assets.StripComments(css)).Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    [Theory]
    [InlineData("#16a34a", null)]
    [InlineData("#2563eb", "#0ea5b7")]
    [InlineData("#7c3aed", "#f59e0b")]
    [InlineData(BrandCssBuilder.SednaRed, BrandCssBuilder.OrbitBlue)]
    public void The_generated_css_sets_only_tokens_the_library_declares(string brand, string? accent)
    {
        var generated = Names(BrandCssBuilder.Build(brand, accent).Css);
        var library = Names(Assets.Css);

        Assert.NotEmpty(generated);
        Assert.Contains("--coral-600", generated);
        var unknown = generated.Except(library).OrderBy(n => n, StringComparer.Ordinal).ToList();
        Assert.True(unknown.Count == 0, "The generated brand.css sets tokens the library does not declare: " + string.Join(", ", unknown));
    }

    [Fact]
    public void A_different_brand_colour_gives_a_different_brand_css()
    {
        var sedna = BrandCssBuilder.Build(BrandCssBuilder.SednaRed, null);
        var green = BrandCssBuilder.Build("#16a34a", null);

        Assert.NotEqual(sedna.Css, green.Css);
        Assert.Null(green.Problem);
        // The step --brand points at carries white text, as the built-in themes' does.
        Assert.True(Colour.Contrast("#ffffff", Token(green, "--coral-600")) >= 4.5);
        // The accent is left alone until it is asked for.
        Assert.DoesNotContain("--orbit-", green.Css, StringComparison.Ordinal);
        Assert.Contains("--orbit-400", BrandCssBuilder.Build("#16a34a", "#0ea5b7").Css, StringComparison.Ordinal);
    }

    [Fact]
    public void Sedna_red_gives_the_librarys_own_ramp()
    {
        var sedna = BrandCssBuilder.Build(BrandCssBuilder.SednaRed, null);

        Assert.Equal("#d73f1a", Token(sedna, "--coral-600"));
        Assert.Equal("#ff6b4a", Token(sedna, "--coral-500"));
        Assert.All(sedna.Checks, c => Assert.True(c.Passes, $"{c.Pair}: {c.MeasuredText} against {c.FloorText}"));
    }

    [Fact]
    public void A_brand_that_lands_on_a_semantic_family_fails_the_hue_check()
    {
        // Crimson, the danger family's own hue: brand and danger would read as one.
        var result = BrandCssBuilder.Build("#c72268", null);

        Assert.Contains(result.Checks, c => !c.Passes && c.Pair.Contains("danger", StringComparison.Ordinal));
    }

    [Fact]
    public void A_value_that_is_no_colour_is_refused_and_said_so()
    {
        var result = BrandCssBuilder.Build("coral", null);

        Assert.Equal(BrandCssBuilder.SednaRed, result.Brand);
        Assert.NotNull(result.Problem);
    }

    [Fact]
    public async Task The_page_renders_the_brand_css_it_applies()
    {
        var html = await app.Client.GetStringAsync("/theme-builder");

        // Applied to the page, and printed for copying: the same text in both places.
        Assert.Matches(new Regex(@"<style>[^<]*--coral-600: #d73f1a;[^<]*</style>"), html);
        Assert.Contains("id=\"theme-css\"", html, StringComparison.Ordinal);
        Assert.Contains("data-copy-target", html, StringComparison.Ordinal);
        Assert.Contains("type=\"color\"", html, StringComparison.Ordinal);
    }

    private static string Token(ThemeResult result, string name) =>
        result.Tokens.Single(t => t.Key == name).Value;
}

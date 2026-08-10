using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Sedna.UI.Tests;

/// <summary>
/// The component is the ergonomic default over <see cref="SednaUiBrand.ToCss"/>: it reads
/// <see cref="SednaUiOptions"/> from DI itself, so the host page writes one tag.
/// </summary>
public class SednaBrandStyleTests : BunitContext
{
    [Fact]
    public void Renders_one_style_element_containing_ToCss_output()
    {
        var options = new SednaUiOptions();
        Services.AddSingleton(options);

        var cut = Render<SednaBrandStyle>();

        var style = cut.Find("style");
        Assert.Equal(SednaUiBrand.ToCss(options), style.TextContent);
    }

    [Fact]
    public void A_second_registered_theme_reaches_the_rendered_markup()
    {
        var forest = new SednaTheme("forest", SednaTheme.Sedna.Palette);
        var options = new SednaUiOptions { Themes = [SednaTheme.Sedna, forest], Default = "sedna" };
        Services.AddSingleton(options);

        var cut = Render<SednaBrandStyle>();

        Assert.Contains("[data-theme=\"forest\"]", cut.Markup, StringComparison.Ordinal);
    }
}

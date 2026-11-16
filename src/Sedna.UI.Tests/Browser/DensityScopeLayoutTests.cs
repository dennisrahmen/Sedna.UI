using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>data-density</c> on a part of the page: a compact list of row tools gets the compact small
/// tier, 24px, on a default page, and a part marked comfortable keeps 28px on a compact page.
/// </summary>
public class DensityScopeLayoutTests : ScriptTestBase
{
    private const string Markup = """
        <ul class="list" data-density="compact" id="dense"><li class="list-row"><span>Sequence 1</span>
          <button class="btn btn--icon btn--ghost btn--sm" type="button" aria-label="Move up" id="tool"><i class="ri-arrow-up-s-line"></i></button></li></ul>
        <div data-density="comfortable"><button class="btn btn--icon btn--sm" type="button" aria-label="Edit" id="plain"><i class="ri-pencil-line"></i></button></div>
        <button class="btn btn--icon btn--sm" type="button" aria-label="Remove" id="page"><i class="ri-delete-bin-line"></i></button>
        """;

    [Theory]
    [InlineData(null, 24, 28, 28)]
    [InlineData("compact", 24, 28, 24)]
    public async Task A_part_of_the_page_takes_its_own_density(string? page, double dense, double plain, double rest)
    {
        if (NoBrowser) return;
        var (p, _) = await OpenStyled(Markup);
        if (page is not null) await Js.Run(p, $"() => document.documentElement.setAttribute('data-density', '{page}')");
        var r = await p.EvaluateAsync<double[]>(@"() => ['tool', 'plain', 'page'].flatMap(id => { const b = document.getElementById(id).getBoundingClientRect(); return [b.width, b.height]; })");
        Assert.Equal(dense, r[0], 0.5);
        Assert.Equal(dense, r[1], 0.5);
        Assert.Equal(plain, r[2], 0.5);
        Assert.Equal(plain, r[3], 0.5);
        Assert.Equal(rest, r[4], 0.5);
        Assert.Equal(rest, r[5], 0.5);
    }
}

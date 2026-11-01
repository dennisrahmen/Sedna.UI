using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The page's own furniture once a browser has laid it out: the rhythm between its
/// sections, and the head it opens with.
/// </summary>
/// <remarks>
/// Both faults this file pins parsed and applied exactly as written. A page-level modal
/// took the rhythm's top margin, which replaces the platform's <c>margin: auto</c> and
/// opened the modal at the top edge. A page head's text kept its max-content width, so a
/// long supporting line pushed the actions onto a line of their own on any screen.
/// </remarks>
public class PageLayoutTests : ScriptTestBase
{
    [Fact]
    public async Task A_modal_written_directly_in_the_page_opens_centred()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <main class="page">
              <p>The page's first section.</p>
              <button class="btn" type="button" onclick="document.getElementById('m').showModal()">Open</button>
              <dialog class="modal" id="m"><div class="modal-body"><p>Centred.</p></div></dialog>
            </main>
            """);
        await page.SetViewportSizeAsync(1280, 800);

        await page.ClickAsync("button");
        // Measured at rest: a modal opens with a short slide, so a rect read on the click
        // is the animation's, not the modal's.
        await page.EvaluateAsync(
            "() => Promise.allSettled(document.getAnimations().map(a => a.finished))");
        // [top gutter, bottom gutter, left gutter, right gutter]
        var gutters = await page.EvaluateAsync<double[]>(
            "() => { const r = document.getElementById('m').getBoundingClientRect(); "
            + "return [r.top, innerHeight - r.bottom, r.left, innerWidth - r.right]; }");

        Assert.Equal(gutters[0], gutters[1], 1);
        Assert.Equal(gutters[2], gutters[3], 1);
        Assert.True(gutters[0] > 100, $"The modal opened {gutters[0]}px from the top edge.");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_rhythm_still_spaces_the_sections_around_a_dialog()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            """
            <main class="page">
              <div class="card" id="a">One</div>
              <div class="card" id="b">Two</div>
              <dialog class="modal" id="m"><div class="modal-body"><p>x</p></div></dialog>
            </main>
            """);

        var gap = await page.EvaluateAsync<double>(
            "() => document.getElementById('b').getBoundingClientRect().top"
            + " - document.getElementById('a').getBoundingClientRect().bottom");
        var expected = await page.EvaluateAsync<double>(
            "() => parseFloat(getComputedStyle(document.getElementById('b')).marginBlockStart)");

        Assert.True(expected > 0, "The rhythm stopped spacing ordinary sections.");
        Assert.Equal(expected, gap, 1);

        Assert.Empty(errors);
    }

    private const string Head =
        """
        <main class="page">
          <div class="page-head">
            <div class="page-head-text" id="text">
              <h1>Secrets</h1>
              <p>Every value is stored encrypted and shown once, when it is created. Rotate a
                 secret from its row; the old value stays valid until the next deployment
                 picks the new one up, so a service that reads it at start-up never sees a
                 gap. A secret nobody has read for ninety days is flagged in the list.</p>
            </div>
            <div class="page-head-actions" id="actions">
              <button class="btn btn-primary" type="button">New secret</button>
            </div>
          </div>
        </main>
        """;

    [Theory]
    [InlineData(1440)]
    [InlineData(1024)]
    [InlineData(768)]
    public async Task A_long_page_head_lede_wraps_beside_the_actions(int width)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Head);
        await page.SetViewportSizeAsync(width, 800);

        // [text top, text right, lede height, actions top, actions left, lede line height]
        var box = await page.EvaluateAsync<double[]>(
            """
            () => {
              const t = document.getElementById('text').getBoundingClientRect();
              const a = document.getElementById('actions').getBoundingClientRect();
              const p = document.querySelector('#text p');
              return [t.top, t.right, p.getBoundingClientRect().height, a.top, a.left,
                      parseFloat(getComputedStyle(p).lineHeight)];
            }
            """);

        Assert.Equal(box[0], box[3], 1);                 // the actions share the heading's line
        Assert.True(box[1] <= box[4], "The text runs under the actions.");
        Assert.True(box[2] > box[5] * 1.5, "The lede did not wrap; the fixture is too short to prove it.");

        Assert.Empty(errors);
    }

    [Fact]
    public async Task On_a_phone_the_actions_drop_below_the_heading()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Head);
        await page.SetViewportSizeAsync(375, 800);

        // [text bottom, actions top, actions right, viewport width]
        var box = await page.EvaluateAsync<double[]>(
            """
            () => {
              const t = document.getElementById('text').getBoundingClientRect();
              const a = document.getElementById('actions').getBoundingClientRect();
              return [t.bottom, a.top, a.right, innerWidth];
            }
            """);

        Assert.True(box[1] >= box[0], "The actions stayed beside a squeezed heading.");
        Assert.True(box[2] <= box[3], "The actions run off the screen.");

        Assert.Empty(errors);
    }
}

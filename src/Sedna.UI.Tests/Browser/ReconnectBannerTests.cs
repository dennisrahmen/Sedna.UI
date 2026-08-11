using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every state Blazor can put on <c>#components-reconnect-modal</c> shows exactly one
/// row.
/// </summary>
/// <remarks>
/// <para>
/// A state with no rule does not degrade, it goes silent: the modal keeps
/// <c>display: none</c> and the reader is told nothing at all while the page is
/// disconnected. The library handled three states; .NET 10's Blazor sets six, so
/// <c>retrying</c>, <c>paused</c> and <c>resume-failed</c> each showed nothing.
/// </para>
/// <para>
/// The state list is checked against the framework's own <c>blazor.web.js</c> by
/// <see cref="The_state_list_matches_the_framework"/>, so a seventh state added by a
/// future SDK fails here rather than in production.
/// </para>
/// </remarks>
public class ReconnectBannerTests : ScriptTestBase
{
    /// <summary>The host-page block, as `docs/getting-started.md` documents it.</summary>
    private const string Banner =
        """
        <div id="components-reconnect-modal">
          <div class="reconnect-banner reconnect-attempting">
            <i class="ri-wifi-off-line"></i><span>Reconnecting…</span>
          </div>
          <div class="reconnect-banner reconnect-paused">
            <i class="ri-pause-circle-line"></i><span>Paused.</span>
          </div>
          <div class="reconnect-banner reconnect-failed">
            <i class="ri-close-circle-line"></i><span>Could not reconnect.</span>
          </div>
          <div class="reconnect-banner reconnect-rejected">
            <i class="ri-error-warning-line"></i><span>Session expired.</span>
          </div>
        </div>
        """;

    /// <summary>The three rows an app is required to supply.</summary>
    private const string ThreeRowBanner =
        """
        <div id="components-reconnect-modal">
          <div class="reconnect-banner reconnect-attempting"><span>Reconnecting…</span></div>
          <div class="reconnect-banner reconnect-failed"><span>Could not reconnect.</span></div>
          <div class="reconnect-banner reconnect-rejected"><span>Session expired.</span></div>
        </div>
        """;

    public static TheoryData<string, string> States() => new()
    {
        { "components-reconnect-show", "reconnect-attempting" },
        { "components-reconnect-retrying", "reconnect-attempting" },
        { "components-reconnect-paused", "reconnect-paused" },
        { "components-reconnect-failed", "reconnect-failed" },
        { "components-reconnect-resume-failed", "reconnect-failed" },
        { "components-reconnect-rejected", "reconnect-rejected" },
    };

    [Theory]
    [MemberData(nameof(States))]
    public async Task Each_state_shows_exactly_one_row(string state, string expected)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Banner);

        var shown = await Shown(page, state);

        Assert.Equal([expected], shown);
        Assert.Equal("block", await page.EvalOnSelectorAsync<string>(
            "#components-reconnect-modal", "el => getComputedStyle(el).display"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_modal_is_hidden_with_no_state_and_when_hidden()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Banner);

        Assert.Empty(await Shown(page, ""));
        Assert.Empty(await Shown(page, "components-reconnect-hide"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_paused_circuit_falls_back_to_the_attempting_row()
    {
        if (NoBrowser) return;
        // .reconnect-paused is optional, and this is what makes it optional: an app that
        // supplies the three documented rows must not render an empty bar in a state
        // that did not exist when it was written.
        var (page, errors) = await OpenStyled(ThreeRowBanner);

        Assert.Equal(["reconnect-attempting"], await Shown(page, "components-reconnect-paused"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_banner_row_outside_the_modal_is_simply_visible()
    {
        if (NoBrowser) return;
        // What lets the catalogue show all four rows at once without inline styles a
        // reader would copy by accident.
        var (page, errors) = await OpenStyled(
            """<div class="reconnect-banner reconnect-attempting"><span>Reconnecting…</span></div>""");

        Assert.Equal("flex", await page.EvalOnSelectorAsync<string>(
            ".reconnect-banner", "el => getComputedStyle(el).display"));

        Assert.Empty(errors);
    }

    // Whether the stylesheet handles every state the FRAMEWORK can set is asserted in
    // the catalogue suite instead: it needs the real blazor.web.js, and this fixture
    // serves its own routes and has no Blazor in it — a fetch here would come back as
    // the fixture's own HTML and the test would pass by matching nothing.

    /// <summary>The rows visible with <paramref name="state"/> on the modal.</summary>
    private static async Task<string[]> Shown(Microsoft.Playwright.IPage page, string state) =>
        await page.EvaluateAsync<string[]>(
            """
            state => {
                const modal = document.getElementById('components-reconnect-modal');
                modal.className = state;
                return [...modal.querySelectorAll('.reconnect-banner')]
                    .filter(el => getComputedStyle(el).display !== 'none')
                    .map(el => el.className.replace('reconnect-banner ', ''));
            }
            """, state);
}

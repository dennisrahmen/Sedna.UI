using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every state Blazor can put on <c>#components-reconnect-modal</c> shows exactly one
/// <c>.status-bar</c> row.
/// </summary>
/// <remarks>
/// <para>
/// A state with no rule does not degrade, it goes silent: the modal keeps
/// <c>display: none</c> and the reader is told nothing at all while the page is
/// disconnected. The library handled three states; .NET 10's Blazor sets six, so
/// <c>retrying</c>, <c>paused</c> and <c>resume-failed</c> each showed nothing.
/// </para>
/// <para>
/// The rows were <c>.reconnect-*</c> and the unhandled-error bar was a separate block
/// of rules on <c>#blazor-error-ui</c> — same strip, same position, same two
/// severities, two vocabularies. One <c>.status-bar</c> family carries both now, and
/// <c>--error</c> is the fifth modifier rather than a second mechanism.
/// </para>
/// </remarks>
public class StatusBarTests : ScriptTestBase
{
    /// <summary>The host-page block, as `docs/getting-started.md` documents it.</summary>
    private const string Bars =
        """
        <div id="components-reconnect-modal">
          <div class="status-bar status-bar--reconnecting">
            <i class="ri-wifi-off-line"></i><span>Reconnecting…</span>
          </div>
          <div class="status-bar status-bar--paused">
            <i class="ri-pause-circle-line"></i><span>Paused.</span>
          </div>
          <div class="status-bar status-bar--failed">
            <i class="ri-close-circle-line"></i><span>Could not reconnect.</span>
          </div>
          <div class="status-bar status-bar--expired">
            <i class="ri-error-warning-line"></i><span>Session expired.</span>
          </div>
        </div>
        """;

    /// <summary>The three rows an app is required to supply.</summary>
    private const string ThreeRows =
        """
        <div id="components-reconnect-modal">
          <div class="status-bar status-bar--reconnecting"><span>Reconnecting…</span></div>
          <div class="status-bar status-bar--failed"><span>Could not reconnect.</span></div>
          <div class="status-bar status-bar--expired"><span>Session expired.</span></div>
        </div>
        """;

    public static TheoryData<string, string> States() => new()
    {
        { "components-reconnect-show", "status-bar--reconnecting" },
        { "components-reconnect-retrying", "status-bar--reconnecting" },
        { "components-reconnect-paused", "status-bar--paused" },
        { "components-reconnect-failed", "status-bar--failed" },
        { "components-reconnect-resume-failed", "status-bar--failed" },
        { "components-reconnect-rejected", "status-bar--expired" },
    };

    [Theory]
    [MemberData(nameof(States))]
    public async Task Each_state_shows_exactly_one_row(string state, string expected)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Bars);

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
        var (page, errors) = await OpenStyled(Bars);

        Assert.Empty(await Shown(page, ""));
        Assert.Empty(await Shown(page, "components-reconnect-hide"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_paused_circuit_falls_back_to_the_reconnecting_row()
    {
        if (NoBrowser) return;
        // .status-bar--paused is optional, and this is what makes it optional: an app
        // that supplies the three documented rows must not render an empty bar in a
        // state that did not exist when it was written.
        var (page, errors) = await OpenStyled(ThreeRows);

        Assert.Equal(["status-bar--reconnecting"], await Shown(page, "components-reconnect-paused"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_row_outside_the_modal_is_simply_visible()
    {
        if (NoBrowser) return;
        // What lets the catalogue show all five rows at once without inline styles a
        // reader would copy by accident.
        var (page, errors) = await OpenStyled(
            """<div class="status-bar status-bar--reconnecting"><span>Reconnecting…</span></div>""");

        Assert.Equal("flex", await page.EvalOnSelectorAsync<string>(
            ".status-bar", "el => getComputedStyle(el).display"));

        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_error_bar_is_the_same_family_inside_its_own_id()
    {
        if (NoBrowser) return;
        // The whole point of the rename. #blazor-error-ui is hidden until the framework
        // sets `display: block` inline on it, and the strip INSIDE it is a flex row in
        // both mount points — the class is never on the id, because an inline
        // `display: block` beats every rule in every layer and would lay this one bar
        // out as inline text while the reconnect rows stayed flex rows.
        var (page, errors) = await OpenStyled(
            """
            <div id="blazor-error-ui">
              <div class="status-bar status-bar--error">
                <i class="ri-alert-line"></i><span>An unhandled error has occurred.</span>
                <button class="status-bar-action" type="button">Reload</button>
                <button class="status-bar-dismiss" type="button" aria-label="Dismiss">x</button>
              </div>
            </div>
            """);

        Assert.Equal("none", await page.EvalOnSelectorAsync<string>(
            "#blazor-error-ui", "el => getComputedStyle(el).display"));

        // Revealed the way the framework reveals it.
        await page.EvalOnSelectorAsync("#blazor-error-ui", "el => el.style.display = 'block'");

        Assert.Equal("flex", await page.EvalOnSelectorAsync<string>(
            ".status-bar--error", "el => getComputedStyle(el).display"));

        // The action is FILLED. `.btn-danger` is a tint, so on a crimson bar its fill
        // vanished and the control read as an empty rectangle — which is why this is a
        // frame class and not a `.btn`.
        var background = await page.EvalOnSelectorAsync<string>(
            ".status-bar-action", "el => getComputedStyle(el).backgroundColor");

        Assert.False(
            background is "transparent" or "rgba(0, 0, 0, 0)",
            $".status-bar-action has no fill ({background}). It sits on a coloured bar, so "
            + "an unfilled control is unreadable.");

        Assert.Empty(errors);
    }

    /// <summary>The rows visible with <paramref name="state"/> on the modal.</summary>
    private static async Task<string[]> Shown(Microsoft.Playwright.IPage page, string state) =>
        await page.EvaluateAsync<string[]>(
            """
            state => {
                const modal = document.getElementById('components-reconnect-modal');
                modal.className = state;
                return [...modal.querySelectorAll('.status-bar')]
                    .filter(el => getComputedStyle(el).display !== 'none')
                    .map(el => el.className.replace('status-bar ', ''));
            }
            """, state);
}

using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A grouped list's group folds from its head: the rows up to the next head go, the count
/// stays, a managed toggle follows the app, and a row rendered into a folded group is folded.
/// </summary>
public class GroupFoldTests : ScriptTestBase
{
    private static string List(string mode = "", string expanded = "true") => $"""
        <ul class="list" id="list" style="width:400px">
          <li class="sticky-group-head" style="--group-head-index: 0">
            <button class="sticky-group-toggle" id="t1" type="button" data-group-toggle{mode} aria-expanded="{expanded}">Overdue</button>
            <span class="sticky-group-count" id="c1">2</span>
          </li>
          <li id="a1"><span class="list-row">One</span></li>
          <li id="a2"><span class="list-row">Two</span></li>
          <li class="sticky-group-head" style="--group-head-index: 1">
            <button class="sticky-group-toggle" type="button" data-group-toggle aria-expanded="true">Later</button>
          </li>
          <li id="b1"><span class="list-row">Three</span></li>
        </ul>
        """;

    private static Task<bool[]> Shown(IPage page) =>
        page.EvaluateAsync<bool[]>("() => ['a1', 'a2', 'b1', 'c1'].map(id => document.getElementById(id).getClientRects().length > 0)");

    [Fact]
    public async Task A_click_folds_the_group_up_to_the_next_head_and_the_count_stays()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(List());

        await page.ClickAsync("#t1");
        Assert.Equal(new[] { false, false, true, true }, await Shown(page));
        Assert.Equal("false", await page.GetAttributeAsync("#t1", "aria-expanded"));

        // Safari does not focus a button it clicks, so the keyboard half is reached by Tab.
        await page.FocusAsync("#t1");
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(new[] { true, true, true, true }, await Shown(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_group_rendered_folded_starts_folded_and_a_row_added_to_it_is_folded_too()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(List(expanded: "false"));
        Assert.Equal(new[] { false, false, true, true }, await Shown(page));

        await Js.Run(page, "() => { const li = document.createElement('li'); li.id = 'a3'; li.textContent = 'Four'; document.getElementById('a2').after(li); }");
        await page.WaitForFunctionAsync("() => document.getElementById('a3').hasAttribute('data-group-hidden')");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_managed_toggle_leaves_the_click_to_the_app_and_follows_what_it_renders()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(List(mode: "=\"managed\""));

        await page.ClickAsync("#t1");
        Assert.Equal("true", await page.GetAttributeAsync("#t1", "aria-expanded"));
        Assert.Equal(new[] { true, true, true, true }, await Shown(page));

        await Js.Run(page, "() => document.getElementById('t1').setAttribute('aria-expanded', 'false')");
        await page.WaitForFunctionAsync("() => document.getElementById('a1').hasAttribute('data-group-hidden')");
        Assert.Equal(new[] { false, false, true, true }, await Shown(page));
        Assert.Empty(errors);
    }
}

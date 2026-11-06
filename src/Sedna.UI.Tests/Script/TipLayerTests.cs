using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The hover-hint bubble is seen over an open modal, goes when its trigger does, and Escape
/// dismisses it without closing the dialog it is in.
/// </summary>
/// <remarks>
/// The bubble was a fixed node on &lt;body&gt; at z-index 1000, and a modal dialog is in the top
/// layer, which no z-index reaches: every hint inside a dialog was painted under it. And a
/// hint whose trigger a render replaced stayed on screen in the top left corner until the
/// next click.
/// </remarks>
public class TipLayerTests : ScriptTestBase
{
    private const string Dialog = """
        <dialog class="modal" id="dlg">
            <p>Edit group</p>
            <button type="button" id="perm" data-tip="Members can approve orders up to their limit">Approve</button>
        </dialog>
        <button type="button" id="outside" data-tip="Opens the group">Open</button>
        """;

    private static Task<JsonElement> Bubble(IPage page) =>
        page.EvaluateAsync<JsonElement>("""
            () => {
                const tip = document.querySelector('.sedna-tip');
                if (!tip) return { visible: false, onTop: false, inDialog: false };
                const r = tip.getBoundingClientRect();
                const hit = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2);
                return { visible: tip.classList.contains('sedna-tip--visible'),
                         onTop: tip.contains(hit),
                         inDialog: tip.parentElement && tip.parentElement.id === 'dlg' };
            }
            """);

    [Fact]
    public async Task A_hint_in_an_open_modal_is_painted_above_it()
    {
        if (NoBrowser) return;
        var page = await Open(Dialog);
        await Js.Run(page, "() => document.getElementById('dlg').showModal()");

        await page.Locator("#perm").HoverAsync();
        await Assertions.Expect(page.Locator(".sedna-tip")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));
        var bubble = await Bubble(page);

        Assert.True(bubble.GetProperty("inDialog").GetBoolean(), "The bubble was left outside the modal, where it is inert.");
        Assert.True(bubble.GetProperty("onTop").GetBoolean(), "The bubble is painted under the dialog.");
    }

    [Fact]
    public async Task Escape_dismisses_a_hint_and_leaves_the_dialog_open()
    {
        if (NoBrowser) return;
        var page = await Open(Dialog);
        await Js.Run(page, "() => document.getElementById('dlg').showModal()");
        await page.Locator("#perm").FocusAsync();
        await Assertions.Expect(page.Locator(".sedna-tip")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));

        await page.Keyboard.PressAsync("Escape");

        await Assertions.Expect(page.Locator(".sedna-tip")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('dlg').open"),
            "Escape closed the dialog as well as the hint.");

        // The next Escape is the dialog's again.
        await page.Keyboard.PressAsync("Escape");
        await page.WaitForFunctionAsync("() => !document.getElementById('dlg').open");
    }

    [Fact]
    public async Task A_hint_goes_when_its_trigger_is_removed()
    {
        if (NoBrowser) return;
        var page = await Open(Dialog);

        await page.Locator("#outside").HoverAsync();
        await Assertions.Expect(page.Locator(".sedna-tip")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));

        // A render replaces the hovered element: no mouseout, focusout or mousedown comes.
        await Js.Run(page, "() => document.getElementById('outside').remove()");

        await Assertions.Expect(page.Locator(".sedna-tip")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("sedna-tip--visible"));
    }
}

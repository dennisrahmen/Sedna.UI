using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>closedby="any"</c> on a <c>.modal</c> closes it on a click on the backdrop in every
/// engine on the floor, and <c>data-close-guard</c> still refuses that close.
/// </summary>
public class DialogClosedByTests : ScriptTestBase
{
    private static string Dialog(string extra) => $"""
        <button id="open" onclick="document.getElementById('d').showModal()">Open</button>
        <dialog id="d" class="modal" closedby="any" {extra}><div class="modal-body"><p>Shortcuts</p></div></dialog>
        """;

    [Theory]
    [InlineData("", false)]
    [InlineData("data-close-guard", true)]
    public async Task A_click_beside_it_closes_it_unless_it_is_guarded(string extra, bool staysOpen)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Dialog(extra));
        await page.ClickAsync("#open");
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('d').open"));

        await page.Mouse.ClickAsync(5, 5);
        await Js.Frames(page);

        Assert.Equal(staysOpen, await page.EvaluateAsync<bool>("() => document.getElementById('d').open"));
        Assert.Empty(errors);
    }
}

using Sedna.UI.Tests.TestSupport;
using Microsoft.Playwright;

namespace Sedna.UI.Tests;

/// <summary>Chat: mine on the end, theirs on the start, and a flyover that opens.</summary>
public class ChatLayoutTests : ScriptTestBase
{
    private const string Thread = """
        <div class="card" style="width:480px">
            <div class="card-body">
                <div class="chat" id="chat" role="log">
                    <div class="chat-message" id="theirs">
                        <span class="avatar avatar-sm">AF</span>
                        <div class="chat-body"><div class="chat-bubble">Hello</div></div>
                    </div>
                    <div class="chat-message chat-message--mine" id="mine">
                        <div class="chat-body"><div class="chat-bubble">Hi</div></div>
                    </div>
                </div>
            </div>
        </div>
        <button class="chat-launcher" id="open" type="button" popovertarget="fly">Chat</button>
        <div class="chat-flyover" id="fly" popover="manual"><div class="chat-flyover-head"><strong>Alex</strong></div></div>
        """;

    [Fact]
    public async Task Mine_sits_on_the_end_and_theirs_on_the_start()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Thread);

        var chat = await page.Locator("#chat").BoundingBoxAsync();
        var mine = await page.Locator("#mine").BoundingBoxAsync();
        var theirs = await page.Locator("#theirs").BoundingBoxAsync();

        Assert.True(Math.Abs(mine!.X + mine.Width - (chat!.X + chat.Width)) < 1, "Mine is not on the end.");
        Assert.True(Math.Abs(theirs!.X - chat.X) < 1, "Theirs is not on the start.");
        Assert.True(mine.Width < chat.Width * 0.75, "A bubble should not span the thread.");
    }

    [Fact]
    public async Task The_launcher_opens_the_flyover_in_the_top_layer()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Thread);

        Assert.Equal("none", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('fly')).display"));
        await page.Locator("#open").ClickAsync();
        await Assertions.Expect(page.Locator("#fly")).ToBeVisibleAsync();
        Assert.True(await page.EvaluateAsync<bool>("() => document.getElementById('fly').matches(':popover-open')"));
        Assert.Equal("fixed", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('fly')).position"));
    }
}

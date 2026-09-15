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

    [Fact]
    public async Task The_flyover_is_the_whole_screen_on_a_phone()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Thread);
        await page.SetViewportSizeAsync(375, 700);
        await page.Locator("#open").ClickAsync();

        var box = await page.Locator("#fly").BoundingBoxAsync();
        Assert.NotNull(box);
        Assert.True(Math.Abs(box!.Width - 375) < 1 && Math.Abs(box.Height - 700) < 1,
            $"On a phone the flyover should fill the viewport; it is {box.Width}×{box.Height}.");
    }

    // Twenty messages in a 320px pane: the thread scrolls, the composer does not move.
    private static readonly string Pane = $$"""
        <div class="chat-pane" id="pane" style="height:320px; width:480px">
            <div class="chat" id="chat" role="log">
                <div class="chat-day" id="day">Today</div>
                {{string.Concat(Enumerable.Range(1, 20).Select(i => $"""<div class="chat-message"><div class="chat-body"><div class="chat-bubble">Message {i}</div></div></div>"""))}}
            </div>
            <button class="chat-jump" id="jump" type="button">New</button>
            <form class="chat-composer" id="composer"><div class="chat-composer-row">
                <textarea class="form-input" id="field" rows="1"></textarea>
            </div></form>
        </div>
        """;

    [Fact]
    public async Task The_pane_scrolls_the_thread_and_keeps_the_composer_at_the_bottom()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Pane);

        var pane = await page.Locator("#pane").BoundingBoxAsync();
        var composer = await page.Locator("#composer").BoundingBoxAsync();
        Assert.True(Math.Abs(composer!.Y + composer.Height - (pane!.Y + pane.Height)) < 1, "The composer is not at the pane's bottom.");

        Assert.True(await page.EvaluateAsync<bool>("() => { const c = document.getElementById('chat'); return c.scrollHeight > c.clientHeight; }"),
            "The thread does not scroll inside the pane.");
        Assert.Equal("sticky", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('day')).position"));

        // The jump floats over the thread: above the composer, inside the pane, over the thread's box.
        var jump = await page.Locator("#jump").BoundingBoxAsync();
        var chat = await page.Locator("#chat").BoundingBoxAsync();
        Assert.True(jump!.Y + jump.Height <= composer.Y + 1, "The jump overlaps the composer.");
        Assert.True(jump.Y >= chat!.Y && jump.Y < chat.Y + chat.Height, "The jump is not over the thread.");
    }

    [Fact]
    public async Task The_composer_grows_with_the_text_and_stops_at_a_few_lines()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled(Pane);
        var field = page.Locator("#field");

        var oneLine = (await field.BoundingBoxAsync())!.Height;
        await field.FillAsync("one\ntwo\nthree\nfour");
        var fourLines = (await field.BoundingBoxAsync())!.Height;
        Assert.True(fourLines > oneLine * 2, $"The field did not grow with the text ({oneLine} → {fourLines}).");

        await field.FillAsync(string.Join("\n", Enumerable.Range(1, 40).Select(i => $"line {i}")));
        var forty = (await field.BoundingBoxAsync())!.Height;
        Assert.True(forty < oneLine * 12, $"The field grew without a limit ({forty}px for forty lines).");
        Assert.True(await page.EvaluateAsync<bool>("() => { const f = document.getElementById('field'); return f.scrollHeight > f.clientHeight; }"),
            "Past the limit the field should scroll.");
    }
    [Fact]
    public async Task A_group_and_a_person_start_their_names_at_the_same_x()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <ul class="list" style="width:360px">
                <li><a class="list-row" href="#"><span class="avatar">AF</span><span class="list-main" id="person"><span class="list-title">Alex Fischer</span></span></a></li>
                <li><a class="list-row" href="#"><span class="avatar-pair" id="pair"><span class="avatar" id="back">PN</span><span class="avatar" id="front">TF</span></span><span class="list-main" id="group"><span class="list-title">Dispatch</span></span></a></li>
            </ul>
            """);

        var person = (await page.Locator("#person").BoundingBoxAsync())!.X;
        var group = (await page.Locator("#group").BoundingBoxAsync())!.X;
        Assert.True(Math.Abs(person - group) < 0.5, $"A person's name starts at {person} and a group's at {group}.");

        // The pair stays inside its square, the front one lower and further along.
        var pair = (await page.Locator("#pair").BoundingBoxAsync())!;
        var back = (await page.Locator("#back").BoundingBoxAsync())!;
        var front = (await page.Locator("#front").BoundingBoxAsync())!;
        Assert.True(back.X >= pair.X && front.X + front.Width <= pair.X + pair.Width + 0.5
                    && front.Y + front.Height <= pair.Y + pair.Height + 0.5, "The stacked avatars leave their square.");
        Assert.True(front.X > back.X && front.Y > back.Y, "The front avatar is not stacked corner to corner.");
    }
}

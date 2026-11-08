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
                        <span class="avatar avatar--sm">AF</span>
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
    // The submit button with send, stop and retry written once each, and a state on its holder.
    private static string Send(string? state) => $$"""
        <span class="chat-composer-send" id="send" {{(state is null ? "" : "data-state=\"" + state + "\"")}}>
            <button class="btn btn--brand btn--solid btn--icon" type="submit" id="submit">
                <span data-when="idle" id="w-send"><i class="ri-arrow-up-line" aria-hidden="true"></i><span class="visually-hidden">Send</span></span>
                <span data-when="submitted streaming" id="w-stop"><i class="ri-stop-fill" aria-hidden="true"></i><span class="visually-hidden">Stop</span></span>
                <span data-when="error" id="w-retry"><i class="ri-restart-line" aria-hidden="true"></i><span class="visually-hidden">Retry</span></span>
            </button>
        </span>
        """;

    [Theory]
    [InlineData(null, "w-send", "Send")]
    [InlineData("idle", "w-send", "Send")]
    [InlineData("submitted", "w-stop", "Stop")]
    [InlineData("streaming", "w-stop", "Stop")]
    [InlineData("error", "w-retry", "Retry")]
    public async Task The_submit_button_shows_the_words_of_its_state_and_nothing_else(string? state, string shown, string name)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<form class="chat-composer chat-composer--prompt" style="width:480px"><div class="chat-composer-tools">{Send(state)}</div></form>""");

        foreach (var id in new[] { "w-send", "w-stop", "w-retry" })
        {
            var display = await page.EvaluateAsync<string>("id => getComputedStyle(document.getElementById(id)).display", id);
            Assert.Equal(id == shown ? "contents" : "none", display);
        }
        await Assertions.Expect(page.Locator("#submit")).ToHaveAccessibleNameAsync(name);

        // One glyph in the button, centred in its round box.
        var button = (await page.Locator("#submit").BoundingBoxAsync())!;
        var glyph = (await page.Locator($"#{shown} > i").BoundingBoxAsync())!;
        Assert.Equal(button.X + button.Width / 2, glyph.X + glyph.Width / 2, 1.0);
        Assert.Equal(button.Y + button.Height / 2, glyph.Y + glyph.Height / 2, 1.0);
        Assert.Equal(1, await page.EvaluateAsync<int>("() => [...document.querySelectorAll('#submit i')].filter(i => i.getClientRects().length).length"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_ghost_field_in_a_composer_row_takes_the_row_and_grows_like_a_bare_field()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <form class="chat-composer chat-composer--prompt" style="width:480px">
                <div class="chat-composer-row" id="row">
                    <div class="ghost-field" id="host">
                        <textarea class="form-input" id="field" rows="1" data-suggestion="Restart the sync worker"></textarea>
                        <div class="ghost-text" aria-hidden="true"></div>
                    </div>
                </div>
            </form>
            """);

        var row = (await page.Locator("#row").BoundingBoxAsync())!;
        var host = (await page.Locator("#host").BoundingBoxAsync())!;
        var field = page.Locator("#field");
        var box = (await field.BoundingBoxAsync())!;
        Assert.Equal(row.Width, host.Width, 0.5);
        Assert.Equal(host.Width, box.Width, 0.5);
        // The prompt's field has no box of its own: the composer is the box.
        Assert.Equal("0px", await page.EvaluateAsync<string>("() => getComputedStyle(document.getElementById('field')).borderTopWidth"));

        await field.FillAsync("one\ntwo\nthree\nfour");
        Assert.True((await field.BoundingBoxAsync())!.Height > box.Height * 2, "The field in a ghost field did not grow with the text.");
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_model_option_puts_its_note_under_the_name_and_its_shortcut_at_the_end()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="form-combo form-combo--inline" style="width:320px">
                <div class="form-combo-panel">
                    <div class="form-combo-list" role="listbox" aria-label="Models">
                        <div class="form-combo-option" role="option" id="opt" aria-selected="true">
                            <i class="ri-cloud-line" id="icon" aria-hidden="true"></i>
                            <span class="form-combo-option-text" id="text">Balanced<span class="form-combo-option-note" id="note">Good for most questions, and a note long enough to need cutting short</span></span>
                            <kbd class="kbd" id="kbd">Alt 2</kbd>
                        </div>
                    </div>
                </div>
            </div>
            """);

        var opt = (await page.Locator("#opt").BoundingBoxAsync())!;
        var text = (await page.Locator("#text").BoundingBoxAsync())!;
        var note = (await page.Locator("#note").BoundingBoxAsync())!;
        var kbd = (await page.Locator("#kbd").BoundingBoxAsync())!;
        var icon = (await page.Locator("#icon").BoundingBoxAsync())!;

        Assert.True(note.Y >= text.Y + 10, "The note is not on a line of its own under the name.");
        Assert.True(note.X + note.Width <= kbd.X + 0.5, "The note runs under the shortcut.");
        Assert.True(icon.X + icon.Width <= text.X, "The icon does not lead the row.");
        var padEnd = await page.EvaluateAsync<double>("() => parseFloat(getComputedStyle(document.getElementById('opt')).paddingRight)");
        Assert.Equal(opt.X + opt.Width - padEnd, kbd.X + kbd.Width, 1.0);
        Assert.True(await page.EvaluateAsync<bool>("() => { const n = document.getElementById('note'); return n.scrollWidth > n.clientWidth; }"),
            "A long note should be cut short, not wrap the row.");
        Assert.Empty(errors);
    }
}

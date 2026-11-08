using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.requestNotify()</c> answers with a boolean, whatever the browser says.
/// </summary>
/// <remarks>
/// <c>ISednaUi.RequestNotifyAsync</c> reads the answer as a <c>bool</c>. An answer of
/// <c>undefined</c> arrives in .NET as JSON <c>null</c>, which does not convert, so the call
/// threw into the component and took the circuit down with it. The browser's own
/// <c>Notification</c> is replaced on the page for each case, so the four answers are the
/// same in every engine and no permission prompt is involved.
/// </remarks>
public class NotifyTests : ScriptTestBase
{
    private static async Task<string> Ask(Microsoft.Playwright.IPage page, string notification) =>
        await page.EvaluateAsync<string>(
            $$"""
            async () => {
                {{notification}}
                const answer = await sednaUi.requestNotify();
                return typeof answer + ':' + answer;
            }
            """);

    [Fact]
    public async Task Permission_already_granted_answers_true()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>");

        var answer = await Ask(page,
            "window.Notification = { permission: 'granted', requestPermission: () => Promise.reject(new Error('asked again')) };");

        Assert.Equal("boolean:true", answer);
    }

    [Fact]
    public async Task Permission_already_denied_answers_false_without_asking()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>");

        var answer = await Ask(page,
            "window.Notification = { permission: 'denied', requestPermission: () => Promise.reject(new Error('asked again')) };");

        Assert.Equal("boolean:false", answer);
    }

    [Fact]
    public async Task A_reader_not_yet_asked_is_asked_and_their_answer_is_the_result()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>");

        var granted = await Ask(page,
            "window.Notification = { permission: 'default', requestPermission: () => Promise.resolve('granted') };");
        var refused = await Ask(page,
            "window.Notification = { permission: 'default', requestPermission: () => Promise.resolve('denied') };");

        Assert.Equal("boolean:true", granted);
        Assert.Equal("boolean:false", refused);
    }

    [Fact]
    public async Task A_browser_without_notifications_answers_false()
    {
        if (NoBrowser) return;
        var page = await Open("<p>x</p>");

        var answer = await Ask(page, "delete window.Notification;");

        Assert.Equal("boolean:false", answer);
    }
}

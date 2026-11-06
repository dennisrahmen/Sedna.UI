using System.Text.Json;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>sednaUi.toast.present</c>: a toast the app wrote in a <c>&lt;template&gt;</c>, shown in the
/// library's one stack with everything that stack has, its slots filled as text, and a result that
/// says which of its controls the reader pressed.
/// </summary>
public class ToastPresentTests : ScriptTestBase
{
    private const string Templates = """
        <template id="assigned">
          <div class="toast toast--info">
            <i class="ri-user-received-line" aria-hidden="true"></i>
            <a class="toast-body" data-href data-toast-value="open"><strong data-title></strong><span data-text></span></a>
            <button class="toast-close" type="button" aria-label="Dismiss"><i class="ri-close-line" aria-hidden="true"></i></button>
          </div>
        </template>
        <template id="review">
          <div class="toast toast--warn">
            <i class="ri-alert-line" aria-hidden="true"></i>
            <div class="toast-body">
              <strong data-title></strong><span data-text></span><small data-note></small>
              <div class="toast-actions">
                <button class="btn btn--sm" type="button" id="later" data-toast-value="later">Later</button>
                <button class="btn btn--sm btn--warn btn--solid" type="button" id="review-now" data-toast-value="review">Review</button>
              </div>
            </div>
          </div>
        </template>
        <template id="more">
          <div class="toast"><div class="toast-body"><span data-count></span></div></div>
        </template>
        """;

    [Fact]
    public async Task A_template_toast_joins_the_librarys_stack_with_its_slots_filled_as_text()
    {
        if (NoBrowser) return;
        var page = await Open(Templates + "<a id='elsewhere' href='#'>Elsewhere</a>");

        var probe = await page.EvaluateAsync<JsonElement>("""
            () => {
                sednaUi.toast('Saved', { timeout: 0 });
                const id = sednaUi.toast.present('assigned',
                    { title: 'Assigned to you', text: '<b>ORD-4182</b>', href: '/orders/4182' }, { timeout: 0 });
                const bad = sednaUi.toast.present('assigned', { title: 'x', text: '', href: 'javascript:alert(1)' }, { timeout: 0 });
                const stacks = document.querySelectorAll('.toast-stack');
                const toasts = stacks[0].querySelectorAll('.toast');
                const card = toasts[1];
                return {
                    id, stacks: stacks.length, count: toasts.length,
                    text: card.querySelector('[data-text]').textContent,
                    markup: card.querySelector('b') !== null,
                    href: card.querySelector('a').getAttribute('href'),
                    marker: card.querySelector('[data-href]') !== null,
                    badHref: toasts[2].querySelector('a').hasAttribute('href'),
                    emptySlot: toasts[2].querySelector('[data-text]') !== null,
                    missing: sednaUi.toast.present('no-such-template', {}),
                };
            }
            """);

        Assert.True(probe.GetProperty("id").GetInt32() > 0);
        Assert.Equal(1, probe.GetProperty("stacks").GetInt32());          // one stack, the library's
        Assert.Equal(3, probe.GetProperty("count").GetInt32());
        Assert.Equal("<b>ORD-4182</b>", probe.GetProperty("text").GetString());   // text, never markup
        Assert.False(probe.GetProperty("markup").GetBoolean());
        Assert.Equal("/orders/4182", probe.GetProperty("href").GetString());
        Assert.False(probe.GetProperty("marker").GetBoolean());
        Assert.False(probe.GetProperty("badHref").GetBoolean());          // a script URL is dropped
        Assert.False(probe.GetProperty("emptySlot").GetBoolean());        // a slot with nothing to say goes
        Assert.Equal(0, probe.GetProperty("missing").GetInt32());
    }

    [Fact]
    public async Task The_result_is_the_value_of_the_control_pressed_and_null_for_any_other_end()
    {
        if (NoBrowser) return;
        var page = await Open(Templates);

        await page.EvaluateAsync("""
            () => {
                window.results = {};
                const watch = (name, id) => sednaUi.toast.result(id).then(v => window.results[name] = v);
                watch('review', sednaUi.toast.present('review', { title: 'Retention set to 400 days', text: 'Usually 30 to 365.' }, { timeout: 0 }));
                watch('closed', sednaUi.toast.present('assigned', { title: 'Assigned to you' }, { timeout: 0 }));
                const gone = sednaUi.toast.present('more', { count: '+3 more' }, { timeout: 0 });
                watch('dismissed', gone);
                sednaUi.toast.dismiss(gone);
                watch('timed out', sednaUi.toast.present('more', { count: '+1 more' }, { timeout: 50 }));
            }
            """);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-sedna-toasts] .toast:not([hidden])').length === 2");
        await page.ClickAsync("#review-now");
        await page.Locator(".toast--info .toast-close").ClickAsync();
        await page.WaitForFunctionAsync("() => Object.keys(window.results).length === 4");

        var results = await page.EvaluateAsync<Dictionary<string, string?>>("() => window.results");
        Assert.Equal("review", results["review"]);
        Assert.Null(results["closed"]);
        Assert.Null(results["dismissed"]);
        Assert.Null(results["timed out"]);
        Assert.Equal(0, await page.Locator(".toast").CountAsync());

        // Asked after it ended, it still answers how.
        Assert.Equal("review", await page.EvaluateAsync<string?>("() => sednaUi.toast.result(1)"));
    }

    [Fact]
    public async Task A_link_body_still_navigates_and_ends_the_toast_with_its_value()
    {
        if (NoBrowser) return;
        var page = await Open(Templates, serve: new Dictionary<string, Served>
        {
            ["/orders/4182"] = new("<p>ORD-4182</p>", "text/html; charset=utf-8"),
        });
        await page.EvaluateAsync("""
            () => {
                const id = sednaUi.toast.present('assigned', { title: 'Assigned to you', href: '/orders/4182' }, { timeout: 0 });
                sednaUi.toast.result(id).then(v => sessionStorage.setItem('ended', v));
            }
            """);
        await page.WaitForFunctionAsync("() => !document.querySelector('.toast').hidden");
        await page.ClickAsync(".toast-body");

        // The toast settled with its value, and the link went where it points.
        await page.WaitForURLAsync("**/orders/4182");
        Assert.Equal("open", await page.EvaluateAsync<string>("() => sessionStorage.getItem('ended')"));
    }

    [Fact]
    public async Task Replacing_keeps_the_place_the_id_and_the_wait()
    {
        if (NoBrowser) return;
        var page = await Open(Templates);
        var probe = await page.EvaluateAsync<JsonElement>("""
            () => {
                const first = sednaUi.toast.present('assigned', { title: 'Assigned to you' }, { timeout: 0 });
                const more = sednaUi.toast.present('more', { count: '+1 more' }, { timeout: 0 });
                sednaUi.toast.present('assigned', { title: 'Last' }, { timeout: 0 });
                sednaUi.toast.result(more).then(v => window.moreEnded = v === null ? 'null' : v);
                const same = sednaUi.toast.present('more', { count: '+2 more' }, { timeout: 0, replace: more });
                const toasts = [...document.querySelectorAll('[data-sedna-toasts] .toast')];
                return { same: same === more, at: toasts.findIndex(t => t.textContent.includes('+2 more')), count: toasts.length,
                         first: sednaUi.toast.dismiss(first) };
            }
            """);

        Assert.True(probe.GetProperty("same").GetBoolean());
        Assert.Equal(1, probe.GetProperty("at").GetInt32());              // the middle, where it was
        Assert.Equal(3, probe.GetProperty("count").GetInt32());
        Assert.False(await page.EvaluateAsync<bool>("() => 'moreEnded' in window"));   // still waiting
    }

    [Fact]
    public async Task A_template_toast_over_an_open_modal_is_inside_it_and_takes_the_click()
    {
        if (NoBrowser) return;
        var page = await Open(Templates + """<dialog class="modal" id="dlg"><p>Editing</p></dialog>""");
        await Js.Run(page, "() => document.getElementById('dlg').showModal()");
        await page.EvaluateAsync("() => { sednaUi.toast.result(sednaUi.toast.present('review', { title: 'Check this' }, { timeout: 0 })).then(v => window.ended = v); }");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-sedna-toasts]').parentElement.id === 'dlg' && !document.querySelector('.toast').hidden");

        await page.Locator("#later").ClickAsync(new() { Timeout = 2000 });
        await page.WaitForFunctionAsync("() => window.ended === 'later'");
    }

    [Fact]
    public async Task The_pointer_on_a_template_toast_holds_its_timer()
    {
        if (NoBrowser) return;
        var page = await Open(Templates);
        await page.EvaluateAsync("() => sednaUi.toast.present('review', { title: 'Check this' }, { timeout: 1200 })");
        await page.WaitForFunctionAsync("() => !document.querySelector('.toast').hidden");
        await page.HoverAsync(".toast");
        await page.WaitForTimeoutAsync(1600);
        Assert.Equal(1, await page.Locator(".toast").CountAsync());
        await page.Mouse.MoveAsync(1, 1);
        await Assertions.Expect(page.Locator(".toast")).ToHaveCountAsync(0, new() { Timeout = 3000 });
    }
}

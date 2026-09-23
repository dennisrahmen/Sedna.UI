using Bunit;
using Sedna.UI.Tests.TestSupport;
using Microsoft.JSInterop;

namespace Sedna.UI.Tests;

/// <summary>
/// The typed wrappers over the browser API. These assert the <b>call shape</b> —
/// the JavaScript identifier and the arguments — because that is the contract
/// between this package's C# and this package's script, and the two are versioned
/// together.
/// </summary>
/// <remarks>
/// The browser end is proved separately: <c>Script/</c> drives the real functions
/// in a real browser, and the catalogue's own toggles exercise the settings path
/// end to end. A fake alone would happily agree with a wrapper that calls a
/// function nobody ships.
/// </remarks>
public class InteropTests : BunitContext
{
    private ISednaUi Wrapper(SednaUiOptions? options = null) =>
        new SednaUi(JSInterop.JSRuntime, options ?? new SednaUiOptions());

    private JSRuntimeInvocation Only(string identifier)
    {
        var calls = JSInterop.Invocations[identifier];
        return Assert.Single(calls);
    }

    [Fact]
    public async Task Toast_passes_the_message_and_the_options_the_script_reads()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        JSInterop.Setup<int>("sednaUi.toast.show", _ => true).SetResult(7);

        var toast = await Wrapper().ToastAsync("Restarted orders-console-01", ToastKind.Go, title: "Done",
            timeoutMs: 0, dismissible: false);

        Assert.Equal(7, toast.Id);
        var call = Only("sednaUi.toast.show");
        Assert.Equal("Restarted orders-console-01", call.Arguments[0]);

        var options = (IDictionary<string, object?>)call.Arguments[1]!;
        // The script's own vocabulary, which is also the CSS modifier suffix.
        Assert.Equal("go", options["kind"]);
        Assert.Equal("Done", options["title"]);
        // 0 means "stays until dismissed", so it has to survive as 0 rather than
        // being treated as "not set" and defaulted back to 4000.
        Assert.Equal(0, options["timeout"]);
        Assert.Equal(false, options["dismissible"]);
        // Unset, so the script uses the configured label rather than an empty one.
        Assert.False(options.ContainsKey("dismissLabel"));
    }

    [Fact]
    public async Task A_toast_handle_dismisses_and_replaces_the_toast_it_names()
    {
        JSInterop.Setup<int>("sednaUi.toast.show", _ => true).SetResult(3);
        JSInterop.Setup<bool>("sednaUi.toast.dismiss", _ => true).SetResult(true);
        // The toast had gone, so the script showed a new one and reports its id.
        JSInterop.Setup<int>("sednaUi.toast.replace", _ => true).SetResult(4);

        var toast = await Wrapper().ToastAsync("Uploading…", timeoutMs: 0, dismissLabel: "Schließen");
        await toast.ReplaceAsync("Uploaded", ToastKind.Go);

        var replace = Only("sednaUi.toast.replace");
        Assert.Equal(3, replace.Arguments[0]);
        Assert.Equal("Uploaded", replace.Arguments[1]);
        // The label chosen for the toast survives replacing it.
        Assert.Equal("Schließen", ((IDictionary<string, object?>)replace.Arguments[2]!)["dismissLabel"]);
        Assert.Equal(4, toast.Id);

        Assert.True(await toast.DismissAsync());
        Assert.Equal(4, Only("sednaUi.toast.dismiss").Arguments[0]);
    }

    [Fact]
    public async Task Tips_are_switched_with_a_boolean()
    {
        JSInterop.SetupVoid("sednaUi.tips.setEnabled", _ => true).SetVoidResult();

        await Wrapper().SetTipsEnabledAsync(false);

        Assert.Equal(false, Only("sednaUi.tips.setEnabled").Arguments[0]);
    }

    [Theory]
    [InlineData(ToastKind.Info, "info")]
    [InlineData(ToastKind.Go, "go")]
    [InlineData(ToastKind.Warn, "warn")]
    [InlineData(ToastKind.Danger, "danger")]
    public async Task Every_toast_kind_maps_to_a_family_the_stylesheet_defines(
        ToastKind kind, string expected)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await Wrapper().ToastAsync("x", kind);

        Assert.Equal(expected, ((IDictionary<string, object?>)Only("sednaUi.toast.show").Arguments[1]!)["kind"]);
        // .toast-go, .toast-warn, .toast-danger, .toast-info all exist.
        Assert.Contains($".toast-{expected}", Assets.Css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShowModal_completes_with_the_return_value_the_script_resolves()
    {
        JSInterop.Setup<string?>("sednaUi.modal.show", _ => true).SetResult("delete");

        var answer = await Wrapper().ShowModalAsync("delete-queue");

        Assert.Equal("delete", answer);
        Assert.Equal("delete-queue", Only("sednaUi.modal.show").Arguments[0]);
    }

    [Fact]
    public async Task ShowModal_uses_the_overload_Blazor_does_not_time_out()
    {
        JSInterop.Setup<string?>("sednaUi.modal.show", _ => true).SetResult(null);

        await Wrapper().ShowModalAsync("delete-queue");

        // THE defect this guards. The token-less InvokeAsync wraps every call in Blazor
        // Server's one-minute default timeout, so a dialog a reader left open for a minute
        // threw TaskCanceledException into the app's handler. Only the CancellationToken
        // overload is exempt, and bUnit records which one was used.
        Assert.NotNull(Only("sednaUi.modal.show").CancellationToken);
    }

    [Fact]
    public async Task A_cancelled_ShowModal_closes_the_dialog_it_stopped_waiting_for()
    {
        var show = JSInterop.Setup<string?>("sednaUi.modal.show", _ => true);
        JSInterop.SetupVoid("sednaUi.modal.close", _ => true).SetVoidResult();
        using var cts = new CancellationTokenSource();

        var waiting = Wrapper().ShowModalAsync("delete-queue", cts.Token);
        await cts.CancelAsync();
        show.SetCanceled();     // what the real runtime does with a cancelled token

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        // A dialog left open with nobody awaiting its answer would be a trap.
        Assert.Equal("delete-queue", Only("sednaUi.modal.close").Arguments[0]);
    }

    [Fact]
    public async Task A_spotlight_step_sends_only_the_options_that_were_set()
    {
        JSInterop.SetupModule("sednaUi.spotlight.follow", _ => true).SetupVoid("stop").SetVoidResult();

        await using var step = await Wrapper().FollowSpotlightAsync("#hole", "[data-live]", new SpotlightOptions
        {
            Tip = "#tip",
            Placement = SpotlightPlacement.Auto,
            Lock = new SpotlightLock { Interactive = true },
        });

        var call = Only("sednaUi.spotlight.follow");
        Assert.Equal("#hole", call.Arguments[0]);
        Assert.Equal("[data-live]", call.Arguments[1]);

        // Unset options are left out rather than sent as null: the script tests several
        // with `typeof x === 'number'` and others with truthiness, and a null that reads
        // as "set" to one of them would move the bubble.
        var options = Assert.IsType<Dictionary<string, object>>(call.Arguments[2]);
        Assert.Equal(["lock", "placement", "tip"], options.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("auto", options["placement"]);
        Assert.Equal(true, ((Dictionary<string, object>)options["lock"])["interactive"]);
    }

    [Fact]
    public async Task Configure_sends_every_option_the_script_accepts()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var northwind = new SednaTheme("northwind", SednaTheme.Sedna.Palette);

        await Wrapper(new SednaUiOptions
        {
            StoragePrefix = "app-a.",
            NotifyIcon = "/logo.png",
            LangCookie = true,
            Themes = [northwind],
            Default = "northwind",
            ToastDismissLabel = "Schließen",
        }).ConfigureAsync();

        var options = Only("sednaUi.configure").Arguments[0]!;
        Assert.Equal("app-a.", Read(options, "storagePrefix"));
        Assert.Equal("/logo.png", Read(options, "notifyIcon"));
        Assert.Equal(true, Read(options, "langCookie"));
        // The default theme travels too: the script stamps data-theme from it with nothing
        // stored, and SednaUiBrand.ToCss emits this theme — not "sedna" — at bare :root.
        Assert.Equal("northwind", Read(options, "themeDefault"));
        // The one word the library writes into the page itself.
        Assert.Equal("Schließen", Read(options, "toastDismissLabel"));
    }

    [Fact]
    public async Task Settings_are_saved_through_the_scripts_own_path()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await Wrapper().SaveSettingAsync("variant", "light");

        // Not setItem: settings.save also stamps <html> and writes the language
        // cookie, which a raw localStorage write would skip.
        var call = Only("sednaUi.settings.save");
        Assert.Equal("variant", call.Arguments[0]);
        Assert.Equal("light", call.Arguments[1]);
    }

    [Fact]
    public async Task Storage_helpers_do_not_apply_the_library_prefix()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await Wrapper(new SednaUiOptions { StoragePrefix = "app-a." })
            .SetItemAsync("lastQueue", "42");

        // A plain bridge for the app's own keys, not a view onto library settings.
        Assert.Equal("lastQueue", Only("sednaUi.setItem").Arguments[0]);
    }

    [Fact]
    public async Task A_palette_command_travels_as_data_with_an_href()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await Wrapper().RegisterCommandsAsync(
            [new PaletteCommand { Label = "Open the queue", Href = "/queue", Icon = "ri-inbox-line" }]);

        var commands = Assert.IsAssignableFrom<IReadOnlyList<PaletteCommand>>(
            Only("sednaUi.palette.register").Arguments[0]!);
        var command = Assert.Single(commands);

        // A function does not cross the boundary, so navigation is the one action a
        // serialisable command can carry.
        Assert.Equal("/queue", command.Href);
    }

    [Fact]
    public async Task A_search_item_travels_as_data_with_an_href()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await Wrapper().RegisterSearchAsync(
            [new SearchItem { Title = "Badges", Href = "/badge", Code = "/badge", Meta = "Semantic pills." }]);

        var items = Assert.IsAssignableFrom<IReadOnlyList<SearchItem>>(
            Only("sednaUi.search.register").Arguments[0]!);
        var item = Assert.Single(items);

        // Same boundary as the palette: the index is data, and navigation is the
        // one action a serialisable result can carry.
        Assert.Equal("/badge", item.Href);
    }

    [Fact]
    public async Task The_time_zone_is_read_through_the_script_and_stored_nowhere()
    {
        JSInterop.Setup<string?>("sednaUi.timeZone").SetResult("Pacific/Auckland");

        var zone = await Wrapper().GetTimeZoneAsync();

        Assert.Equal("Pacific/Auckland", zone);
        // No arguments, and not a settings key: the zone is read from Intl on every
        // call, so there is no stored value to go stale when a reader travels — and
        // nothing for an app to have to invalidate.
        Assert.Empty(Only("sednaUi.timeZone").Arguments);
    }

    // The wrappers build anonymous objects, which is what the JSON serialiser sees.
    private static object? Read(object bag, string property) =>
        bag.GetType().GetProperty(property)?.GetValue(bag);
}

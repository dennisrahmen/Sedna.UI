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

        await Wrapper().ToastAsync("Approved INC0031209", ToastKind.Go, title: "Done",
            timeoutMs: 0, dismissible: false);

        var call = Only("sednaUi.toast");
        Assert.Equal("Approved INC0031209", call.Arguments[0]);

        var options = call.Arguments[1]!;
        // The script's own vocabulary, which is also the CSS modifier suffix.
        Assert.Equal("go", Read(options, "kind"));
        Assert.Equal("Done", Read(options, "title"));
        // 0 means "stays until dismissed", so it has to survive as 0 rather than
        // being treated as "not set" and defaulted back to 4000.
        Assert.Equal(0, Read(options, "timeout"));
        Assert.Equal(false, Read(options, "dismissible"));
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

        Assert.Equal(expected, Read(Only("sednaUi.toast").Arguments[1]!, "kind"));
        // .toast-go, .toast-warn, .toast-danger, .toast-info all exist.
        Assert.Contains($".toast-{expected}", Assets.Css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirm_maps_its_labels_onto_the_scripts_option_names()
    {
        JSInterop.Setup<bool>("sednaUi.confirm", _ => true).SetResult(true);

        var answer = await Wrapper().ConfirmAsync("Delete the queue?", "This cannot be undone.",
            confirmLabel: "Delete", cancelLabel: "Keep", danger: true);

        Assert.True(answer);
        var options = Only("sednaUi.confirm").Arguments[0]!;
        Assert.Equal("Delete the queue?", Read(options, "title"));
        Assert.Equal("This cannot be undone.", Read(options, "message"));
        // The script reads `confirm` and `cancel`, not `confirmLabel`.
        Assert.Equal("Delete", Read(options, "confirm"));
        Assert.Equal("Keep", Read(options, "cancel"));
        Assert.Equal(true, Read(options, "danger"));
    }

    [Fact]
    public async Task Configure_sends_every_option_the_script_accepts()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await Wrapper(new SednaUiOptions
        {
            StoragePrefix = "app-a.",
            NotifyIcon = "/logo.png",
            LangCookie = true,
        }).ConfigureAsync();

        var options = Only("sednaUi.configure").Arguments[0]!;
        Assert.Equal("app-a.", Read(options, "storagePrefix"));
        Assert.Equal("/logo.png", Read(options, "notifyIcon"));
        Assert.Equal(true, Read(options, "langCookie"));
    }

    [Fact]
    public async Task Settings_are_saved_through_the_scripts_own_path()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await Wrapper().SaveSettingAsync("theme", "light");

        // Not setItem: settings.save also stamps <html> and writes the language
        // cookie, which a raw localStorage write would skip.
        var call = Only("sednaUi.settings.save");
        Assert.Equal("theme", call.Arguments[0]);
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

        // A callback cannot cross the boundary — the library never calls back into
        // .NET — so navigation is the one action a serialisable command can carry.
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

    // The wrappers build anonymous objects, which is what the JSON serialiser sees.
    private static object? Read(object bag, string property) =>
        bag.GetType().GetProperty(property)?.GetValue(bag);
}

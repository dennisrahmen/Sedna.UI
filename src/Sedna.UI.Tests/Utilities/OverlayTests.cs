using Bunit;
using Bunit.JSInterop.InvocationHandlers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Sedna.UI.Tests;

/// <summary>
/// <see cref="ISednaOverlays"/> and <see cref="SednaOverlayHost"/>: an app component rendered,
/// its own dialog opened, and the result it closed with handed back.
/// </summary>
/// <remarks>
/// The script end — <c>modal.show</c> settling on every route out of a dialog — is proved
/// in a real browser by <c>Script/ModalTests</c>. These pin the .NET half: that the host adds
/// no element of its own, that the dialog is opened only once the component has rendered,
/// and that each way of closing maps to the documented result.
/// </remarks>
public class OverlayTests : BunitContext
{
    private readonly JSRuntimeInvocationHandler<string?> _show;

    public OverlayTests()
    {
        Services.AddSednaUi();
        _show = JSInterop.Setup<string?>("sednaUi.modal.show", _ => true);
        JSInterop.SetupVoid("sednaUi.modal.close", _ => true).SetVoidResult();
        JSInterop.SetupVoid("sednaUi.modal.idle", _ => true).SetVoidResult();
    }

    private ISednaOverlays Overlays => Services.GetRequiredService<ISednaOverlays>();

    [Fact]
    public void The_host_renders_no_element_of_its_own()
    {
        var host = Render<SednaOverlayHost>();

        // A presenter adds nothing the app did not write — with nothing open, nothing at all.
        Assert.Equal(string.Empty, host.Markup.Trim());
    }

    [Fact]
    public async Task The_component_writes_the_dialog_and_its_result_is_returned()
    {
        var host = Render<SednaOverlayHost>();

        // The Action overload: the Func<Task> one would await the whole wait, which ends only
        // when the test closes the dialog below.
        Task<bool?> shown = null!;
        await host.InvokeAsync(() => { shown = Overlays.ShowAsync<DeleteQueue, bool?>(
            new() { [nameof(DeleteQueue.Queue)] = "returns-eu" }); });

        // The only markup is the component's own, carrying the id the presenter opens.
        host.WaitForAssertion(() =>
            Assert.Equal("sedna-overlay-1", host.Find("dialog.modal").GetAttribute("id")));
        Assert.Contains("returns-eu", host.Find("dialog h3").TextContent, StringComparison.Ordinal);
        Assert.Equal("sedna-overlay-1", _show.Invocations.Single().Arguments[0]);

        var probe = host.FindComponent<DeleteQueue>().Instance;
        await host.InvokeAsync(() => probe.Overlay.CloseAsync(true));
        _show.SetResult(null);

        Assert.True(await shown);
        // And once it has closed, the component leaves the document.
        host.WaitForAssertion(() => Assert.Empty(host.FindAll("dialog")));
    }

    [Fact]
    public async Task Escape_is_the_default_result_and_a_nullable_result_tells_it_apart()
    {
        var host = Render<SednaOverlayHost>();

        Task<bool?> shown = null!;
        await host.InvokeAsync(() => { shown = Overlays.ShowAsync<DeleteQueue, bool?>(); });
        host.WaitForAssertion(() => host.Find("dialog"));

        // Escape closes the dialog in the browser with no returnValue and no CloseAsync.
        _show.SetResult(null);

        Assert.Null(await shown);
    }

    [Fact]
    public async Task A_string_result_is_the_dialog_method_buttons_value()
    {
        var host = Render<SednaOverlayHost>();

        Task<string?> shown = null!;
        await host.InvokeAsync(() => { shown = Overlays.ShowAsync<DeleteQueue, string>(); });
        host.WaitForAssertion(() => host.Find("dialog"));

        // A <form method="dialog"> button closed it: no C# ran, the value came back.
        _show.SetResult("delete");

        Assert.Equal("delete", await shown);
    }

    [Fact]
    public async Task A_result_of_the_wrong_type_is_an_error_not_a_silent_default()
    {
        var host = Render<SednaOverlayHost>();

        Task<int> shown = null!;
        await host.InvokeAsync(() => { shown = Overlays.ShowAsync<DeleteQueue, int>(); });
        host.WaitForAssertion(() => host.Find("dialog"));

        var probe = host.FindComponent<DeleteQueue>().Instance;
        await host.InvokeAsync(() => probe.Overlay.CloseAsync("not a number"));
        _show.SetResult(null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => shown);
        Assert.Contains("DeleteQueue closed with a String", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Showing_without_a_host_throws_instead_of_never_completing()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Overlays.ShowAsync<DeleteQueue, bool>());

        Assert.Contains("<SednaOverlayHost />", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_host_renders_nothing()
    {
        var first = Render<SednaOverlayHost>();
        var second = Render<SednaOverlayHost>();

        await first.InvokeAsync(() => { _ = Overlays.ShowAsync<DeleteQueue, bool>(); });

        first.WaitForAssertion(() => first.Find("dialog"));
        Assert.Empty(second.FindAll("dialog"));
    }

    /// <summary>An app's confirmation, written the way the documentation tells an app to.</summary>
    private sealed class DeleteQueue : ComponentBase
    {
        [CascadingParameter] public SednaOverlay Overlay { get; set; } = default!;

        [Parameter] public string? Queue { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "dialog");
            builder.AddAttribute(1, "id", Overlay.Id);
            builder.AddAttribute(2, "class", "modal modal-sm");
            builder.OpenElement(3, "h3");
            builder.AddContent(4, $"Delete {Queue}?");
            builder.CloseElement();
            builder.CloseElement();
        }
    }
}

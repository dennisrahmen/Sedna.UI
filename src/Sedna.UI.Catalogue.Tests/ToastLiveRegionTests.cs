using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// An app-rendered toast stack carries its own live region.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue tells a reader not to put <c>role="alert"</c> on a toast, because
/// the library adds the live region when it creates the stack. That is true of
/// <c>sednaUi.toast</c> and false of the other documented path: an app rendering the
/// stack from its own state never has a library-created stack, so following the
/// instruction literally produced toasts that were never announced at all.
/// </para>
/// <para>
/// The fix is in the markup a reader copies rather than only in the prose beside it,
/// and this is what keeps it there. Every example is a file the page renders and
/// prints from the same bytes, so an example that carries the attribute cannot
/// document one thing and demonstrate another.
/// </para>
/// </remarks>
public class ToastLiveRegionTests
{
    [Fact]
    public void The_app_rendered_stack_example_is_announced()
    {
        var markup = File.ReadAllText(
            Path.Combine(CatalogueAssets.ExamplesDir, "Toast", "Toast.razor"));

        var stack = markup.Split('\n').First(l => l.Contains("toast-stack", StringComparison.Ordinal));

        Assert.Contains("aria-live=\"polite\"", stack, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", stack, StringComparison.Ordinal);
    }

    [Fact]
    public void No_individual_toast_claims_a_role_of_its_own()
    {
        // Two live regions over one message announce it twice, and `alert` is
        // assertive — so every routine confirmation would cut across whatever is
        // being read.
        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(CatalogueAssets.ExamplesDir, "Toast")))
        {
            var markup = File.ReadAllText(file);
            Assert.DoesNotContain("role=\"alert\"", markup, StringComparison.Ordinal);
        }
    }
}

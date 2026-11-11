using System.Diagnostics;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// No component changes without its design-guide entry being read again.
/// </summary>
/// <remarks>
/// <para>
/// Each entry carries a stamp of the sources that define its page — the page, its examples, and
/// the css and script parts behind the classes the page teaches. <c>build/guide-stamp.sh</c> is
/// the one implementation of that: this test runs its <c>--check</c> and fails with what it
/// printed, which names each page, the sources that changed and the command to run once the
/// entry has been reviewed.
/// </para>
/// <para>
/// The stamp is deliberately strict. A changed example or a changed rule can change what a
/// component is for or what it should be combined with, and only a reader can tell whether it
/// did; restamping without reading is what this exists to prevent.
/// </para>
/// </remarks>
[Trait(Scope.Name, Scope.Site)]
public class GuideStampTests
{
    [Fact]
    public void Every_entry_is_stamped_against_its_page_as_it_is_now()
    {
        var (exit, output) = Run("--check");

        Assert.True(exit == 0,
            "A component changed after its design-guide entry was last reviewed. For each page below, read "
            + "the page and its examples, correct the entry in docs/design-guide/components/ where it no "
            + "longer holds, then run the command it names." + Environment.NewLine + output);
    }

    [Fact]
    public void A_stamp_names_its_sources()
    {
        // The vacuity guard: a check over sources it could not find would pass everything.
        var (exit, output) = Run("--sources", "/table");

        Assert.Equal(0, exit);
        foreach (var source in new[] { "Components/Pages/Table.razor", "Examples/Table/Basic.razor", "css-parts/32-tables.css" })
            Assert.Contains(source, output, StringComparison.Ordinal);
    }

    private static (int Exit, string Output) Run(params string[] arguments)
    {
        var start = new ProcessStartInfo("bash")
        {
            WorkingDirectory = Assets.RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add(Path.Combine(Assets.RepoRoot, "build", "guide-stamp.sh"));
        foreach (var argument in arguments) start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        return (process.ExitCode, stdout.Result + stderr.Result);
    }
}

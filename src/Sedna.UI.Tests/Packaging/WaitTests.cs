using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// No test, in either test project, waits on the test runner's clock.
/// </summary>
/// <remarks>
/// <para>
/// A wait in milliseconds on the runner's side proves something only on a machine fast
/// enough: under load the commands that reach the page arrive late, its frames come late,
/// and a timer that should have fired has not — or a transition that should still be
/// running has ended. Every one of these failed a release run at some point. What a test
/// waits on instead says what it is waiting for:
/// </para>
/// <list type="bullet">
/// <item>a result — <c>WaitForFunctionAsync</c>, an <c>Expect</c>, a load state;</item>
/// <item>the page's own timers — the page clock, paused with <c>clockPausedAt</c> and run
/// with <c>page.Clock.RunForAsync</c>;</item>
/// <item>nothing happening after a change — <c>Js.Frames</c>, the frames every handler has
/// run by;</item>
/// <item>time passing on the page — <c>Js.Elapse</c>, which counts the page's clock and its
/// frames, so a timer shorter than the wait has fired whatever the runner's load;</item>
/// <item>an animation's middle or end — paused and seeked, or finished with <c>Js.Settle</c>.</item>
/// </list>
/// <para>
/// A wait written inside a page script runs on the page's clock already, and is not
/// matched here.
/// </para>
/// </remarks>
public class WaitTests
{
    private static readonly Regex RunnerWait = new(@"\bWaitForTimeoutAsync\(|\bTask\.Delay\(|\bThread\.Sleep\(", RegexOptions.Compiled);

    [Fact]
    public void No_test_waits_on_the_runner_s_clock()
    {
        var offenders = new[] { "Sedna.UI.Tests", "Sedna.UI.Catalogue.Tests" }
            .Select(p => Path.Combine(Assets.RepoRoot, "src", p))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && Path.GetFileName(f) != nameof(WaitTests) + ".cs")
            .SelectMany(f => File.ReadAllLines(f)
                .Select((line, i) => (line, i))
                .Where(x => RunnerWait.IsMatch(x.line) && !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Select(x => $"{Path.GetRelativePath(Assets.RepoRoot, f)}:{x.i + 1}"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Wait on a result, the page clock, Js.Frames, Js.Elapse or the animations instead:\n"
            + string.Join('\n', offenders));
    }
}

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
/// A timer inside a page script is the same wait moved into the page: <c>setTimeout(r, 300)</c>
/// still races whatever the page is doing against the wall clock. One with a delay is allowed
/// only where the timer is the scenario — an app that acts a moment later, a step that must
/// land before the library's own timer on the same clock — and the line says so with
/// <c>page-timer:</c> and why. A zero delay is a task yield, and fine.
/// </para>
/// <para>
/// A wait for a result is not given a shorter limit than the suite's patience either: a cap of
/// a few seconds adds nothing but a way to fail on a loaded runner, and what a test proves
/// did not happen it proves with the page clock or its frames, never with a timeout.
/// </para>
/// </remarks>
public class WaitTests
{
    private static readonly Regex RunnerWait = new(@"\bWaitForTimeoutAsync\(|\bTask\.Delay\(|\bThread\.Sleep\(", RegexOptions.Compiled);
    private static readonly Regex PageTimer = new(@"\bsetTimeout\(.*,\s*[1-9][0-9_]*\s*\)", RegexOptions.Compiled);
    private static readonly Regex ShortLimit = new(@"\bTimeout\s*=\s*([0-9_]+)\b", RegexOptions.Compiled);

    /// <summary>The patience the suite gives an <c>Expect</c> (<c>Expectations.cs</c>); a wait is never given less.</summary>
    private const int Patience = 15_000;

    [Fact]
    public void No_test_waits_on_the_runner_s_clock() =>
        AssertNone(line => RunnerWait.IsMatch(line),
            "Wait on a result, the page clock, Js.Frames, Js.Elapse or the animations instead:");

    [Fact]
    public void No_page_script_in_a_test_waits_on_a_timer_unless_the_timer_is_the_scenario() =>
        AssertNone(line => PageTimer.IsMatch(line) && !line.Contains("page-timer:", StringComparison.Ordinal),
            "Wait on an event, frames or the animations — or mark a timer that is the scenario with `page-timer:` and why:");

    [Fact]
    public void No_wait_is_given_less_than_the_suite_s_patience() =>
        AssertNone(line => ShortLimit.Match(line) is { Success: true } m && int.Parse(m.Groups[1].Value.Replace("_", "", StringComparison.Ordinal), System.Globalization.CultureInfo.InvariantCulture) < Patience,
            $"Leave the limit at its default; a cap under {Patience} ms only adds a way to fail under load:");

    private static void AssertNone(Func<string, bool> offends, string message)
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
                .Where(x => offends(x.line) && !x.line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                .Select(x => $"{Path.GetRelativePath(Assets.RepoRoot, f)}:{x.i + 1}"))
            .ToList();

        Assert.True(offenders.Count == 0, message + "\n" + string.Join('\n', offenders));
    }
}

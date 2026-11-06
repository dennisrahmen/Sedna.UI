using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every workflow that builds the solution generates the class history first, from a checkout
/// with its tags. The history is not committed and both projects embed it, so a workflow that
/// skips the step fails to build — which is how the code scanning job broke when it moved out
/// of the repository.
/// </summary>
public class WorkflowBuildTests
{
    [Fact]
    public void Every_workflow_that_builds_generates_the_class_history_first()
    {
        var dir = Path.Combine(Assets.RepoRoot, ".github", "workflows");
        var problems = new List<string>();
        var checkedAny = false;
        foreach (var file in Directory.GetFiles(dir, "*.yml"))
        {
            var text = File.ReadAllText(file);
            var build = Regex.Match(text, @"dotnet (?:build|pack|test)(?![^\n]*--no-build)");
            if (!build.Success) continue;
            checkedAny = true;
            var history = text.IndexOf("build/class-history.sh", StringComparison.Ordinal);
            if (history < 0 || history > build.Index)
                problems.Add($"{Path.GetFileName(file)} builds before it generates the class history");
            if (!text.Contains("fetch-depth: 0", StringComparison.Ordinal))
                problems.Add($"{Path.GetFileName(file)} checks out without the tags the history is dated by");
        }

        Assert.True(checkedAny, "No workflow builds the solution, so this measures nothing.");
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    /// <summary>
    /// Every job says how long it may take. Without a limit a stalled job — a slow mirror, a
    /// hung browser — holds its workflow for the six hours GitHub allows, and a release waits
    /// on it; one did, for half an hour, before anyone looked.
    /// </summary>
    [Fact]
    public void Every_job_in_every_workflow_has_a_time_limit()
    {
        var dir = Path.Combine(Assets.RepoRoot, ".github", "workflows");
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.yml"))
        {
            var text = File.ReadAllText(file);
            var jobs = text.IndexOf("\njobs:", StringComparison.Ordinal);
            Assert.True(jobs >= 0, $"{Path.GetFileName(file)} has no jobs.");
            var starts = Regex.Matches(text[jobs..], @"^  (?<job>[a-z][a-z0-9_-]*):\s*$", RegexOptions.Multiline).ToList();
            for (var j = 0; j < starts.Count; j++)
            {
                var from = starts[j].Index;
                var to = j + 1 < starts.Count ? starts[j + 1].Index : text.Length - jobs;
                if (!Regex.IsMatch(text[jobs..][from..to], @"^    timeout-minutes:\s*\d+", RegexOptions.Multiline))
                    problems.Add($"{Path.GetFileName(file)}: the {starts[j].Groups["job"].Value} job has no timeout-minutes");
            }
        }
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }
}

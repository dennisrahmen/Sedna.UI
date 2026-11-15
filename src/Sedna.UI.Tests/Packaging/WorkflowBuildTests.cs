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
    /// on it; one did, for half an hour, before anyone looked. A job that runs on a runner
    /// carries `timeout-minutes`; a job that calls a reusable workflow cannot, so it passes
    /// only when every job of the workflow it calls has one; and a limit that is
    /// `${{ matrix.timeout }}` counts only when the plan gives every slice a positive number.
    /// </summary>
    [Fact]
    public void Every_job_in_every_workflow_has_a_time_limit()
    {
        var dir = Path.Combine(Assets.RepoRoot, ".github", "workflows");
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.yml"))
            problems.AddRange(Unlimited(Path.GetFileName(file), new HashSet<string>()));

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private static List<string> Unlimited(string workflow, HashSet<string> calling)
    {
        var problems = new List<string>();
        if (!calling.Add(workflow)) return problems;
        var text = WorkflowYaml.Read(workflow);
        foreach (var (name, job) in WorkflowYaml.Jobs(text))
        {
            var where = $"{workflow}: the {name} job";
            var limit = Regex.Match(job, @"^    timeout-minutes:\s*(?<v>.+?)\s*$", RegexOptions.Multiline);
            var call = Regex.Match(job, @"^    uses:\s*\./\.github/workflows/(?<file>[\w.-]+\.yml)\s*$", RegexOptions.Multiline);

            if (call.Success)
            {
                // A reusable-workflow call has no `timeout-minutes` of its own; the called jobs do.
                if (limit.Success) problems.Add($"{where} calls a reusable workflow and cannot carry timeout-minutes; remove it and put the limits on the called jobs");
                problems.AddRange(Unlimited(call.Groups["file"].Value, calling).Select(p => $"{p} (called by {where})"));
            }
            else if (!Regex.IsMatch(job, @"^    runs-on:", RegexOptions.Multiline))
            {
                problems.Add($"{where} has neither runs-on nor uses, so nothing says how long it may take");
            }
            else if (!limit.Success)
            {
                problems.Add($"{where} has no timeout-minutes");
            }
            else if (limit.Groups["v"].Value == "${{ matrix.timeout }}")
            {
                var slices = WorkflowYaml.Matrix(text);
                if (slices.Count == 0 || slices.Any(s => s.Timeout <= 0))
                    problems.Add($"{where} takes its limit from matrix.timeout, and the plan gives {string.Join(", ", slices.Where(s => s.Timeout <= 0).Select(s => s.Slug).DefaultIfEmpty("no slice"))} none");
            }
            else if (!Regex.IsMatch(limit.Groups["v"].Value, @"^[1-9]\d*$"))
            {
                problems.Add($"{where} has timeout-minutes `{limit.Groups["v"].Value}`, which is not a positive number of minutes");
            }
        }

        calling.Remove(workflow);
        return problems;
    }
}

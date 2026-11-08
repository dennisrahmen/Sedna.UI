using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The words a <c>.workflow</c> takes in <c>data-state</c> — on a step, on a branch, on an
/// option of a decision — are the words <c>68-workflow.css</c> says it takes, and each of
/// them does something.
/// </summary>
/// <remarks>
/// A state the header lists and no rule styles is a word an app writes to no effect, and
/// a state styled but not listed is one nobody can find. Both are silent in a browser.
/// </remarks>
public class WorkflowVocabularyTests
{
    private static readonly string Raw =
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", "68-workflow.css"));

    private static readonly string Css = Assets.StripComments(Raw);

    private static List<string> Documented(string heading) =>
        Regex.Match(Raw, heading + @":\s*(.*?)\.\s*$", RegexOptions.Multiline)
            .Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal)
            .ToList();

    private static List<string> Styled(string selectorBefore) =>
        Regex.Matches(Css, Regex.Escape(selectorBefore) + @"\[data-state=""([a-z]+)""\]")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    [Theory]
    [InlineData("States", ".workflow-step")]
    [InlineData("Branches", ".workflow-branch")]
    [InlineData("Options", ".workflow-decision > ")]
    public void The_documented_states_are_the_styled_ones(string heading, string selector)
    {
        var documented = Documented(heading);
        Assert.NotEmpty(documented);
        Assert.Equal(documented, Styled(selector));
    }

    /// <summary>
    /// The agent's parts take <c>data-state</c> words of their own — a tool call, a step,
    /// a plan item, an approval, a running thought — and each header lists them the same
    /// way the workflow's does.
    /// </summary>
    [Theory]
    [InlineData("66-agent-tools.css", "Tool states", ".agent-tool")]
    [InlineData("66-agent-steps.css", "Step states", ".agent-steps > li")]
    [InlineData("66-agent-steps.css", "Plan states", ".agent-plan > li")]
    [InlineData("66-agent-steps.css", "Approval states", ".agent-approval")]
    [InlineData("66-agent-steps.css", "Reasoning states", ".reasoning")]
    public void The_agent_parts_document_the_states_they_style(string part, string heading, string selector)
    {
        var raw = File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", part));
        var css = Assets.StripComments(raw);

        var documented = Regex.Match(raw, heading + @":\s*(.*?)\.\s*$", RegexOptions.Multiline)
            .Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order(StringComparer.Ordinal)
            .ToList();
        var styled = Regex.Matches(css, Regex.Escape(selector) + @"\[data-state=""([a-z]+)""\]")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.NotEmpty(documented);
        Assert.Equal(documented, styled);
    }

    [Fact]
    public void A_tool_call_reuses_the_words_the_library_already_has()
    {
        // A tool call's states are the library's words for the same things, not a model
        // API's: running, done and failed as on a step, waiting as on a workflow step,
        // approved and denied as on an approval. Only `streaming` is its own, and it is the
        // word `.chat-bubble--streaming` already uses for text still arriving.
        var tools = File.ReadAllText(Path.Combine(Assets.ProjectDir, "css-parts", "66-agent-tools.css"));
        var states = Regex.Match(tools, @"Tool states:\s*(.*?)\.\s*$", RegexOptions.Multiline).Groups[1].Value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var elsewhere = Documented("States")
            .Concat(new[] { "pending", "approved", "denied", "streaming" })
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(states, s => Assert.Contains(s, elsewhere));
        Assert.DoesNotContain(states, s => s.Contains('-', StringComparison.Ordinal));
    }

    [Fact]
    public void Every_step_state_also_paints_the_wire_into_the_step()
    {
        // A wire takes the state of the step it leads into, so each state a step takes
        // has a wire rule as well — `next` and `skipped` dash it, `running` lights it,
        // and the reached ones colour it. Only `.workflow-path > [data-state=…]` counts.
        var wired = Styled(".workflow-path > ");
        Assert.Equal(Documented("States"), wired);
    }
}

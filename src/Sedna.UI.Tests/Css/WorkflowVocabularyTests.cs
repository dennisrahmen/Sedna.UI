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

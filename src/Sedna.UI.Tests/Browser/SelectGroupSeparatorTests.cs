using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Two groups in a dropdown are divided by one line: the second group draws it, and an <c>&lt;hr&gt;</c>
/// written before it — the habit — is the divider instead, never a second line beside it.
/// </summary>
public class SelectGroupSeparatorTests : ScriptTestBase
{
    private const string Fixture = """
        <select class="form-select" id="plain"><optgroup label="A"><legend>A</legend><option>One</option></optgroup>
          <optgroup label="B"><legend>B</legend><option>Two</option></optgroup>
          <optgroup label="C"><legend>C</legend><option>Three</option></optgroup></select>
        <select class="form-select" id="habit"><optgroup label="A"><legend>A</legend><option>One</option></optgroup><hr>
          <optgroup label="B"><legend>B</legend><option>Two</option></optgroup></select>
        <select class="form-select" id="box" multiple size="4"><optgroup label="A"><legend>A</legend><option>One</option></optgroup>
          <optgroup label="B"><legend>B</legend><option>Two</option></optgroup></select>
        """;

    [Fact]
    public async Task One_line_divides_two_groups()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        if (!await page.EvaluateAsync<bool>("() => CSS.supports('appearance', 'base-select')")) return;
        var lines = await page.EvaluateAsync<string[]>("""
            () => {
                const top = (id, n) => getComputedStyle(document.querySelectorAll('#' + id + ' optgroup')[n]).borderTopWidth;
                return [top('plain', 0), top('plain', 1), top('plain', 2), top('habit', 1),
                        getComputedStyle(document.querySelector('#habit hr')).borderTopWidth, top('box', 1)];
            }
            """);
        Assert.Equal(["0px", "1px", "1px", "0px", "1px", "0px"], lines);
        Assert.Empty(errors);
    }
}

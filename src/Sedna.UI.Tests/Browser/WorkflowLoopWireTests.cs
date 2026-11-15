using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The wire out of a loop is one wire, in the colour of the step it leads into — not the loop's
/// colour up to where the way back leaves and the next step's after it.
/// </summary>
/// <remarks>
/// The loop draws its own wire from the last step to its edge in its own colour, so a loop at work
/// followed by a step that has not run read as a wire that started blue and turned grey. The wire
/// into a step is drawn over it, whatever that step's state.
/// </remarks>
public class WorkflowLoopWireTests : ScriptTestBase
{
    private static string Flow(string loopState, string nextState) => $"""
        <div style="padding:24px; width:900px">
          <ol class="workflow workflow--sm" aria-label="Troubleshooting">
            <li class="workflow-loop" data-state="{loopState}">
              <ol class="workflow-path">
                <li class="workflow-step" data-state="done"><article class="workflow-node"><header class="workflow-node-head"><span class="workflow-node-title">Ask</span></header></article></li>
                <li class="workflow-step"><article class="workflow-node" id="last"><header class="workflow-node-head"><span class="workflow-node-title">Check</span></header></article></li>
              </ol>
              <span class="workflow-loop-count"><i class="ri-repeat-line"></i><span>Turn 2</span></span>
            </li>
            <li class="workflow-step"{(nextState.Length > 0 ? $" data-state=\"{nextState}\"" : "")}><span class="workflow-end" id="end"><i class="ri-flag-line"></i><span>Fixed</span></span></li>
          </ol>
        </div>
        """;

    [Theory]
    [InlineData("running", "")]
    [InlineData("running", "done")]
    [InlineData("done", "")]
    [InlineData("waiting", "")]
    public async Task The_wire_out_of_a_loop_is_one_colour(string loopState, string nextState)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Flow(loopState, nextState), deviceScaleFactor: Ink.Scale);
        await Js.Settle(page);

        var edge = await page.EvaluateAsync<double[]>("""
            () => { const a = document.getElementById('last').getBoundingClientRect(), b = document.getElementById('end').getBoundingClientRect(),
                          s = document.querySelector('.workflow').getBoundingClientRect();
                    return [s.left, s.top, s.width, s.height, a.right, b.left, (b.top + b.bottom) / 2]; }
            """);
        var shot = Ink.Png.Decode(await page.ScreenshotAsync(new() { Clip = new() { X = (float)edge[0], Y = (float)edge[1], Width = (float)edge[2], Height = (float)edge[3] } }));

        var y = (int)Math.Round((edge[6] - edge[1]) * Ink.Scale);
        var colours = new List<(int R, int G, int B)>();
        for (var x = (int)Math.Ceiling((edge[4] - edge[0] + 2) * Ink.Scale); x < (edge[5] - edge[0] - 2) * Ink.Scale; x++)
            colours.Add(shot.Pixel(x, y));

        Assert.True(colours.Count > 40, "The wire is drawn between the two steps.");
        var first = colours[0];
        var changed = colours.FindIndex(c => !Ink.Same(c, first, 6));
        Assert.True(changed < 0,
            $"Loop {loopState}, next step '{nextState}': the wire is {first} for {changed / (double)Ink.Scale:0.0}px and then {colours[changed < 0 ? 0 : changed]}.");
        Assert.Empty(errors);
    }
}

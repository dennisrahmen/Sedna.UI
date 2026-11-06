using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A content component follows the width it is given, not the window's: a transfer in a
/// narrow panel stacks on a wide screen, and the same one in a wide panel does not.
/// </summary>
public class ContainerWidthTests : ScriptTestBase
{
    private static string Transfer(int width) => $"""
        <div style="width:{width}px">
          <div class="transfer">
            <select class="form-select" id="from" multiple size="4"><option>Alex Fischer</option></select>
            <div class="transfer-actions"><button class="btn" type="button">Add</button><button class="btn" type="button">Remove</button></div>
            <select class="form-select" id="to" multiple size="4"></select>
          </div>
        </div>
        """;

    [Theory]
    [InlineData(400, true)]
    [InlineData(900, false)]
    public async Task A_transfer_stacks_by_its_own_width(int width, bool stacked)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Transfer(width));
        await page.SetViewportSizeAsync(1400, 800);

        var below = await page.EvaluateAsync<bool>("""
            () => document.getElementById('to').getBoundingClientRect().top > document.getElementById('from').getBoundingClientRect().bottom
            """);
        Assert.Equal(stacked, below);
        Assert.Empty(errors);
    }
}

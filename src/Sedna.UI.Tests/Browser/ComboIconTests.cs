using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A leading icon in a single-select combo sits at the start of the field's one row,
/// before the value, and the value and the actions keep their places.
/// </summary>
public class ComboIconTests : ScriptTestBase
{
    [Fact]
    public async Task A_leading_icon_shares_the_row_with_the_value_and_the_actions()
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled("""
            <div class="form-combo" style="width: 320px">
              <div class="form-combo-box" id="box">
                <i class="form-combo-icon ri-search-line" id="icon"></i>
                <span class="form-combo-value" id="value">Restock the north shelf</span>
                <input class="form-combo-input" id="input" type="text" role="combobox" placeholder=" " aria-expanded="false" />
                <span class="form-combo-actions" id="actions">
                  <button class="form-combo-btn form-combo-toggle" type="button" tabindex="-1" aria-label="Show tasks"><i class="ri-arrow-down-s-line"></i></button>
                </span>
              </div>
            </div>
            """);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
                const b = id => document.getElementById(id).getBoundingClientRect();
                const mid = r => (r.top + r.bottom) / 2;
                return [Math.abs(mid(b('icon')) - mid(b('value'))), Math.abs(mid(b('actions')) - mid(b('value'))),
                        b('value').left - b('icon').right, b('input').left - b('icon').right,
                        b('box').height];
            }
            """);
        Assert.True(r[0] < 2, $"The icon sits {r[0]:0.0}px off the value's line.");
        Assert.True(r[1] < 2, $"The actions sit {r[1]:0.0}px off the value's line.");
        Assert.True(r[2] >= 0 && r[3] >= 0, "The value or the input overlaps the icon.");
    }
}

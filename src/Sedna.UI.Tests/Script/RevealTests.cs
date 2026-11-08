using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A password field's reveal button (<c>[data-reveal]</c>, 21-reveal.js): the field's type and
/// the button's <c>aria-pressed</c> follow each press, the label stays the app's, and the icon
/// that shows is the one for the state.
/// </summary>
public class RevealTests : ScriptTestBase
{
    private const string Fixture = """
        <div class="form-field" style="max-width:320px">
          <label class="form-label" for="pw">Password</label>
          <div class="input-group">
            <input class="form-input" id="pw" type="password" autocomplete="current-password" value="harbour-lantern-42">
            <button class="input-reveal" id="eye" type="button" data-reveal aria-controls="pw" aria-pressed="false" aria-label="Show password">
              <i class="ri-eye-line" id="shut"></i><i class="ri-eye-off-line" id="open"></i>
            </button>
          </div>
        </div>
        """;

    private static Task<string[]> State(IPage page, string field = "pw", string button = "eye") =>
        page.EvaluateAsync<string[]>("""
            ([f, b]) => {
              const field = document.getElementById(f), button = document.getElementById(b);
              return [field.type, button.getAttribute('aria-pressed'), button.getAttribute('aria-label')];
            }
            """, new[] { field, button });

    [Fact]
    public async Task A_press_shows_the_password_and_another_hides_it_with_the_state_announced()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.ClickAsync("#eye");
        Assert.Equal(["text", "true", "Show password"], await State(page));
        Assert.Equal("harbour-lantern-42", await page.InputValueAsync("#pw"));

        await page.ClickAsync("#eye");
        Assert.Equal(["password", "false", "Show password"], await State(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_keyboard_presses_it_as_any_button()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        await page.FocusAsync("#eye");
        await page.Keyboard.PressAsync("Space");
        Assert.Equal(["text", "true", "Show password"], await State(page));
        await page.Keyboard.PressAsync("Enter");
        Assert.Equal(["password", "false", "Show password"], await State(page));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task Without_aria_controls_it_reveals_the_password_field_beside_it()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="input-group">
              <input class="form-input" id="other" type="password" value="a">
              <button class="input-reveal" id="near" type="button" data-reveal aria-pressed="false" aria-label="Show password"><i class="ri-eye-line"></i></button>
            </div>
            """);

        await page.ClickAsync("#near");
        Assert.Equal(["text", "true", "Show password"], await State(page, "other", "near"));
        await page.ClickAsync("#near");
        Assert.Equal(["password", "false", "Show password"], await State(page, "other", "near"));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_first_icon_shows_while_hidden_and_the_second_while_shown()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);
        const string Shown = "() => ['shut', 'open'].map(id => getComputedStyle(document.getElementById(id)).display !== 'none')";

        Assert.Equal(new[] { true, false }, await page.EvaluateAsync<bool[]>(Shown));
        await page.ClickAsync("#eye");
        Assert.Equal(new[] { false, true }, await page.EvaluateAsync<bool[]>(Shown));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task It_sits_inside_the_group_at_the_fields_height()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var r = await page.EvaluateAsync<double[]>("""
            () => {
              const g = document.querySelector('.input-group').getBoundingClientRect();
              const b = document.getElementById('eye').getBoundingClientRect();
              return [g.top, g.bottom, g.right, b.top, b.bottom, b.right, g.height];
            }
            """);

        Assert.Equal(36, r[6], 0.5);
        Assert.True(r[3] > r[0] && r[4] < r[1], "The reveal button spills out of the group's height.");
        Assert.True(r[5] <= r[2], "The reveal button sits outside the group.");
        Assert.Empty(errors);
    }
}

using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A theme, colour-blind or density toggle changes token values only, never geometry.
/// </summary>
public class ThemeToggleTests : ScriptTestBase
{
    [Fact]
    public async Task The_theme_and_density_toggles_only_change_token_values()
    {
        if (NoBrowser) return;

        var (page, errors) = await OpenStyled("<p>fixture</p>");
        Assert.Empty(errors);

        // The whole override model rests on this: if flipping the theme changed a
        // rule rather than a token, CSS load order would become load-bearing again
        // and an app's own stylesheet would start winning by accident.
        var report = await page.EvaluateAsync<string[]>(@"() => {
            const probe = document.createElement('div');
            probe.style.cssText = 'position:absolute;left:-9999px;top:0';
            probe.innerHTML = `<div class='card'><div class='card-body'>
                <button class='btn btn-primary'>b</button>
                <table class='table'><tr><th>h</th><td>d</td></tr></table>
            </div></div>`;
            document.body.appendChild(probe);

            const watch = ['.card', '.btn-primary', '.table th', '.table td'];
            const geometry = ['padding', 'margin', 'borderWidth', 'borderRadius', 'fontSize',
                              'fontWeight', 'display', 'minHeight', 'gap'];
            const snap = () => watch.map(s => {
                const el = probe.querySelector(s), cs = getComputedStyle(el);
                return s + '|' + geometry.map(p => p + ':' + cs[p]).join(';');
            });

            const root = document.documentElement;
            const dark = snap();
            root.setAttribute('data-theme', 'light');
            const light = snap();
            root.setAttribute('data-cvd', '1');
            const cvd = snap();
            root.removeAttribute('data-theme'); root.removeAttribute('data-cvd');
            probe.remove();

            const out = [];
            for (let i = 0; i < dark.length; i++) {
                if (light[i] !== dark[i]) out.push('theme changed geometry: ' + dark[i] + ' -> ' + light[i]);
                if (cvd[i] !== light[i]) out.push('cvd changed geometry: ' + light[i] + ' -> ' + cvd[i]);
            }
            return out;
        }");

        Assert.True(report.Length == 0,
            "A theme or palette toggle moved geometry, not just colour:"
            + Environment.NewLine + string.Join(Environment.NewLine, report));

        await page.CloseAsync();
    }
}

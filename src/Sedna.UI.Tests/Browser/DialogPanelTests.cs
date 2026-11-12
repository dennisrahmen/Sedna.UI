using System.Text.RegularExpressions;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A dialog class draws its panel on the class, so its markup looks the same out of a
/// <c>&lt;dialog&gt;</c> as in one.
/// </summary>
/// <remarks>
/// <para>
/// Every rule of the command palette sat on <c>dialog.palette</c>, so a palette shown in
/// the page — a static picture of one, an app that keeps its commands on screen — had no
/// background, border or corners, and the catalogue wrapped its palette demos in a card
/// to stand in for them. <c>.modal</c> was already split: the panel on the class, the
/// placing, the backdrop and the entrance on <c>dialog.modal</c>.
/// </para>
/// <para>
/// The dialog classes are found as <see cref="DialogTests"/> finds them, so a new one is
/// held the day it is written.
/// </para>
/// </remarks>
public class DialogPanelTests : ScriptTestBase
{
    /// <summary>The declarations that are the panel, as opposed to its place or its backdrop.</summary>
    private static readonly string[] Panel = ["background", "background-color", "border-radius", "box-shadow"];

    [Theory]
    [MemberData(nameof(DialogTests.DialogClasses), MemberType = typeof(DialogTests))]
    public void A_dialog_class_draws_its_panel_on_the_class(string name)
    {
        var css = Assets.StripComments(Assets.Css);

        // `transparent` and `none` take the UA's dialog ground away rather than paint one,
        // which is a dialog's own business — the lightbox's stage is the whole screen.
        var onDialog = Regex.Matches(css, @"(?<selector>[^{}@;]*)\{(?<body>[^{}]*)\}")
            .Where(m => Regex.IsMatch(m.Groups["selector"].Value, $@"(?<![\w-])dialog\.{Regex.Escape(name)}(?![\w-])"))
            .Where(m => !m.Groups["selector"].Value.Contains("::", StringComparison.Ordinal))
            .SelectMany(m => Regex.Matches(m.Groups["body"].Value, @"(?<![\w-])(?<prop>[a-z-]+)\s*:\s*(?<value>[^;]+)")
                .Where(d => Panel.Contains(d.Groups["prop"].Value))
                .Where(d => !Regex.IsMatch(d.Groups["value"].Value.Trim(), @"^(?:transparent|none)$"))
                .Select(d => $"{Assets.Squash(m.Groups["selector"].Value)} {{ {d.Groups["prop"].Value}: {d.Groups["value"].Value.Trim()} }}"))
            .ToList();

        Assert.True(onDialog.Count == 0,
            $"The panel of .{name} is drawn only on a <dialog>, so the same markup in the page has none. "
            + $"Move it to .{name}:{Environment.NewLine}{string.Join(Environment.NewLine, onDialog)}");
    }

    [Fact]
    public async Task A_palette_in_the_page_is_the_panel_the_dialog_draws()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("""
            <div class="palette" id="rest" style="width:480px"><input class="palette-input" aria-label="Run a command" /></div>
            <dialog class="palette" id="dlg"><input class="palette-input" aria-label="Run a command" /></dialog>
            <dialog class="palette" id="closed"><input class="palette-input" aria-label="Run a command" /></dialog>
            """);
        await Js.Run(page, "() => document.getElementById('dlg').showModal()");

        var looks = await page.EvaluateAsync<Dictionary<string, string[]>>("""
            () => Object.fromEntries(['rest', 'dlg', 'closed'].map(id => {
                const cs = getComputedStyle(document.getElementById(id));
                return [id, [cs.display, cs.backgroundColor, cs.borderTopWidth, cs.borderTopColor, cs.borderTopLeftRadius, cs.boxShadow, cs.paddingTop]];
            }))
            """);

        Assert.True(looks["rest"][0] == "flex", $"{Engine}: a palette in the page is display {looks["rest"][0]}.");
        Assert.True(looks["rest"][1..].SequenceEqual(looks["dlg"][1..]),
            $"{Engine}: the palette in the page is [{string.Join(" | ", looks["rest"])}] and the dialog [{string.Join(" | ", looks["dlg"])}].");
        Assert.NotEqual("0px", looks["rest"][4]);
        Assert.Equal("none", looks["closed"][0]);
        Assert.Empty(errors);
    }
}

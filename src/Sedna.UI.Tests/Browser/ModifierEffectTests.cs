using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A modifier changes the declaration it names, measured in a layout engine.
/// </summary>
/// <remarks>
/// <para>
/// The gap this closes: nothing else in the suite can tell a modifier that works from
/// one that is outranked. <c>CoverageTests</c> asks whether a class is <i>mentioned</i>
/// in the catalogue, <c>TokenDeclarationTests</c> whether a <c>var()</c> is declared —
/// so a class that parses, ships, gets a catalogue example and does absolutely nothing
/// passes every guard there is.
/// </para>
/// <para>
/// Which is what happened. <c>textarea.form-input { resize: vertical }</c> weighs
/// (0,1,1), because only a textarea is resizable, and the modifiers were written as bare
/// <c>.form-input--resize-none</c> at (0,1,0) — so both lost to the rule they existed to
/// override and every text area stayed vertical. In the same cascade layer, specificity
/// decides, and the modifier is the shorter selector.
/// </para>
/// <para>
/// Assert the computed value, not the stylesheet text: the whole failure is that the
/// declaration is present and unreachable.
/// </para>
/// </remarks>
public class ModifierEffectTests : ScriptTestBase
{
    private const string Fixture =
        """
        <div style="padding:20px; width:520px">
          <textarea class="form-input" id="base" rows="2"></textarea>
          <textarea class="form-input form-input--resize-none" id="none" rows="2"></textarea>
          <textarea class="form-input form-input--resize-both" id="both" rows="2"></textarea>

          <input class="form-input" id="w-base" />
          <input class="form-input sedna-w-fit" id="w-fit" />
          <input class="form-input sedna-w" id="w-set" />
          <input class="form-input sedna-w sedna-w--lg" id="w-lg" />
        </div>
        """;

    [Fact]
    public async Task The_resize_modifiers_change_the_axis_they_name()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var resize = await page.EvaluateAsync<string[]>(
            "ids => ids.map(id => getComputedStyle(document.getElementById(id)).resize)",
            new[] { "base", "none", "both" });

        Assert.Equal("vertical", resize[0]);
        Assert.Equal("none", resize[1]);
        Assert.Equal("both", resize[2]);
        Assert.Empty(errors);
    }

    [Fact]
    public async Task The_width_utilities_beat_the_controls_own_full_width_rule()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(Fixture);

        var widths = await page.EvaluateAsync<double[]>(
            "ids => ids.map(id => Math.round(document.getElementById(id).getBoundingClientRect().width))",
            new[] { "w-base", "w-fit", "w-set", "w-lg" });

        var (full, fit, set, large) = (widths[0], widths[1], widths[2], widths[3]);

        // The point of the family: `.form-input` is width:100%, and these are the way out.
        // They live in @layer sedna.utilities, which comes after sedna.paint, so a (0,1,0)
        // utility beats the component rule without !important — the thing a modifier in
        // the same layer could not do.
        Assert.True(fit < full,
            $"sedna-w-fit measured {fit}px against the full-width {full}px, so it did not take effect.");
        Assert.True(set < full,
            $"sedna-w measured {set}px against the full-width {full}px, so it did not take effect.");
        Assert.True(large > set,
            $"sedna-w--lg measured {large}px and the 12rem default {set}px, so the step did nothing.");
        Assert.Empty(errors);
    }
}

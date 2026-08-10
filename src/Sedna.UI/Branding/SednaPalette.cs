namespace Sedna.UI;

/// <summary>
/// The twelve ramps tier 1 declares — everything <see cref="SednaUiBrand.ToCss"/> emits for
/// one theme, in one variant.
/// </summary>
/// <remarks>
/// This is deliberately the same twelve families as <c>00-palette.css</c>: <see cref="Slate"/>,
/// <see cref="Coral"/>, <see cref="Orbit"/>, <see cref="Navy"/>, and the eight categorical
/// support ramps. All twelve are required — a theme that only means to change its brand hue
/// still has to say what its neutrals are, because nothing here falls back to Sedna's own
/// ramps implicitly. <c>--white</c> and <c>--black</c> are not part of a palette: they are the
/// two fixed absolutes <c>00-palette.css</c> declares outside every ramp, and
/// <see cref="SednaUiBrand.ToCss"/> emits them itself.
/// </remarks>
public sealed class SednaPalette
{
    /// <summary>The neutral spine — surfaces and chrome.</summary>
    public SednaRamp Slate { get; }

    /// <summary>The brand ramp.</summary>
    public SednaRamp Coral { get; }

    /// <summary>Accent and information.</summary>
    public SednaRamp Orbit { get; }

    /// <summary>Mark and illustration only — not consumed by any semantic token.</summary>
    public SednaRamp Navy { get; }

    /// <summary>The <c>go</c> family.</summary>
    public SednaRamp Green { get; }

    /// <summary>The <c>warn</c> family.</summary>
    public SednaRamp Amber { get; }

    /// <summary>The <c>danger</c> family.</summary>
    public SednaRamp Crimson { get; }

    /// <summary>The <c>secret</c> family.</summary>
    public SednaRamp Violet { get; }

    /// <summary>Categorical badge hue — no meaning.</summary>
    public SednaRamp Cyan { get; }

    /// <summary>Categorical badge hue — no meaning.</summary>
    public SednaRamp Orange { get; }

    /// <summary>Categorical badge hue — no meaning.</summary>
    public SednaRamp Teal { get; }

    /// <summary>Inline code.</summary>
    public SednaRamp Indigo { get; }

    /// <summary>Builds a palette from all twelve ramps. Every one is required.</summary>
    public SednaPalette(
        SednaRamp slate,
        SednaRamp coral,
        SednaRamp orbit,
        SednaRamp navy,
        SednaRamp green,
        SednaRamp amber,
        SednaRamp crimson,
        SednaRamp violet,
        SednaRamp cyan,
        SednaRamp orange,
        SednaRamp teal,
        SednaRamp indigo)
    {
        Slate = slate ?? throw new ArgumentNullException(nameof(slate));
        Coral = coral ?? throw new ArgumentNullException(nameof(coral));
        Orbit = orbit ?? throw new ArgumentNullException(nameof(orbit));
        Navy = navy ?? throw new ArgumentNullException(nameof(navy));
        Green = green ?? throw new ArgumentNullException(nameof(green));
        Amber = amber ?? throw new ArgumentNullException(nameof(amber));
        Crimson = crimson ?? throw new ArgumentNullException(nameof(crimson));
        Violet = violet ?? throw new ArgumentNullException(nameof(violet));
        Cyan = cyan ?? throw new ArgumentNullException(nameof(cyan));
        Orange = orange ?? throw new ArgumentNullException(nameof(orange));
        Teal = teal ?? throw new ArgumentNullException(nameof(teal));
        Indigo = indigo ?? throw new ArgumentNullException(nameof(indigo));
    }

    /// <summary>Every ramp, paired with the CSS family name it emits as (<c>--{family}-{step}</c>).</summary>
    internal IEnumerable<(string Family, SednaRamp Ramp)> Ramps()
    {
        yield return ("slate", Slate);
        yield return ("coral", Coral);
        yield return ("orbit", Orbit);
        yield return ("navy", Navy);
        yield return ("green", Green);
        yield return ("amber", Amber);
        yield return ("crimson", Crimson);
        yield return ("violet", Violet);
        yield return ("cyan", Cyan);
        yield return ("orange", Orange);
        yield return ("teal", Teal);
        yield return ("indigo", Indigo);
    }
}

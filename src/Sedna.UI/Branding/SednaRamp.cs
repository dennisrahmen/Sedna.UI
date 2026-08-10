using System.Collections.Immutable;

namespace Sedna.UI;

/// <summary>
/// One palette ramp — a hue at a set of lightness steps, e.g. the eleven steps of the coral
/// ramp in <c>00-palette.css</c>.
/// </summary>
/// <remarks>
/// A ramp is either supplied whole (<see cref="SednaRamp(IReadOnlyDictionary{int, string})"/>
/// — how <see cref="SednaTheme.Sedna"/> is built, because Sedna's own ramps carry measured
/// contrast and must not be re-derived) or generated from one anchor colour
/// (<see cref="FromAnchor"/> — how a new theme's ramps come to exist). Both paths produce the
/// same type, so <see cref="SednaUiBrand.ToCss"/> never needs to know which one built a given
/// ramp.
/// </remarks>
public sealed class SednaRamp
{
    private readonly ImmutableSortedDictionary<int, string> _steps;

    /// <summary>The steps this ramp defines, in ascending order (e.g. 50, 100, 200, … 950).</summary>
    public IReadOnlyList<int> Steps => _steps.Keys.ToImmutableArray();

    /// <summary>The <c>#rrggbb</c> colour at <paramref name="step"/>.</summary>
    /// <exception cref="KeyNotFoundException">The ramp has no such step.</exception>
    public string this[int step] =>
        _steps.TryGetValue(step, out var hex)
            ? hex
            : throw new KeyNotFoundException(
                $"This ramp has no step {step}. Defined steps: {string.Join(", ", _steps.Keys)}.");

    /// <summary>Builds a ramp from an already-designed set of steps, taken verbatim.</summary>
    /// <param name="steps">Step number (50–950) to <c>#rrggbb</c> colour. Must not be empty.</param>
    public SednaRamp(IReadOnlyDictionary<int, string> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        if (steps.Count == 0)
            throw new ArgumentException("A ramp must define at least one step.", nameof(steps));

        var builder = ImmutableSortedDictionary.CreateBuilder<int, string>();
        foreach (var (step, hex) in steps)
        {
            // Validated and normalised to lowercase here, once, so every consumer of `this[]`
            // — the CSS emitter, the reproduction test — can assume a clean #rrggbb literal.
            builder[step] = NormaliseHex(hex);
        }

        _steps = builder.ToImmutable();
    }

    /// <summary>
    /// Generates a full ramp from one anchor colour, per <c>docs/BRANDING.md</c> §2.1: a
    /// lightness curve shared by every hue, and a chroma bell that peaks at the anchor.
    /// </summary>
    /// <param name="anchorHex">The exact, unrounded anchor colour, e.g. Sedna Red <c>#FF6B4A</c>.</param>
    /// <param name="anchorStep">
    /// Which step the anchor sits at — 500 for coral, 400 for orbit. The anchor's own hue and
    /// chroma are carried through unchanged at this step; every other step is generated.
    /// </param>
    /// <param name="steps">
    /// Which steps to generate. Defaults to the eleven standard steps (50…950) that coral,
    /// orbit and navy use. Every value must be one of <see cref="StandardSteps"/>.
    /// </param>
    /// <remarks>
    /// <para>
    /// <b>Lightness</b> comes from a fixed step → L table (<see cref="LightnessCurve"/>),
    /// shared by every generated ramp regardless of hue — the same claim
    /// <c>docs/BRANDING.md</c> makes about Sedna's own ramps. It was fit, once, from Sedna's
    /// eight <i>support</i> ramps (green, amber, crimson, violet, cyan, orange, teal, indigo)
    /// plus navy for the two extreme steps the support ramps don't reach — deliberately not
    /// from coral or orbit, so that reproducing coral and orbit from their anchors is a
    /// genuine held-out check rather than a curve fitted to its own answer. See
    /// <c>SednaRampGenerationTests</c> for that check and the measured result.
    /// </para>
    /// <para>
    /// <b>Chroma</b> follows an asymmetric Gaussian bell centred on the anchor step: it decays
    /// faster on the lighter side than the darker side, because sRGB's gamut narrows sharply
    /// as lightness approaches white, which no amount of curve-fitting escapes. The two
    /// half-widths were fit the same way, from the same eight ramps, using each ramp's own
    /// peak-chroma step as its centre.
    /// </para>
    /// <para>
    /// <b>Hue</b> is held constant at the anchor's own hue. Real hand-tuned ramps drift by a
    /// degree or so per step (<c>docs/BRANDING.md</c> §2.1 rule 2); a constant hue is the
    /// simplest model that stays inside that tolerance without inventing a second curve to fit.
    /// </para>
    /// </remarks>
    public static SednaRamp FromAnchor(string anchorHex, int anchorStep, IReadOnlyList<int>? steps = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorHex);
        steps ??= StandardSteps;

        var anchorIndex = IndexOf(anchorStep);
        if (anchorIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(anchorStep), anchorStep,
                $"The anchor step must be one of: {string.Join(", ", StandardSteps)}.");

        var (_, anchorC, anchorH) = Oklch.FromHex(anchorHex);

        var result = new Dictionary<int, string>(steps.Count);
        foreach (var step in steps)
        {
            var index = IndexOf(step);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(steps), step,
                    $"Every generated step must be one of: {string.Join(", ", StandardSteps)}.");

            if (!LightnessCurve.TryGetValue(step, out var l))
                throw new ArgumentOutOfRangeException(nameof(steps), step,
                    $"No lightness curve point for step {step}.");

            var distance = index - anchorIndex;
            var c = distance == 0
                ? anchorC
                : anchorC * Math.Exp(-((double)(distance * distance)) / BellWidth(distance));

            result[step] = Oklch.ToHex(l, c, anchorH);
        }

        return new SednaRamp(result);
    }

    /// <summary>The eleven steps coral, orbit and navy use, and the default for <see cref="FromAnchor"/>.</summary>
    public static IReadOnlyList<int> StandardSteps { get; } = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    private static int IndexOf(int step)
    {
        for (var i = 0; i < StandardSteps.Count; i++)
            if (StandardSteps[i] == step)
                return i;
        return -1;
    }

    /// <summary>
    /// The shared lightness curve, OKLab <c>L</c> per standard step. See the "Lightness"
    /// paragraph on <see cref="FromAnchor"/> for how these eleven numbers were derived.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, double> LightnessCurve = new Dictionary<int, double>
    {
        [50] = 0.9753,
        [100] = 0.9505,
        [200] = 0.8974,
        [300] = 0.8361,
        [400] = 0.7702,
        [500] = 0.7038,
        [600] = 0.6129,
        [700] = 0.5368,
        [800] = 0.4546,
        [900] = 0.3711,
        [950] = 0.2717,
    };

    // Chroma-bell half-widths, in step-index units (index distance, not hex step number). Fit
    // by least squares on ln(C / peakC) against (index distance)^2 through the origin, using
    // Sedna's eight support ramps, each ramp's own highest-chroma step as its centre, sides fit
    // separately. See the "Chroma" paragraph on FromAnchor.
    private const double SigmaLighter = 3.2951;
    private const double SigmaDarker = 5.5115;

    private static double BellWidth(int distance)
    {
        var sigma = distance < 0 ? SigmaLighter : SigmaDarker;
        return sigma * sigma;
    }

    private static string NormaliseHex(string hex)
    {
        // Round-trips through the parser so a malformed value fails here, at construction,
        // rather than silently reaching the emitted stylesheet.
        Oklch.FromHex(hex);
        var span = hex.AsSpan().TrimStart('#');
        return "#" + span.ToString().ToLowerInvariant();
    }
}

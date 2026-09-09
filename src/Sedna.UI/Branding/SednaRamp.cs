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
    /// <param name="contrastSolvedStep">
    /// Solve one nominated step for a WCAG contrast floor instead of reading its lightness off
    /// the shared curve — see <see cref="ContrastSolvedStep"/> for why this is a parameter here
    /// rather than special-cased for any particular step number. <see langword="null"/> (the
    /// default) generates every requested step from the curve, as before.
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
    /// <para>
    /// <b><see cref="ContrastSolvedStep"/></b> — when the caller nominates one, that step skips
    /// both the lightness curve and the chroma bell: its lightness is found by binary search
    /// against <see cref="Oklch.Contrast"/>, hue is still the anchor's own, and chroma is
    /// <see cref="Oklch.MaxChroma"/> at whatever lightness the search lands on. See
    /// <c>docs/BRANDING.md</c> §3.1 for why a shared curve cannot do this and a per-hue solve is
    /// required.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="contrastSolvedStep"/> names a floor this hue cannot reach at any
    /// lightness. Thrown rather than silently emitting a step that fails its own contrast
    /// requirement.
    /// </exception>
    public static SednaRamp FromAnchor(
        string anchorHex,
        int anchorStep,
        IReadOnlyList<int>? steps = null,
        ContrastSolvedStep? contrastSolvedStep = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorHex);
        steps ??= StandardSteps;

        var anchorIndex = IndexOf(anchorStep);
        if (anchorIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(anchorStep), anchorStep,
                $"The anchor step must be one of: {string.Join(", ", StandardSteps)}.");

        if (contrastSolvedStep is { } floor && !steps.Contains(floor.Step))
            throw new ArgumentException(
                $"contrastSolvedStep names step {floor.Step}, which is not in steps ({string.Join(", ", steps)}).",
                nameof(contrastSolvedStep));

        var (_, anchorC, anchorH) = Oklch.FromHex(anchorHex);

        var result = new Dictionary<int, string>(steps.Count);
        foreach (var step in steps)
        {
            var index = IndexOf(step);
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(steps), step,
                    $"Every generated step must be one of: {string.Join(", ", StandardSteps)}.");

            if (contrastSolvedStep is { } solve && solve.Step == step)
            {
                result[step] = SolveForContrast(anchorH, solve);
                continue;
            }

            if (!LightnessCurve.TryGetValue(step, out var l))
                throw new ArgumentOutOfRangeException(nameof(steps), step,
                    $"No lightness curve point for step {step}.");

            var distance = index - anchorIndex;
            // The multiplication is done in double, not cast after the fact. `distance` is a
            // ramp-index delta and cannot overflow an int in practice, but `(double)(d * d)`
            // multiplies as int first and only then widens — which is the shape of a real
            // overflow bug, and CodeQL rightly does not try to prove the bound.
            var squared = (double)distance * distance;
            var c = distance == 0
                ? anchorC
                : anchorC * Math.Exp(-squared / BellWidth(distance));

            result[step] = Oklch.ToHex(l, c, anchorH);
        }

        return new SednaRamp(result);
    }

    /// <summary>The eleven steps coral, orbit and navy use, and the default for <see cref="FromAnchor"/>.</summary>
    public static IReadOnlyList<int> StandardSteps { get; } = [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950];

    /// <summary>
    /// The fourteen steps the surface ramp uses: <see cref="StandardSteps"/> plus 750, 850 and
    /// 925.
    /// </summary>
    /// <remarks>
    /// The dark end is where surfaces live, and the eleven standard steps are too far apart
    /// there — the stylesheet needs a hover fill between 700 and 800, a topbar between 800 and
    /// 900, and a canvas below 900. <see cref="Surface"/> generates all fourteen;
    /// <c>SurfaceRampTests</c> asserts this list covers every <c>--slate-*</c> step the shipped
    /// stylesheet references, so a generated base cannot leave one undefined.
    /// </remarks>
    public static IReadOnlyList<int> SurfaceSteps { get; } =
        [50, 100, 200, 300, 400, 500, 600, 700, 750, 800, 850, 900, 925, 950];

    /// <summary>
    /// Generates the surface ramp — the greys an app's chrome, canvas and cards are painted
    /// from — from one anchor colour.
    /// </summary>
    /// <param name="anchorHex">
    /// The colour the canvas should be, i.e. what <c>--bg</c> resolves to in the dark variant.
    /// A neutral grey gives a neutral base; a tinted one tints the whole ramp proportionally.
    /// </param>
    /// <param name="anchorStep">Which step <paramref name="anchorHex"/> IS. 900 — the canvas — by default.</param>
    /// <remarks>
    /// <para>
    /// <b>Why this is not <see cref="FromAnchor"/>.</b> That method exists for brand hues: its
    /// lightness curve was fit across the coloured ramps and its chroma bell peaks at the anchor
    /// and falls away on both sides. A surface ramp wants neither. Its lightness has to land
    /// where Sedna's own slate lands — 900 is a canvas, not a mid grey, and the shared curve puts
    /// step 900 at more than half lightness — and its chroma has to stay a whisper across the
    /// whole ramp, rising slightly into the dark end, or a "grey" background reads as coloured.
    /// Generating a base with <see cref="FromAnchor"/> produced a washed-out mid-grey canvas,
    /// which is what made a per-theme background impractical before this existed.
    /// </para>
    /// <para>
    /// <b>The profile is measured, not typed.</b> Both curves are read at first use from
    /// <see cref="SednaTheme.Sedna"/>'s own slate ramp through <see cref="Oklch.FromHex"/>, so
    /// they cannot drift from the ramp they describe and no fourteen numbers are retyped here.
    /// Chroma is scaled by the anchor's own chroma at its step, so a pure grey anchor produces a
    /// pure grey ramp and Sedna's own canvas colour reproduces Sedna's slate — which
    /// <c>SurfaceRampTests</c> asserts step by step.
    /// </para>
    /// <para>
    /// The one ordering constraint: this reads <see cref="SednaTheme.Sedna"/>, so a theme whose
    /// own static initialiser calls it must be declared after that property. Lazy, so nothing
    /// pays for it until a theme generates a base.
    /// </para>
    /// </remarks>
    public static SednaRamp Surface(string anchorHex, int anchorStep = 900)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(anchorHex);

        var profile = SurfaceProfile.Value;
        if (!profile.TryGetValue(anchorStep, out var at))
            throw new ArgumentOutOfRangeException(nameof(anchorStep), anchorStep,
                $"The anchor step must be one of: {string.Join(", ", SurfaceSteps)}.");

        var (_, anchorC, anchorH) = Oklch.FromHex(anchorHex);

        // A grey anchor means a grey ramp, not a division by zero.
        var chromaScale = at.C <= 1e-6 ? 0 : anchorC / at.C;
        // The whole profile rotates by however far the anchor sits from the step it stands for,
        // so the base's own hue drift — 248° at the light end to 271° at the dark, which is what
        // keeps light surfaces from reading cold — survives the move to another hue.
        var hueShift = anchorH - at.H;

        var result = new Dictionary<int, string>(SurfaceSteps.Count);
        foreach (var step in SurfaceSteps)
        {
            var (l, c, h) = profile[step];
            result[step] = Oklch.ToHex(l, c * chromaScale, (h + hueShift + 360) % 360);
        }

        return new SednaRamp(result);
    }

    /// <summary>Sedna's slate ramp, measured: the lightness, chroma and hue of each surface step.</summary>
    private static readonly Lazy<IReadOnlyDictionary<int, (double L, double C, double H)>> SurfaceProfile =
        new(() =>
        {
            var slate = SednaTheme.Sedna.Palette.Slate;
            var profile = new Dictionary<int, (double L, double C, double H)>(SurfaceSteps.Count);

            foreach (var step in SurfaceSteps)
                profile[step] = Oklch.FromHex(slate[step]);

            return profile;
        });

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

    /// <summary>
    /// Solves one step for <paramref name="floor"/> instead of the shared curve: hue is fixed at
    /// <paramref name="hue"/>, lightness is found by binary search, and chroma is
    /// <see cref="Oklch.MaxChroma"/> at whatever lightness the search lands on — so the result is
    /// as vivid as the sRGB gamut allows at the contrast boundary, not a fixed chroma carried
    /// over from a different lightness (which is what goes dull; see <c>docs/BRANDING.md</c> §3.1).
    /// </summary>
    /// <remarks>
    /// The search runs entirely in rounded 8-bit hex — every candidate is rendered through
    /// <see cref="Oklch.ToHex"/> and measured with <see cref="Oklch.Contrast"/> before being
    /// compared to the floor — so the ratio this returns is the one the emitted stylesheet
    /// actually has, not a pre-rounding estimate rounding could later undercut.
    /// </remarks>
    private static string SolveForContrast(double hue, ContrastSolvedStep floor)
    {
        string RenderAtMaxChroma(double l) => Oklch.ToHex(l, Oklch.MaxChroma(l, hue), hue);
        double ContrastAt(double l) => Oklch.Contrast(floor.AgainstHex, RenderAtMaxChroma(l));

        // The darkest end of the search range: at L just above 0 the maximum in-gamut chroma is
        // just above 0 too — the sRGB gamut narrows to a point at both ends of the lightness
        // axis — so this is within a hair of true black, the highest contrast any colour at this
        // hue can reach against a lighter background. If even that fails the floor, no lightness
        // on this hue can clear it: a real limit of the hue, not a bug in the search.
        const double darkest = 1e-4;
        const double lightest = 1 - 1e-4;

        var bestPossible = ContrastAt(darkest);
        if (bestPossible < floor.MinimumRatio)
            throw new InvalidOperationException(
                $"Step {floor.Step} at hue {hue:0.0}° cannot reach {floor.MinimumRatio:0.00}:1 "
                + $"contrast against {floor.AgainstHex} at any lightness. The best achievable, at "
                + $"the darkest point on this hue, is {bestPossible:0.00}:1.");

        // Binary search for the lightest (least dark, most vivid) point that still clears the
        // floor: contrast against a fixed light background falls as lightness rises, so this
        // narrows toward the boundary from both sides. `lo` always satisfies the floor by
        // invariant (it starts at `darkest`, which does), so it is always a safe result to return.
        var lo = darkest;
        var hi = lightest;
        for (var i = 0; i < 60; i++)
        {
            var mid = (lo + hi) / 2;
            if (ContrastAt(mid) >= floor.MinimumRatio) lo = mid; else hi = mid;
        }

        return RenderAtMaxChroma(lo);
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

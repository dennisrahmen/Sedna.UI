namespace Sedna.UI;

/// <summary>
/// Nominates one step of a <see cref="SednaRamp.FromAnchor"/> ramp to be solved for a WCAG
/// contrast floor against a fixed background, instead of taking its lightness from the shared
/// curve every other step uses.
/// </summary>
/// <remarks>
/// <para>
/// This exists because a fixed lightness curve cannot guarantee a contrast ratio across hues:
/// OKLCH lightness and WCAG relative luminance weight the red/green/blue channels differently, so
/// two hues at the same OKLCH <c>L</c> can sit far apart in WCAG luminance
/// (<c>docs/BRANDING.md</c> §3.1). A theme's brand step has to be solved per hue, the way Sedna's
/// own <c>coral-600</c> was solved by hand.
/// </para>
/// <para>
/// <b>Why this is a parameter and not a special case inside <see cref="SednaRamp.FromAnchor"/>.</b>
/// Which step needs solving, and against what, is a fact about the semantic tier that consumes
/// the ramp — <c>--brand</c> resolves to <c>var(--coral-600)</c> — not a fact about ramps in
/// general. A ramp has no opinion on which of its steps a theme happens to expose through
/// <c>--brand</c>; the caller that builds a theme does, so the caller names the step.
/// </para>
/// </remarks>
/// <param name="Step">
/// Which standard step to solve. Must be one of the steps <see cref="SednaRamp.FromAnchor"/> is
/// asked to generate.
/// </param>
/// <param name="AgainstHex">
/// The <c>#rrggbb</c> colour the step must contrast against — for the brand slot, <c>--on-solid</c>'s
/// white.
/// </param>
/// <param name="MinimumRatio">
/// The WCAG 2.1 contrast ratio to solve for. Pass a little above the WCAG floor you actually
/// need (e.g. 4.6 for a 4.5:1 requirement): the floor is a cliff, not a plateau, and leaving no
/// margin means a later change to the solver's own precision, or to the shared lightness curve
/// neighbouring steps are read from, could tip a barely-passing result back under it.
/// </param>
public readonly record struct ContrastSolvedStep(int Step, string AgainstHex, double MinimumRatio);

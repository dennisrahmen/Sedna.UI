namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// One heading on the Tokens page and the tokens under it.
/// </summary>
/// <param name="Name">The heading.</param>
/// <param name="Tokens">The token names, in the order they are declared.</param>
/// <param name="Swatch">
/// Whether a colour chip is meaningful. False for the typography and metric
/// groups, where a background swatch would just be an empty box.
/// </param>
/// <param name="Note">
/// One sentence under the heading, for a group whose printed values would otherwise
/// read as broken. Only the safe-area group needs one.
/// </param>
internal sealed record TokenGroup(
    string Name,
    IReadOnlyList<string> Tokens,
    bool Swatch = true,
    string? Note = null);

/// <summary>
/// The token page's grouping.
/// </summary>
/// <remarks>
/// <para>
/// Hand-kept, and deliberately so: the grouping is editorial. What is <i>not</i>
/// hand-kept is whether it is complete — <c>TokenPageTests</c> compares this against
/// the token export, in both directions, so a new token cannot be added without
/// appearing here and a removed one cannot linger. That test was described here long
/// before it existed, and 45 tokens were missing from the page by the time it did.
/// </para>
/// <para>
/// The values are not here at all. A token can be remapped by a theme, the
/// colour-blind palette, a contrast preference or an app override, and only the
/// browser knows which won — so the page reads them from the loaded stylesheet at
/// runtime.
/// </para>
/// </remarks>
internal static class TokenGroups
{
    public static IReadOnlyList<TokenGroup> All { get; } =
    [
        new("Typography", ["--font-sans", "--font-mono"], Swatch: false),

        new("Surfaces, text, lines", [
            "--bg", "--bg-elevated", "--bg-hover", "--fg", "--fg-soft", "--muted",
            "--border", "--border-strong", "--border-hover", "--divider", "--card-bg",
            "--surface-soft", "--surface-strong", "--on-solid", "--redacted",
        ]),

        new("Named surfaces and elevation", [
            "--surface-app", "--surface-chrome", "--surface-content",
            "--surface-raised-1", "--surface-raised-2", "--surface-raised-3",
        ]),

        new("Brand — the app override point", [
            "--brand", "--brand-hover", "--brand-active", "--brand-soft", "--brand-text",
            "--brand-tint", "--brand-tint-strong",
            "--brand-ring", "--brand-ring-soft", "--brand-ring-check",
            "--brand-glow", "--accent",
            // The accent inside a state illustration. It lives with the brand tokens
            // because that is what it points at by default; .empty-state--failed moves
            // it into the danger ramp for its own subtree.
            "--state-accent",
        ]),

        new("Sidebar", [
            "--sidebar-bg", "--sidebar-border", "--sidebar-fg", "--sidebar-active",
            "--sidebar-hover",
        ]),

        new("Status — go (sends outward)", [
            "--go-solid", "--go-hover", "--go-active", "--go-bg", "--go-border", "--go-fg",
            "--go-ring",
        ]),

        new("Status — warn (control changes)", [
            "--warn-solid", "--warn-hover", "--warn-active", "--warn-bg", "--warn-bg-hover",
            "--warn-bg-active", "--warn-border", "--warn-fg", "--warn-ring", "--warn-ring-solid",
        ]),

        new("Status — danger", [
            "--danger-solid", "--danger-bg", "--danger-bg-hover", "--danger-bg-active",
            "--danger-border", "--danger-border-strong", "--danger-fg", "--danger-ring",
        ]),

        new("Status — info & secret", [
            "--info-bg", "--info-border", "--info-fg", "--info-solid", "--info-ring",
            "--secret-bg", "--secret-border", "--secret-fg",
        ]),

        new("Severity — an ordered five-step ramp", [
            "--sev-1-solid", "--sev-1-bg", "--sev-1-border", "--sev-1-fg",
            "--sev-2-solid", "--sev-2-bg", "--sev-2-border", "--sev-2-fg",
            "--sev-3-solid", "--sev-3-bg", "--sev-3-border", "--sev-3-fg",
            "--sev-4-solid", "--sev-4-bg", "--sev-4-border", "--sev-4-fg",
            "--sev-5-solid", "--sev-5-bg", "--sev-5-border", "--sev-5-fg",
        ]),

        new("Categorical badge hues", [
            "--badge-cyan-bg", "--badge-cyan-border", "--badge-cyan-fg",
            "--badge-orange-bg", "--badge-orange-border", "--badge-orange-fg",
            "--badge-teal-bg", "--badge-teal-border", "--badge-teal-fg",
        ]),

        new("Component surfaces", [
            "--badge-bg", "--table-head-bg", "--btn-active-bg", "--code-fg", "--code-bg",
            "--backdrop", "--overlay", "--spotlight-dim", "--tip-bg", "--tip-border", "--tip-fg",
            "--scrollbar", "--scrollbar-hover", "--scroll-shade",
            "--progress-track", "--skeleton-bg", "--skeleton-sheen",
        ]),

        new("Shadows", [
            "--shadow-topbar", "--shadow-bottombar", "--shadow-nav-tools", "--shadow-modal", "--shadow-tip",
            "--shadow-flyout", "--shadow-dropdown", "--shadow-float", "--shadow-pop",
            "--shadow-tile", "--shadow-edge-start", "--shadow-edge-end",
        ], Swatch: false),

        new("Reconnect banner", [
            "--reconnect-warn-bg", "--reconnect-warn-border", "--reconnect-warn-fg",
            "--reconnect-fail-bg", "--reconnect-fail-border", "--reconnect-fail-fg",
        ]),

        new("Spacing scale", [
            "--space-1", "--space-2", "--space-3", "--space-4", "--space-5", "--space-6",
            "--space-7", "--space-8", "--space-9", "--space-10", "--space-11",
        ], Swatch: false),

        new("Type scale", [
            "--text-1", "--text-2", "--text-3", "--text-4", "--text-5", "--text-6",
            "--text-7", "--text-8", "--text-9", "--text-10", "--text-11",
        ], Swatch: false),

        new("Control height", [
            "--control-height-sm", "--control-height", "--control-height-lg",
        ], Swatch: false),

        new("Choice inset", [
            "--choice-inset-check", "--choice-inset-switch",
        ], Swatch: false),

        new("Corner rounding", [
            "--radius-control", "--radius-surface", "--radius-panel", "--radius-inner",
            "--radius-small", "--radius-pill",
        ], Swatch: false),

        new("Motion", [
            "--motion-fast", "--motion-mid", "--motion-slow",
            "--spin-duration", "--progress-duration", "--skeleton-duration", "--pulse-duration",
        ], Swatch: false),

        new("Density and metrics", [
            "--page-max", "--cell-pad-x", "--cell-pad-y", "--card-pad-block", "--card-pad-inline",
            "--list-pad-block", "--list-pad-inline", "--list-gap", "--list-lines", "--code-clamp",
        ], Swatch: false),

        // How much of each viewport edge the device has taken — a home indicator, a
        // notch, a rounded corner. Every one reads 0px in this browser and on every
        // desktop, which is the whole design: `env()` is the device detection, so a
        // rule that adds one is correct everywhere without asking what it is running
        // on. They stay 0px until the host page carries `viewport-fit=cover`.
        //
        // The Note exists because that reads as a broken page rather than as a
        // correct answer — four zeros in a list where every other group shows a real
        // value. It is the one group whose printed value needs a sentence.
        new("Safe area", [
            "--safe-block-start", "--safe-block-end", "--safe-inline-start", "--safe-inline-end",
        ], Swatch: false,
            Note: "0px is the right answer on a desktop, and on a phone with nothing at that "
                + "edge. A value appears where the device actually reserves the edge — a notch, "
                + "a home indicator, a rounded corner — and only once the host page carries "
                + "viewport-fit=cover. Reading env() yourself gives the same zeros; the point "
                + "of the token is that a rule can add the inset without asking what it is "
                + "running on."),

        // Not colours and not sizes: two values the library needs because the browser
        // draws something we cannot reach. `--color-scheme` goes on <html> and is what
        // makes native scrollbars, a <select>'s option list and the date picker's panel
        // follow the theme; `--picker-invert` is how far to invert the calendar glyph,
        // which is a UA glyph in a fixed colour.
        new("Browser-drawn chrome", ["--color-scheme", "--picker-invert"], Swatch: false),

        // ── Tier 1: the palette ────────────────────────────────────────────────
        // The ramps every semantic token above resolves to. Listed here because the
        // token EXPORT contains them — they are `:root` declarations and a design tool
        // reading the export needs them — and because a reader comparing a role against
        // the ramp it points at should not have to open the stylesheet to do it.
        //
        // A palette token is never remapped by theme, variant, colour-vision or
        // contrast, so unlike every group above, what the page reads here is the same
        // in every state.
        new("Palette — the two absolutes", [
            "--white", "--black"
        ]),
        new("Palette — slate (the neutral spine)", [
            "--slate-50", "--slate-100", "--slate-200", "--slate-300", "--slate-400",
            "--slate-500", "--slate-600", "--slate-700", "--slate-750", "--slate-800",
            "--slate-850", "--slate-900", "--slate-925", "--slate-950"
        ]),
        new("Palette — coral (the brand ramp)", [
            "--coral-50", "--coral-100", "--coral-200", "--coral-300", "--coral-400",
            "--coral-500", "--coral-600", "--coral-700", "--coral-800", "--coral-900",
            "--coral-950"
        ]),
        new("Palette — orbit (accent and information)", [
            "--orbit-50", "--orbit-100", "--orbit-200", "--orbit-300", "--orbit-400",
            "--orbit-500", "--orbit-600", "--orbit-700", "--orbit-800", "--orbit-900",
            "--orbit-950"
        ]),
        new("Palette — navy (mark and illustration only)", [
            "--navy-50", "--navy-100", "--navy-200", "--navy-300", "--navy-400", "--navy-500",
            "--navy-600", "--navy-700", "--navy-800", "--navy-900", "--navy-950"
        ]),
        new("Palette — green (go)", [
            "--green-200", "--green-300", "--green-400", "--green-500", "--green-600",
            "--green-700", "--green-800", "--green-900"
        ]),
        new("Palette — amber (warn)", [
            "--amber-200", "--amber-300", "--amber-400", "--amber-500", "--amber-600",
            "--amber-700", "--amber-800", "--amber-900"
        ]),
        new("Palette — crimson (danger)", [
            "--crimson-200", "--crimson-300", "--crimson-400", "--crimson-500",
            "--crimson-600", "--crimson-700", "--crimson-800", "--crimson-900"
        ]),
        new("Palette — violet (secret)", [
            "--violet-200", "--violet-300", "--violet-400", "--violet-500", "--violet-600",
            "--violet-700", "--violet-800", "--violet-900"
        ]),
        new("Palette — cyan", [
            "--cyan-200", "--cyan-300", "--cyan-400", "--cyan-500", "--cyan-600", "--cyan-700",
            "--cyan-800", "--cyan-900"
        ]),
        new("Palette — orange", [
            "--orange-200", "--orange-300", "--orange-400", "--orange-500", "--orange-600",
            "--orange-700", "--orange-800", "--orange-900"
        ]),
        new("Palette — teal", [
            "--teal-200", "--teal-300", "--teal-400", "--teal-500", "--teal-600", "--teal-700",
            "--teal-800", "--teal-900"
        ]),
        new("Palette — indigo (inline code)", [
            "--indigo-200", "--indigo-300", "--indigo-400", "--indigo-500", "--indigo-600",
            "--indigo-700", "--indigo-800", "--indigo-900"
        ]),
    ];

    /// <summary>Every token named on the page, in display order.</summary>
    public static IReadOnlyList<string> AllTokens { get; } =
        All.SelectMany(g => g.Tokens).ToList();
}

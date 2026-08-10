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
internal sealed record TokenGroup(string Name, IReadOnlyList<string> Tokens, bool Swatch = true);

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

        new("Brand — the app override point", [
            "--brand", "--brand-hover", "--brand-active", "--brand-soft", "--brand-text",
            "--brand-tint", "--brand-ring", "--brand-ring-soft", "--brand-ring-check",
            "--brand-glow", "--accent",
        ]),

        new("Sidebar", ["--sidebar-bg", "--sidebar-border", "--sidebar-fg", "--sidebar-active"]),

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
            "--shadow-topbar", "--shadow-nav-tools", "--shadow-modal", "--shadow-tip",
            "--shadow-flyout", "--shadow-dropdown", "--shadow-float", "--shadow-pop",
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

        new("Corner rounding", [
            "--radius-control", "--radius-surface", "--radius-panel", "--radius-inner",
            "--radius-small", "--radius-pill",
        ], Swatch: false),

        new("Motion", [
            "--motion-fast", "--motion-mid", "--motion-slow",
            "--spin-duration", "--progress-duration", "--skeleton-duration", "--pulse-duration",
        ], Swatch: false),

        new("Density and metrics", [
            "--page-max", "--cell-pad-x", "--cell-pad-y", "--code-clamp",
        ], Swatch: false),

        // Not colours and not sizes: two values the library needs because the browser
        // draws something we cannot reach. `--color-scheme` goes on <html> and is what
        // makes native scrollbars, a <select>'s option list and the date picker's panel
        // follow the theme; `--picker-invert` is how far to invert the calendar glyph,
        // which is a UA glyph in a fixed colour.
        new("Browser-drawn chrome", ["--color-scheme", "--picker-invert"], Swatch: false),
    ];

    /// <summary>Every token named on the page, in display order.</summary>
    public static IReadOnlyList<string> AllTokens { get; } =
        All.SelectMany(g => g.Tokens).ToList();
}

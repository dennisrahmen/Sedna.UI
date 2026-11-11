using System.Text.Json;
using Sedna.UI.Catalogue.Components.Docs;
using Sedna.UI.Catalogue.Navigation;

namespace Sedna.UI.Catalogue.Theming;

/// <summary>Whether a token is a palette step (tier 1) or a role (tier 2) — <c>docs/BRANDING.md</c> §4.1.</summary>
internal enum TokenTier { Role, Palette }

/// <summary>Whether the builder offers a colour picker for a token, or a text field.</summary>
internal enum TokenKind { Colour, Value }

/// <summary>One token the theme builder lets an app redefine, with the library's own values.</summary>
/// <param name="Name">The custom property, <c>--brand</c>.</param>
/// <param name="Kind">A colour, or any other value — a length, a duration, a font stack, a shadow.</param>
/// <param name="Tier">A palette step, set once for both variants, or a role.</param>
/// <param name="Dark">The value at bare <c>:root</c>: the dark variant, and the light one unless remapped.</param>
/// <param name="Light">The light block's own value, or null where light keeps the dark value.</param>
internal sealed record ThemeToken(string Name, TokenKind Kind, TokenTier Tier, string Dark, string? Light);

/// <summary>One group of tokens in the builder, as the Tokens page groups them.</summary>
/// <param name="Section">Which of the builder's three sections it sits in.</param>
internal sealed record ThemeTokenGroup(string Name, string Slug, string Section, string? Note, IReadOnlyList<ThemeToken> Tokens);

/// <summary>
/// Every token an app may redefine in its <c>brand.css</c>, with the library's dark and light
/// values, read from the token export the library ships.
/// </summary>
/// <remarks>
/// <para>
/// Nothing here is listed by hand. The names and values are the export's — bare <c>:root</c> and
/// <c>:root[data-variant="light"]</c> — and the grouping is <see cref="TokenGroups"/>, which
/// <c>TokenPageTests</c> holds to the export in both directions. A token the export has and no
/// group names still appears, under <see cref="Ungrouped"/>.
/// </para>
/// <para>
/// Two kinds of token are left out because an app does not set them: one whose value is
/// <c>env()</c>, which the device reports, and one the library declares as <c>initial</c>,
/// which the app's own code writes at runtime.
/// </para>
/// </remarks>
internal sealed class ThemeTokens
{
    public const string LightSelector = ":root[data-variant=\"light\"]";

    public const string ColourSection = "Colour roles";
    public const string ValueSection = "Type, space, shape and motion";
    public const string PaletteSection = "Palette";
    public const string Ungrouped = "Not on the Tokens page yet";

    /// <summary>The three sections, in the order the builder shows them.</summary>
    public static IReadOnlyList<string> Sections { get; } = [ColourSection, ValueSection, PaletteSection];

    private ThemeTokens(
        IReadOnlyList<ThemeTokenGroup> groups,
        IReadOnlyDictionary<string, string> dark,
        IReadOnlyDictionary<string, string> light,
        IReadOnlyList<string> order)
    {
        Groups = groups;
        DarkDefaults = dark;
        LightDefaults = light;
        Order = order;
        ByName = groups.SelectMany(g => g.Tokens).ToDictionary(t => t.Name, StringComparer.Ordinal);
        DarkColours = CssColour.ResolveAll(dark);
        LightColours = CssColour.ResolveAll(LightEffective(dark, light));
    }

    /// <summary>The groups, editable tokens only, in the order the builder lists them.</summary>
    public IReadOnlyList<ThemeTokenGroup> Groups { get; }

    /// <summary>Every editable token by name.</summary>
    public IReadOnlyDictionary<string, ThemeToken> ByName { get; }

    /// <summary>Every token at bare <c>:root</c>, editable or not, as the library declares it.</summary>
    public IReadOnlyDictionary<string, string> DarkDefaults { get; }

    /// <summary>The light block's own declarations, and nothing it inherits.</summary>
    public IReadOnlyDictionary<string, string> LightDefaults { get; }

    /// <summary>The library's colours in the dark variant, resolved.</summary>
    public IReadOnlyDictionary<string, Rgba> DarkColours { get; }

    /// <summary>The library's colours in the light variant, resolved.</summary>
    public IReadOnlyDictionary<string, Rgba> LightColours { get; }

    /// <summary>Every <c>:root</c> token in declaration order: the palette first, then the roles.</summary>
    public IReadOnlyList<string> Order { get; }

    /// <summary>Bare <c>:root</c> with the light block over it, as the cascade applies them.</summary>
    public static Dictionary<string, string> LightEffective(
        IReadOnlyDictionary<string, string> dark, IReadOnlyDictionary<string, string> light)
    {
        var merged = new Dictionary<string, string>(dark, StringComparer.Ordinal);
        foreach (var (name, value) in light) merged[name] = value;
        return merged;
    }

    /// <summary>Reads the library's <c>Sedna.UI.tokens.json</c>.</summary>
    public static ThemeTokens From(JsonDocument export)
    {
        var dark = new Dictionary<string, string>(StringComparer.Ordinal);
        var light = new Dictionary<string, string>(StringComparer.Ordinal);
        var order = new List<string>();

        foreach (var block in export.RootElement.GetProperty("blocks").EnumerateArray())
        {
            if (block.GetProperty("media").ValueKind != JsonValueKind.Null) continue;
            var target = block.GetProperty("selector").GetString() switch
            {
                ":root" => dark,
                LightSelector => light,
                _ => null,
            };
            if (target is null) continue;

            foreach (var token in block.GetProperty("tokens").EnumerateObject())
            {
                var value = token.Value.GetString() ?? string.Empty;
                if (target == dark && !dark.ContainsKey(token.Name)) order.Add(token.Name);
                target[token.Name] = value;
            }
        }

        var darkColours = CssColour.ResolveAll(dark);
        var lightColours = CssColour.ResolveAll(LightEffective(dark, light));

        ThemeToken? Token(string name, bool palette)
        {
            if (!dark.TryGetValue(name, out var value)) return null;
            if (value.Trim() == "initial" || value.Contains("env(", StringComparison.Ordinal)) return null;
            var colour = darkColours.ContainsKey(name) && (!light.ContainsKey(name) || lightColours.ContainsKey(name));
            return new ThemeToken(name, colour ? TokenKind.Colour : TokenKind.Value,
                palette ? TokenTier.Palette : TokenTier.Role, value, light.GetValueOrDefault(name));
        }

        var groups = new List<ThemeTokenGroup>();
        var placed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in TokenGroups.All)
        {
            var palette = group.Name.StartsWith(PaletteSection, StringComparison.Ordinal);
            var tokens = group.Tokens.Select(n => Token(n, palette)).OfType<ThemeToken>().ToList();
            placed.UnionWith(group.Tokens);
            if (tokens.Count == 0) continue;

            var section = palette ? PaletteSection
                : tokens.Any(t => t.Kind == TokenKind.Colour) ? ColourSection
                : ValueSection;
            groups.Add(new ThemeTokenGroup(group.Name, Slug.From(group.Name), section, group.Note, tokens));
        }

        var rest = order.Where(n => !placed.Contains(n)).Select(n => Token(n, palette: false)).OfType<ThemeToken>().ToList();
        if (rest.Count > 0)
            groups.Add(new ThemeTokenGroup(Ungrouped, Slug.From(Ungrouped), ValueSection, null, rest));

        // Sections in their order, and the groups inside each in the Tokens page's order.
        var ordered = groups.OrderBy(g => Sections.ToList().IndexOf(g.Section)).ToList();
        return new ThemeTokens(ordered, dark, light, order);
    }
}

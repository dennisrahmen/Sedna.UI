namespace Sedna.UI.Catalogue.Mcp;

/// <summary>
/// Sent once at the MCP handshake, and typically used as a system message.
/// </summary>
/// <remarks>
/// Phrased as rules rather than description, because a model acts on rules. The
/// version paragraph is the one that matters: an agent copying markup for a class
/// its app does not have produces a page that renders unstyled with no error
/// anywhere, and nothing downstream catches it.
/// </remarks>
internal static class McpInstructions
{
    public const string Text = """
        Sedna.UI is a Blazor design system: one design-token contract plus semantic CSS
        classes. Page content is CSS classes and plain HTML — there is no <DataTable>, no
        <AppShell>, and no component wrapper of any kind. Build a page by copying markup from
        this catalogue and applying the classes.

        Use search first, then get_example for the exact markup. Do not write shared-UI markup
        from memory, and do not invent class names: a class that is not in the stylesheet does
        nothing at all, silently.

        This catalogue is built from the main branch and can be ahead of any released NuGet
        version. Every class, token and example carries `since` — the release it first shipped
        in, or the literal "unreleased" when it is in no release yet. Pass `installedVersion`
        (the version the app has pinned) to search, get_example, describe_class and get_tokens,
        and `meta.warning` names anything that version does not have. Do not copy those.

        Never override a library class in an app's own stylesheet. If something is missing,
        report it against the library rather than working around it locally.
        """;
}

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

        The library draws only its tier 3 surfaces — a toast, a hover hint, a graph, a rich-text
        editor — and every control, menu, panel and legend around one is markup the app writes.

        Dialogs take one of two routes, decided by where the dialog's markup lives. A small
        dialog that is part of a page's own markup — a confirmation, a one-field prompt — is a
        <dialog class="modal" id="..."> written in that page and awaited with
        ISednaUi.ShowModalAsync(id). A modal, drawer or sheet with a .razor file of its own — its
        own parameters, state or validation, or opened from more than one page — is presented
        with ISednaOverlays.ShowAsync<TComponent, TResult>, with one <SednaOverlayHost /> in the
        layout. Never render such a component inline in a page and open it through a @ref.

        Start from the goal, not the component. Search for what the app's user should be able
        to do, in plain words — "let the user upload a file", "confirm a destructive action" —
        and a hit of kind "goal" names the page to start from; get_guide with its id says which
        parts go around it, how they combine and the usual mistake. get_guide with a page route
        says what that component is for, what it is combined with and what it is not for, and
        get_guide with "principles" covers composition, spacing, colour, actions, feedback,
        density, motion and words.

        Use search first, then get_example for the exact markup. Do not write shared-UI markup
        from memory, and do not invent class names: a class that is not in the stylesheet does
        nothing at all, silently. Before handing a page over, pass it to check_markup, which names
        every class the stylesheet does not declare. describe_api answers for a C# or sednaUi
        member: its signatures, its documentation and the examples that use it.

        This catalogue is built from the main branch and can be ahead of any released NuGet
        version. Every class, token and example carries `since` — the release it first shipped
        in, or the literal "unreleased" when it is in no release yet. Pass `installedVersion`
        (the version the app has pinned) to search, get_example, describe_class and get_tokens,
        and `meta.warning` names anything that version does not have. Do not copy those.

        Upgrading an app to a newer release: call get_changes with the version the app has as
        from, which lists every class, token and C# member the releases in between added, removed
        and renamed, and links each release's notes. get_integration_guide with section "upgrade"
        lists every rename too. describe_class answers an old name the same way, under `renamed`. Replace the
        whole class, never a prefix — one old class can become two.

        Never override a library class in an app's own stylesheet. If something is missing,
        report it against the library rather than working around it locally.
        """;
}

namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// Announces that a theme, palette or density toggle was used, so a page showing
/// computed values can re-read them.
/// </summary>
/// <remarks>
/// A scoped service rather than a JavaScript <c>MutationObserver</c> calling back
/// into .NET. The toggles and the pages that care are in the same circuit, so a C#
/// event is both simpler and one fewer boundary to cross — and it keeps the
/// catalogue's own JavaScript to the rule it is held to: it reads, it never writes,
/// and it never calls back.
/// </remarks>
internal sealed class ThemeState
{
    public event Action? Changed;

    public void NotifyChanged() => Changed?.Invoke();
}

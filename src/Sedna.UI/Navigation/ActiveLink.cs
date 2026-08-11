using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;

namespace Sedna.UI;

/// <summary>
/// Active-state matching for a hand-written navigation link.
/// </summary>
/// <remarks>
/// <para>
/// The frame is CSS classes and the markup is yours, so the sidebar is a plain
/// <c>&lt;a class="nav-link"&gt;</c>. The one thing that markup cannot express is
/// which link is the current page. That is what this supplies.
/// </para>
/// <para>
/// Matching drops the query string and the fragment, and treats a trailing slash
/// as insignificant, so <c>/queue</c>, <c>/queue/</c>, <c>/queue?page=2</c> and
/// <c>/queue#top</c> are one address. With the default
/// <see cref="NavLinkMatch.Prefix"/> the match must end on a path-segment
/// boundary, so <c>/queue</c> does not light up on <c>/queue-archive</c>. The link
/// to the app root needs <see cref="NavLinkMatch.All"/>, or it is active
/// everywhere.
/// </para>
/// <para>
/// These are pure functions and do not subscribe to
/// <see cref="NavigationManager.LocationChanged"/>. A page re-rendered by
/// navigation picks up the new state for free. Navigation markup that survives
/// navigation — a sidebar rendered by the layout — has to subscribe and call
/// <c>StateHasChanged</c>.
/// </para>
/// <para>
/// <b>Subscribe in the component that renders the links</b>, not in the layout
/// around it. When a parent re-renders, Blazor only hands new parameters to a
/// child component whose parameter frames differ, so a sidebar whose parameters
/// are unchanged is skipped and keeps rendering the previous address. A
/// subscription one level too high therefore looks correct and does nothing: the
/// active link updates on the next unrelated interaction instead of on
/// navigation. The catalogue's own sidebar is the worked example.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;a class="@Nav.CssClass("queue")" aria-current="@Nav.AriaCurrent("queue")" href="queue"&gt;
///     &lt;i class="ri-inbox-line"&gt;&lt;/i&gt;&lt;span&gt;Queue&lt;/span&gt;
/// &lt;/a&gt;
/// </code>
/// </example>
public static class ActiveLink
{
    /// <summary>
    /// Compares two absolute addresses and reports whether the target is the
    /// current one.
    /// </summary>
    /// <param name="currentAbsoluteUri">The current address, absolute.</param>
    /// <param name="targetAbsoluteUri">The link's address, absolute.</param>
    /// <param name="match">
    /// <see cref="NavLinkMatch.Prefix"/> also matches addresses below the target;
    /// <see cref="NavLinkMatch.All"/> matches only the target itself.
    /// </param>
    /// <returns><see langword="true"/> when the target is the current address.</returns>
    /// <exception cref="ArgumentNullException">Either address is null.</exception>
    public static bool Matches(
        string currentAbsoluteUri,
        string targetAbsoluteUri,
        NavLinkMatch match = NavLinkMatch.Prefix)
    {
        ArgumentNullException.ThrowIfNull(currentAbsoluteUri);
        ArgumentNullException.ThrowIfNull(targetAbsoluteUri);

        var current = Normalise(currentAbsoluteUri);
        var target = Normalise(targetAbsoluteUri);

        if (string.Equals(current, target, StringComparison.OrdinalIgnoreCase)) return true;
        if (match == NavLinkMatch.All) return false;

        // The boundary check is the whole point: without it /queue also matches
        // /queue-archive, and two items light up at once.
        return current.Length > target.Length
            && current.StartsWith(target, StringComparison.OrdinalIgnoreCase)
            && current[target.Length] == '/';
    }

    /// <summary>
    /// Reports whether <paramref name="href"/> is the current page.
    /// </summary>
    /// <param name="navigation">The app's navigation manager.</param>
    /// <param name="href">
    /// The link's address, relative to the base path. <c>""</c> is the app root,
    /// which needs <see cref="NavLinkMatch.All"/> or it is active everywhere. A
    /// null href is never active.
    /// </param>
    /// <param name="match">How the address is compared.</param>
    /// <returns><see langword="true"/> when the link points at the current page.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="navigation"/> is null.</exception>
    public static bool IsActive(
        this NavigationManager navigation,
        string? href,
        NavLinkMatch match = NavLinkMatch.Prefix)
    {
        ArgumentNullException.ThrowIfNull(navigation);
        if (href is null) return false;

        return Matches(navigation.Uri, navigation.ToAbsoluteUri(href).AbsoluteUri, match);
    }

    /// <summary>
    /// Builds the link's class attribute, appending <c>active</c> when it is the
    /// current page.
    /// </summary>
    /// <param name="navigation">The app's navigation manager.</param>
    /// <param name="href">The link's address. See <see cref="IsActive"/>.</param>
    /// <param name="baseClass">
    /// The classes that are always present. Defaults to <c>nav-link</c>; pass
    /// <c>"nav-link nav-link-tool"</c> for a pinned tool link.
    /// </param>
    /// <param name="match">How the address is compared.</param>
    /// <returns>The class attribute value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="navigation"/> is null.</exception>
    public static string CssClass(
        this NavigationManager navigation,
        string? href,
        string baseClass = "nav-link",
        NavLinkMatch match = NavLinkMatch.Prefix)
        => navigation.IsActive(href, match) ? baseClass + " active" : baseClass;

    /// <summary>
    /// Returns <c>"page"</c> when the link is the current page, otherwise
    /// <see langword="null"/>.
    /// </summary>
    /// <param name="navigation">The app's navigation manager.</param>
    /// <param name="href">The link's address. See <see cref="IsActive"/>.</param>
    /// <param name="match">How the address is compared.</param>
    /// <returns>The <c>aria-current</c> value, or null.</returns>
    /// <remarks>
    /// Blazor does not render an attribute whose value is null, so binding this to
    /// <c>aria-current</c> omits the attribute when the link is not current. The
    /// <c>active</c> class only colours the item; this is what is announced.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="navigation"/> is null.</exception>
    public static string? AriaCurrent(
        this NavigationManager navigation,
        string? href,
        NavLinkMatch match = NavLinkMatch.Prefix)
        => navigation.IsActive(href, match) ? "page" : null;

    private static string Normalise(string absoluteUri)
    {
        var cut = absoluteUri.IndexOfAny(['?', '#']);
        var path = cut < 0 ? absoluteUri : absoluteUri[..cut];
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}

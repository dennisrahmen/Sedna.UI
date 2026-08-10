using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Sedna.UI.Tests;

/// <summary>
/// The three extension methods a page actually writes, over a real
/// NavigationManager.
/// </summary>
public class ActiveLinkExtensionTests : BunitContext
{
    private BunitNavigationManager Nav =>
        Services.GetRequiredService<BunitNavigationManager>();

    [Fact]
    public void CssClass_appends_active_and_leaves_the_base_class_alone()
    {
        Nav.NavigateTo("http://localhost/queue");

        Assert.Equal("nav-link active", Nav.CssClass("queue"));
        Assert.Equal("nav-link", Nav.CssClass("topics"));
        // A tool link keeps both of its classes: the helper appends, never replaces.
        Assert.Equal("nav-link nav-link-tool active", Nav.CssClass("queue", "nav-link nav-link-tool"));
    }

    [Fact]
    public void AriaCurrent_is_page_or_absent()
    {
        Nav.NavigateTo("http://localhost/queue");

        // The class alone only colours the item; aria-current is what is announced.
        // Null, not empty: Blazor omits an attribute whose value is null.
        Assert.Equal("page", Nav.AriaCurrent("queue"));
        Assert.Null(Nav.AriaCurrent("topics"));
    }

    [Fact]
    public void A_null_href_is_never_active()
    {
        Nav.NavigateTo("http://localhost/queue");

        Assert.False(Nav.IsActive(null));
        Assert.Equal("nav-link", Nav.CssClass(null));
        Assert.Null(Nav.AriaCurrent(null));
    }

    [Fact]
    public void The_root_link_needs_All()
    {
        Nav.NavigateTo("http://localhost/queue");

        Assert.True(Nav.IsActive(""));                        // the documented trap
        Assert.False(Nav.IsActive("", NavLinkMatch.All));
    }

    [Fact]
    public void The_helpers_reject_a_null_navigation_manager()
    {
        Assert.Throws<ArgumentNullException>(() => ActiveLink.IsActive(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => ActiveLink.CssClass(null!, "x"));
        Assert.Throws<ArgumentNullException>(() => ActiveLink.AriaCurrent(null!, "x"));
    }
}

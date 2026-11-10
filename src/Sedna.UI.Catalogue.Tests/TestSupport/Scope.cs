namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// The trait that marks a test of the website rather than of the library: its search
/// metadata, its routes and links, its MCP server, its own stylesheet and data.
/// </summary>
/// <remarks>
/// A release publishes the package, so <c>release.yml</c> leaves these out
/// (<c>Scope!=Site</c>) and <c>catalogue.yml</c> runs them on every push to main. Every
/// other test here drives the library through the catalogue's pages and stays in the release.
/// </remarks>
public static class Scope
{
    /// <summary>The trait's name.</summary>
    public const string Name = "Scope";

    /// <summary>The value for a test of the website alone.</summary>
    public const string Site = "Site";
}

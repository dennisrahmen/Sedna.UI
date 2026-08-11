using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>
/// Whole artefacts, addressable by URI.
/// </summary>
/// <remarks>
/// <para>
/// The stylesheet is the case resources exist for: a large, verbatim file where
/// ranking and filtering are meaningless. As a <i>tool</i> it would let a model
/// decide to pull 200 KB into its own context; as a <i>resource</i> the user
/// attaches it deliberately, which is the right party to decide.
/// </para>
/// <para>
/// Nothing is resource-only. MCP clients vary widely in resource support and
/// several ignore them entirely, so <c>get_tokens</c> and
/// <c>get_integration_guide</c> duplicate two of these <b>on purpose</b>: the tool
/// is the guaranteed path, the resource the ergonomic one.
/// </para>
/// </remarks>
[McpServerResourceType]
internal sealed class CatalogueResources(CatalogueIndex index, VersionEnvelope versions)
{
    [McpServerResource(UriTemplate = "sednaui://stylesheet", Name = "stylesheet",
        MimeType = "text/css")]
    [Description("The whole shipped stylesheet, exactly as the package delivers it.")]
    public string Stylesheet() => index.RawStylesheet;

    [McpServerResource(UriTemplate = "sednaui://tokens", Name = "tokens",
        MimeType = "application/json")]
    [Description("The design-token export: an ordered array of blocks, media condition included.")]
    public string Tokens() => index.Tokens.RootElement.GetRawText();

    [McpServerResource(UriTemplate = "sednaui://version", Name = "version",
        MimeType = "application/json")]
    [Description("What this catalogue was built from, and the latest released version.")]
    public string Version() =>
        System.Text.Json.JsonSerializer.Serialize(versions.For(null));

    [McpServerResource(UriTemplate = "sednaui://docs/{name}", Name = "docs",
        MimeType = "text/markdown")]
    [Description("A documentation file: getting-started, architecture, CLAUDE.consuming-app, releasing, " +
        "migrating-to-sedna-ui, accessibility, development.")]
    public string Doc(string name) =>
        Docs.Read(name.EndsWith(".md", StringComparison.Ordinal) ? name : name + ".md");
}

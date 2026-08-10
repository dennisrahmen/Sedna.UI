using System.Text.RegularExpressions;
using System.Xml.Linq;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The csproj properties that decide what lands in the .nupkg, and the dependency ban.
/// </summary>
public class PackageConfigTests
{
    [Fact]
    public void The_package_is_configured_the_way_the_release_workflow_expects()
    {
        var project = XDocument.Load(Path.Combine(Assets.ProjectDir, "Sedna.UI.csproj"));

        string? Property(string name) => project
            .Descendants(name)
            .Select(e => e.Value.Trim())
            .FirstOrDefault();

        Assert.Equal("Sedna.UI", Property("PackageId"));
        Assert.Equal("Sedna.UI", Property("AssemblyName"));
        Assert.Equal("net10.0", Property("TargetFramework"));
        Assert.Equal("true", Property("IsPackable"));
        Assert.Equal("README.md", Property("PackageReadmeFile"));
        Assert.Equal("icon.png", Property("PackageIcon"));
        Assert.False(string.IsNullOrWhiteSpace(Property("PackageLicenseExpression")),
            "nuget.org requires a license expression on a public package.");

        // The Razor SDK is what turns wwwroot into _content/Sedna.UI static
        // web assets. Plain Microsoft.NET.Sdk would build and pack fine and ship
        // no CSS at all.
        Assert.Equal("Microsoft.NET.Sdk.Razor", project.Root!.Attribute("Sdk")?.Value);
    }

    [Fact]
    public void The_package_takes_no_third_party_dependency()
    {
        // "Loading anything from a remote URL at runtime" is permanently out of
        // scope, and a third-party package is the same exposure moved to build time:
        // a supply-chain risk, a licence to audit, and a transitive version conflict
        // in every consuming app. Everything the package needs ships inside it.
        //
        // Microsoft.AspNetCore.Components.Web is the one allowed reference, and it is
        // unavoidable: ComponentBase, RenderFragment and NavigationManager live
        // there. It is not a FrameworkReference because that would stop a Blazor
        // WebAssembly app consuming this library.
        var project = XDocument.Load(Path.Combine(Assets.ProjectDir, "Sedna.UI.csproj"));

        var thirdParty = project
            .Descendants("PackageReference")
            .Select(e => e.Attribute("Include")?.Value ?? string.Empty)
            .Where(id => !id.StartsWith("Microsoft.", StringComparison.Ordinal)
                      && !id.StartsWith("System.", StringComparison.Ordinal))
            .ToList();

        Assert.True(thirdParty.Count == 0,
            "The shipped package must not depend on a third-party package. Found: "
            + string.Join(", ", thirdParty));
    }

    [Fact]
    public void The_readme_and_license_packed_into_the_nupkg_exist()
    {
        Assert.True(File.Exists(Path.Combine(Assets.RepoRoot, "README.md")));
        Assert.True(File.Exists(Path.Combine(Assets.RepoRoot, "LICENSE")));
    }

    [Fact]
    public void Only_one_project_in_this_repository_is_packable()
    {
        // The mechanised form of "one package ships from this repo". There are three
        // projects now — the library, the catalogue app and two test suites — and
        // the app taking a dependency the library may not is only safe while it
        // stays unpackable.
        var packable = Directory
            .EnumerateFiles(Path.Combine(Assets.RepoRoot, "src"), "*.csproj",
                SearchOption.AllDirectories)
            .Where(path =>
            {
                var project = XDocument.Load(path);
                var declared = project.Descendants("IsPackable").FirstOrDefault()?.Value;

                // A Web or test SDK defaults to false; a library SDK defaults to
                // true. Absent therefore means "packable" only for the library.
                return declared is null
                    ? project.Root?.Attribute("Sdk")?.Value == "Microsoft.NET.Sdk.Razor"
                    : string.Equals(declared, "true", StringComparison.OrdinalIgnoreCase);
            })
            .Select(Path.GetFileName)
            .ToList();

        Assert.Equal(["Sedna.UI.csproj"], packable);
    }
}

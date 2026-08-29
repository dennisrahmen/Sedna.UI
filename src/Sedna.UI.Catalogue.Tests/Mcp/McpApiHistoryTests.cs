using System.Net.Http.Json;
using System.Text.Json;
using Sedna.UI.Catalogue.Mcp;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// <c>since</c> for an example whose content is C# or JavaScript rather than markup.
/// </summary>
/// <remarks>
/// <para>
/// The failure these guard against had no error in it. An example's floor was the
/// newest release among the CSS classes it applies, and a snippet of
/// <c>ISednaUi</c> calls applies none — so it fell through to <c>latestRelease</c> and
/// reported every JavaScript and C# example in the catalogue as first shipping in
/// whatever the newest release happened to be. An agent following the documented rule,
/// "do not copy anything whose since is newer than your pinned version", concluded the
/// command palette was unavailable while it was fully available from C#.
/// </para>
/// <para>
/// So an example is now dated by the public C# surface it writes as well, and the two
/// implementations of "what is that surface" are held against each other here.
/// </para>
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class McpApiHistoryTests(CatalogueAppFixture app)
{
    /// <summary>
    /// The parsed history and the real assembly agree about today's C# surface.
    /// </summary>
    /// <remarks>
    /// <c>build/api-inventory.sh</c> parses source, because an old tag's surface cannot
    /// be reflected over without building that tag once per CI run, for ever. Reflection
    /// is exact but only for HEAD. Neither alone is trustworthy; the pair is — a parser
    /// that drifts from the compiler's view is reported here rather than quietly
    /// attributing a member to the wrong release. It is the same reason the landing
    /// page's figures are measured twice.
    /// </remarks>
    [Fact]
    public void The_parsed_csharp_history_matches_the_assembly()
    {
        var reflected = CatalogueIndex.ReadPublicApi();
        var recorded = History().GetProperty("csharp").EnumerateObject()
            .Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        var missing = reflected.Except(recorded).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var extra = recorded.Except(reflected).OrderBy(n => n, StringComparer.Ordinal).ToList();

        Assert.True(missing.Count == 0,
            "The assembly exports these and build/api-inventory.sh does not see them, so they "
            + "have no release attributed. Fix the parser, then run build/class-history.sh: "
            + string.Join(", ", missing));
        Assert.True(extra.Count == 0,
            "build/api-inventory.sh reports these and the assembly does not export them, so a "
            + "release is attributed to something nobody can call: " + string.Join(", ", extra));
    }

    /// <summary>Every example has a floor that is not simply "the newest release".</summary>
    [Fact]
    public async Task A_snippet_with_no_classes_is_dated_by_its_csharp()
    {
        // Palette/RegisterCommands is a .txt snippet: it applies no class at all, and
        // it is the example the report that prompted this was written about.
        var example = (await Tool("get_example", new { ids = new[] { "Palette/RegisterCommands" } }))
            .GetProperty("examples").EnumerateArray().Single();

        Assert.Empty(example.GetProperty("classes").EnumerateArray());

        var members = example.GetProperty("csharp").EnumerateArray()
            .Select(m => m.GetString()).ToList();
        Assert.Contains("RegisterCommandsAsync", members);
        Assert.Contains("PaletteCommand", members);

        // The whole point: it reports the release its C# actually shipped in, rather
        // than the newest release the fallback used to hand back.
        var since = example.GetProperty("since").GetString()!;
        Assert.NotEqual("unreleased", since);
        var declared = History().GetProperty("csharp")
            .GetProperty("ISednaUi.RegisterCommandsAsync").GetString();
        Assert.Equal(declared, since);
    }

    /// <summary>A version older than the newest release is not warned about for free.</summary>
    [Fact]
    public async Task An_older_installed_version_is_not_warned_off_a_capability_it_has()
    {
        var declared = History().GetProperty("csharp")
            .GetProperty("ISednaUi.RegisterCommandsAsync").GetString()!;

        var result = await Tool("get_example", new
        {
            ids = new[] { "Palette/RegisterCommands" },
            installedVersion = declared,
        });

        var meta = result.GetProperty("meta");
        Assert.False(meta.TryGetProperty("warning", out var warning) && warning.ValueKind != JsonValueKind.Null,
            $"Installing exactly {declared} was warned about: {warning}");
    }

    private static JsonElement History()
    {
        var assembly = typeof(VersionEnvelope).Assembly;
        using var stream = assembly.GetManifestResourceStream(
            assembly.GetManifestResourceNames()
                .First(n => n.EndsWith("class-history.json", StringComparison.Ordinal)))!;
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.Clone();
    }

    private async Task<JsonElement> Tool(string name, object arguments)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "tools/call",
                @params = new { name, arguments },
            }),
        };
        request.Headers.Add("Accept", "application/json, text/event-stream");

        var response = await app.Client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"{name} returned {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync();
        var envelope = body.Split('\n')
            .FirstOrDefault(l => l.StartsWith("data: ", StringComparison.Ordinal)) is { } sse
            ? JsonDocument.Parse(sse[6..]).RootElement.GetProperty("result")
            : JsonDocument.Parse(body).RootElement.GetProperty("result");

        var text = envelope.GetProperty("content")[0].GetProperty("text").GetString()!;
        return JsonDocument.Parse(text).RootElement.Clone();
    }
}

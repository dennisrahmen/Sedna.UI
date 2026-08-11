using System.Net.Http.Json;
using System.Text.Json;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The MCP endpoint, driven the way an agent drives it: JSON-RPC over HTTP.
/// </summary>
/// <remarks>
/// Against the running app rather than the tool class directly, because half of
/// what can break here is wiring — a missing <c>UseCors</c>, an antiforgery
/// rejection, a serialiser that drops a null. Calling the C# method would have
/// passed through every one of those.
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class McpToolTests(CatalogueAppFixture app)
{
    private static readonly string[] Expected =
        ["search", "get_example", "describe_class", "get_page", "get_tokens",
         "get_integration_guide"];

    [Fact]
    public async Task The_server_offers_exactly_the_documented_tools()
    {
        var tools = (await Rpc("tools/list")).GetProperty("tools").EnumerateArray().ToList();
        var names = tools.Select(t => t.GetProperty("name").GetString()).OrderBy(n => n).ToList();

        Assert.Equal(Expected.OrderBy(n => n), names);
    }

    [Fact]
    public async Task Every_tool_is_read_only()
    {
        // The endpoint is public and unauthenticated, and a client honouring the
        // read-only hint calls these without prompting. There must never be a
        // seventh tool that writes.
        foreach (var tool in (await Rpc("tools/list")).GetProperty("tools").EnumerateArray())
        {
            var hints = tool.GetProperty("annotations");
            Assert.True(hints.GetProperty("readOnlyHint").GetBoolean(),
                $"{tool.GetProperty("name")} is not marked read-only.");
            Assert.False(hints.GetProperty("destructiveHint").GetBoolean());
        }
    }

    [Fact]
    public async Task The_handshake_states_the_version_rule()
    {
        var instructions = (await Initialize()).GetProperty("instructions").GetString();

        Assert.NotNull(instructions);
        Assert.Contains("since", instructions, StringComparison.Ordinal);
        Assert.Contains("installedVersion", instructions, StringComparison.Ordinal);
    }

    [Theory]
    // The page beats an example that merely contains the thing.
    [InlineData("table", "page", "/table")]
    [InlineData("badge", "page", "/badge")]
    // A page whose route IS the query wins outright — "drawer" wants the Drawers page,
    // not the class it documents.
    [InlineData("drawer", "page", "/drawer")]
    // A compound name is found by its compound name, not by its stem.
    [InlineData("badge go", "class", ".badge-go")]
    [InlineData("stat delta", "class", ".stat-delta")]
    [InlineData("chip dismiss", "class", ".chip-dismiss")]
    public async Task The_ranking_puts_the_obvious_answer_first(
        string query, string kind, string reference)
    {
        var hits = (await Tool("search", new { query, limit = 1 }))
            .GetProperty("hits").EnumerateArray().ToList();

        var top = Assert.Single(hits);
        Assert.Equal(kind, top.GetProperty("kind").GetString());
        Assert.Equal(reference, top.GetProperty("ref").GetString());
    }

    [Fact]
    public async Task Search_returns_references_and_never_markup()
    {
        // A search that returned markup would spend the context window on the first
        // call, which is the whole reason get_example is a separate tool.
        var result = await Tool("search", new { query = "badge" });

        foreach (var hit in result.GetProperty("hits").EnumerateArray())
        {
            Assert.False(hit.TryGetProperty("markup", out _));
        }
    }

    [Fact]
    public async Task Search_clamps_its_limit_and_says_so()
    {
        var result = await Tool("search", new { query = "a", limit = 500 });

        Assert.True(result.GetProperty("hits").GetArrayLength() <= 25);
        Assert.True(result.GetProperty("truncated").GetBoolean());
    }

    [Fact]
    public async Task Get_example_returns_the_bytes_the_site_renders()
    {
        var example = (await Tool("get_example", new { ids = new[] { "Badge/Semantic" } }))
            .GetProperty("examples").EnumerateArray().Single();

        var markup = example.GetProperty("markup").GetString()!;
        var onDisk = File.ReadAllText(
                Path.Combine(CatalogueAssets.ExamplesDir, "Badge", "Semantic.razor"))
            .Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

        // Byte-for-byte the file the page compiles and prints.
        Assert.Equal(onDisk, markup);
    }

    [Fact]
    public async Task Get_example_refuses_more_than_five_ids()
    {
        var response = await Rpc("tools/call", new
        {
            name = "get_example",
            arguments = new { ids = new[] { "a", "b", "c", "d", "e", "f" } },
        });

        Assert.True(response.GetProperty("isError").GetBoolean());
    }

    [Fact]
    public async Task Describe_class_reports_what_the_stylesheet_declares()
    {
        var css = (await Tool("describe_class", new { names = new[] { ".badge-go" } }))
            .GetProperty("classes").EnumerateArray().Single();

        Assert.Equal(".badge-go", css.GetProperty("name").GetString());
        Assert.Equal("sedna.paint", css.GetProperty("layer").GetString());

        // Read from the sheet, so it cannot describe a rule that is not there.
        var declarations = css.GetProperty("declarations").GetString()!;
        Assert.Contains("--go-bg", declarations, StringComparison.Ordinal);
        Assert.DoesNotContain("/*", declarations, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Since_is_always_a_word_never_a_missing_key()
    {
        // The SDK serialises with WhenWritingNull, so a null `since` would vanish
        // and "unreleased" would be indistinguishable from "not reported" — the one
        // distinction the field exists to make.
        var classes = (await Tool("describe_class",
                new { names = new[] { ".accordion", ".btn-go" } }))
            .GetProperty("classes").EnumerateArray().ToList();

        foreach (var css in classes)
        {
            Assert.True(css.TryGetProperty("since", out var since));
            Assert.False(string.IsNullOrEmpty(since.GetString()));
        }
    }

    [Fact]
    public async Task An_installed_version_is_warned_about_what_it_does_not_have()
    {
        var result = await Tool("describe_class", new
        {
            names = new[] { ".accordion", ".btn" },
            installedVersion = "0.1.0",
        });

        // .accordion is on main and in no release; .btn shipped in 0.1.0.
        var warning = result.GetProperty("meta").GetProperty("warning").GetString();
        Assert.Contains(".accordion", warning!, StringComparison.Ordinal);
        Assert.DoesNotContain(".btn,", warning!, StringComparison.Ordinal);

        // Still returned. Filtering silently would hide that an upgrade is the fix.
        Assert.Equal(2, result.GetProperty("classes").GetArrayLength());
    }

    [Fact]
    public async Task Every_response_carries_the_version_envelope()
    {
        foreach (var (name, arguments) in new (string, object)[]
                 {
                     ("search", new { query = "badge" }),
                     ("get_example", new { ids = new[] { "Badge/Semantic" } }),
                     ("describe_class", new { names = new[] { ".btn" } }),
                     ("get_page", new { }),
                     ("get_tokens", new { filter = "brand" }),
                     ("get_integration_guide", new { }),
                 })
        {
            var meta = (await Tool(name, arguments)).GetProperty("meta");

            Assert.Equal("main", meta.GetProperty("source").GetString());
            Assert.False(string.IsNullOrEmpty(meta.GetProperty("latestRelease").GetString()));
        }
    }

    [Fact]
    public async Task The_integration_guide_serves_the_repositorys_own_documentation()
    {
        var markdown = (await Tool("get_integration_guide", new { section = "host-page" }))
            .GetProperty("markdown").GetString()!;

        // Never retyped. This is the successor to the guard that exists because
        // every consuming app copies that block.
        var documented = File.ReadAllText(
            Path.Combine(Assets.RepoRoot, "docs", "getting-started.md"));

        Assert.Equal(Normalise(documented), Normalise(markdown));
    }

    [Fact]
    public async Task Get_tokens_keeps_the_blocks_ordered()
    {
        var blocks = (await Tool("get_tokens", new { }))
            .GetProperty("blocks").EnumerateArray().ToList();

        // An ordered array, not a map keyed by theme: `:root` appears more than once
        // and a map would silently lose all but the last.
        var roots = blocks.Count(b => b.GetProperty("selector").GetString() == ":root");
        Assert.True(roots > 1, "The token export should carry :root more than once.");
    }

    // ── plumbing ────────────────────────────────────────────────────────────

    private static string Normalise(string s) => s.Replace("\r\n", "\n", StringComparison.Ordinal);

    private async Task<JsonElement> Initialize() => await Rpc("initialize", new
    {
        protocolVersion = "2025-06-18",
        capabilities = new { },
        clientInfo = new { name = "tests", version = "1" },
    });

    private async Task<JsonElement> Tool(string name, object arguments)
    {
        var result = await Rpc("tools/call", new { name, arguments });

        // The SDK answers with the payload as JSON text; structuredContent is not
        // guaranteed for an anonymous return type.
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    private async Task<JsonElement> Rpc(string method, object? parameters = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new
            {
                jsonrpc = "2.0",
                id = 1,
                method,
                @params = parameters ?? new { },
            }),
        };
        // Streamable HTTP may answer as either, and the server picks.
        request.Headers.Add("Accept", "application/json, text/event-stream");

        var response = await app.Client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode,
            $"{method} returned {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync();
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
                return JsonDocument.Parse(line[6..]).RootElement.GetProperty("result").Clone();
        }

        return JsonDocument.Parse(body).RootElement.GetProperty("result").Clone();
    }
}

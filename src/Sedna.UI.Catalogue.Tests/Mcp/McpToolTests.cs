using System.Net.Http.Json;
using System.Text.Json;
using Sedna.UI.Catalogue.Mcp;
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

    /// <summary>One valid call per tool.</summary>
    private static readonly (string Name, object Arguments)[] EveryTool =
    [
        ("search", new { query = "badge" }),
        ("get_example", new { ids = new[] { "Badge/Semantic" } }),
        ("describe_class", new { names = new[] { ".btn" } }),
        ("get_page", new { }),
        ("get_tokens", new { filter = "brand" }),
        ("get_integration_guide", new { }),
    ];

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
    public async Task No_schema_a_tool_publishes_uses_a_boolean_subschema()
    {
        // `{"properties":{"result":true}}` is legal JSON Schema and the Zod validator
        // in the MCP TypeScript SDK rejects it, so a client that validates the tool
        // list drops every tool in it — while the server stays connected and its
        // instructions load. The whole surface disappears with no error anywhere,
        // which is why this is a test and not a comment.
        foreach (var tool in (await Rpc("tools/list")).GetProperty("tools").EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString();

            foreach (var key in new[] { "inputSchema", "outputSchema" })
            {
                if (tool.TryGetProperty(key, out var schema))
                    AssertEverySubschemaIsAnObject(schema, $"{name}.{key}");
            }
        }
    }

    private static void AssertEverySubschemaIsAnObject(JsonElement schema, string path)
    {
        if (schema.ValueKind != JsonValueKind.Object) return;

        foreach (var keyword in new[] { "properties", "patternProperties", "$defs", "definitions" })
        {
            if (!schema.TryGetProperty(keyword, out var map)
                || map.ValueKind != JsonValueKind.Object) continue;

            foreach (var member in map.EnumerateObject())
            {
                Assert.True(member.Value.ValueKind == JsonValueKind.Object,
                    $"{path}.{keyword}.{member.Name} is {member.Value.ValueKind}, not a schema object.");
                AssertEverySubschemaIsAnObject(member.Value, $"{path}.{keyword}.{member.Name}");
            }
        }

        foreach (var keyword in new[] { "items", "additionalProperties" })
        {
            if (schema.TryGetProperty(keyword, out var nested))
                AssertEverySubschemaIsAnObject(nested, $"{path}.{keyword}");
        }
    }

    [Fact]
    public async Task A_tool_returns_the_structured_content_it_advertises()
    {
        // A tool that declares an outputSchema MUST return structuredContent that
        // conforms to it. Advertising one and returning text is a protocol
        // violation, and a client is entitled to drop the tool for it — which is
        // exactly what it looks like from the other side: the server connects, its
        // instructions load, and not one tool registers.
        var advertised = (await Rpc("tools/list")).GetProperty("tools").EnumerateArray()
            .ToDictionary(t => t.GetProperty("name").GetString()!,
                t => t.TryGetProperty("outputSchema", out _));

        foreach (var (name, arguments) in EveryTool)
        {
            var result = await Rpc("tools/call", new { name, arguments });

            Assert.Equal(advertised[name], result.TryGetProperty("structuredContent", out _));
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
    public async Task Get_example_refuses_more_than_five_ids_and_says_what_the_limit_is()
    {
        var message = await Failure("get_example",
            new { ids = new[] { "a", "b", "c", "d", "e", "f" } });

        // Every rejection is an McpException, whose message the SDK propagates. Any
        // other exception type reaches the caller as the bare "An error occurred
        // invoking 'get_example'.", which leaves a model guessing at the limit.
        Assert.Contains("At most 5", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_kind_names_the_kinds_instead_of_returning_nothing()
    {
        // Zero hits reads as "the catalogue has none of those" rather than "that is
        // not a kind", and an agent retries the same wrong call.
        var message = await Failure("search", new { query = "badge", kind = "component" });

        foreach (var kind in new[] { "example", "class", "token", "page" })
            Assert.Contains(kind, message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Example")]
    [InlineData("CLASS")]
    public async Task A_kind_is_matched_whatever_its_case(string kind)
    {
        var result = await Tool("search", new { query = "badge", kind });

        Assert.True(result.GetProperty("hits").GetArrayLength() > 0);
    }

    [Fact]
    public async Task An_unknown_guide_section_names_the_sections()
    {
        var message = await Failure("get_integration_guide", new { section = "everything" });

        foreach (var section in new[] { "host-page", "branding", "javascript", "rules" })
            Assert.Contains(section, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_installed_version_that_is_not_a_version_is_rejected()
    {
        // It would otherwise compare as 0.0.0 and produce a confident warning built
        // from nonsense — ".btn is not in not-a-version" — and a typo would silently
        // stop protecting the one thing the envelope exists to protect.
        var message = await Failure("describe_class",
            new { names = new[] { ".btn" }, installedVersion = "not-a-version" });

        Assert.Contains("not a version", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0.3.0")]
    [InlineData("v0.3.0")]
    [InlineData("0.3")]
    [InlineData("1.0.0-rc.1")]
    public async Task A_version_a_project_file_could_hold_is_accepted(string installedVersion)
    {
        var result = await Tool("describe_class",
            new { names = new[] { ".btn" }, installedVersion });

        Assert.Equal(installedVersion,
            result.GetProperty("meta").GetProperty("installedVersion").GetString());
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
    public async Task An_id_the_stylesheet_declares_is_reported_rather_than_missed()
    {
        // The expensive answer was silence: `notFound` for #blazor-error-ui read as
        // "the library does not style that", and the app wrote its own twenty lines
        // for a rule the sheet already ships.
        var result = await Tool("describe_class", new { names = new[] { "#blazor-error-ui" } });

        // Not a class, so not among the classes — the tool does not pretend otherwise.
        Assert.Empty(result.GetProperty("classes").EnumerateArray());

        var other = result.GetProperty("otherSelectors").EnumerateArray().Single();
        Assert.Equal("#blazor-error-ui", other.GetProperty("name").GetString());
        // The id is the MOUNT — fixed to the bottom edge, hidden until the framework
        // reveals it — and nothing else. The severity moved onto `.status-bar--error`
        // when the reconnect rows and this bar became one family, which is the whole
        // point of that rename: the id is the framework's, the strip inside it is ours.
        var declarations = other.GetProperty("declarations").GetString()!;
        Assert.Contains("position: fixed", declarations, StringComparison.Ordinal);
        Assert.Contains("StatusBar/ErrorBarHostPage",
            other.GetProperty("usedByExamples").EnumerateArray().Select(e => e.GetString()));

        var note = result.GetProperty("notes").EnumerateArray().Single();
        Assert.Contains("indexes classes", note.GetProperty("note").GetString()!,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_name_that_is_not_a_class_is_told_why()
    {
        var notes = (await Tool("describe_class", new
            {
                names = new[] { "#no-such-id", ".table th", ".no-such-class" },
            }))
            .GetProperty("notes").EnumerateArray()
            .ToDictionary(n => n.GetProperty("name").GetString()!,
                n => n.GetProperty("note").GetString()!);

        Assert.Contains("declares no", notes["#no-such-id"], StringComparison.Ordinal);
        Assert.Contains("selector", notes[".table th"], StringComparison.Ordinal);
        Assert.Contains("search", notes[".no-such-class"], StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_class_is_findable_by_what_its_rules_say()
    {
        // "checkbox" is a word in no class NAME — the sheet reaches the control as
        // input[type="checkbox"] on two classes. This query answered total: 0, which
        // reads as "the library has no checkbox" and cost an app a hand-written one.
        var refs = (await Tool("search", new { query = "checkbox", kind = "class" }))
            .GetProperty("hits").EnumerateArray()
            .Select(h => h.GetProperty("ref").GetString()).ToList();

        Assert.Contains(".form-check", refs);
    }

    [Fact]
    public async Task An_example_is_findable_by_the_id_it_is_about()
    {
        // Searching this id returned the reconnect banner — a different mechanism —
        // while the snippet that IS about it went unlisted, because ids were indexed
        // nowhere.
        var refs = (await Tool("search", new { query = "blazor-error-ui" }))
            .GetProperty("hits").EnumerateArray()
            .Where(h => h.GetProperty("kind").GetString() == "example")
            .Select(h => h.GetProperty("ref").GetString()).ToList();

        Assert.Contains("StatusBar/ErrorBarHostPage", refs);
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
        foreach (var (name, arguments) in EveryTool)
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

    [Fact]
    public async Task A_snippet_is_dated_by_the_classes_its_prose_names()
    {
        // Spotlight/SpotlightLock applies .btn and .btn-group, and names
        // .spotlight-lock in prose because its API is what puts the class on the
        // page. Reading the class attributes alone dated the snippet to the release
        // the buttons came from — understated, which is the direction that gets an
        // agent to copy something its app does not have.
        var example = (await Tool("get_example",
                new { ids = new[] { "Spotlight/SpotlightLock" } }))
            .GetProperty("examples").EnumerateArray().Single();

        var classes = example.GetProperty("classes").EnumerateArray()
            .Select(c => c.GetString()).ToList();
        Assert.Contains("spotlight-lock", classes);

        var declared = (await Tool("describe_class", new { names = new[] { ".spotlight-lock" } }))
            .GetProperty("classes").EnumerateArray().Single().GetProperty("since").GetString()!;
        var since = example.GetProperty("since").GetString()!;

        Assert.True(since == "unreleased" || VersionEnvelope.Compare(since, declared) >= 0,
            $"The snippet reports {since}, older than .spotlight-lock's {declared}.");
    }

    [Fact]
    public void The_commit_falls_back_to_the_environment()
    {
        // Nothing passes SOURCE_COMMIT as a build argument on the host that runs
        // this, so without the fallback meta.commit is permanently "unknown".
        Assert.Equal("baked",
            VersionEnvelope.ResolveCommit("1.0.0+baked", _ => "from-environment"));
        Assert.Equal("from-build-arg",
            VersionEnvelope.ResolveCommit("1.0.0", n => n == "SOURCE_COMMIT" ? "from-build-arg" : null));
        Assert.Equal("from-railway",
            VersionEnvelope.ResolveCommit(null, n => n == "RAILWAY_GIT_COMMIT_SHA" ? "from-railway" : null));
        Assert.Equal("unknown", VersionEnvelope.ResolveCommit("1.0.0+", _ => null));

        // A Railway variable set to ${{RAILWAY_GIT_COMMIT_SHA}} renders empty when the
        // platform has no git variable to resolve, and an empty commit reads as a
        // field nobody populated rather than one that could not be.
        Assert.Equal("unknown", VersionEnvelope.ResolveCommit("1.0.0", _ => ""));
        Assert.Equal("from-railway",
            VersionEnvelope.ResolveCommit("1.0.0", n => n == "SOURCE_COMMIT" ? "" : "from-railway"));
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

    /// <summary>A rejected call's message, which the SDK propagates from McpException.</summary>
    private async Task<string> Failure(string name, object arguments)
    {
        var result = await Rpc("tools/call", new { name, arguments });

        Assert.True(result.GetProperty("isError").GetBoolean(), $"{name} was expected to fail.");
        return result.GetProperty("content")[0].GetProperty("text").GetString()!;
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

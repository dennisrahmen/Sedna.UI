using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>ISednaGraphs</c> against the graph module it calls. Every member is one call to
/// <c>sednaUi.graph.invoke(id, method, args)</c>, and the module's <c>invoke</c> switch is the
/// other half of that contract — so these read the shipped module and hold the two together:
/// the method names, the arguments, and the JSON names the model and the filter read.
/// </summary>
/// <remarks>
/// The call shape is asserted with bUnit's JSInterop, as in <see cref="InteropTests"/>. The wire
/// format is asserted with a real <see cref="JSRuntime"/>, whose serializer options are the ones
/// Blazor sends with: a hand-built <c>JsonSerializerOptions</c> would agree with a model that the
/// real one writes differently.
/// </remarks>
public class GraphInteropTests : BunitContext
{
    private const string Bridge = "sednaUi.graph.invoke";
    private const string Id = "svc-map";

    private static string GraphModule { get; } =
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "wwwroot", "js", "Sedna.UI.graph.js"));

    private static string Initializer { get; } =
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "wwwroot", "Sedna.UI.lib.module.js"));

    private static JsonSerializerOptions Wire { get; } = new WireRuntime().Options;

    private ISednaGraphs Graphs() => new SednaGraphs(JSInterop.JSRuntime);

    private static readonly SednaGraphData Tiny = new(
        [new SednaGraphNode("api", "Orders API"), new SednaGraphNode("db", "Orders database")],
        [new SednaGraphEdge("api", "db")]);

    private const string TinyJson =
        """{"nodes":[{"id":"api","label":"Orders API"},{"id":"db","label":"Orders database"}],"edges":[{"source":"api","target":"db"}]}""";

    /// <summary>One member, the method name the module's switch expects, and the arguments as JSON.</summary>
    public sealed record GraphCall(string Name, Func<ISednaGraphs, Task> Act, string Method, string Args)
    {
        public override string ToString() => Name;
    }

    public static IEnumerable<GraphCall> AllCalls =>
    [
        new("SetDataAsync", g => g.SetDataAsync(Id, Tiny), "set", $$"""[{{TinyJson}},{"relayout":false}]"""),
        new("SetDataAsync, laid out again", g => g.SetDataAsync(Id, Tiny, relayout: true), "set",
            $$"""[{{TinyJson}},{"relayout":true}]"""),
        new("FilterAsync", g => g.FilterAsync(Id, new SednaGraphFilter
            {
                Nodes = new Dictionary<string, IReadOnlyList<string>> { ["kind"] = ["service"] },
            }), "filter",
            """[{"nodes":{"kind":["service"]},"muted":true,"isolated":true,"depth":1}]"""),
        new("SearchAsync", g => g.SearchAsync(Id, "orders api"), "search", """["orders api"]"""),
        // Null clears the marks: the script reads "" as no words, and null would be "null".
        new("SearchAsync, cleared", g => g.SearchAsync(Id, null), "search", """[""]"""),
        new("SelectAsync", g => g.SelectAsync(Id, "api"), "select", """["api"]"""),
        new("SelectAsync, cleared", g => g.SelectAsync(Id, null), "select", "[null]"),
        new("FocusAsync", g => g.FocusAsync(Id, "api", 2), "focus", """["api",2]"""),
        new("FocusAsync, everything again", g => g.FocusAsync(Id, null), "focus", "[null,1]"),
        // The script clamps too; clamping here keeps the value it reports back the one sent.
        new("FocusAsync, too shallow", g => g.FocusAsync(Id, "api", 0), "focus", """["api",1]"""),
        new("FocusAsync, too deep", g => g.FocusAsync(Id, "api", 9), "focus", """["api",6]"""),
        new("LayoutAsync", g => g.LayoutAsync(Id, SednaGraphLayout.Dagre, "LR"), "layout",
            """["dagre",{"direction":"LR"}]"""),
        new("LayoutAsync, the current one again", g => g.LayoutAsync(Id), "layout", """[null,{"direction":null}]"""),
        new("SetOptionAsync", g => g.SetOptionAsync(Id, "labels", "all"), "option", """["labels","all"]"""),
        new("FitAsync", g => g.FitAsync(Id), "fit", "[]"),
        new("ZoomAsync", g => g.ZoomAsync(Id, 1.25), "zoom", "[1.25]"),
        new("ExportAsync", g => g.ExportAsync(Id), "export", """["svg"]"""),
        new("ExportAsync, PNG", g => g.ExportAsync(Id, SednaGraphExport.Png), "export", """["png"]"""),
        new("DownloadAsync", g => g.DownloadAsync(Id), "download", """["svg",null]"""),
        new("DownloadAsync, named PNG", g => g.DownloadAsync(Id, SednaGraphExport.Png, "service-map"), "download",
            """["png","service-map"]"""),
        new("CollapseAsync", g => g.CollapseAsync(Id, "checkout"), "collapse", """["checkout"]"""),
        new("CollapseAsync, every group", g => g.CollapseAsync(Id), "collapse", "[null]"),
        new("ExpandAsync", g => g.ExpandAsync(Id, "checkout"), "expand", """["checkout"]"""),
        new("ExpandAsync, every group", g => g.ExpandAsync(Id), "expand", "[null]"),
        // The script's own name for drawing links is the plugin's: connect.
        new("SetDrawingAsync", g => g.SetDrawingAsync(Id, true), "connect", "[true]"),
        new("SetDrawingAsync, off", g => g.SetDrawingAsync(Id, false), "connect", "[false]"),
        new("SetHullsAsync", g => g.SetHullsAsync(Id, true), "hulls", "[true]"),
        new("SetHullsAsync, off", g => g.SetHullsAsync(Id, false), "hulls", "[false]"),
        // data-graph-src fetched again: the URL is the element's, so nothing else is sent.
        new("ReloadAsync", g => g.ReloadAsync(Id), "reload", "[]"),
        new("StatsAsync", g => g.StatsAsync(Id), "stats", "[]"),
    ];

    public static TheoryData<GraphCall> Calls()
    {
        var data = new TheoryData<GraphCall>();
        foreach (var call in AllCalls) data.Add(call);
        return data;
    }

    [Theory]
    [MemberData(nameof(Calls), DisableDiscoveryEnumeration = true)]
    public async Task Every_member_is_one_call_to_the_bridge_with_the_id_the_method_and_its_arguments(GraphCall call)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await call.Act(Graphs());

        var invocation = Assert.Single(JSInterop.Invocations[Bridge]);
        Assert.Equal(3, invocation.Arguments.Count);
        Assert.Equal(Id, invocation.Arguments[0]);
        Assert.Equal(call.Method, invocation.Arguments[1]);
        Assert.IsType<object?[]>(invocation.Arguments[2]);

        var sent = JsonSerializer.SerializeToNode(invocation.Arguments[2], Wire);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(call.Args), sent),
            $"{call.Name} sent {sent?.ToJsonString()}, expected {call.Args}.");

        // A layout over a large graph, an export, a download: any of them can outlast the
        // one-minute timeout Blazor applies to a call made without a token.
        Assert.NotNull(invocation.CancellationToken);
    }

    [Fact]
    public void The_methods_the_wrapper_sends_are_exactly_the_cases_the_script_handles()
    {
        var sent = AllCalls.Select(c => c.Method).ToHashSet(StringComparer.Ordinal);
        var handled = InvokeCases().ToHashSet(StringComparer.Ordinal);

        // A name the switch lacks is a console warning and a null, never an exception — so a
        // typo on either side would pass every other test here and do nothing in a browser.
        Assert.Empty(sent.Except(handled));
        // And a case nothing in C# reaches is a member the interface is missing.
        Assert.Empty(handled.Except(sent));
    }

    [Fact]
    public void Every_member_of_the_interface_is_covered_by_a_call_above()
    {
        var members = typeof(ISednaGraphs).GetMethods().Select(m => m.Name).ToHashSet(StringComparer.Ordinal);
        var covered = AllCalls.Select(c => c.Name.Split(',')[0]).ToHashSet(StringComparer.Ordinal);

        Assert.Equal(members.Order(StringComparer.Ordinal), covered.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void The_bridge_is_the_front_doors_invoke()
    {
        var script = File.ReadAllText(Assets.JsPath);

        // The one global the wrapper calls: `sednaUi.graph.invoke(id, method, args)` in
        // js-parts/43-graph.js, which starts the graph if it has not started.
        Assert.Contains("ui.graph = {", script, StringComparison.Ordinal);
        Assert.Matches(@"invoke:\s*function\s*\(id,\s*method,\s*args\)", script);
    }

    [Fact]
    public async Task A_call_passes_the_callers_token()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        using var cts = new CancellationTokenSource();

        await Graphs().SetDataAsync(Id, Tiny, cancellationToken: cts.Token);

        Assert.Equal(cts.Token, Assert.Single(JSInterop.Invocations[Bridge]).CancellationToken);
    }

    [Fact]
    public async Task What_the_script_answers_comes_back_as_it_is()
    {
        var stats = new SednaGraphStats(5, 4, 0, 7, 9, "api");
        JSInterop.Setup<SednaGraphStats?>(Bridge, i => (string?)i.Arguments[1] == "set").SetResult(stats);
        byte[] svg = [.. "<svg/>"u8];
        JSInterop.Setup<IJSStreamReference?>(Bridge, i => (string?)i.Arguments[1] == "export")
            .SetResult(new Bytes(svg));

        Assert.Equal(stats, await Graphs().SetDataAsync(Id, Tiny));
        // The picture is streamed, not returned in the call's own message: a Blazor Server
        // circuit's message-size limit refused a PNG of a few records.
        Assert.Equal(svg, await Graphs().ExportAsync(Id));
    }

    [Fact]
    public async Task An_export_with_no_graph_behind_it_is_null()
    {
        // The script answers no bytes, rather than no stream, for an id with no graph: a stream
        // reference cannot be made of nothing.
        JSInterop.Setup<IJSStreamReference?>(Bridge, _ => true).SetResult(new Bytes([]));
        Assert.Null(await Graphs().ExportAsync(Id));
    }

    // Owns the one stream it hands out, as a real reference does, and disposes it with itself.
    private sealed class Bytes(byte[] data) : IJSStreamReference
    {
        private readonly MemoryStream _stream = new(data);

        public long Length => data.Length;

        public ValueTask<Stream> OpenReadStreamAsync(long maxAllowedSize = 512000, CancellationToken cancellationToken = default)
            => ValueTask.FromResult<Stream>(_stream);

        public ValueTask DisposeAsync() => _stream.DisposeAsync();
    }

    [Fact]
    public async Task A_graph_that_is_not_there_answers_null()
    {
        // The script warns and resolves null for an id with no [data-graph] behind it, because an
        // exception crossing back from a Blazor handler tears the circuit down.
        JSInterop.Setup<SednaGraphStats?>(Bridge, _ => true).SetResult(null);

        Assert.Null(await Graphs().StatsAsync("not-on-this-page"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task A_graph_id_is_required(string? id)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var graphs = Graphs();

        // Null is an ArgumentNullException, the others an ArgumentException — both "the id".
        await Assert.ThrowsAnyAsync<ArgumentException>(() => graphs.SetDataAsync(id!, Tiny));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => graphs.FilterAsync(id!, new SednaGraphFilter()));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => graphs.SearchAsync(id!, "orders"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => graphs.SelectAsync(id!, "api"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => graphs.FitAsync(id!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => graphs.ExportAsync(id!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => graphs.StatsAsync(id!));

        // Refused before the browser is asked anything.
        Assert.Empty(JSInterop.Invocations);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-1.25)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task A_zoom_factor_is_a_positive_number(double factor)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // The script would read a zero as "no factor" and zoom by 1, silently.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Graphs().ZoomAsync(Id, factor));
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public async Task Data_a_filter_and_an_option_name_are_required()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var graphs = Graphs();

        await Assert.ThrowsAsync<ArgumentNullException>(() => graphs.SetDataAsync(Id, null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => graphs.FilterAsync(Id, null!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => graphs.SetOptionAsync(Id, " ", "all"));

        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void The_service_needs_a_runtime() =>
        Assert.Throws<ArgumentNullException>(() => new SednaGraphs(null!));

    // ── The wire: what the script's model and filter read ──────────────────────────

    [Fact]
    public void A_record_travels_under_the_names_the_model_reads()
    {
        var node = new SednaGraphNode("orders-api", "Orders API")
        {
            Kind = "service",
            Tone = SednaGraphTone.Series(2),
            Tones = new Dictionary<string, string> { ["team"] = "3" },
            Shape = SednaGraphShape.Rounded,
            Icon = "ri-server-line",
            Group = "orders",
            Cluster = "shop",
            Parent = "checkout",
            Collapsed = true,
            Weight = 2.5,
            Muted = true,
            Root = true,
            Hub = true,
            Href = "/services/orders-api",
            Meta = "orders-console-01",
            Tags = ["critical", "eu"],
            X = 10,
            Y = -20,
            Display = SednaGraphDisplay.Box,
            State = SednaGraphState.Running,
            Fields = new Dictionary<string, string?> { ["owner"] = "Alex Fischer", ["teamLead"] = "Priya Nair", ["pager"] = null },
        };

        var json = Sent(new SednaGraphData([node], [])).Data["nodes"]![0]!.AsObject();

        // Every name is one node() in 10-model.js reads, except `collapsed`: the model keeps
        // every other key in `fields`, and the fold is read from there.
        var names = json.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Empty(names.Except(NodeKeys()).Except(["collapsed"]));
        // Every property set, and every one sent — so a property added to the record and not
        // to this fixture fails here, where the script's list is checked.
        Assert.Equal(PublicProperties<SednaGraphNode>(), names.Count);

        Assert.Equal("orders-api", (string?)json["id"]);
        Assert.Equal("2", (string?)json["tone"]);
        Assert.Equal("rounded", (string?)json["shape"]);
        Assert.Equal("box", (string?)json["display"]);
        Assert.Equal("running", (string?)json["state"]);
        Assert.Equal(2.5, (double?)json["weight"]);
        Assert.Equal(-20, (double?)json["y"]);
        Assert.Equal(["critical", "eu"], json["tags"]!.AsArray().Select(t => (string?)t));
        Assert.Equal("3", (string?)json["tones"]!["team"]);

        // An app's field names are its own words and cross untouched: no naming policy is
        // applied to a dictionary's keys, so `teamLead` is what a filter and a slot name.
        Assert.Equal(["owner", "pager", "teamLead"],
            json["fields"]!.AsObject().Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Null(json["fields"]!["pager"]);
    }

    [Fact]
    public void Collapsed_reaches_the_script_as_the_field_it_folds_by()
    {
        var group = new SednaGraphNode("checkout", "Checkout") { Collapsed = true };

        var json = Sent(new SednaGraphData([group], [])).Data["nodes"]![0]!;

        Assert.True((bool?)json["collapsed"]);
        // node() files an unknown scalar under `fields`; plugins() folds a group whose
        // fields.collapsed is set, and loads the plugin when any record carries one.
        Assert.DoesNotContain("'collapsed'", NodeKeysSource(), StringComparison.Ordinal);
        Assert.Contains("n.fields.collapsed !== undefined", GraphModule, StringComparison.Ordinal);
        Assert.Contains("flag(n.data('fields')?.collapsed)", GraphModule, StringComparison.Ordinal);
    }

    [Fact]
    public void A_record_sends_only_what_is_set()
    {
        var sent = Sent(new SednaGraphData([new SednaGraphNode("db", "Orders database")], [new SednaGraphEdge("api", "db")]));

        // A large graph over a circuit sends what it uses: no nulls, and no false flags —
        // `muted: false` would cost the same bytes on every record as saying nothing.
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"id":"db","label":"Orders database"}"""), sent.Data["nodes"]![0]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"source":"api","target":"db"}"""), sent.Data["edges"]![0]));
    }

    [Fact]
    public void An_empty_graph_is_two_empty_lists()
    {
        var sent = Sent(SednaGraphData.Empty);

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"nodes":[],"edges":[]}"""), sent.Data));
    }

    [Fact]
    public void A_link_travels_under_the_names_the_model_reads()
    {
        var edge = new SednaGraphEdge("storefront", "orders-api")
        {
            Id = "storefront-calls-orders",
            Label = "calls",
            Kind = "calls",
            Tone = SednaGraphTone.Warn,
            Line = SednaGraphLine.Dashed,
            Weight = 2,
            Arrow = SednaGraphArrow.Both,
            Muted = true,
            State = SednaGraphState.Skipped,
            Fields = new Dictionary<string, string?> { ["protocol"] = "https" },
        };

        var json = Sent(new SednaGraphData([], [edge])).Data["edges"]![0]!.AsObject();

        var names = json.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
        Assert.Empty(names.Except(EdgeKeys()));
        Assert.Equal(PublicProperties<SednaGraphEdge>(), names.Count);
        Assert.Equal("dashed", (string?)json["line"]);
        Assert.Equal("both", (string?)json["arrow"]);
        Assert.Equal("warn", (string?)json["tone"]);
        Assert.Equal("skipped", (string?)json["state"]);
        Assert.True((bool?)json["muted"]);
    }

    [Fact]
    public void A_state_travels_as_its_lowercase_word_and_only_when_set()
    {
        var sent = Sent(new SednaGraphData(
            [new SednaGraphNode("parse", "Parse the payload") { State = SednaGraphState.Done }, new SednaGraphNode("ticket", "Create a ticket")],
            [new SednaGraphEdge("parse", "ticket"), new SednaGraphEdge("ticket", "parse") { State = SednaGraphState.Failed }]));

        Assert.Equal("done", (string?)sent.Data["nodes"]![0]!["state"]);
        Assert.Equal("failed", (string?)sent.Data["edges"]![1]!["state"]);
        // A step the run has not reached says nothing, and a link without a state of its own
        // follows its target in the script: neither sends the key.
        Assert.False(sent.Data["nodes"]![1]!.AsObject().ContainsKey("state"));
        Assert.False(sent.Data["edges"]![0]!.AsObject().ContainsKey("state"));
    }

    [Fact]
    public void Every_state_is_one_the_model_accepts()
    {
        var states = SetValues("STATES");

        // Both directions: a state the model draws that C# cannot name is one an app has to send
        // as a string, and a C# name the model refuses is drawn as not reached, with a warning.
        Assert.Equal(states.Order(StringComparer.Ordinal),
            Enum.GetValues<SednaGraphState>().Select(Name).Order(StringComparer.Ordinal));
        // node() and edge() both read it through stateOf, which refuses anything else.
        Assert.Equal(2, Regex.Matches(GraphModule, @"state: stateOf\(raw\.state\)").Count);
    }

    [Fact]
    public void Every_shape_is_one_the_model_draws()
    {
        var shapes = ObjectKeys("SHAPES");

        foreach (var shape in Enum.GetValues<SednaGraphShape>())
        {
            var name = JsonSerializer.Serialize(shape, Wire).Trim('"');
            // A shape the model does not know is drawn as a circle, with a warning, which is
            // exactly the silent fallback a typed enum exists to rule out.
            Assert.True(shapes.Contains(name), $"SednaGraphShape.{shape} is sent as \"{name}\", which SHAPES does not know.");
        }
    }

    [Fact]
    public void Every_line_arrow_and_display_is_one_the_model_accepts()
    {
        var lines = SetValues("LINES");
        var arrows = SetValues("ARROWS");

        Assert.Equal(lines.Order(StringComparer.Ordinal),
            Enum.GetValues<SednaGraphLine>().Select(Name).Order(StringComparer.Ordinal));
        Assert.Equal(arrows.Order(StringComparer.Ordinal),
            Enum.GetValues<SednaGraphArrow>().Select(Name).Order(StringComparer.Ordinal));

        // node(): `display === 'box' || display === 'dot' ? display : null`.
        foreach (var display in Enum.GetValues<SednaGraphDisplay>())
            Assert.Contains($"display === '{Name(display)}'", GraphModule, StringComparison.Ordinal);

        // The export's format is named by hand in SednaGraphs; the script reads "png" and
        // treats anything else as SVG, so the two names have to be these.
        Assert.Equal(["svg", "png"], Enum.GetValues<SednaGraphExport>().Select(Name));
        Assert.Contains("a[0] === 'png' ? 'png' : 'svg'", GraphModule, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_tone_is_one_the_model_paints()
    {
        var tones = ObjectKeys("TONE_TOKENS");
        var named = typeof(SednaGraphTone)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .Concat(Enumerable.Range(1, 6).Select(SednaGraphTone.Series))
            .ToList();

        // Both directions: a tone the model paints that C# cannot name is one an app has to
        // type as a string, and a C# name the model refuses paints the brand colour.
        Assert.Equal(tones.Order(StringComparer.Ordinal), named.Order(StringComparer.Ordinal));
        Assert.Throws<ArgumentOutOfRangeException>(() => SednaGraphTone.Series(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SednaGraphTone.Series(7));
    }

    [Fact]
    public void Every_layout_is_one_the_script_runs()
    {
        var layouts = Regex.Match(GraphModule, @"const LAYOUT_NAMES = \[(?<list>[^\]]*)\]").Groups["list"].Value;
        var script = Regex.Matches(layouts, "'([a-z]+)'").Select(m => m.Groups[1].Value);
        var named = typeof(SednaGraphLayout)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!);

        Assert.Equal(script.Order(StringComparer.Ordinal), named.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void A_filter_travels_under_the_names_filterOf_reads()
    {
        var filter = new SednaGraphFilter
        {
            Nodes = new Dictionary<string, IReadOnlyList<string>> { ["team"] = ["checkout"] },
            Except = new Dictionary<string, IReadOnlyList<string>> { ["kind"] = ["queue", "job"] },
            Edges = new Dictionary<string, IReadOnlyList<string>> { ["kind"] = ["calls"] },
            EdgesExcept = new Dictionary<string, IReadOnlyList<string>> { ["kind"] = ["mentions"] },
            Hide = ["legacy-api"],
            Muted = false,
            Isolated = false,
            Focus = "orders-api",
            Depth = 2,
        };

        var json = SentFilter(filter);

        var reads = Regex.Match(GraphModule, @"function filterOf\(spec\) \{(?<body>.*?)\n\}", RegexOptions.Singleline)
            .Groups["body"].Value;
        foreach (var (name, _) in json)
            Assert.Contains($"s.{name}", reads, StringComparison.Ordinal);

        Assert.Equal(PublicProperties<SednaGraphFilter>(), json.Count);
        Assert.Equal(["queue", "job"], json["except"]!["kind"]!.AsArray().Select(v => (string?)v));
        Assert.False((bool?)json["muted"]);
        Assert.False((bool?)json["isolated"]);
        Assert.Equal(2, (int?)json["depth"]);
    }

    [Fact]
    public void An_empty_filter_shows_everything()
    {
        var json = SentFilter(new SednaGraphFilter());

        // The two switches are written even at their defaults, which are the script's own
        // (`s.muted !== false`), so neither side can drift into hiding records by omission.
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse("""{"muted":true,"isolated":true,"depth":1}"""), json));
    }

    [Fact]
    public void What_the_script_answers_reads_back_as_stats()
    {
        // stats(g) in 42-filter.js, as the bridge resolves it.
        var stats = JsonSerializer.Deserialize<SednaGraphStats>(
            """{"nodes":5,"edges":4,"matches":2,"totalNodes":7,"totalEdges":9,"selected":"orders-api"}""", Wire);

        Assert.Equal(new SednaGraphStats(5, 4, 2, 7, 9, "orders-api"), stats);
    }

    // ── The events: what Blazor hands a handler ────────────────────────────────────

    [Fact]
    public void A_records_detail_reads_as_node_event_args()
    {
        // nodeDetail() in 70-graph.js: every field a string or null, tags a list.
        var e = JsonSerializer.Deserialize<SednaGraphNodeEventArgs>(
            """
            {"id":"orders-api","label":"Orders API","kind":"service","group":"orders","cluster":null,
             "parent":"checkout","meta":"orders-console-01","href":"/services/orders-api","tone":"2","state":"waiting",
             "tags":["critical","eu"],"fields":{"owner":"Alex Fischer","collapsed":"true","pager":null},
             "keyboard":true}
            """, Wire)!;

        Assert.Equal("orders-api", e.Id);
        Assert.Equal("Orders API", e.Label);
        Assert.Equal("checkout", e.Parent);
        Assert.Equal(SednaGraphState.Waiting, e.State);
        Assert.Equal(["critical", "eu"], e.Tags);
        Assert.Equal("Alex Fischer", e.Fields["owner"]);
        Assert.Null(e.Fields["pager"]);
        Assert.True(e.Keyboard);
    }

    [Fact]
    public void A_cleared_selection_and_a_fold_read_as_node_event_args()
    {
        var cleared = JsonSerializer.Deserialize<SednaGraphNodeEventArgs>("""{"id":null}""", Wire)!;
        // nodeDetail() sends `state: null` for a record the run has not reached.
        var folded = JsonSerializer.Deserialize<SednaGraphNodeEventArgs>("""{"id":"checkout","label":"Checkout","state":null}""", Wire)!;

        Assert.Null(cleared.Id);
        Assert.Null(cleared.State);
        Assert.Null(folded.State);
        Assert.Empty(cleared.Tags);
        Assert.Empty(cleared.Fields);
        Assert.Equal("checkout", folded.Id);
    }

    [Fact]
    public void A_context_menu_a_drawn_link_and_stats_read_as_their_event_args()
    {
        // context(): the record, or { id: null } on the background, with a rounded point.
        var context = JsonSerializer.Deserialize<SednaGraphContextEventArgs>("""{"id":null,"x":312,"y":88}""", Wire)!;
        var connect = JsonSerializer.Deserialize<SednaGraphConnectEventArgs>("""{"source":"storefront","target":"search"}""", Wire)!;
        var change = JsonSerializer.Deserialize<SednaGraphEventArgs>(
            """{"nodes":5,"edges":4,"matches":0,"totalNodes":7,"totalEdges":9,"selected":null}""", Wire)!;

        Assert.Equal((312, 88), (context.X, context.Y));
        Assert.Equal(("storefront", "search"), (connect.Source, connect.Target));
        Assert.Equal((5, 7, 9), (change.Nodes, change.TotalNodes, change.TotalEdges));
    }

    [Fact]
    public void The_events_Razor_binds_are_the_events_the_initializer_registers()
    {
        var razor = typeof(EventHandlers).GetCustomAttributes<EventHandlerAttribute>()
            .Select(a => a.AttributeName)
            .ToList();
        var list = Regex.Match(Initializer, @"const events = \[(?<list>[^\]]*)\];").Groups["list"].Value;
        var browser = Regex.Matches(list, "'([a-z-]+)'").Select(m => m.Groups[1].Value).ToList();

        // A name in one list and not the other is an attribute that compiles and never fires,
        // or an event whose data reaches the handler as an empty EventArgs.
        Assert.All(razor, name => Assert.StartsWith("on", name, StringComparison.Ordinal));
        Assert.Equal(razor.Select(n => n[2..]).Order(StringComparer.Ordinal), browser.Order(StringComparer.Ordinal));
        Assert.Equal(browser.Count, browser.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Every_graph_event_the_module_dispatches_is_bindable_and_cancelable_where_it_is()
    {
        var handlers = typeof(EventHandlers).GetCustomAttributes<EventHandlerAttribute>()
            .ToDictionary(a => a.AttributeName[2..], StringComparer.Ordinal);
        var dispatched = Regex.Matches(GraphModule, @"emit\('(?<name>sedna-graph-[a-z]+)',[^;]*?(?<cancelable>, true)?\)")
            .GroupBy(m => m.Groups["name"].Value)
            .ToDictionary(g => g.Key, g => g.Any(m => m.Groups["cancelable"].Success), StringComparer.Ordinal);

        Assert.Equal(
            handlers.Keys.Where(k => k.StartsWith("sedna-graph-", StringComparison.Ordinal)).Order(StringComparer.Ordinal),
            dispatched.Keys.Order(StringComparer.Ordinal));

        // `@onsedna-graph-open:preventDefault` is what stops a record's href being followed,
        // and it compiles only where the attribute enables it — on exactly the two events the
        // module dispatches cancelable.
        foreach (var (name, cancelable) in dispatched)
            Assert.True(handlers[name].EnablePreventDefault == cancelable,
                $"{name} is dispatched {(cancelable ? "" : "not ")}cancelable; its [EventHandler] disagrees.");

        Assert.Equal(typeof(SednaGraphContextEventArgs), handlers["sedna-graph-context"].EventArgsType);
        Assert.Equal(typeof(SednaGraphConnectEventArgs), handlers["sedna-graph-connect"].EventArgsType);
        Assert.Equal(typeof(SednaGraphEventArgs), handlers["sedna-graph-change"].EventArgsType);
        Assert.Equal(typeof(SednaGraphNodeEventArgs), handlers["sedna-graph-select"].EventArgsType);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────

    private static int PublicProperties<T>() =>
        typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance).Length;

    private static string Name<T>(T value) where T : struct, Enum => JsonSerializer.Serialize(value, Wire).Trim('"');

    private static IEnumerable<string> InvokeCases()
    {
        var body = Regex.Match(GraphModule, @"async function invoke\(g, method, args\) \{(?<body>.*?)default:",
            RegexOptions.Singleline).Groups["body"].Value;
        Assert.False(string.IsNullOrEmpty(body), "invoke(g, method, args) is not in Sedna.UI.graph.js.");
        return Regex.Matches(body, @"case '([a-z]+)':").Select(m => m.Groups[1].Value);
    }

    private static string NodeKeysSource() =>
        Regex.Match(GraphModule, @"const NODE_KEYS = new Set\(\[(?<list>[^\]]*)\]\)").Groups["list"].Value;

    private static HashSet<string> NodeKeys() =>
        Regex.Matches(NodeKeysSource(), "'([a-zA-Z]+)'").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> EdgeKeys() => SetValues("EDGE_KEYS");

    private static HashSet<string> SetValues(string constant)
    {
        var list = Regex.Match(GraphModule, $@"const {constant} = new Set\(\[(?<list>[^\]]*)\]\)").Groups["list"].Value;
        Assert.False(string.IsNullOrEmpty(list), $"{constant} is not in Sedna.UI.graph.js.");
        return Regex.Matches(list, "'([a-zA-Z-]+)'").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> ObjectKeys(string constant)
    {
        var body = Regex.Match(GraphModule, $@"const {constant} = \{{(?<body>.*?)\}};", RegexOptions.Singleline)
            .Groups["body"].Value;
        Assert.False(string.IsNullOrEmpty(body), $"{constant} is not in Sedna.UI.graph.js.");
        return Regex.Matches(body, @"(?:'(?<q>[a-z0-9-]+)'|(?<b>[a-z0-9]+))\s*:")
            .Select(m => m.Groups["q"].Success ? m.Groups["q"].Value : m.Groups["b"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>What <see cref="ISednaGraphs.SetDataAsync"/> puts on the wire, through a real runtime.</summary>
    private static (JsonNode Data, JsonNode Options) Sent(SednaGraphData data)
    {
        var args = SendThroughRuntime(g => g.SetDataAsync(Id, data), "set");
        return (args[0]!, args[1]!);
    }

    private static JsonObject SentFilter(SednaGraphFilter filter) =>
        SendThroughRuntime(g => g.FilterAsync(Id, filter), "filter")[0]!.AsObject();

    private static JsonArray SendThroughRuntime(Func<ISednaGraphs, Task> act, string method)
    {
        var runtime = new WireRuntime();
        // Never completes: nothing answers. The JSON is captured as the call is begun.
        _ = act(new SednaGraphs(runtime));

        var (identifier, argsJson) = Assert.Single(runtime.Sent);
        Assert.Equal(Bridge, identifier);
        var root = JsonNode.Parse(argsJson!)!.AsArray();
        Assert.Equal(Id, (string?)root[0]);
        Assert.Equal(method, (string?)root[1]);
        return root[2]!.AsArray();
    }

    /// <summary>
    /// A <see cref="JSRuntime"/> with nothing behind it, which records the JSON each call would
    /// send. Its serializer options are the framework's own — the ones Blazor Server sends with,
    /// and parses event arguments with.
    /// </summary>
    private sealed class WireRuntime : JSRuntime
    {
        public List<(string Identifier, string? ArgsJson)> Sent { get; } = [];

        public JsonSerializerOptions Options => JsonSerializerOptions;

        protected override void BeginInvokeJS(long taskId, string identifier, string? argsJson,
            JSCallResultType resultType, long targetInstanceId) =>
            Sent.Add((identifier, argsJson));

        protected override void EndInvokeDotNet(DotNetInvocationInfo invocationInfo, in DotNetInvocationResult invocationResult)
        {
        }
    }
}

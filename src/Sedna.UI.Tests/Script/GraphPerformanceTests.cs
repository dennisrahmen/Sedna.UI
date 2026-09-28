using System.Text.Json;
using Sedna.UI.Tests.TestSupport;
using Xunit.Abstractions;

namespace Sedna.UI.Tests;

/// <summary>
/// A large graph drawn within a bound: the size an app reaches for the graph to read by
/// its groups, in the layout made for it.
/// </summary>
/// <remarks>
/// The bound is generous on purpose — about three times what the drawing took on the
/// machine it was measured on — so a slower CI runner passes and a regression that makes
/// the layout quadratic, or restyles every element per record, does not. The class runs
/// on its own, after the rest of the suite: a timing taken while other browser tests share
/// the processor measures them as much as the graph.
/// </remarks>
[Collection(nameof(GraphPerformanceTests))]
public class GraphPerformanceTests : GraphTestBase
{
    private readonly ITestOutputHelper _output;

    public GraphPerformanceTests(ITestOutputHelper output) => _output = output;

    // Fifty teams of thirty in five clusters, each team a chain with cross links, and a
    // bridge from every tenth record to another team: 1 500 records, about 3 000 links.
    private const string Build = """
        () => {
            let s = 0x5eda;
            const rnd = () => (s = (s * 1664525 + 1013904223) >>> 0) / 4294967296;
            const nodes = [], edges = [];
            for (let g = 0; g < 50; g++) {
                for (let i = 0; i < 30; i++) {
                    const id = `t${g}-${i}`;
                    nodes.push({ id: i === 0 ? `team-${g}` : id, label: i === 0 ? `Team ${g}` : `host-${g}-${i}`,
                                 group: `team-${g}`, cluster: `c${g % 5}`, kind: i % 3 ? 'service' : 'store', tone: String(1 + (g % 6)) });
                }
            }
            const idOf = (g, i) => (i === 0 ? `team-${g}` : `t${g}-${i}`);
            for (let g = 0; g < 50; g++) {
                for (let i = 1; i < 30; i++) edges.push({ source: idOf(g, Math.floor(rnd() * i)), target: idOf(g, i) });
                for (let i = 0; i < 27; i++) edges.push({ source: idOf(g, i), target: idOf(g, (i + 3 + Math.floor(rnd() * 20)) % 30) });
                for (let i = 0; i < 30; i += 10) edges.push({ source: idOf(g, i), target: idOf((g + 1 + Math.floor(rnd() * 48)) % 50, Math.floor(rnd() * 30)) });
            }
            return { nodes, edges };
        }
        """;

    [Fact]
    public async Task Fifteen_hundred_records_and_three_thousand_links_are_laid_out_as_islands_in_time()
    {
        if (NoBrowser) return;
        var page = await OpenGraph(Graph(inside: "", attrs: "data-graph-eager data-graph-deferred data-graph-layout=\"islands\""));
        await Ready(page);

        var result = await page.EvaluateAsync<JsonElement>($$"""
            async () => {
                const data = ({{Build}})();
                const g = await sednaUi.graph.get('g');
                const t0 = performance.now();
                const stats = await g.set(data);
                const ms = performance.now() - t0;
                return { ms, nodes: stats.nodes, edges: stats.edges, links: data.edges.length };
            }
            """);
        var ms = result.GetProperty("ms").GetDouble();
        _output.WriteLine($"{result.GetProperty("nodes").GetInt32()} records, {result.GetProperty("edges").GetInt32()} links: {ms:0} ms");

        Assert.Equal(1500, result.GetProperty("nodes").GetInt32());
        Assert.InRange(result.GetProperty("edges").GetInt32(), 2900, 3100);
        Assert.Equal("ready", await StateOf(page));
        // Measured over repeated runs on the machine this was written on, the class alone:
        // 0.9–3.0 s, most runs 1.0–1.6 s. The bound is about three times the usual run.
        Assert.True(ms < 5_000, $"Drawing 1 500 records and their links as islands took {ms:0} ms; the bound is 5 000.");

        // Then what a live page does all day: a few records arrive in a drawing that stands.
        var more = await page.EvaluateAsync<double>($$"""
            async () => {
                const data = ({{Build}})();
                for (let i = 0; i < 20; i++) {
                    data.nodes.push({ id: `new-${i}`, label: `new-host-${i}`, group: `team-${i}` });
                    data.edges.push({ source: `team-${i}`, target: `new-${i}` });
                }
                const g = await sednaUi.graph.get('g');
                const t0 = performance.now();
                await g.set(data);
                return performance.now() - t0;
            }
            """);
        _output.WriteLine($"20 more records into the standing drawing: {more:0} ms");
        Assert.Equal(1520, (await Shown(page)).Length);
        // Measured alongside the above: 83–284 ms, most runs 115–155 ms.
        Assert.True(more < 600, $"Adding twenty records to 1 500 took {more:0} ms; the bound is 600.");
        AssertQuiet();
    }
}

/// <summary>Runs <see cref="GraphPerformanceTests"/> alone, after every parallel test.</summary>
[CollectionDefinition(nameof(GraphPerformanceTests), DisableParallelization = true)]
public class GraphPerformanceCollection;

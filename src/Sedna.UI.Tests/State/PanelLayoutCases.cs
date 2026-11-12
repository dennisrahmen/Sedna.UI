using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The cases both halves of the panel layout are held to: <c>SednaPanels.Arrange</c> here and
/// <c>sednaUi.panels.arrange</c> in <see cref="PanelGridTests"/>. One file, so the two cannot
/// learn different rules.
/// </summary>
public static class PanelLayoutCases
{
    public sealed record Case(string Name, int Columns, SednaPanel[] Panels, SednaPanel? Moved, SednaPanel[] Expect);

    public static string Json { get; } =
        File.ReadAllText(Path.Combine(Assets.RepoRoot, "src", "Sedna.UI.Tests", "State", "panel-layout-cases.json"));

    public static IReadOnlyList<Case> All { get; } = Load();

    public static TheoryData<string> Names()
    {
        var data = new TheoryData<string>();
        foreach (var c in All) data.Add(c.Name);
        return data;
    }

    public static Case Named(string name) => All.Single(c => c.Name == name);

    private static SednaPanel Panel(JsonElement e) => new(
        e[0].GetString()!, e[1].GetInt32(), e[2].GetInt32(), e[3].GetInt32(), e[4].GetInt32());

    private static List<Case> Load()
    {
        using var doc = JsonDocument.Parse(Json);
        return doc.RootElement.EnumerateArray().Select(c => new Case(
            c.GetProperty("name").GetString()!,
            c.GetProperty("columns").GetInt32(),
            c.GetProperty("panels").EnumerateArray().Select(Panel).ToArray(),
            c.GetProperty("moved").ValueKind == JsonValueKind.Null ? null : Panel(c.GetProperty("moved")),
            c.GetProperty("expect").EnumerateArray().Select(Panel).ToArray())).ToList();
    }
}

/// <summary>
/// <see cref="SednaPanels.Arrange"/>: a panel in the way goes down, every panel floats up, and
/// the result is in reading order — case by case, from the file the script is held to as well.
/// </summary>
public class PanelLayoutTests
{
    [Theory]
    [MemberData(nameof(PanelLayoutCases.Names), MemberType = typeof(PanelLayoutCases))]
    public void Arrange_settles_each_case_as_the_script_does(string name)
    {
        var c = PanelLayoutCases.Named(name);
        Assert.Equal(c.Expect, SednaPanels.Arrange(c.Panels, c.Columns, c.Moved));
    }

    [Fact]
    public void No_two_panels_overlap_and_none_hangs_over_a_gap_whatever_goes_in()
    {
        var random = new Random(241);
        for (var round = 0; round < 200; round++)
        {
            var columns = random.Next(1, 25);
            var panels = Enumerable.Range(0, random.Next(1, 12))
                .Select(i => new SednaPanel($"p{i}", random.Next(-2, columns + 2), random.Next(-2, 20), random.Next(0, columns + 3), random.Next(0, 6)))
                .ToList();
            var moved = random.Next(2) == 0 ? null : panels[random.Next(panels.Count)] with { X = random.Next(0, columns), Y = random.Next(0, 20) };

            var result = SednaPanels.Arrange(panels, columns, moved);

            Assert.Equal(panels.Count, result.Count);
            foreach (var p in result)
            {
                Assert.InRange(p.X, 0, columns - p.W);
                Assert.InRange(p.W, 1, columns);
                Assert.True(p.H >= 1);
                foreach (var q in result.Where(q => q != p))
                    Assert.False(p.X < q.X + q.W && q.X < p.X + p.W && p.Y < q.Y + q.H && q.Y < p.Y + p.H, $"{p} overlaps {q}");
                // Resting on the top, or on a panel just above it in a column it covers.
                Assert.True(p.Y == 0 || result.Any(q => q.Y + q.H == p.Y && q.X < p.X + p.W && p.X < q.X + q.W), $"{p} hangs over a gap");
            }
            Assert.Equal(result.OrderBy(p => p.Y).ThenBy(p => p.X), result);
        }
    }

    [Fact]
    public void The_moved_panel_keeps_its_size_and_column()
    {
        var result = SednaPanels.Arrange(
            [new("orders", 0, 0, 6, 4), new("returns", 6, 0, 6, 4)], 12, new("orders", 3, 9, 5, 2));
        Assert.Contains(new SednaPanel("orders", 3, 4, 5, 2), result);
    }
}

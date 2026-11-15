using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The shards of the page scans together are exactly the catalogue. No browser: this is what
/// stops a route from silently falling out of every CI job.
/// </summary>
public class ShardTests
{
    public static TheoryData<int> Counts() => new() { 1, 2, 3, 4, 5, 7, 8, 16, 500 };

    [Theory]
    [MemberData(nameof(Counts))]
    public void The_shards_are_disjoint_and_together_every_routed_page(int n)
    {
        var all = RoutedPages.All;
        Assert.NotEmpty(all);

        var shards = Enumerable.Range(1, n).Select(k => new Shard(k, n).Select(all)).ToList();

        Assert.Equal(all.Count, shards.Sum(s => s.Count));
        Assert.Equal(all.Count, shards.SelectMany(s => s).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(all, shards.SelectMany(s => s).OrderBy(r => r, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void A_route_belongs_to_the_shard_its_text_names_whatever_else_is_listed()
    {
        // Fixed values: the function is the same in every process and on every machine, so
        // the four CI jobs agree on who runs what. Changing it moves routes between jobs.
        Assert.Equal([1, 1, 4], new[] { "/", "/badge", "/table" }.Select(r => Shard.Of(r, 4)));
        foreach (var route in RoutedPages.All)
            Assert.Equal(Shard.Of(route, 4), Shard.Of(route, 4));
    }

    [Fact]
    public void Four_shards_are_close_to_even_over_the_real_routes()
    {
        var sizes = Enumerable.Range(1, 4).Select(k => new Shard(k, 4).Select(RoutedPages.All).Count).ToList();
        var mean = RoutedPages.All.Count / 4.0;
        Assert.All(sizes, s => Assert.InRange(s, mean * 0.7, mean * 1.3));
    }

    [Theory]
    [InlineData(null, 1, 1)]
    [InlineData("", 1, 1)]
    [InlineData("1/1", 1, 1)]
    [InlineData("1/4", 1, 4)]
    [InlineData("4/4", 4, 4)]
    public void A_shard_is_read_from_k_over_n(string? value, int k, int n) =>
        Assert.Equal(new Shard(k, n), Shard.Parse(value));

    [Theory]
    [InlineData("0/4")]
    [InlineData("5/4")]
    [InlineData("1/0")]
    [InlineData("-1/4")]
    [InlineData("1/-4")]
    [InlineData("1")]
    [InlineData("1/")]
    [InlineData("/4")]
    [InlineData("a/b")]
    [InlineData("1/2/3")]
    [InlineData(" 1/4")]
    [InlineData("1.5/4")]
    public void A_value_that_names_no_shard_throws_so_a_typo_cannot_run_nothing(string value) =>
        Assert.Throws<InvalidOperationException>(() => Shard.Parse(value));

    [Fact]
    public void A_shard_with_no_route_is_legal_and_runs_nothing()
    {
        // 500 shards over fewer routes: most are empty, and the union test above shows nothing was lost.
        var empty = Enumerable.Range(1, 500).First(k => new Shard(k, 500).Select(RoutedPages.All).Count == 0);
        Assert.Empty(new Shard(empty, 500).Select(RoutedPages.All));
    }
}

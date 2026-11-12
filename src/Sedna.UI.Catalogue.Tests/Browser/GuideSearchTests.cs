using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The topbar search offers a goal for every phrase written for it, ranked by the library's own
/// matcher in the browser — the one the reader actually types into.
/// </summary>
/// <remarks>
/// <c>sednaUi.search.rank</c> is the ranking the panel shows, so asking it directly checks every
/// phrase without typing each one; a goal has to be the first goal ranked and inside the rows the
/// panel shows before it says how many more there are.
/// </remarks>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public class GuideSearchTests(CatalogueAppFixture app)
{
    /// <summary>The rows the results panel shows (25-search.js's MAX).</summary>
    private const int Shown = 8;

    [Fact]
    public async Task Every_phrase_offers_its_goal_in_the_topbar_search()
    {
        if (app.NoBrowser) return;
        var page = await app.OpenInteractiveAsync("/");
        await page.WaitForFunctionAsync("() => sednaUi.search.rank('data grid').length > 0", null, new() { Timeout = 15_000 });

        var problems = new List<string>();
        foreach (var goal in GuideFiles.Guide.Goals)
        {
            foreach (var phrase in goal.Phrases)
            {
                var ranked = await page.EvaluateAsync<Ranked[]>(
                    "q => sednaUi.search.rank(q).map(i => ({ title: i.title, tag: i.tag || '' }))", phrase);
                var index = Array.FindIndex(ranked, r => r.Tag == "goal");

                if (index < 0)
                    problems.Add($"\"{phrase}\" offers no goal; it should offer \"{goal.Goal}\".");
                else if (ranked[index].Title != goal.Goal)
                    problems.Add($"\"{phrase}\" offers \"{ranked[index].Title}\" before \"{goal.Goal}\".");
                else if (index >= Shown)
                    problems.Add($"\"{phrase}\" ranks \"{goal.Goal}\" {index + 1}th, below the rows the panel shows.");
            }
        }

        await page.CloseAsync();
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    private sealed class Ranked
    {
        public string Title { get; set; } = string.Empty;
        public string Tag { get; set; } = string.Empty;
    }
}

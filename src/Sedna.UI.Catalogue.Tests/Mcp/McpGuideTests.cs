using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The design guide over MCP: <c>search</c> finds a goal from the words a developer types,
/// <c>get_guide</c> answers every topic it names, and <c>get_page</c> carries the entry.
/// </summary>
/// <remarks>
/// The search test is what keeps goal search right as the guide grows: every phrase of every
/// goal, typed as it is written, has to bring that goal back as the first goal and among the
/// first hits overall. A new goal whose phrases another goal answers better fails here, naming
/// both.
/// </remarks>
[Trait(Scope.Name, Scope.Site)]
[Collection(CatalogueAppCollection.Name)]
public class McpGuideTests(CatalogueAppFixture app)
{
    /// <summary>How far down the hits a goal may come and still be what the agent reads first.</summary>
    private const int TopHits = 3;

    private McpClient Mcp => new(app.Client);

    [Fact]
    public async Task Every_phrase_finds_its_goal_first()
    {
        var problems = new List<string>();

        foreach (var goal in GuideFiles.Guide.Goals)
        {
            foreach (var query in goal.Phrases.Append(goal.Goal))
            {
                var hits = (await Mcp.Tool("search", new { query, limit = 25 }))
                    .GetProperty("hits").EnumerateArray().ToList();
                var firstGoal = hits.FindIndex(h => h.GetProperty("kind").GetString() == "goal");

                if (firstGoal < 0)
                    problems.Add($"\"{query}\" finds no goal; it should find \"{goal.Id}\".");
                else if (hits[firstGoal].GetProperty("ref").GetString() != goal.Id)
                    problems.Add($"\"{query}\" finds the goal \"{hits[firstGoal].GetProperty("ref").GetString()}\" "
                                 + $"before \"{goal.Id}\". Make the phrase say what only \"{goal.Id}\" is about.");
                else if (firstGoal >= TopHits)
                    problems.Add($"\"{query}\" ranks \"{goal.Id}\" {firstGoal + 1}th, after "
                                 + string.Join(", ", hits.Take(firstGoal).Select(h => h.GetProperty("ref").GetString())) + ".");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    [Theory]
    // Sentences no goal was written with, the way an agent asks for a page.
    [InlineData("let the user sort a table", "sort-a-list")]
    [InlineData("let the user page through results", "page-through-results")]
    [InlineData("show a kpi on a dashboard", "show-a-key-figure")]
    [InlineData("let the user edit cells in a grid", "edit-cells-in-place")]
    [InlineData("show disk usage against its quota", "show-usage-against-a-limit")]
    public async Task A_sentence_about_the_user_finds_the_goal(string query, string goal)
    {
        var hits = (await Mcp.Tool("search", new { query, limit = TopHits }))
            .GetProperty("hits").EnumerateArray().ToList();

        Assert.True(hits.Count > 0 && hits[0].GetProperty("ref").GetString() == goal,
            $"\"{query}\" ranks {string.Join(", ", hits.Select(h => h.GetProperty("ref").GetString()))}, not \"{goal}\" first.");
    }

    [Fact]
    public async Task A_goal_hit_opens_its_primary_answer()
    {
        var goal = GuideFiles.Guide.Goals[0];
        var hit = (await Mcp.Tool("search", new { query = goal.Phrases[0], kind = "goal" }))
            .GetProperty("hits")[0];

        Assert.Equal("goal", hit.GetProperty("kind").GetString());
        Assert.Equal(goal.PrimaryPage, hit.GetProperty("page").GetString());
        Assert.Equal(goal.Goal, hit.GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_kind_goal_returns_goals_alone()
    {
        var hits = (await Mcp.Tool("search", new { query = "table", kind = "goal" })).GetProperty("hits").EnumerateArray().ToList();

        Assert.NotEmpty(hits);
        Assert.All(hits, h => Assert.Equal("goal", h.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task Get_guide_lists_the_principles_and_serves_each_section()
    {
        var guide = GuideFiles.Guide;
        var list = await Mcp.Tool("get_guide", new { topic = "principles" });
        var ids = list.GetProperty("sections").EnumerateArray().Select(s => s.GetProperty("id").GetString()).ToList();
        Assert.Equal(guide.Sections.Select(s => s.Id), ids);

        // Omitted is principles.
        Assert.Equal("principles", (await Mcp.Tool("get_guide", new { })).GetProperty("topic").GetString());

        foreach (var section in guide.Sections)
        {
            var markdown = (await Mcp.Tool("get_guide", new { topic = section.Id })).GetProperty("markdown").GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(markdown), $"get_guide(\"{section.Id}\") returned no text.");
            // An agent is not reading the repository: every link it is handed is absolute.
            Assert.DoesNotContain("](../", markdown, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Get_guide_answers_a_goal_with_its_parts()
    {
        foreach (var goal in GuideFiles.Guide.Goals)
        {
            var result = await Mcp.Tool("get_guide", new { topic = goal.Id });
            var answer = result.GetProperty("answer").EnumerateArray().ToList();

            Assert.Equal(goal.Answer.Count, answer.Count);
            Assert.Equal(goal.PrimaryPage, Assert.Single(answer, a => a.GetProperty("role").GetString() == "primary")
                .GetProperty("page").GetString());
            Assert.Equal(goal.Avoid, result.GetProperty("avoid").GetString());
        }

        var all = await Mcp.Tool("get_guide", new { topic = "goals" });
        Assert.Equal(GuideFiles.Guide.Goals.Count, all.GetProperty("goals").GetArrayLength());
    }

    [Fact]
    public async Task Get_guide_and_get_page_answer_a_page_with_its_entry()
    {
        foreach (var entry in GuideFiles.Guide.Entries)
        {
            var byGuide = await Mcp.Tool("get_guide", new { topic = entry.Page });
            Assert.Equal(entry.UseFor, byGuide.GetProperty("entry").GetProperty("useFor").GetString());

            var byPage = await Mcp.Tool("get_page", new { id = entry.Page });
            var guide = byPage.GetProperty("guide");
            Assert.Equal(entry.UseFor, guide.GetProperty("useFor").GetString());
            Assert.Equal(entry.CombineWith.Count, guide.GetProperty("combineWith").GetArrayLength());
            Assert.Equal(entry.Goals, guide.GetProperty("goals").EnumerateArray().Select(g => g.GetProperty("id").GetString()));
        }
    }

    [Fact]
    public async Task A_page_with_no_entry_says_why_rather_than_failing()
    {
        var result = await Mcp.Tool("get_guide", new { topic = "/getting-started" });

        Assert.False(result.TryGetProperty("entry", out _));
        Assert.Contains("no design-guide entry", result.GetProperty("note").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_topic_names_the_topics()
    {
        var message = await Mcp.Failure("get_guide", new { topic = "everything" });

        foreach (var form in new[] { "principles", "goals", "/table", "spacing" })
            Assert.Contains(form, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_handshake_says_to_start_from_the_goal()
    {
        var result = await Mcp.Rpc("initialize", new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "test", version = "1.0" },
        });
        var instructions = result.GetProperty("instructions").GetString()!;

        Assert.Contains("get_guide", instructions, StringComparison.Ordinal);
        Assert.Contains("Start from the goal", instructions, StringComparison.Ordinal);
    }
}

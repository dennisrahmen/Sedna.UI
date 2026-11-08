using System.Text.Json;
using Sedna.UI.Catalogue.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// <c>check_markup</c>, <c>get_changes</c> and <c>describe_api</c>, driven over the endpoint
/// the way an agent drives them.
/// </summary>
[Collection(CatalogueAppCollection.Name)]
public class McpReferenceToolTests(CatalogueAppFixture app)
{
    private McpClient Mcp => new(app.Client);

    private static IEnumerable<string> Names(JsonElement list) =>
        list.EnumerateArray().Select(e => e.GetProperty("name").GetString()!);

    // ── check_markup ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_typo_is_unknown_and_names_the_class_it_meant()
    {
        var result = await Mcp.Tool("check_markup", new { markup = """<button class="btn btn--brnad" type="button">Save</button>""" });

        var unknown = result.GetProperty("unknown")[0];
        Assert.Equal(".btn--brnad", unknown.GetProperty("name").GetString());
        Assert.Contains(".btn--brand", unknown.GetProperty("nearest").EnumerateArray().Select(n => n.GetString()));
        Assert.False(result.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task A_renamed_class_names_every_class_that_replaced_it()
    {
        var result = await Mcp.Tool("check_markup", new { markup = """<button class="btn btn-go">Send</button>""" });

        var renamed = result.GetProperty("renamed")[0];
        Assert.Equal(".btn-go", renamed.GetProperty("name").GetString());
        Assert.Equal([".btn--go", ".btn--solid"], renamed.GetProperty("use").EnumerateArray().Select(n => n.GetString()!).ToArray());
        Assert.Empty(result.GetProperty("unknown").EnumerateArray());
    }

    [Fact]
    public async Task A_class_the_installed_version_still_has_is_right_and_flagged_for_the_upgrade()
    {
        var result = await Mcp.Tool("check_markup", new
        {
            markup = """<button class="btn btn-go">Send</button>""",
            installedVersion = "0.27.0",
        });

        Assert.Empty(result.GetProperty("renamed").EnumerateArray());
        Assert.Empty(result.GetProperty("unknown").EnumerateArray());
        var later = result.GetProperty("changesOnUpgrade")[0];
        Assert.Equal(".btn-go", later.GetProperty("name").GetString());
        Assert.Equal("0.28.0", later.GetProperty("changesIn").GetString());
        Assert.True(result.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task A_class_newer_than_the_installed_version_is_reported_with_its_release()
    {
        var result = await Mcp.Tool("check_markup", new
        {
            markup = """<div class="kv kv--text"><span class="k">Owner</span><span class="v">Alex Fischer</span></div>""",
            installedVersion = "0.27.0",
        });

        var names = Names(result.GetProperty("notInInstalledVersion")).ToList();
        Assert.Contains(".kv--text", names);
        Assert.DoesNotContain(".kv", names);
        Assert.False(string.IsNullOrEmpty(result.GetProperty("meta").GetProperty("warning").GetString()));
    }

    [Fact]
    public async Task Clean_markup_is_ok_and_razor_built_classes_are_left_unchecked()
    {
        var result = await Mcp.Tool("check_markup", new
        {
            markup = """<span class="badge badge--go @Extra"><i class="ri-check-line"></i> Done</span>""",
        });

        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        Assert.Contains("@Extra", result.GetProperty("dynamicClasses").EnumerateArray().Select(n => n.GetString()));
    }

    [Fact]
    public async Task An_icon_the_font_does_not_have_is_reported()
    {
        var result = await Mcp.Tool("check_markup", new { markup = """<i class="ri-not-an-icon-line"></i><i class="ri-check-line"></i>""" });

        Assert.Equal(["ri-not-an-icon-line"], result.GetProperty("unknownIcons").EnumerateArray().Select(n => n.GetString()!).ToArray());
    }

    [Fact]
    public async Task Markup_over_the_limit_is_rejected_with_the_limit()
    {
        var message = await Mcp.Failure("check_markup", new { markup = new string('x', 70 * 1024) });
        Assert.Contains("64 KB", message, StringComparison.Ordinal);
    }

    // ── get_changes ─────────────────────────────────────────────────────────

    [Fact]
    public async Task A_release_that_renamed_classes_lists_them_as_renamed_and_removed()
    {
        var result = await Mcp.Tool("get_changes", new { from = "0.27.0", to = "0.28.0" });

        Assert.Contains(".btn-go", Names(result.GetProperty("renamed")));
        Assert.Contains(".btn-go", Names(result.GetProperty("removed").GetProperty("classes").GetProperty("names")));
        Assert.Equal(["0.28.0"], result.GetProperty("releases").EnumerateArray().Select(r => r.GetProperty("version").GetString()!).ToArray());
        Assert.EndsWith("/tag/v0.28.0", result.GetProperty("releases")[0].GetProperty("notes").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_range_reports_what_it_added_and_nothing_from_before_it()
    {
        var result = await Mcp.Tool("get_changes", new { from = "0.27.0", to = "0.28.0", kind = "classes" });

        var added = Names(result.GetProperty("added").GetProperty("classes").GetProperty("names")).ToList();
        Assert.Contains(".btn--go", added);
        Assert.DoesNotContain(".btn", added);
        Assert.False(result.GetProperty("added").TryGetProperty("tokens", out _));
    }

    [Fact]
    public async Task Main_includes_what_no_release_has_shipped_yet()
    {
        var latest = (await Mcp.Tool("get_page", new { })).GetProperty("meta").GetProperty("latestRelease").GetString()!;
        var result = await Mcp.Tool("get_changes", new { from = latest, to = "main", kind = "classes" });

        foreach (var entry in result.GetProperty("added").GetProperty("classes").GetProperty("names").EnumerateArray())
            Assert.Equal("unreleased", entry.GetProperty("since").GetString());
    }

    [Theory]
    [InlineData("0.30.0", "0.28.0", "newer than")]
    [InlineData("0.27.0", "0.28.0", "Unknown kind", "styles")]
    [InlineData("not-a-version", null, "not a version")]
    public async Task A_bad_range_is_rejected_with_the_reason(string from, string? to, string reason, string? kind = null)
    {
        var message = await Mcp.Failure("get_changes", new { from, to, kind });
        Assert.Contains(reason, message, StringComparison.Ordinal);
    }

    // ── describe_api ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_csharp_member_comes_with_its_signature_documentation_and_release()
    {
        var result = await Mcp.Tool("describe_api", new { names = new[] { "ISednaUi.ShowModalAsync" } });

        var member = result.GetProperty("csharp")[0];
        Assert.Equal("method", member.GetProperty("kind").GetString());
        Assert.Contains(member.GetProperty("signatures").EnumerateArray(), s => s.GetString()!.Contains("ShowModalAsync(", StringComparison.Ordinal));
        Assert.False(string.IsNullOrEmpty(member.GetProperty("summary").GetString()), "The XML documentation was not found.");
        Assert.NotEqual("unreleased", member.GetProperty("since").GetString());
        Assert.NotEmpty(member.GetProperty("usedByExamples").EnumerateArray());
    }

    [Fact]
    public async Task A_type_lists_its_members()
    {
        var result = await Mcp.Tool("describe_api", new { names = new[] { "ISednaOverlays" } });

        var type = result.GetProperty("csharp")[0];
        Assert.Equal("interface", type.GetProperty("kind").GetString());
        Assert.Contains(type.GetProperty("signatures").EnumerateArray(), s => s.GetString()!.Contains("ShowAsync", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("sednaUi.toast")]
    [InlineData("toast.show")]
    public async Task A_script_member_comes_with_its_row_of_the_documented_table(string name)
    {
        var result = await Mcp.Tool("describe_api", new { names = new[] { name } });

        var row = result.GetProperty("script")[0];
        Assert.Contains("toast-stack", row.GetProperty("purpose").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_name_is_answered_with_the_nearest_ones()
    {
        var result = await Mcp.Tool("describe_api", new { names = new[] { "ISednaUi.ShowModalAsnc" } });

        var missing = result.GetProperty("notFound")[0];
        Assert.Contains("ISednaUi.ShowModalAsync", missing.GetProperty("nearest").EnumerateArray().Select(n => n.GetString()));
    }
}

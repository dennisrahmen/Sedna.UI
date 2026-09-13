using System.Text.Json;
using Sedna.UI.Catalogue.Mcp;

namespace Sedna.UI.Catalogue.Tests.Mcp;

/// <summary>
/// The latest release's class history fills in what the embedded copy has not
/// caught up with, and nothing more.
/// </summary>
/// <remarks>
/// Between a tag and the first merge after it, the committed <c>class-history.json</c>
/// still says null for everything that release shipped. The release's own copy —
/// generated at the tag — is asked only there, so it can turn "unreleased" into a
/// version and can never turn a version into a different one.
/// </remarks>
public class ReleasedHistoryTests
{
    private const string Embedded = """
        {
          "latestRelease": "0.13.0",
          "classes": { "btn": "0.2.0", "choice-hint": null, "brand-new": null },
          "tokens": { "--brand": "0.2.0", "--choice-inset-check": null },
          "csharp": { "ISednaUi.ToastAsync": "0.2.0", "ISednaUi.PingAsync": null },
          "examples": { "Badge/Semantic": "0.2.0", "Script/Loading": null }
        }
        """;

    private const string Released = """
        {
          "latestRelease": "0.14.0",
          "classes": { "btn": "0.2.0", "choice-hint": "0.14.0" },
          "tokens": { "--brand": "0.2.0", "--choice-inset-check": "0.14.0" },
          "csharp": { "ISednaUi.ToastAsync": "0.2.0", "ISednaUi.PingAsync": "0.14.0" },
          "examples": { "Badge/Semantic": "0.2.0", "Script/Loading": "0.14.0" }
        }
        """;

    private static HistoryMaps Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return HistoryMaps.Parse(document.RootElement);
    }

    private static (VersionEnvelope Envelope, ReleasedHistory Released) Build()
    {
        var released = new ReleasedHistory();
        return (new VersionEnvelope(Parse(Embedded), released, "test"), released);
    }

    [Fact]
    public void Without_the_release_the_embedded_copy_is_the_whole_answer()
    {
        var (envelope, _) = Build();

        Assert.Equal("0.13.0", envelope.LatestRelease);
        Assert.Equal("0.2.0", envelope.SinceClass(".btn"));
        Assert.Null(envelope.SinceClass("choice-hint"));
        Assert.Null(envelope.SinceMember("PingAsync"));
        Assert.Null(envelope.SinceExample("Script/Loading", [], []));
    }

    [Fact]
    public void The_release_dates_what_the_embedded_copy_still_calls_unreleased()
    {
        var (envelope, released) = Build();
        released.Set(Parse(Released));

        Assert.Equal("0.14.0", envelope.LatestRelease);
        Assert.Equal("0.14.0", envelope.SinceClass("choice-hint"));
        Assert.Equal("0.14.0", envelope.SinceToken("--choice-inset-check"));
        Assert.Equal("0.14.0", envelope.SinceMember("ISednaUi.PingAsync"));
        Assert.Equal("0.14.0", envelope.SinceMember("PingAsync"));
        Assert.Equal("0.14.0", envelope.SinceExample("Script/Loading", [], []));
        Assert.Equal("0.14.0", envelope.SinceExample("x", [".btn", ".choice-hint"], []));
    }

    [Fact]
    public void The_release_never_overrides_a_version_the_embedded_copy_has()
    {
        var (envelope, released) = Build();
        released.Set(Parse("""
            {
              "latestRelease": "0.14.0",
              "classes": { "btn": "0.9.0" },
              "tokens": {}, "csharp": {}, "examples": {}
            }
            """));

        Assert.Equal("0.2.0", envelope.SinceClass("btn"));
    }

    [Fact]
    public void An_entry_merged_after_the_tag_stays_unreleased()
    {
        // The release's copy does not know `brand-new` at all, so it cannot date it.
        var (envelope, released) = Build();
        released.Set(Parse(Released));

        Assert.Null(envelope.SinceClass("brand-new"));

        var meta = envelope.For("0.13.0", [("choice-hint", envelope.SinceClass("choice-hint")),
                                           ("brand-new", envelope.SinceClass("brand-new"))]);
        Assert.Contains("1 of these are not in 0.13.0: choice-hint", meta.Warning, StringComparison.Ordinal);
        Assert.Contains("1 of these are in no release at all", meta.Warning, StringComparison.Ordinal);
        Assert.Contains("brand-new", meta.Warning, StringComparison.Ordinal);
    }

    [Fact]
    public void An_older_release_copy_cannot_move_latestRelease_backwards()
    {
        var (envelope, released) = Build();
        released.Set(Parse("""
            { "latestRelease": "0.12.0", "classes": {}, "tokens": {}, "csharp": {}, "examples": {} }
            """));

        Assert.Equal("0.13.0", envelope.LatestRelease);
    }
}

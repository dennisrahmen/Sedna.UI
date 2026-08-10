using System.Text.RegularExpressions;
using Sedna.UI.Catalogue.Tests.TestSupport;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Catalogue.Tests;

/// <summary>
/// The stylesheet handles every reconnect state the framework can actually produce.
/// </summary>
/// <remarks>
/// <para>
/// Read out of the framework's own <c>blazor.web.js</c>, which this app serves, rather
/// than from memory or a changelog. The library handled three states and .NET 10 sets
/// six: <c>retrying</c>, <c>paused</c> and <c>resume-failed</c> each left the modal at
/// <c>display: none</c>, so a disconnected reader was told nothing at all.
/// </para>
/// <para>
/// That is the failure this pins. A state with no rule is not a cosmetic gap — it is
/// silence in the one moment the frame exists to speak, and nothing in a source scan or
/// a browser test of the states we already know about can see a state we have not heard
/// of. An SDK bump that adds a seventh fails here.
/// </para>
/// </remarks>
[Collection(CatalogueAppCollection.Name)]
public class ReconnectStateTests(CatalogueAppFixture app)
{
    /// <summary>
    /// Names belonging to Blazor's own default reconnect UI, which it only ever builds
    /// when the host page supplies no <c>#components-reconnect-modal</c>.
    /// </summary>
    /// <remarks>
    /// They are in the same file and match the same prefix, so they have to be named to
    /// be excluded. Everything left over is a state applied to OUR element.
    /// </remarks>
    private static readonly string[] BlazorsOwnUi =
    [
        "components-reconnect-modal",
        "components-reconnect-dialog",
        "components-reconnect-overlay",
        "components-reconnect-fade",
        "components-reconnect-fade-in",
        "components-reconnect-slide",
        "components-reconnect-visible",
        "components-reconnect-state-changed",
        // Element IDS Blazor fills with the attempt counter, not classes. The host page
        // opts into them; the shipped stylesheet styles the span around them.
        "components-reconnect-current-attempt",
        "components-reconnect-max-retries",
    ];

    /// <summary>
    /// The states the shipped stylesheet has a rule for, read out of the sheet.
    /// </summary>
    /// <remarks>
    /// Derived rather than listed, so both sides of the comparison come from the
    /// artefacts themselves and neither is a list somebody has to remember to update.
    /// </remarks>
    private static ISet<string> Handled() =>
        Regex.Matches(Assets.StripComments(Assets.Css),
                @"#components-reconnect-modal\.(components-reconnect-[a-z-]+)")
            .Select(m => m.Groups[1].Value)
            // `hide` needs no rule: the modal is display:none by default, which is
            // exactly what hiding it means.
            .Append("components-reconnect-hide")
            .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public async Task Every_state_the_framework_sets_has_a_rule()
    {
        var script = await app.Client.GetStringAsync(
            new Uri("_framework/blazor.web.js", UriKind.Relative));

        var found = Regex.Matches(script, @"components-reconnect-[a-z-]+")
            .Select(m => m.Value)
            .ToHashSet(StringComparer.Ordinal);

        // Two vacuity guards. A renamed asset path or a minifier change would otherwise
        // make this pass by finding nothing on either side.
        Assert.True(found.Count >= 8,
            $"Only {found.Count} reconnect names in blazor.web.js — the scan has stopped seeing "
            + "the file.");
        Assert.True(Handled().Count >= 6,
            "The stylesheet scan found almost no reconnect rules, so this test is asserting "
            + "nothing. Check the selector it looks for.");

        var unhandled = found
            .Except(Handled(), StringComparer.Ordinal)
            .Except(BlazorsOwnUi, StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Assert.True(unhandled.Count == 0,
            "Blazor sets these on #components-reconnect-modal and the stylesheet has no rule for "
            + "them, so each one leaves the reader with no banner at all. Map them onto a row in "
            + $"18-frame-reconnect-banner.css: {string.Join(", ", unhandled)}");
    }
}

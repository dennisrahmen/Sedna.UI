using System.Text.Json;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A <c>.guard</c> covers the element it is a child of, with no positioning from the app.
/// </summary>
/// <remarks>
/// The guard is absolute, so it covered the nearest positioned ancestor, and a card it was
/// written into needed an inline <c>position: relative</c> — without one the scrim and its
/// prompt spread over the page. The library positions the holder now, at no specificity,
/// so a holder a component already positions keeps its own position.
/// </remarks>
public class GuardTests : ScriptTestBase
{
    private const string Guard = """<div class="guard" data-guard><div class="guard-card"><p>Claim this order to answer it.</p></div></div>""";

    [Fact]
    public async Task A_guard_covers_its_card_and_leaves_a_positioned_holder_as_it_is()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <div style="padding: 40px">
              <div class="card" id="card" style="max-width: 420px">
                <div class="card-body" inert>ORD-4211 is held at customs.<br>The broker needs an invoice.<br>Raised 07:41.</div>
                {Guard}
              </div>
              <div style="height: 600px; overflow: auto">
                <div style="height: 40px"></div>
                <div class="cal-agenda">
                  <section class="cal-agenda-day">
                    <h3 class="cal-agenda-head" id="sticky"><span class="cal-agenda-weekday">Monday</span>{Guard}</h3>
                    <p class="cal-agenda-empty">Nothing planned</p>
                  </section>
                </div>
                <div style="height: 1200px"></div>
              </div>
            </div>
            """);

        var measured = await page.EvaluateAsync<JsonElement>("""
            () => Object.fromEntries(['card', 'sticky'].map(id => {
                const holder = document.getElementById(id);
                // The guard covers the holder's padding box: inside its border.
                const r = holder.getBoundingClientRect();
                const a = { left: r.left + holder.clientLeft, top: r.top + holder.clientTop };
                a.right = a.left + holder.clientWidth;
                a.bottom = a.top + holder.clientHeight;
                const b = holder.querySelector('[data-guard]').getBoundingClientRect();
                return [id, { position: getComputedStyle(holder).position,
                              // A holder shorter than the guard's own padding, like the heading,
                              // leaves the guard taller than it, so the bottom edge is not compared.
                              off: Math.max(Math.abs(a.left - b.left), Math.abs(a.top - b.top), Math.abs(a.right - b.right)),
                              covers: b.bottom >= a.bottom - 0.5 }];
            }))
            """);

        var card = measured.GetProperty("card");
        Assert.Equal("relative", card.GetProperty("position").GetString());
        Assert.True(card.GetProperty("off").GetDouble() < 0.5,
            $"{Engine}: the guard misses its card by {card.GetProperty("off").GetDouble()}px — it covers something else.");
        Assert.True(card.GetProperty("covers").GetBoolean(), $"{Engine}: the guard stops short of the card's bottom.");

        var sticky = measured.GetProperty("sticky");
        Assert.Equal("sticky", sticky.GetProperty("position").GetString());
        Assert.True(sticky.GetProperty("off").GetDouble() < 0.5, $"{Engine}: the guard misses the sticky heading it is in by {sticky.GetProperty("off").GetDouble()}px.");
        Assert.Empty(errors);
    }
}

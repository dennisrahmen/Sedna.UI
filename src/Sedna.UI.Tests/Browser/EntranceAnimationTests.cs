using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// An element that has finished arriving leaves nothing behind that traps a fixed panel:
/// a <c>position: fixed</c> descendant of it still has the viewport as its containing
/// block.
/// </summary>
/// <remarks>
/// <para>
/// The defect this pins: <c>.list--stagger</c> and <c>.table--stagger</c> ran their
/// arrival with fill mode <c>both</c>, which keeps the finished animation in effect on
/// <c>transform</c>. An element with a transform in effect — even one whose value is
/// <c>none</c> — is the containing block of its fixed descendants, so an anchored
/// <c>.menu</c> in a row was laid out inside the row and clipped by the card's
/// <c>overflow: hidden</c>: a row-actions menu on the last row showed its first item and
/// nothing else.
/// </para>
/// <para>
/// Measured with a fixed probe rather than by reading <c>getComputedStyle().transform</c>,
/// because the computed value of a filled animation without a <c>to</c> keyframe is
/// <c>none</c> while the containing block it creates is real. The source half is
/// <see cref="EntranceAnimationScanTests"/>, which fails on the next animation that
/// would do the same.
/// </para>
/// </remarks>
public class EntranceAnimationTests : ScriptTestBase
{
    private const string MenuAnchor =
        """
        <div class="menu-anchor">
          <button class="btn btn--sm" type="button" data-menu-toggle aria-expanded="false">Actions</button>
          <div class="menu" hidden>
            <button class="menu-item" type="button">Reassign</button>
            <button class="menu-item" type="button">Snooze</button>
            <button class="menu-item" type="button">Cancel order</button>
          </div>
        </div>
        """;

    /// <summary>Every element in the library that runs an entrance animation, in place.</summary>
    public static TheoryData<string, string> Arrivals() => new()
    {
        {
            ".list--stagger > li",
            $"""
            <div class="card" style="max-width:480px"><ul class="list list--stagger">
              <li><div class="list-row"><span class="list-main"><span class="list-title">ORD-4211</span></span>{MenuAnchor}</div></li>
              <li><div class="list-row"><span class="list-main"><span class="list-title">ORD-4212</span></span>{MenuAnchor}</div></li>
            </ul></div>
            """
        },
        {
            ".table--stagger tbody > tr",
            $"""
            <div class="card" style="max-width:480px"><table class="table table--stagger">
              <thead><tr><th>Order</th><th></th></tr></thead>
              <tbody>
                <tr><td>ORD-4211</td><td>{MenuAnchor}</td></tr>
                <tr><td>ORD-4212</td><td>{MenuAnchor}</td></tr>
              </tbody>
            </table></div>
            """
        },
        {
            ".deck-step",
            $"""
            <div class="card deck"><div class="deck-body"><div class="deck-step"><p>Step one</p>{MenuAnchor}</div></div></div>
            """
        },
        {
            "dialog.modal",
            $"""
            <dialog class="modal" id="arrival-modal"><div class="modal-body"><p>Reassign ORD-4211</p>{MenuAnchor}</div></dialog>
            <script>document.getElementById('arrival-modal').showModal();</script>
            """
        },
    };

    [Theory]
    [MemberData(nameof(Arrivals))]
    public async Task A_finished_arrival_leaves_a_fixed_descendant_on_the_viewport(string selector, string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(markup);

        var trapped = await page.EvaluateAsync<string[]>(
            """
            async selector => {
              const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });
              const arriving = [...document.querySelectorAll(selector)];
              if (arriving.length === 0) return ['nothing matched ' + selector];
              // Every entrance animation the selector's elements run, run to its end. A short
              // one may have ended before the page was handed over, which is the same state.
              await Promise.all(arriving.flatMap(el => el.getAnimations()).map(a => a.finished));
              await frames(2);
              return arriving.flatMap((el, i) => {
                const probe = document.createElement('div');
                probe.style.cssText = 'position:fixed; inset:0; pointer-events:none';
                el.append(probe);
                const r = probe.getBoundingClientRect();
                probe.remove();
                const onViewport = Math.abs(r.left) < 1 && Math.abs(r.top) < 1
                  && Math.abs(r.width - innerWidth) < 1 && Math.abs(r.height - innerHeight) < 1;
                return onViewport ? [] : [`${selector} #${i + 1}: a fixed probe filled ${Math.round(r.left)},${Math.round(r.top)} ${Math.round(r.width)}×${Math.round(r.height)}, not the viewport`];
              });
            }
            """, selector);

        Assert.True(trapped.Length == 0, string.Join(Environment.NewLine, trapped));
        Assert.Empty(errors);
    }

    [Fact]
    public async Task A_menu_on_the_last_staggered_row_of_a_card_is_not_clipped()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled(
            $"""
            <div style="padding:24px">
              <div class="card" id="card" style="max-width:480px"><ul class="list list--stagger">
                <li><div class="list-row"><span class="list-main"><span class="list-title">ORD-4211</span></span>{MenuAnchor}</div></li>
                <li><div class="list-row"><span class="list-main"><span class="list-title">ORD-4212</span></span>{MenuAnchor}</div></li>
              </ul></div>
            </div>
            """);

        var report = await page.EvaluateAsync<string>(
            """
            async () => {
              const frames = n => new Promise(r => { const step = k => k ? requestAnimationFrame(() => step(k - 1)) : r(); step(n); });
              await Promise.all(document.getAnimations().map(a => a.finished));
              const rows = document.querySelectorAll('.list--stagger > li');
              const last = rows[rows.length - 1];
              last.querySelector('[data-menu-toggle]').click();
              await frames(3);
              const menu = last.querySelector('.menu');
              if (menu.hidden) return 'the menu did not open';
              const card = document.getElementById('card').getBoundingClientRect();
              const item = menu.querySelector('.menu-item:last-child').getBoundingClientRect();
              if (item.bottom <= card.bottom) return 'the fixture does not reach past the card: ' + item.bottom + ' <= ' + card.bottom;
              const hit = document.elementFromPoint(item.left + item.width / 2, item.top + item.height / 2);
              return hit && hit.closest('.menu') === menu ? '' : 'the last item is clipped; the point hit ' + (hit ? hit.outerHTML.slice(0, 80) : 'nothing');
            }
            """);

        Assert.True(report.Length == 0, report);
        Assert.Empty(errors);
    }
}

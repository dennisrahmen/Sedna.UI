using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The item with <c>aria-current</c> in a <c>.menu</c> is marked by the library: the tint
/// and ink a current nav link carries, and a tick at the trailing edge in a column of its
/// own, so a trailing note stays lined up down the panel.
/// </summary>
public class MenuCurrentItemTests : ScriptTestBase
{
    private const string Fixture = """
        <a class="nav-link active" id="nav" href="#">Orders</a>
        <div class="menu" style="position:static">
          <button class="menu-item" id="plain" type="button"><i class="ri-eye-line"></i> <span>Viewer</span><span class="menu-item-note" id="n1">V</span></button>
          <button class="menu-item" id="current" type="button" aria-current="true"><i class="ri-shield-star-line"></i> <span>Maintainer</span><span class="menu-item-note" id="n2">M</span></button>
          <button class="menu-item" id="word" type="button" aria-current="true">Owner</button>
          <button class="menu-item" id="not" type="button" aria-current="false">Contributor</button>
        </div>
        """;

    [Theory]
    [InlineData("ltr")]
    [InlineData("rtl")]
    public async Task The_current_item_is_tinted_and_ticked_at_its_trailing_edge(string dir)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""<div dir="{dir}" style="width:320px">{Fixture}</div>""");

        var r = await page.EvaluateAsync<Dictionary<string, string>>("""
            () => {
              const s = (id, pseudo) => getComputedStyle(document.getElementById(id), pseudo);
              const rect = id => document.getElementById(id).getBoundingClientRect();
              const rtl = getComputedStyle(document.getElementById('current')).direction === 'rtl';
              const end = id => rtl ? -rect(id).left : rect(id).right;
              const tick = id => {
                const after = s(id, '::after');
                return after.content === 'none' ? 'none' : after.borderBottomWidth;
              };
              return {
                navBg: s('nav').backgroundColor, navInk: s('nav').color,
                currentBg: s('current').backgroundColor, currentInk: s('current').color,
                plainBg: s('plain').backgroundColor,
                wordBg: s('word').backgroundColor,
                notBg: s('not').backgroundColor,
                tickCurrent: tick('current'), tickWord: tick('word'), tickPlain: tick('plain'), tickNot: tick('not'),
                notesAligned: String(Math.abs(end('n1') - end('n2')) < 0.5),
                noteClearOfTick: String(end('current') - end('n2') >= 20),
              };
            }
            """);

        Assert.Equal(r["navBg"], r["currentBg"]);
        Assert.Equal(r["navInk"], r["currentInk"]);
        Assert.Equal(r["navBg"], r["wordBg"]);
        Assert.NotEqual(r["navBg"], r["plainBg"]);
        Assert.NotEqual(r["navBg"], r["notBg"]);
        Assert.Equal("2px", r["tickCurrent"]);
        Assert.Equal("2px", r["tickWord"]);
        Assert.Equal("none", r["tickPlain"]);
        Assert.Equal("none", r["tickNot"]);
        Assert.Equal("True", r["notesAligned"], ignoreCase: true);
        Assert.Equal("True", r["noteClearOfTick"], ignoreCase: true);
        Assert.Empty(errors);
    }
}

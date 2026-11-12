using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A container that holds the app's own content inside its padding takes the browser's
/// margin off the block that starts it and the block that ends it, and a classed paragraph
/// inside it keeps its own spacing.
/// </summary>
/// <remarks>
/// Without the reset a <c>&lt;dl&gt;</c> of key/value rows, a paragraph or a heading opening
/// a card body added the browser's em-sized margin to the body's padding, so the content
/// sat lower than the head's gutter said it would, and every example needed
/// <c>sedna-mt-0 sedna-mb-0</c> on it.
/// </remarks>
public class ContainerParagraphTests : ScriptTestBase
{
    public static TheoryData<string, string> Containers() => new()
    {
        { "<div class=\"modal-body\">", "</div>" },
        { "<div class=\"drawer-body\">", "</div>" },
        { "<div class=\"card\"><div class=\"card-body\">", "</div></div>" },
        { "<div class=\"chat-bubble\">", "</div>" },
    };

    /// <summary>
    /// Every container that pads the app's flow content, each as its markup is written.
    /// </summary>
    public static TheoryData<string, string> Padded() => new()
    {
        { "<div class=\"card\"><div class=\"card-body\">", "</div></div>" },
        { "<div class=\"card\"><div class=\"card-body\"><p>Intro</p><div class=\"card-section\">", "</div></div></div>" },
        { "<div class=\"modal\"><div class=\"modal-body\">", "</div></div>" },
        { "<div class=\"drawer-body\">", "</div>" },
        { "<div class=\"modal deck\"><div class=\"deck-body\"><div class=\"deck-step\">", "</div></div></div>" },
        { "<div class=\"artifact\"><div class=\"artifact-body\">", "</div></div>" },
        { "<div class=\"callout\"><div class=\"callout-body\">", "</div></div>" },
        { "<details class=\"accordion\" open><summary>Returns</summary><div class=\"accordion-body\">", "</div></details>" },
        { "<div class=\"prose\">", "</div>" },
        { "<div class=\"markdown-body\">", "</div>" },
    };

    private static readonly string[] Blocks = ["p", "ul", "ol", "dl", "h1", "h2", "h3", "h4", "h5", "h6"];

    [Theory]
    [MemberData(nameof(Padded))]
    public async Task The_first_and_last_block_lose_the_browsers_margin(string open, string close)
    {
        if (NoBrowser) return;
        var markup = string.Concat(Blocks.Select(tag =>
        {
            var body = tag switch
            {
                "ul" or "ol" => "<li>ORD-4211</li>",
                "dl" => "<dt>Carrier</dt><dd>Northwind Retail freight</dd>",
                _ => "ORD-4211",
            };
            return $"{open}<{tag} data-edge>{body}</{tag}>{close}";
        }));
        var (page, _) = await OpenStyled(markup);

        var margins = await page.EvaluateAsync<string[]>(
            """
            () => [...document.querySelectorAll('[data-edge]')].flatMap(el => {
              const s = getComputedStyle(el);
              return s.marginTop === '0px' && s.marginBottom === '0px'
                ? [] : [`<${el.localName}> in .${el.parentElement.className}: ${s.marginTop} / ${s.marginBottom}`];
            })
            """);

        Assert.True(margins.Length == 0,
            "A block that is the only thing in a padded container keeps a margin of the browser's:"
            + Environment.NewLine + string.Join(Environment.NewLine, margins));
    }

    [Theory]
    [MemberData(nameof(Containers))]
    public async Task A_classed_paragraph_keeps_its_own_margin(string open, string close)
    {
        if (NoBrowser) return;
        var (page, _) = await OpenStyled($"""
            <fieldset class="form-section"><legend>Outside</legend><p class="form-section-hint" id="outside">Hint</p></fieldset>
            {open}<fieldset class="form-section"><legend>Inside</legend><p class="form-section-hint" id="inside">Hint</p></fieldset>{close}
            """);

        var margins = await page.EvaluateAsync<string[]>("""
            () => ['outside', 'inside'].map(id => {
                const s = getComputedStyle(document.getElementById(id));
                return s.marginTop + ' ' + s.marginBottom;
            })
            """);
        Assert.True(margins[0] == margins[1],
            $"A .form-section-hint has margins {margins[1]} inside {open} and {margins[0]} outside it.");
    }
}

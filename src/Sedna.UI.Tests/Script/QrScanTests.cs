using System.Globalization;
using Microsoft.Playwright;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The QR code as a camera meets it: every payload kind, error correction level, module and eye
/// shape, tone, logo and quiet zone is drawn in the browser, saved as the picture an app would
/// download, and read back by ZXing — a decoder the encoder's author did not write — from raw
/// pixels. What the page shows and what a phone reads are the same thing, or a test here fails.
/// </summary>
public class QrScanTests : ScriptTestBase
{
    private const string Url = "https://example.com/pair/7Q4K?device=dock-4&lang=de";

    /// <summary>A tiny logo as a data URL, so it needs no request and can be read into the picture.</summary>
    private const string Logo =
        "data:image/svg+xml;charset=utf-8,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 40 40'%3E%3Ccircle cx='20' cy='20' r='18' fill='%23e8431d'/%3E%3Cpath d='M12 20h16M20 12v16' stroke='%23ffffff' stroke-width='5'/%3E%3C/svg%3E";

    public static TheoryData<string> PayloadNames => new()
    {
        "text", "url", "wifi", "email", "phone", "sms", "mecard", "vcard", "geo", "event",
    };

    private static string Payload(string name) => name switch
    {
        "text" => "Dock 4 is closed until 07:00 — Straße, データ",
        "url" => SednaQrPayload.Url(Url),
        "wifi" => SednaQrPayload.WiFi("Guest; \"lobby\"", "p:a,ss\\word", hidden: true),
        "email" => SednaQrPayload.Email("support@example.com", "Pairing help", "Line one\r\nTwo & three"),
        "phone" => SednaQrPayload.Phone("+49 (30) 123-456"),
        "sms" => SednaQrPayload.Sms("+49 30 123456", "Where is the order?"),
        "mecard" => SednaQrPayload.MeCard("Alex", "Fischer", "+4930123456", "alex.fischer@example.com", "https://example.com", "Dock 4", "Likes mornings"),
        "vcard" => SednaQrPayload.VCard("Alex", "Fischer", "Northwind Retail", "Dispatcher", "+4930123456", "alex.fischer@example.com",
            "https://example.com", "Dock 4; Hall 2", "Likes mornings, mostly"),
        "geo" => SednaQrPayload.Geo(52.52, 13.405, 25),
        "event" => SednaQrPayload.Event("Dock walk, week 47", new DateTimeOffset(2026, 11, 20, 9, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 20, 11, 0, 0, TimeSpan.Zero), "Dock 4", "Bring a vest"),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    /// <summary>One code: its id, its text, its attributes and its classes, as the app would write them.</summary>
    private sealed record Spec(string Id, string Text, string Attributes = "", string Classes = "qr", string Inner = "");

    /// <summary>
    /// Opens a page with these codes. The text is set from script, so a payload keeps its exact
    /// characters: an HTML parser turns a CR LF in an attribute into a bare LF.
    /// </summary>
    private async Task<IPage> OpenCodes(params Spec[] codes)
    {
        var (page, _) = await OpenStyled("<main id=\"stage\" style=\"display:flex;flex-wrap:wrap;gap:16px;padding:16px\"></main>");
        await Js.Run(page, """
            async specs => {
              const stage = document.getElementById('stage');
              for (const s of specs) {
                const span = document.createElement('span');
                span.id = s.id;
                span.className = s.classes;
                span.setAttribute('aria-label', 'Code ' + s.id);
                span.setAttribute('data-qr-eager', '');
                for (const [name, value] of Object.entries(s.attributes)) span.setAttribute(name, value);
                span.innerHTML = s.inner;
                span.setAttribute('data-qr', s.text);
                stage.append(span);
              }
              await sednaUi.qr.init();
            }
            """, codes.Select(c => new
        {
            id = c.Id,
            text = c.Text,
            classes = c.Classes,
            inner = c.Inner,
            attributes = c.Attributes.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Split('=', 2)).ToDictionary(a => a[0], a => a.Length > 1 ? a[1].Trim('"') : string.Empty),
        }).ToArray());
        foreach (var c in codes) await page.WaitForFunctionAsync($"() => document.getElementById('{c.Id}').dataset.qrState === 'ready'");
        return page;
    }

    [Theory]
    [MemberData(nameof(PayloadNames))]
    public async Task Every_payload_kind_scans_back_to_its_payload(string name)
    {
        if (NoBrowser) return;
        var payload = Payload(name);
        var page = await OpenCodes(new Spec("q", payload));

        Assert.Equal(payload, await QrReader.Scan(page, "q"));
    }

    [Theory]
    [InlineData("L")]
    [InlineData("M")]
    [InlineData("Q")]
    [InlineData("H")]
    public async Task Every_level_scans_back_at_its_own_level(string level)
    {
        if (NoBrowser) return;
        var payload = Payload("vcard");
        var page = await OpenCodes(new Spec("q", payload, $"data-qr-level={level}"));

        Assert.Equal(payload, await QrReader.Scan(page, "q"));
        Assert.Equal(level, await page.EvaluateAsync<string>("() => sednaUi.qr.get('q').then(h => h.level)"));
    }

    [Theory]
    [InlineData("square", "square")]
    [InlineData("square", "rounded")]
    [InlineData("square", "round")]
    [InlineData("rounded", "square")]
    [InlineData("rounded", "rounded")]
    [InlineData("rounded", "round")]
    [InlineData("dots", "square")]
    [InlineData("dots", "rounded")]
    [InlineData("dots", "round")]
    public async Task Every_module_shape_and_eye_shape_scans(string shape, string eye)
    {
        if (NoBrowser) return;
        var page = await OpenCodes(
            new Spec("short", Url, $"data-qr-shape={shape} data-qr-eye={eye}"),
            new Spec("long", Payload("vcard"), $"data-qr-shape={shape} data-qr-eye={eye} data-qr-level=Q"));

        Assert.Equal(Url, await QrReader.Scan(page, "short"));
        Assert.Equal(Payload("vcard"), await QrReader.Scan(page, "long"));
    }

    [Theory]
    [InlineData("L", "L", "Q")]
    [InlineData("M", "M", "Q")]
    [InlineData("Q", "Q", "Q")]
    [InlineData("H", "H", "H")]
    public async Task A_logo_raises_the_level_to_at_least_q_and_the_code_still_scans(string asked, string _, string used)
    {
        if (NoBrowser) return;
        var page = await OpenCodes(new Spec("q", Url, $"data-qr-level={asked}", Inner: $"<img class=\"qr-logo\" alt=\"\" src=\"{Logo}\">"));

        var level = await page.EvaluateAsync<string>("() => sednaUi.qr.get('q').then(h => h.level)");
        Assert.Equal(used, level);
        Assert.True(int.Parse(await page.GetAttributeAsync("#q", "data-qr-version") ?? "0", CultureInfo.InvariantCulture) >= 5,
            "A code with a logo is at least version 5, so the logo is not most of it.");
        Assert.Equal(Url, await QrReader.Scan(page, "q"));
    }

    [Theory]
    [InlineData("Q", "square", "square")]
    [InlineData("Q", "dots", "round")]
    [InlineData("H", "rounded", "rounded")]
    public async Task A_logos_square_leaves_the_level_a_margin_to_spare(string level, string shape, string eye)
    {
        if (NoBrowser) return;
        // Long and short texts: the share the square may take is the same, the symbol it is cut from is not.
        var texts = new[] { Url, Payload("mecard"), Payload("vcard") };
        var page = await OpenCodes(texts.Select((t, i) =>
            new Spec("q" + i, t, $"data-qr-level={level} data-qr-shape={shape} data-qr-eye={eye}",
                Inner: $"<img class=\"qr-logo\" alt=\"\" src=\"{Logo}\">")).ToArray());

        for (var i = 0; i < texts.Length; i++)
        {
            var geometry = await page.EvaluateAsync<double[]>($$"""
                () => {
                  const el = document.getElementById('q{{i}}');
                  const logo = el.querySelector('.qr-logo');
                  return [+el.dataset.qrModules, parseFloat(logo.style.getPropertyValue('--qr-logo-modules')) + 2];
                }
                """);
            var (pixels, size) = await QrReader.Pixels(page, "q" + i, 480);
            // Wipe a square a third as large again as the one the code leaves out: the error
            // correction has to carry it, which is what a phone with a worse camera does.
            QrReader.Erase(pixels, size, geometry[1] * 1.33 / (geometry[0] + 8));
            Assert.True(texts[i] == QrReader.Read(pixels, size, size), $"q{i} at {level}, {shape}/{eye} did not scan with its square a third larger.");
        }
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    public async Task Every_tone_holds_enough_contrast_in_either_variant_and_scans(string variant)
    {
        if (NoBrowser) return;
        var tones = new[] { "brand", "go", "warn", "danger", "info", "secret", "cyan", "orange", "teal", "navy", "orbit", "coral" };
        var page = await OpenCodes(tones.Select(t => new Spec(t, Url, Classes: $"qr qr--{t}")).Append(new Spec("plain", Url)).ToArray());
        await Js.Run(page, $"() => document.documentElement.dataset.variant = '{variant}'");

        foreach (var id in tones.Append("plain"))
        {
            var report = await page.EvaluateAsync<string[]>($$"""
                async () => {
                  const el = document.getElementById('{{id}}');
                  await new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r)));
                  const lum = c => {
                    const [r, g, b] = c.match(/[\d.]+/g).slice(0, 3).map(Number).map(v => { v /= 255; return v <= 0.04045 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4); });
                    return 0.2126 * r + 0.7152 * g + 0.0722 * b;
                  };
                  const a = lum(getComputedStyle(el.querySelector('svg')).fill), b = lum(getComputedStyle(el).backgroundColor);
                  return [el.dataset.qrContrast, ((Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05)).toFixed(2), String(a < b)];
                }
                """);
            Assert.Equal("ok", report[0]);
            Assert.True(double.Parse(report[1], CultureInfo.InvariantCulture) >= 7, $"{variant}: .qr--{id} has a contrast of {report[1]}:1 against its paper.");
            Assert.Equal("true", report[2]);
            Assert.Equal(Url, await QrReader.Scan(page, id));
        }
    }

    [Fact]
    public async Task A_custom_colour_that_scans_badly_is_drawn_in_the_safe_pair_and_says_so()
    {
        if (NoBrowser) return;
        var page = await OpenCodes(
            new Spec("pale", Url, "style=\"--qr-ink:#c8d0dc\""),
            new Spec("lighter", Url, "style=\"--qr-ink:#f1f5f9;--qr-paper:#0f172a\""),
            new Spec("clear", Url, "style=\"--qr-paper:rgba(255,255,255,0.4)\""),
            new Spec("good", Url, "style=\"--qr-ink:#7a1f5c;--qr-paper:#fdf6e3\""));

        Assert.Equal("fallback", await page.GetAttributeAsync("#pale", "data-qr-contrast"));
        Assert.Equal("fallback", await page.GetAttributeAsync("#lighter", "data-qr-contrast"));
        Assert.Equal("fallback", await page.GetAttributeAsync("#clear", "data-qr-contrast"));
        Assert.Equal("ok", await page.GetAttributeAsync("#good", "data-qr-contrast"));
        // What is drawn after a fallback is the stylesheet's dark ink on white.
        foreach (var id in new[] { "pale", "lighter", "clear" })
        {
            Assert.Equal("rgb(15, 23, 42)", await page.EvaluateAsync<string>($"() => getComputedStyle(document.querySelector('#{id} > svg')).fill"));
            Assert.Equal("rgb(255, 255, 255)", await page.EvaluateAsync<string>($"() => getComputedStyle(document.getElementById('{id}')).backgroundColor"));
            Assert.Equal(Url, await QrReader.Scan(page, id));
        }

        Assert.Equal("rgb(122, 31, 92)", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#good > svg')).fill"));
        Assert.Equal(Url, await QrReader.Scan(page, "good"));
    }

    [Fact]
    public async Task An_inverted_code_is_drawn_only_when_the_app_allows_it()
    {
        if (NoBrowser) return;
        var page = await OpenCodes(
            new Spec("allowed", Url, "data-qr-invert style=\"--qr-ink:#f1f5f9;--qr-paper:#0f172a\""),
            new Spec("refused", Url, "style=\"--qr-ink:#f1f5f9;--qr-paper:#0f172a\""));

        Assert.Equal("inverted", await page.GetAttributeAsync("#allowed", "data-qr-contrast"));
        Assert.Equal("rgb(241, 245, 249)", await page.EvaluateAsync<string>("() => getComputedStyle(document.querySelector('#allowed > svg')).fill"));
        Assert.Equal("fallback", await page.GetAttributeAsync("#refused", "data-qr-contrast"));
        var (rgba, size) = await QrReader.Pixels(page, "allowed");
        Assert.Equal(Url, QrReader.Read(rgba, size, size, inverted: true));
    }

    [Fact]
    public async Task The_verdict_is_taken_again_when_the_colours_change()
    {
        if (NoBrowser) return;
        var page = await OpenCodes(new Spec("q", Url));

        Assert.Equal("ok", await page.GetAttributeAsync("#q", "data-qr-contrast"));
        await Js.Run(page, "() => document.getElementById('q').style.setProperty('--qr-ink', '#e2e8f0')");
        await page.WaitForFunctionAsync("() => document.getElementById('q').dataset.qrContrast === 'fallback'");
        await Js.Run(page, "() => document.getElementById('q').style.removeProperty('--qr-ink')");
        await page.WaitForFunctionAsync("() => document.getElementById('q').dataset.qrContrast === 'ok'");
    }

    [Theory]
    [InlineData(4)]
    [InlineData(2)]
    [InlineData(6)]
    public async Task The_quiet_zone_is_a_token_in_modules_and_the_picture_keeps_it(int quiet)
    {
        if (NoBrowser) return;
        var page = await OpenCodes(new Spec("q", Url, $"style=\"--qr-quiet:{quiet}\""));

        var geometry = await page.EvaluateAsync<double[]>("""
            () => {
              const el = document.getElementById('q');
              const r = el.getBoundingClientRect(), s = el.querySelector('svg').getBoundingClientRect();
              return [r.width, s.left - r.left, s.width, +el.dataset.qrModules];
            }
            """);
        var module = geometry[0] / (geometry[3] + (2 * quiet));
        Assert.Equal(quiet * module, geometry[1], 1);
        Assert.Equal(geometry[3] * module, geometry[2], 1);

        // The picture: the paper round the symbol is the token's, so the first dark pixel on the
        // finder's top row is where the quiet zone says.
        var (pixels, size) = await QrReader.Pixels(page, "q", 600);
        var perModule = (double)size / (geometry[3] + (2 * quiet));
        var y = (int)((quiet + 0.5) * perModule);
        var firstDark = Enumerable.Range(0, size).First(x => pixels[((y * size) + x) * 4] < 128);
        Assert.True(Math.Abs(firstDark - (quiet * perModule)) <= perModule * 0.6, $"The symbol starts at {firstDark}px; a quiet zone of {quiet} modules is {quiet * perModule}px.");
        if (quiet >= 4) Assert.Equal(Url, QrReader.Read(pixels, size, size));
    }

    [Fact]
    public async Task A_code_is_drawn_again_when_its_style_or_its_logo_changes()
    {
        if (NoBrowser) return;
        var page = await OpenCodes(new Spec("q", Url));
        var before = await page.GetAttributeAsync("#q > svg > path", "d");

        await Js.Run(page, "() => document.getElementById('q').setAttribute('data-qr-shape', 'dots')");
        await page.WaitForFunctionAsync("(was) => document.querySelector('#q > svg > path').getAttribute('d') !== was", before);
        Assert.Equal(Url, await QrReader.Scan(page, "q"));

        await Js.Run(page, "src => { const i = document.createElement('img'); i.className = 'qr-logo'; i.alt = ''; i.src = src; document.getElementById('q').append(i); }", Logo);
        await page.WaitForFunctionAsync("() => document.getElementById('q').dataset.qrVersion >= 5 && document.querySelector('#q .qr-logo').style.getPropertyValue('--qr-logo-modules') !== ''");
        Assert.Equal("Q", await page.EvaluateAsync<string>("() => sednaUi.qr.get('q').then(h => h.level)"));
        Assert.Equal(Url, await QrReader.Scan(page, "q"));

        await Js.Run(page, "() => document.querySelector('#q .qr-logo').remove()");
        await page.WaitForFunctionAsync("() => document.getElementById('q').dataset.qrVersion < 5");
        Assert.Equal("M", await page.EvaluateAsync<string>("() => sednaUi.qr.get('q').then(h => h.level)"));
    }

    [Theory]
    [InlineData("svg", "qr-code.svg", 1024)]
    [InlineData("png", "qr-code.png", 1024)]
    [InlineData("png", "pairing.png", 640)]
    public async Task A_button_the_app_wrote_saves_the_code_and_the_file_scans(string format, string expectedName, int size)
    {
        if (NoBrowser) return;
        var name = expectedName.StartsWith("pairing", StringComparison.Ordinal) ? " data-qr-name=\"pairing\"" : string.Empty;
        var page = await OpenCodes(new Spec("q", Url, "style=\"--qr-ink:#7a1f5c;--qr-paper:#fdf6e3\"", Inner: $"<img class=\"qr-logo\" alt=\"\" src=\"{Logo}\">"));
        await Js.Run(page, """
            html => {
              const figure = document.createElement('figure');
              figure.className = 'qr-figure';
              document.getElementById('stage').append(figure);
              figure.append(document.getElementById('q'));
              figure.insertAdjacentHTML('beforeend', html);
              document.addEventListener('sedna-qr-download', e => { window.__saved = JSON.stringify(e.detail); });
            }
            """, $"<button type=\"button\" id=\"save\" data-qr-download=\"{format}\" data-qr-size=\"{size}\"{name}>Save</button>");

        var download = await page.RunAndWaitForDownloadAsync(() => page.Locator("#save").ClickAsync());
        Assert.Equal(expectedName, download.SuggestedFilename);
        await page.WaitForFunctionAsync("() => window.__saved");
        Assert.Contains($"\"size\":{size}", await page.EvaluateAsync<string>("() => window.__saved"), StringComparison.Ordinal);

        var bytes = await File.ReadAllBytesAsync((await download.PathAsync())!);
        var mime = format == "svg" ? "image/svg+xml" : "image/png";
        // At the size the file is, which is the app's: a whole number of pixels to the module reads best.
        var pixels = await QrReader.PixelsOfImage(page, $"data:{mime};base64,{Convert.ToBase64String(bytes)}", size);
        Assert.Equal(Url, QrReader.Read(pixels, size, size));

        if (format == "svg")
        {
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            Assert.Contains("<svg", text, StringComparison.Ordinal);
            Assert.Contains("viewBox", text, StringComparison.Ordinal);
            Assert.Contains("#7a1f5c", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("#fdf6e3", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("<image", text, StringComparison.Ordinal);
            Assert.DoesNotContain("var(", text, StringComparison.Ordinal);
            Assert.Contains($"width=\"{size}\"", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task An_inline_svg_logo_is_written_into_the_file_with_its_colours_resolved()
    {
        if (NoBrowser) return;
        const string svg = "<svg class=\"qr-logo\" viewBox=\"0 0 24 24\"><circle cx=\"12\" cy=\"12\" r=\"12\" style=\"fill: var(--coral-600)\"></circle><path d=\"M7 12h10M12 7v10\" style=\"fill: none; stroke: var(--white); stroke-width: 3\"></path></svg>";
        var page = await OpenCodes(new Spec("q", Url, Inner: svg));

        // The logo is a child of the code that the drawing's own rules leave alone.
        Assert.Equal("1", await page.EvaluateAsync<string>("() => String(document.querySelectorAll('#q > svg:not(.qr-logo)').length)"));
        var text = await page.EvaluateAsync<string>("async () => (await sednaUi.qr.get('q')).toSvg({ size: 600 })");
        var logo = Uri.UnescapeDataString(text.Split("href=\"data:image/svg+xml;charset=utf-8,", 2)[1].Split('"')[0]);
        Assert.DoesNotContain("var(", logo, StringComparison.Ordinal);
        Assert.Contains("fill:#", logo, StringComparison.Ordinal);
        Assert.Contains("stroke:#ffffff", logo, StringComparison.Ordinal);
        Assert.Equal(Url, await QrReader.Scan(page, "q"));
    }

    [Fact]
    public async Task A_save_that_cannot_be_made_says_so_with_an_event_and_a_rejection()
    {
        if (NoBrowser) return;
        var page = await OpenCodes(new Spec("q", Url));
        await Js.Run(page, "() => document.addEventListener('sedna-qr-error', e => { window.__failed = e.detail.message; })");

        var rejected = await page.EvaluateAsync<string>("""
            async () => {
              try { await sednaUi.qr.download('q', { format: 'gif' }); return 'saved'; } catch (e) { return e.message; }
            }
            """);
        Assert.Contains("svg or png", rejected, StringComparison.Ordinal);
        Assert.Contains("svg or png", await page.EvaluateAsync<string>("() => window.__failed"), StringComparison.Ordinal);

        rejected = await page.EvaluateAsync<string>("""
            async () => {
              try { await sednaUi.qr.download('q', { format: 'png', size: 4 }); return 'saved'; } catch (e) { return e.message; }
            }
            """);
        Assert.Contains("pixels", rejected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_logo_that_cannot_be_read_into_the_file_stops_the_save()
    {
        if (NoBrowser) return;
        var page = await OpenCodes(new Spec("q", Url, Inner: "<img class=\"qr-logo\" alt=\"\" src=\"http://localhost:1/missing.png\">"));

        var rejected = await page.EvaluateAsync<string>("""
            async () => {
              try { await sednaUi.qr.download('q', { format: 'svg' }); return 'saved'; } catch (e) { return e.message; }
            }
            """);
        Assert.Contains("logo", rejected, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_code_without_a_script_start_waits_for_the_page_to_ask_it_to_save()
    {
        if (NoBrowser) return;
        // The handle starts a code that has not started: saving needs no earlier look at it.
        var (page, _) = await OpenStyled("<span class=\"qr\" id=\"q\" data-qr=\"https://example.com/\" aria-label=\"Code\"></span>");
        var blob = await page.EvaluateAsync<int>("async () => (await (await sednaUi.qr.get('q')).toBlob({ format: 'svg' })).size");
        Assert.True(blob > 200);
    }
}

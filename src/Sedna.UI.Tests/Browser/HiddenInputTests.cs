using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// A control that hides its native input is that input's containing block, so the keyboard
/// reaching the input scrolls the region the control is in — a modal's body — and nothing
/// around it. The same holds on every catalogue page, for every control that hides one
/// (<c>HiddenInputScanTests</c> in the catalogue's tests).
/// </summary>
public class HiddenInputTests : ScriptTestBase
{
    public static TheoryData<string, string> Controls() => new()
    {
        { "dropzone", """<label class="dropzone" data-dropzone><input type="file" multiple class="visually-hidden" id="hidden"><i class="ri-upload-cloud-2-line"></i> <span>Drop files here, or click to choose</span></label>""" },
        { "form-file", """<label class="form-file"><input type="file" id="hidden"><span class="btn"><i class="ri-attachment-2"></i> <span>Choose a file</span></span></label>""" },
        { "rating", """<div class="rating" role="radiogroup" aria-label="Rate the hand-over"><label class="rating-star"><input type="radio" name="r" id="hidden" aria-label="1 star"></label><label class="rating-star"><input type="radio" name="r" aria-label="2 stars"></label></div>""" },
        { "rating-half", """<div class="rating" role="radiogroup" aria-label="Rate the fit"><span class="rating-star"><label class="rating-half"><input type="radio" name="h" id="hidden" aria-label="Half a star"></label><label class="rating-half"><input type="radio" name="h" aria-label="1 star"></label></span></div>""" },
    };

    [Theory]
    [MemberData(nameof(Controls))]
    public async Task Focusing_a_hidden_input_low_in_a_modal_scrolls_its_body_not_the_dialog(string control, string markup)
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled($"""
            <dialog class="modal" id="d" style="--modal-max-height: 320px" aria-label="Attach">
              <div class="modal-header"><h3>Attach</h3></div>
              <div class="modal-body" id="body">
                <p style="min-height: 600px">The order's notes, long enough to scroll.</p>
                {markup}
              </div>
              <div class="modal-footer"><button class="btn" type="button">Close</button></div>
            </dialog>
            """);
        await Js.Run(page, "() => document.getElementById('d').showModal()");
        // Focused as the keyboard focuses it, which scrolls it into view: WebKit on Apple's
        // platforms does not Tab to a file input unless the reader has asked it to.
        await page.Locator("#hidden").FocusAsync();
        await Js.Frames(page);

        var at = await page.EvaluateAsync<double[]>("""
            () => {
              const d = document.getElementById('d'), body = document.getElementById('body');
              const input = document.getElementById('hidden').getBoundingClientRect();
              const shown = body.getBoundingClientRect();
              return [d.scrollTop, body.scrollTop, input.top - shown.top, shown.bottom - input.bottom,
                      d.clientHeight, d.scrollHeight, body.clientHeight, body.scrollHeight];
            }
            """);

        Assert.True(at[0] < 0.5, $"Focusing the {control}'s input scrolled the dialog itself by {at[0]}px.");
        Assert.True(at[1] > 0, $"The body did not scroll to the {control} (dialog {at[4]}/{at[5]}, body {at[6]}/{at[7]}).");
        Assert.True(at[2] >= -1 && at[3] >= -1, $"The {control}'s input is outside the body it sits in ({at[2]}, {at[3]}).");
        Assert.Empty(errors);
    }
}

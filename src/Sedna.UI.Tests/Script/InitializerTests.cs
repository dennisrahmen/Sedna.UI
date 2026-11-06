using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// The Blazor initializer registers every event the library dispatches, and an event type the
/// app has already registered is left as it is rather than stopping the app from starting.
/// </summary>
public class InitializerTests : ScriptTestBase
{
    [Fact]
    public async Task An_event_the_app_registered_first_is_skipped_and_the_rest_are_registered()
    {
        if (NoBrowser) return;
        var (page, errors) = await OpenStyled("<p>App</p>");

        var registered = await page.EvaluateAsync<string[]>("""
            async () => {
              const names = [];
              const blazor = {
                registerCustomEventType(name) {
                  if (name === 'sedna-drop') throw new Error("The event 'sedna-drop' is already registered.");
                  names.push(name);
                }
              };
              (await import('/Sedna.UI.lib.module.js')).afterWebStarted(blazor);
              return names;
            }
            """);

        Assert.DoesNotContain("sedna-drop", registered);
        Assert.Contains("sedna-dragend", registered);
        Assert.Contains("sedna-resize", registered);
        Assert.Empty(errors);
    }
}

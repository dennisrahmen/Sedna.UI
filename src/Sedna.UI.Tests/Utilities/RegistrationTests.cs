using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Sedna.UI.Tests;

/// <summary>
/// AddSednaUi is the one line an app writes in Program.cs.
/// </summary>
public class RegistrationTests
{
    private static ServiceCollection WithJsRuntime()
    {
        var services = new ServiceCollection();
        services.AddScoped<IJSRuntime>(_ => new UnusedJsRuntime());
        return services;
    }

    [Fact]
    public void The_wrapper_is_registered_scoped()
    {
        var services = WithJsRuntime();
        services.AddSednaUi();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ISednaUi));

        // Scoped because IJSRuntime is: in Blazor Server one scope is one circuit,
        // and a singleton would call into whichever browser connected first.
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void Registering_twice_does_not_produce_two_services()
    {
        var services = WithJsRuntime();
        services.AddSednaUi(o => o.StoragePrefix = "app-a.");
        services.AddSednaUi(o => o.StoragePrefix = "app-b.");

        Assert.Single(services, d => d.ServiceType == typeof(ISednaUi));

        // The first registration wins, so a library that also calls this cannot
        // silently replace the app's configuration.
        using var provider = services.BuildServiceProvider();
        Assert.Equal("app-a.", provider.GetRequiredService<SednaUiOptions>().StoragePrefix);
    }

    [Fact]
    public void The_options_default_to_the_documented_values()
    {
        var services = WithJsRuntime();
        services.AddSednaUi();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<SednaUiOptions>();

        // The same default the boot script uses. ScriptContractTests asserts the two
        // scripts agree; this is the third place the value appears.
        Assert.Equal("sedna.", options.StoragePrefix);
        Assert.Null(options.NotifyIcon);
        Assert.False(options.LangCookie);
        // And the theme both scripts fall back to with nothing stored.
        Assert.Equal("sedna", options.Default);
    }

    [Fact]
    public void The_settings_service_starts_with_the_apps_own_default_theme()
    {
        var services = WithJsRuntime();
        services.AddSednaUi(o =>
        {
            o.Themes = [new SednaTheme("northwind", SednaTheme.Sedna.Palette)];
            o.Default = "northwind";
        });

        using var provider = services.BuildServiceProvider();
        var settings = provider.CreateScope().ServiceProvider.GetRequiredService<ISednaSettings>();

        // Before StartAsync there is nothing but the options to go on, and the theme with
        // nothing stored is the one emitted at bare :root — not the built-in name. Reporting
        // "sedna" here showed the wrong option selected in a settings UI until interop ran.
        Assert.Equal("northwind", settings.Current.Theme);
    }

    [Fact]
    public void The_settings_service_is_registered_scoped_too()
    {
        var services = WithJsRuntime();
        services.AddSednaUi();

        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(ISednaSettings));

        // Scoped for the same reason as the wrapper, and one more: it holds the state of one
        // browser and a DotNetObjectReference to hand back to it.
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Fact]
    public void The_settings_service_starts_with_the_documented_defaults()
    {
        var services = WithJsRuntime();
        services.AddSednaUi();

        using var provider = services.BuildServiceProvider();
        var settings = provider.CreateScope().ServiceProvider.GetRequiredService<ISednaSettings>();

        // Interop cannot run during prerendering, so Current holds defaults until StartAsync —
        // and IsLive is how a settings UI knows not to draw a toggle from them yet.
        Assert.False(settings.IsLive);
        Assert.Equal("sedna", settings.Current.Theme);
        Assert.Equal("dark", settings.Current.Variant);
    }

    [Theory]
    [InlineData("Dark")]
    [InlineData("auto")]
    [InlineData("")]
    public async Task An_unknown_variant_is_rejected_rather_than_coerced(string variant)
    {
        var services = WithJsRuntime();
        services.AddSednaUi();

        using var provider = services.BuildServiceProvider();
        var settings = provider.CreateScope().ServiceProvider.GetRequiredService<ISednaSettings>();

        // Falling back to dark would overwrite the reader's own stored choice with a typo, and
        // nothing would report it. Same for a direction that is neither ltr nor rtl.
        await Assert.ThrowsAsync<ArgumentException>(() => settings.SetVariantAsync(variant));
        await Assert.ThrowsAsync<ArgumentException>(() => settings.SetDirectionAsync(variant));
    }

    [Fact]
    public void AddSednaUi_rejects_a_null_collection() =>
        Assert.Throws<ArgumentNullException>(() =>
            SednaUiServiceCollectionExtensions.AddSednaUi(null!));

    // Resolving the wrapper needs an IJSRuntime in the container; nothing here
    // calls it.
    private sealed class UnusedJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new NotSupportedException();
    }
}

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

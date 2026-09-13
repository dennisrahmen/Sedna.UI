using Microsoft.JSInterop;

namespace Sedna.UI;

/// <summary>
/// One live spotlight step, from <see cref="ISednaUi.FollowSpotlightAsync"/>: keeps the app's
/// hole and bubble attached to the target until it is disposed.
/// </summary>
/// <remarks>
/// A handle the app owns, which is how a function-shaped JavaScript API crosses the interop
/// boundary: the script's <c>follow()</c> returns an object with <c>update()</c> and
/// <c>stop()</c>, and this holds a reference to it. Dispose it when the step ends — from
/// the component's own <c>DisposeAsync</c> — or the listeners and any lock outlive the step.
/// </remarks>
public sealed class SednaSpotlight : IAsyncDisposable
{
    private readonly IJSObjectReference _step;
    private bool _disposed;

    internal SednaSpotlight(IJSObjectReference step) => _step = step;

    /// <summary>
    /// Places the hole and the bubble again. Call it after every render: the bubble's own
    /// size decides which side it fits on, and that is known only once its text is laid out.
    /// </summary>
    /// <returns>
    /// The side the bubble went on — <c>top</c>, <c>right</c>, <c>bottom</c> or <c>left</c>
    /// — or <see langword="null"/> when there is no bubble, or nothing visible to highlight.
    /// </returns>
    public async Task<string?> UpdateAsync()
    {
        if (_disposed) return null;
        return await _step.InvokeAsync<string?>("update");
    }

    /// <summary>Ends the step: detaches its listeners, releases any lock, and hides the hole.</summary>
    /// <returns>A task that completes once the step has ended.</returns>
    /// <remarks>
    /// A <see cref="JSDisconnectedException"/> is swallowed here: disposal runs when a circuit
    /// ends, and by then the browser — and the step with it — is already gone.
    /// </remarks>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            await _step.InvokeVoidAsync("stop");
            await _step.DisposeAsync();
        }
        catch (JSDisconnectedException) { /* the circuit went first */ }
    }
}

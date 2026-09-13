using Microsoft.AspNetCore.Components;

namespace Sedna.UI;

/// <summary>
/// Presents an app component as an overlay and waits for the result it closes with.
/// </summary>
/// <remarks>
/// <para>
/// A presenter, not a wrapper: the component is the app's own file, and it writes its own
/// <c>&lt;dialog&gt;</c> — a <c>.modal</c>, a <c>.drawer</c> or a <c>.sheet</c> — with the
/// library's classes and the app's words. This renders it through
/// <see cref="SednaOverlayHost"/>, opens that dialog with
/// <see cref="ISednaUi.ShowModalAsync"/>, waits, and removes the component once the dialog
/// has closed and finished animating out. No element is added that the app did not write.
/// </para>
/// <para>
/// The component receives a <see cref="SednaOverlay"/> as a cascading parameter. It puts
/// <see cref="SednaOverlay.Id"/> on its <c>&lt;dialog&gt;</c>, and closes through
/// <see cref="SednaOverlay.CloseAsync"/> or <see cref="SednaOverlay.CancelAsync"/>:
/// <code>
/// &lt;dialog id="@Overlay.Id" class="modal modal-sm" aria-labelledby="@($"{Overlay.Id}-title")"&gt;
///     …
///     &lt;button class="btn btn-danger" type="button" @onclick="() =&gt; Overlay.CloseAsync(true)"&gt;Delete&lt;/button&gt;
/// &lt;/dialog&gt;
///
/// @code {
///     [CascadingParameter] public SednaOverlay Overlay { get; set; } = default!;
/// }
/// </code>
/// </para>
/// <para>
/// The <c>&lt;dialog&gt;</c> has to be in the component's <b>first</b> render. Load data
/// inside it, behind a <c>.skeleton</c>, rather than rendering the dialog only once the
/// data has arrived — the dialog is opened as soon as the component has rendered once.
/// </para>
/// <para>
/// Place <see cref="SednaOverlayHost"/> once, in the layout. Calling
/// <see cref="ShowAsync{TComponent, TResult}"/> with no host rendered throws, because
/// nothing would ever render the component and the call would never complete.
/// </para>
/// </remarks>
public interface ISednaOverlays
{
    /// <summary>
    /// Renders <typeparamref name="TComponent"/>, opens the dialog it wrote, and waits for
    /// it to close.
    /// </summary>
    /// <typeparam name="TComponent">The app component that writes the <c>&lt;dialog&gt;</c>.</typeparam>
    /// <typeparam name="TResult">What the component closes with.</typeparam>
    /// <param name="parameters">The component's parameters, by name.</param>
    /// <param name="cancellationToken">Stops the wait, closes the dialog and removes the component.</param>
    /// <returns>
    /// The value the component passed to <see cref="SednaOverlay.CloseAsync"/>. When it
    /// closed any other way — <kbd>Escape</kbd>, <see cref="SednaOverlay.CancelAsync"/>, a
    /// <c>&lt;form method="dialog"&gt;</c> button — the dialog's <c>returnValue</c> if
    /// <typeparamref name="TResult"/> is <see cref="string"/> and there is one, and
    /// <see langword="default"/> otherwise. Use a nullable <typeparamref name="TResult"/>,
    /// such as <c>bool?</c>, where "cancelled" must differ from a real <c>false</c>.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// No <see cref="SednaOverlayHost"/> is rendered, or the component closed with a value
    /// that is not a <typeparamref name="TResult"/>.
    /// </exception>
    Task<TResult?> ShowAsync<TComponent, TResult>(
        Dictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent;

    /// <summary>
    /// Renders <typeparamref name="TComponent"/>, opens the dialog it wrote, and waits for
    /// it to close — for an overlay with nothing to hand back, such as a filter drawer.
    /// </summary>
    /// <typeparam name="TComponent">The app component that writes the <c>&lt;dialog&gt;</c>.</typeparam>
    /// <param name="parameters">The component's parameters, by name.</param>
    /// <param name="cancellationToken">Stops the wait, closes the dialog and removes the component.</param>
    /// <returns>A task that completes once the dialog has closed.</returns>
    /// <exception cref="InvalidOperationException">No <see cref="SednaOverlayHost"/> is rendered.</exception>
    Task ShowAsync<TComponent>(
        Dictionary<string, object?>? parameters = null,
        CancellationToken cancellationToken = default)
        where TComponent : IComponent;
}

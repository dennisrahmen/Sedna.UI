namespace Sedna.UI;

/// <summary>
/// Typed access to the graphs on a page — <c>sednaUi.graph</c> — by the id of their
/// <c>[data-graph]</c> element.
/// </summary>
/// <remarks>
/// <para>
/// The graph is the app's markup: a <c>&lt;div class="graph" data-graph id="…"&gt;</c> with a
/// <c>.graph-canvas</c> inside it, its toolbar, its tooltip and its empty state — see the Graph pages
/// of the catalogue. This service only calls into the one the app wrote, so every method takes that
/// element's id and does nothing, with a console warning, when there is none.
/// </para>
/// <para>
/// Call it from <c>OnAfterRenderAsync</c>, once the element exists. A graph that has not started yet
/// — one below the fold, which starts as it scrolls near — is started by the call. For records that
/// arrive only by call, put <c>data-graph-deferred</c> on the element: it waits, behind its
/// <c>.graph-wait</c>, for the first <see cref="SetDataAsync"/> instead of drawing an empty state.
/// </para>
/// <para>
/// What the reader does comes back as events on the element, bindable with <c>@using Sedna.UI</c>:
/// <c>@onsedna-graph-select</c>, <c>-open</c>, <c>-hover</c>, <c>-context</c>, <c>-connect</c>,
/// <c>-expand</c>, <c>-collapse</c>, <c>-change</c> and <c>-ready</c>. Nothing here holds a
/// <c>DotNetObjectReference</c>.
/// </para>
/// </remarks>
public interface ISednaGraphs
{
    /// <summary>
    /// Replaces the graph's records. What stayed keeps its place, what changed is restyled in place,
    /// and what arrived is put beside what it links to — so a link written while the page is open
    /// appears where the reader is looking.
    /// </summary>
    /// <param name="graphId">The <c>[data-graph]</c> element's id.</param>
    /// <param name="data">Every record and link, not a difference: the script works the difference out.</param>
    /// <param name="relayout">Lays the whole drawing out again instead of fitting the new records in.</param>
    /// <param name="cancellationToken">Stops waiting for a large graph's layout.</param>
    /// <returns>What is on screen afterwards, or null when there is no such graph.</returns>
    Task<SednaGraphStats?> SetDataAsync(string graphId, SednaGraphData data, bool relayout = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Narrows the drawing to what <paramref name="filter"/> allows. Controls in the page with
    /// <c>data-graph-filter</c> apply on top of it, for the fields they name.
    /// </summary>
    /// <returns>What is on screen afterwards, or null when there is no such graph.</returns>
    Task<SednaGraphStats?> FilterAsync(string graphId, SednaGraphFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the records whose name, id, meta, kind or tags contain every word, and brings them into view.</summary>
    /// <param name="graphId">The <c>[data-graph]</c> element's id.</param>
    /// <param name="text">The words; null or empty clears the marks.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    Task<SednaGraphStats?> SearchAsync(string graphId, string? text, CancellationToken cancellationToken = default);

    /// <summary>Selects a record and centres it, or clears the selection. Dispatches <c>sedna-graph-select</c>.</summary>
    Task SelectAsync(string graphId, string? nodeId, CancellationToken cancellationToken = default);

    /// <summary>Shows only a record and its neighbourhood to <paramref name="depth"/> hops, or everything again.</summary>
    Task<SednaGraphStats?> FocusAsync(string graphId, string? nodeId, int depth = 1,
        CancellationToken cancellationToken = default);

    /// <summary>Lays the graph out again, in <paramref name="layout"/> — one of <see cref="SednaGraphLayout"/>.</summary>
    /// <param name="graphId">The <c>[data-graph]</c> element's id.</param>
    /// <param name="layout">The layout; null keeps the current one and runs it again.</param>
    /// <param name="direction">For <c>dagre</c> and <c>tree</c>: <c>TB</c>, <c>LR</c>, <c>BT</c> or <c>RL</c>.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    Task LayoutAsync(string graphId, string? layout = null, string? direction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes how the graph is drawn: <c>labels</c> (auto, all, none), <c>edge-labels</c> (always,
    /// hover, none), <c>colour</c> (a name from <see cref="SednaGraphNode.Tones"/>, or <c>tone</c>),
    /// <c>nodes</c> (dot, box), <c>curve</c>, <c>arrows</c>, <c>spacing</c>, <c>direction</c>,
    /// <c>layout</c> or <c>depth</c>.
    /// </summary>
    Task SetOptionAsync(string graphId, string option, string value, CancellationToken cancellationToken = default);

    /// <summary>Fits everything on screen into the frame.</summary>
    Task FitAsync(string graphId, CancellationToken cancellationToken = default);

    /// <summary>Zooms by <paramref name="factor"/> about the middle — 1.25 in, 0.8 out.</summary>
    Task ZoomAsync(string graphId, double factor, CancellationToken cancellationToken = default);

    /// <summary>The whole drawing as a picture.</summary>
    /// <returns>A <c>data:</c> URL of the SVG or PNG, or null when there is no such graph.</returns>
    Task<string?> ExportAsync(string graphId, SednaGraphExport format = SednaGraphExport.Svg,
        CancellationToken cancellationToken = default);

    /// <summary>Downloads the whole drawing as a file, as the export buttons do.</summary>
    /// <param name="graphId">The <c>[data-graph]</c> element's id.</param>
    /// <param name="format">SVG or PNG.</param>
    /// <param name="fileName">The file's name, without its extension; unset, the graph's id.</param>
    /// <param name="cancellationToken">Stops waiting.</param>
    Task DownloadAsync(string graphId, SednaGraphExport format = SednaGraphExport.Svg, string? fileName = null,
        CancellationToken cancellationToken = default);

    /// <summary>Folds a group, or every group when <paramref name="groupId"/> is null.</summary>
    Task<SednaGraphStats?> CollapseAsync(string graphId, string? groupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Unfolds a group, or every group when <paramref name="groupId"/> is null.</summary>
    Task<SednaGraphStats?> ExpandAsync(string graphId, string? groupId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches drawing links by dragging from one record to another. Each one drawn dispatches
    /// <c>sedna-graph-connect</c>; the app adds it to its own data.
    /// </summary>
    Task SetDrawingAsync(string graphId, bool on, CancellationToken cancellationToken = default);

    /// <summary>Switches the soft outlines around each <see cref="SednaGraphNode.Group"/>.</summary>
    Task SetHullsAsync(string graphId, bool on, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the graph's <c>data-graph-src</c> again and shows what changed, as
    /// <see cref="SetDataAsync"/> does — for a map that follows data the app does not hold.
    /// </summary>
    /// <returns>What is on screen afterwards, or null when there is no such graph.</returns>
    Task<SednaGraphStats?> ReloadAsync(string graphId, CancellationToken cancellationToken = default);

    /// <summary>What is on screen now.</summary>
    Task<SednaGraphStats?> StatsAsync(string graphId, CancellationToken cancellationToken = default);
}

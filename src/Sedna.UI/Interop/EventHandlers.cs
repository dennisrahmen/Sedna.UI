using Microsoft.AspNetCore.Components;

namespace Sedna.UI;

/// <summary>
/// Makes the events <c>Sedna.UI.js</c> dispatches bindable from Razor, with their data:
/// drag and drop — <c>@onsedna-drop</c>, <c>@onsedna-dragstart</c>, <c>@onsedna-dragend</c> — and
/// the graph — <c>@onsedna-graph-select</c>, <c>-open</c>, <c>-hover</c>, <c>-context</c>,
/// <c>-connect</c>, <c>-expand</c>, <c>-collapse</c>, <c>-change</c> and <c>-ready</c> — and the
/// rich-text editor — <c>@onsedna-editor-change</c>, <c>@onsedna-editor-ready</c> and
/// <c>@onsedna-editor-media</c> — a pane or a diagram following what is newest,
/// <c>@onsedna-follow</c> — a tree, <c>@onsedna-tree-expand</c>, <c>-collapse</c>,
/// <c>-select</c> and <c>-check</c> — and a <c>&lt;time data-countdown&gt;</c> reaching zero,
/// <c>@onsedna-countdown-end</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Razor compiler finds an event only through a class of exactly this name, and only in
/// a namespace the component imports — so <c>@using Sedna.UI</c> in <c>_Imports.razor</c>.
/// <c>Sedna.UI.lib.module.js</c>, which Blazor loads by itself, registers the same names in the
/// browser.
/// </para>
/// <para>
/// The script never moves an item and never adds a link. A drop, a link drawn or a record opened
/// is an event the app handles in its own data and re-renders; see <see cref="SednaDropEventArgs"/>
/// and <see cref="SednaGraphConnectEventArgs"/>.
/// </para>
/// </remarks>
[EventHandler("onsedna-dragstart", typeof(SednaDragEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onsedna-drop", typeof(SednaDropEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-dragend", typeof(SednaDragEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-graph-ready", typeof(SednaGraphEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-graph-change", typeof(SednaGraphEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-graph-select", typeof(SednaGraphNodeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-graph-open", typeof(SednaGraphNodeEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onsedna-graph-hover", typeof(SednaGraphNodeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-graph-context", typeof(SednaGraphContextEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onsedna-graph-connect", typeof(SednaGraphConnectEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-graph-expand", typeof(SednaGraphNodeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-graph-collapse", typeof(SednaGraphNodeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-editor-ready", typeof(SednaEditorEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-editor-change", typeof(SednaEditorEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-editor-media", typeof(SednaEditorMediaEventArgs), enableStopPropagation: true, enablePreventDefault: true)]
[EventHandler("onsedna-editor-file", typeof(SednaEditorFileEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-follow", typeof(SednaFollowEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-resize", typeof(SednaResizeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-tree-expand", typeof(SednaTreeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-tree-collapse", typeof(SednaTreeEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-tree-select", typeof(SednaTreeSelectEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-tree-check", typeof(SednaTreeSelectEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
[EventHandler("onsedna-countdown-end", typeof(SednaCountdownEventArgs), enableStopPropagation: true, enablePreventDefault: false)]
public static class EventHandlers
{
}

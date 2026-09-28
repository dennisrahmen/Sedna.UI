using System.Text.Json.Serialization;

namespace Sedna.UI;

/// <summary>
/// The records a graph draws and the links between them, as <see cref="ISednaGraphs.SetDataAsync"/>
/// hands them to the browser.
/// </summary>
/// <remarks>
/// The same model as the markup form — <c>li[data-node]</c> and <c>li[data-edge]</c> inside a
/// <c>[data-graph-data]</c> list — so a graph can start from markup and be replaced by a call, or
/// the other way round. A link whose end is not a record, and a record whose id is used twice, are
/// dropped by the script with a warning rather than failing the call.
/// </remarks>
/// <param name="Nodes">The records.</param>
/// <param name="Edges">The links between them.</param>
public sealed record SednaGraphData(IReadOnlyList<SednaGraphNode> Nodes, IReadOnlyList<SednaGraphEdge> Edges)
{
    /// <summary>A graph with nothing in it — the empty state, not a missing one.</summary>
    public static SednaGraphData Empty { get; } = new([], []);
}

/// <summary>A record on the graph: a node.</summary>
/// <remarks>
/// Only <see cref="Id"/> and <see cref="Label"/> are required. Everything left unset is left out of
/// what crosses to the browser, so a large graph sends only what it uses.
/// </remarks>
/// <param name="Id">Unique among the graph's records and links.</param>
/// <param name="Label">The name written on the canvas and read out by the keyboard's announcements.</param>
public sealed record SednaGraphNode(string Id, string Label)
{
    /// <summary>A category — what a filter chip, a legend key and a tooltip slot can name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; init; }

    /// <summary>
    /// The record's colour: a series from <see cref="SednaGraphTone"/>, the same colour a legend
    /// swatch with the matching <c>.series-*</c> class paints. Unset is the brand colour.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tone { get; init; }

    /// <summary>
    /// Other colourings, by name — <c>["team"] = "3"</c> — which <c>data-graph-colour="team"</c> or
    /// <see cref="ISednaGraphs.SetOptionAsync"/> switches the whole graph to.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Tones { get; init; }

    /// <summary>The record's outline. A kind with a shape of its own never relies on colour alone.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SednaGraphShape? Shape { get; init; }

    /// <summary>A Remix Icon class drawn inside the record — <c>ri-server-line</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon { get; init; }

    /// <summary>
    /// Which island the <c>islands</c> layout puts it on, and which outline <c>data-graph-hulls</c>
    /// draws around it. The record whose <see cref="Id"/> is a group's name is that group's heart.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Group { get; init; }

    /// <summary>Groups sharing a cluster are packed side by side by the <c>islands</c> layout.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Cluster { get; init; }

    /// <summary>The record it is drawn inside: a compound group, which <c>data-graph-collapse</c> can fold.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Parent { get; init; }

    /// <summary>Starts a <see cref="Parent"/> group folded, when the graph can fold groups.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Collapsed { get; init; }

    /// <summary>How large the record is drawn. Unset, it is how many links it has.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Weight { get; init; }

    /// <summary>Drawn quieter — settled, retired, less important — and hidden by a filter that hides muted records.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Muted { get; init; }

    /// <summary>The record the graph is about: ringed, always named, and where the keyboard starts.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Root { get; init; }

    /// <summary>Named in bold and placed first when names compete for room.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Hub { get; init; }

    /// <summary>Where opening the record goes, unless <c>sedna-graph-open</c> is handled and cancelled.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Href { get; init; }

    /// <summary>A second line: in the default tooltip, under the name in a box, and in announcements as <c>{meta}</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Meta { get; init; }

    /// <summary>Values a filter matches on any one of, and a search finds.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Tags { get; init; }

    /// <summary>A position, for the <c>preset</c> layout.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? X { get; init; }

    /// <summary>A position, for the <c>preset</c> layout.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Y { get; init; }

    /// <summary>Draws this record as a dot or as a box, whatever the graph draws the rest as.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SednaGraphDisplay? Display { get; init; }

    /// <summary>
    /// The app's own fields, by name — what a filter, a tooltip slot (<c>data-graph-field="owner"</c>)
    /// and an announcement (<c>{owner}</c>) can name, and what the events carry back.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string?>? Fields { get; init; }
}

/// <summary>A link between two records: an edge.</summary>
/// <param name="Source">The id of the record it starts at.</param>
/// <param name="Target">The id of the record it ends at.</param>
public sealed record SednaGraphEdge(string Source, string Target)
{
    /// <summary>Unique among the graph's records and links. Unset, one is made from the ends.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }

    /// <summary>What the link says — written on it when it is lit, and in the side panel.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Label { get; init; }

    /// <summary>A category — what an <c>edge.kind</c> filter names.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Kind { get; init; }

    /// <summary>The link's colour, from <see cref="SednaGraphTone"/>. Unset is the quiet border colour.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tone { get; init; }

    /// <summary>Solid, dashed or dotted: what a link means, without relying on colour.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SednaGraphLine? Line { get; init; }

    /// <summary>How heavy the line is: 1 is normal, 0.6 light, 2 heavy.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Weight { get; init; }

    /// <summary>Which ends have an arrowhead. Unset takes the graph's <c>data-graph-arrows</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public SednaGraphArrow? Arrow { get; init; }

    /// <summary>Drawn quieter.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Muted { get; init; }

    /// <summary>The app's own fields, by name.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string?>? Fields { get; init; }
}

/// <summary>
/// The colours a record or a link can wear. Each is the colour of the <c>.series-*</c> class of the
/// same name, so a legend swatch and the canvas agree in every theme.
/// </summary>
/// <remarks>
/// <c>1</c> to <c>6</c> are categorical: merely different from each other. The semantic ones mean
/// something — never colour a category with <see cref="Danger"/>.
/// </remarks>
public static class SednaGraphTone
{
    /// <summary>The categorical series <paramref name="n"/>, 1 to 6 — the brand is 1.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> is not 1 to 6.</exception>
    public static string Series(int n) => n is >= 1 and <= 6
        ? n.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : throw new ArgumentOutOfRangeException(nameof(n), n, "A series is 1 to 6.");

    /// <summary>Sends something outward, or is healthy.</summary>
    public const string Go = "go";

    /// <summary>Needs attention.</summary>
    public const string Warn = "warn";

    /// <summary>Failed, or destructive.</summary>
    public const string Danger = "danger";

    /// <summary>Informational.</summary>
    public const string Info = "info";

    /// <summary>The quiet border colour.</summary>
    public const string Muted = "muted";

    /// <summary>The brand colour itself.</summary>
    public const string Brand = "brand";

    /// <summary>The accent colour.</summary>
    public const string Accent = "accent";
}

/// <summary>The layouts a graph can be drawn in — <c>data-graph-layout</c>.</summary>
public static class SednaGraphLayout
{
    /// <summary>Springs: the default for a map of records.</summary>
    public const string Force = "force";

    /// <summary>One island per <see cref="SednaGraphNode.Group"/>, packed in rows: a large graph read by its groups.</summary>
    public const string Islands = "islands";

    /// <summary>Rings of hops around the root or the focus.</summary>
    public const string Rings = "rings";

    /// <summary>The most connected in the middle.</summary>
    public const string Concentric = "concentric";

    /// <summary>Breadth-first from the roots.</summary>
    public const string Tree = "tree";

    /// <summary>A layered hierarchy: org charts, flows, dependency trees.</summary>
    public const string Dagre = "dagre";

    /// <summary>Fast springs that also lay out nested groups.</summary>
    public const string Fcose = "fcose";

    /// <summary>A grid, busiest first.</summary>
    public const string Grid = "grid";

    /// <summary>A circle, busiest first.</summary>
    public const string Circle = "circle";

    /// <summary>Where <see cref="SednaGraphNode.X"/> and <see cref="SednaGraphNode.Y"/> put each record.</summary>
    public const string Preset = "preset";
}

/// <summary>A record's outline.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SednaGraphShape>))]
public enum SednaGraphShape
{
    /// <summary>A circle — the default.</summary>
    [JsonStringEnumMemberName("circle")] Circle,

    /// <summary>A square with sharp corners.</summary>
    [JsonStringEnumMemberName("square")] Square,

    /// <summary>A square with rounded corners.</summary>
    [JsonStringEnumMemberName("rounded")] Rounded,

    /// <summary>A diamond.</summary>
    [JsonStringEnumMemberName("diamond")] Diamond,

    /// <summary>A hexagon.</summary>
    [JsonStringEnumMemberName("hexagon")] Hexagon,

    /// <summary>An octagon.</summary>
    [JsonStringEnumMemberName("octagon")] Octagon,

    /// <summary>A pentagon.</summary>
    [JsonStringEnumMemberName("pentagon")] Pentagon,

    /// <summary>A triangle.</summary>
    [JsonStringEnumMemberName("triangle")] Triangle,

    /// <summary>A tag.</summary>
    [JsonStringEnumMemberName("tag")] Tag,

    /// <summary>A star.</summary>
    [JsonStringEnumMemberName("star")] Star,

    /// <summary>A barrel.</summary>
    [JsonStringEnumMemberName("barrel")] Barrel,

    /// <summary>A rhomboid.</summary>
    [JsonStringEnumMemberName("rhomboid")] Rhomboid,

    /// <summary>A vee.</summary>
    [JsonStringEnumMemberName("vee")] Vee,
}

/// <summary>How a link's line is drawn.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SednaGraphLine>))]
public enum SednaGraphLine
{
    /// <summary>A solid line — the default.</summary>
    [JsonStringEnumMemberName("solid")] Solid,

    /// <summary>A dashed line.</summary>
    [JsonStringEnumMemberName("dashed")] Dashed,

    /// <summary>A dotted line.</summary>
    [JsonStringEnumMemberName("dotted")] Dotted,
}

/// <summary>Which ends of a link carry an arrowhead.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SednaGraphArrow>))]
public enum SednaGraphArrow
{
    /// <summary>Neither.</summary>
    [JsonStringEnumMemberName("none")] None,

    /// <summary>The target end.</summary>
    [JsonStringEnumMemberName("target")] Target,

    /// <summary>The source end.</summary>
    [JsonStringEnumMemberName("source")] Source,

    /// <summary>Both ends.</summary>
    [JsonStringEnumMemberName("both")] Both,
}

/// <summary>How a record is drawn.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SednaGraphDisplay>))]
public enum SednaGraphDisplay
{
    /// <summary>A shape sized by its links, its name beneath: a map of many records.</summary>
    [JsonStringEnumMemberName("dot")] Dot,

    /// <summary>Its name inside a box: a diagram of a few dozen, where each record is read.</summary>
    [JsonStringEnumMemberName("box")] Box,
}

/// <summary>What <see cref="ISednaGraphs.ExportAsync"/> writes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SednaGraphExport>))]
public enum SednaGraphExport
{
    /// <summary>A vector drawing, every record a group with a title.</summary>
    [JsonStringEnumMemberName("svg")] Svg,

    /// <summary>A picture at twice the screen's resolution.</summary>
    [JsonStringEnumMemberName("png")] Png,
}

/// <summary>What a graph shows, as <see cref="ISednaGraphs.FilterAsync"/> hands it to the browser.</summary>
/// <remarks>
/// <see cref="Nodes"/> is a choice — a record without the field is not what was chosen, and goes.
/// <see cref="Except"/> is a switch turned off — it hides what it names and nothing else. A field
/// names a property (<c>kind</c>, <c>group</c>, <c>cluster</c>, <c>tone</c>, <c>tags</c>) or one of
/// the record's <see cref="SednaGraphNode.Fields"/>.
/// </remarks>
public sealed record SednaGraphFilter
{
    /// <summary>Only records with one of these values, per field.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Nodes { get; init; }

    /// <summary>Not records with one of these values, per field.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Except { get; init; }

    /// <summary>Only links with one of these values, per field.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Edges { get; init; }

    /// <summary>Not links with one of these values, per field.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? EdgesExcept { get; init; }

    /// <summary>Records and links hidden by id.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Hide { get; init; }

    /// <summary>Shows records marked <see cref="SednaGraphNode.Muted"/>. On by default.</summary>
    public bool Muted { get; init; } = true;

    /// <summary>Shows records with no visible link. On by default.</summary>
    public bool Isolated { get; init; } = true;

    /// <summary>Shows only this record and its neighbourhood.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Focus { get; init; }

    /// <summary>How many hops of <see cref="Focus"/>'s neighbourhood are shown, 1 to 6.</summary>
    public int Depth { get; init; } = 1;
}

/// <summary>What a graph shows after a change, for a count beside it.</summary>
/// <param name="Nodes">Records on screen.</param>
/// <param name="Edges">Links on screen.</param>
/// <param name="Matches">Records a search has marked.</param>
/// <param name="TotalNodes">Records in the data, shown or not.</param>
/// <param name="TotalEdges">Links in the data, shown or not.</param>
/// <param name="Selected">The selected record's id, or null.</param>
public sealed record SednaGraphStats(int Nodes, int Edges, int Matches, int TotalNodes, int TotalEdges, string? Selected);

namespace Sedna.UI.Catalogue.Navigation;

/// <summary>
/// The catalogue's page registry: the one list the sidebar, the landing-page
/// tiles, the search filter, the command palette, the legacy redirects and the MCP
/// server are all built from.
/// </summary>
/// <remarks>
/// <para>
/// <c>NavigationTests</c> compares this against the <c>[Route]</c> attributes the
/// compiler emits, in both directions, so an entry with no page and a page with no
/// entry both fail.
/// </para>
/// <para>
/// One family per page, and one job per group. A group holds between two and six
/// entries so that it can be scanned; a page that carries two families gets split
/// rather than named with an ampersand.
/// </para>
/// <para>
/// <c>Keywords</c> is hand-kept and deliberately not generated. It carries the terms
/// a reader would actually type that appear nowhere in the label or the blurb —
/// "sticky" should find the Tables page, and it does not.
/// </para>
/// </remarks>
internal static class CataloguePages
{
    public const string Start = "Start";
    public const string Frame = "Frame";
    public const string Structure = "Structure";
    public const string Actions = "Actions";
    public const string Forms = "Forms";
    public const string Data = "Data";
    public const string Media = "Text and media";
    public const string Feedback = "Feedback";
    public const string Overlays = "Overlays";
    public const string Reference = "Reference";

    public static IReadOnlyList<CataloguePage> All { get; } =
    [
        new("/", Start, "Overview", "ri-home-4-line",
            "What the library is, and what an app built with it looks like.",
            "overview home intro showcase"),
        new("/getting-started", Start, "Getting started", "ri-rocket-2-line",
            "Install the package, wire the host page, and rebrand the app.",
            "install nuget host page brand.css tokens registration setup agent mcp"),
        new("/concepts", Start, "Concepts", "ri-shapes-line",
            "The two tiers, the cascade layers, the z-order scale and the token contract.",
            "tier layer cascade z-index override specificity naming contract"),
        new("/tokens", Start, "Tokens", "ri-palette-line",
            "Every colour, font and shadow token, read live from the loaded stylesheet.",
            "colour color token variable var swatch"),

        new("/frame", Frame, "Shell and nav", "ri-side-bar-line",
            "Layout, sidebar, topbar and user widget — the chrome this site is built from.",
            "layout sidebar topbar nav user-widget collapsed rail reconnect active link"),
        new("/layouts", Frame, "Layouts", "ri-layout-3-line",
            "Auth and full-bleed shells, collapsible nav groups, and the skip link.",
            "auth sign-in full-bleed nav-group skip-link landmark"),

        new("/structure", Structure, "Page structure", "ri-layout-top-line",
            "Title rows, dividers, callouts and code blocks.",
            "page-head divider callout note code-block copy clamp expand"),
        new("/card", Structure, "Cards", "ri-square-line",
            "Head, body and key/value rows. Put whatever markup you need inside.",
            "card-head card-body kv key value warning caveat"),
        new("/grid", Structure, "Grids", "ri-layout-grid-line",
            "Three breakpoint-free layout primitives.",
            "masonry card-grid field-grid layout"),
        new("/collections", Structure, "Collections", "ri-list-unordered",
            "Lists, steps, timelines, accordions and trees.",
            "list steps wizard timeline history accordion details tree hierarchy lockup"),
        new("/tabs", Structure, "Tabs and segmented", "ri-folder-2-line",
            "Swap a region of content, or change a setting in place.",
            "tab tablist panel segmented radio"),

        new("/button", Actions, "Buttons", "ri-cursor-line",
            "Six variants chosen by meaning, three sizes, plus icon-only, ghost and the FAB.",
            "btn primary go warn danger secret disabled icon link ghost fab floating small large"),
        new("/button-group", Actions, "Button groups", "ri-layout-row-line",
            "Adjacent actions joined into one control, and the split button.",
            "btn-group split-btn caret dropdown zoom"),
        new("/toolbar", Actions, "Toolbar", "ri-filter-3-line",
            "The filter bar that sits above a table or list.",
            "filter search count"),
        new("/menu", Actions, "Menus and popovers", "ri-more-2-fill",
            "A dropdown of actions, an anchored panel of content, and the value trigger.",
            "dropdown actions anchor scrim disclosure popover popovertarget value-trigger picker"),

        new("/form", Forms, "Forms", "ri-edit-box-line",
            "Fields, selects, checkboxes, radios, switches, validation and input groups.",
            "input label select checkbox radio switch validation aria-invalid input-group fieldset legend actions"),
        new("/form-file", Forms, "File inputs", "ri-attachment-2",
            "File pickers and a dropzone that takes a real drop.",
            "file dropzone upload attachment drag drop file-list"),
        new("/form-numeric", Forms, "Ranges and steppers", "ri-number-1",
            "Ranges, steppers and date inputs — the controls whose internals are per-engine.",
            "range slider stepper number date time picker spinner appearance"),

        new("/table", Data, "Tables", "ri-table-line",
            "One class on the table, plus sticky headers, sorting and a stacked layout.",
            "sticky sortable aria-sort zebra selected expandable tfoot totals numeric stacked col-num"),
        new("/stat", Data, "Stats", "ri-numbers-line",
            "One number that matters, which way it moved, and what it is measured against.",
            "kpi number metric delta tile target unavailable dashboard"),
        new("/badge", Data, "Badges", "ri-price-tag-3-line",
            "Semantic pills in three sizes, plus three categorical hues.",
            "pill status cyan orange teal small large"),
        new("/chip", Data, "Chips", "ri-price-tag-line",
            "Removable filters and recipients, and the set they sit in.",
            "chip tag filter dismissible removable chip-set"),
        new("/avatar", Data, "Avatars", "ri-user-line",
            "A person or an actor, the group, and the lockup that names them.",
            "avatar initials group person user identity lockup name email subtext"),
        new("/pager", Data, "Pagination and breadcrumbs", "ri-more-line",
            "Moving through a long result set, and where the page sits.",
            "pagination page-link breadcrumb trail"),

        new("/media", Media, "Media and prose", "ri-image-line",
            "Bounded media, long-form text, and redacted values.",
            "image figure gallery aspect prose article redacted secret"),
        new("/markdown", Media, "Markdown", "ri-markdown-line",
            "Rendered Markdown, and an editor with a live preview.",
            "md-editor preview render markdown-body"),

        new("/alert", Feedback, "Alerts", "ri-error-warning-line",
            "Inline banners for a state that persists while the page is open.",
            "banner inline warning danger info"),
        new("/toast", Feedback, "Toasts", "ri-notification-3-line",
            "A confirmation that appears, says what happened, and goes away.",
            "toast stack notification transient dismiss"),
        new("/progress", Feedback, "Progress and spinners", "ri-loader-4-line",
            "Determinate bars, indeterminate bars, spinners and skeletons.",
            "progress bar spinner skeleton loading busy indeterminate placeholder"),
        new("/empty-state", Feedback, "Empty and live states", "ri-inbox-line",
            "Nothing to show, waiting for something, and the state of a live connection.",
            "empty-state nothing found access failed retry pending connection streaming health status stale output log"),

        new("/modal", Overlays, "Modal", "ri-window-2-line",
            "A question that has to be answered before anything else happens.",
            "dialog backdrop confirm showModal sm lg"),
        new("/drawer", Overlays, "Drawers and sheets", "ri-layout-right-line",
            "A panel from an edge, for a secondary flow that can be left.",
            "drawer sheet side panel filter edge scrim"),
        new("/palette", Overlays, "Command palette", "ri-command-line",
            "The Ctrl-K box: type, see matching commands, run one.",
            "palette command ctrl-k cmdk combobox listbox spotlight-search"),
        new("/spotlight", Overlays, "Spotlight", "ri-focus-3-line",
            "Dims the page except one element, for a tour or a first-run hint.",
            "spotlight tour onboarding hint highlight hole walkthrough"),

        new("/utility", Reference, "Utilities", "ri-scissors-cut-line",
            "The single-purpose classes: layout, gaps, text, state and print.",
            "text-end truncate clamp visually-hidden sr-only sedna-row sedna-col sedna-fill gap margin print"),
        new("/script", Reference, "The script", "ri-code-s-slash-line",
            "Toasts, confirmations, clipboard, hover hints, and the delegated behaviours.",
            "sednaUi toast confirm copy clipboard tips hover menu tabs dropzone follow interop javascript"),
        // Not "/mcp": that route is the MCP endpoint itself, mapped in Program.cs.
        new("/mcp-server", Reference, "MCP server", "ri-robot-2-line",
            "The read-only endpoint an agent points at, its six tools and four resources.",
            "mcp agent ai claude tool resource read-only streamable http rate limit since"),
    ];

    /// <summary>The group names, in the order the sidebar shows them.</summary>
    public static IReadOnlyList<string> Groups { get; } =
        [Start, Frame, Structure, Actions, Forms, Data, Media, Feedback, Overlays, Reference];

    /// <summary>
    /// Routes that used to exist, and where they went. The site is public and linked
    /// from the README, the package page and the docs, so a split page leaves its old
    /// address working rather than 404ing.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Moved { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/feedback"] = "/toast",
            ["/overlay"] = "/drawer",
            ["/everything"] = "/",
        };

    /// <summary>The pages in one group, in registration order.</summary>
    public static IEnumerable<CataloguePage> InGroup(string group) =>
        All.Where(p => p.Group == group);
}

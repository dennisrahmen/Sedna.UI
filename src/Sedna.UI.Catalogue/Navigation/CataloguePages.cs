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
/// One family per page, and one job per group. A group is an area on the sidebar's
/// rail, so it can hold as many pages as its job has; a page that carries two
/// families gets split rather than named with an ampersand.
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
    /// <summary>The layout around the controls, and the controls themselves.</summary>
    public const string Forms = "Forms";
    public const string Data = "Data";
    /// <summary>The small things that name or mark a record: badges, chips, avatars.</summary>
    public const string Labels = "Labels";
    /// <summary>Content the app did not author: media, prose, Markdown.</summary>
    public const string Content = "Content";
    public const string Feedback = "Feedback";
    public const string Overlays = "Overlays";
    public const string Utilities = "Utilities";
    /// <summary>The section of Start that holds the reference pages: the script, C#, the MCP server.</summary>
    public const string Reference = "Reference";

    /// <summary>
    /// The page every unknown address renders. Deliberately not in <see cref="All"/>:
    /// it is no catalogue page, so it belongs in no group, index or search.
    /// </summary>
    public const string NotFoundRoute = "/not-found";

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
        new("/branding", Start, "Branding", "ri-drop-line",
            "The mark, its detail tiers, the palette read live, and the decisions behind it.",
            "logo mark tile wordmark lockup clear space misuse svg outfit theme forest cobalt switch"),

        new("/frame", Frame, "Shell", "ri-layout-3-line",
            "The three page shells, the rhythm inside them, and the skip link.",
            "layout shell bare auth sign-in full-bleed page-gap rhythm spacing margin stack owl skip-link landmark narrow responsive drawer"),
        new("/nav", Frame, "Sidebar and nav", "ri-side-bar-line",
            "Links, the current one, collapsible groups, and the icon rail.",
            "sidebar nav nav-link nav-group active link collapsed rail flyout icon badge count external open details group ActiveLink"),
        new("/nav-layouts", Frame, "Nav layouts", "ri-route-line",
            "Arranging a nav that has outgrown one list: accordions, a filter, an area rail, areas in the topbar.",
            "accordion filter search narrow long many pages area areas rail panel two-level topbar-nav header links sidebar--areas nav-areas nav-filter data-nav-filter data-nav-area keywords overwhelming"),
        new("/bottombar", Frame, "Bottom bar", "ri-layout-bottom-2-line",
            "The bar across the bottom of a phone, for the few places or actions used most.",
            "bottombar bottom bar tab bar mobile phone dock accessory hide on scroll layout--bottombar tint card icons menu first sheet actions"),
        new("/topbar", Frame, "Topbar and user", "ri-layout-top-2-line",
            "Header search, the status and version chips, and the user widget.",
            "topbar header search search-max user-widget user-menu avatar version build tag status health tip hover hint"),
        new("/status-bar", Frame, "Status bar", "ri-signal-wifi-error-line",
            "The strip that says the circuit dropped, the session expired, or something threw.",
            "status-bar reconnect reconnecting paused failed expired error blazor-error-ui unhandled circuit disconnect banner"),
        new("/hover-hints", Frame, "Hover hints", "ri-cursor-line",
            "One line of what-it-does on any control, and which side it opens.",
            "tip tooltip hover hint data-tip data-tip-pos sedna-tip gate title bubble delegated explain consequence"),
        new("/common-pages", Frame, "Common pages", "ri-signpost-line",
            "Access denied, not found, the error, the expired session, the maintenance window.",
            "403 404 500 503 401 access denied forbidden unauthorized permission role not found missing error crash session expired signed out timeout maintenance outage unavailable downtime status code reference trace correlation"),

        new("/structure", Structure, "Page structure", "ri-layout-top-line",
            "Title rows, dividers, callouts and code blocks.",
            "page-head divider callout note code-block copy clamp expand lip"),
        new("/card", Structure, "Cards", "ri-square-line",
            "Head, body and key/value rows. Put whatever markup you need inside.",
            "card-head card-body card-foot footer foot kv key value warning caveat flush padding canvas actions"),
        new("/grid", Structure, "Grids", "ri-layout-grid-line",
            "Three breakpoint-free layout primitives.",
            "masonry card-grid field-grid form-grid form fields two-up columns layout span full row stranded"),
        new("/list", Structure, "Lists", "ri-list-unordered",
            "Rows that open, act or merely show, as dividers or as cards, with stacking group headers.",
            "list list-row list-main list-title list-sub list-meta lockup line count clamp wrap list--cards cards sticky group header flush density"),
        new("/collections", Structure, "Collections", "ri-node-tree",
            "Steps, accordions and trees.",
            "steps wizard accordion details summary tree hierarchy folder expand exclusive"),
        new("/timeline", Structure, "Timeline", "ri-time-line",
            "A record of what happened and when, with icon, avatar and gutter variants.",
            "timeline history audit trail event feed activity log run comment thread marker dot when"),
        new("/tabs", Structure, "Tabs", "ri-folder-2-line",
            "Swap a region of content in place, and a strip of links that only looks like one.",
            "tab tablist panel tab-count tab--active data-tabs strip links"),
        new("/segmented", Structure, "Segmented control", "ri-toggle-line",
            "Change a setting in place: a few short options, built from radios.",
            "segmented radio toggle group option setting view switch sizes toolbar"),

        new("/button", Actions, "Buttons", "ri-cursor-line",
            "Six variants chosen by meaning, three sizes, plus icon-only, ghost and the FAB.",
            "btn primary go warn danger secret disabled icon link ghost fab floating small large"),
        new("/button-group", Actions, "Button groups", "ri-layout-row-line",
            "Adjacent actions joined into one control, and the split button.",
            "btn-group split-btn caret dropdown zoom"),
        new("/toolbar", Actions, "Toolbar", "ri-filter-3-line",
            "The filter bar that sits above a table or list.",
            "filter search count width wide grow toolbar-input selection bulk"),
        new("/menu", Actions, "Menus", "ri-more-2-fill",
            "A dropdown of actions opened from a control, and the value trigger.",
            "dropdown actions anchor scrim disclosure menu-anchor data-menu-toggle value-trigger picker overflow"),
        new("/popover", Actions, "Popovers", "ri-message-2-line",
            "A small amount of content anchored to the control that opened it.",
            "popover popovertarget anchor top layer light dismiss definition detail explain tooltip rich"),

        new("/form", Forms, "Form layout", "ri-edit-box-line",
            "The anatomy of a field, two-up rows, sections and the actions row.",
            "form-field form-label form-hint field-grid two-up fieldset legend section actions split start required settings card assemble submit whole"),
        new("/form-validation", Forms, "Validation", "ri-shield-check-line",
            "How a rejected value is shown, and what a screen reader is told.",
            "form-error form-warning aria-invalid aria-describedby required label marker invalid message reject summary"),
        new("/form-text", Forms, "Text fields", "ri-input-field",
            "The text input in three sizes, its states, and the input group around it.",
            "input text email search password url placeholder disabled readonly form-input-sm form-input-lg value-display input-group input-affix unit prefix affix width narrow"),
        new("/form-textarea", Forms, "Text areas", "ri-text-block",
            "Multi-line input, and locking which way it can be dragged.",
            "textarea rows resize resize-none resize-both vertical lock multiline comment note maxlength counter"),
        new("/form-select", Forms, "Selects", "ri-dropdown-list",
            "The themed drop-down, options with content, and the list box.",
            "select form-select select-sm select-lg option optgroup legend selectedcontent picker caret multiple size listbox multiselect base-select transfer picklist dual list move"),
        new("/form-combo", Forms, "Combo fields", "ri-price-tag-3-line",
            "A searchable select, chips for several picks, a server-backed list and free entry.",
            "combo combobox form-combo autocomplete typeahead searchable select multiselect chips tags token input email recipients free entry server lazy load more paging debounce"),
        new("/form-choice", Forms, "Checks and switches", "ri-checkbox-line",
            "Checkboxes, radios and the switch — and which of the three to reach for.",
            "checkbox radio switch toggle form-check fieldset exclusive setting immediate saved disabled policy dependent hint choice"),
        new("/form-file", Forms, "File inputs", "ri-attachment-2",
            "File pickers and a dropzone that takes a real drop.",
            "file dropzone upload attachment drag drop file-list"),
        new("/form-numeric", Forms, "Ranges and steppers", "ri-number-1",
            "Ranges, steppers and date inputs — the controls whose internals are per-engine.",
            "range slider stepper number date time picker spinner appearance disabled"),

        new("/table", Data, "Tables", "ri-table-line",
            "One class on the table, plus sticky headers, sorting, pinned columns and a stacked layout.",
            "sticky sortable aria-sort zebra selected expandable tfoot totals numeric stacked col-num pinned frozen pin-start pin-end column width min-width shrink fit prose control actions"),
        new("/stat", Data, "Stats", "ri-numbers-line",
            "One number that matters, which way it moved, and what it is measured against.",
            "kpi number metric delta tile target unavailable dashboard"),
        new("/pager", Data, "Pagination", "ri-more-line",
            "Moving through a long result set, and the breadcrumb trail that says where it sits.",
            "pagination page-link breadcrumb trail button view state server paged"),

        new("/badge", Labels, "Badges", "ri-price-tag-3-line",
            "Semantic pills in three sizes, plus three categorical hues.",
            "pill status cyan orange teal small large severity"),
        new("/chip", Labels, "Chips", "ri-price-tag-line",
            "Removable filters and recipients, and the set they sit in.",
            "chip tag filter dismissible removable chip-set"),
        new("/avatar", Labels, "Avatars", "ri-user-line",
            "A person or an actor, the group, and the lockup that names them.",
            "avatar initials group person user identity lockup name email subtext"),

        new("/media", Content, "Media", "ri-image-line",
            "A bounded frame for an image you did not size, and a gallery of attachments.",
            "image figure gallery aspect ratio contain cover thumbnail attachment photo"),
        new("/prose", Content, "Prose", "ri-article-line",
            "Long-form text from a source the app does not control, and redacted values.",
            "prose article body measure foreign contain sanitized html redacted secret masked"),
        new("/markdown", Content, "Markdown", "ri-markdown-line",
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
            "progress bar spinner skeleton loading busy indeterminate placeholder block height surface"),
        new("/empty-state", Feedback, "Empty states", "ri-inbox-line",
            "Nothing to show, and waiting for something: the drawing, the icon, and the block itself.",
            "empty-state nothing found access failed retry pending illustration drawing icon state-art"),
        new("/state-art", Feedback, "State art", "ri-landscape-line",
            "The thirteen drawings that say which state a view is in, their accents and motion, and drawing your own.",
            "state-art illustration drawing sprite symbol use SednaStateArt empty icon accent motion animation live sedna-sprite sedna-art own custom svg"),
        new("/live-state", Feedback, "Live states", "ri-pulse-line",
            "The state of a connection, a pane of machine output, and work still running.",
            "live connection streaming health health-badge status stale output log follow tail activity running job cancel"),

        new("/modal", Overlays, "Modal", "ri-window-2-line",
            "A question that has to be answered before anything else happens.",
            "dialog backdrop confirm showModal sm lg scroll tall max-height footer ShowModalAsync deck first-run"),
        new("/drawer", Overlays, "Drawers and sheets", "ri-layout-right-line",
            "A panel from an edge, for a secondary flow that can be left.",
            "drawer sheet side panel filter edge scrim"),
        new("/palette", Overlays, "Command palette", "ri-command-line",
            "The Ctrl-K box: type, see matching commands, run one.",
            "palette command ctrl-k cmdk combobox listbox spotlight-search"),
        new("/spotlight", Overlays, "Spotlight", "ri-focus-3-line",
            "Dims the page except one element, for a tour or a first-run hint.",
            "spotlight tour onboarding hint highlight hole walkthrough follow lock placement coach mark"),

        new("/utility-layout", Utilities, "Layout", "ri-align-item-vertical-center-line",
            "Rows, columns, alignment, sizing and the hairline between them.",
            "sedna-row sedna-row-wrap sedna-col sedna-push flex align baseline between wrap fill shrink w-full divider hairline min-width"),
        new("/utility-spacing", Utilities, "Spacing", "ri-space",
            "The gap inside a row or column, and a top or bottom margin on one element.",
            "sedna-gap sedna-mt sedna-mb margin gap space rhythm zero reset"),
        new("/utility-text", Utilities, "Text", "ri-text",
            "Where a line sits, how loud it is, and what happens when it is too long.",
            "text-start text-center text-end text-muted text-mono text-nums tabular text-nowrap text-sm text-lg text-break text-clamp text-truncate ellipsis overflow"),
        new("/utility-visibility", Utilities, "Visibility", "ri-eye-off-line",
            "Screen-reader-only text, unavailable state, and what reaches the printer.",
            "visually-hidden sr-only screen reader focusable skip sedna-invisible sedna-busy sedna-disabled aria-busy aria-disabled sedna-no-print sedna-print-only paper"),
        new("/utility-scroll", Utilities, "Scrolling", "ri-scroll-to-bottom-line",
            "A region that scrolls on its own, with the library's scroll shadows.",
            "sedna-scroll sedna-scroll-x scroll-max overflow max-height shadow fade sm md lg horizontal wide table"),
        new("/utility-safe-area", Utilities, "Safe area", "ri-smartphone-line",
            "Holding your own bar clear of a home indicator, a notch or a rounded corner.",
            "sedna-safe-top sedna-safe-bottom sedna-safe-inline env safe-area-inset viewport-fit cover notch home indicator iphone island phone landscape"),

        new("/script", Start, "The script", "ri-code-s-slash-line",
            "Loading it, and the JavaScript behind toasts, confirmations, the clipboard and the delegated behaviours.",
            "sednaUi toast confirm copy clipboard menu tabs dropzone follow javascript boot theme default time zone cookie tz select refresh selectedcontent",
            Section: Reference),
        new("/interop", Start, "From C#", "ri-braces-line",
            "ISednaUi: toasts, confirmations, settings, the time zone and the modal, called from a component.",
            "ISednaUi interop C# csharp AddSednaUi ToastAsync ConfirmAsync ISednaSettings LoadSettingsAsync GetTimeZoneAsync ShowModalAsync prerender OnAfterRenderAsync",
            Section: Reference),
        // Not "/mcp": that route is the MCP endpoint itself, mapped in Program.cs.
        new("/mcp-server", Start, "MCP server", "ri-robot-2-line",
            "The read-only endpoint an agent points at, its six tools and four resources.",
            "mcp agent ai claude tool resource read-only streamable http rate limit since",
            Section: Reference),
    ];

    /// <summary>The group names, in the order the sidebar's rail shows them.</summary>
    public static IReadOnlyList<string> Groups { get; } =
        [Start, Frame, Structure, Actions, Forms, Data, Labels, Content, Feedback, Overlays, Utilities];

    /// <summary>
    /// The icon each group carries in the sidebar's area rail. <c>NavigationTests</c>
    /// fails on a group without one.
    /// </summary>
    public static IReadOnlyDictionary<string, string> GroupIcons { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [Start] = "ri-compass-3-line",
            [Frame] = "ri-layout-masonry-line",
            [Structure] = "ri-layout-grid-line",
            [Actions] = "ri-play-circle-line",
            [Forms] = "ri-file-list-3-line",
            [Data] = "ri-database-2-line",
            [Labels] = "ri-price-tag-3-line",
            [Content] = "ri-file-text-line",
            [Feedback] = "ri-notification-3-line",
            [Overlays] = "ri-stack-line",
            [Utilities] = "ri-tools-line",
        };

    /// <summary>
    /// Routes that used to exist, and where they went. The site is public and linked
    /// from the README, the package page and the docs, so a split page leaves its old
    /// address working rather than 404ing.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Moved { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["/feedback"] = "/toast",
            // One page carrying nine families, split into the Utilities group. It is
            // linked from the README and from apps' own CLAUDE.md blocks.
            ["/utility"] = "/utility-layout",
            // The Frame group was two pages carrying fourteen examples between them, with
            // the nav documented on one and most of its examples on the other. Four now.
            ["/layouts"] = "/frame",
            ["/overlay"] = "/drawer",
            ["/everything"] = "/",
        };

    /// <summary>The pages in one group, in registration order.</summary>
    public static IEnumerable<CataloguePage> InGroup(string group) =>
        All.Where(p => p.Group == group);

    /// <summary>The section names one group's panel carries, in first-seen order.</summary>
    public static IEnumerable<string> SectionsOf(string group) =>
        InGroup(group).Select(p => p.Section).OfType<string>().Distinct(StringComparer.Ordinal);
}

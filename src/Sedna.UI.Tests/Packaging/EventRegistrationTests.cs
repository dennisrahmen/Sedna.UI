using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// Every event a shipped script dispatches is bindable from Razor with its data, in every
/// Blazor hosting model, and cancelable exactly where the script dispatches it cancelable.
/// </summary>
/// <remarks>
/// <para>
/// Three lists have to agree, and they live in three languages: the events the scripts
/// dispatch, the names <c>Sedna.UI.lib.module.js</c> registers with Blazor, and the
/// <c>[EventHandler]</c> attributes on <c>EventHandlers</c>. A name missing from the
/// registration reaches a handler as an empty <c>EventArgs</c>; a name missing from the
/// attributes does not compile as <c>@on…</c> at all. <c>sedna-follow</c> was dispatched
/// and documented and in neither.
/// </para>
/// <para>
/// The initializer runs under whichever callback the hosting model calls — a standalone
/// WebAssembly app calls <c>afterWebAssemblyStarted</c> and not <c>afterStarted</c> — so
/// every one of them registers. A new event, or a new script that dispatches one, is
/// covered by this test without being named in it.
/// </para>
/// </remarks>
public class EventRegistrationTests
{
    private static readonly string Wwwroot = Path.Combine(Assets.ProjectDir, "wwwroot");

    private static string Initializer => File.ReadAllText(Path.Combine(Wwwroot, "Sedna.UI.lib.module.js"));

    // Every script an app's page runs: the main script and every module it imports on demand.
    private static IEnumerable<string> Scripts =>
        Directory.EnumerateFiles(Path.Combine(Wwwroot, "js"), "Sedna.UI*.js")
            .Where(p => !p.EndsWith("Sedna.UI.boot.js", StringComparison.Ordinal))
            .Select(p => StripComments(File.ReadAllText(p)));

    private static string StripComments(string js) =>
        Regex.Replace(Regex.Replace(js, @"/\*.*?\*/", "", RegexOptions.Singleline), @"(?m)^\s*//.*$", "");

    private static readonly Regex Dispatch = new(
        @"(?<call>CustomEvent|\bemit|\bsend)\((?:[^,()'""]+,\s*)?'(?<name>sedna-[a-z]+(?:-[a-z]+)*)'",
        RegexOptions.Compiled);

    /// <summary>The events dispatched, and whether any dispatch of each is cancelable.</summary>
    private static Dictionary<string, bool> Dispatched()
    {
        var found = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var source in Scripts)
        {
            foreach (Match m in Dispatch.Matches(source))
            {
                var open = source.IndexOf('(', m.Index);
                var args = Arguments(source, open);
                var cancelable = m.Groups["call"].Value == "CustomEvent"
                    ? Regex.IsMatch(args, @"cancelable:\s*true")
                    : Regex.IsMatch(args, @",\s*true\s*$");
                var name = m.Groups["name"].Value;
                found[name] = found.GetValueOrDefault(name) || cancelable;
            }
        }
        return found;
    }

    // The text between a call's parentheses, nested brackets and strings included.
    private static string Arguments(string source, int open)
    {
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            var c = source[i];
            if (c is '(' or '{' or '[') depth++;
            else if (c is ')' or '}' or ']') depth--;
            else if (c is '\'' or '"' or '`')
            {
                var end = i + 1;
                while (end < source.Length && source[end] != c) end += source[end] == '\\' ? 2 : 1;
                i = end;
            }
            if (depth == 0) return source[(open + 1)..i];
        }
        throw new InvalidOperationException("Unbalanced call in a shipped script.");
    }

    private static Dictionary<string, EventHandlerAttribute> Declared() =>
        typeof(EventHandlers).GetCustomAttributes<EventHandlerAttribute>()
            .ToDictionary(a => a.AttributeName[2..], StringComparer.Ordinal);

    private static List<string> Registered()
    {
        var list = Regex.Match(Initializer, @"const events = \[(?<list>[^\]]*)\];").Groups["list"].Value;
        return Regex.Matches(list, "'([a-z-]+)'").Select(m => m.Groups[1].Value).ToList();
    }

    [Fact]
    public void Every_dispatched_event_is_declared_and_registered_and_nothing_else_is()
    {
        var dispatched = Dispatched().Keys.Order(StringComparer.Ordinal).ToList();
        Assert.NotEmpty(dispatched);

        Assert.Equal(dispatched, Declared().Keys.Order(StringComparer.Ordinal));
        Assert.Equal(dispatched, Registered().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void An_event_can_be_prevented_from_Razor_exactly_where_the_script_lets_it()
    {
        var declared = Declared();
        foreach (var (name, cancelable) in Dispatched())
            Assert.True(declared[name].EnablePreventDefault == cancelable,
                $"{name} is dispatched {(cancelable ? "" : "not ")}cancelable, and its [EventHandler] "
                + $"{(declared[name].EnablePreventDefault ? "enables" : "does not enable")} :preventDefault.");
    }

    [Theory]
    [InlineData("afterWebStarted")]
    [InlineData("afterStarted")]
    [InlineData("afterServerStarted")]
    [InlineData("afterWebAssemblyStarted")]
    public void Every_hosting_model_registers_the_events(string callback)
    {
        var export = Regex.Match(Initializer, $@"export function {callback}\((?<arg>\w*)\)\s*\{{(?<body>[^}}]*)\}}");
        Assert.True(export.Success, $"The initializer does not export {callback}.");
        Assert.Contains($"register({export.Groups["arg"].Value})", export.Groups["body"].Value, StringComparison.Ordinal);
        Assert.NotEqual("", export.Groups["arg"].Value);
    }

    /// <summary>
    /// The tree's events by name, so a refactor that stops dispatching one — and so drops it
    /// from every list at once, which the agreement test above would accept — still fails.
    /// </summary>
    [Theory]
    [InlineData("sedna-tree-expand", typeof(SednaTreeEventArgs))]
    [InlineData("sedna-tree-collapse", typeof(SednaTreeEventArgs))]
    [InlineData("sedna-tree-select", typeof(SednaTreeSelectEventArgs))]
    [InlineData("sedna-tree-check", typeof(SednaTreeSelectEventArgs))]
    public void The_tree_s_events_are_dispatched_registered_and_bindable_with_their_data(string name, Type args)
    {
        Assert.True(Dispatched().ContainsKey(name), $"No shipped script dispatches {name}.");
        Assert.Contains(name, Registered());
        Assert.Equal(args, Declared()[name].EventArgsType);
    }

    [Fact]
    public void The_tree_s_event_data_reads_into_its_arguments()
    {
        // The initializer passes event.detail through as it is; these are the script's names.
        var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var expand = System.Text.Json.JsonSerializer.Deserialize<SednaTreeEventArgs>("""{"item":"2024","lazy":true}""", web)!;
        Assert.Equal("2024", expand.Item);
        Assert.True(expand.Lazy);

        var check = System.Text.Json.JsonSerializer.Deserialize<SednaTreeSelectEventArgs>(
            """{"item":"north","selected":true,"items":["north","north-inbound"]}""", web)!;
        Assert.Equal("north", check.Item);
        Assert.True(check.Selected);
        Assert.Equal(["north", "north-inbound"], check.Items);
    }

    [Fact]
    public void The_resize_event_carries_the_column_key_into_its_arguments()
    {
        Assert.True(Dispatched().ContainsKey("sedna-resize"), "No shipped script dispatches sedna-resize.");
        Assert.Equal(typeof(SednaResizeEventArgs), Declared()["sedna-resize"].EventArgsType);
        Assert.Matches(@"detail:\s*\{[^}]*\bkey:", Script("26-resize.js"));

        var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var column = System.Text.Json.JsonSerializer.Deserialize<SednaResizeEventArgs>(
            """{"property":"--col-width","value":240,"folded":false,"key":"customer"}""", web)!;
        Assert.Equal("--col-width", column.Property);
        Assert.Equal(240, column.Value);
        Assert.Equal("customer", column.Key);

        var card = System.Text.Json.JsonSerializer.Deserialize<SednaResizeEventArgs>(
            """{"property":"--card-height","value":null,"folded":false,"key":null}""", web)!;
        Assert.Null(card.Value);
        Assert.Null(card.Key);
    }

    [Fact]
    public void The_selection_event_carries_the_action_the_text_and_where_it_came_from()
    {
        Assert.True(Dispatched().ContainsKey("sedna-selection"), "No shipped script dispatches sedna-selection.");
        Assert.Contains("sedna-selection", Registered());
        Assert.Equal(typeof(SednaSelectionEventArgs), Declared()["sedna-selection"].EventArgsType);
        Assert.False(Declared()["sedna-selection"].EnablePreventDefault);

        var web = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);
        var quote = System.Text.Json.JsonSerializer.Deserialize<SednaSelectionEventArgs>(
            """{"action":"quote","text":"nightly sync","item":"m-1","source":"thread"}""", web)!;
        Assert.Equal("quote", quote.Action);
        Assert.Equal("nightly sync", quote.Text);
        Assert.Equal("m-1", quote.Item);
        Assert.Equal("thread", quote.Source);

        var across = System.Text.Json.JsonSerializer.Deserialize<SednaSelectionEventArgs>(
            """{"action":"copy","text":"two messages","item":null,"source":null}""", web)!;
        Assert.Null(across.Item);
        Assert.Null(across.Source);
    }

    private static string Script(string part) =>
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "js-parts", part));
}

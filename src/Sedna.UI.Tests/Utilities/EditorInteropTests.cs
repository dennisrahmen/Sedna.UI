using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Sedna.UI.Tests.TestSupport;

namespace Sedna.UI.Tests;

/// <summary>
/// <c>ISednaEditors</c> against the editor module it calls. Every member is one call to
/// <c>sednaUi.editor.invoke(id, method, args)</c>, and the module's <c>invoke</c> switch is the other
/// half of that contract — so these read the shipped module and hold the two together.
/// </summary>
/// <remarks>
/// The wire format is read with a real <see cref="JSRuntime"/>'s serializer options, the ones Blazor
/// uses, as in <see cref="GraphInteropTests"/>.
/// </remarks>
public class EditorInteropTests : BunitContext
{
    private const string Bridge = "sednaUi.editor.invoke";
    private const string Id = "reply";

    private static string EditorModule { get; } =
        File.ReadAllText(Path.Combine(Assets.ProjectDir, "wwwroot", "js", "Sedna.UI.editor.js"));

    private static JsonSerializerOptions Wire { get; } = new WireRuntime().Options;

    private ISednaEditors Editors() => new SednaEditors(JSInterop.JSRuntime);

    public sealed record EditorCall(string Name, Func<ISednaEditors, Task> Act, string Method, string Args)
    {
        public override string ToString() => Name;
    }

    public static IEnumerable<EditorCall> AllCalls =>
    [
        new("GetStateAsync", e => e.GetStateAsync(Id), "state", "[]"),
        new("SetHtmlAsync", e => e.SetHtmlAsync(Id, "<p>Hi</p>"), "set", """["<p>Hi</p>"]"""),
        // Null empties the document: the script reads "" as nothing, and null would be "null".
        new("SetHtmlAsync, emptied", e => e.SetHtmlAsync(Id, null), "set", """[""]"""),
        new("InsertTextAsync", e => e.InsertTextAsync(Id, " — Alex"), "insert", """[" — Alex"]"""),
        new("FocusAsync", e => e.FocusAsync(Id), "focus", "[]"),
        new("SetEnabledAsync", e => e.SetEnabledAsync(Id, false), "enable", "[false]"),
    ];

    public static TheoryData<EditorCall> Calls()
    {
        var data = new TheoryData<EditorCall>();
        foreach (var call in AllCalls) data.Add(call);
        return data;
    }

    [Theory]
    [MemberData(nameof(Calls), DisableDiscoveryEnumeration = true)]
    public async Task Every_member_is_one_call_to_the_bridge_with_the_id_the_method_and_its_arguments(EditorCall call)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        await call.Act(Editors());

        var invocation = Assert.Single(JSInterop.Invocations[Bridge]);
        Assert.Equal(3, invocation.Arguments.Count);
        Assert.Equal(Id, invocation.Arguments[0]);
        Assert.Equal(call.Method, invocation.Arguments[1]);
        var sent = JsonSerializer.SerializeToNode(invocation.Arguments[2], Wire);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(call.Args), sent),
            $"{call.Name} sent {sent?.ToJsonString()}, expected {call.Args}.");
        Assert.NotNull(invocation.CancellationToken);
    }

    [Fact]
    public void The_methods_the_wrapper_sends_are_exactly_the_cases_the_script_handles()
    {
        var body = Regex.Match(EditorModule, @"function invoke\(e, method, args\) \{(?<body>.*?)default:",
            RegexOptions.Singleline).Groups["body"].Value;
        Assert.False(string.IsNullOrEmpty(body), "invoke(e, method, args) is not in Sedna.UI.editor.js.");
        var handled = Regex.Matches(body, @"case '([a-z]+)':").Select(m => m.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var sent = AllCalls.Select(c => c.Method).ToHashSet(StringComparer.Ordinal);

        Assert.Empty(sent.Except(handled));
        Assert.Empty(handled.Except(sent));
    }

    [Fact]
    public async Task An_editor_that_is_not_there_answers_null()
    {
        JSInterop.Setup<SednaEditorState?>(Bridge, _ => true).SetResult(null);
        Assert.Null(await Editors().GetStateAsync("not-on-this-page"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task An_editor_id_is_required(string? id)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var editors = Editors();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => editors.GetStateAsync(id!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => editors.SetHtmlAsync(id!, "<p>x</p>"));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => editors.FocusAsync(id!));
        Assert.Empty(JSInterop.Invocations);
    }

    [Fact]
    public void What_the_script_answers_reads_back_as_state()
    {
        // stateOf(e) in 20-value.js, as the bridge resolves it.
        var state = JsonSerializer.Deserialize<SednaEditorState>(
            """{"html":"<p>Hi</p>","text":"Hi","length":2,"isEmpty":false}""", Wire);
        Assert.Equal(new SednaEditorState("<p>Hi</p>", "Hi", 2, false), state);
        Assert.Contains("isEmpty:", EditorModule, StringComparison.Ordinal);
    }

    [Fact]
    public void The_events_detail_reads_as_editor_event_args()
    {
        var e = JsonSerializer.Deserialize<SednaEditorEventArgs>(
            """{"html":"","text":"","length":0,"isEmpty":true}""", Wire)!;
        Assert.Equal((string.Empty, 0, true), (e.Html, e.Length, e.IsEmpty));
    }

    [Fact]
    public void Every_editor_event_the_module_dispatches_is_bindable()
    {
        var handlers = typeof(EventHandlers).GetCustomAttributes<EventHandlerAttribute>()
            .ToDictionary(a => a.AttributeName[2..], StringComparer.Ordinal);
        var dispatched = Regex.Matches(EditorModule, @"emit\('(?<name>sedna-editor-[a-z]+)'")
            .Select(m => m.Groups["name"].Value).Distinct().Order(StringComparer.Ordinal).ToList();

        Assert.Equal(
            handlers.Keys.Where(k => k.StartsWith("sedna-editor-", StringComparison.Ordinal)).Order(StringComparer.Ordinal),
            dispatched);
        Assert.All(dispatched, name => Assert.Equal(typeof(SednaEditorEventArgs), handlers[name].EventArgsType));
    }

    private sealed class WireRuntime : JSRuntime
    {
        public JsonSerializerOptions Options => JsonSerializerOptions;

        protected override void BeginInvokeJS(long taskId, string identifier, string? argsJson,
            JSCallResultType resultType, long targetInstanceId)
        {
        }

        protected override void EndInvokeDotNet(DotNetInvocationInfo invocationInfo, in DotNetInvocationResult invocationResult)
        {
        }
    }
}

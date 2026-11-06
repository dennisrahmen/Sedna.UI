using System.Globalization;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Sedna.UI.Catalogue.Mcp;

/// <summary>
/// Checks every tool call's arguments against the tool's own input schema before it runs, and
/// answers a mistake in words.
/// </summary>
/// <remarks>
/// <para>
/// An argument that does not bind — a string where the tool takes an array, a word where it
/// takes a number — fails inside the SDK, and the caller gets only "An error occurred
/// invoking …". A model reads that as the tool being down and retries the same call; a
/// consuming app's agent reported get_example and describe_class broken for exactly this. So
/// the schema the tool publishes is the contract, and it is checked here, once, for every
/// tool, including one added later.
/// </para>
/// <para>
/// One mistake is common enough to accept rather than refuse: a single string for a list of
/// them (<c>"ids": "Badge/Semantic"</c>). It becomes a list of one. A number written as a
/// string becomes the number. Everything else is an <see cref="McpException"/> naming the
/// argument, what it takes, and the arguments the tool has.
/// </para>
/// </remarks>
internal static class ArgumentGuard
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Wrap(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            // MatchedPrimitive is not set yet where a call-tool filter runs, so the tool is
            // looked up by name in the server's own collection.
            if (context.Params is { } call
                && (context.MatchedPrimitive as McpServerTool
                    ?? (context.Server.ServerOptions.ToolCollection is { } tools
                        && tools.TryGetPrimitive(call.Name, out var found) ? found : null)) is { } tool)
            {
                try
                {
                    call.Arguments = Check(tool.ProtocolTool.Name, tool.ProtocolTool.InputSchema, call.Arguments);
                }
                catch (McpException rejected)
                {
                    // Returned rather than thrown: from a filter the SDK prefixes a thrown
                    // message with "An error occurred invoking …", which reads as a broken tool.
                    return new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = rejected.Message }],
                    };
                }
            }
            return await next(context, cancellationToken);
        };

    internal static IDictionary<string, JsonElement> Check(
        string tool, JsonElement schema, IDictionary<string, JsonElement>? given)
    {
        var arguments = new Dictionary<string, JsonElement>(given ?? new Dictionary<string, JsonElement>(), StringComparer.Ordinal);
        var properties = schema.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object
            ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal)
            : new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var takes = properties.Count == 0 ? "no arguments" : string.Join(", ", properties.Keys);

        foreach (var name in arguments.Keys)
            if (!properties.ContainsKey(name))
                throw new McpException($"{tool} has no argument \"{name}\". It takes: {takes}.");

        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            foreach (var name in required.EnumerateArray().Select(r => r.GetString()).OfType<string>())
                if (!arguments.ContainsKey(name))
                    throw new McpException($"{tool} needs \"{name}\": {Describe(properties[name])}. It takes: {takes}.");

        foreach (var (name, value) in arguments.ToList())
            arguments[name] = Fit(tool, name, properties[name], value);

        return arguments;
    }

    private static JsonElement Fit(string tool, string name, JsonElement property, JsonElement value)
    {
        var types = Types(property);
        if (types.Count == 0 || types.Contains(KindOf(value))) return CheckItems(tool, name, property, value);

        if (types.Contains("array") && value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
            return CheckItems(tool, name, property, JsonSerializer.SerializeToElement(new[] { value }));

        if (value.ValueKind == JsonValueKind.String
            && (types.Contains("integer") || types.Contains("number"))
            && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            && (!types.Contains("integer") || Math.Abs(number % 1) < double.Epsilon || types.Contains("number")))
            return JsonSerializer.SerializeToElement(number);

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _) && types.Contains("integer"))
            return value;

        throw new McpException(
            $"{tool}: \"{name}\" takes {Describe(property)}, and was given {Article(KindOf(value))}.");
    }

    private static JsonElement CheckItems(string tool, string name, JsonElement property, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array || !property.TryGetProperty("items", out var items)) return value;
        var types = Types(items);
        if (types.Count == 0) return value;
        foreach (var item in value.EnumerateArray())
            if (!types.Contains(KindOf(item)))
                throw new McpException(
                    $"{tool}: every item of \"{name}\" is {Describe(items)}; one is {Article(KindOf(item))}.");
        return value;
    }

    private static List<string> Types(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type)) return [];
        return type.ValueKind switch
        {
            JsonValueKind.String => [type.GetString()!],
            JsonValueKind.Array => type.EnumerateArray().Select(t => t.GetString()).OfType<string>().ToList(),
            _ => [],
        };
    }

    private static string KindOf(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => "string",
        JsonValueKind.Number => value.TryGetInt64(out _) ? "integer" : "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        JsonValueKind.Array => "array",
        JsonValueKind.Object => "object",
        _ => "null",
    };

    private static string Describe(JsonElement property)
    {
        var types = Types(property).Where(t => t != "null").ToList();
        var what = types.Count == 0 ? "a value" : string.Join(" or ", types.Select(t => t == "array"
            ? "an array" + (property.TryGetProperty("items", out var i) && Types(i) is [var it] ? $" of {it}s" : "")
            : Article(t)));
        return property.TryGetProperty("description", out var d) && d.GetString() is { Length: > 0 } text
            ? $"{what} — {text.TrimEnd('.')}"
            : what;
    }

    private static string Article(string kind) => kind switch
    {
        "integer" or "object" or "array" => "an " + kind,
        "null" => "null",
        _ => "a " + kind,
    };
}

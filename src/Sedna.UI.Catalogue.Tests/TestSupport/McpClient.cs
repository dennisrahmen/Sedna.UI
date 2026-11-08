using System.Net.Http.Json;
using System.Text.Json;

namespace Sedna.UI.Catalogue.Tests.TestSupport;

/// <summary>
/// The MCP endpoint driven the way an agent drives it: JSON-RPC over HTTP, against the
/// running app.
/// </summary>
internal sealed class McpClient(HttpClient client)
{
    /// <summary>A tool's payload, parsed from the JSON text the SDK answers with.</summary>
    public async Task<JsonElement> Tool(string name, object arguments)
    {
        var result = await Rpc("tools/call", new { name, arguments });
        Assert.False(result.TryGetProperty("isError", out var error) && error.GetBoolean(),
            $"{name} failed: {result.GetProperty("content")[0].GetProperty("text").GetString()}");
        var text = result.GetProperty("content")[0].GetProperty("text").GetString()!;
        return JsonDocument.Parse(text).RootElement.Clone();
    }

    /// <summary>A rejected call's message, which the SDK propagates from McpException.</summary>
    public async Task<string> Failure(string name, object arguments)
    {
        var result = await Rpc("tools/call", new { name, arguments });
        Assert.True(result.TryGetProperty("isError", out var error) && error.GetBoolean(),
            $"{name} was expected to fail.");
        return result.GetProperty("content")[0].GetProperty("text").GetString()!;
    }

    public async Task<JsonElement> Rpc(string method, object? parameters = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters ?? new { } }),
        };
        request.Headers.Add("Accept", "application/json, text/event-stream");

        var response = await client.SendAsync(request);
        Assert.True(response.IsSuccessStatusCode, $"{method} returned {(int)response.StatusCode}.");

        var body = await response.Content.ReadAsStringAsync();
        foreach (var line in body.Split('\n'))
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
                return JsonDocument.Parse(line[6..]).RootElement.GetProperty("result").Clone();
        }

        return JsonDocument.Parse(body).RootElement.GetProperty("result").Clone();
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

public sealed record McpToolDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("inputSchema")] JsonElement InputSchema);

public sealed class McpRequestHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly SewerStudioToolRegistry _registry;

    public McpRequestHandler(SewerStudioToolRegistry registry) => _registry = registry;

    public async Task<JsonElement?> HandleAsync(JsonElement request)
    {
        if (!request.TryGetProperty("method", out var methodElement))
            return ErrorResponse(GetId(request), -32600, "Invalid Request");

        var method = methodElement.GetString();
        var hasId = request.TryGetProperty("id", out var id);

        return method switch
        {
            "initialize" when hasId => ResultResponse(id, CreateInitializeResult(request)),
            "initialized" => null,
            "notifications/initialized" => null,
            "ping" when hasId => ResultResponse(id, new { }),
            "tools/list" when hasId => ResultResponse(id, new { tools = _registry.ListTools() }),
            "tools/call" when hasId => await HandleToolCallAsync(id, request).ConfigureAwait(false),
            _ when hasId => ErrorResponse(id, -32601, $"Method not found: {method}"),
            _ => null
        };
    }

    private static object CreateInitializeResult(JsonElement request)
    {
        var protocolVersion = "2024-11-05";
        if (request.TryGetProperty("params", out var p)
            && p.TryGetProperty("protocolVersion", out var pv)
            && pv.ValueKind == JsonValueKind.String)
        {
            protocolVersion = pv.GetString() ?? protocolVersion;
        }

        return new
        {
            protocolVersion,
            capabilities = new { tools = new { } },
            serverInfo = new { name = "sewerstudio-mcp-server", version = "0.1.0" }
        };
    }

    private async Task<JsonElement> HandleToolCallAsync(JsonElement id, JsonElement request)
    {
        try
        {
            var parameters = request.GetProperty("params");
            var name = parameters.GetProperty("name").GetString()
                       ?? throw new InvalidOperationException("Tool name fehlt");
            var arguments = parameters.TryGetProperty("arguments", out var args)
                ? args
                : default;

            var result = await _registry.CallAsync(name, arguments).ConfigureAwait(false);
            var text = JsonSerializer.Serialize(result, JsonOptions);
            return ResultResponse(id, new
            {
                content = new[] { new { type = "text", text } },
                isError = false
            });
        }
        catch (Exception ex)
        {
            return ResultResponse(id, new
            {
                content = new[] { new { type = "text", text = ex.Message } },
                isError = true
            });
        }
    }

    private static JsonElement? GetId(JsonElement request)
        => request.TryGetProperty("id", out var id) ? id : null;

    private static JsonElement ResultResponse(JsonElement id, object result)
        => JsonSerializer.SerializeToElement(new
        {
            jsonrpc = "2.0",
            id,
            result
        }, JsonOptions);

    private static JsonElement ErrorResponse(JsonElement? id, int code, string message)
        => JsonSerializer.SerializeToElement(new
        {
            jsonrpc = "2.0",
            id,
            error = new { code, message }
        }, JsonOptions);
}

public sealed class StdioMcpServer(
    McpRequestHandler handler,
    TextReader input,
    TextWriter output,
    TextWriter error)
{
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        string? line;
        while ((line = await input.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(line);
                var response = await handler.HandleAsync(doc.RootElement).ConfigureAwait(false);
                if (response is null)
                    continue;

                await output.WriteLineAsync(response.Value.GetRawText()).ConfigureAwait(false);
                await output.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await error.WriteLineAsync($"MCP parse/dispatch error: {ex.Message}").ConfigureAwait(false);
            }
        }
    }
}

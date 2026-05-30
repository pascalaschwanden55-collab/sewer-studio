using System.Text.Json;

namespace AuswertungPro.Tools.SewerStudioMcpServer;

public sealed class SewerStudioToolRegistry
{
    private readonly SewerStudioMcpOptions _options;
    private readonly IReadOnlyList<McpToolDefinition> _tools;

    public SewerStudioToolRegistry(SewerStudioMcpOptions options)
    {
        _options = options;
        _tools =
        [
            new McpToolDefinition(
                "list_haltungen",
                "Lists SewerStudio holding folders read-only from the configured Haltungen root.",
                Schema(new
                {
                    type = "object",
                    properties = new
                    {
                        haltungen_root = new { type = "string", description = "Optional override; defaults to D:\\Haltungen or SEWERSTUDIO_HALTUNGEN_ROOT." }
                    },
                    additionalProperties = false
                })),
            new McpToolDefinition(
                "get_protocol_entries",
                "Parses the inspection PDF for one case_id via PdfProtocolTableParser and returns protocol entries.",
                Schema(new
                {
                    type = "object",
                    properties = new
                    {
                        case_id = new { type = "string", description = "Haltung/case id, e.g. 06.24341-35625." },
                        haltungen_root = new { type = "string", description = "Optional Haltungen root override." }
                    },
                    required = new[] { "case_id" },
                    additionalProperties = false
                })),
            new McpToolDefinition(
                "get_diagnostic_report",
                "Reads ProtocolPipelineDiagnostics output entries.csv and haltungen.json read-only.",
                Schema(new
                {
                    type = "object",
                    properties = new
                    {
                        diagnostics_output_dir = new { type = "string", description = "Optional diagnostics output directory override." },
                        max_entries = new { type = "integer", minimum = 1, maximum = 50000, description = "Maximum entries.csv rows to return. Default 5000." }
                    },
                    additionalProperties = false
                })),
            new McpToolDefinition(
                "list_training_samples",
                "Lists read-only training_samples.json records for one case_id.",
                Schema(new
                {
                    type = "object",
                    properties = new
                    {
                        case_id = new { type = "string", description = "Haltung/case id." },
                        knowledge_root = new { type = "string", description = "Optional KnowledgeRoot override. Default C:\\KI_BRAIN or SEWERSTUDIO_KNOWLEDGE_ROOT." }
                    },
                    required = new[] { "case_id" },
                    additionalProperties = false
                })),
            new McpToolDefinition(
                "live_control_health",
                "Checks whether the running SewerStudio app exposes the local Live-Control endpoint.",
                Schema(new
                {
                    type = "object",
                    properties = new
                    {
                        live_control_url = new { type = "string", description = "Optional override. Default http://127.0.0.1:8765/ or SEWERSTUDIO_LIVE_CONTROL_URL." }
                    },
                    additionalProperties = false
                })),
            new McpToolDefinition(
                "live_set_resource_brush",
                "Changes a WPF ResourceDictionary SolidColorBrush in the running app, e.g. AccentBrush -> gelb.",
                Schema(new
                {
                    type = "object",
                    properties = new
                    {
                        key = new { type = "string", description = "Resource key, e.g. AccentBrush, CardBrush, WarningBrush." },
                        color = new { type = "string", description = "Color name or hex, e.g. gelb, yellow, #F59E0B." },
                        live_control_url = new { type = "string", description = "Optional Live-Control URL override." }
                    },
                    required = new[] { "key", "color" },
                    additionalProperties = false
                })),
            new McpToolDefinition(
                "live_set_button_background",
                "Changes button backgrounds in the running app by button name/content/tooltip match. Empty target affects visible buttons up to max_matches.",
                Schema(new
                {
                    type = "object",
                    properties = new
                    {
                        target = new { type = "string", description = "Optional name/content/tooltip fragment, e.g. Speichern or StartButton." },
                        color = new { type = "string", description = "Color name or hex, e.g. gelb, yellow, #F59E0B." },
                        max_matches = new { type = "integer", minimum = 1, maximum = 500, description = "Safety cap. Default 50." },
                        live_control_url = new { type = "string", description = "Optional Live-Control URL override." }
                    },
                    required = new[] { "color" },
                    additionalProperties = false
                }))
        ];
    }

    public IReadOnlyList<McpToolDefinition> ListTools() => _tools;

    public async Task<object> CallAsync(string name, JsonElement arguments)
    {
        object result = name switch
        {
            "list_haltungen" => new
            {
                haltungen_root = GetString(arguments, "haltungen_root") ?? _options.HaltungenRoot,
                haltungen = HaltungenReader.List(GetString(arguments, "haltungen_root") ?? _options.HaltungenRoot)
            },
            "get_protocol_entries" => ProtocolEntriesReader.Read(
                GetString(arguments, "haltungen_root") ?? _options.HaltungenRoot,
                RequireString(arguments, "case_id")),
            "get_diagnostic_report" => DiagnosticReportReader.Read(
                GetString(arguments, "diagnostics_output_dir") ?? _options.DiagnosticsOutputDir,
                GetInt(arguments, "max_entries") ?? 5000),
            "list_training_samples" => new
            {
                case_id = RequireString(arguments, "case_id"),
                knowledge_root = GetString(arguments, "knowledge_root") ?? _options.KnowledgeRoot,
                samples = TrainingSamplesReader.List(
                    GetString(arguments, "knowledge_root") ?? _options.KnowledgeRoot,
                    RequireString(arguments, "case_id"))
            },
            "live_control_health" => await LiveControlClient.HealthAsync(
                GetString(arguments, "live_control_url") ?? _options.LiveControlUrl).ConfigureAwait(false),
            "live_set_resource_brush" => await LiveControlClient.SetResourceBrushAsync(
                GetString(arguments, "live_control_url") ?? _options.LiveControlUrl,
                RequireString(arguments, "key"),
                RequireString(arguments, "color")).ConfigureAwait(false),
            "live_set_button_background" => await LiveControlClient.SetButtonBackgroundAsync(
                GetString(arguments, "live_control_url") ?? _options.LiveControlUrl,
                GetString(arguments, "target"),
                RequireString(arguments, "color"),
                GetInt(arguments, "max_matches")).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unbekanntes Tool: {name}")
        };

        return result;
    }

    private static JsonElement Schema(object schema)
        => JsonSerializer.SerializeToElement(schema);

    private static string RequireString(JsonElement args, string name)
        => GetString(args, name) ?? throw new InvalidOperationException($"Argument '{name}' fehlt");

    private static string? GetString(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object
            || !args.TryGetProperty(name, out var value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? GetInt(JsonElement args, string name)
    {
        if (args.ValueKind != JsonValueKind.Object || !args.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
            return parsed;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out parsed))
            return parsed;
        return null;
    }
}

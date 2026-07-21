using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Training.Inventory;

/// <summary>Einziger JSON-Vertrag fuer Inventarberichte und spaetere Reparaturwerkzeuge.</summary>
public static class TrainingDataInventoryJson
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static byte[] SerializeToUtf8Bytes(TrainingDataInventoryReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        TrainingInventoryReportValidator.Validate(report);
        return JsonSerializer.SerializeToUtf8Bytes(report, Options);
    }

    public static TrainingDataInventoryReport Deserialize(ReadOnlySpan<byte> json)
    {
        var report = JsonSerializer.Deserialize<TrainingDataInventoryReport>(json, Options)
                     ?? throw new JsonException("Inventarbericht ist leer.");
        TrainingInventoryReportValidator.Validate(report);
        return report;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}

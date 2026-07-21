using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Application.Ai.Training.ExportPlans;

/// <summary>Ein kanonischer JSON-Schreiber fuer beide Exportwege.</summary>
public static class TrainingExportPlanSerializer
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public static byte[] SerializeManifest(TrainingExportPlan plan)
    {
        TrainingExportPlanValidator.Validate(plan);
        var json = JsonSerializer.SerializeToUtf8Bytes(plan, Options);
        if (json.Length > 0 && json[^1] == (byte)'\n')
            return json;
        var withNewline = new byte[json.Length + 1];
        json.CopyTo(withNewline, 0);
        withNewline[^1] = (byte)'\n';
        return withNewline;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = null,
            WriteIndented = true
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}

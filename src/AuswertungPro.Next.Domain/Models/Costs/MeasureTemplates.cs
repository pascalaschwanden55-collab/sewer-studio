using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Models.Costs;

public sealed class MeasureTemplates
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("templates")]
    public List<MeasureTemplate> Templates { get; set; } = new();
}

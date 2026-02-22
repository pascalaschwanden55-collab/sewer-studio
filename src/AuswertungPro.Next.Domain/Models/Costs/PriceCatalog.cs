using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Models.Costs;

public sealed class PriceCatalog
{
    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "CHF";

    [JsonPropertyName("items")]
    public List<PriceItem> Items { get; set; } = new();
}

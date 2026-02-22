using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Models.Costs;

public sealed class PriceItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("group")]
    public string Group { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("unit")]
    public string Unit { get; set; } = string.Empty;

    [JsonPropertyName("dn_min")]
    public int? DnMin { get; set; }

    [JsonPropertyName("dn_max")]
    public int? DnMax { get; set; }

    [JsonPropertyName("unit_price")]
    public decimal UnitPrice { get; set; }

    [JsonPropertyName("source")]
    public PriceSource? Source { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

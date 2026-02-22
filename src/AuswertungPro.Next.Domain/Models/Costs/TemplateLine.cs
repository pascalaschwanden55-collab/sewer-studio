using System.Text.Json;
using System.Text.Json.Serialization;

namespace AuswertungPro.Next.Domain.Models.Costs;

public sealed class TemplateLine
{
    [JsonPropertyName("group")]
    public string Group { get; set; } = string.Empty;

    [JsonPropertyName("item_ref")]
    public string ItemRef { get; set; } = string.Empty;

    [JsonPropertyName("qty")]
    public JsonElement Qty { get; set; }

    [JsonPropertyName("when")]
    public string? When { get; set; }
}

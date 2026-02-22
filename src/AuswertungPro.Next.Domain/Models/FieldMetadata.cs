using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Domain.Models;

public sealed class FieldMetadata
{
    public string FieldName { get; set; } = "";
    public FieldSource Source { get; set; } = FieldSource.Manual;
    public bool UserEdited { get; set; }
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public JsonObject? Conflict { get; set; }
}

using System.Text.Json.Nodes;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

public sealed class ImportMessage
{
    public string Level { get; set; } = "Info"; // Info|Warn|Error
    public string Message { get; set; } = "";
    public string Context { get; set; } = "";
}

public sealed class ImportStats
{
    public int Found { get; set; }
    public int CreatedRecords { get; set; }
    public int UpdatedRecords { get; set; }
    public int UpdatedFields { get; set; }
    public int Conflicts { get; set; }
    public int Errors { get; set; }
    public int Uncertain { get; set; }

    public List<JsonObject> ConflictDetails { get; } = new();
    public List<ImportMessage> Messages { get; } = new();
}

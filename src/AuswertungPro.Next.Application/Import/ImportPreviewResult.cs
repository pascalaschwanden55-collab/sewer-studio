namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ergebnis eines Dry-Run-Imports: zeigt was passieren wuerde.
/// </summary>
public sealed class ImportPreviewResult
{
    public int RecordsToCreate { get; set; }
    public int RecordsToUpdate { get; set; }
    public int ConflictsExpected { get; set; }
    public int MediaFilesFound { get; set; }
    public int MediaUnmatched { get; set; }
    public List<ImportPreviewChange> Changes { get; } = new();
    public List<ImportPreviewConflict> ConflictDetails { get; } = new();

    public static ImportPreviewResult FromLog(ImportRunLog log)
    {
        var result = new ImportPreviewResult
        {
            RecordsToCreate = log.TotalCreated,
            RecordsToUpdate = log.TotalUpdated,
            ConflictsExpected = log.TotalConflicts
        };

        foreach (var entry in log.EntriesList)
        {
            if (entry.Status is ImportLogStatus.Created or ImportLogStatus.Updated)
            {
                result.Changes.Add(new ImportPreviewChange
                {
                    RecordKey = entry.RecordKey ?? "",
                    Action = entry.Status == ImportLogStatus.Created ? "Neu" : "Update",
                    Field = entry.Field ?? "",
                    Detail = entry.Detail ?? ""
                });
            }
            else if (entry.Status == ImportLogStatus.Conflict)
            {
                result.ConflictDetails.Add(new ImportPreviewConflict
                {
                    RecordKey = entry.RecordKey ?? "",
                    Field = entry.Field ?? "",
                    Detail = entry.Detail ?? ""
                });
            }
        }

        return result;
    }
}

public sealed class ImportPreviewChange
{
    public string RecordKey { get; set; } = "";
    public string Action { get; set; } = "";
    public string Field { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class ImportPreviewConflict
{
    public string RecordKey { get; set; } = "";
    public string Field { get; set; } = "";
    public string Detail { get; set; } = "";
}

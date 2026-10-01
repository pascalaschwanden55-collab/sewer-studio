namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// UI-unabhaengiges Speicherformat des Training Centers. Die Eigenschaftsnamen
/// und der numerische Status entsprechen dem bestehenden JSON-Format.
/// </summary>
public sealed class TrainingCenterDocument
{
    public List<TrainingCaseDocument> Cases { get; set; } = new();
    public List<string> RootFolders { get; set; } = new();
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TrainingCaseDocument
{
    public string CaseId { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string VideoPath { get; set; } = "";
    public string ProtocolPath { get; set; } = "";
    public DateTime? InspectionDate { get; set; }
    public int Status { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}

public interface ITrainingCenterDocumentStore
{
    string StoreFilePath { get; }

    Task<TrainingCenterDocument> LoadAsync();

    Task SaveAsync(TrainingCenterDocument document);
}

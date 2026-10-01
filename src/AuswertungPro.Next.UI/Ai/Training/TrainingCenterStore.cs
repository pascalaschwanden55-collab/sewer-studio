using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class TrainingCenterStore
{
    private readonly ITrainingCenterDocumentStore _documents;

    public string StoreFilePath => _documents.StoreFilePath;

    public TrainingCenterStore(string? storeFilePath = null)
        : this(new TrainingCenterDocumentFileStore(storeFilePath))
    {
    }

    internal TrainingCenterStore(ITrainingCenterDocumentStore documents)
        => _documents = documents ?? throw new ArgumentNullException(nameof(documents));

    public async Task<TrainingCenterState> LoadAsync()
    {
        var document = await _documents.LoadAsync();
        return new TrainingCenterState
        {
            Cases = document.Cases.Select(ToUiCase).ToList(),
            RootFolders = document.RootFolders.ToList(),
            UpdatedUtc = document.UpdatedUtc
        };
    }

    /// <summary>
    /// Atomar speichern: temp-Datei + rename, mit Backup vor dem Schreiben.
    /// </summary>
    public async Task SaveAsync(TrainingCenterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.UpdatedUtc = DateTime.UtcNow;
        await _documents.SaveAsync(new TrainingCenterDocument
        {
            Cases = state.Cases.Select(ToDocumentCase).ToList(),
            RootFolders = state.RootFolders.ToList(),
            UpdatedUtc = state.UpdatedUtc
        });
    }

    private static TrainingCase ToUiCase(TrainingCaseDocument source)
        => new()
        {
            CaseId = source.CaseId,
            FolderPath = source.FolderPath,
            VideoPath = source.VideoPath,
            ProtocolPath = source.ProtocolPath,
            InspectionDate = source.InspectionDate,
            Status = (TrainingCaseStatus)source.Status,
            CreatedUtc = source.CreatedUtc
        };

    private static TrainingCaseDocument ToDocumentCase(TrainingCase source)
        => new()
        {
            CaseId = source.CaseId,
            FolderPath = source.FolderPath,
            VideoPath = source.VideoPath,
            ProtocolPath = source.ProtocolPath,
            InspectionDate = source.InspectionDate,
            Status = (int)source.Status,
            CreatedUtc = source.CreatedUtc
        };
}

namespace AuswertungPro.Next.Application.UseCases.PdfTrainingReview;

/// <summary>
/// Sichtbarer Hinweis zu einem Ordner, der beim PDF-Scan nicht gelesen wurde.
/// </summary>
public sealed record TrainingPdfFolderDiscoveryIssue(
    string Path,
    string ReasonCode,
    string Message);

/// <summary>
/// Eindeutige, stabil sortierte PDF-Pfade aus allen gewaehlten Ordnern.
/// </summary>
public sealed record TrainingPdfFolderDiscoveryResult(
    IReadOnlyList<string> PdfPaths,
    IReadOnlyList<TrainingPdfFolderDiscoveryIssue> Issues);

/// <summary>
/// Sucht PDF-Dateien rekursiv, ohne Verzeichnisverknuepfungen zu betreten.
/// </summary>
public interface ITrainingPdfFolderDiscoveryService
{
    TrainingPdfFolderDiscoveryResult Discover(
        IReadOnlyList<string> roots,
        CancellationToken cancellationToken);
}

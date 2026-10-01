using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

public sealed record ShaftDistributionProgress(
    int Processed,
    int Total,
    string? CurrentFile);

public sealed record ShaftDistributionRequest(
    Project Project,
    string DestinationFolder,
    IReadOnlyList<string>? PdfFiles = null,
    string? PdfSourceFolder = null,
    DistributionTargetConfig? DirectoryConfig = null,
    DistributionVariant Variant = DistributionVariant.Normal,
    IProgress<ShaftDistributionProgress>? Progress = null,
    IImportFileStagingSession? FileStaging = null);

public sealed record ShaftDistributionItem(
    bool Success,
    string Message,
    string SourcePdfPath,
    string? TargetPdfPath,
    string? ReadPdfPath,
    string? ShaftFolder);

public sealed record ShaftDistributionResult(
    IReadOnlyList<ShaftDistributionItem> Items,
    bool UsesPersistentProjectTransaction);

/// <summary>
/// Verteilt Schachtprotokolle. Bei einem Projektziel werden die Dateien nur
/// vorbereitet; Veroeffentlichung und Projekt-Speichern bleiben beim Workflow.
/// </summary>
public interface IShaftDistributionService
{
    ShaftDistributionResult Distribute(ShaftDistributionRequest request);
}

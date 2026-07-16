using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Kompatibilitätsfassade für bestehende Aufrufer. Datei- und PDF-Zugriffe liegen im
/// injizierbaren <see cref="IInspectionProtocolFileLocator"/>.
/// </summary>
public static class DataPageProtocolPathResolver
{
    private static readonly IInspectionProtocolFileLocator Default = new InspectionProtocolFileLocator();

    internal static IInspectionProtocolFileLocator CompatibilityService => Default;

    public static string? ResolveExistingPath(string? raw, string? projectPath)
        => CompatibilityService.ResolveExistingPath(raw, projectPath);

    public static string? FindProtocolPath(
        HaltungRecord record,
        string? resolvedLink,
        string? initialFolder,
        string? projectPath,
        string? storedFilesRaw)
        => CompatibilityService.FindProtocolPath(
            record,
            resolvedLink,
            initialFolder,
            projectPath,
            storedFilesRaw);

    public static List<string> ResolveOriginalPdfPaths(HaltungRecord record, string projectFolder)
        => CompatibilityService.ResolveOriginalPdfPaths(record, projectFolder);

    public static void AddResolvedPdf(List<string> paths, string? raw, string projectFolder)
        => CompatibilityService.AddResolvedPdf(paths, raw, projectFolder);

    public static void ResolveSchachtPdfPaths(
        SchachtRecord schacht,
        string projectFolder,
        List<string> paths)
        => CompatibilityService.ResolveSchachtPdfPaths(schacht, projectFolder, paths);

    public static IReadOnlyList<string> BuildHoldingTokens(HaltungRecord record)
        => ProtocolPathResolver.BuildHoldingTokens(record);

    public static string? PickBestPdfCandidate(
        IEnumerable<string> candidates,
        IReadOnlyList<string> holdingTokens)
        => PdfCandidateSelector.PickBest(candidates, holdingTokens);

    public static IReadOnlyList<string> ParseStoredPathList(string raw)
        => PdfCandidateSelector.ParseStoredPathList(raw);
}

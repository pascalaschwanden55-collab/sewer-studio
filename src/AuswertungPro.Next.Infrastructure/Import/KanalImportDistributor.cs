using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Neue Aufrufer erhalten
/// <see cref="IKanalImportDistributor"/> zentral als Instanz.
/// </summary>
public static class KanalImportDistributor
{
    private static readonly KanalImportDistributionService DefaultService = new();

    public sealed record Result(
        int VideosDistributed,
        int OriginalProtocolsDistributed,
        int Errors,
        IReadOnlyList<string> Messages);

    public static Result Distribute(
        Project project,
        string projectFolder,
        string archivedPdfDir,
        string sourceVideoDir,
        bool splitPdf = true,
        string? primaryProtocolPdf = null)
        => DefaultService.Distribute(
            project,
            projectFolder,
            archivedPdfDir,
            sourceVideoDir,
            splitPdf,
            primaryProtocolPdf);

    internal static string? SelectPrimaryProtocolPdf(string archivedPdfDir)
        => DefaultService.SelectPrimaryProtocolPdf(archivedPdfDir);

    internal static string ResolveDateStamp(HaltungRecord record)
        => DefaultService.ResolveDateStamp(record);

    internal static string UniquePath(string path)
        => DefaultService.UniquePath(path);
}

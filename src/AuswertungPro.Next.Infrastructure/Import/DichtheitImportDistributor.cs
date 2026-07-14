using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Neue Aufrufer erhalten
/// <see cref="IDichtheitImportDistributor"/> zentral als Instanz.
/// </summary>
public static class DichtheitImportDistributor
{
    private static readonly DichtheitImportDistributionService DefaultService = new();

    public sealed record Result(
        int Verteilt,
        int NichtZugeordnet,
        int Uebersprungen,
        IReadOnlyList<string> Messages);

    public static Result Distribute(
        Project project,
        string projectFolder,
        string sourceFolder,
        PdfKiSchiedsrichter? ki = null)
        => DefaultService.Distribute(project, projectFolder, sourceFolder, ki);

    internal static IReadOnlyList<string> FindeUnsichereKandidaten(string sourceFolder)
        => DefaultService.FindeUnsichereKandidaten(sourceFolder);

    internal static IReadOnlyList<string> FindeKandidaten(string sourceFolder)
        => DefaultService.FindeKandidaten(sourceFolder);
}

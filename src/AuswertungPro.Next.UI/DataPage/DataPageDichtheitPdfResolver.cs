using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Kompatibilitaetsfassade fuer bestehende Aufrufer. Die echte Ordnersuche liegt im
/// injizierbaren <see cref="IDichtheitProtocolFileLocator"/>.
/// </summary>
public static class DataPageDichtheitPdfResolver
{
    private static readonly IDichtheitProtocolFileLocator Default = new DichtheitProtocolFileLocator();

    internal static IDichtheitProtocolFileLocator CompatibilityService => Default;

    public static IReadOnlyList<string> Resolve(HaltungRecord? record, string? projectFolder)
        => Resolve(record, projectFolder, configuredRoot: null);

    public static IReadOnlyList<string> Resolve(
        HaltungRecord? record,
        string? projectFolder,
        string? configuredRoot)
        => CompatibilityService.FindPdfPaths(record, projectFolder, configuredRoot);
}

using System.Threading;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Xtf;

/// <summary>
/// Kompatibilitaetsfassade fuer die VSA/XTF-Medienpfadsuche.
/// </summary>
internal static class VsaMediaPathResolver
{
    private static IVsaMediaPathResolver _current = new VsaMediaPathFileResolver();

    internal static IVsaMediaPathResolver Current => Volatile.Read(ref _current);

    internal static string ResolvePhoto(string xtfPath, string? relativeFolder, string? fileName)
        => Current.ResolvePhoto(xtfPath, relativeFolder, fileName);

    internal static string ResolveVideo(string xtfPath, string? relativeFolder, string? fileName)
        => Current.ResolveVideo(xtfPath, relativeFolder, fileName);
}

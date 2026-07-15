using System.Threading;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Kompatibilitaetsfassade fuer die KINS-Gesamtprotokollsuche.
/// </summary>
public static class KinsGesamtprotokollLocator
{
    private static IKinsGesamtprotokollLocator _current = new KinsGesamtprotokollFileLocator();

    public static IKinsGesamtprotokollLocator Current => Volatile.Read(ref _current);

    public static void Use(IKinsGesamtprotokollLocator locator)
        => Volatile.Write(
            ref _current,
            locator ?? throw new ArgumentNullException(nameof(locator)));

    public static string? Finde(string sourceFolder)
        => Current.Finde(sourceFolder);
}

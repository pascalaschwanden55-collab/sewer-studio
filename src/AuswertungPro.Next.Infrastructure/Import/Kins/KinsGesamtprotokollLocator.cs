using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Kompatibilitaetsfassade fuer die KINS-Gesamtprotokollsuche.
/// </summary>
public static class KinsGesamtprotokollLocator
{
    private static readonly IKinsGesamtprotokollLocator Default =
        new KinsGesamtprotokollFileLocator();

    public static IKinsGesamtprotokollLocator Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IKinsGesamtprotokollLocator locator)
        => throw new NotSupportedException(
            "Die globale KINS-Gesamtprotokollsuche kann nicht mehr ausgetauscht werden. " +
            "IKinsGesamtprotokollLocator bitte per Konstruktor uebergeben.");

    public static string? Finde(string sourceFolder)
        => Current.Finde(sourceFolder);
}

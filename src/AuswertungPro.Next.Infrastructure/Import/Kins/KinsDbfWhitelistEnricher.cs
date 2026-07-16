using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Kompatibilitaetsfassade fuer die KINS-DBF-Anreicherung.
/// </summary>
public static class KinsDbfWhitelistEnricher
{
    private static readonly IKinsDbfWhitelistEnricher Default =
        new KinsDbfWhitelistEnrichmentService();

    public static IKinsDbfWhitelistEnricher Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IKinsDbfWhitelistEnricher enricher)
        => throw new NotSupportedException(
            "Die globale KINS-DBF-Anreicherung kann nicht mehr ausgetauscht werden. " +
            "IKinsDbfWhitelistEnricher bitte per Konstruktor uebergeben.");

    public static KinsDbfEnrichResult Apply(
        Project project,
        string sourceFolder,
        ImportRunContext? ctx = null)
    {
        var result = Current.Apply(project, sourceFolder, ctx);
        return new KinsDbfEnrichResult(
            result.HaltungsfelderGesetzt,
            result.SchaechteNeu,
            result.SchaechteAktualisiert,
            result.Messages);
    }
}

using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Kompatibilitaetsfassade fuer die KINS-Textanreicherung.
/// </summary>
public static class KinsDvdTextEnricher
{
    private static readonly IKinsDvdTextEnricher Default =
        new KinsDvdTextEnrichmentService();

    public static IKinsDvdTextEnricher Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IKinsDvdTextEnricher enricher)
        => throw new NotSupportedException(
            "Die globale KINS-Textanreicherung kann nicht mehr ausgetauscht werden. " +
            "IKinsDvdTextEnricher bitte per Konstruktor uebergeben.");

    public static KinsDvdTextEnrichResult Apply(Project project, string kiDvDatenPath)
    {
        var result = Current.Apply(project, kiDvDatenPath);
        return new KinsDvdTextEnrichResult(
            result.TimecodesGesetzt,
            result.LaengenGesetzt,
            result.DatumGesetzt,
            result.Messages);
    }
}

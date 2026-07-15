using System.Threading;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Kompatibilitaetsfassade fuer die KINS-Textanreicherung.
/// </summary>
public static class KinsDvdTextEnricher
{
    private static IKinsDvdTextEnricher _current = new KinsDvdTextEnrichmentService();

    public static IKinsDvdTextEnricher Current => Volatile.Read(ref _current);

    public static void Use(IKinsDvdTextEnricher enricher)
        => Volatile.Write(
            ref _current,
            enricher ?? throw new ArgumentNullException(nameof(enricher)));

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

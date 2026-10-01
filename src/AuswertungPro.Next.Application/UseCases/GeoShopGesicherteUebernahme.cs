using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.UseCases;

public static class GeoShopGesicherteUebernahme
{
    public static int WendeAn(GeoShopPlan plan, IReadOnlyList<GeoShopZiel> ziele, IGeoShopSicherung sicherung)
    {
        ArgumentNullException.ThrowIfNull(sicherung);
        if (plan.Positionen.Count == 0) return 0;
        if (plan.Positionen.Any(p => p.Ziel.Projekt is null))
            throw new InvalidOperationException("Für die gesicherte Übernahme müssen alle Ziele an ein Projekt gebunden sein.");
        foreach (var projekt in plan.Positionen.Select(p => p.Ziel.Projekt).OfType<Project>().Distinct())
            sicherung.Sichere(projekt); // Fehler stoppen zwingend vor der ersten Aenderung.
        return GeoShopAbgleichAnwender.WendeAn(plan, ziele);
    }
}

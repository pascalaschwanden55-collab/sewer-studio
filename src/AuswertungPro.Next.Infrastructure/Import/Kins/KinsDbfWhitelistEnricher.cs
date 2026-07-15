using System.Threading;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import.Kins;

/// <summary>
/// Kompatibilitaetsfassade fuer die KINS-DBF-Anreicherung.
/// </summary>
public static class KinsDbfWhitelistEnricher
{
    private static IKinsDbfWhitelistEnricher _current = new KinsDbfWhitelistEnrichmentService();

    public static IKinsDbfWhitelistEnricher Current => Volatile.Read(ref _current);

    public static void Use(IKinsDbfWhitelistEnricher enricher)
        => Volatile.Write(
            ref _current,
            enricher ?? throw new ArgumentNullException(nameof(enricher)));

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

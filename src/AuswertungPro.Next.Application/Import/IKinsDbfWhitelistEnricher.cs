using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ergebnis der Anreicherung aus den KINS-FoxPro-Stammdaten.
/// </summary>
public sealed record KinsDbfEnrichmentResult(
    int HaltungsfelderGesetzt,
    int SchaechteNeu,
    int SchaechteAktualisiert,
    IReadOnlyList<string> Messages);

/// <summary>
/// Reichert ein Projekt kontrolliert aus haltung.DBF und schacht.DBF an.
/// </summary>
public interface IKinsDbfWhitelistEnricher
{
    KinsDbfEnrichmentResult Apply(
        Project project,
        string sourceFolder,
        ImportRunContext? context = null);
}

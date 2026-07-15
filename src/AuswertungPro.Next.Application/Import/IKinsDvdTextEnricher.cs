using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Ergebnis der Anreicherung aus einer KINS kiDVDaten.txt.
/// </summary>
public sealed record KinsDvdTextEnrichmentResult(
    int TimecodesGesetzt,
    int LaengenGesetzt,
    int DatumGesetzt,
    IReadOnlyList<string> Messages);

/// <summary>
/// Reichert bereits importierte Haltungen mit KINS-Timecodes, Laenge und Datum an.
/// </summary>
public interface IKinsDvdTextEnricher
{
    KinsDvdTextEnrichmentResult Apply(Project project, string kiDvDatenPath);
}

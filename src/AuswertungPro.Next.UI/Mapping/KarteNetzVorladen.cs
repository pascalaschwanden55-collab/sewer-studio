using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using AuswertungPro.Next.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Waermt den Kartennetz-Cache im Hintergrund, sobald ein Projekt geladen ist — so ist die
/// Karte beim ersten Oeffnen bereits fertig ("beim Start in den RAM laden"). Kapselt das
/// Karten-Wissen (XTF-Pfad, Zustandsdaten), damit die ShellViewModel schlank bleibt (ein Aufruf).
/// </summary>
public static class KarteNetzVorladen
{
    public static void ImHintergrund(ServiceProvider services, Project project)
    {
        // Sparmodus auf schwachen Rechnern: NICHT vorladen — das Netz wird dann erst beim
        // Oeffnen der Karte gebaut. Kern (Import/Export/Verteilung/Bewertung) bleibt unberuehrt.
        var totalRamGb = System.GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024);
        if (!MapPreloadPolicy.ShouldPreload(totalRamGb))
            return;

        // Pfad + Zustandsdaten auf dem aufrufenden (UI-)Thread ermitteln (leichtgewichtig),
        // das schwere Bauen (XTF-Parse + Features) laeuft im Hintergrund.
        var xtfPath = services.KatasterXtfPaths.Resolve(
            services.Settings.AbwasserkatasterXtfPath,
            services.Settings.KantonUriXtfDirectory);
        var kondition = HaltungConditionProvider.Build(project.Data);

        // invertiert=true entspricht der Karte (EZ-Skala) -> gleicher Cache-Key, echtes Reuse.
        // Danach die Schaechte (Kreise) warmladen — separat, damit ein Fehler das Netz nicht stoert.
        Task.Run(() =>
        {
            services.NetworkFeatures.EnsureBuilt(xtfPath, kondition, invertiert: true);
            try
            {
                services.NetworkFeatures.EnsureManholesBuilt(xtfPath);
            }
            catch (System.Exception ex)
            {
                // Das Kanalnetz bleibt nutzbar. Der fehlgeschlagene optionale Schacht-Cache
                // muss aber im Tageslog sichtbar sein.
                services.Logger.LogWarning(
                    ex,
                    "Kartennetz-Vorladen: Schacht-Cache konnte fuer {XtfPath} nicht erstellt werden.",
                    xtfPath ?? "(kein XTF-Pfad)");
            }
        }).SafeFireAndForget(
            "KarteNetzVorladen",
            logger: services.Logger);
    }
}

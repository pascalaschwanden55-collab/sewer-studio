namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Entscheidet, ob das Kartennetz beim Projekt-Laden vorab in den RAM geladen wird.
/// Auf schwachen Rechnern (wenig RAM) NICHT vorladen — das Netz wird dann erst beim Oeffnen
/// der Karte gebaut (lazy). So bleibt der Kern (Import/Export/Verteilung/Bewertung) auch mit
/// 16 GB zuverlaessig; die KI ist ohnehin ein separater, opt-in Prozess und wird nicht geladen.
/// </summary>
public static class MapPreloadPolicy
{
    /// <summary>Ab dieser Gesamt-RAM-Groesse (GB) wird vorgeladen; darunter Sparmodus (lazy).</summary>
    public const double MinRamGbForPreload = 24.0;

    public static bool ShouldPreload(double totalRamGb) => totalRamGb >= MinRamGbForPreload;
}

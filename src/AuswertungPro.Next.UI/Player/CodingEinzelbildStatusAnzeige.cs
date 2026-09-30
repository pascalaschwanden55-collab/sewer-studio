using System.Windows.Media;
using AuswertungPro.Next.Application.UseCases.CodingEinzelbild;

namespace AuswertungPro.Next.UI.Player;

/// <summary>
/// Übersetzt die fachlichen Meldungen der Einzelbildanalyse in Statuszeile, Farbe und Puls.
/// Der Ablauf selbst liegt in <see cref="CodingEinzelbildAnalyseUseCase"/>.
/// </summary>
public static class CodingEinzelbildStatusAnzeige
{
    public static void Zeigen(
        CodingEinzelbildMeldung meldung,
        string activityText,
        Action<string, Color, string?, bool> setCodingAiState)
    {
        ArgumentNullException.ThrowIfNull(meldung);
        ArgumentNullException.ThrowIfNull(setCodingAiState);

        switch (meldung.Art)
        {
            case CodingEinzelbildMeldungsArt.BildWirdAufgenommen:
                setCodingAiState(activityText, PlayerStatusColors.Warning, "Schritt 1 von 4: Snapshot", true);
                break;
            case CodingEinzelbildMeldungsArt.BildNichtExtrahierbar:
                setCodingAiState("Frame nicht extrahierbar", PlayerStatusColors.Error, "Multi-Model", false);
                break;
            case CodingEinzelbildMeldungsArt.DateneinblendungErkannt:
                setCodingAiState(
                    "Dateneinblendung erkannt - übersprungen",
                    PlayerStatusColors.Muted,
                    "Warte auf sauberes Videobild...",
                    false);
                break;
            case CodingEinzelbildMeldungsArt.ModelleLaufen:
                setCodingAiState(activityText, PlayerStatusColors.Warning, "Schritt 2 von 4: YOLO und DINO", true);
                break;
            case CodingEinzelbildMeldungsArt.Modellfehler:
                setCodingAiState($"Fehler: {meldung.Fehler}", PlayerStatusColors.Error, "Multi-Model", false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(meldung), meldung.Art, "Unbekannte Meldung der Einzelbildanalyse.");
        }
    }
}

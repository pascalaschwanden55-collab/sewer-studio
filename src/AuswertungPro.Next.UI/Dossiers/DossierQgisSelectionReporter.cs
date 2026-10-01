using AuswertungPro.Next.UI.QgisBridge;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Dossiers;

/// <summary>
/// Verbindet die sichtbaren Dossierzeilen mit der bestehenden QGIS-Auswahl.
/// Die eigentliche Zoomlogik bleibt unveraendert in der QGIS-Bruecke.
/// </summary>
internal static class DossierQgisSelectionReporter
{
    public static void Report(DossierHoldingRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        QgisBridgeSelection.Set(row.Holding);
    }

    public static void Report(DossierShaftRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        QgisBridgeSelection.SetSchacht(row.Shaft);
    }
}

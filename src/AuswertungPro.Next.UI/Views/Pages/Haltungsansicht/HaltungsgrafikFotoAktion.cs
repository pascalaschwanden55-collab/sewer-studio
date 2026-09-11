using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;

/// <summary>Symbol und Klartext zeigen Ereignisfotos als schwebende Karte.</summary>
internal static class HaltungsgrafikFotoAktion
{
    internal static void Verbinde(FrameworkElement element, HaltungsgrafikMarke marke,
        Action<IReadOnlyList<string>>? oeffnen)
    {
        var hatFoto = marke.FotoPaths.Count > 0;
        // Der bisherige Callback bleibt fuer bestehende Aufrufer kompatibel, wird hier
        // aber bewusst nicht mehr ausgefuehrt: Fotos bleiben innerhalb der Vorschau.
        _ = oeffnen;
        element.ToolTip = marke.Tooltip + "\nKein Foto hinterlegt";
        if (!hatFoto) return;

        element.ToolTip = null;
        element.Cursor = Cursors.Hand;
        element.Focusable = true;
        element.DataContext = marke;
        AutomationProperties.SetName(element, "Fotovorschau: " + marke.Tooltip);
        AutomationProperties.SetHelpText(element,
            "Maus darüber oder Enter: Fotokarte. Mausrad: weiteres Foto. Escape: schliessen.");
        PhotoHoverPreviewBehavior.SetPhotoPathsSelector(element,
            item => (item as HaltungsgrafikMarke)?.FotoPaths);
        PhotoHoverPreviewBehavior.SetIsEnabled(element, true);
    }
}

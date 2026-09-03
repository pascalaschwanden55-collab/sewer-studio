using System.Windows;
using AuswertungPro.Next.Application.UseCases.Xtf;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Oeffnet <see cref="XtfExportVorschauWindow"/> modal ueber dem Hauptfenster.
/// </summary>
public sealed class XtfExportVorschauDialogService : IXtfExportVorschauDialog
{
    public bool Bestaetige(XtfExportVorschau vorschau)
        => Zeige(vorschau) == true;

    public void ZeigeFehler(XtfExportVorschau vorschau)
        => Zeige(vorschau with { IstFehler = true });

    private static bool? Zeige(XtfExportVorschau vorschau)
    {
        var fenster = new XtfExportVorschauWindow(vorschau);
        var besitzer = System.Windows.Application.Current?.MainWindow;
        if (besitzer is not null && !ReferenceEquals(besitzer, fenster) && besitzer.IsLoaded)
            fenster.Owner = besitzer;
        else
            fenster.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        return fenster.ShowDialog();
    }
}

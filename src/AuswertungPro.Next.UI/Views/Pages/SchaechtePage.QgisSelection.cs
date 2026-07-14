using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI;
using AuswertungPro.Next.UI.Behaviors;

namespace AuswertungPro.Next.UI.Views.Pages;

public partial class SchaechtePage
{
    // QGIS: Jeder Klick auf eine Schachtzeile meldet den Schacht erneut an die Bridge —
    // auch ein erneuter Klick auf die bereits ausgewaehlte Zeile laesst QGIS wieder aufleuchten.
    // (Selected-Changed feuert bei einem Klick auf die schon markierte Zeile NICHT, deshalb
    // reicht die ViewModel-Meldung in OnSelectedChanged fuer den Re-Klick nicht aus.)
    private void Grid_QgisReselectOnClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject clicked
            && VisualTreeSafe.FindAncestor<DataGridRow>(clicked)?.Item is SchachtRecord record)
        {
            var schachtnummer = record.GetFieldValue("Schachtnummer");
            if (!string.IsNullOrWhiteSpace(schachtnummer))
                QgisBridge.QgisBridgeSelection.SetSchacht(schachtnummer);
        }
    }
}

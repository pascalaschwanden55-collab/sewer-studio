using System.Windows.Controls;
using AuswertungPro.Next.UI.DataPage;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Fixwelle 2b, Runde 2: Laden und Speichern des Spaltenlayouts der Haltungstabelle.
///
/// Warum eine eigene Klasse: Die Teildateien der Seite <c>DataPage</c> stehen zusammen unter
/// einer Zeilengrenze (Waechter <c>MaintainabilityFitnessTests</c>). Dateinahe Logik gehoert
/// daneben, nicht hinein — die Seite reicht nur noch Raster, Steuerung und Einstellungen
/// herein.
/// </summary>
internal static class DataPageLayoutPersistenz
{
    /// <summary>
    /// Stellt das gespeicherte Layout wieder her. <paramref name="settings"/> ist null, wenn
    /// die Seite (noch) kein ViewModel hat — dann bleiben die Spalten in ihrer
    /// Aufbaureihenfolge, statt den Seitenaufbau abzubrechen.
    /// </summary>
    public static void Restore(
        DataGrid grid,
        DataGridColumnLayoutController controller,
        AppSettings? settings)
    {
        if (settings is null)
        {
            controller.Restore(grid.Columns, layout: null);
            return;
        }

        var layout = settings.DataPageLayout;

        // VOR dem Wiederherstellen: Ein bestehendes Layout traegt ueberall Left und die
        // Kopfbreite und wuerde die neuen Vorgaben zu Ausrichtung und Startbreite sonst
        // gleich wieder ueberschreiben (siehe ZahlenRechtsMigration).
        if (layout is not null)
            ZahlenRechtsMigration.WendeAn(layout, settings.Save);

        controller.Restore(grid.Columns, layout);
    }

    /// <summary>
    /// Schreibt das aktuelle Layout zurueck. Beim Entladen der Seite kann der DataContext
    /// bereits null sein — dann gibt es nichts zu speichern.
    /// </summary>
    public static void Save(
        DataGrid grid,
        DataGridColumnLayoutController controller,
        AppSettings? settings)
    {
        if (settings is null || controller.IsRestoring || grid.Columns.Count == 0)
            return;

        var layout = settings.DataPageLayout ?? new DataPageLayoutSettings();
        layout.Columns = controller.Capture(grid.Columns).Columns;
        settings.DataPageLayout = layout;
        settings.Save();
    }
}

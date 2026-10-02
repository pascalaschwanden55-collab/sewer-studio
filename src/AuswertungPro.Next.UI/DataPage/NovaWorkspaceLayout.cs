using System;
using System.Windows;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Gemeinsame Layoutregeln der Nova-Arbeitsflaechen von Haltungen und Schaechten (Liste, Uebersicht
/// rechts, Eingabefelder unten): Auf-/Zuklappen der Eingabefelder und das getrennte Ein-/Ausblenden
/// von Uebersicht und Eingabefeldern. Beide Controller (<see cref="DataPageNovaWorkspaceController"/>,
/// <see cref="SchaechteNovaWorkspaceController"/>) nutzen genau diese eine Stelle, damit die Regel
/// nicht an zwei Orten gepflegt wird (Deepscan 02.10.2026, B4).
/// </summary>
internal static class NovaWorkspaceLayout
{
    internal const double SideColStandard = 320;
    internal const double SideColMin = 240;
    internal const double SideColMax = 560;

    /// <summary>Die Zeilen, Spalten und Elemente der Arbeitsflaeche, auf die die Regeln wirken.</summary>
    internal sealed record Flaechen(
        FrameworkElement Uebersicht,
        GridSplitter SideSplitter,
        FrameworkElement FelderDrawer,
        ColumnDefinition SideSplitterCol,
        ColumnDefinition SideCol,
        GridSplitter DrawerSplitter,
        RowDefinition DrawerSplitterRow,
        RowDefinition DrawerRow);

    /// <summary>
    /// Auf- oder zugeklappte Eingabefelder: Zugeklappt bleibt nur die Kopfzeile stehen, die Zeile
    /// schrumpft auf Auto und die Trennlinie verschwindet. Aufgeklappt gelten Mindesthoehe,
    /// Trennlinie und die gespeicherte beziehungsweise berechnete Hoehe wieder.
    /// </summary>
    internal static void WendeSchubladeAn(Flaechen f, bool istOffen, Action wendeHoeheAn)
    {
        if (istOffen)
        {
            f.DrawerSplitter.Visibility = Visibility.Visible;
            f.DrawerSplitterRow.Height = new GridLength(DataPageWorkspaceLayoutPolicy.SplitterHoehe);
            f.DrawerRow.MinHeight = DataPageWorkspaceLayoutPolicy.MinDrawer;
            // Kam die Zeile aus dem zugeklappten Zustand (Auto), zuerst eine feste Hoehe geben.
            if (f.DrawerRow.Height.IsAuto)
                f.DrawerRow.Height = new GridLength(DataPageWorkspaceLayoutPolicy.MinDrawer);
            wendeHoeheAn();
        }
        else
        {
            f.DrawerSplitter.Visibility = Visibility.Collapsed;
            f.DrawerSplitterRow.Height = new GridLength(0);
            f.DrawerRow.MinHeight = 0;
            f.DrawerRow.Height = GridLength.Auto;
        }
    }

    /// <summary>
    /// Blendet Uebersicht und Eingabefelder getrennt ein oder aus. Die festen Spalten- und
    /// Zeilenmasse werden dabei mit auf 0 gesetzt, sonst bliebe eine Luecke. Getrennt gebraucht
    /// wird das von der Aufklapp-Liste: Dort bleibt die Uebersicht rechts, waehrend die
    /// Eingabefelder-Schublade verschwindet; das Formular steht in der aufgeklappten Zeile.
    /// </summary>
    /// <param name="gespeicherteBreite">Gespeicherte Breite der Seitenspalte; null = Standardbreite.</param>
    /// <param name="wendeSchubladeAn">Wendet den Auf-/Zuklapp-Zustand an (wenn die Eingabefelder sichtbar sind).</param>
    internal static void SetzeSichtbar(
        Flaechen f,
        bool uebersicht,
        bool eingabefelder,
        Func<double?> gespeicherteBreite,
        Action wendeSchubladeAn)
    {
        f.Uebersicht.Visibility = uebersicht ? Visibility.Visible : Visibility.Collapsed;
        f.SideSplitter.Visibility = uebersicht ? Visibility.Visible : Visibility.Collapsed;
        f.FelderDrawer.Visibility = eingabefelder ? Visibility.Visible : Visibility.Collapsed;

        if (uebersicht)
        {
            f.SideSplitterCol.Width = new GridLength(DataPageWorkspaceLayoutPolicy.SplitterHoehe);
            f.SideCol.MinWidth = SideColMin;
            f.SideCol.MaxWidth = SideColMax;
            var breite = gespeicherteBreite() is { } w
                ? Math.Clamp(w, SideColMin, SideColMax)
                : SideColStandard;
            f.SideCol.Width = new GridLength(breite);
        }
        else
        {
            f.SideSplitterCol.Width = new GridLength(0);
            f.SideCol.MinWidth = 0;
            f.SideCol.Width = new GridLength(0);
        }

        if (eingabefelder)
        {
            wendeSchubladeAn();
        }
        else
        {
            f.DrawerSplitter.Visibility = Visibility.Collapsed;
            f.DrawerSplitterRow.Height = new GridLength(0);
            f.DrawerRow.MinHeight = 0;
            f.DrawerRow.Height = new GridLength(0);
        }
    }
}

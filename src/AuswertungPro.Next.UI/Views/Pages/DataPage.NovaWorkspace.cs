using System;
using System.Windows;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.DataPage;
using AuswertungPro.Next.UI.ViewModels.Pages;

namespace AuswertungPro.Next.UI.Views.Pages;

/// <summary>
/// Nova-Etappe 1: Arbeitsflaeche der Haltungen-Seite mit Liste, Uebersicht rechts und
/// Eingabefeldern unten. Beide Trennlinien merken sich ihre Lage ueber
/// SplitterPersistenceBehavior (ViewKey "DataPage"); die Standardhoehe der Eingabefelder
/// kommt aus DataPageWorkspaceLayoutPolicy (sieben Zeilen bleiben sichtbar).
/// </summary>
public partial class DataPage
{
    private const string NovaViewKey = "DataPage";
    private const string DrawerSplitterKey = "HaltungenEingabefelder";
    private const string SideSplitterKey = "HaltungenUebersicht";
    private const double SideColStandard = 320;
    private const double SideColMin = 240;
    private const double SideColMax = 560;
    private const double TabellenkopfHoehe = 32;

    /// <summary>Verdrahtet Uebersicht und Eingabefelder mit dem ViewModel und waehlt die Standardansicht.</summary>
    private void InitNovaWorkspace(DataPageViewModel vm)
    {
        Uebersicht.BeobachtungenRequested = record => RouteHaltungsansichtAction("beobachtungen", record);
        AktualisiereFelderDrawer();

        // Standardansicht: Nova-Arbeitsflaeche; die bisherige Haltungsansicht bleibt ueber den Toggle
        // erreichbar und wird Standard, wenn die Einstellung aus ist.
        HaltungsansichtToggle.IsChecked = !vm.Settings.ShowHaltungenNovaLayout;
        ApplyHaltungsansichtSichtbarkeit();
    }

    /// <summary>Eingabefelder neu aus der gewaehlten Haltung aufbauen (Auswahlwechsel, externe Feldaenderung).</summary>
    private void AktualisiereFelderDrawer()
    {
        if (DataContext is not DataPageViewModel vm)
            return;

        FelderDrawer.Titel = vm.Selected?.GetFieldValue("Haltungsname") ?? string.Empty;
        FelderDrawer.Groups = vm.Selected is { } record ? BuildHaltungRecordDetailsForAnsicht(record) : null;
    }

    /// <summary>Hoehe der Eingabefelder aus Flaeche, Zeilenhoehe und gespeicherter Lage der Trennlinie.</summary>
    private void ApplyDrawerHeight()
    {
        if (DataContext is not DataPageViewModel vm || GridHost.ActualHeight <= 0 || !IstNovaArbeitsflaecheSichtbar)
            return;

        var gespeichert = SplitterPersistenceCore.TryGetStored(NovaViewKey, DrawerSplitterKey, out var s) ? s : (double?)null;
        var hoehe = DataPageWorkspaceLayoutPolicy.DrawerHeight(GridHost.ActualHeight, vm.GridMinRowHeight, TabellenkopfHoehe, gespeichert);
        DrawerRow.Height = new GridLength(hoehe);
    }

    private bool IstNovaArbeitsflaecheSichtbar => FelderDrawer.Visibility == Visibility.Visible;

    /// <summary>
    /// Blendet Uebersicht, Eingabefelder und beide Trennlinien ein oder aus. Die festen Spalten-
    /// und Zeilenmasse werden dabei mit auf 0 gesetzt, sonst bliebe bei der Haltungsansicht eine Luecke.
    /// </summary>
    private void SetNovaWorkspaceVisible(bool sichtbar)
    {
        // Der Toggle kann schon waehrend InitializeComponent feuern, bevor die Flaeche existiert.
        if (FelderDrawer is null || Uebersicht is null || SideSplitter is null || DrawerSplitter is null)
            return;

        var v = sichtbar ? Visibility.Visible : Visibility.Collapsed;
        Uebersicht.Visibility = v;
        SideSplitter.Visibility = v;
        DrawerSplitter.Visibility = v;
        FelderDrawer.Visibility = v;

        if (sichtbar)
        {
            SideSplitterCol.Width = new GridLength(DataPageWorkspaceLayoutPolicy.SplitterHoehe);
            SideCol.MinWidth = SideColMin;
            SideCol.MaxWidth = SideColMax;
            var breite = SplitterPersistenceCore.TryGetStored(NovaViewKey, SideSplitterKey, out var w)
                ? Math.Clamp(w, SideColMin, SideColMax)
                : SideColStandard;
            SideCol.Width = new GridLength(breite);
            DrawerSplitterRow.Height = new GridLength(DataPageWorkspaceLayoutPolicy.SplitterHoehe);
            DrawerRow.MinHeight = DataPageWorkspaceLayoutPolicy.MinDrawer;
            ApplyDrawerHeight();
        }
        else
        {
            SideSplitterCol.Width = new GridLength(0);
            SideCol.MinWidth = 0;
            SideCol.Width = new GridLength(0);
            DrawerSplitterRow.Height = new GridLength(0);
            DrawerRow.MinHeight = 0;
            DrawerRow.Height = new GridLength(0);
        }
    }
}

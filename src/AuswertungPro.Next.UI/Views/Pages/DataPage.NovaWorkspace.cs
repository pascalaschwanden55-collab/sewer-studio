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
/// kommt aus DataPageWorkspaceLayoutPolicy (sieben Zeilen bleiben sichtbar). Die
/// Eingabefelder bleiben ueber DataPageDetailLiveSync mit dem Datensatz gleich, weil Tabelle
/// und Formular gemeinsam sichtbar sind (Nachpruefung W01).
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

    private DataPageDetailLiveSync? _felderSync;

    /// <summary>
    /// Einmalige Verdrahtung im Konstruktor, unabhaengig vom ViewModel: Der Doppelklick in der
    /// Uebersicht und das Auf-/Zuklappen der Eingabefelder (Nachpruefung W03) muessen auch
    /// funktionieren, bevor ein DataContext gesetzt ist.
    /// </summary>
    private void VerdrahteNovaWorkspace()
    {
        Uebersicht.BeobachtungenRequested = record => RouteHaltungsansichtAction("beobachtungen", record);
        FelderDrawer.IsOpenChanged += (_, _) => ApplyDrawerOpenState();
    }

    /// <summary>Verbindet Uebersicht und Eingabefelder mit dem ViewModel und waehlt die Standardansicht.</summary>
    private void InitNovaWorkspace(DataPageViewModel vm)
    {
        AktualisiereFelderDrawer();

        // Standardansicht: Nova-Arbeitsflaeche; die bisherige Haltungsansicht bleibt ueber den Toggle
        // erreichbar und wird Standard, wenn die Einstellung aus ist.
        HaltungsansichtToggle.IsChecked = !vm.Settings.ShowHaltungenNovaLayout;
        ApplyHaltungsansichtSichtbarkeit();
    }

    /// <summary>
    /// Eingabefelder neu aus der gewaehlten Haltung aufbauen (Auswahlwechsel, externe Feldaenderung)
    /// und den Live-Abgleich mit genau diesem Datensatz anschliessen.
    /// </summary>
    private void AktualisiereFelderDrawer()
    {
        _felderSync?.Dispose();
        _felderSync = null;

        if (DataContext is not DataPageViewModel vm || vm.Selected is not { } record)
        {
            FelderDrawer.Titel = string.Empty;
            FelderDrawer.Groups = null;
            return;
        }

        var gruppen = BuildHaltungRecordDetailsForAnsicht(record);
        FelderDrawer.Titel = record.GetFieldValue("Haltungsname");
        FelderDrawer.Groups = gruppen;
        _felderSync = new DataPageDetailLiveSync(record, gruppen);
    }

    /// <summary>Hoehe der Eingabefelder aus Flaeche, Zeilenhoehe und gespeicherter Lage der Trennlinie.</summary>
    private void ApplyDrawerHeight()
    {
        if (DataContext is not DataPageViewModel vm || GridHost.ActualHeight <= 0
            || !IstNovaArbeitsflaecheSichtbar || !FelderDrawer.IsOpen)
            return;

        var gespeichert = SplitterPersistenceCore.TryGetStored(NovaViewKey, DrawerSplitterKey, out var s) ? s : (double?)null;
        var hoehe = DataPageWorkspaceLayoutPolicy.DrawerHeight(GridHost.ActualHeight, vm.GridMinRowHeight, TabellenkopfHoehe, gespeichert);
        DrawerRow.Height = new GridLength(hoehe);
    }

    private bool IstNovaArbeitsflaecheSichtbar => FelderDrawer.Visibility == Visibility.Visible;

    /// <summary>
    /// Auf- oder zugeklappte Eingabefelder: Zugeklappt bleibt nur die Kopfzeile stehen, die Zeile
    /// schrumpft auf Auto und die Trennlinie verschwindet. Aufgeklappt gelten Mindesthoehe,
    /// Trennlinie und die gespeicherte beziehungsweise berechnete Hoehe wieder.
    /// </summary>
    private void ApplyDrawerOpenState()
    {
        if (!IstNovaArbeitsflaecheSichtbar)
            return;

        if (FelderDrawer.IsOpen)
        {
            DrawerSplitter.Visibility = Visibility.Visible;
            DrawerSplitterRow.Height = new GridLength(DataPageWorkspaceLayoutPolicy.SplitterHoehe);
            DrawerRow.MinHeight = DataPageWorkspaceLayoutPolicy.MinDrawer;
            // Kam die Zeile aus dem zugeklappten Zustand (Auto), zuerst eine feste Hoehe geben.
            if (DrawerRow.Height.IsAuto)
                DrawerRow.Height = new GridLength(DataPageWorkspaceLayoutPolicy.MinDrawer);
            ApplyDrawerHeight();
        }
        else
        {
            DrawerSplitter.Visibility = Visibility.Collapsed;
            DrawerSplitterRow.Height = new GridLength(0);
            DrawerRow.MinHeight = 0;
            DrawerRow.Height = GridLength.Auto;
        }
    }

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
            ApplyDrawerOpenState();
        }
        else
        {
            DrawerSplitter.Visibility = Visibility.Collapsed;
            SideSplitterCol.Width = new GridLength(0);
            SideCol.MinWidth = 0;
            SideCol.Width = new GridLength(0);
            DrawerSplitterRow.Height = new GridLength(0);
            DrawerRow.MinHeight = 0;
            DrawerRow.Height = new GridLength(0);
        }
    }
}

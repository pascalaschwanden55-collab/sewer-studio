using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 2: Arbeitsflaeche der Schaechte-Seite mit Liste, Schachtansicht rechts und
/// Eingabefeldern unten. Kopie von <see cref="DataPageNovaWorkspaceController"/> (Hoehenregel,
/// Trennlinien, Auf-/Zuklappen). Am Schacht wird die Zustandsklasse nie berechnet; der
/// Formular-Live-Abgleich (<see cref="DataPageDetailLiveSync"/>, Fix-Runde 1) laeuft trotzdem
/// genauso wie bei den Haltungen mit, ueber dieselbe generische Ueberladung auf
/// <see cref="SchachtRecord"/>. Ein Feldwert wird dadurch ohne Neuaufbau der ganzen Drawer-Gruppen
/// nachgezogen (kein Fokusverlust bei jedem Tastendruck).
/// </summary>
public sealed class SchaechteNovaWorkspaceController
{
    private const string NovaViewKey = "SchaechtePage";
    private const string DrawerSplitterKey = "SchaechteEingabefelder";
    private const string SideSplitterKey = "SchaechteSchachtansicht";
    private const double SideColStandard = 320;
    private const double SideColMin = 240;
    private const double SideColMax = 560;
    // Spaltenkopf plus waagrechte Bildlaufleiste der Tabelle (wie DataPage).
    private const double TabellenkopfHoehe = 54;

    /// <summary>Die benannten Elemente der Arbeitsflaeche aus SchaechtePage.xaml.</summary>
    public sealed record Elemente(
        Grid GridHost,
        RowDefinition DrawerSplitterRow,
        RowDefinition DrawerRow,
        ColumnDefinition SideSplitterCol,
        ColumnDefinition SideCol,
        GridSplitter SideSplitter,
        GridSplitter DrawerSplitter,
        SchachtUebersichtPanel Uebersicht,
        HaltungFelderDrawer FelderDrawer);

    private readonly Elemente _e;
    private readonly Func<SchaechtePageViewModel?> _vm;
    private readonly Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>> _detailBuilder;
    private readonly Action<SchachtRecord> _pdf;

    // Zuklappen wegen Platzmangel ist automatisch; oeffnet der Benutzer danach selbst,
    // bleibt seine Wahl bis zum naechsten Seitenaufbau bestehen.
    private bool _drawerAutomatischZugeklappt;
    private bool _drawerVomBenutzerGeoeffnet;
    private DataPageDetailLiveSync? _felderSync;

    public SchaechteNovaWorkspaceController(
        Elemente elemente,
        Func<SchaechtePageViewModel?> viewModel,
        Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>> detailBuilder,
        Action<SchachtRecord> pdf)
    {
        _e = elemente ?? throw new ArgumentNullException(nameof(elemente));
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _detailBuilder = detailBuilder ?? throw new ArgumentNullException(nameof(detailBuilder));
        _pdf = pdf ?? throw new ArgumentNullException(nameof(pdf));
    }

    /// <summary>Einmalige Verdrahtung, unabhaengig vom ViewModel.</summary>
    public void Verdrahte()
    {
        _e.Uebersicht.PdfRequested = _pdf;
        _e.FelderDrawer.IsOpenChanged += (_, _) =>
        {
            // Nur beim Oeffnen auswerten: Das automatische Zuklappen setzt das Flag selbst und
            // darf es hier nicht gleich wieder loeschen.
            if (_e.FelderDrawer.IsOpen)
            {
                if (_drawerAutomatischZugeklappt)
                    _drawerVomBenutzerGeoeffnet = true;
                _drawerAutomatischZugeklappt = false;
            }
            ApplyDrawerOpenState();
        };
        // SplitterPersistenceBehavior schreibt beim Laden der Trennlinie die gespeicherte Hoehe in die
        // Zeile, auch wenn die Eingabefelder gerade zugeklappt sind. Danach den Zustand erneut anwenden.
        _e.DrawerSplitter.Loaded += (_, _) => ApplyDrawerOpenState();
        _e.FelderDrawer.IsTallChanged += (_, _) =>
        {
            _e.FelderDrawer.IsOpen = true;
            ApplyDrawerHeight();
        };
    }

    /// <summary>
    /// Reicht den aktiven Codekatalog und alle Haltungen des Projekts an die Schachtansicht
    /// weiter. Die Schachtgrafik braucht den Katalog fuer die Klartexte der Schaeden und die
    /// Haltungen, um die an diesen Schacht angeschlossenen Zu- und Ablaeufe zu finden — die Seite
    /// selbst holt dafuer keinen Dienst.
    /// </summary>
    public void VerbindeKatalog()
    {
        if (_vm() is not { } vm)
            return;
        _e.Uebersicht.Catalog = vm.CodeCatalog;
        _e.Uebersicht.Haltungen = vm.Project.Data;
    }

    /// <summary>
    /// Eingabefelder neu aus dem gewaehlten Schacht aufbauen (Auswahlwechsel, Wechsel der alten
    /// Schachtansicht) und den Live-Abgleich mit genau diesem Datensatz anschliessen. Eine reine
    /// Feldaenderung am bereits angezeigten Schacht rebuildet die Gruppen NICHT erneut (das wuerde
    /// den Bearbeitungsfokus verlieren) - dafuer sorgt <see cref="_felderSync"/>. Ohne Auswahl
    /// bleibt der Drawer leer.
    /// </summary>
    public void AktualisiereFelderDrawer()
    {
        _felderSync?.Dispose();
        _felderSync = null;
        _e.FelderDrawer.Hinweis = string.Empty;

        if (_vm() is not { } vm || vm.Selected is not { } record)
        {
            _e.FelderDrawer.Titel = string.Empty;
            _e.FelderDrawer.Groups = null;
            return;
        }

        var gruppen = _detailBuilder(record);
        _e.FelderDrawer.Titel = record.GetFieldValue("Schachtnummer");
        _e.FelderDrawer.Groups = gruppen;
        _felderSync = new DataPageDetailLiveSync(record, record.GetFieldValue, gruppen);
    }

    /// <summary>
    /// Leert die Eingabefelder und entsorgt ihren Live-Abgleich. Gebraucht, sobald die Seite die
    /// Aufklapp-Liste zeigt: Das Formular steht dann in der aufgeklappten Zeile, und ein zweiter
    /// Live-Sync auf denselben Datensatz waere verschwendete Arbeit und ein zweiter Schreibweg
    /// auf dasselbe Formular (Fix-Runde 1 zu Task 6, dasselbe Muster wie
    /// <see cref="DataPageNovaWorkspaceController.LeereFelderDrawer"/>).
    /// </summary>
    public void LeereFelderDrawer()
    {
        _felderSync?.Dispose();
        _felderSync = null;
        _e.FelderDrawer.Hinweis = string.Empty;
        _e.FelderDrawer.Titel = string.Empty;
        _e.FelderDrawer.Groups = null;
    }

    private bool IstSichtbar => _e.FelderDrawer.Visibility == Visibility.Visible;

    /// <summary>
    /// Hoehe der Eingabefelder aus Flaeche, Zeilenhoehe und gespeicherter Lage der Trennlinie.
    /// Dieselbe <see cref="DataPageWorkspaceLayoutPolicy.Berechne"/> wie bei den Haltungen.
    /// </summary>
    public void ApplyDrawerHeight()
    {
        if (_vm() is not { } vm || _e.GridHost.ActualHeight <= 0 || !IstSichtbar || !_e.FelderDrawer.IsOpen)
            return;

        // GridHost traegt hier keine Filterzeile mehr (die Spaltenansicht-Chips liegen ausserhalb),
        // die ganze Hoehe zaehlt deshalb zur Flaeche.
        var flaeche = _e.GridHost.ActualHeight;
        double? gespeichert = _e.FelderDrawer.IsTall
            ? Math.Round(flaeche * 0.6)
            : SplitterPersistenceCore.TryGetStored(NovaViewKey, DrawerSplitterKey, out var s) ? s : null;
        var layout = DataPageWorkspaceLayoutPolicy.Berechne(flaeche, vm.GridMinRowHeight, TabellenkopfHoehe, gespeichert);
        if (layout.Zugeklappt && !_drawerVomBenutzerGeoeffnet)
        {
            // Platz reicht nicht fuer sieben Zeilen: Eingabefelder zuklappen, Liste hat Vorrang.
            _drawerAutomatischZugeklappt = true;
            _e.FelderDrawer.IsOpen = false;
            return;
        }
        _e.DrawerRow.Height = new GridLength(layout.Hoehe);
    }

    /// <summary>
    /// Auf- oder zugeklappte Eingabefelder: Zugeklappt bleibt nur die Kopfzeile stehen, die Zeile
    /// schrumpft auf Auto und die Trennlinie verschwindet. Aufgeklappt gelten Mindesthoehe,
    /// Trennlinie und die gespeicherte beziehungsweise berechnete Hoehe wieder.
    /// </summary>
    private void ApplyDrawerOpenState()
    {
        if (!IstSichtbar)
            return;

        if (_e.FelderDrawer.IsOpen)
        {
            _e.DrawerSplitter.Visibility = Visibility.Visible;
            _e.DrawerSplitterRow.Height = new GridLength(DataPageWorkspaceLayoutPolicy.SplitterHoehe);
            _e.DrawerRow.MinHeight = DataPageWorkspaceLayoutPolicy.MinDrawer;
            // Kam die Zeile aus dem zugeklappten Zustand (Auto), zuerst eine feste Hoehe geben.
            if (_e.DrawerRow.Height.IsAuto)
                _e.DrawerRow.Height = new GridLength(DataPageWorkspaceLayoutPolicy.MinDrawer);
            ApplyDrawerHeight();
        }
        else
        {
            _e.DrawerSplitter.Visibility = Visibility.Collapsed;
            _e.DrawerSplitterRow.Height = new GridLength(0);
            _e.DrawerRow.MinHeight = 0;
            _e.DrawerRow.Height = GridLength.Auto;
        }
    }

    /// <summary>
    /// Blendet Uebersicht und Eingabefelder getrennt ein oder aus (Task 6, Aufklapp-Liste). Die
    /// festen Spalten- und Zeilenmasse werden dabei mit auf 0 gesetzt, sonst bliebe eine Luecke.
    /// Getrennt gebraucht wird das von der Aufklapp-Liste: Dort bleibt die Schachtansicht
    /// rechts, waehrend die Eingabefelder-Schublade verschwindet — das Formular steht in der
    /// aufgeklappten Zeile.
    /// </summary>
    public void SetzeSichtbar(bool uebersicht, bool eingabefelder)
    {
        _e.Uebersicht.Visibility = uebersicht ? Visibility.Visible : Visibility.Collapsed;
        _e.SideSplitter.Visibility = uebersicht ? Visibility.Visible : Visibility.Collapsed;
        _e.FelderDrawer.Visibility = eingabefelder ? Visibility.Visible : Visibility.Collapsed;

        if (uebersicht)
        {
            _e.SideSplitterCol.Width = new GridLength(DataPageWorkspaceLayoutPolicy.SplitterHoehe);
            _e.SideCol.MinWidth = SideColMin;
            _e.SideCol.MaxWidth = SideColMax;
            var breite = SplitterPersistenceCore.TryGetStored(NovaViewKey, SideSplitterKey, out var w)
                ? Math.Clamp(w, SideColMin, SideColMax)
                : SideColStandard;
            _e.SideCol.Width = new GridLength(breite);
        }
        else
        {
            _e.SideSplitterCol.Width = new GridLength(0);
            _e.SideCol.MinWidth = 0;
            _e.SideCol.Width = new GridLength(0);
        }

        if (eingabefelder)
        {
            ApplyDrawerOpenState();
        }
        else
        {
            _e.DrawerSplitter.Visibility = Visibility.Collapsed;
            _e.DrawerSplitterRow.Height = new GridLength(0);
            _e.DrawerRow.MinHeight = 0;
            _e.DrawerRow.Height = new GridLength(0);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Behaviors;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova-Etappe 1: Arbeitsflaeche der Haltungen-Seite mit Liste, Uebersicht rechts und
/// Eingabefeldern unten. Die Seite reicht nur ihre benannten Elemente herein; Hoehenregel,
/// Trennlinien, Auf-/Zuklappen (W03) und der Live-Abgleich des Formulars mit dem Datensatz
/// (W01) liegen hier, damit die Seite selbst nicht weiter waechst.
/// </summary>
public sealed class DataPageNovaWorkspaceController
{
    private const string NovaViewKey = "DataPage";
    private const string DrawerSplitterKey = "HaltungenEingabefelder";
    private const string SideSplitterKey = "HaltungenUebersicht";
    private const double SideColStandard = 320;
    private const double SideColMin = 240;
    private const double SideColMax = 560;
    // Spaltenkopf plus waagrechte Bildlaufleiste der Tabelle (Sichtprobe 06.09.2026).
    private const double TabellenkopfHoehe = 54;

    /// <summary>Die benannten Elemente der Arbeitsflaeche aus DataPage.xaml.</summary>
    public sealed record Elemente(
        Grid GridHost,
        FrameworkElement FilterChips,
        RowDefinition DrawerSplitterRow,
        RowDefinition DrawerRow,
        ColumnDefinition SideSplitterCol,
        ColumnDefinition SideCol,
        GridSplitter SideSplitter,
        GridSplitter DrawerSplitter,
        HaltungUebersichtPanel Uebersicht,
        HaltungFelderDrawer FelderDrawer);

    private readonly Elemente _e;
    private readonly Func<DataPageViewModel?> _vm;
    private readonly Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>> _detailBuilder;
    private readonly Action<HaltungRecord> _beobachtungen;

    private DataPageDetailLiveSync? _felderSync;
    // Zuklappen wegen Platzmangel ist automatisch; oeffnet der Benutzer danach selbst,
    // bleibt seine Wahl bis zum naechsten Seitenaufbau bestehen.
    private bool _drawerAutomatischZugeklappt;
    private bool _drawerVomBenutzerGeoeffnet;

    public DataPageNovaWorkspaceController(
        Elemente elemente,
        Func<DataPageViewModel?> viewModel,
        Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>> detailBuilder,
        Action<HaltungRecord> beobachtungen)
    {
        _e = elemente ?? throw new ArgumentNullException(nameof(elemente));
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _detailBuilder = detailBuilder ?? throw new ArgumentNullException(nameof(detailBuilder));
        _beobachtungen = beobachtungen ?? throw new ArgumentNullException(nameof(beobachtungen));
    }

    /// <summary>Einmalige Verdrahtung, unabhaengig vom ViewModel.</summary>
    public void Verdrahte()
    {
        _e.Uebersicht.BeobachtungenRequested = _beobachtungen;
        _e.Uebersicht.PlayerRequested = r =>
        {
            if (_vm() is { } vm && vm.PlayVideoCommand.CanExecute(r))
                vm.PlayVideoCommand.Execute(r);
        };
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
        // "Gross anzeigen" (Inventar 4.3/8.6): oeffnet mit und wechselt in ApplyDrawerHeight auf
        // eine 60-Prozent-Wunschhoehe, die dieselbe Sieben-Zeilen-Regel wie jede andere Hoehe
        // durchlaeuft (kein eigener Rechenweg am Policy vorbei). Die Groesse wird nicht gespeichert;
        // Ausschalten stellt die normale gespeicherte Hoehe wieder her.
        _e.FelderDrawer.IsTallChanged += (_, _) =>
        {
            _e.FelderDrawer.IsOpen = true;
            ApplyDrawerHeight();
        };
    }

    /// <summary>
    /// Eingabefelder neu aus der gewaehlten Haltung aufbauen (Auswahlwechsel, externe Feldaenderung)
    /// und den Live-Abgleich mit genau diesem Datensatz anschliessen.
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
        _e.FelderDrawer.Titel = record.GetFieldValue("Haltungsname");
        _e.FelderDrawer.Groups = gruppen;
        _felderSync = new DataPageDetailLiveSync(record, gruppen);
    }

    /// <summary>
    /// Nachpruefung W01: Der Datensatz hat sich seit der Anzeige geaendert. Die neuere Korrektur
    /// bleibt; die verworfene Eingabe steht als Hinweis in der Kopfzeile der Eingabefelder.
    /// </summary>
    public void MeldeKonflikt(string fieldName, string aktuellerWert, string eingabe)
        => _e.FelderDrawer.Hinweis = DataPageKonfliktHinweis.Text(fieldName, aktuellerWert, eingabe);

    private bool IstSichtbar => _e.FelderDrawer.Visibility == Visibility.Visible;

    /// <summary>
    /// Hoehe der Eingabefelder aus Flaeche, Zeilenhoehe und gespeicherter Lage der Trennlinie.
    /// "Gross anzeigen" (<see cref="HaltungFelderDrawer.IsTall"/>) ersetzt dabei nur die
    /// Wunschhoehe durch 60 % der Arbeitsflaeche; dieselbe <see cref="DataPageWorkspaceLayoutPolicy.Berechne"/>
    /// klemmt sie weiterhin auf die Sieben-Zeilen-Regel, sodass der Zustand auch Resize und
    /// Zu-/Aufklappen uebersteht.
    /// </summary>
    public void ApplyDrawerHeight()
    {
        if (_vm() is not { } vm || _e.GridHost.ActualHeight <= 0 || !IstSichtbar || !_e.FelderDrawer.IsOpen)
            return;

        // Die Filterzeile liegt im selben Raster ueber der Liste und zaehlt nicht zur Flaeche.
        var flaeche = _e.GridHost.ActualHeight - _e.FilterChips.ActualHeight;
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
    /// Auf- oder zugeklappte Eingabefelder (W03): Zugeklappt bleibt nur die Kopfzeile stehen, die
    /// Zeile schrumpft auf Auto und die Trennlinie verschwindet. Aufgeklappt gelten Mindesthoehe,
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
    /// Blendet Uebersicht, Eingabefelder und beide Trennlinien ein oder aus. Die festen Spalten-
    /// und Zeilenmasse werden dabei mit auf 0 gesetzt, sonst bliebe bei der Haltungsansicht eine Luecke.
    /// </summary>
    public void SetzeSichtbar(bool sichtbar)
    {
        var v = sichtbar ? Visibility.Visible : Visibility.Collapsed;
        _e.Uebersicht.Visibility = v;
        _e.SideSplitter.Visibility = v;
        _e.FelderDrawer.Visibility = v;

        if (sichtbar)
        {
            _e.SideSplitterCol.Width = new GridLength(DataPageWorkspaceLayoutPolicy.SplitterHoehe);
            _e.SideCol.MinWidth = SideColMin;
            _e.SideCol.MaxWidth = SideColMax;
            var breite = SplitterPersistenceCore.TryGetStored(NovaViewKey, SideSplitterKey, out var w)
                ? Math.Clamp(w, SideColMin, SideColMax)
                : SideColStandard;
            _e.SideCol.Width = new GridLength(breite);
            ApplyDrawerOpenState();
        }
        else
        {
            _e.DrawerSplitter.Visibility = Visibility.Collapsed;
            _e.SideSplitterCol.Width = new GridLength(0);
            _e.SideCol.MinWidth = 0;
            _e.SideCol.Width = new GridLength(0);
            _e.DrawerSplitterRow.Height = new GridLength(0);
            _e.DrawerRow.MinHeight = 0;
            _e.DrawerRow.Height = new GridLength(0);
        }
    }
}

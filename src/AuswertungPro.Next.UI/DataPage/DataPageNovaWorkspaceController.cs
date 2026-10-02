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
        PhotoHoverPreviewBehavior.SetProjectRootProvider(_e.Uebersicht, () => _vm()?.GrafikFotoProjektRoot);
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
    /// Reicht den aktiven Codekatalog des ViewModels an die Uebersicht weiter. Die Haltungsgrafik
    /// braucht ihn fuer die Klartexte; die Seite selbst holt keinen Dienst.
    /// </summary>
    public void VerbindeKatalog()
    {
        if (_vm() is { } vm)
            _e.Uebersicht.Catalog = vm.CodeCatalog;
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
    /// Leert die Eingabefelder und entsorgt ihren Live-Abgleich. Gebraucht, sobald die Seite die
    /// Aufklapp-Liste zeigt: Zwei Formulare am selben Datensatz waeren doppelte Arbeit, und ein
    /// Konflikthinweis landete im unsichtbaren.
    /// </summary>
    public void LeereFelderDrawer()
    {
        _felderSync?.Dispose();
        _felderSync = null;
        _e.FelderDrawer.Hinweis = string.Empty;
        _e.FelderDrawer.Titel = string.Empty;
        _e.FelderDrawer.Groups = null;
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
    /// Auf- oder zugeklappte Eingabefelder; die Regel steht in <see cref="NovaWorkspaceLayout"/>.
    /// </summary>
    private void ApplyDrawerOpenState()
    {
        if (!IstSichtbar)
            return;

        NovaWorkspaceLayout.WendeSchubladeAn(Flaechen, _e.FelderDrawer.IsOpen, ApplyDrawerHeight);
    }

    private NovaWorkspaceLayout.Flaechen Flaechen => new(
        _e.Uebersicht, _e.SideSplitter, _e.FelderDrawer, _e.SideSplitterCol, _e.SideCol,
        _e.DrawerSplitter, _e.DrawerSplitterRow, _e.DrawerRow);

    /// <summary>
    /// Blendet Uebersicht und Eingabefelder getrennt ein oder aus. Die festen Spalten- und
    /// Zeilenmasse werden dabei mit auf 0 gesetzt, sonst bliebe eine Luecke. Getrennt gebraucht
    /// wird das von der Aufklapp-Liste: Dort bleibt die Uebersicht rechts, waehrend die
    /// Eingabefelder-Schublade verschwindet — das Formular steht in der aufgeklappten Zeile.
    /// </summary>
    public void SetzeSichtbar(bool uebersicht, bool eingabefelder)
    {
        NovaWorkspaceLayout.SetzeSichtbar(
            Flaechen,
            uebersicht,
            eingabefelder,
            () => SplitterPersistenceCore.TryGetStored(NovaViewKey, SideSplitterKey, out var w) ? w : null,
            ApplyDrawerOpenState);
    }
}

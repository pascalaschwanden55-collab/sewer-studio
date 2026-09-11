using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova, Aufklapp-Liste: verbindet die Liste mit dem Formular der aufgeklappten Haltung —
/// nach demselben Muster wie <see cref="DataPageNovaWorkspaceController"/> fuer die
/// Eingabefelder-Schublade.
///
/// Zwei Regeln tragen diese Klasse:
/// 1. Es gibt genau EIN Formular, naemlich das der aufgeklappten Haltung. Beim Wechsel wird der
///    bisherige <see cref="DataPageDetailLiveSync"/> entsorgt, sonst wuerde ein alter Datensatz
///    weiter in ein nicht mehr sichtbares Formular schreiben.
/// 2. Es gibt keinen zweiten Schreibweg. Die Felder kommen fertig aus dem Detail-Builder der
///    Seite (<c>DataPageRecordDetailsBuilder</c> + <c>DataPageDetailItemFactory</c> mit der
///    Konfliktregel); hier wird nur gebaut, angeschlossen und wieder entsorgt.
/// </summary>
public sealed class DataPageAufklappListeController : IDisposable
{
    private readonly HaltungAufklappListe _liste;
    private readonly Func<DataPageViewModel?> _vm;
    private readonly Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>> _detailBuilder;

    private readonly ObjektaktenInlineBindung _objektakte = new();
    private DataPageDetailLiveSync? _sync;
    private bool _verdrahtet;

    public DataPageAufklappListeController(
        HaltungAufklappListe liste,
        Func<DataPageViewModel?> viewModel,
        Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>> detailBuilder)
    {
        _liste = liste ?? throw new ArgumentNullException(nameof(liste));
        _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        _detailBuilder = detailBuilder ?? throw new ArgumentNullException(nameof(detailBuilder));
    }

    /// <summary>Einmalige Verdrahtung, unabhaengig vom ViewModel.</summary>
    public void Verdrahte()
    {
        if (_verdrahtet)
            return;

        // Der Builder gehoert der Seite; die Liste zeigt ihn nur an, gerufen wird er hier.
        _liste.DetailBuilder = _detailBuilder;
        _liste.Reihenfolge.Datensaetze = () => _vm()?.Records;
        _liste.Reihenfolge.DarfVerschieben = () => _vm() is { } vm && vm.IsProjectReady;
        _liste.Reihenfolge.Verschiebe = (eintrag, position) =>
        {
            if (_vm() is not { } vm || eintrag is not HaltungRecord record
                || !(vm.IsProjectReady) || !vm.Records.Contains(record)) return false;
            vm.Selected = record;
            return vm.MoveToPosition(position);
        };
        _liste.AufgeklapptChanged += OnAufgeklapptChanged;
        _liste.AnsichtAnpassenRequested += OnAnsichtAnpassen;
        _verdrahtet = true;
        // Ein nach Unloaded noch offenes Formular braucht wieder seinen Live-Abgleich.
        AktualisiereFormular();
    }

    /// <summary>
    /// Baut das Formular der aufgeklappten Haltung neu auf und schliesst den Live-Abgleich mit
    /// genau diesem Datensatz an. Ohne aufgeklappte Haltung bleibt nichts stehen.
    ///
    /// Die Seite ruft das zusaetzlich bei einem Wechsel von <c>vm.Selected</c>: Eine Auswahl per
    /// Pfeiltaste klappt bewusst nicht auf, aber eine verschwundene Haltung (geloescht,
    /// Projektwechsel) darf kein Formular hinterlassen.
    /// </summary>
    public void AktualisiereFormular()
    {
        Views.Controls.ObjektakteView.UebernehmeEingabe(_liste);
        _sync?.Dispose();
        _sync = null;
        _liste.Hinweis = string.Empty;

        var record = _liste.Aufgeklappt;
        if (record is null || _liste.DetailBuilder is null)
        {
            _objektakte.Leere(_liste);
            _liste.Objektakte = null;
            _liste.ZeigeThemen(null);
            return;
        }

        if (!IstNochInDerListe(record))
        {
            // Geloescht oder Projektwechsel: nicht nur die Themen leeren, sondern wirklich
            // zuklappen. Sonst bliebe der Pfeil gedreht und die Liste behauptete, da sei noch
            // etwas offen. Der erneute AufgeklapptChanged laeuft oben mit record == null aus.
            _objektakte.Leere(_liste);
            _liste.Objektakte = null;
            _liste.ZeigeThemen(null);
            _liste.KlappeZu();
            return;
        }

        _liste.Objektakte = _objektakte.Aktualisiere(_liste, record, _vm(), () => _vm()?.ObjektakteErstellen?.Invoke(record.Id));
        var gruppen = _liste.DetailBuilder(record);
        _liste.ZeigeThemen(AufklappDetailLayout.Themen(gruppen, _vm()?.Settings.DataPageLayout.DetailLayout));
        _sync = new DataPageDetailLiveSync(record, gruppen);
    }

    /// <summary>
    /// Nachpruefung W01: Der Datensatz hat sich seit der Anzeige geaendert. Die neuere Korrektur
    /// bleibt; die verworfene Eingabe steht als Hinweis in der Kopfzeile des Formulars. Der
    /// Wortlaut ist derselbe wie bei den Eingabefeldern (<see cref="DataPageKonfliktHinweis"/>).
    /// </summary>
    public void MeldeKonflikt(string fieldName, string aktuellerWert, string eingabe)
        => _liste.Hinweis = DataPageKonfliktHinweis.Text(fieldName, aktuellerWert, eingabe);

    /// <summary>
    /// Gehoert die aufgeklappte Haltung noch zum Bestand? Gefragt werden beide Quellen, die es
    /// wissen koennen: das ViewModel, sobald die Seite eines gesetzt hat, und die Liste selbst.
    /// Es ist dieselbe Sammlung — die Seite bindet <c>Records</c> —, aber die Liste kann die
    /// Frage auch ohne ViewModel beantworten, und genau das braucht der Seitenaufbau.
    /// </summary>
    private bool IstNochInDerListe(HaltungRecord record)
    {
        if (_vm() is { } vm && !vm.Records.Contains(record))
            return false;

        return _liste.ItemsSource is not System.Collections.IEnumerable quelle || Enthaelt(quelle, record);
    }

    private static bool Enthaelt(System.Collections.IEnumerable quelle, HaltungRecord record)
    {
        foreach (var eintrag in quelle)
        {
            if (ReferenceEquals(eintrag, record))
                return true;
        }

        return false;
    }

    private void OnAufgeklapptChanged(object? sender, EventArgs e) => AktualisiereFormular();

    private void OnAnsichtAnpassen(object? sender, EventArgs e)
    {
        if (_liste.Aufgeklappt is not { } record || _vm()?.Settings is not { } settings)
            return;
        var layout = AufklappLayoutWindow.Bearbeite(_liste, _detailBuilder(record), settings.DataPageLayout.DetailLayout);
        if (layout is null)
            return;
        settings.DataPageLayout.DetailLayout = RecordDetailLayoutSettingsMapper.ToSettings(layout);
        settings.Save();
        AktualisiereFormular();
    }

    public void Dispose()
    {
        _objektakte.Leere(_liste);
        _liste.Objektakte = null;
        _liste.Reihenfolge.Beende();
        _liste.Reihenfolge.Datensaetze = null;
        _liste.Reihenfolge.DarfVerschieben = null;
        _liste.Reihenfolge.Verschiebe = null;
        if (_verdrahtet)
        {
            _liste.AufgeklapptChanged -= OnAufgeklapptChanged;
            _liste.AnsichtAnpassenRequested -= OnAnsichtAnpassen;
            _verdrahtet = false;
        }

        _sync?.Dispose();
        _sync = null;
    }
}

using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Nova, Aufklapp-Liste (Task 6): verbindet die Schacht-Liste mit dem Formular des
/// aufgeklappten Schachts — nach demselben Muster wie <see cref="DataPageAufklappListeController"/>
/// fuer die Haltungen.
///
/// Zwei Regeln tragen diese Klasse:
/// 1. Es gibt genau EIN Formular, naemlich das des aufgeklappten Schachts. Beim Wechsel wird der
///    bisherige <see cref="DataPageDetailLiveSync"/> entsorgt, sonst wuerde ein alter Datensatz
///    weiter in ein nicht mehr sichtbares Formular schreiben.
/// 2. Es gibt keinen zweiten Schreibweg. Die Felder kommen fertig aus dem Detail-Builder der
///    Seite (<c>SchaechteRecordDetailsBuilder</c>, derselbe wie fuer die Eingabefelder-Schublade);
///    hier wird nur gebaut, angeschlossen und wieder entsorgt.
/// </summary>
public sealed class SchaechteAufklappListeController : IDisposable
{
    private readonly SchachtAufklappListe _liste;
    private readonly Func<SchaechtePageViewModel?> _vm;
    private readonly Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>> _detailBuilder;

    private readonly ObjektaktenInlineBindung _objektakte = new();
    private DataPageDetailLiveSync? _sync;
    private bool _verdrahtet;

    public SchaechteAufklappListeController(
        SchachtAufklappListe liste,
        Func<SchaechtePageViewModel?> viewModel,
        Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>> detailBuilder)
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

        _liste.DetailBuilder = _detailBuilder;
        _liste.Reihenfolge.Datensaetze = () => _vm()?.Records;
        _liste.Reihenfolge.DarfVerschieben = () => _vm() is { } vm && vm.CanMutateShaftData;
        _liste.Reihenfolge.Verschiebe = (eintrag, position) =>
        {
            if (_vm() is not { } vm || eintrag is not SchachtRecord record
                || !(vm.CanMutateShaftData) || !vm.Records.Contains(record)) return false;
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
    /// Baut das Formular des aufgeklappten Schachts neu auf und schliesst den Live-Abgleich mit
    /// genau diesem Datensatz an. Ohne aufgeklappten Schacht bleibt nichts stehen.
    /// </summary>
    public void AktualisiereFormular()
    {
        Views.Controls.ObjektakteView.UebernehmeEingabe(_liste);
        _sync?.Dispose();
        _sync = null;

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
            // etwas offen.
            _objektakte.Leere(_liste);
            _liste.Objektakte = null;
            _liste.ZeigeThemen(null);
            _liste.KlappeZu();
            return;
        }

        _liste.Objektakte = _objektakte.Aktualisiere(_liste, record, _vm(), () => _vm()?.ObjektakteErstellen?.Invoke(record.Id));
        var gruppen = _liste.DetailBuilder(record);
        _liste.ZeigeThemen(AufklappDetailLayout.Themen(gruppen, _vm()?.Settings.SchaechtePageLayout.DetailLayout));
        // Allgemeine Ueberladung: SchachtRecord meldet Feldaenderungen genau wie HaltungRecord
        // (Fields[Name]/Fields), siehe Doku dort.
        _sync = new DataPageDetailLiveSync(record, record.GetFieldValue, gruppen);
    }

    /// <summary>
    /// Gehoert der aufgeklappte Schacht noch zum Bestand? Gefragt werden beide Quellen, die es
    /// wissen koennen: das ViewModel, sobald die Seite eines gesetzt hat, und die Liste selbst.
    /// </summary>
    private bool IstNochInDerListe(SchachtRecord record)
    {
        if (_vm() is { } vm && !vm.Records.Contains(record))
            return false;

        return _liste.ItemsSource is not System.Collections.IEnumerable quelle || Enthaelt(quelle, record);
    }

    private static bool Enthaelt(System.Collections.IEnumerable quelle, SchachtRecord record)
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
        var layout = AufklappLayoutWindow.Bearbeite(_liste, _detailBuilder(record), settings.SchaechtePageLayout.DetailLayout);
        if (layout is null)
            return;
        settings.SchaechtePageLayout.DetailLayout = RecordDetailLayoutSettingsMapper.ToSettings(layout);
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

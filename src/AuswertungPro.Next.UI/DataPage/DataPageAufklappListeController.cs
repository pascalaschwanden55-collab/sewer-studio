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
        _liste.AufgeklapptChanged += OnAufgeklapptChanged;
        _verdrahtet = true;
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
        _sync?.Dispose();
        _sync = null;
        _liste.Hinweis = string.Empty;

        var record = _liste.Aufgeklappt;
        if (record is null || _liste.DetailBuilder is null || !IstNochInDerListe(record))
        {
            _liste.ZeigeThemen(null);
            return;
        }

        var gruppen = _liste.DetailBuilder(record);
        _liste.ZeigeThemen(HaltungThemenGruppierung.Bilde(gruppen));
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
    /// Gehoert die aufgeklappte Haltung noch zum Projekt? Ohne ViewModel (Test, Seitenaufbau)
    /// wird die Frage nicht gestellt — dann zaehlt allein, was die Liste zeigt.
    /// </summary>
    private bool IstNochInDerListe(HaltungRecord record)
        => _vm() is not { } vm || vm.Records.Contains(record);

    private void OnAufgeklapptChanged(object? sender, EventArgs e) => AktualisiereFormular();

    public void Dispose()
    {
        if (_verdrahtet)
        {
            _liste.AufgeklapptChanged -= OnAufgeklapptChanged;
            _verdrahtet = false;
        }

        _sync?.Dispose();
        _sync = null;
    }
}

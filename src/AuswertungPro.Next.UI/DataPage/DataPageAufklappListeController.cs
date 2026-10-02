using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages.Haltungsansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Aufklapp-Liste der Haltungen; der Ablauf steht in <see cref="AufklappListeController{TListe,TRecord}"/>.
/// Schranke ist <c>IsProjectReady</c>.
/// </summary>
public sealed class DataPageAufklappListeController(
    HaltungAufklappListe liste,
    Func<DataPageViewModel?> viewModel,
    Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>> detailBuilder)
    : AufklappListeController<HaltungAufklappListe, HaltungRecord>(liste, detailBuilder)
{
    private readonly Func<DataPageViewModel?> _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    protected override Collection<HaltungRecord>? Datensaetze => _vm()?.Records;
    protected override object? Seite => _vm();
    protected override bool DarfAendern => _vm() is { IsProjectReady: true };
    protected override AppSettings? Einstellungen => _vm()?.Settings;
    protected override DataPageLayoutSettings Layout(AppSettings einstellungen) => einstellungen.DataPageLayout;
    protected override string? Wert(HaltungRecord record, string feld) => record.GetFieldValue(feld);
    protected override ObjektakteViewModel? ErstelleObjektakte(HaltungRecord record) => _vm()?.ObjektakteErstellen?.Invoke(record.Id);

    protected override bool WaehleUndVerschiebe(HaltungRecord record, int position)
    {
        if (_vm() is not { } vm) return false;
        vm.Selected = record;
        return vm.MoveToPosition(position);
    }

    /// <summary>
    /// Nachpruefung W01: Der Datensatz hat sich seit der Anzeige geaendert. Die neuere Korrektur
    /// bleibt; die verworfene Eingabe steht als Hinweis in der Kopfzeile des Formulars. Der
    /// Wortlaut ist derselbe wie bei den Eingabefeldern (<see cref="DataPageKonfliktHinweis"/>).
    /// </summary>
    public void MeldeKonflikt(string fieldName, string aktuellerWert, string eingabe)
        => SetzeHinweis(DataPageKonfliktHinweis.Text(fieldName, aktuellerWert, eingabe));
}

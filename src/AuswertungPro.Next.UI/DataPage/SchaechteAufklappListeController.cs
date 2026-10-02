using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;
using AuswertungPro.Next.UI.ViewModels.Pages;
using AuswertungPro.Next.UI.Views.Pages.Schachtansicht;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

/// <summary>
/// Aufklapp-Liste der Schaechte; der Ablauf steht in <see cref="AufklappListeController{TListe,TRecord}"/>.
/// Schranke ist <c>CanMutateShaftData</c> (sperrt auch waehrend des Protokollimports).
/// </summary>
public sealed class SchaechteAufklappListeController(
    SchachtAufklappListe liste,
    Func<SchaechtePageViewModel?> viewModel,
    Func<SchachtRecord, IReadOnlyList<RecordDetailGroup>> detailBuilder)
    : AufklappListeController<SchachtAufklappListe, SchachtRecord>(liste, detailBuilder)
{
    private readonly Func<SchaechtePageViewModel?> _vm = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

    protected override Collection<SchachtRecord>? Datensaetze => _vm()?.Records;
    protected override object? Seite => _vm();
    protected override bool DarfAendern => _vm() is { CanMutateShaftData: true };
    protected override AppSettings? Einstellungen => _vm()?.Settings;
    protected override DataPageLayoutSettings Layout(AppSettings einstellungen) => einstellungen.SchaechtePageLayout;
    protected override string? Wert(SchachtRecord record, string feld) => record.GetFieldValue(feld);
    protected override ObjektakteViewModel? ErstelleObjektakte(SchachtRecord record) => _vm()?.ObjektakteErstellen?.Invoke(record.Id);

    protected override bool WaehleUndVerschiebe(SchachtRecord record, int position)
    {
        if (_vm() is not { } vm) return false;
        vm.Selected = record;
        return vm.MoveToPosition(position);
    }
}

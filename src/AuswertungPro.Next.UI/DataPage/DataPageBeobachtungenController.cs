using System.Collections.ObjectModel;
using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageBeobachtungenVsaResult(bool Ok, string? ErrorMessage);

public sealed record DataPageBeobachtungenWindowRequest(
    ObservableCollection<ProtocolEntry> Entries,
    string HoldingName,
    ICommand OpenProtocolCommand,
    HaltungRecord Record,
    Action VsaUpdateAction,
    Action SyncHoldingFieldsAction);

public sealed class DataPageBeobachtungenController
{
    private readonly Action<string, string> _showInfo;
    private readonly Action<string, string> _showWarn;
    private readonly Func<HaltungRecord, DataPageBeobachtungenVsaResult?> _evaluateVsa;

    public DataPageBeobachtungenController(
        Action<string, string> showInfo,
        Action<string, string> showWarn,
        Func<HaltungRecord, DataPageBeobachtungenVsaResult?> evaluateVsa)
    {
        _showInfo = showInfo ?? throw new ArgumentNullException(nameof(showInfo));
        _showWarn = showWarn ?? throw new ArgumentNullException(nameof(showWarn));
        _evaluateVsa = evaluateVsa ?? throw new ArgumentNullException(nameof(evaluateVsa));
    }

    public DataPageBeobachtungenWindowRequest? BuildOpenRequest(
        HaltungRecord? record,
        ObservableCollection<ProtocolEntry> entries,
        ICommand openProtocolCommand,
        Action<HaltungRecord> selectRecord,
        Action refreshSelectedRecord,
        Action<HaltungRecord, bool> syncHoldingFields)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(openProtocolCommand);
        ArgumentNullException.ThrowIfNull(selectRecord);
        ArgumentNullException.ThrowIfNull(refreshSelectedRecord);
        ArgumentNullException.ThrowIfNull(syncHoldingFields);

        if (record is null)
        {
            _showInfo(DataPageRecordCommandRouter.MissingSelectionMessage, "Beobachtungen");
            return null;
        }

        selectRecord(record);
        var holdingName = record.GetFieldValue("Haltungsname");

        return new DataPageBeobachtungenWindowRequest(
            entries,
            holdingName,
            openProtocolCommand,
            record,
            () => UpdateVsa(record, holdingName, refreshSelectedRecord),
            () => syncHoldingFields(record, true));
    }

    private void UpdateVsa(
        HaltungRecord record,
        string holdingName,
        Action refreshSelectedRecord)
    {
        var result = _evaluateVsa(record);
        if (result is null)
            return;

        if (result.Ok)
        {
            refreshSelectedRecord();
            _showInfo($"VSA Zustand aktualisiert f\u00fcr {holdingName}.", "VSA");
            return;
        }

        _showWarn($"VSA Fehler: {result.ErrorMessage}", "VSA");
    }
}

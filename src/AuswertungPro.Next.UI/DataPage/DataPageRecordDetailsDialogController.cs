using System.Windows.Input;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageRecordDetailsDialogRequest(
    string Title,
    string Header,
    string SubHeader,
    IReadOnlyList<RecordDetailGroup> Groups,
    ICommand? SuggestMeasuresCommand);

public sealed class DataPageRecordDetailsDialogController
{
    private const string DefaultTitle = "Haltungsdetails";
    private const string SubHeaderText = "Komplette Zeile in Spaltenreihenfolge der Haltungs-Ansicht.";

    private readonly Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>> _buildGroups;
    private readonly Func<HaltungRecord, ICommand?> _createSuggestMeasuresCommand;

    public DataPageRecordDetailsDialogController(
        Func<HaltungRecord, IReadOnlyList<RecordDetailGroup>> buildGroups,
        Func<HaltungRecord, ICommand?> createSuggestMeasuresCommand)
    {
        _buildGroups = buildGroups ?? throw new ArgumentNullException(nameof(buildGroups));
        _createSuggestMeasuresCommand = createSuggestMeasuresCommand ?? throw new ArgumentNullException(nameof(createSuggestMeasuresCommand));
    }

    public DataPageRecordDetailsDialogRequest Build(HaltungRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var holding = record.GetFieldValue("Haltungsname");
        var hasHolding = !string.IsNullOrWhiteSpace(holding);
        var header = hasHolding ? $"Haltung {holding}" : DefaultTitle;
        var title = hasHolding ? $"{DefaultTitle} - {holding}" : DefaultTitle;

        return new DataPageRecordDetailsDialogRequest(
            title,
            header,
            SubHeaderText,
            _buildGroups(record),
            _createSuggestMeasuresCommand(record));
    }
}

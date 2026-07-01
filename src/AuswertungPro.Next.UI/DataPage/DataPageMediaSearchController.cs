using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageMediaSearchResult(
    bool Applied,
    int AppliedVideoCount,
    int AppliedPdfCount,
    int AppliedFotoCount);

public sealed class DataPageMediaSearchController
{
    private readonly Func<IReadOnlyList<HaltungRecord>> _getRecords;
    private readonly Func<string?> _getLastVideoSourceFolder;
    private readonly Func<string?> _getLastVideoFolder;
    private readonly Func<IReadOnlyList<HaltungRecord>, string?, DataPageMediaSearchResult?> _showMediaSearch;
    private readonly Action _markProjectDirty;
    private readonly Action _notifyRecordsChanged;
    private readonly Action<string> _setStatus;

    public DataPageMediaSearchController(
        Func<IReadOnlyList<HaltungRecord>> getRecords,
        Func<string?> getLastVideoSourceFolder,
        Func<string?> getLastVideoFolder,
        Func<IReadOnlyList<HaltungRecord>, string?, DataPageMediaSearchResult?> showMediaSearch,
        Action markProjectDirty,
        Action notifyRecordsChanged,
        Action<string> setStatus)
    {
        _getRecords = getRecords ?? throw new ArgumentNullException(nameof(getRecords));
        _getLastVideoSourceFolder = getLastVideoSourceFolder ?? throw new ArgumentNullException(nameof(getLastVideoSourceFolder));
        _getLastVideoFolder = getLastVideoFolder ?? throw new ArgumentNullException(nameof(getLastVideoFolder));
        _showMediaSearch = showMediaSearch ?? throw new ArgumentNullException(nameof(showMediaSearch));
        _markProjectDirty = markProjectDirty ?? throw new ArgumentNullException(nameof(markProjectDirty));
        _notifyRecordsChanged = notifyRecordsChanged ?? throw new ArgumentNullException(nameof(notifyRecordsChanged));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
    }

    public void Open()
    {
        var records = _getRecords();
        if (records.Count == 0)
        {
            _setStatus("Keine Haltungen vorhanden.");
            return;
        }

        var result = _showMediaSearch(records, BuildInitialFolder());
        if (result?.Applied != true)
            return;

        _markProjectDirty();
        _notifyRecordsChanged();
        _setStatus($"Medien verlinkt: {result.AppliedVideoCount} Videos, {result.AppliedPdfCount} PDFs, {result.AppliedFotoCount} Fotos");
    }

    private string? BuildInitialFolder()
    {
        var sourceFolder = _getLastVideoSourceFolder();
        if (!string.IsNullOrWhiteSpace(sourceFolder))
            return sourceFolder;

        var legacyFolder = _getLastVideoFolder();
        return string.IsNullOrWhiteSpace(legacyFolder) ? null : legacyFolder;
    }
}

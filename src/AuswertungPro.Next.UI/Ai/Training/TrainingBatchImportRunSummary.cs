namespace AuswertungPro.Next.UI.Ai.Training;

public sealed class TrainingBatchImportRunSummary
{
    private int _emptyProtocols;
    private int _duplicateOnlyCases;
    private int _missingProtocols;
    private int _unreadableProtocols;
    private string _lastError = "";

    public int TotalNew { get; private set; }
    public int Errors { get; private set; }

    public void AddNewSamples(int count)
    {
        TotalNew += count;
    }

    public void RecordError(string message)
    {
        Errors++;
        _lastError = message;
    }

    public void RecordSkip(TrainingCenterBatchSkipKind kind)
    {
        switch (kind)
        {
            case TrainingCenterBatchSkipKind.DuplicateOnly:
                _duplicateOnlyCases++;
                break;
            case TrainingCenterBatchSkipKind.MissingProtocol:
                _missingProtocols++;
                break;
            case TrainingCenterBatchSkipKind.UnreadableProtocol:
                _unreadableProtocols++;
                break;
            default:
                _emptyProtocols++;
                break;
        }
    }

    public string? BuildNoNewStatus(int processedCaseCount)
    {
        if (TotalNew != 0 || processedCaseCount <= 0)
            return null;

        var diag = $"0 neue Samples aus {processedCaseCount} Faellen.";
        if (Errors > 0) diag += $" {Errors} Fehler (letzter: {_lastError}).";
        if (_emptyProtocols > 0) diag += $" {_emptyProtocols} ohne Eintraege.";
        if (_duplicateOnlyCases > 0) diag += $" {_duplicateOnlyCases} nur Duplikate.";
        if (_missingProtocols > 0) diag += $" {_missingProtocols} fehlende Protokolle.";
        if (_unreadableProtocols > 0) diag += $" {_unreadableProtocols} nicht lesbar.";
        return diag;
    }

    public string BuildCompletionStatus()
    {
        var status = $"Fertig! {TotalNew} Kandidaten gespeichert (Status: Neu). Freigabe ueber Review (Modul I) \u2014 kein Auto-Index.";
        if (Errors > 0)
            status += $" {Errors} Fehler.";
        return status;
    }
}

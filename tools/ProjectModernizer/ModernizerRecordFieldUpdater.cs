using AuswertungPro.Next.Domain.Models;

internal static class ModernizerRecordFieldUpdater
{
    public static void SetIfChanged(
        HaltungRecord record,
        string field,
        string oldValue,
        string newValue,
        bool dryRun,
        ModernizeReport report)
    {
        if (ModernizerPathComparison.PathValueEquals(oldValue, newValue))
            return;

        if (dryRun)
        {
            report.RelinkedPaths++;
            return;
        }

        if (ForceSet(record, field, newValue, report, count: false))
            report.RelinkedPaths++;
    }

    public static bool ForceSet(
        HaltungRecord record,
        string field,
        string value,
        ModernizeReport report,
        bool count = true)
    {
        record.SetFieldValue(field, value, FieldSource.Legacy, userEdited: false);
        if (!ModernizerPathComparison.PathValueEquals(record.GetFieldValue(field), value))
        {
            report.UnresolvedPaths++;
            report.Messages.Add($"{field} nicht aktualisiert: Feld ist benutzerbearbeitet.");
            return false;
        }

        if (count)
            report.RelinkedPaths++;
        return true;
    }
}

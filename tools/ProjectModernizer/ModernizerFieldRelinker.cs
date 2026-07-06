using AuswertungPro.Next.Domain.Models;

internal static class ModernizerFieldRelinker
{
    public static void RelinkHaltungField(
        HaltungRecord record,
        string field,
        ModernizerRelinkContext context,
        Func<string, bool> predicate,
        string unresolvedPrefix = "")
    {
        var raw = record.GetFieldValue(field).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        if (ModernizerPathResolver.TryResolveOrCopyModernPath(
                raw,
                context.Root,
                context.ProjectFolder,
                predicate,
                context.ExternalFiles,
                context.DryRun,
                context.Report,
                context.CopyKind,
                out var rel))
        {
            if (!context.DryRun)
                ModernizerRecordFieldUpdater.ForceSet(record, field, rel, context.Report);
            else
                context.Report.RelinkedPaths++;
        }
        else
        {
            context.Report.UnresolvedPaths++;
            context.Report.Messages.Add($"{unresolvedPrefix}{field} nicht aufgeloest: {raw}");
        }
    }

    public static void RelinkHaltungFieldList(
        HaltungRecord record,
        string field,
        ModernizerRelinkContext context,
        Func<string, bool> predicate,
        string unresolvedPrefix = "")
    {
        var raw = record.GetFieldValue(field).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return;

        var resolvedCount = 0;
        var changed = false;
        var values = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (ModernizerPathResolver.TryResolveOrCopyModernPath(
                    part,
                    context.Root,
                    context.ProjectFolder,
                    predicate,
                    context.ExternalFiles,
                    context.DryRun,
                    context.Report,
                    context.CopyKind,
                    out var rel))
            {
                values.Add(rel);
                changed |= !string.Equals(part.Replace('\\', '/'), rel, StringComparison.OrdinalIgnoreCase);
                resolvedCount++;
            }
            else
            {
                values.Add(part);
                context.Report.UnresolvedPaths++;
                context.Report.Messages.Add($"{unresolvedPrefix}{field} nicht aufgeloest: {part}");
            }
        }

        if (!changed)
            return;

        if (context.DryRun)
        {
            context.Report.RelinkedPaths += resolvedCount;
            return;
        }

        if (ModernizerRecordFieldUpdater.ForceSet(record, field, string.Join(";", values), context.Report, count: false))
            context.Report.RelinkedPaths += resolvedCount;
    }

    public static void RelinkSchachtField(
        SchachtRecord record,
        string field,
        ModernizerRelinkContext context,
        Func<string, bool> predicate,
        string unresolvedPrefix = "Schacht ")
    {
        var raw = record.GetFieldValue(field).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        if (ModernizerPathResolver.TryResolveOrCopyModernPath(
                raw,
                context.Root,
                context.ProjectFolder,
                predicate,
                context.ExternalFiles,
                context.DryRun,
                context.Report,
                context.CopyKind,
                out var rel))
        {
            if (!context.DryRun)
                record.SetFieldValue(field, rel);
            context.Report.RelinkedPaths++;
        }
        else
        {
            context.Report.UnresolvedPaths++;
            context.Report.Messages.Add($"{unresolvedPrefix}{field} nicht aufgeloest: {raw}");
        }
    }
}

using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

internal static class ModernizerFieldFlattener
{
    public static void FlattenRecordField(
        HaltungRecord record,
        string field,
        ModernizerFlattenContext context,
        Func<string, bool> predicate,
        StructureMoveKind moveKind = StructureMoveKind.FlatMedia)
    {
        var raw = record.GetFieldValue(field).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        if (!ModernizerFlattenedPathResolver.TryResolve(
                raw,
                context,
                predicate,
                (value, source) => ModernizerStructureFiles.BuildFlatFieldTarget(
                    value,
                    source,
                    field,
                    context.HoldingRoot,
                    context.HoldingName,
                    context.DateStamp,
                    index: 0),
                moveKind,
                $"{field} nicht aufgeloest fuer Flatten: {raw}",
                keepDirectChild: true,
                out var rel))
            return;

        ModernizerRecordFieldUpdater.SetIfChanged(record, field, raw, rel, context.DryRun, context.Report);
    }

    public static void FlattenRecordFieldList(
        HaltungRecord record,
        string field,
        ModernizerFlattenContext context,
        Func<string, bool> predicate,
        StructureMoveKind moveKind = StructureMoveKind.FlatMedia)
    {
        var raw = record.GetFieldValue(field).Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return;

        var values = new List<string>(parts.Length);
        var changed = false;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (!ModernizerFlattenedPathResolver.TryResolve(
                    part,
                    context,
                    predicate,
                    (value, source) => ModernizerStructureFiles.BuildFlatFieldTarget(
                        value,
                        source,
                        field,
                        context.HoldingRoot,
                        context.HoldingName,
                        context.DateStamp,
                        i),
                    moveKind,
                    $"{field} nicht aufgeloest fuer Flatten: {part}",
                    keepDirectChild: true,
                    out var rel))
            {
                values.Add(part);
                continue;
            }

            values.Add(rel);
            changed |= !ModernizerPathComparison.PathValueEquals(part, rel);
        }

        if (!changed)
            return;

        var value = string.Join(";", ModernizerPathComparison.DeduplicatePathValues(values));
        if (context.DryRun)
        {
            context.Report.RelinkedPaths++;
            return;
        }

        if (ModernizerRecordFieldUpdater.ForceSet(record, field, value, context.Report, count: false))
            context.Report.RelinkedPaths++;
    }
}

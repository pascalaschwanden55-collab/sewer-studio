using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.DataPage;

public enum DataPageClearColumnStatus
{
    AlreadyEmpty,
    RequiresConfirmation
}

public sealed record DataPageClearColumnPlan(
    DataPageClearColumnStatus Status,
    int AffectedCount,
    int TotalCount);

/// <summary>
/// Logik fuer "Spalte leeren"; Dialogtexte und Benutzerbestaetigung bleiben in DataPage.
/// </summary>
public static class DataPageClearColumnController
{
    public static DataPageClearColumnPlan BuildPlan(IEnumerable<HaltungRecord> records, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(records);

        var total = 0;
        var affected = 0;
        foreach (var record in records)
        {
            total++;
            if (!string.IsNullOrEmpty(record.GetFieldValue(fieldName)))
                affected++;
        }

        return new DataPageClearColumnPlan(
            affected == 0 ? DataPageClearColumnStatus.AlreadyEmpty : DataPageClearColumnStatus.RequiresConfirmation,
            affected,
            total);
    }

    public static int ClearColumn(IEnumerable<HaltungRecord> records, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(records);

        var cleared = 0;
        foreach (var record in records)
        {
            // userEdited: true um die Guard-Clause zu umgehen; danach freigeben fuer Re-Importe.
            record.SetFieldValue(fieldName, string.Empty, FieldSource.Manual, userEdited: true);
            if (record.FieldMeta.TryGetValue(fieldName, out var meta))
                meta.UserEdited = false;
            cleared++;
        }

        return cleared;
    }
}

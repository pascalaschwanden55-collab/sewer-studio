using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

// Reine Feld-Applikations-Helfer (verhaltensneutral aus WinCanDbImportService.cs ausgelagert,
// um die Hauptdatei unter der 1000-Zeilen-Ratchet-Grenze zu halten). Kein eigener Zustand.
public sealed partial class WinCanDbImportService
{
    /// <summary>
    /// Erzeugt den "Primaere_Schaeden" Text aus den Protokoll-Eintraegen.
    /// Delegation: Logik liegt jetzt in Common.PrimaryDamagesTextBuilder.
    /// </summary>
    private static void BuildPrimaryDamagesText(HaltungRecord record, List<ProtocolEntry> entries)
    {
        var text = Common.PrimaryDamagesTextBuilder.Build(entries, skipAePrefix: false);
        if (text is not null)
            record.SetFieldValue("Primaere_Schaeden", text, FieldSource.Legacy, userEdited: false);
    }

    private static void ApplyField(HaltungRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        record.SetFieldValue(field, value.Trim(), FieldSource.Legacy, userEdited: false);
    }

    private static bool ApplyImportedField(HaltungRecord record, string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var before = record.GetFieldValue(field);
        record.SetFieldValue(field, value.Trim(), FieldSource.Legacy, userEdited: false);
        var after = record.GetFieldValue(field);
        return !string.Equals(before, after, StringComparison.OrdinalIgnoreCase);
    }

    private static void MergeImportedCandidate(HaltungRecord target, HaltungRecord source)
    {
        var mergeFields = new[]
        {
            "Datum_Jahr",
            "Haltungslaenge_m",
            "DN_mm",
            "Rohrmaterial",
            "Inspektionsrichtung",
            "Bemerkungen",
            "Link"
        };

        foreach (var field in mergeFields)
        {
            var current = target.GetFieldValue(field);
            if (!string.IsNullOrWhiteSpace(current))
                continue;

            var incoming = source.GetFieldValue(field);
            if (string.IsNullOrWhiteSpace(incoming))
                continue;

            target.SetFieldValue(field, incoming, FieldSource.Legacy, userEdited: false);
        }
    }
}

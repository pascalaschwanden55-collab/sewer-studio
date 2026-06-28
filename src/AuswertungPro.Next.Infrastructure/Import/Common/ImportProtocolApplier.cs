using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Protocol;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Gemeinsamer statischer Helfer fuer alle Import-Services, um Protokoll-Revisionen
/// verhaltensneutral anzuwenden (WinCan, IBAK, KINS).
///
/// Logik (Audit I1):
///   - Protocol null oder leer          → EnsureProtocol (Erstanlage)
///   - Gleicher Inhalt wie aktuelle Rev  → kein Schreiben (idempotent)
///   - Inhalt hat sich geaendert         → History.Add + neue Revision mit comment
/// </summary>
internal static class ImportProtocolApplier
{
    /// <summary>
    /// Wendet <paramref name="entries"/> als neue oder aktualisierte Protokoll-Revision
    /// auf <paramref name="record"/> an. Der Aufrufer verantwortet ggf. vorheriges
    /// Klonen der Eintraege (z.B. KINS-eigenes CloneEntry).
    /// </summary>
    internal static void Apply(
        HaltungRecord record,
        List<ProtocolEntry> entries,
        ProtocolService protocolService,
        string comment)
    {
        if (record.Protocol is null)
        {
            record.Protocol = protocolService.EnsureProtocol(
                record.GetFieldValue("Haltungsname") ?? string.Empty, entries, null);
            return;
        }

        if (record.Protocol.Current.Entries.Count == 0 && record.Protocol.Original.Entries.Count == 0)
        {
            record.Protocol = protocolService.EnsureProtocol(
                record.GetFieldValue("Haltungsname") ?? string.Empty, entries, null);
            return;
        }

        // Audit I1: identischer Re-Import erzeugt keine neue Revision
        if (ProtocolContentFingerprint.HasSameContent(record.Protocol.Current, entries))
            return;

        record.Protocol.History.Add(record.Protocol.Current);
        record.Protocol.Current = new ProtocolRevision
        {
            Comment = comment,
            CreatedAt = DateTimeOffset.UtcNow,
            Entries = entries
        };
    }
}

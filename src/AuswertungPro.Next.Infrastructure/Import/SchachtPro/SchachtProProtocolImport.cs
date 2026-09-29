using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>Gemeinsame Datenuebernahme fuer Archiv und QR, mit bestehendem Handwert-/Reimportschutz.</summary>
internal static class SchachtProProtocolImport
{
    internal static void EnsureUniqueTarget(Project project, string key)
    {
        var matches = project.SchaechteData.Count(record => SchachtKeyFields.Any(field =>
            string.Equals(record.GetFieldValue(field).Trim(), key.Trim(), StringComparison.OrdinalIgnoreCase)));
        if (matches > 1) throw new InvalidDataException("Schachtnummer ist im Projekt mehrfach vorhanden. Bitte zuerst die Zuordnung klären.");
    }

    private static readonly string[] SchachtKeyFields =
    {
        "Schachtnummer",
        "SchachtNr",
        "Schacht",
        "Schacht-Nr",
        "Schacht Nummer",
        "Schacht ID",
        "Schacht-ID"
    };

    internal static SchachtRecord? Apply(ProtocolDto dto, bool isLite, string sproPath,
        Project project, ImportRunContext? ctx, List<string> messages,
        ref int found, ref int created, ref int updated, ref int uncertain,
        SchachtProProtocolMapper.MappedProtocol? prepared = null)
    {
        var schachtNr = dto.SchachtNr?.Trim();
        if (string.IsNullOrWhiteSpace(schachtNr))
        {
            uncertain++;
            messages.Add("Protokoll ohne Schachtnummer übersprungen.");
            return null;
        }

        var mapped = prepared ?? SchachtProProtocolMapper.Map(dto, isLite);
        uncertain += mapped.UnknownLabels.Count;
        foreach (var label in mapped.UnknownLabels)
            messages.Add($"Schacht {schachtNr}: unbekanntes Zustands-Label '{label}' als Klartext übernommen.");

        var record = FindSchachtRecord(project.SchaechteData, schachtNr);
        var isNew = record is null;
        if (record is null)
        {
            record = new SchachtRecord();
            if (ctx is null)
                project.SchaechteData.Add(record);
            else
                ctx.WithCollectionLock(() => project.SchaechteData.Add(record));
        }

        found++;

        // Schluessel-Felder immer (Konvention des PDF-Imports).
        record.SetFieldValue(SchachtProFieldNames.Schachtnummer, schachtNr, FieldSource.Spro, userEdited: false);
        record.SetFieldValue(SchachtProFieldNames.NrGross, schachtNr, FieldSource.Spro, userEdited: false);
        record.SetFieldValue(SchachtProFieldNames.NrKlein, schachtNr, FieldSource.Spro, userEdited: false);

        // Das Archiv fuellt und aktualisiert seine Felder, aber eine Handkorrektur in
        // SewerStudio bleibt stehen - auch bei einem versehentlich wiederholten Import.
        // Uebersprungene Felder werden gemeldet, sonst wundert man sich still.
        var geschuetzt = new List<string>();
        foreach (var (field, value) in mapped.Fields)
        {
            if (record.SetFieldValue(field, value, FieldSource.Spro, userEdited: false)
                == FeldSchreibErgebnis.HandwertGeschuetzt
                && !string.Equals(record.GetFieldValue(field), value ?? "", StringComparison.Ordinal))
            {
                geschuetzt.Add(field);
            }
        }

        if (geschuetzt.Count > 0)
        {
            messages.Add(
                $"Schacht {schachtNr}: {geschuetzt.Count} Feld(er) nicht übernommen, weil von Hand geändert - "
                + string.Join(", ", geschuetzt) + ".");
        }

        if (mapped.Anschluesse.Count > 0)
            record.SetzeAnschluesse(mapped.Anschluesse.ToList());

        if (mapped.Entries.Count > 0)
            ApplyProtocol(record, schachtNr, mapped.Entries, sproPath);

        if (isNew)
        {
            created++;
            messages.Add($"Schacht neu angelegt: {schachtNr}");
        }
        else
        {
            updated++;
        }
        return record;
    }

    /// <summary>
    /// Protokoll-Dokument in derselben Form wie der PDF-Import (SchachtProtocolApplier):
    /// Original-Revision mit Import-Kommentar + Arbeitskopie als Current.
    ///
    /// Re-Import-Schutz (Konvention wie VsaFindingProtocolSynchronizer: ein Import
    /// loescht keine Benutzerarbeit):
    /// - manuell hinzugefuegte oder veraenderte Eintraege der Arbeitskopie bleiben erhalten,
    /// - manuell geloeschte Import-Eintraege werden nicht wieder hinzugefuegt,
    /// - die bisherige Arbeitskopie wandert als Revision in die History,
    /// - ein inhaltsgleicher Re-Import laesst das Dokument komplett unangetastet
    ///   (EntryIds bleiben stabil, keine History-Flut).
    /// Der Abgleich ist inhaltsbasiert (Bauteil + Beschreibung), weil jeder Import
    /// neue EntryIds erzeugt.
    /// </summary>
    private static void ApplyProtocol(
        SchachtRecord record,
        string schachtNr,
        List<ProtocolEntry> entries,
        string sproPath)
    {
        var existing = record.Protocol;
        var oldOriginal = existing?.Original?.Entries;
        var oldCurrent = existing?.Current?.Entries;

        // Erstimport (oder leeres Bestandsprotokoll): frisches Dokument wie bisher.
        if (existing is null
            || (oldOriginal is null or { Count: 0 } && oldCurrent is null or { Count: 0 }))
        {
            record.Protocol = new ProtocolDocument
            {
                HaltungId = schachtNr,
                Original = new ProtocolRevision
                {
                    Comment = $"Import aus SchachtPro-{(Path.GetExtension(sproPath).Equals(".spro", StringComparison.OrdinalIgnoreCase) ? "Archiv" : "QR")}: {Path.GetFileName(sproPath)}",
                    Entries = entries.Select(CloneFresh).ToList()
                },
                Current = new ProtocolRevision
                {
                    Comment = "Arbeitskopie",
                    Entries = entries.Select(CloneFresh).ToList()
                }
            };
            return;
        }

        var originalEntries = oldOriginal ?? new List<ProtocolEntry>();
        var currentEntries = oldCurrent ?? new List<ProtocolEntry>();

        var originalKeys = KeySet(originalEntries.Where(e => !e.IsDeleted));
        var currentKeys = KeySet(currentEntries.Where(e => !e.IsDeleted));

        // Manuell geloescht: im bisherigen Original, aber nicht mehr in der Arbeitskopie.
        var deletedKeys = new HashSet<string>(originalKeys, StringComparer.Ordinal);
        deletedKeys.ExceptWith(currentKeys);

        // Manuell hinzugefuegt oder veraendert: in der Arbeitskopie, aber nicht im Original.
        // Identitaet (EntryId, Fotos, Metadaten) bleibt erhalten.
        var carryOvers = currentEntries
            .Where(e => !e.IsDeleted && !originalKeys.Contains(ContentKey(e)))
            .Select(ClonePreservingIdentity)
            .ToList();

        var mergedCurrent = entries
            .Where(e => !deletedKeys.Contains(ContentKey(e)))
            .Select(CloneFresh)
            .Concat(carryOvers)
            .ToList();

        // Inhaltsgleicher Re-Import (Netto-Ergebnis identisch zur bisherigen
        // Arbeitskopie, egal ob Uebernahmen/Loeschungen im Spiel waren):
        // Dokument unangetastet lassen â€” sonst wuerde jeder Re-Import nach einer
        // manuellen Aenderung erneut eine History-Revision erzeugen.
        if (SameContentSequence(currentEntries.Where(e => !e.IsDeleted), mergedCurrent))
        {
            return;
        }

        var history = new List<ProtocolRevision>(existing.History ?? new List<ProtocolRevision>());
        if (existing.Current is not null && currentEntries.Count > 0)
        {
            var archived = CloneRevision(existing.Current);
            archived.Comment = string.IsNullOrWhiteSpace(archived.Comment)
                ? "Vor SchachtPro-Re-Import"
                : $"{archived.Comment} (vor SchachtPro-Re-Import)";
            history.Add(archived);
        }

        record.Protocol = new ProtocolDocument
        {
            HaltungId = schachtNr,
            Original = new ProtocolRevision
            {
                Comment = $"Import aus SchachtPro-{(Path.GetExtension(sproPath).Equals(".spro", StringComparison.OrdinalIgnoreCase) ? "Archiv" : "QR")}: {Path.GetFileName(sproPath)}",
                Entries = entries.Select(CloneFresh).ToList()
            },
            Current = new ProtocolRevision
            {
                Comment = "Arbeitskopie",
                Entries = mergedCurrent
            },
            History = history
        };
    }

    /// <summary>Inhaltsschluessel eines Eintrags (Bauteil + Beschreibung).</summary>
    private static string ContentKey(ProtocolEntry entry)
        => $"{(entry.Code ?? string.Empty).Trim()}\u0001{(entry.Beschreibung ?? string.Empty).Trim()}";

    private static HashSet<string> KeySet(IEnumerable<ProtocolEntry> entries)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
            set.Add(ContentKey(entry));
        return set;
    }

    private static bool SameContentSequence(IEnumerable<ProtocolEntry> left, IReadOnlyList<ProtocolEntry> right)
        => left.Select(ContentKey).SequenceEqual(right.Select(ContentKey));

    /// <summary>Frische Import-Kopie (neue EntryId), wie bisher.</summary>
    private static ProtocolEntry CloneFresh(ProtocolEntry e) => new()
    {
        Code = e.Code,
        Beschreibung = e.Beschreibung,
        Source = e.Source
    };

    /// <summary>Volle Kopie unter Beibehaltung der EntryId (fuer Uebernahmen aus der Arbeitskopie).</summary>
    private static ProtocolEntry ClonePreservingIdentity(ProtocolEntry e) => new()
    {
        EntryId = e.EntryId,
        Code = e.Code,
        Beschreibung = e.Beschreibung,
        MeterStart = e.MeterStart,
        MeterEnd = e.MeterEnd,
        IsStreckenschaden = e.IsStreckenschaden,
        Mpeg = e.Mpeg,
        Zeit = e.Zeit,
        FotoPaths = new List<string>(e.FotoPaths),
        OriginalFotoPaths = new List<string>(e.OriginalFotoPaths ?? []),
        Source = e.Source,
        IsDeleted = e.IsDeleted,
        CodeMeta = e.CodeMeta,
        Ai = e.Ai,
        Training = ProtocolEntryCloner.CloneTrainingMeta(e.Training)
    };

    /// <summary>Archivierte Revision fuer die History (neue RevisionId, Eintraege identitaetswahrend).</summary>
    private static ProtocolRevision CloneRevision(ProtocolRevision revision) => new()
    {
        BasedOnRevisionId = revision.RevisionId,
        ImportFingerprint = revision.ImportFingerprint,
        ImportVideoPaths = revision.ImportVideoPaths?.ToList(),
        CreatedBy = revision.CreatedBy,
        Comment = revision.Comment,
        Entries = revision.Entries.Select(ClonePreservingIdentity).ToList(),
        Changes = new List<ProtocolChange>(revision.Changes)
    };

    private static SchachtRecord? FindSchachtRecord(IEnumerable<SchachtRecord> records, string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;

        foreach (var record in records)
        {
            foreach (var field in SchachtKeyFields)
            {
                var value = record.GetFieldValue(field);
                if (string.IsNullOrWhiteSpace(value))
                    continue;
                if (string.Equals(value.Trim(), key, StringComparison.OrdinalIgnoreCase))
                    return record;
            }
        }

        return null;
    }
}

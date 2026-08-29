using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import.SchachtPro;

/// <summary>
/// Importiert SchachtPro-Projektarchive (.spro = ZIP mit JSON) der Android-App
/// "SchachtPro" als Schacht-Records. Additiv zum bestehenden PDF-Import:
/// strukturierte Daten statt PDF-Parsing.
///
/// Stufe A: Stammdaten, Schachtaufbau, Anschluesse, GPS (LV95!), Fotos.
/// Stufe B: Zustandslabels werden per <see cref="SchachtProZustandMapper"/> auf
/// VSA-KEK/EN-13508-2 D-Codes abgebildet und als Protokoll-Eintraege abgelegt
/// (Bauteil-Namen in der Ordnung des PDF-Imports).
///
/// Fehlerstrategie: ein defektes Projekt/Protokoll bricht den Import nicht ab,
/// sondern wird als Fehler gezaehlt. Archiv-Level-Verstoesse (Zip-Slip, Limits,
/// Manifest ungueltig, zu neue Format-/Schema-Version) sind harte Fehler.
///
/// Fotos werden nur kopiert, wenn eine Datei-Staging-Sitzung am Kontext haengt
/// (echter UI-Importlauf); die Ablage erfolgt unter Fotos/Schächte/&lt;Schacht&gt;
/// und wird als relative Pfade im Feld "Fotos" verlinkt. Kundenoriginale (das
/// Archiv selbst) werden nie veraendert.
/// </summary>
public sealed class SchachtProImportService : ISchachtProImportService
{
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

    private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png"
    };

    public Result<ImportStats> ImportSchachtProArchive(string sproPath, Project project, ImportRunContext? ctx = null)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (string.IsNullOrWhiteSpace(sproPath) || !File.Exists(sproPath))
            return Result<ImportStats>.Fail("SPRO_MISSING", "SchachtPro-Archiv nicht gefunden.");

        ctx?.Log.AddEntry("SchachtPro", "Start", ImportLogStatus.Info, sourceFile: sproPath);

        var messages = new List<string>();
        var found = 0;
        var created = 0;
        var updated = 0;
        var errors = 0;
        var uncertain = 0;

        string? photoWorkDir = null;
        try
        {
            using var reader = SchachtProArchiveReader.Open(sproPath);
            var manifest = reader.ReadManifest();
            messages.Add(
                $"SchachtPro-Archiv: {manifest.Projects!.Count} Projekt(e), " +
                $"App {manifest.AppVersionName}, Format v{manifest.FormatVersion}, DB-Schema v{manifest.DbSchemaVersion}.");

            var staging = ctx?.FileStaging;
            if (staging is null)
                messages.Add("Hinweis: ohne Datei-Staging werden Fotos nicht ins Projekt kopiert (nur Vorschau/Direktaufruf).");

            var projectIndex = 0;
            foreach (var entry in manifest.Projects)
            {
                ctx?.CancellationToken.ThrowIfCancellationRequested();
                projectIndex++;
                ctx?.Progress?.Report(new ImportProgress(
                    "SchachtPro importieren",
                    projectIndex,
                    manifest.Projects.Count,
                    $"Projekt {projectIndex}/{manifest.Projects.Count}: {entry.Name}",
                    entry.Name));

                try
                {
                    ImportProject(
                        reader,
                        entry,
                        sproPath,
                        project,
                        staging,
                        ctx,
                        messages,
                        ref photoWorkDir,
                        ref found,
                        ref created,
                        ref updated,
                        ref errors,
                        ref uncertain);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    errors++;
                    messages.Add($"Fehler bei Projekt '{entry.Name}': {ex.Message}");
                    ctx?.Log.AddEntry("SchachtPro", "Projekt", ImportLogStatus.Error,
                        recordKey: entry.Name, sourceFile: sproPath, detail: ex.Message);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (SchachtProArchiveException ex)
        {
            ctx?.Log.AddEntry("SchachtPro", "Archiv", ImportLogStatus.Error,
                sourceFile: sproPath, detail: ex.Message);
            return Result<ImportStats>.Fail(ex.Code, ex.Message);
        }
        catch (Exception ex)
        {
            ctx?.Log.AddEntry("SchachtPro", "Archiv", ImportLogStatus.Error,
                sourceFile: sproPath, detail: ex.Message);
            return Result<ImportStats>.Fail("SPRO_READ_ERROR", $"Archiv konnte nicht gelesen werden: {ex.Message}");
        }
        finally
        {
            if (photoWorkDir is not null)
            {
                try { Directory.Delete(photoWorkDir, recursive: true); }
                catch { /* Arbeitsordner-Bereinigung ist best-effort */ }
            }
        }

        if (found > 0)
        {
            project.ModifiedAtUtc = DateTime.UtcNow;
            project.Dirty = true;
        }

        messages.Add($"SchachtPro: {found} Protokoll(e) verarbeitet, {created} Schaechte neu, {updated} aktualisiert, {errors} Fehler, {uncertain} unklar.");
        return Result<ImportStats>.Success(new ImportStats(found, created, updated, errors, uncertain, messages));
    }

    private static void ImportProject(
        SchachtProArchiveReader reader,
        ManifestProjectEntryDto entry,
        string sproPath,
        Project project,
        IImportFileStagingSession? staging,
        ImportRunContext? ctx,
        List<string> messages,
        ref string? photoWorkDir,
        ref int found,
        ref int created,
        ref int updated,
        ref int errors,
        ref int uncertain)
    {
        var exportId = entry.ExportId!;
        string? json;
        try
        {
            json = reader.ReadProjectJson(exportId);
        }
        catch (SchachtProArchiveException ex)
        {
            errors++;
            messages.Add($"Projekt '{entry.Name}' uebersprungen: {ex.Message}");
            return;
        }

        if (json is null)
        {
            errors++;
            messages.Add($"Projekt '{entry.Name}' uebersprungen: projects/{exportId}.json fehlt im Archiv.");
            return;
        }

        ProjectDto? projectDto;
        JsonElement protocolsElement;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var snapshotExportId = root.TryGetProperty("exportId", out var idElement)
                ? idElement.GetString()
                : null;
            if (!string.Equals(snapshotExportId, exportId, StringComparison.Ordinal))
            {
                errors++;
                messages.Add($"Projekt '{entry.Name}' uebersprungen: abweichende Export-ID im Projekt-JSON.");
                return;
            }

            projectDto = root.TryGetProperty("project", out var projectElement)
                ? JsonSerializer.Deserialize<ProjectDto>(projectElement.GetRawText(), SchachtProArchiveJson.Options)
                : null;
            if (projectDto?.Name is null || !root.TryGetProperty("protocols", out protocolsElement)
                                          || protocolsElement.ValueKind != JsonValueKind.Array)
            {
                errors++;
                messages.Add($"Projekt '{entry.Name}' uebersprungen: Projekt-Snapshot unvollstaendig.");
                return;
            }

            var isLite = string.Equals(projectDto.Mode, "LITE", StringComparison.OrdinalIgnoreCase);

            // Auftraggeber des Projekts als Projekt-Metadatum (nur leeres Feld fuellen).
            if (!string.IsNullOrWhiteSpace(projectDto.AuftraggeberName)
                && project.Metadata.TryGetValue(SchachtProFieldNames.ProjektMetadataAuftraggeber, out var existing)
                && string.IsNullOrWhiteSpace(existing))
            {
                project.Metadata[SchachtProFieldNames.ProjektMetadataAuftraggeber] = projectDto.AuftraggeberName.Trim();
            }

            var protocolIndex = 0;
            foreach (var protocolElement in protocolsElement.EnumerateArray())
            {
                ctx?.CancellationToken.ThrowIfCancellationRequested();
                var currentIndex = protocolIndex++;
                if (protocolElement.ValueKind != JsonValueKind.Object)
                {
                    errors++;
                    messages.Add($"Protokoll {currentIndex + 1} in '{entry.Name}' uebersprungen: kein JSON-Objekt.");
                    continue;
                }

                ProtocolDto? dto;
                try
                {
                    dto = JsonSerializer.Deserialize<ProtocolDto>(
                        protocolElement.GetRawText(), SchachtProArchiveJson.Options);
                }
                catch (JsonException ex)
                {
                    errors++;
                    messages.Add($"Protokoll {currentIndex + 1} in '{entry.Name}' uebersprungen: beschaedigt ({ex.Message}).");
                    continue;
                }

                if (dto is null)
                {
                    errors++;
                    messages.Add($"Protokoll {currentIndex + 1} in '{entry.Name}' uebersprungen: leer.");
                    continue;
                }

                try
                {
                    ImportProtocol(
                        reader,
                        dto,
                        currentIndex,
                        isLite,
                        sproPath,
                        project,
                        staging,
                        ctx,
                        messages,
                        ref photoWorkDir,
                        ref found,
                        ref created,
                        ref updated,
                        ref uncertain);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // Fehlerstrategie: ein defektes Protokoll bricht weder das
                    // Projekt noch den Gesamtimport ab.
                    errors++;
                    messages.Add($"Protokoll {currentIndex + 1} in '{entry.Name}' uebersprungen: {ex.Message}");
                    ctx?.Log.AddEntry("SchachtPro", "Protokoll", ImportLogStatus.Error,
                        recordKey: entry.Name, sourceFile: sproPath, detail: ex.Message);
                }
            }
        }
        catch (JsonException ex)
        {
            errors++;
            messages.Add($"Projekt '{entry.Name}' uebersprungen: Projekt-JSON beschaedigt ({ex.Message}).");
            ctx?.Log.AddEntry("SchachtPro", "ProjektJson", ImportLogStatus.Error,
                recordKey: entry.Name, sourceFile: sproPath, detail: ex.Message);
        }
    }

    private static void ImportProtocol(
        SchachtProArchiveReader reader,
        ProtocolDto dto,
        int protocolIndex,
        bool isLite,
        string sproPath,
        Project project,
        IImportFileStagingSession? staging,
        ImportRunContext? ctx,
        List<string> messages,
        ref string? photoWorkDir,
        ref int found,
        ref int created,
        ref int updated,
        ref int uncertain)
    {
        var schachtNr = dto.SchachtNr?.Trim();
        if (string.IsNullOrWhiteSpace(schachtNr))
        {
            uncertain++;
            messages.Add("Protokoll ohne Schachtnummer uebersprungen.");
            return;
        }

        var mapped = SchachtProProtocolMapper.Map(dto, isLite);
        uncertain += mapped.UnknownLabels.Count;
        foreach (var label in mapped.UnknownLabels)
            messages.Add($"Schacht {schachtNr}: unbekanntes Zustands-Label '{label}' als Klartext uebernommen.");

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
                $"Schacht {schachtNr}: {geschuetzt.Count} Feld(er) nicht uebernommen, weil von Hand geaendert - "
                + string.Join(", ", geschuetzt) + ".");
        }

        if (mapped.Entries.Count > 0)
            ApplyProtocol(record, schachtNr, mapped.Entries, sproPath);

        if (staging is not null && dto.Photos is { Count: > 0 })
        {
            CopyPhotos(reader, dto, protocolIndex, schachtNr, staging, record, messages, ref photoWorkDir);
        }

        if (isNew)
        {
            created++;
            messages.Add($"Schacht neu angelegt: {schachtNr}");
        }
        else
        {
            updated++;
        }
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
                    Comment = $"Import aus SchachtPro-Archiv: {Path.GetFileName(sproPath)}",
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
        // Dokument unangetastet lassen — sonst wuerde jeder Re-Import nach einer
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
                Comment = $"Import aus SchachtPro-Archiv: {Path.GetFileName(sproPath)}",
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
        CreatedBy = revision.CreatedBy,
        Comment = revision.Comment,
        Entries = revision.Entries.Select(ClonePreservingIdentity).ToList(),
        Changes = new List<ProtocolChange>(revision.Changes)
    };

    /// <summary>
    /// Kopiert die Protokoll-Fotos ueber die Staging-Sitzung nach
    /// Fotos/Schächte/&lt;Schacht&gt;/ und verlinkt sie relativ im Feld "Fotos".
    /// Fehlende oder ungueltige Foto-Referenzen werden gemeldet und uebersprungen.
    /// </summary>
    private static void CopyPhotos(
        SchachtProArchiveReader reader,
        ProtocolDto dto,
        int protocolIndex,
        string schachtNr,
        IImportFileStagingSession staging,
        SchachtRecord record,
        List<string> messages,
        ref string? photoWorkDir)
    {
        var targetDir = ProjectStructure.FotosSchachtDir(staging.ProjectRoot, schachtNr);
        var relativePaths = new List<string>();

        for (var photoIndex = 0; photoIndex < dto.Photos!.Count; photoIndex++)
        {
            var photo = dto.Photos[photoIndex];
            if (string.IsNullOrWhiteSpace(photo.ArchivePath))
            {
                messages.Add($"Schacht {schachtNr}: Foto ohne Archivpfad uebersprungen.");
                continue;
            }

            Stream? source;
            try
            {
                source = reader.OpenValidatedEntry(photo.ArchivePath, "photos");
            }
            catch (SchachtProArchiveException ex)
            {
                messages.Add($"Schacht {schachtNr}: Foto '{photo.ArchivePath}' abgelehnt ({ex.Message}).");
                continue;
            }

            if (source is null)
            {
                messages.Add($"Schacht {schachtNr}: Foto fehlt im Archiv ({photo.ArchivePath}).");
                continue;
            }

            var extension = Path.GetExtension(photo.ArchivePath);
            if (!PhotoExtensions.Contains(extension))
            {
                source.Dispose();
                messages.Add($"Schacht {schachtNr}: Foto-Typ '{extension}' nicht unterstuetzt ({photo.ArchivePath}).");
                continue;
            }

            try
            {
                photoWorkDir ??= Directory.CreateDirectory(
                    Path.Combine(Path.GetTempPath(), $"schachtpro-import-{Guid.NewGuid():N}")).FullName;

                // Dateiname aus dem Archiv uebernehmen (<protokollIdx>_<fotoIdx>.jpg) —
                // deterministisch, dadurch ist ein Re-Import idempotent (gleicher Inhalt
                // wird von der Staging-Sitzung als vorhanden erkannt).
                var tempFile = Path.Combine(photoWorkDir, $"{protocolIndex}_{photoIndex}{extension.ToLowerInvariant()}");
                using (source)
                using (var output = File.Create(tempFile))
                    source.CopyTo(output);

                var targetPath = staging.StageCopy(tempFile, targetDir);
                relativePaths.Add(ProjectPathResolver.MakeRelative(targetPath, staging.ProjectRoot));
            }
            catch (Exception ex)
            {
                messages.Add($"Schacht {schachtNr}: Foto '{photo.ArchivePath}' konnte nicht kopiert werden ({ex.Message}).");
            }
        }

        if (relativePaths.Count > 0
            && record.SetFieldValue(SchachtProFieldNames.Fotos, string.Join(";", relativePaths),
                   FieldSource.Spro, userEdited: false)
               == FeldSchreibErgebnis.HandwertGeschuetzt)
        {
            // Sonst lägen die Fotos auf der Platte und der Schacht zeigte nicht darauf.
            messages.Add(
                $"Schacht {schachtNr}: {relativePaths.Count} Foto(s) kopiert, aber das Feld "
                + "'Fotos' wurde von Hand geaendert und bleibt unveraendert.");
        }
    }

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

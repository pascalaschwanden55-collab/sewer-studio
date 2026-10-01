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
            var manifest = reader.ReadManifest(ctx?.CancellationToken ?? CancellationToken.None);
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
            messages.Add($"Projekt '{entry.Name}' übersprungen: {ex.Message}");
            return;
        }

        if (json is null)
        {
            errors++;
            messages.Add($"Projekt '{entry.Name}' übersprungen: projects/{exportId}.json fehlt im Archiv.");
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
                messages.Add($"Projekt '{entry.Name}' übersprungen: abweichende Export-ID im Projekt-JSON.");
                return;
            }

            projectDto = root.TryGetProperty("project", out var projectElement)
                ? JsonSerializer.Deserialize<ProjectDto>(projectElement.GetRawText(), SchachtProArchiveJson.Options)
                : null;
            if (projectDto?.Name is null || !root.TryGetProperty("protocols", out protocolsElement)
                                          || protocolsElement.ValueKind != JsonValueKind.Array)
            {
                errors++;
                messages.Add($"Projekt '{entry.Name}' übersprungen: Projekt-Snapshot unvollständig.");
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
                    messages.Add($"Protokoll {currentIndex + 1} in '{entry.Name}' übersprungen: kein JSON-Objekt.");
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
                    messages.Add($"Protokoll {currentIndex + 1} in '{entry.Name}' übersprungen: beschädigt ({ex.Message}).");
                    continue;
                }

                if (dto is null)
                {
                    errors++;
                    messages.Add($"Protokoll {currentIndex + 1} in '{entry.Name}' übersprungen: leer.");
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
                    messages.Add($"Protokoll {currentIndex + 1} in '{entry.Name}' übersprungen: {ex.Message}");
                    ctx?.Log.AddEntry("SchachtPro", "Protokoll", ImportLogStatus.Error,
                        recordKey: entry.Name, sourceFile: sproPath, detail: ex.Message);
                }
            }
        }
        catch (JsonException ex)
        {
            errors++;
            messages.Add($"Projekt '{entry.Name}' übersprungen: Projekt-JSON beschädigt ({ex.Message}).");
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
        var record = SchachtProProtocolImport.Apply(dto, isLite, sproPath, project, ctx,
            messages, ref found, ref created, ref updated, ref uncertain);
        if (record is not null && staging is not null && (dto.Photos is { Count: > 0 }
            || !string.IsNullOrWhiteSpace(dto.ConnectionPhoto?.PhotoPath)))
            CopyPhotos(reader, dto, protocolIndex, dto.SchachtNr!.Trim(), staging, record, messages, ref photoWorkDir);
    }

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
        var photos = dto.Photos?.ToList() ?? [];
        var hasConnectionPhoto = !string.IsNullOrWhiteSpace(dto.ConnectionPhoto?.PhotoPath);
        if (hasConnectionPhoto)
            photos.Add(new PhotoDto { ArchivePath = dto.ConnectionPhoto!.PhotoPath });

        for (var photoIndex = 0; photoIndex < photos.Count; photoIndex++)
        {
            var photo = photos[photoIndex];
            var isConnectionPhoto = hasConnectionPhoto && photoIndex == photos.Count - 1;
            if (string.IsNullOrWhiteSpace(photo.ArchivePath))
            {
                messages.Add($"Schacht {schachtNr}: Foto ohne Archivpfad übersprungen.");
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
                messages.Add($"Schacht {schachtNr}: Foto-Typ '{extension}' nicht unterstützt ({photo.ArchivePath}).");
                continue;
            }

            try
            {
                photoWorkDir ??= Directory.CreateDirectory(
                    Path.Combine(Path.GetTempPath(), $"schachtpro-import-{Guid.NewGuid():N}")).FullName;

                // Dateiname aus dem Archiv uebernehmen (<protokollIdx>_<fotoIdx>.jpg) —
                // deterministisch, dadurch ist ein Re-Import idempotent (gleicher Inhalt
                // wird von der Staging-Sitzung als vorhanden erkannt).
                var photoName = isConnectionPhoto ? $"{protocolIndex}_connection" : $"{protocolIndex}_{photoIndex}";
                var tempFile = Path.Combine(photoWorkDir, photoName + extension.ToLowerInvariant());
                using (source)
                using (var output = File.Create(tempFile))
                    source.CopyTo(output);

                var targetPath = staging.StageCopy(tempFile, targetDir);
                relativePaths.Add(ProjectPathResolver.MakeRelative(targetPath, staging.ProjectRoot));
                if (isConnectionPhoto)
                    messages.Add($"Schacht {schachtNr}: Anschlussfoto als Original kopiert. " +
                        "Ausrichtung und Schachtgrafik-Überlagerung aus SchachtPro werden nicht übernommen.");
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
                + "'Fotos' wurde von Hand geändert und bleibt unverändert.");
        }
    }

}

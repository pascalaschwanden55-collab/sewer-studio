using System.Text;
using System.Text.Json;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Schreibt die drei Berichtsdateien eines Importlaufs atomar auf den Datentraeger.
/// </summary>
public sealed class ImportRunReportFileExporter : IImportRunReportExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Export(ImportRunLog log, string reportDirectory)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportDirectory);

        Directory.CreateDirectory(reportDirectory);
        var stamp = log.StartedAtUtc.ToString("yyyyMMdd_HHmmss");
        var id = log.RunId;
        var baseName = $"run_{id}_{stamp}";
        var textPath = Path.Combine(reportDirectory, $"{baseName}.txt");
        var jsonPath = Path.Combine(reportDirectory, $"{baseName}.json");
        var errorPath = Path.Combine(reportDirectory, $"fehlerliste_{id}.txt");

        WriteTextReport(log, textPath);
        WriteJsonReport(log, jsonPath);
        WriteErrorReport(log, errorPath);
        return textPath;
    }

    private static void WriteTextReport(ImportRunLog log, string path)
    {
        var text = new StringBuilder();
        text.AppendLine("=== IMPORT-BERICHT ===");
        text.AppendLine($"RunId:       {log.RunId}");
        text.AppendLine($"Import-Typ:  {log.ImportType}");
        text.AppendLine($"Quelle:      {log.SourcePath ?? "(nicht angegeben)"}");
        text.AppendLine($"Gestartet:   {log.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        text.AppendLine($"Beendet:     {log.CompletedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(laeuft)"} UTC");
        text.AppendLine($"Dauer:       {log.Duration:hh\\:mm\\:ss\\.fff}");
        text.AppendLine($"Dry-Run:     {(log.WasDryRun ? "Ja" : "Nein")}");
        text.AppendLine($"Abgebrochen: {(log.WasCancelled ? "Ja" : "Nein")}");
        text.AppendLine();
        text.AppendLine("--- Zusammenfassung ---");
        text.AppendLine($"Erstellt:    {log.TotalCreated}");
        text.AppendLine($"Aktualisiert:{log.TotalUpdated}");
        text.AppendLine($"Uebersprungen:{log.TotalSkipped}");
        text.AppendLine($"Konflikte:   {log.TotalConflicts}");
        text.AppendLine($"Fehler:      {log.TotalErrors}");
        text.AppendLine();

        var conflicts = log.EntriesList.Where(entry => entry.Status == ImportLogStatus.Conflict).ToList();
        if (conflicts.Count > 0)
        {
            text.AppendLine("--- Konflikte ---");
            foreach (var conflict in conflicts)
                text.AppendLine($"  [{conflict.RecordKey}] {conflict.Field}: {conflict.Detail}");
            text.AppendLine();
        }

        var errors = log.EntriesList.Where(entry => entry.Status == ImportLogStatus.Error).ToList();
        if (errors.Count > 0)
        {
            text.AppendLine("--- Fehler ---");
            foreach (var error in errors)
                text.AppendLine($"  [{error.RecordKey ?? error.SourceFile}] {error.Operation}: {error.Detail}");
            text.AppendLine();
        }

        text.AppendLine("--- Alle Eintraege ---");
        foreach (var entry in log.EntriesList)
        {
            text.AppendLine(
                $"  {entry.TimestampUtc:HH:mm:ss.fff} [{entry.Status,-8}] {entry.Phase}/{entry.Operation} " +
                $"{entry.RecordKey ?? ""} {entry.Field ?? ""} {entry.Detail ?? ""}".TrimEnd());
        }

        AtomicTextFileWriter.WriteAllText(path, text.ToString());
    }

    private static void WriteJsonReport(ImportRunLog log, string path)
    {
        var report = new
        {
            log.RunId,
            log.ImportType,
            log.SourcePath,
            log.StartedAtUtc,
            log.CompletedAtUtc,
            DurationMs = (int)log.Duration.TotalMilliseconds,
            log.WasDryRun,
            log.WasCancelled,
            Summary = new
            {
                log.TotalCreated,
                log.TotalUpdated,
                log.TotalSkipped,
                log.TotalConflicts,
                log.TotalErrors
            },
            Entries = log.EntriesList.Select(entry => new
            {
                entry.TimestampUtc,
                entry.Phase,
                entry.Operation,
                entry.SourceFile,
                entry.TargetPath,
                entry.RecordKey,
                entry.Field,
                Status = entry.Status.ToString(),
                entry.Detail
            })
        };

        AtomicTextFileWriter.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
    }

    private static void WriteErrorReport(ImportRunLog log, string path)
    {
        var issues = log.EntriesList
            .Where(entry => entry.Status is ImportLogStatus.Error or ImportLogStatus.Conflict)
            .ToList();

        if (issues.Count == 0)
        {
            AtomicTextFileWriter.WriteAllText(path, "Keine Fehler oder Konflikte.\n");
            return;
        }

        var text = new StringBuilder();
        text.AppendLine($"=== FEHLERLISTE Import {log.RunId} ({log.ImportType}) ===");
        text.AppendLine($"Zeitpunkt: {log.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        text.AppendLine($"Fehler: {log.TotalErrors}, Konflikte: {log.TotalConflicts}");
        text.AppendLine();

        foreach (var entry in issues)
        {
            var tag = entry.Status == ImportLogStatus.Error ? "FEHLER" : "KONFLIKT";
            text.AppendLine($"[{tag}] {entry.Phase}/{entry.Operation}");
            if (!string.IsNullOrWhiteSpace(entry.RecordKey))
                text.AppendLine($"  Haltung: {entry.RecordKey}");
            if (!string.IsNullOrWhiteSpace(entry.Field))
                text.AppendLine($"  Feld:    {entry.Field}");
            if (!string.IsNullOrWhiteSpace(entry.Detail))
                text.AppendLine($"  Detail:  {entry.Detail}");
            text.AppendLine();
        }

        AtomicTextFileWriter.WriteAllText(path, text.ToString());
    }
}

using System.Text;
using System.Text.Json;

namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Erzeugt Berichte fuer einen Import-Lauf in __IMPORT_REPORTS/.
/// </summary>
public static class ImportRunReportExporter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Exportiert drei Dateien:
    /// - run_{id}_{timestamp}.txt  (Menschenlesbar)
    /// - run_{id}_{timestamp}.json (Maschinenlesbar)
    /// - fehlerliste_{id}.txt      (Nur Fehler/Konflikte)
    /// </summary>
    public static string Export(ImportRunLog log, string reportDir)
    {
        Directory.CreateDirectory(reportDir);
        var stamp = log.StartedAtUtc.ToString("yyyyMMdd_HHmmss");
        var id = log.RunId;

        var baseName = $"run_{id}_{stamp}";
        var txtPath = Path.Combine(reportDir, $"{baseName}.txt");
        var jsonPath = Path.Combine(reportDir, $"{baseName}.json");
        var errorPath = Path.Combine(reportDir, $"fehlerliste_{id}.txt");

        WriteTextReport(log, txtPath);
        WriteJsonReport(log, jsonPath);
        WriteErrorReport(log, errorPath);

        return txtPath;
    }

    private static void WriteTextReport(ImportRunLog log, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== IMPORT-BERICHT ===");
        sb.AppendLine($"RunId:       {log.RunId}");
        sb.AppendLine($"Import-Typ:  {log.ImportType}");
        sb.AppendLine($"Quelle:      {log.SourcePath ?? "(nicht angegeben)"}");
        sb.AppendLine($"Gestartet:   {log.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Beendet:     {log.CompletedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(laeuft)"} UTC");
        sb.AppendLine($"Dauer:       {log.Duration:hh\\:mm\\:ss\\.fff}");
        sb.AppendLine($"Dry-Run:     {(log.WasDryRun ? "Ja" : "Nein")}");
        sb.AppendLine($"Abgebrochen: {(log.WasCancelled ? "Ja" : "Nein")}");
        sb.AppendLine();
        sb.AppendLine("--- Zusammenfassung ---");
        sb.AppendLine($"Erstellt:    {log.TotalCreated}");
        sb.AppendLine($"Aktualisiert:{log.TotalUpdated}");
        sb.AppendLine($"Uebersprungen:{log.TotalSkipped}");
        sb.AppendLine($"Konflikte:   {log.TotalConflicts}");
        sb.AppendLine($"Fehler:      {log.TotalErrors}");
        sb.AppendLine();

        var conflicts = log.EntriesList.Where(e => e.Status == ImportLogStatus.Conflict).ToList();
        if (conflicts.Count > 0)
        {
            sb.AppendLine("--- Konflikte ---");
            foreach (var c in conflicts)
            {
                sb.AppendLine($"  [{c.RecordKey}] {c.Field}: {c.Detail}");
            }
            sb.AppendLine();
        }

        var errors = log.EntriesList.Where(e => e.Status == ImportLogStatus.Error).ToList();
        if (errors.Count > 0)
        {
            sb.AppendLine("--- Fehler ---");
            foreach (var e in errors)
            {
                sb.AppendLine($"  [{e.RecordKey ?? e.SourceFile}] {e.Operation}: {e.Detail}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("--- Alle Eintraege ---");
        foreach (var entry in log.EntriesList)
        {
            sb.AppendLine($"  {entry.TimestampUtc:HH:mm:ss.fff} [{entry.Status,-8}] {entry.Phase}/{entry.Operation} " +
                          $"{entry.RecordKey ?? ""} {entry.Field ?? ""} {entry.Detail ?? ""}".TrimEnd());
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }

    private static void WriteJsonReport(ImportRunLog log, string path)
    {
        var dto = new
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
            Entries = log.EntriesList.Select(e => new
            {
                e.TimestampUtc,
                e.Phase,
                e.Operation,
                e.SourceFile,
                e.TargetPath,
                e.RecordKey,
                e.Field,
                Status = e.Status.ToString(),
                e.Detail
            })
        };

        var json = JsonSerializer.Serialize(dto, JsonOpts);
        File.WriteAllText(path, json, Encoding.UTF8);
    }

    private static void WriteErrorReport(ImportRunLog log, string path)
    {
        var issues = log.EntriesList
            .Where(e => e.Status is ImportLogStatus.Error or ImportLogStatus.Conflict)
            .ToList();

        if (issues.Count == 0)
        {
            File.WriteAllText(path, "Keine Fehler oder Konflikte.\n", Encoding.UTF8);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== FEHLERLISTE Import {log.RunId} ({log.ImportType}) ===");
        sb.AppendLine($"Zeitpunkt: {log.StartedAtUtc:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Fehler: {log.TotalErrors}, Konflikte: {log.TotalConflicts}");
        sb.AppendLine();

        foreach (var e in issues)
        {
            var tag = e.Status == ImportLogStatus.Error ? "FEHLER" : "KONFLIKT";
            sb.AppendLine($"[{tag}] {e.Phase}/{e.Operation}");
            if (!string.IsNullOrWhiteSpace(e.RecordKey))
                sb.AppendLine($"  Haltung: {e.RecordKey}");
            if (!string.IsNullOrWhiteSpace(e.Field))
                sb.AppendLine($"  Feld:    {e.Field}");
            if (!string.IsNullOrWhiteSpace(e.Detail))
                sb.AppendLine($"  Detail:  {e.Detail}");
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
    }
}

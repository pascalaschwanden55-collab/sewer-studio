using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Devis;

/// <summary>
/// Importiert vorgefertigte Marktdaten-JSONs aus einem Quellverzeichnis (z.B. Knowledge/sanierung/)
/// in das Config-Verzeichnis der App. Aktualisiert anschliessend SubmissionsPositionService und
/// HistorischeSanierungenService mit Cache-Invalidation.
///
/// Verwendung:
/// - User waehlt Quellordner (typischerweise Knowledge/sanierung/ nach Python-Reparser-Lauf)
/// - Service kopiert die 3 JSON-Files (marktpreise, submission_positionen, historische_sanierungen)
/// - Services laden frisch beim naechsten Lookup
/// </summary>
public sealed class MarktdatenImportService
{
    private readonly string _configDir;
    private readonly SubmissionsPositionService _submissions;
    private readonly HistorischeSanierungenService _historie;

    public static readonly string[] ExpectedFiles =
    {
        "submission_positionen.json",
        "historische_sanierungen.json",
        "marktpreise_burglen_2026.json", // optional - rein fuer Diagnose, nicht direkt geladen
    };

    public MarktdatenImportService(
        string configDir,
        SubmissionsPositionService submissions,
        HistorischeSanierungenService historie)
    {
        _configDir = configDir;
        _submissions = submissions;
        _historie = historie;
    }

    /// <summary>Pruef-Report vor dem Import: welche Dateien sind im Quellordner verfuegbar?</summary>
    public ImportPreview PreviewImport(string sourceDir)
    {
        var preview = new ImportPreview { SourceDir = sourceDir };
        foreach (var file in ExpectedFiles)
        {
            var path = Path.Combine(sourceDir, file);
            if (File.Exists(path))
            {
                preview.AvailableFiles.Add(file);
                preview.FileInfos[file] = new FileInfo(path);
                if (file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        using var doc = JsonDocument.Parse(json);
                        // Anzahl Records grob ermitteln
                        if (doc.RootElement.TryGetProperty("haltungen", out var ha) && ha.ValueKind == JsonValueKind.Array)
                            preview.RecordCounts[file] = ha.GetArrayLength();
                        else if (doc.RootElement.TryGetProperty("blocks", out var bl) && bl.ValueKind == JsonValueKind.Array)
                            preview.RecordCounts[file] = bl.GetArrayLength();
                        else if (doc.RootElement.TryGetProperty("marktpreise", out var mp) && mp.ValueKind == JsonValueKind.Array)
                            preview.RecordCounts[file] = mp.GetArrayLength();
                    }
                    catch { /* JSON-Diagnose optional */ }
                }
            }
            else
            {
                preview.MissingFiles.Add(file);
            }
        }
        return preview;
    }

    /// <summary>Fuehrt den Import aus: kopiert verfuegbare JSON-Files, invalidiert Service-Caches.</summary>
    public ImportResult Import(string sourceDir)
    {
        var result = new ImportResult();
        if (!Directory.Exists(sourceDir))
        {
            result.Errors.Add($"Quellverzeichnis nicht gefunden: {sourceDir}");
            return result;
        }

        Directory.CreateDirectory(_configDir);

        // Nur die 2 von Services genutzten JSONs in Config kopieren.
        // Die marktpreise_burglen_2026.json ist nur Quelle fuer den Reparser, nicht direkt geladen.
        var copyTargets = new[] { "submission_positionen.json", "historische_sanierungen.json" };
        foreach (var file in copyTargets)
        {
            var src = Path.Combine(sourceDir, file);
            var dst = Path.Combine(_configDir, file);
            if (!File.Exists(src))
            {
                result.SkippedFiles.Add(file);
                continue;
            }
            try
            {
                // Backup der bestehenden Datei
                if (File.Exists(dst))
                {
                    var bak = dst + $".bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                    File.Copy(dst, bak, overwrite: true);
                    result.BackupFiles.Add(Path.GetFileName(bak));
                }
                File.Copy(src, dst, overwrite: true);
                result.ImportedFiles.Add(file);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{file}: {ex.Message}");
            }
        }

        // Service-Caches invalidieren -> naechster Lookup liest die neuen JSONs
        _submissions.Invalidate();
        _historie.Invalidate();
        result.CachesInvalidated = true;

        return result;
    }
}

public sealed class ImportPreview
{
    public string SourceDir { get; set; } = "";
    public List<string> AvailableFiles { get; } = new();
    public List<string> MissingFiles { get; } = new();
    public Dictionary<string, FileInfo> FileInfos { get; } = new();
    public Dictionary<string, int> RecordCounts { get; } = new();
}

public sealed class ImportResult
{
    public List<string> ImportedFiles { get; } = new();
    public List<string> SkippedFiles { get; } = new();
    public List<string> BackupFiles { get; } = new();
    public List<string> Errors { get; } = new();
    public bool CachesInvalidated { get; set; }
}

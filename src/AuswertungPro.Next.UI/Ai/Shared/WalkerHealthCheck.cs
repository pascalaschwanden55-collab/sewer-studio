using System;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;
using AuswertungPro.Next.UI.Ai.Pipeline;
using Microsoft.Data.Sqlite;

namespace AuswertungPro.Next.UI.Ai.Shared;

/// <summary>
/// Ein-Klick Health-Check fuer die gesamte KI-Infrastruktur.
/// Prueft: Python-Venv, Ollama, Sidecar (Port 8100), Modelle, VRAM, SQLite-KB, ffmpeg.
/// Ideal nach Reboots oder vor Batch-Laeufen.
/// </summary>
public sealed class WalkerHealthCheck
{
    public sealed record CheckResult(string Component, bool Ok, string Detail);

    public sealed record HealthReport(
        IReadOnlyList<CheckResult> Results,
        int Passed,
        int Failed,
        TimeSpan Duration)
    {
        public bool AllGreen => Failed == 0;
    }

    /// <summary>Fuehrt alle Health-Checks sequentiell aus.</summary>
    public static async Task<HealthReport> RunAllAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var results = new List<CheckResult>();

        results.Add(CheckPythonVenv());
        results.Add(CheckFfmpeg());
        results.Add(await CheckOllamaAsync(ct));
        results.Add(await CheckSidecarAsync(ct));
        results.Add(await CheckOllamaModelsAsync(ct));
        results.Add(CheckVramBudget());
        results.Add(CheckKnowledgeBase());

        sw.Stop();
        int passed = 0, failed = 0;
        foreach (var r in results)
        {
            if (r.Ok) passed++;
            else failed++;
        }

        return new HealthReport(results, passed, failed, sw.Elapsed);
    }

    // ── Einzelne Checks ──────────────────────────────────────────────

    /// <summary>Prueft ob das Python-Venv existiert und python.exe darin liegt.</summary>
    public static CheckResult CheckPythonVenv()
    {
        var sidecarDir = FindSidecarDir();
        if (sidecarDir == null)
            return new("Python Venv", false, "sidecar/ Verzeichnis nicht gefunden");

        var venvPython = Path.Combine(sidecarDir, ".venv", "Scripts", "python.exe");
        if (!File.Exists(venvPython))
            venvPython = Path.Combine(sidecarDir, ".venv", "bin", "python");

        return File.Exists(venvPython)
            ? new("Python Venv", true, venvPython)
            : new("Python Venv", false, $"python nicht gefunden in {Path.GetDirectoryName(venvPython)}");
    }

    /// <summary>Prueft ob ffmpeg erreichbar ist.</summary>
    public static CheckResult CheckFfmpeg()
    {
        bool available = FfmpegLocator.IsFfmpegAvailable();
        var path = FfmpegLocator.ResolveFfmpeg();
        return new("ffmpeg", available, available ? path : $"Nicht erreichbar: {path}");
    }

    /// <summary>Prueft ob Ollama auf dem konfigurierten Port antwortet.</summary>
    public static async Task<CheckResult> CheckOllamaAsync(CancellationToken ct = default)
    {
        try
        {
            var config = Ollama.OllamaConfig.Load();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var resp = await http.GetAsync(new Uri(config.BaseUri, "/api/tags"), ct);
            return resp.IsSuccessStatusCode
                ? new("Ollama", true, config.BaseUri.ToString())
                : new("Ollama", false, $"HTTP {(int)resp.StatusCode} auf {config.BaseUri}");
        }
        catch (Exception ex)
        {
            return new("Ollama", false, ex.Message);
        }
    }

    /// <summary>Prueft ob der Sidecar (FastAPI) auf Port 8100 antwortet.</summary>
    public static async Task<CheckResult> CheckSidecarAsync(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var client = new VisionPipelineClient(new Uri("http://127.0.0.1:8100"), http);
            var health = await client.HealthCheckAsync(ct);

            if (health == null)
                return new("Sidecar (8100)", false, "Nicht erreichbar");

            var gpu = health.Gpu != null ? $"GPU: {health.Gpu.CurrentModel}, {health.Gpu.VramAllocatedGb:F1}/{health.Gpu.VramTotalGb:F1} GB" : "GPU: unbekannt";
            return health.Status == "ok"
                ? new("Sidecar (8100)", true, $"OK — {gpu}, v{health.Version}")
                : new("Sidecar (8100)", false, $"Status: {health.Status}");
        }
        catch (Exception ex)
        {
            return new("Sidecar (8100)", false, ex.Message);
        }
    }

    /// <summary>Prueft ob die erwarteten Ollama-Modelle geladen sind.</summary>
    public static async Task<CheckResult> CheckOllamaModelsAsync(CancellationToken ct = default)
    {
        try
        {
            var config = Ollama.OllamaConfig.Load();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var resp = await http.GetAsync(new Uri(config.BaseUri, "/api/tags"), ct);
            if (!resp.IsSuccessStatusCode)
                return new("Ollama Modelle", false, "Ollama nicht erreichbar");

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);

            var models = new List<string>();
            if (doc.RootElement.TryGetProperty("models", out var arr))
            {
                foreach (var m in arr.EnumerateArray())
                {
                    if (m.TryGetProperty("name", out var name))
                        models.Add(name.GetString() ?? "?");
                }
            }

            bool hasVision = models.Exists(m => m.Contains("qwen", StringComparison.OrdinalIgnoreCase));
            bool hasEmbed = models.Exists(m => m.Contains("nomic", StringComparison.OrdinalIgnoreCase)
                                              || m.Contains("embed", StringComparison.OrdinalIgnoreCase));

            var missing = new List<string>();
            if (!hasVision) missing.Add("Vision-Modell (qwen)");
            if (!hasEmbed) missing.Add("Embed-Modell (nomic)");

            return missing.Count == 0
                ? new("Ollama Modelle", true, $"{models.Count} Modelle: {string.Join(", ", models)}")
                : new("Ollama Modelle", false, $"Fehlt: {string.Join(", ", missing)} — Vorhanden: {string.Join(", ", models)}");
        }
        catch (Exception ex)
        {
            return new("Ollama Modelle", false, ex.Message);
        }
    }

    /// <summary>Prueft VRAM via nvidia-smi (nur auf Windows mit NVIDIA GPU).</summary>
    public static CheckResult CheckVramBudget()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=memory.used,memory.total --format=csv,noheader,nounits",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc == null)
                return new("VRAM", false, "nvidia-smi nicht gefunden");

            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            if (string.IsNullOrEmpty(output))
                return new("VRAM", false, "nvidia-smi gab keine Daten");

            var parts = output.Split(',');
            if (parts.Length >= 2
                && int.TryParse(parts[0].Trim(), out int usedMb)
                && int.TryParse(parts[1].Trim(), out int totalMb))
            {
                int freeMb = totalMb - usedMb;
                bool ok = freeMb >= 2048; // Mindestens 2 GB frei
                return new("VRAM", ok,
                    $"{usedMb:N0}/{totalMb:N0} MB belegt — {freeMb:N0} MB frei" +
                    (ok ? "" : " (< 2 GB frei!)"));
            }

            return new("VRAM", true, output);
        }
        catch (Exception ex)
        {
            return new("VRAM", false, ex.Message);
        }
    }

    /// <summary>Prueft SQLite-KB-Integritaet (Tabellen, Sample-Anzahl).</summary>
    public static CheckResult CheckKnowledgeBase()
    {
        try
        {
            var dbPath = KnowledgeBaseContext.DefaultDbPath;
            if (!File.Exists(dbPath))
                return new("KnowledgeBase", false, $"DB nicht gefunden: {dbPath}");

            using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            conn.Open();

            // Integritaets-Check
            using var intCmd = conn.CreateCommand();
            intCmd.CommandText = "PRAGMA integrity_check;";
            var integrity = intCmd.ExecuteScalar()?.ToString();
            if (integrity != "ok")
                return new("KnowledgeBase", false, $"Integritaetsfehler: {integrity}");

            // Sample-Anzahl
            using var countCmd = conn.CreateCommand();
            countCmd.CommandText = "SELECT COUNT(*) FROM Samples;";
            var count = Convert.ToInt64(countCmd.ExecuteScalar());

            // Embedding-Anzahl
            using var embCmd = conn.CreateCommand();
            embCmd.CommandText = "SELECT COUNT(*) FROM Embeddings;";
            var embCount = Convert.ToInt64(embCmd.ExecuteScalar());

            return new("KnowledgeBase", true,
                $"{count} Samples, {embCount} Embeddings — {new FileInfo(dbPath).Length / 1024:N0} KB");
        }
        catch (Exception ex)
        {
            return new("KnowledgeBase", false, ex.Message);
        }
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────

    private static string? FindSidecarDir()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // Typisch: src/AuswertungPro.Next.UI/bin/Debug/net10.0-windows/ → 5 Ebenen hoch → sidecar/
        var candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "sidecar"));
        if (Directory.Exists(candidate)) return candidate;

        // Fallback: neben der Solution
        candidate = Path.GetFullPath(Path.Combine(baseDir, "..", "sidecar"));
        if (Directory.Exists(candidate)) return candidate;

        return null;
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Import.WinCan;

/// <summary>
/// Konvertiert eine WinCan-SDF-Datei (SQL Server Compact 4.0) in eine SQLite-.db3
/// mit identischem Schema (SECTION/SECINSP/SECOBS/...). Die resultierende .db3 ist
/// direkt vom InspectionProfileExtractor lesbar.
///
/// Implementierung: .NET 10 kann die SSCE-4.0-Assembly (.NET-Framework) nicht per
/// Reflection laden, daher rufen wir zwei Helper-Scripts unter ..\tools\sdf-convert\
/// auf:
///   1. convert_sdf_to_db3.ps1  →  SSCE-Lesen via Windows-PowerShell (.NET 4.x)
///      exportiert alle WinCan-Tabellen als JSON nach %TEMP%
///   2. sdf_json_to_sqlite.py   →  schreibt aus dem JSON eine SQLite-.db3
///
/// Voraussetzungen:
///   - SSCE 4.0 Runtime installiert (System.Data.SqlServerCe.dll)
///   - Windows-PowerShell (powershell.exe) im PATH (standardmaessig vorhanden)
///   - Python 3 im PATH (fuer den JSON-zu-SQLite-Schritt)
/// </summary>
public static class SdfToSqliteConverter
{
    private static readonly string[] SsceDllCandidates =
    {
        @"C:\Program Files\Microsoft SQL Server Compact Edition\v4.0\Desktop\System.Data.SqlServerCe.dll",
        @"C:\Program Files (x86)\Microsoft SQL Server Compact Edition\v4.0\Desktop\System.Data.SqlServerCe.dll",
    };

    /// <summary>True wenn der SSCE-Runtime-Assembly auf diesem Rechner vorhanden ist.</summary>
    public static bool IsSsceAvailable() => SsceDllCandidates.Any(File.Exists);

    /// <summary>
    /// Konvertiert die SDF in eine SQLite .db3. Standard-Ziel:
    /// C:\KI_BRAIN\sdf_converted\{SdfName}.db3
    /// CancellationToken bricht laufende PowerShell-/Python-Prozesse hart ab.
    /// </summary>
    public static string Convert(string sdfPath, string? outputPath = null, CancellationToken ct = default)
    {
        if (!File.Exists(sdfPath))
            throw new FileNotFoundException("SDF nicht gefunden", sdfPath);

        if (!IsSsceAvailable())
            throw new InvalidOperationException(
                "SSCE 4.0 Runtime nicht gefunden. Installieren via 'Microsoft SQL Server Compact 4.0 SP1' (SSCERuntime_x64-ENU.exe).");

        outputPath ??= Path.Combine(@"C:\KI_BRAIN\sdf_converted",
            Path.GetFileNameWithoutExtension(sdfPath) + ".db3");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var toolsDir = FindToolsDir();
        if (toolsDir is null)
            throw new InvalidOperationException(
                "Helper-Scripts nicht gefunden. Erwartet: <Repo>\\tools\\sdf-convert\\convert_sdf_to_db3.ps1");

        var ps1 = Path.Combine(toolsDir, "convert_sdf_to_db3.ps1");
        var py = Path.Combine(toolsDir, "sdf_json_to_sqlite.py");

        ct.ThrowIfCancellationRequested();

        // Schritt 1: PowerShell-Export → JSON-Pfad auf stdout
        var jsonPath = RunPowerShell(ps1, sdfPath, ct);
        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
            throw new InvalidOperationException(
                $"PowerShell-Konvertierung lieferte keine JSON-Datei. Letzter stdout: '{jsonPath}'");

        ct.ThrowIfCancellationRequested();

        // Schritt 2: Python → SQLite
        try { RunPython(py, jsonPath, outputPath, ct); }
        finally { try { File.Delete(jsonPath); } catch { /* best-effort */ } }

        if (!File.Exists(outputPath))
            throw new InvalidOperationException("SQLite-Ausgabe wurde nicht erzeugt.");

        return outputPath;
    }

    private static string? FindToolsDir()
    {
        // Starte bei BaseDirectory und wandere Richtung Repo-Root bis "tools\sdf-convert" gefunden.
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir.FullName, "tools", "sdf-convert");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        return null;
    }

    private static string RunPowerShell(string scriptPath, string sdfPath, CancellationToken ct = default)
    {
        // Zentraler ProcessRunner mit asynchronem Drain + Timeout (Audit STAB-H1).
        // CancellationToken kann den PowerShell-Prozess hart abbrechen (Audit-Fix: SDF-Cancel).
        var args = new[]
        {
            "-NoProfile", "-ExecutionPolicy", "Bypass",
            "-File", scriptPath, "-SdfPath", sdfPath
        };
        var result = AuswertungPro.Next.Application.Common.ProcessRunner
            .RunAsync("powershell.exe", args, timeout: TimeSpan.FromMinutes(15), ct: ct)
            .GetAwaiter().GetResult();

        if (result.StartFailed)
            throw new InvalidOperationException("powershell.exe konnte nicht gestartet werden");
        if (result.TimedOut)
            throw new InvalidOperationException(
                $"PowerShell-Timeout nach {result.Duration.TotalSeconds:F0}s. stderr: {result.Stderr}");
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"PowerShell-Exit {result.ExitCode}: {result.Stderr}");

        // Script gibt den JSON-Pfad als LETZTE non-empty Zeile aus
        var lines = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return line;
        }
        return result.Stdout.Trim();
    }

    private static void RunPython(string scriptPath, string jsonPath, string outDb3, CancellationToken ct = default)
    {
        // Zentraler ProcessRunner mit asynchronem Drain + Timeout (Audit STAB-H1).
        // CancellationToken bricht laufenden Python-Prozess hart ab.
        var args = new[] { scriptPath, jsonPath, outDb3 };
        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["PYTHONIOENCODING"] = "utf-8"
        };
        var result = AuswertungPro.Next.Application.Common.ProcessRunner
            .RunAsync("python", args, timeout: TimeSpan.FromMinutes(15), environment: env, ct: ct)
            .GetAwaiter().GetResult();

        if (result.StartFailed)
            throw new InvalidOperationException("python konnte nicht gestartet werden");
        if (result.TimedOut)
            throw new InvalidOperationException(
                $"Python-Timeout nach {result.Duration.TotalSeconds:F0}s. stderr: {result.Stderr}");
        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Python-Exit {result.ExitCode}: {result.Stderr}\nstdout: {result.Stdout}");
    }
}

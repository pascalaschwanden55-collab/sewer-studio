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
    /// </summary>
    public static string Convert(string sdfPath, string? outputPath = null)
    {
        if (!File.Exists(sdfPath))
            throw new FileNotFoundException("SDF nicht gefunden", sdfPath);

        if (!IsSsceAvailable())
            throw new InvalidOperationException(
                "SSCE 4.0 Runtime nicht gefunden. Installieren via 'Microsoft SQL Server Compact 4.0 SP1' (SSCERuntime_x64-ENU.exe).");

        outputPath ??= Path.Combine(@"C:\KI_BRAIN\sdf_converted",
            Path.GetFileNameWithoutExtension(sdfPath) + ".db3");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        // Scripts: relativ zum Repo-Root via AppDomain.BaseDirectory/../../../../tools
        // (bin\Debug\net10.0-windows...) Fallback: via Environment-Hint.
        var toolsDir = FindToolsDir();
        if (toolsDir is null)
            throw new InvalidOperationException(
                "Helper-Scripts nicht gefunden. Erwartet: <Repo>\\tools\\sdf-convert\\convert_sdf_to_db3.ps1");

        var ps1 = Path.Combine(toolsDir, "convert_sdf_to_db3.ps1");
        var py = Path.Combine(toolsDir, "sdf_json_to_sqlite.py");

        // Schritt 1: PowerShell-Export → JSON-Pfad auf stdout
        var jsonPath = RunPowerShell(ps1, sdfPath);
        if (string.IsNullOrWhiteSpace(jsonPath) || !File.Exists(jsonPath))
            throw new InvalidOperationException(
                $"PowerShell-Konvertierung lieferte keine JSON-Datei. Letzter stdout: '{jsonPath}'");

        // Schritt 2: Python → SQLite
        try { RunPython(py, jsonPath, outputPath); }
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

    private static string RunPowerShell(string scriptPath, string sdfPath)
    {
        // ArgumentList.Add statt String-Interpolation: Pfade mit Anfuehrungszeichen
        // oder Shell-Metazeichen koennen sonst Kommandozeilen-Injection ausloesen.
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("-NoProfile");
        psi.ArgumentList.Add("-ExecutionPolicy");
        psi.ArgumentList.Add("Bypass");
        psi.ArgumentList.Add("-File");
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add("-SdfPath");
        psi.ArgumentList.Add(sdfPath);
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("powershell.exe konnte nicht gestartet werden");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"PowerShell-Exit {p.ExitCode}: {stderr}");

        // Script gibt den JSON-Pfad als LETZTE non-empty Zeile aus
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i].Trim();
            if (line.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return line;
        }
        return stdout.Trim();
    }

    private static void RunPython(string scriptPath, string jsonPath, string outDb3)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(scriptPath);
        psi.ArgumentList.Add(jsonPath);
        psi.ArgumentList.Add(outDb3);
        psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
        using var p = Process.Start(psi)
            ?? throw new InvalidOperationException("python konnte nicht gestartet werden");
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"Python-Exit {p.ExitCode}: {stderr}\nstdout: {stdout}");
    }
}

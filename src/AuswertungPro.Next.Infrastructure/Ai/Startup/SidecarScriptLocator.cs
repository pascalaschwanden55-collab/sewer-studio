using System;
using System.IO;
using System.Linq;

namespace AuswertungPro.Next.Infrastructure.Ai.Startup;

// -----------------------------------------------------------------------
// Sucht das Sidecar-Startskript (start_sidecar.ps1) auf dem Dateisystem
// und gibt den Pfad zur PowerShell-Executable zurueck.
// Gehoert in Infrastructure, da es auf das Dateisystem zugreift.
// -----------------------------------------------------------------------

public static class SidecarScriptLocator
{
    /// <summary>
    /// Sucht start_sidecar.ps1 ausgehend vom Anwendungsverzeichnis und dem
    /// aktuellen Arbeitsverzeichnis aufwaerts durch die Ordnerhierarchie.
    /// Gibt den ersten gefundenen Pfad zurueck oder null, wenn kein Skript
    /// gefunden wurde.
    /// </summary>
    public static string? FindDefaultSidecarScript()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory }
                     .Where(p => !string.IsNullOrWhiteSpace(p))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "sidecar", "start_sidecar.ps1");
                if (File.Exists(candidate))
                    return candidate;

                dir = dir.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Gibt den vollstaendigen Pfad zur powershell.exe zurueck.
    /// Prueft zuerst den System32-Standardpfad; faellt auf "powershell.exe"
    /// zurueck (PATH-Aufloesung durch das Betriebssystem).
    /// </summary>
    public static string ResolvePowerShellExe()
    {
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrWhiteSpace(windowsDir))
        {
            var candidate = Path.Combine(windowsDir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(candidate))
                return candidate;
        }

        return "powershell.exe";
    }
}

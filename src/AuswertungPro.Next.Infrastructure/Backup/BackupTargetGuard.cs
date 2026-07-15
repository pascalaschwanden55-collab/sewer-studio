using System;
using System.Collections.Generic;
using System.IO;
using AuswertungPro.Next.Application.Backup;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Sicherheitspruefungen fuer den Datensicherungs-Spiegel.
/// Verhindert, dass ein falsch gewaehlter Ordner leergeraeumt wird oder
/// das Ziel in einer Quelle landet (Endlos-Aufblaehung).
/// </summary>
public static class BackupTargetGuard
{
    private static IBackupTargetMarkerGuard _markerGuard = new BackupTargetMarkerGuardService();

    public static IBackupTargetMarkerGuard MarkerGuard => Volatile.Read(ref _markerGuard);

    public static void UseMarkerGuard(IBackupTargetMarkerGuard markerGuard)
        => Volatile.Write(
            ref _markerGuard,
            markerGuard ?? throw new ArgumentNullException(nameof(markerGuard)));

    /// <summary>
    /// Prueft den Spiegel-Root und legt die Marker-Datei an.
    /// Erlaubt: Ordner existiert nicht / ist leer (Marker wird angelegt)
    /// oder Marker existiert bereits (Folgelauf).
    /// Verboten: Ordner enthaelt Fremddaten ohne Marker.
    /// </summary>
    /// <returns>null = ok, sonst deutsche Fehlermeldung.</returns>
    public static string? ValidateAndCreateMarker(string backupRoot)
        => MarkerGuard.ValidateAndCreateMarker(backupRoot);

    /// <summary>Defense-in-Depth: jeder Loeschpfad MUSS unterhalb des Spiegel-Roots liegen.</summary>
    public static bool IsInsideBackupRoot(string backupRoot, string path)
    {
        var root = Normalize(backupRoot);
        var candidate = Normalize(path);
        return candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verhindert Ziel-in-Quelle (z. B. Ziel = C:\KI_BRAIN → Spiegel wuerde sich
    /// selbst mitsichern) und Quelle-in-Ziel.
    /// </summary>
    /// <returns>null = ok, sonst deutsche Fehlermeldung.</returns>
    public static string? CheckSourceTargetConflict(string backupRoot, IEnumerable<string> sourceRoots)
    {
        var target = Normalize(backupRoot);

        foreach (var source in sourceRoots)
        {
            if (string.IsNullOrWhiteSpace(source))
                continue;

            var src = Normalize(source);
            if (target.Equals(src, StringComparison.OrdinalIgnoreCase)
                || target.StartsWith(src + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return $"Der Zielordner liegt innerhalb der Sicherungsquelle \"{source}\". " +
                       "Bitte einen Ordner ausserhalb der zu sichernden Daten waehlen.";
            }

            if (src.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return $"Die Sicherungsquelle \"{source}\" liegt innerhalb des Zielordners. " +
                       "Bitte einen anderen Zielordner waehlen.";
            }
        }

        return null;
    }

    private static string Normalize(string path)
        => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
}

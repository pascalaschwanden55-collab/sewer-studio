using System;
using System.IO;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>Ermittelt den freien Platz auf dem Laufwerk des Sicherungsziels.</summary>
public static class BackupDiskSpaceGuard
{
    /// <summary>
    /// Sicherheitsreserve fuer Dateisystem-Metadaten, Manifest und kleine Aenderungen,
    /// die nach der Vorabpruefung noch entstehen.
    /// </summary>
    public const long MinimumReserveBytes = 64L * 1024 * 1024;

    public static long? GetAvailableBytes(string targetPath)
    {
        try
        {
            var fullPath = Path.GetFullPath(targetPath);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrWhiteSpace(root))
                return null;

            return new DriveInfo(root).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    public static string? Validate(long requiredBytes, long? availableBytes)
    {
        if (availableBytes is null)
            return "Der freie Speicherplatz am Sicherungsziel konnte nicht sicher ermittelt werden.";

        if (availableBytes.Value < requiredBytes)
        {
            return $"Zu wenig freier Speicherplatz am Sicherungsziel. Benoetigt: " +
                   $"{ByteSizeFormatter.Format(requiredBytes)}, frei: " +
                   $"{ByteSizeFormatter.Format(availableBytes.Value)}.";
        }

        return null;
    }
}

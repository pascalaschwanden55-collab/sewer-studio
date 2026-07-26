using System;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Erkennt Verknuepfungen/Junctions (Reparse Points) in Pfaden. Beim Spiegeln und
/// bei der Verwaisten-Loeschung duerfen solche Eintraege nie betreten werden: der
/// dahinterliegende Inhalt liegt ausserhalb des eigenen Baums und wuerde sonst
/// kopiert oder geloescht.
/// </summary>
internal static class ReparsePointGuard
{
    /// <summary>true, wenn der Eintrag selbst eine Verknuepfung/Junction ist (nicht lesbar = false).</summary>
    public static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// true, wenn <paramref name="path"/> selbst oder ein Elternordner zwischen ihm und
    /// <paramref name="root"/> (exklusive) eine Verknuepfung/Junction ist. Der Root
    /// selbst wird bewusst nicht geprueft (er wurde bereits validiert).
    /// </summary>
    public static bool HasReparsePointBelow(string root, string path)
    {
        var fullRoot = Path.GetFullPath(root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var current = Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        while (!string.Equals(current, fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (IsReparsePoint(current))
                return true;

            var parent = Path.GetDirectoryName(current);
            if (parent is null || parent.Length >= current.Length)
                return false;   // Laufwerks-Root erreicht — Kette endet garantiert
            current = parent;
        }

        return false;
    }
}

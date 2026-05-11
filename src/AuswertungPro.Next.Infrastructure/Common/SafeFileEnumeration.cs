using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Common;

/// <summary>
/// Zentrale Safe-Enumeration fuer Verzeichnis-Scans, die gesperrte oder
/// fluechtige Unterordner toleriert. Robustheits-Fix 2026-05-10
/// (Deep-Dive Punkt #1): vorher konnte ein gesperrter Unterordner
/// einen ganzen Import abbrechen, bevor die per-Datei-Fehlerstrategie
/// greifen konnte.
///
/// Verhalten: <c>UnauthorizedAccessException</c>,
/// <c>DirectoryNotFoundException</c>, <c>IOException</c> werden je
/// Unter-Ordner gefangen — der Lauf laeuft weiter, der Order wird
/// uebersprungen. Eine optionale Fehler-Liste sammelt die betroffenen
/// Pfade fuer Caller-Diagnose.
///
/// Statisch, ohne State — testbar mit Temp-Verzeichnis-Setup.
/// </summary>
public static class SafeFileEnumeration
{
    /// <summary>Liefert alle erreichbaren Verzeichnisse unter <paramref name="root"/>
    /// (inkl. root selbst) als pre-order Traversal. Gesperrte Unterordner werden
    /// uebersprungen, optionale Sammlung in <paramref name="skippedDirectories"/>.</summary>
    public static IEnumerable<string> EnumerateDirectoriesSafe(
        string root,
        ICollection<string>? skippedDirectories = null)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            yield break;

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;

            IEnumerable<string>? children = null;
            try
            {
                children = Directory.EnumerateDirectories(current);
            }
            catch (System.UnauthorizedAccessException)
            {
                skippedDirectories?.Add(current);
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                skippedDirectories?.Add(current);
                continue;
            }
            catch (IOException)
            {
                skippedDirectories?.Add(current);
                continue;
            }

            foreach (var c in children)
                stack.Push(c);
        }
    }

    /// <summary>Liefert alle Dateien unter <paramref name="root"/> die zum Pattern
    /// passen. Bei <paramref name="recursive"/>=true wird in Unterordner abgestiegen
    /// (mit Safe-Behandlung). Gesperrte Ordner werden uebersprungen, optionale
    /// Sammlung der Skip-Pfade in <paramref name="skippedDirectories"/>.</summary>
    public static IEnumerable<string> EnumerateFilesSafe(
        string root,
        string searchPattern = "*",
        bool recursive = true,
        ICollection<string>? skippedDirectories = null)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            yield break;

        IEnumerable<string> dirs = recursive
            ? EnumerateDirectoriesSafe(root, skippedDirectories)
            : new[] { root };

        foreach (var dir in dirs)
        {
            IEnumerable<string>? files = null;
            try
            {
                files = Directory.EnumerateFiles(dir, searchPattern);
            }
            catch (System.UnauthorizedAccessException)
            {
                skippedDirectories?.Add(dir);
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                skippedDirectories?.Add(dir);
                continue;
            }
            catch (IOException)
            {
                skippedDirectories?.Add(dir);
                continue;
            }

            foreach (var f in files)
                yield return f;
        }
    }
}

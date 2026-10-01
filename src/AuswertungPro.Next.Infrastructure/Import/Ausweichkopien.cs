using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Ausweichnamen bei einer Namenskollision tragen einen Zeitstempel
/// (<c>foto_20260717_123000.jpg</c>, <c>foto_20260717_123000_2.jpg</c>). Der Stempel aendert
/// sich bei jedem Lauf; ohne diese Suche legte jeder erneute Import eine weitere, inhaltsgleiche
/// Kopie an. Gesucht wird nur im Zielordner selbst und nur nach genau diesem Namensmuster.
/// </summary>
internal static class Ausweichkopien
{
    internal static string? FindeGleiche(
        string zielOrdner,
        string dateiname,
        Func<string, bool> istGleich)
    {
        if (!Directory.Exists(zielOrdner))
            return null;

        var name = Path.GetFileNameWithoutExtension(dateiname);
        var endung = Path.GetExtension(dateiname);
        var muster = new Regex(
            "^" + Regex.Escape(name) + @"_\d{8}_\d{6}(_\d+)?" + Regex.Escape(endung) + "$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var pfad in SafeFileEnumeration
                     .EnumerateFilesSafe(zielOrdner, name + "_*" + endung, recursive: false)
                     .Where(p => muster.IsMatch(Path.GetFileName(p)))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            if (istGleich(pfad))
                return pfad;
        }

        return null;
    }
}

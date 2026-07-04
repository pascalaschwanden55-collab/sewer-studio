using System;
using System.Globalization;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Formatiert Byte-Groessen menschenlesbar ("1,2 GB", "340,0 MB", "12 KB").
/// Ab MB mit einer Nachkommastelle, darunter ganzzahlig.
/// </summary>
public static class ByteSizeFormatter
{
    private const double Kb = 1024.0;
    private const double Mb = Kb * 1024.0;
    private const double Gb = Mb * 1024.0;

    public static string Format(long bytes, IFormatProvider? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;

        if (bytes < 0)
            return "0 B";
        if (bytes >= Gb)
            return string.Format(culture, "{0:F1} GB", bytes / Gb);
        if (bytes >= Mb)
            return string.Format(culture, "{0:F1} MB", bytes / Mb);
        if (bytes >= Kb)
            return string.Format(culture, "{0:F0} KB", bytes / Kb);
        return string.Format(culture, "{0} B", bytes);
    }
}

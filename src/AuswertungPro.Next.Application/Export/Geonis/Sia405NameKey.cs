using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Application.Export.Geonis;

/// <summary>
/// Vergleichsschluessel fuer Haltungs- und Schachtbezeichnungen im GEONIS-Rueckschrieb.
///
/// Bewusst dieselbe Normalisierung wie beim Import (Leerzeichen entfernen, Schraegstrich und
/// Gedankenstriche auf "-"), zusaetzlich Grossschreibung. Projekt und Kataster muessen mit exakt
/// derselben Regel verglichen werden, sonst findet der Abgleich vorhandene Objekte nicht wieder.
/// </summary>
public static class Sia405NameKey
{
    public static string Normalize(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0)
            return string.Empty;

        v = Regex.Replace(v, @"\s+", string.Empty);
        v = v.Replace('/', '-');
        v = v.Replace('–', '-'); // Halbgeviertstrich
        v = v.Replace('—', '-'); // Geviertstrich
        return v.ToUpperInvariant();
    }
}

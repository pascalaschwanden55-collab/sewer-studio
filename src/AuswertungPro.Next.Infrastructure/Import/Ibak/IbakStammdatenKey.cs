namespace AuswertungPro.Next.Infrastructure.Import.Ibak;

/// <summary>
/// Gemeinsame Schlüssel-Normalisierung für Haltungsnamen im IBAK-Import-Namespace.
///
/// Alle drei Quellen (PDF, XTF, FDB) müssen denselben Schlüssel für dieselbe
/// Haltung erzeugen, damit der StammdatenAggregator die Maps korrekt joinen kann.
///
/// Normalisierungsregeln:
///   - Trim: führende/nachfolgende Leerzeichen entfernen
///   - Leerzeichen im Innern entfernen ("36 262-36 275" → "36262-36275")
///   - Schrägstrich → Bindestrich ("36262/36275" → "36262-36275")
///   - En-Dash (–, U+2013) → Bindestrich
///   - Em-Dash (—, U+2014) → Bindestrich
/// </summary>
internal static class IbakStammdatenKey
{
    /// <summary>
    /// Normalisiert einen Haltungsnamen zum Join-Schlüssel.
    /// Gibt null zurück wenn der Eingabewert leer oder nur Whitespace ist.
    /// </summary>
    internal static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim()
            .Replace(" ", "")
            .Replace("/", "-")
            .Replace("–", "-")   // En-Dash
            .Replace("—", "-");  // Em-Dash
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }
}

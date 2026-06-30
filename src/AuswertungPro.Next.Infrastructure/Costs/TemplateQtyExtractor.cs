using System.Text.Json;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Extrahiert den Mengen-String aus einem JSON-Element einer Vorlagen-Zeile.
/// Ein Qty-Feld kann als JSON-String ("1") oder als JSON-Zahl (1) vorliegen.
/// </summary>
public static class TemplateQtyExtractor
{
    /// <summary>
    /// Gibt den Menge-String zurück. Bei String-Werten wird der Rohtext geliefert,
    /// bei numerischen Werten der JSON-Rohtext (z.B. "1.5"). Fallback: "1".
    /// </summary>
    public static string ExtractQtyString(JsonElement qty)
    {
        return qty.ValueKind == JsonValueKind.String
            ? qty.GetString() ?? "1"
            : qty.GetRawText();
    }
}

using System.Collections.ObjectModel;

namespace AuswertungPro.Next.Domain.Models;

/// <summary>
/// Die Schachtformen der Urner GEONIS-Maske. Diese Form bleibt ein Programmfeld;
/// SIA405 beschreibt den Normschacht ueber Dimension 1 und Dimension 2.
/// </summary>
public static class SchachtformVokabular
{
    public static readonly IReadOnlyList<string> Auswahl = new ReadOnlyCollection<string>(
    [
        "",
        "Unbekannt",
        "Rund",
        "Oval",
        "Quadratisch",
        "Rechteckig",
        "Vieleckig"
    ]);

    private static readonly IReadOnlyDictionary<string, string> Zuordnungen =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["unbekannt"] = "Unbekannt",
            ["unknown"] = "Unbekannt",
            ["U"] = "Unbekannt",
            ["rund"] = "Rund",
            ["round"] = "Rund",
            ["circular"] = "Rund",
            ["kreisfoermig"] = "Rund",
            ["kreisförmig"] = "Rund",
            ["oval"] = "Oval",
            ["eifoermig"] = "Oval",
            ["eiförmig"] = "Oval",
            ["quadratisch"] = "Quadratisch",
            ["square"] = "Quadratisch",
            ["rechteckig"] = "Rechteckig",
            ["rectangular"] = "Rechteckig",
            ["vieleckig"] = "Vieleckig",
            ["polygonal"] = "Vieleckig"
        };

    /// <summary>
    /// Fuehrt bekannte deutsche und englische Importwerte auf die Dropdown-Schreibweise.
    /// Ein unbekannter Freitext bleibt erhalten und wird nicht umgedeutet.
    /// </summary>
    public static string Normalisieren(string? wert)
    {
        var text = (wert ?? "").Trim();
        if (text.Length == 0)
            return "";

        if (Zuordnungen.TryGetValue(text, out var normalisiert))
            return normalisiert;

        if (text.StartsWith("rundschacht", StringComparison.OrdinalIgnoreCase))
            return "Rund";

        return text;
    }
}

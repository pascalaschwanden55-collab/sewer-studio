using System;

namespace AuswertungPro.Next.Infrastructure.Costs;

/// <summary>
/// Leitet den Anzeige-Gruppen-Namen aus dem Katalog-Schluessel ab und liefert
/// die Sortierposition innerhalb der Gruppen-Liste.
/// </summary>
public static class CatalogItemGrouping
{
    /// <summary>Kanonische Reihenfolge der Katalog-Gruppen.</summary>
    public static readonly string[] GroupOrder =
    {
        "Installation", "Vorarbeiten", "Hauptarbeit",
        "Qualitaetskontrolle", "Qualitaet",
        "Sonstiges"
    };

    /// <summary>Leitet den Gruppen-Namen aus dem Katalog-Schluessel ab.</summary>
    public static string DeriveGroupFromKey(string key)
    {
        if (key.StartsWith("INSTALL", StringComparison.OrdinalIgnoreCase)) return "Installation";
        if (key.StartsWith("VORARBEIT", StringComparison.OrdinalIgnoreCase)) return "Vorarbeiten";
        if (key.StartsWith("QK_", StringComparison.OrdinalIgnoreCase)) return "Qualitaetskontrolle";
        if (key.StartsWith("HAUPTARBEIT", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        // Alle Hauptarbeit-Positionen: Schlauchliner, LEM, Kurzliner, Manschette, Anschluss
        if (key.StartsWith("SCHLAUCHLINER", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        if (key.StartsWith("LINERENDMANSCHETTE", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        if (key.StartsWith("KURZLINER", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        if (key.StartsWith("MANSCHETTE", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        if (key.StartsWith("ANSCHLUSS", StringComparison.OrdinalIgnoreCase)) return "Hauptarbeit";
        return "Sonstiges";
    }

    /// <summary>Liefert den Sortier-Index einer Gruppe (unbekannte Gruppen ans Ende).</summary>
    public static int GetGroupOrder(string? group)
    {
        if (string.IsNullOrWhiteSpace(group))
            return GroupOrder.Length + 1;

        var idx = Array.FindIndex(GroupOrder,
            g => string.Equals(g, group.Trim(), StringComparison.OrdinalIgnoreCase));
        // Unbekannte nicht-leere Gruppe erhaelt denselben Rang wie leere Gruppe (nach allen bekannten)
        return idx >= 0 ? idx : GroupOrder.Length + 1;
    }
}

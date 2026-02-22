// AuswertungPro – KI Videoanalyse Modul
namespace AuswertungPro.Next.UI.Ai.Training.Models;

/// <summary>
/// Strukturierte Quantifizierung einer Beobachtung.
/// Wert + Einheit + Typ + optionale Uhrzeigerposition — klar getrennt, nicht mehrdeutig.
/// </summary>
public sealed record QuantificationDetail
{
    /// <summary>Numerischer Wert (z.B. 3.0 für 3 mm).</summary>
    public required double Value { get; init; }

    /// <summary>Einheit: "%", "mm", "cm", "Stück".</summary>
    public required string Unit { get; init; }

    /// <summary>Typ: "Querschnittsverminderung", "Spaltbreite", "Versatz", etc.</summary>
    public required string Type { get; init; }

    /// <summary>Uhrzeigerposition (getrennt von Hauptposition). Null wenn nicht angegeben.</summary>
    public string? ClockPosition { get; init; }
}

// AuswertungPro – KI Videoanalyse Modul
namespace AuswertungPro.Next.UI.Ai.Analysis.Models;

/// <summary>
/// Differenzierte Konfidenz einer KI-Beobachtung.
/// Drei unabhängige Dimensionen statt eines einzigen Werts.
/// </summary>
public sealed record ObservationConfidence
{
    /// <summary>Ist überhaupt etwas da? (0.0–1.0)</summary>
    public required double Detection { get; init; }

    /// <summary>Welcher VSA-Code? (0.0–1.0)</summary>
    public required double Classification { get; init; }

    /// <summary>Wie viel / wie schlimm? (0.0–1.0)</summary>
    public required double Quantification { get; init; }

    /// <summary>Gewichtetes Mittel aller drei Dimensionen.</summary>
    public double Overall => (Detection + Classification + Quantification) / 3.0;

    public static ObservationConfidence Low  => new() { Detection = 0.3, Classification = 0.3, Quantification = 0.3 };
    public static ObservationConfidence High => new() { Detection = 0.9, Classification = 0.9, Quantification = 0.9 };
}

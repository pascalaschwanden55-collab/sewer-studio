namespace AuswertungPro.Next.Domain.Ai.Training;

/// <summary>Eine KI-Erkennung aus dem Video-Blinddurchlauf.</summary>
public sealed record BlindDetection
{
    /// <summary>Zeitpunkt im Video (Sekunden).</summary>
    public required double TimeSeconds { get; init; }

    /// <summary>Geschaetzter oder OSD-erkannter Meterstand.</summary>
    public required double Meter { get; init; }

    /// <summary>Von der KI erkannter VSA-Code (kann null sein bei unklarer Codierung).</summary>
    public string? VsaCode { get; init; }

    /// <summary>YOLO/DINO Label (z.B. "crack", "root intrusion").</summary>
    public required string Label { get; init; }

    /// <summary>Geschaetzte Schwere (1-5).</summary>
    public int Severity { get; init; }

    /// <summary>Uhrzeigerposition (z.B. "3", "12").</summary>
    public string? ClockPosition { get; init; }

    /// <summary>Konfidenz der Erkennung (0.0 - 1.0).</summary>
    public double Confidence { get; init; }

    /// <summary>Pfad zum extrahierten Frame (PNG).</summary>
    public string? FramePath { get; init; }

    public double? BboxX1 { get; init; }
    public double? BboxY1 { get; init; }
    public double? BboxX2 { get; init; }
    public double? BboxY2 { get; init; }

    /// <summary>True wenn bereits einem Protokolleintrag zugeordnet (Greedy-Assignment).</summary>
    public bool IsAssigned { get; set; }
}

using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Schatten;

/// <summary>Wie weit die Schattenauswertung fuer eine Haltung gekommen ist.</summary>
public enum SchattenStatus
{
    OhneCodierung, // keine Findings und kein Primaere_Schaeden-Text -> nichts zu rechnen
    NurRegeln,     // Regelteil gerechnet, KI (noch) nicht gelaufen
    MitKi,         // Regelteil + KI-Empfehlung
    KiFallback     // KI versucht, aber Fallback/Fehler -> Regelwerte gelten
}

/// <summary>
/// Eigenstaendige Parallel-Auswertung EINER Haltung. Wird nie in HaltungRecord-Felder
/// geschrieben — nur in den Schatten-Store (eigene Datei im Projektordner).
/// </summary>
public sealed record SchattenHaltungErgebnis
{
    public string Haltung { get; init; } = "";
    public string CodierungsHash { get; init; } = "";
    public DateTime BerechnetUtc { get; init; }
    public SchattenStatus Status { get; init; }

    // (a) Zustand — von der VSA-Bewertung auf dem Klon abgelesen
    public string? NoteD { get; init; }
    public string? NoteS { get; init; }
    public string? NoteB { get; init; }
    public string? Zustandsklasse { get; init; }
    public bool Geschaetzt { get; init; }

    // (b/c) Regelteil — aus dem Massnahmen-Lernmodell
    public IReadOnlyList<string> RegelMassnahmen { get; init; } = Array.Empty<string>();
    public decimal? RegelKosten { get; init; }
    public int? AehnlicheFaelle { get; init; }
    public bool RegelModellGenutzt { get; init; }

    // (d) KI-Teil — aus der LLM-Sanierungsoptimierung (null wenn nicht gelaufen)
    public string? KiMassnahme { get; init; }
    public double? KiConfidence { get; init; }
    public decimal? KostenMin { get; init; }
    public decimal? KostenErwartet { get; init; }
    public decimal? KostenMax { get; init; }
    public string? KiBegruendung { get; init; }
    public IReadOnlyList<string> RisikoFlags { get; init; } = Array.Empty<string>();
    public bool IsFallback { get; init; }
    public string? KiFehler { get; init; }
}

/// <summary>Persistenzform (eigene Datei, Vorbild ProjectCostStore).</summary>
public sealed class SchattenAuswertungStore
{
    public int Version { get; set; } = 1;
    public DateTime? LetzterLaufUtc { get; set; }
    public string? KiModell { get; set; }
    public Dictionary<string, SchattenHaltungErgebnis> ByHaltung { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Fortschrittsmeldung fuer die Seite (Phase: "Regeln" | "KI").</summary>
public sealed record SchattenFortschritt(string Phase, int Aktuell, int Gesamt, string Haltung);

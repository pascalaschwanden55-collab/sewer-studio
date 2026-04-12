// AuswertungPro – Video-Selbsttraining: Voll-automatischer Batch-Betrieb
using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.UI.Ai.Training.Models;

/// <summary>Anfrage fuer den Batch-Selbsttraining-Durchlauf.</summary>
public sealed class BatchSelfTrainingRequest
{
    /// <summary>Ordner mit WinCan-Exports (jeder Unterordner = ein Export mit DB3 + Videos).</summary>
    public required string ExportRootPath { get; init; }

    /// <summary>True = auch Unterordner rekursiv durchsuchen.</summary>
    public bool RecurseSubdirectories { get; init; } = true;

    /// <summary>Frame-Abstand in Sekunden fuer den Blinddurchlauf.
    /// 3.0s = guter Kompromiss (Kamera bewegt sich ~1.5m in 3s, Schaeden sind laenger sichtbar).
    /// 1.5s = genauer aber doppelt so lang.</summary>
    public double FrameStepSeconds { get; init; } = 3.0;

    /// <summary>
    /// Meter-Toleranz fuer Zuordnung KI ↔ Protokoll.
    /// In realen Kanalvideos ist die OSD-/Meter-Zuordnung oft verrauscht, daher
    /// konservativ grosszuegiger als beim Benchmark.
    /// </summary>
    public double MeterTolerance { get; init; } = 1.5;

    /// <summary>
    /// Treffer (KI + Protokoll stimmen ueberein) automatisch in KB uebernehmen?
    /// Default: true — sichere Treffer brauchen kein manuelles Review.
    /// </summary>
    public bool AutoApproveMatches { get; init; } = true;

    /// <summary>
    /// Protokoll-Korrekturen (KI lag falsch, Protokoll hat recht) automatisch in KB?
    /// Default: true — das Protokoll ist die Ground-Truth.
    /// </summary>
    public bool AutoApproveCorrections { get; init; } = true;

    /// <summary>
    /// False Positives (KI meldet etwas das nicht im Protokoll steht) verwerfen?
    /// Default: true — KI-Erfindungen nicht lernen.
    /// </summary>
    public bool DiscardFalsePositives { get; init; } = true;

    /// <summary>
    /// Uebersehene Schaeden (im Protokoll, KI hat nichts): Frame extrahieren und als Beispiel speichern?
    /// Default: true — "So sieht ein Schaden aus den du verpasst hast."
    /// </summary>
    public bool LearnFromMissed { get; init; } = true;

    /// <summary>Bereits verarbeitete Haltungen ueberspringen? (anhand Historie)</summary>
    public bool SkipAlreadyProcessed { get; init; } = true;

    /// <summary>Maximale Anzahl Haltungen (0 = unbegrenzt). Fuer Tests nützlich.</summary>
    public int MaxHaltungen { get; init; } = 0;
}

/// <summary>Fortschrittsmeldung waehrend des Batch-Durchlaufs.</summary>
public sealed class BatchSelfTrainingProgress
{
    /// <summary>Aktuelle Haltungsnummer (1-basiert).</summary>
    public int CurrentIndex { get; init; }

    /// <summary>Gesamtanzahl gefundener Haltungen.</summary>
    public int TotalHaltungen { get; init; }

    /// <summary>Name der aktuellen Haltung.</summary>
    public string HaltungId { get; init; } = "";

    /// <summary>Aktuelle Phase: "Import", "Analyse", "Vergleich", "KB-Update".</summary>
    public string Phase { get; init; } = "";

    /// <summary>Statustext fuer die Anzeige.</summary>
    public string Status { get; init; } = "";

    /// <summary>Geschaetzte Restzeit (null wenn noch nicht berechenbar).</summary>
    public TimeSpan? EstimatedRemaining { get; init; }

    /// <summary>Laufende KB-Statistik.</summary>
    public BatchKbStats RunningStats { get; init; } = new();
}

/// <summary>Laufende KB-Statistik waehrend des Batch-Durchlaufs.</summary>
public sealed class BatchKbStats
{
    public int TruePositives { get; set; }
    public int FalseNegatives { get; set; }
    public int FalsePositives { get; set; }
    public int CodeMismatches { get; set; }
    public int KbIndexed { get; set; }
    public int KbDeduplicated { get; set; }
    public int KbSkipped { get; set; }
    public int Errors { get; set; }

    // CodeMismatch: Objekt erkannt aber falsch klassifiziert → Precision-Fehler (FP-Variante).
    // Recall zaehlt nur ob ein Objekt ueberhaupt erkannt wurde — CodeMismatch ist kein FN.
    public double Precision => (TruePositives + FalsePositives + CodeMismatches) > 0
        ? (double)TruePositives / (TruePositives + FalsePositives + CodeMismatches) : 0;
    public double Recall => (TruePositives + FalseNegatives) > 0
        ? (double)TruePositives / (TruePositives + FalseNegatives) : 0;
    public double F1
    {
        get
        {
            var p = Precision;
            var r = Recall;
            return (p + r) > 0 ? 2 * p * r / (p + r) : 0;
        }
    }
}

/// <summary>Ergebnis einer einzelnen Haltung im Batch.</summary>
public sealed class BatchHaltungResult
{
    public required string HaltungId { get; init; }
    public required string VideoPath { get; init; }
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int ProtocolEntries { get; init; }
    public int KiDetections { get; init; }
    public int TruePositives { get; init; }
    public int FalseNegatives { get; init; }
    public int FalsePositives { get; init; }
    public int CodeMismatches { get; init; }
    public int KbIndexed { get; init; }
    public int KbDeduplicated { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>Gesamtergebnis des Batch-Durchlaufs.</summary>
public sealed class BatchSelfTrainingResult
{
    public DateTime StartedUtc { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public int TotalHaltungen { get; init; }
    public int Processed { get; init; }
    public int Skipped { get; init; }
    public int Failed { get; init; }
    public BatchKbStats FinalStats { get; init; } = new();
    public List<BatchHaltungResult> HaltungResults { get; init; } = [];
}

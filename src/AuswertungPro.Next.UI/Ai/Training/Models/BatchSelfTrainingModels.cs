// AuswertungPro – Video-Selbsttraining: Voll-automatischer Batch-Betrieb
using System;
using System.Collections.Generic;
using AuswertungPro.Next.UI.Ai.Shared;

namespace AuswertungPro.Next.UI.Ai.Training.Models;

/// <summary>Anfrage fuer den Batch-Selbsttraining-Durchlauf.</summary>
public sealed class BatchSelfTrainingRequest
{
    /// <summary>Ordner mit WinCan-Exports (jeder Unterordner = ein Export mit DB3 + Videos).</summary>
    public required string ExportRootPath { get; init; }

    /// <summary>True = auch Unterordner rekursiv durchsuchen.</summary>
    public bool RecurseSubdirectories { get; init; } = true;

    /// <summary>Frame-Abstand in Sekunden fuer den Blinddurchlauf.
    /// 2.0s = erhoehter Durchsatz bei ausreichend GPU-Leistung (RTX 5090).
    /// Bei 0.055 m/s Fahrgeschwindigkeit: alle ~11cm ein Frame.</summary>
    public double FrameStepSeconds { get; init; } = 2.0;

    /// <summary>
    /// Meter-Toleranz fuer Zuordnung KI ↔ Protokoll.
    /// In realen Kanalvideos ist die OSD-/Meter-Zuordnung oft verrauscht, daher
    /// konservativ grosszuegiger als beim Benchmark.
    /// </summary>
    public double MeterTolerance { get; init; } = MeterTolerances.BatchProcessing;

    /// <summary>
    /// Treffer (KI + Protokoll stimmen ueberein) automatisch in KB uebernehmen?
    /// V4.2: Default false — KB-Vergiftung stoppen. Opt-in mit Confidence-Gate.
    /// </summary>
    public bool AutoApproveMatches { get; init; } = false;

    /// <summary>
    /// Protokoll-Korrekturen (KI lag falsch, Protokoll hat recht) automatisch in KB?
    /// Default: false — PDF-OCR hat ~5-8% Fehler, blindes Uebernehmen vergiftet die KB.
    /// Korrekturen muessen manuell im Review bestaetigt werden.
    /// </summary>
    public bool AutoApproveCorrections { get; init; } = false;

    /// <summary>
    /// False Positives (KI meldet etwas das nicht im Protokoll steht) verwerfen?
    /// Default: true — KI-Erfindungen nicht lernen.
    /// </summary>
    public bool DiscardFalsePositives { get; init; } = true;

    /// <summary>
    /// Uebersehene Schaeden (im Protokoll, KI hat nichts): Frame extrahieren und als Beispiel speichern?
    /// V4.2: Default false — OSD-Frame-Mapping kann falsch sein, Schaden auf Frame nicht garantiert sichtbar.
    /// </summary>
    public bool LearnFromMissed { get; init; } = false;

    /// <summary>
    /// V4.2: Minimale KI-Detection-Confidence fuer Auto-Approve (0.0 - 1.0).
    /// Unter diesem Wert wird das Sample nicht automatisch indexiert (skip → Review-Queue in Phase 1.4).
    /// </summary>
    public double MinDetectionConfidence { get; init; } = 0.85;

    /// <summary>
    /// V4.2 Phase 2: Protokoll-First-Modus aktivieren.
    /// Pipeline analysiert nur gezielt Protokoll-Fundstellen, Qwen bekommt Yes/No-Fragen.
    /// V4.2 Nachbesserung: Default true — reduziert Qwen-Calls drastisch und verhindert
    /// Open-Set-Halluzination (BCC-Kollaps). Opt-out moeglich via false.
    /// </summary>
    public bool UseProtocolFirst { get; init; } = true;

    /// <summary>Meter-Toleranz um Protokoll-Eintraege im Protokoll-First-Modus (Default 1.0m).</summary>
    public double ProtocolFirstMeterTolerance { get; init; } = 1.0;

    /// <summary>
    /// V4.2 Phase 2.4: Ueberraschungsfund-Pass aktivieren.
    /// Zweiter langsamer Durchlauf auf den Luecken zwischen Protokoll-Zonen —
    /// faengt Schaeden ein, die der Operateur uebersehen hat.
    /// Treffer landen in der Review-Queue, nicht direkt in KB.
    /// Default false (opt-in). Wirkt nur zusammen mit UseProtocolFirst.
    /// </summary>
    public bool EnableSurpriseGapsPass { get; init; } = false;

    /// <summary>Frame-Step (Sekunden) fuer den Ueberraschungsfund-Pass. Default 10s.</summary>
    public double SurpriseGapsFrameStep { get; init; } = 10.0;

    /// <summary>Bereits verarbeitete Haltungen ueberspringen? (anhand Historie)</summary>
    public bool SkipAlreadyProcessed { get; init; } = true;

    /// <summary>Maximale Anzahl Haltungen (0 = unbegrenzt). Fuer Tests nützlich.</summary>
    public int MaxHaltungen { get; init; } = 0;

    /// <summary>
    /// Anzahl parallel verarbeiteter Haltungen.
    /// Jede Haltung bekommt ihre eigene Pipeline-Instanz (Thread-Safe).
    /// Default: aus TrainingCenterSettings.CaseParallelism (VRAM-adaptiv).
    /// Env: SEWERSTUDIO_SELFTRAIN_CASE_PARALLELISM
    /// </summary>
    public int MaxParallelHaltungen { get; init; } = new TrainingCenterSettings().CaseParallelism;
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

using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Ai.Training;

namespace AuswertungPro.Next.Application.Ai.Training;

public enum TrainingCaseStatus
{
    New = 0,
    Approved = 1,
    Rejected = 2,
    /// <summary>Fall wurde durch Selbsttraining verarbeitet.</summary>
    SelfTrained = 3,
    /// <summary>Fall wurde durch Batch-Import + KB verarbeitet.</summary>
    BatchImported = 4
}

/// <summary>
/// Phase 5.3 Sub-B (Audit 2026-05-06 Top-10 Punkt 4): POCO-Modell fuer einen
/// Trainingsfall (Video + Protokoll). Frueher MVVM-Klasse im UI-Layer; jetzt
/// hier ohne Notify-Logik damit Application-Services (Import, SampleGenerator,
/// SelfTraining) den Typ verwenden koennen ohne MVVM mitzuschleppen.
///
/// JSON-Property-Namen bleiben unveraendert — bestehende
/// `training_center.json`-Dateien sind rueckwaertskompatibel lesbar.
///
/// Mutable Felder: Status (Approve/Reject-Workflow) plus die spaet ergaenzten
/// Stammdaten (Rohrmaterial, NennweiteMm, Profil) — werden nach dem PDF-Import
/// gesetzt.
/// </summary>
public sealed class TrainingCase
{
    public string CaseId { get; set; } = "";
    public string FolderPath { get; set; } = "";
    public string VideoPath { get; set; } = "";
    public string ProtocolPath { get; set; } = "";
    public TrainingCaseStatus Status { get; set; } = TrainingCaseStatus.New;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Rohrmaterial aus PDF/Import (z.B. "Polyethylen", "Beton"). Null wenn unbekannt.</summary>
    public string? Rohrmaterial { get; set; }

    /// <summary>Nennweite in mm aus PDF/Import. Null wenn unbekannt.</summary>
    public int? NennweiteMm { get; set; }

    /// <summary>Rohrprofil-Text (z.B. "Kreisprofil 300mm"). Null wenn unbekannt.</summary>
    public string? Profil { get; set; }

    public override string ToString() => CaseId;
}

/// <summary>
/// JSON-Persistenz-DTO: List der Trainingsfaelle + konfigurierte Stamm-Ordner +
/// Last-Update-Timestamp. Wird via TrainingCenterStore.LoadAsync/SaveAsync
/// in `%APPDATA%/AuswertungPro/training_center.json` gespiegelt.
/// </summary>
public sealed class TrainingCenterState
{
    public List<TrainingCase> Cases { get; set; } = new();
    public List<string> RootFolders { get; set; } = new();
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Ein Ergebnis-Eintrag im Selbsttraining-Verlauf.</summary>
public sealed class SelfTrainingEntryResult
{
    public int Index { get; init; }
    public string VsaCode { get; init; } = "";
    public double Meter { get; init; }
    public MatchLevel Level { get; init; }
    public string Summary { get; init; } = "";
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.Ai.KnowledgeBase;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Deterministisches QualityGate fuer Training-Samples.
/// Prueft jedes Sample auf Vollstaendigkeit und Gueltigkeit BEVOR es
/// in die KB indexiert oder als Approved markiert wird.
///
/// Ergebnis pro Sample:
///   Green  → vollstaendig, auto-Approve + KB-Index
///   Yellow → brauchbar, gespeichert aber in Review Queue
///   Red    → unbrauchbar, Reject (nicht speichern, nicht indexieren)
///
/// Bewertung: gewichtete Punkte statt einfache Issue-Zaehlung.
///   Jeder Mangel hat ein Gewicht (1-3). Summe bestimmt Grade:
///     0         → Green
///     1-3       → Yellow
///     4+ oder Hard-Red → Red
/// </summary>
public sealed class SampleQualityGateService
{
    /// <summary>Schwelle ab der Yellow in Red kippt (Summe der Gewichte).</summary>
    private const int RedThreshold = 4;

    /// <summary>
    /// Codes die am Rohranfang (MeterStart=0) normal vorkommen (VSA-konform).
    /// BCD=Rohranfang, BDB=Beginn der Bestandsaufnahme, BCE=Rohrende (Rueckwaertsbefahrung).
    /// </summary>
    private static readonly string[] ZeroMeterCodes =
        ["BCD", "BDB", "BCE"];

    /// <summary>
    /// Erstellt das Gate. Code-Pruefung erfolgt IMMER gegen den VsaCodeTree
    /// (nur offizielle VSA/EN 13508-2 Leitungscodes).
    /// Der optionale allowedCodes-Parameter wird aus Kompatibilitaet akzeptiert aber ignoriert.
    /// </summary>
    public SampleQualityGateService(IEnumerable<string>? allowedCodes = null)
    {
        // allowedCodes wird ignoriert — wir pruefen immer gegen VsaCodeTree
    }

    /// <summary>Prueft ein einzelnes Sample und gibt das Ergebnis zurueck.</summary>
    public SampleQualityResult Evaluate(TrainingSample sample)
    {
        var issues = new List<QualityIssue>();

        // ── Hard-Red: sofortiger Ausschluss (eines reicht) ────────────

        // Code muss vorhanden sein (mind. 2 Zeichen: z.B. "BA", "BC")
        if (string.IsNullOrWhiteSpace(sample.Code) || sample.Code.Length < 2)
            return HardRed("Code fehlt oder zu kurz (min. 2 Zeichen)");

        // Code MUSS ein gueltiger VSA-Leitungscode sein (IMMER pruefen).
        // Keine WinCan-internen Codes (BEGINN, BOGEN, FOTO etc.).
        // Keine Schachtcodes (D-Gruppe).
        if (!KnowledgeBase.KnowledgeBaseManager.IsValidVsaLeitungscode(sample.Code))
            return HardRed($"Code '{sample.Code}' ist kein gueltiger VSA-Leitungscode");

        // SampleId ist Pflicht (internes Tracking)
        if (string.IsNullOrWhiteSpace(sample.SampleId))
            return HardRed("SampleId fehlt");

        // CaseId ist Pflicht (Zuordnung zur Haltung)
        if (string.IsNullOrWhiteSpace(sample.CaseId))
            return HardRed("CaseId fehlt");

        // Beschreibung komplett leer → unbrauchbar fuer KB
        if (string.IsNullOrWhiteSpace(sample.Beschreibung))
            return HardRed("Beschreibung fehlt komplett");

        // ── Gewichtete Maengel ────────────────────────────────────────

        // Signatur noetig fuer Dedup (Gewicht 2: ohne Signatur drohen Duplikate)
        if (string.IsNullOrWhiteSpace(sample.Signature))
            issues.Add(new("Signatur fehlt (Dedup nicht moeglich)", 2));

        // Frame-Pruefung: SourceType-bewusst
        // BatchImport/PdfPhoto ohne Frame ist normal (protocol-only) → kein Abzug
        // Selbsttraining ohne Frame ist verdaechtig → Abzug
        var isBatchOrPdf = sample.SourceType is SourceTypeNames.BatchImport
                           or SourceTypeNames.PdfPhoto;
        if (!isBatchOrPdf && string.IsNullOrWhiteSpace(sample.FramePath))
        {
            issues.Add(new("Kein Frame-Pfad (erwartet bei Selbsttraining)", 2));
        }
        else if (!string.IsNullOrWhiteSpace(sample.FramePath) && !File.Exists(sample.FramePath))
        {
            // Frame referenziert aber nicht vorhanden → Datenverlust
            issues.Add(new("Frame-Datei existiert nicht", 2));
        }

        // Beschreibung ist nur Code-Echo (wenig Mehrwert, aber nicht fatal)
        if (sample.Beschreibung.Trim().Equals(sample.Code.Trim(), StringComparison.OrdinalIgnoreCase))
            issues.Add(new("Beschreibung ist nur Code-Echo", 1));

        // MeterStart=0 nur verdaechtig bei Codes die normalerweise NICHT am Rohranfang sind
        if (sample.MeterStart <= 0
            && !IsZeroMeterCode(sample.Code))
        {
            // Gewicht 1: Warnung, nicht schwerwiegend (es gibt reale Faelle)
            issues.Add(new("MeterStart ist 0 (unueblich fuer diesen Code)", 1));
        }

        // Streckenschaden ohne sinnvolle Laenge
        if (sample.IsStreckenschaden
            && sample.MeterEnd <= sample.MeterStart)
        {
            issues.Add(new("Streckenschaden ohne Ausdehnung (MeterEnd <= MeterStart)", 1));
        }

        // ── Ergebnis berechnen ────────────────────────────────────────

        if (issues.Count == 0)
            return new SampleQualityResult(SampleQualityGrade.Green, []);

        var totalWeight = issues.Sum(i => i.Weight);
        var grade = totalWeight >= RedThreshold
            ? SampleQualityGrade.Red
            : SampleQualityGrade.Yellow;

        return new SampleQualityResult(grade, issues.Select(i => i.Text).ToList());
    }

    /// <summary>Prueft eine Liste von Samples und gibt Statistik zurueck.</summary>
    public SampleQualityBatchResult EvaluateBatch(IReadOnlyList<TrainingSample> samples)
    {
        var results = new List<(TrainingSample Sample, SampleQualityResult Result)>();
        foreach (var s in samples)
            results.Add((s, Evaluate(s)));

        return new SampleQualityBatchResult(results);
    }

    // ── Hilfsmethoden ─────────────────────────────────────────────────

    /// <summary>Sofortiges Red ohne weitere Pruefung.</summary>
    private static SampleQualityResult HardRed(string reason)
        => new(SampleQualityGrade.Red, [reason]);

    /// <summary>Prueft ob ein Code normalerweise bei MeterStart=0 vorkommt.</summary>
    private static bool IsZeroMeterCode(string code)
    {
        foreach (var prefix in ZeroMeterCodes)
        {
            if (code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Interner Typ fuer gewichtete Maengel.</summary>
    private readonly record struct QualityIssue(string Text, int Weight);
}

// ── Ergebnis-Typen ──────────────────────────────────────────────────────

public enum SampleQualityGrade { Green, Yellow, Red }

public sealed record SampleQualityResult(
    SampleQualityGrade Grade,
    IReadOnlyList<string> Issues)
{
    public bool IsGreen => Grade == SampleQualityGrade.Green;
    public bool IsAcceptable => Grade != SampleQualityGrade.Red;
}

public sealed record SampleQualityBatchResult(
    IReadOnlyList<(TrainingSample Sample, SampleQualityResult Result)> Results)
{
    public int Green => Results.Count(r => r.Result.Grade == SampleQualityGrade.Green);
    public int Yellow => Results.Count(r => r.Result.Grade == SampleQualityGrade.Yellow);
    public int Red => Results.Count(r => r.Result.Grade == SampleQualityGrade.Red);
    public int Total => Results.Count;

    /// <summary>Nur Samples die das Gate passiert haben (Green + Yellow).</summary>
    public IReadOnlyList<TrainingSample> Accepted =>
        Results.Where(r => r.Result.IsAcceptable).Select(r => r.Sample).ToList();

    /// <summary>Abgelehnte Samples (Red).</summary>
    public IReadOnlyList<TrainingSample> Rejected =>
        Results.Where(r => !r.Result.IsAcceptable).Select(r => r.Sample).ToList();
}

// Tests fuer H5 aus V4.2 Gesamt-Audit: EvalRunnerService (~300 Zeilen) war
// komplett ungetestet. CSV-Format, Per-Code-Metriken, Overall-Metriken,
// Git-Integration und Pfad-Validierung sind aber V4.2-Qualitaetshebel #1
// — Benchmark-Aussagen sind nur so verlaesslich wie der Runner selbst.
//
// Die Helper-Methoden sind als `internal static` sichtbar; EvalSample wurde
// zu `internal` angehoben. InternalsVisibleTo auf Pipeline.Tests ist im
// UI-Projekt gesetzt, keine Reflection noetig.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class EvalRunnerServiceTests : IDisposable
{
    private readonly string _tempDir;

    public EvalRunnerServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SewerStudioEvalRunnerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private static EvalRunnerService.EvalSample Sample(string path, string? expected, string? predicted = null, string? error = null)
        => new(path, expected) { PredictedCode = predicted, Error = error };

    // ── ExtractMainCode: Haupt-3-Zeichen-Extraktion ──

    [Fact]
    public void ExtractMainCode_NullOrEmpty_LiefertNull()
    {
        Assert.Null(EvalRunnerService.ExtractMainCode(null));
        Assert.Null(EvalRunnerService.ExtractMainCode(""));
        Assert.Null(EvalRunnerService.ExtractMainCode("   "));
    }

    [Fact]
    public void ExtractMainCode_KuerzerAls3_LiefertInputUppercase()
    {
        Assert.Equal("BA", EvalRunnerService.ExtractMainCode("ba"));
        Assert.Equal("X", EvalRunnerService.ExtractMainCode("x"));
    }

    [Fact]
    public void ExtractMainCode_LaengerAls3_LiefertPrefix3Uppercase()
    {
        Assert.Equal("BAB", EvalRunnerService.ExtractMainCode("BABBA"));
        Assert.Equal("BCD", EvalRunnerService.ExtractMainCode("bcd_whatever"));
    }

    [Fact]
    public void ExtractMainCode_IstCaseInsensitiv()
    {
        Assert.Equal("BAB", EvalRunnerService.ExtractMainCode("babba"));
        Assert.Equal("BAB", EvalRunnerService.ExtractMainCode("BabBa"));
    }

    [Fact]
    public void ExtractMainCode_WhitespaceGetrimmt()
    {
        Assert.Equal("BCD", EvalRunnerService.ExtractMainCode("  bcd_01  "));
    }

    // ── LoadExpectedCode: YOLO-Label-Parse ──

    [Fact]
    public void LoadExpectedCode_NichtExistenteDatei_LiefertNull()
    {
        var path = Path.Combine(_tempDir, "doesnotexist.txt");
        Assert.Null(EvalRunnerService.LoadExpectedCode(path));
    }

    [Fact]
    public void LoadExpectedCode_LeereDatei_LiefertNull()
    {
        // Leere Label-Datei = Negativ-Beispiel
        var path = Path.Combine(_tempDir, "empty.txt");
        File.WriteAllText(path, "");
        Assert.Null(EvalRunnerService.LoadExpectedCode(path));
    }

    [Fact]
    public void LoadExpectedCode_UngueltigeClassId_LiefertNull()
    {
        var path = Path.Combine(_tempDir, "bad.txt");
        File.WriteAllText(path, "not_a_number 0.5 0.5 0.1 0.1");
        Assert.Null(EvalRunnerService.LoadExpectedCode(path));
    }

    [Fact]
    public void LoadExpectedCode_YoloFormat_NimmtErstesTokenAlsClassId()
    {
        // YOLO-Label-Zeile: class_id x y w h — classId=0 mappt per VsaYoloClassMap
        var path = Path.Combine(_tempDir, "label.txt");
        File.WriteAllText(path, "0 0.5 0.5 0.1 0.1");
        var code = EvalRunnerService.LoadExpectedCode(path);
        // Ergebnis haengt von VsaYoloClassMap ab — wichtig ist, dass nicht null und nicht „not_a_number"
        Assert.NotNull(code);
    }

    [Fact]
    public void LoadExpectedCode_NimmtNurErsteZeile()
    {
        var path = Path.Combine(_tempDir, "multi.txt");
        File.WriteAllText(path, "0 0.1 0.1 0.1 0.1\nXXX garbage line\n");
        // 1. Zeile parst -> nicht null
        var code = EvalRunnerService.LoadExpectedCode(path);
        Assert.NotNull(code);
    }

    // ── ComputeOverallMetrics ──

    [Fact]
    public void ComputeOverallMetrics_AllesKorrekt_F1Eins()
    {
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("a.png", "BAB", "BAB"),
            Sample("b.png", "BAC", "BAC"),
            Sample("c.png", "BCD", "BCD"),
        };
        var (p, r, f1) = EvalRunnerService.ComputeOverallMetrics(samples);
        Assert.Equal(1.0, p, 3);
        Assert.Equal(1.0, r, 3);
        Assert.Equal(1.0, f1, 3);
    }

    [Fact]
    public void ComputeOverallMetrics_AllesFalsch_F1Null()
    {
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("a.png", "BAB", "XXX"),
            Sample("b.png", "BAC", "YYY"),
        };
        var (_, _, f1) = EvalRunnerService.ComputeOverallMetrics(samples);
        Assert.Equal(0.0, f1, 3);
    }

    [Fact]
    public void ComputeOverallMetrics_Halb_F1IstEinHalb()
    {
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("a.png", "BAB", "BAB"), // TP
            Sample("b.png", "BAC", "XXX"), // FN+FP (prediction not correct, expected exists)
        };
        // tp=1, fp=1, fn=1 → P=0.5, R=0.5, F1=0.5
        var (p, r, f1) = EvalRunnerService.ComputeOverallMetrics(samples);
        Assert.Equal(0.5, p, 3);
        Assert.Equal(0.5, r, 3);
        Assert.Equal(0.5, f1, 3);
    }

    [Fact]
    public void ComputeOverallMetrics_ErrorPredictionZaehltNichtAlsFP()
    {
        // Wenn das Modell ERROR liefert, soll das nicht als False Positive zaehlen
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("a.png", "BAB", "ERROR", error: "timeout"),
        };
        var (p, r, f1) = EvalRunnerService.ComputeOverallMetrics(samples);
        // fp=0, tp=0, fn=1 → P undefiniert→0, R=0, F1=0
        Assert.Equal(0.0, p, 3);
        Assert.Equal(0.0, r, 3);
        Assert.Equal(0.0, f1, 3);
    }

    [Fact]
    public void ComputeOverallMetrics_LeereListe_NullMetriken()
    {
        var samples = new List<EvalRunnerService.EvalSample>();
        var (p, r, f1) = EvalRunnerService.ComputeOverallMetrics(samples);
        Assert.Equal(0.0, p);
        Assert.Equal(0.0, r);
        Assert.Equal(0.0, f1);
    }

    // ── ComputePerCodeMetrics ──

    [Fact]
    public void ComputePerCodeMetrics_TrennungProCode()
    {
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("a.png", "BAB", "BAB"), // BAB TP
            Sample("b.png", "BAB", "BAC"), // BAB FN, BAC FP
            Sample("c.png", "BAC", "BAC"), // BAC TP
            Sample("d.png", "BCD", null),  // BCD FN (kein FP)
        };
        var result = EvalRunnerService.ComputePerCodeMetrics(samples);

        Assert.Equal(3, result.Count);

        var bab = result["BAB"];
        Assert.Equal(2, bab.Support);
        Assert.Equal(1, bab.TP);
        Assert.Equal(0, bab.FP);
        Assert.Equal(1, bab.FN);

        var bac = result["BAC"];
        Assert.Equal(1, bac.Support);
        Assert.Equal(1, bac.TP);
        Assert.Equal(1, bac.FP); // BAB wurde als BAC vorhergesagt
        Assert.Equal(0, bac.FN);

        var bcd = result["BCD"];
        Assert.Equal(1, bcd.Support);
        Assert.Equal(0, bcd.TP);
        Assert.Equal(1, bcd.FN);
    }

    [Fact]
    public void ComputePerCodeMetrics_CaseInsensitiv()
    {
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("a.png", "BAB", "bab"), // gleicher Code, andere Schreibweise → TP
        };
        var result = EvalRunnerService.ComputePerCodeMetrics(samples);
        // Dict ist OrdinalIgnoreCase → nur ein Eintrag
        Assert.Single(result);
        Assert.Equal(1, result.First().Value.TP);
    }

    [Fact]
    public void ComputePerCodeMetrics_F1FormelKorrekt()
    {
        // 4× BAB erwartet, 2× korrekt vorhergesagt, 2× als BAC; 2× BAC erwartet, 2× TP
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("1.png", "BAB", "BAB"),
            Sample("2.png", "BAB", "BAB"),
            Sample("3.png", "BAB", "BAC"),
            Sample("4.png", "BAB", "BAC"),
            Sample("5.png", "BAC", "BAC"),
            Sample("6.png", "BAC", "BAC"),
        };
        var result = EvalRunnerService.ComputePerCodeMetrics(samples);

        // BAB: tp=2, fp=0, fn=2 → P=1.0, R=0.5, F1 = 2*1*0.5/(1+0.5) = 0.667
        var bab = result["BAB"];
        Assert.Equal(1.0, bab.Precision, 3);
        Assert.Equal(0.5, bab.Recall, 3);
        Assert.Equal(0.667, bab.F1, 3);

        // BAC: tp=2, fp=2, fn=0 → P=0.5, R=1.0, F1 = 0.667
        var bac = result["BAC"];
        Assert.Equal(0.5, bac.Precision, 3);
        Assert.Equal(1.0, bac.Recall, 3);
        Assert.Equal(0.667, bac.F1, 3);
    }

    // ── WriteCsv ──

    [Fact]
    public void WriteCsv_SchreibtDateiMitHeaderAndOverallUndPerCode()
    {
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("a.png", "BAB", "BAB"),
            Sample("b.png", "BAC", "BAC"),
        };
        var perCode = EvalRunnerService.ComputePerCodeMetrics(samples);
        var path = Path.Combine(_tempDir, "out.csv");

        EvalRunnerService.WriteCsv(path, samples, perCode, "abc1234");

        Assert.True(File.Exists(path));
        var lines = File.ReadAllLines(path);

        // Header-Kommentar mit Commit
        Assert.Contains(lines, l => l.StartsWith("# Eval-Run"));
        Assert.Contains(lines, l => l.Contains("# Git-Commit: abc1234"));
        Assert.Contains(lines, l => l.Contains("# Samples: 2"));

        // Per-Code-Block-Header
        Assert.Contains("Code;Support;TP;FP;FN;Precision;Recall;F1", lines);
        // Frame-Detail-Block-Header
        Assert.Contains("File;Expected;Predicted;Match;Error", lines);
        // Frame-Detail-Eintrag
        Assert.Contains(lines, l => l.StartsWith("a.png;BAB;BAB;1;"));
    }

    [Fact]
    public void WriteCsv_OhneCommit_LaesstGitZeileWeg()
    {
        var samples = new List<EvalRunnerService.EvalSample> { Sample("a.png", "BAB", "BAB") };
        var perCode = EvalRunnerService.ComputePerCodeMetrics(samples);
        var path = Path.Combine(_tempDir, "nocommit.csv");

        EvalRunnerService.WriteCsv(path, samples, perCode, commit: null);

        var content = File.ReadAllText(path);
        Assert.DoesNotContain("# Git-Commit", content);
    }

    [Fact]
    public void WriteCsv_EscaptSemikolonUndZeilenumbruecheInError()
    {
        // Error-Strings koennen Semikolons/Newlines enthalten → CSV muss robust sein
        var samples = new List<EvalRunnerService.EvalSample>
        {
            Sample("crash.png", "BAB", "ERROR", error: "timeout;connection\nreset\r")
        };
        var perCode = EvalRunnerService.ComputePerCodeMetrics(samples);
        var path = Path.Combine(_tempDir, "err.csv");

        EvalRunnerService.WriteCsv(path, samples, perCode, null);

        var lines = File.ReadAllLines(path);
        var errLine = lines.First(l => l.StartsWith("crash.png;"));
        Assert.DoesNotContain("\n", errLine);
        Assert.DoesNotContain("\r", errLine);
        // Semikolons in Error wurden zu Komma ersetzt, damit Spalten stabil bleiben
        var parts = errLine.Split(';');
        Assert.Equal(5, parts.Length); // File;Expected;Predicted;Match;Error
    }

    // ── TryGetGitCommit (repo-unabhaengig) ──

    [Fact]
    public void TryGetGitCommit_CrashFreiBeiFehlendemRepo()
    {
        // Darf unter keinen Umstaenden werfen. Rueckgabe ist null oder ein Hash/Ref-Inhalt.
        var commit = EvalRunnerService.TryGetGitCommit();
        // Kein Assert fuer konkreten Wert — wir pruefen nur dass kein Crash passiert
        // und bei vorhandenem .git ein nicht-leerer String kommt.
        if (commit is not null)
            Assert.NotEmpty(commit);
    }
}

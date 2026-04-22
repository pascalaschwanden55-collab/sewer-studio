using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.Ai.QualityGate;
using AuswertungPro.Next.UI.Ai.Shared;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Erzwingt die 4-Layer-Architektur (Domain → Application → Infrastructure → UI).
/// Verhindert illegale Abhaengigkeiten:
/// - Domain darf nichts referenzieren (reine Modelle)
/// - Application darf nur Domain referenzieren
/// - Infrastructure darf Domain + Application referenzieren, aber nicht UI
/// - UI darf alles referenzieren
///
/// Zusaetzlich: Thin-AI-Prinzip — keine Business-Logik im Python-Sidecar.
/// </summary>
[Trait("Category", "Architecture")]
public class ArchitectureLayerGuardTests
{
    private static readonly string SolutionRoot = FindSolutionRoot();

    // ── Layer-Referenz-Regeln ─────────────────────────────────────

    [Fact]
    public void Domain_Referenziert_Keine_Anderen_Projekte()
    {
        var csproj = Path.Combine(SolutionRoot, "src", "AuswertungPro.Next.Domain",
            "AuswertungPro.Next.Domain.csproj");
        var refs = ExtractProjectReferences(csproj);

        Assert.Empty(refs); // Domain = Modelle, Enums, Records — kein I/O, keine Abhaengigkeiten
    }

    [Fact]
    public void Application_Referenziert_Nur_Domain()
    {
        var csproj = Path.Combine(SolutionRoot, "src", "AuswertungPro.Next.Application",
            "AuswertungPro.Next.Application.csproj");
        var refs = ExtractProjectReferences(csproj);

        foreach (var r in refs)
        {
            Assert.Contains("Domain", r, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Infrastructure", r, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(".UI", r, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Infrastructure_Referenziert_Nicht_UI()
    {
        var csproj = Path.Combine(SolutionRoot, "src", "AuswertungPro.Next.Infrastructure",
            "AuswertungPro.Next.Infrastructure.csproj");
        var refs = ExtractProjectReferences(csproj);

        foreach (var r in refs)
        {
            Assert.DoesNotContain(".UI", r, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Thin-AI-Prinzip ──────────────────────────────────────────

    [Fact]
    public void Sidecar_Enthaelt_Keine_BusinessLogik()
    {
        // Python-Sidecar darf nur Inference machen (YOLO, DINO, SAM),
        // keine VSA-Codes, keine Plausibilitaet, keine QualityGate-Logik
        var sidecarDir = Path.Combine(SolutionRoot, "sidecar", "sidecar");
        if (!Directory.Exists(sidecarDir))
        {
            // Sidecar nicht vorhanden — OK (z.B. CI ohne Python)
            return;
        }

        var pyFiles = Directory.GetFiles(sidecarDir, "*.py", SearchOption.AllDirectories);
        var verbotenePatterns = new[]
        {
            @"\bVSA\b",              // VSA-Code-Logik gehoert nach C#
            @"\bQualityGate\b",      // QualityGate gehoert nach C#
            @"\bPlausibilityService\b",  // PlausibilityService gehoert nach C# (Kommentare OK)
            @"\bSeverity\s*[=<>]",   // Severity-Bewertung gehoert nach C#
            @"\bEN.?13508\b",        // Norm-Logik gehoert nach C#
        };

        var violations = new List<string>();
        foreach (var pyFile in pyFiles)
        {
            // Kommentare + Docstrings entfernen — Thin-AI-Prinzip gilt fuer ausfuehrbaren Code,
            // dokumentarische Erwaehnungen von Codes/Begriffen sind erlaubt (s. „Kommentare OK").
            var content = StripPythonCommentsAndDocstrings(File.ReadAllText(pyFile));
            foreach (var pattern in verbotenePatterns)
            {
                if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                {
                    var fileName = Path.GetRelativePath(SolutionRoot, pyFile);
                    violations.Add($"{fileName}: Pattern '{pattern}' gefunden — Business-Logik gehoert nach C#");
                }
            }
        }

        Assert.True(violations.Count == 0,
            $"Thin-AI-Verletzung im Sidecar:\n{string.Join("\n", violations)}");
    }

    /// <summary>
    /// Entfernt Python-Docstrings ("""...""" und '''...''') und Zeilenkommentare (#...)
    /// aus dem Quelltext, damit Pattern-Checks nur noch Code treffen.
    /// </summary>
    private static string StripPythonCommentsAndDocstrings(string src)
    {
        // Triple-quoted Strings (inkl. mehrzeilig) entfernen.
        src = Regex.Replace(src, @"""""""[\s\S]*?""""""", "", RegexOptions.Multiline);
        src = Regex.Replace(src, @"'''[\s\S]*?'''", "", RegexOptions.Multiline);
        // Zeilenkommentare (robust genug fuer Sidecar-Files: keine # in Strings dort).
        src = Regex.Replace(src, @"#[^\n]*", "", RegexOptions.Multiline);
        return src;
    }

    // ── QualityGate-Replay Smoke-Test ────────────────────────────

    [Fact]
    public void QualityGateReplay_Erkennt_Regression()
    {
        var evidence = new[]
        {
            new EvidenceVector(YoloConf: 0.8, DinoConf: 0.9, PlausibilityScore: 0.85),
            new EvidenceVector(YoloConf: 0.5, DinoConf: 0.6, PlausibilityScore: 0.5),
            new EvidenceVector(YoloConf: 0.3, DinoConf: 0.3, PlausibilityScore: 0.3),
        };

        // "Gute" Gewichte: DINO hoch
        var goodWeights = new CategoryWeights
        {
            WDino = 0.40, WYolo = 0.30, WPlausibility = 0.30
        };

        // "Schlechte" Gewichte: alles gleich, DINO abgewertet
        var badWeights = new CategoryWeights
        {
            WDino = 0.10, WYolo = 0.10, WPlausibility = 0.10, WLlm = 0.40, WSam = 0.30
        };

        var (before, after) = QualityGateReplay.CompareWeights(
            evidence, goodWeights, badWeights);

        // Mit schlechten Gewichten sollten mehr Eintraege absinken (weniger Green)
        Assert.True(after.GreenAfter <= before.GreenAfter,
            $"Erwartet: weniger Green nach Weight-Regression. Before={before.GreenAfter}, After={after.GreenAfter}");
    }

    // ── Walker-Health-Check Smoke-Test ───────────────────────────

    [Fact]
    public void WalkerHealthCheck_PythonVenv_Findet_Sidecar()
    {
        var result = WalkerHealthCheck.CheckPythonVenv();
        // In der Entwicklungsumgebung sollte das Venv existieren
        // Im CI kann es fehlen — dann ist der Check trotzdem korrekt (Ok=false)
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Detail));
    }

    [Fact]
    public void WalkerHealthCheck_Ffmpeg_Check()
    {
        var result = WalkerHealthCheck.CheckFfmpeg();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result.Detail));
    }

    [Fact]
    public void WalkerHealthCheck_KnowledgeBase_Check()
    {
        var result = WalkerHealthCheck.CheckKnowledgeBase();
        Assert.NotNull(result);
        // Wenn KB existiert → Ok=true + Details; wenn nicht → Ok=false + Pfad
        Assert.False(string.IsNullOrEmpty(result.Detail));
    }

    // ── Hilfsmethoden ────────────────────────────────────────────

    private static List<string> ExtractProjectReferences(string csprojPath)
    {
        if (!File.Exists(csprojPath))
            return new List<string>();

        var content = File.ReadAllText(csprojPath);
        var refs = new List<string>();

        // ProjectReference Include="..\..\xxx\xxx.csproj"
        foreach (Match match in Regex.Matches(content, @"ProjectReference\s+Include=""([^""]+)"""))
        {
            refs.Add(match.Groups[1].Value);
        }

        return refs;
    }

    private static string FindSolutionRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "AuswertungPro.sln")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback
        return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
            "..", "..", "..", "..", ".."));
    }
}

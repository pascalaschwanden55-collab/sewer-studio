// Guardrail-Tests: Sicherstellung dass Batch-Pfade nie AnalyzeWithEscalationAsync aufrufen
// und Eskalationskriterien korrekt auswerten.
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

[Trait("Category", "Unit")]
public sealed class EscalationGuardrailTests
{
    // ── Guardrail A: Batch-Pfade duerfen NICHT AnalyzeWithEscalationAsync aufrufen ──

    /// <summary>
    /// Stellt sicher dass MultiModelAnalysisService nie AnalyzeWithEscalationAsync aufruft.
    /// Eskalation mit VRAM-Swap ist fuer Einzelbilder, nicht fuer Batch-Verarbeitung.
    /// </summary>
    [Fact]
    public void MultiModelAnalysisService_NeverCallsEscalation()
    {
        var sourceFile = FindSourceFile("MultiModelAnalysisService.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);
        Assert.DoesNotContain("AnalyzeWithEscalationAsync", content,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Stellt sicher dass SelfTrainingOrchestrator nie AnalyzeWithEscalationAsync aufruft.
    /// Selbsttraining misst die 8B-Erkennungsqualitaet — Eskalation wuerde das verfaelschen.
    /// </summary>
    [Fact]
    public void SelfTrainingOrchestrator_NeverCallsEscalation()
    {
        var sourceFile = FindSourceFile("SelfTrainingOrchestrator.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);
        Assert.DoesNotContain("AnalyzeWithEscalationAsync", content,
            StringComparison.Ordinal);
    }

    // ── Hilfsmethoden ──

    /// <summary>Findet eine Quelldatei im src-Verzeichnis relativ zum Test-Projekt.</summary>
    private static string FindSourceFile(string fileName)
    {
        // Von tests/...Tests/ nach src/ navigieren
        var testDir = AppContext.BaseDirectory;
        var solutionDir = testDir;
        while (solutionDir is not null && !File.Exists(Path.Combine(solutionDir, "AuswertungPro.sln")))
            solutionDir = Path.GetDirectoryName(solutionDir);

        if (solutionDir is null)
            return fileName; // Fallback: Assert.True(File.Exists) schlaegt fehl

        // Rekursiv suchen
        var matches = Directory.GetFiles(Path.Combine(solutionDir, "src"), fileName, SearchOption.AllDirectories);
        return matches.Length > 0 ? matches[0] : fileName;
    }
}

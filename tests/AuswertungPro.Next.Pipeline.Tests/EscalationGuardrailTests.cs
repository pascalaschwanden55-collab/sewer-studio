// Guardrail-Tests: Sicherstellung dass Batch-Pfade nie AnalyzeWithEscalationAsync aufrufen
// und Eskalationskriterien korrekt auswerten.
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Infrastructure.Ai;
using AuswertungPro.Next.UI.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

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

    /// <summary>
    /// Der sichtbare PlayerWindow-Codiermodus ist ein Einzelbild-/Live-Pfad.
    /// Er muss denselben Qwen-Retry wie CodingModeWindow nutzen, sonst endet
    /// ein schwacher Erstversuch direkt als "Kein Befund".
    /// </summary>
    [Fact]
    public void PlayerWindowCodingMode_UsesEscalationForLiveQwenFallback()
    {
        var sourceFile = FindSourceFile("PlayerWindow.CodingMode.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);
        Assert.Contains("_codingEnhancedVision.AnalyzeWithEscalationAsync", content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EscalationReason_RetriesNoFindingFrameWhenMeterWasRead()
    {
        var analysis = new EnhancedFrameAnalysis(
            Meter: 0.71,
            PipeMaterial: "unbekannt",
            PipeDiameterMm: 150,
            Findings: Array.Empty<EnhancedFinding>(),
            ImageQuality: "mittel",
            IsEmptyFrame: true,
            Error: null,
            ViewType: "axial");

        var reason = InvokeEscalationReason(analysis);

        Assert.Equal("NoFindings", reason);
    }

    [Fact]
    public void EscalationReason_DoesNotRetryTrulyEmptyFrameWithoutMeter()
    {
        var analysis = new EnhancedFrameAnalysis(
            Meter: null,
            PipeMaterial: "unbekannt",
            PipeDiameterMm: null,
            Findings: Array.Empty<EnhancedFinding>(),
            ImageQuality: "schlecht",
            IsEmptyFrame: true,
            Error: null,
            ViewType: "axial");

        var reason = InvokeEscalationReason(analysis);

        Assert.Equal("None", reason);
    }

    [Fact]
    public void EnhancedVisionRetryPrompt_HasDedicatedNoFindingsHint()
    {
        var sourceFile = FindSourceFile("EnhancedVisionAnalysisService.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        Assert.Contains("EscalationReason.NoFindings =>", content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerWindowCodingMode_RechecksSidecarBeforeEachFrameAnalysis()
    {
        var sourceFile = FindSourceFile("PlayerWindow.CodingMode.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        Assert.Contains("TryEnsureCodingMultiModelAsync(_codingAnalysisCts.Token)", content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerWindowCodingMode_DisplaysSamRingScanMasksWithoutDinoBoxes()
    {
        var sourceFile = FindSourceFile("PlayerWindow.CodingMode.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        Assert.Contains("SAM Ring-Scan", content, StringComparison.Ordinal);
        Assert.Contains("!mmResult.HasDetections", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerWindowCodingMode_PassesSamContextToQwenFallback()
    {
        var sourceFile = FindSourceFile("PlayerWindow.CodingMode.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        Assert.Contains("CreateQwenContext(mmResult", content, StringComparison.Ordinal);
        Assert.Contains("b64, qwenContext", content, StringComparison.Ordinal);
        Assert.DoesNotContain("b64, context: null", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerWindowCodingMode_RingScanUsesLightweightPreviewRenderer()
    {
        var sourceFile = FindSourceFile("PlayerWindow.CodingMode.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        Assert.Contains("ShowRingScanPreview(", content, StringComparison.Ordinal);
        Assert.Contains("RenderMaskBoundingBoxes", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerWindowCodingMode_DoesNotReportPlainNoFindingWhenSamCandidatesExist()
    {
        var sourceFile = FindSourceFile("PlayerWindow.CodingMode.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        Assert.Contains("SAM-Kandidaten, kein VSA-Code", content, StringComparison.Ordinal);
        Assert.Contains("qwenContext?.SamMasks.Count > 0", content, StringComparison.Ordinal);
    }

    [Fact]
    public void EnhancedVisionContextPrompt_ExplainsRingSegmentsAreCandidates()
    {
        var sourceFile = FindSourceFile("EnhancedVisionAnalysisService.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        Assert.Contains("ring_segment_* sind nur SAM-Ring-Scan-Kandidaten", content,
            StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerWindowCodingMode_DoesNotAutoCreateYellowQualityGateEvents()
    {
        var sourceFile = FindSourceFile("PlayerWindow.CodingMode.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);
        var methodStart = content.IndexOf("AddAiFindingsAsEvents(", StringComparison.Ordinal);
        var gateCheck = content.IndexOf("if (!gateResult.IsGreen)", methodStart, StringComparison.Ordinal);
        var addEvent = content.IndexOf("_codingSessionService?.AddEvent(entry)", methodStart, StringComparison.Ordinal);

        Assert.True(methodStart >= 0, "AddAiFindingsAsEvents nicht gefunden.");
        Assert.True(gateCheck >= 0, "Nicht-gruene QualityGate-Ergebnisse muessen vor AddEvent gefiltert werden.");
        Assert.True(addEvent >= 0, "AddEvent-Aufruf nicht gefunden.");
        Assert.True(gateCheck < addEvent, "QualityGate-Filter muss vor Event-Erzeugung liegen.");
        Assert.Contains("QualityGateRed", content, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerWindowCodingMode_UsesCurrentDetectionOrOsdMeterForQwenEvents()
    {
        var sourceFile = FindSourceFile("PlayerWindow.CodingMode.cs");
        Assert.True(File.Exists(sourceFile), $"Quelldatei nicht gefunden: {sourceFile}");

        var content = File.ReadAllText(sourceFile);

        Assert.Contains("var currentMeter = ResolveAnalysisMeter(result)", content, StringComparison.Ordinal);
        Assert.Contains("double meter = ResolveAnalysisMeter(result);", content, StringComparison.Ordinal);
        Assert.Contains("result.MeterReading is > 0.001 and <= 500", content, StringComparison.Ordinal);
    }

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

    private static string InvokeEscalationReason(EnhancedFrameAnalysis analysis)
    {
        var method = typeof(EnhancedVisionAnalysisService).GetMethod(
            "GetEscalationReason",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var value = method.Invoke(null, new object[] { analysis });
        return value?.ToString() ?? "";
    }
}

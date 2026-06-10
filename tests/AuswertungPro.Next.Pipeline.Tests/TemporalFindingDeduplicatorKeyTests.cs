using System;
using System.Reflection;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Prueft, dass der Dedup-Key-Builder Labels ohne expliziten Code ueber den
/// zentralen VsaCodeResolver aufloest. Die Logik (BuildFindingKey) lebt seit D6
/// (Commit 0242f583) in der internen Klasse TemporalFindingDeduplicator, nicht
/// mehr in VideoFullAnalysisService — daher Zugriff per Reflection ueber die
/// Infrastructure-Assembly. Die Label-zu-Code-Zuordnung selbst ist zusaetzlich
/// in VsaCodeResolverTests abgedeckt; dieser Test sichert die Verdrahtung
/// (VsaCodeHint == null -> InferCodeFromLabel).
/// </summary>
[Collection(VsaCodeResolverTestCollection.Name)]
public sealed class TemporalFindingDeduplicatorKeyTests
{
    public TemporalFindingDeduplicatorKeyTests()
    {
        VsaResolverTestCatalog.ConfigureDefault();
    }

    [Theory]
    [InlineData("Wurzeleinwuchs", "BBA")]
    [InlineData("root intrusion", "BBA")]
    [InlineData("Inkrustation verkalkt", "BBB")]
    [InlineData("attached deposit", "BBB")]
    public void BuildFindingKey_InfersCodeFromLabel_ViaCentralResolver(string label, string expectedCode)
    {
        var finding = new EnhancedFinding(
            Label: label,
            VsaCodeHint: null,
            Severity: 3,
            PositionClock: null,
            ExtentPercent: null,
            HeightMm: null,
            WidthMm: null,
            IntrusionPercent: null,
            CrossSectionReductionPercent: null,
            DiameterReductionMm: null,
            BboxX1: null,
            BboxY1: null,
            BboxX2: null,
            BboxY2: null,
            Notes: null);

        // TemporalFindingDeduplicator + TemporalDedupOptions sind internal und fuer
        // Pipeline.Tests nicht direkt sichtbar -> ueber die Infrastructure-Assembly reflektieren.
        var infrastructure = typeof(VsaCodeResolver).Assembly;

        var optionsType = infrastructure.GetType(
            "AuswertungPro.Next.Infrastructure.Ai.Pipeline.TemporalDedupOptions");
        Assert.NotNull(optionsType);
        var options = Activator.CreateInstance(optionsType!);

        var dedupType = infrastructure.GetType(
            "AuswertungPro.Next.Infrastructure.Ai.Pipeline.TemporalFindingDeduplicator");
        Assert.NotNull(dedupType);
        var deduplicator = Activator.CreateInstance(dedupType!, options);

        var method = dedupType!.GetMethod(
            "BuildFindingKey",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(method);

        var key = Assert.IsType<string>(method!.Invoke(deduplicator, new object[] { finding }));
        Assert.Equal(expectedCode, key);
    }

    [Theory]
    [InlineData(true, "BAB|6:00")]   // Default: Uhrlage trennt Befunde
    [InlineData(false, "BAB")]       // Klassifikator-Regime: Ganzbild-Code, Uhrlage kein Split-Kriterium
    public void BuildFindingKey_RespektiertClockInKey(bool clockInKey, string expectedKey)
    {
        var finding = new EnhancedFinding(
            Label: "crack",
            VsaCodeHint: "BAB",
            Severity: 2,
            PositionClock: "6:00",
            ExtentPercent: 30,
            HeightMm: null,
            WidthMm: null,
            IntrusionPercent: null,
            CrossSectionReductionPercent: null,
            DiameterReductionMm: null,
            BboxX1: null,
            BboxY1: null,
            BboxX2: null,
            BboxY2: null,
            Notes: null);

        var infrastructure = typeof(VsaCodeResolver).Assembly;
        var optionsType = infrastructure.GetType(
            "AuswertungPro.Next.Infrastructure.Ai.Pipeline.TemporalDedupOptions")!;
        var options = Activator.CreateInstance(optionsType)!;
        optionsType.GetProperty("ClockInKey")!.SetValue(options, clockInKey);

        var dedupType = infrastructure.GetType(
            "AuswertungPro.Next.Infrastructure.Ai.Pipeline.TemporalFindingDeduplicator")!;
        var deduplicator = Activator.CreateInstance(dedupType, options);
        var method = dedupType.GetMethod("BuildFindingKey", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var key = Assert.IsType<string>(method.Invoke(deduplicator, new object[] { finding }));
        Assert.Equal(expectedKey, key);
    }
}

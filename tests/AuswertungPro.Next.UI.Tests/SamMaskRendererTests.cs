using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Pipeline;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer die defensive RLE-Dekodierung in <see cref="SamMaskRenderer"/>.
/// Die RLE-Strings kommen ungeprueft vom Python-Sidecar — kaputte Werte duerfen
/// weder werfen noch durch absurde Dimensionen den Speicher sprengen.
/// </summary>
public class SamMaskRendererTests
{
    [Fact]
    public void DecodeRle_ValidRle_DecodesCorrectly()
    {
        // "1,2,2" auf 2x2: Start=Vordergrund, Lauf 2 (obere Zeile), Lauf 2 (untere, Hintergrund)
        var mask = SamMaskRenderer.DecodeRle("1,2,2", width: 2, height: 2);

        Assert.True(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.False(mask[1, 0]);
        Assert.False(mask[1, 1]);
    }

    [Fact]
    public void DecodeRle_InvalidStartToken_ReturnsEmptyMaskWithoutThrow()
    {
        var mask = SamMaskRenderer.DecodeRle("x,2,2", width: 2, height: 2);

        // Dimensionen bleiben gueltig, aber nichts ist gesetzt
        Assert.Equal(2, mask.GetLength(0));
        Assert.Equal(2, mask.GetLength(1));
        Assert.False(mask[0, 0]);
        Assert.False(mask[1, 1]);
    }

    [Fact]
    public void DecodeRle_InvalidRunToken_DecodesValidPrefixWithoutThrow()
    {
        // "1,2,abc,1": erster Lauf (2 Pixel Vordergrund) gueltig, danach Abbruch
        var mask = SamMaskRenderer.DecodeRle("1,2,abc,1", width: 2, height: 2);

        Assert.True(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.False(mask[1, 0]);
    }

    [Fact]
    public void DecodeRle_NegativeDimensions_ReturnsEmptyWithoutThrow()
    {
        var mask = SamMaskRenderer.DecodeRle("1,2,2", width: -1, height: -5);

        Assert.Equal(0, mask.GetLength(0));
        Assert.Equal(0, mask.GetLength(1));
    }

    [Fact]
    public void DecodeRle_HugeRunLength_DoesNotOverflowOrThrow()
    {
        // "1,3,2147483647,5": gueltiger Praefix (3 Pixel), dann ein Riesen-Run nahe int.MaxValue.
        // Ohne long-Arithmetik wuerde pos += runLength ueberlaufen → negativer Index → Crash.
        var mask = SamMaskRenderer.DecodeRle("1,3,2147483647,5", width: 2, height: 2);

        Assert.True(mask[0, 0]);
        Assert.True(mask[0, 1]);
        Assert.True(mask[1, 0]);
        Assert.False(mask[1, 1]);
    }

    [Fact]
    public void DecodeRle_OversizedDimensions_ReturnsEmptyWithoutAllocating()
    {
        // 100000 x 100000 waeren 10^10 bool = ~10 GB → muss abgewiesen werden
        var mask = SamMaskRenderer.DecodeRle("1,4,4", width: 100_000, height: 100_000);

        Assert.Equal(0, mask.GetLength(0));
        Assert.Equal(0, mask.GetLength(1));
    }

    [Fact]
    public void RenderCandidates_DoesNotDrawHiddenBackgroundMask()
    {
        Exception? threadError = null;
        int childCount = -1;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                var summary = SamMaskRenderer.RenderCandidates(
                    canvas,
                    [Candidate("water wall", 0.98, 0.32, 0.95)],
                    imageWidth: 100,
                    imageHeight: 100,
                    canvasWidth: 100,
                    canvasHeight: 100,
                    options: SamMaskRenderer.WinCanStyleOptions);

                childCount = canvas.Children.Count;
                Assert.Equal(1, summary.Hidden);
                Assert.Equal(0, summary.Rendered);
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        Assert.Equal(0, childCount);
    }

    [Fact]
    public void RenderCandidates_LargeDefectDrawsFillContourAndLabel()
    {
        // Backup-Verhalten (11.06): eine sichtbare Maske wird IMMER gefuellt + Kontur
        // gezeichnet, auch wenn sie grossflaechig ist. Kein OutlineOnly-Strippen mehr.
        Exception? threadError = null;
        int childCount = -1;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                var summary = SamMaskRenderer.RenderCandidates(
                    canvas,
                    [Candidate("incrustation infiltration", 0.86, 0.26, 0.55)],
                    imageWidth: 100,
                    imageHeight: 100,
                    canvasWidth: 100,
                    canvasHeight: 100,
                    options: SamMaskRenderer.WinCanStyleOptions);

                childCount = canvas.Children.Count;
                Assert.Equal(1, summary.Rendered);
                Assert.Equal(0, summary.OutlineOnly);
                Assert.Equal(1, summary.SubtleFill);
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        Assert.Equal(3, childCount); // fill path + contour path + label
    }

    [Fact]
    public void RenderMasks_NullBboxRendersMaskWithoutLabelOrThrow()
    {
        var logger = new CapturingLogger();
        Exception? threadError = null;
        int childCount = -1;

        var thread = new Thread(() =>
        {
            try
            {
                var canvas = new Canvas();
                var response = new SamResponse(
                    [
                        new SamMaskResult(
                            Label: "crack",
                            Confidence: 0.9,
                            Bbox: null!,
                            MaskRle: "1,1,3",
                            MaskAreaPixels: 1,
                            ImageAreaPixels: 4,
                            HeightPixels: 1,
                            WidthPixels: 1,
                            CentroidX: 0.5,
                            CentroidY: 0.5)
                    ],
                    ImageWidth: 2,
                    ImageHeight: 2,
                    InferenceTimeMs: 1);
                var quantified = new[]
                {
                    new MaskQuantificationService.QuantifiedMask("crack", 0.9, null, null, null, null, null, null)
                };

                SamMaskRenderer.RenderMasks(canvas, response, quantified, 100, 100, logger);
                childCount = canvas.Children.Count;
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(threadError);
        // Maske rendert (Fuellung + Kontur), aber kein Label-Badge, weil die Bbox null ist.
        // Bei samConfidence 0.9 (>= MinimumFillDetectionConfidence 0.60) und kleiner Flaeche
        // greift SubtleFill -> Fuellungs-Pfad + Kontur-Pfad = 2 Kinder, kein Label.
        Assert.Equal(2, childCount);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void DecideVisualMode_HidesConfirmedBackgroundWaterWall()
    {
        var candidate = Candidate("water wall", samConfidence: 0.98, dinoConfidence: 0.32, areaRatio: 0.95);

        var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

        Assert.Equal(SamMaskRenderer.MaskVisualMode.Hidden, decision.Mode);
        Assert.Equal("background_label", decision.Reason);
    }

    [Fact]
    public void DecideVisualMode_FillsLargeDefect()
    {
        // Backup-Verhalten: grosse Maske bleibt sichtbar UND gefuellt (kein OutlineOnly).
        var candidate = Candidate("incrustation infiltration", samConfidence: 0.86, dinoConfidence: 0.26, areaRatio: 0.55);

        var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

        Assert.Equal(SamMaskRenderer.MaskVisualMode.SubtleFill, decision.Mode);
    }

    [Fact]
    public void DecideVisualMode_FillsManualMaskWithoutDinoConfidence()
    {
        // Manueller Mark-Pfad: KEINE DINO-Confidence, SAM-Confidence unter 0.60. Im kaputten
        // Stand fiel das auf OutlineOnly (duenne Kontur). Backup-Verhalten: gefuellt.
        var candidate = Candidate("manuell", samConfidence: 0.45, dinoConfidence: null, areaRatio: 0.10);

        var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

        Assert.Equal(SamMaskRenderer.MaskVisualMode.SubtleFill, decision.Mode);
    }

    [Fact]
    public void DecideVisualMode_KeepsDinoThresholdFindingVisible()
    {
        var candidate = Candidate("root ball seal", samConfidence: 0.96, dinoConfidence: 0.26, areaRatio: 0.064);

        var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

        Assert.NotEqual(SamMaskRenderer.MaskVisualMode.Hidden, decision.Mode);
    }

    [Fact]
    public void DecideVisualMode_UsesSubtleFillForSmallHighConfidenceDefect()
    {
        var candidate = Candidate("root ball seal", samConfidence: 0.96, dinoConfidence: 0.72, areaRatio: 0.064);

        var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

        Assert.Equal(SamMaskRenderer.MaskVisualMode.SubtleFill, decision.Mode);
    }

    [Fact]
    public void DecideVisualMode_IsNullSafeForMissingBbox()
    {
        var mask = new SamMaskResult(
            Label: "root",
            Confidence: 0.8,
            Bbox: null!,
            MaskRle: "1,1,9999",
            MaskAreaPixels: 100,
            ImageAreaPixels: 10_000,
            HeightPixels: 10,
            WidthPixels: 10,
            CentroidX: 10,
            CentroidY: 10);
        var candidate = new SamMaskRenderer.MaskRenderCandidate(mask, Quant("root", 0.8), DetectionConfidence: 0.3);

        var decision = SamMaskRenderer.DecideVisualMode(candidate, SamMaskRenderer.WinCanStyleOptions);

        Assert.NotEqual(SamMaskRenderer.MaskVisualMode.Hidden, decision.Mode);
    }

    private static SamMaskRenderer.MaskRenderCandidate Candidate(
        string label,
        double samConfidence,
        double? dinoConfidence,
        double areaRatio)
    {
        var imageArea = 10_000;
        var maskArea = (int)Math.Round(imageArea * areaRatio);
        var mask = new SamMaskResult(
            Label: label,
            Confidence: samConfidence,
            Bbox: [10, 10, 40, 40],
            MaskRle: "1,1,9999",
            MaskAreaPixels: maskArea,
            ImageAreaPixels: imageArea,
            HeightPixels: 30,
            WidthPixels: 30,
            CentroidX: 25,
            CentroidY: 25);
        return new SamMaskRenderer.MaskRenderCandidate(mask, Quant(label, samConfidence), dinoConfidence);
    }

    private static MaskQuantificationService.QuantifiedMask Quant(string label, double confidence)
        => new(label, confidence, null, null, null, null, null, null);

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}

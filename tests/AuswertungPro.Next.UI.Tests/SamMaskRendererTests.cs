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
    public void RenderMasks_LogsSkippedMaskViaLogger()
    {
        var logger = new CapturingLogger();
        Exception? threadError = null;

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
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Contains("Maske 0", entry.Message);
        Assert.NotNull(entry.Exception);
    }

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

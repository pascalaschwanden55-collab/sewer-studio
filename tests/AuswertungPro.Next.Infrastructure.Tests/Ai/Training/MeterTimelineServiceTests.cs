using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training;

/// <summary>
/// AP-3 (Audit 2026-08-10): Erfundene Meterstaende duerfen nicht als "gemessen"
/// in die Trainingsdaten. Die alte Glaettung erfand bei unlesbarem OSD eine Reihe
/// aus Null-Metern und interpolierte ueber jede Luecke; die neue Kette nutzt
/// Sequenz-Plausibilitaet und Lueckenfuellen mit den drei Klammern.
/// </summary>
public sealed class MeterTimelineServiceTests
{
    [Fact]
    public void Unlesbares_OSD_liefert_keinen_einzigen_Meterwert()
    {
        // Frueher stand hier eine Zeitreihe aus lauter 0-Metern — eine erfundene
        // Null sieht aus wie eine Messung. Leer ist ehrlich.
        var folge = MeterTimelineService.BereinigeTimeline(
            [(0.0, null), (5.0, null), (10.0, null)]);

        Assert.NotEmpty(folge);
        Assert.All(folge, punkt => Assert.Null(punkt.Meter));
    }

    [Fact]
    public void Eine_Luecke_ueber_zehn_Sekunden_wird_nicht_gefuellt()
    {
        var folge = MeterTimelineService.BereinigeTimeline(
            [(0.0, 10.0), (5.0, null), (20.0, 11.0)]);

        var (meter, _) = MeterTimelineService.InterpolateMeter(folge, 5.0);
        Assert.Null(meter);
    }

    [Fact]
    public void Eine_Rueckwaertsfahrt_wird_nicht_interpoliert()
    {
        // Der Meterstand faellt zwischen zwei Messungen — die Kamera ist
        // zurueckgefahren; ein Zwischenwert waere falsch.
        var folge = MeterTimelineService.BereinigeTimeline(
            [(0.0, 10.0), (1.0, null), (2.0, 9.0)]);

        var (meter, _) = MeterTimelineService.InterpolateMeter(folge, 1.0);
        Assert.Null(meter);
    }

    [Fact]
    public void Ein_gefuellter_Wert_bleibt_als_geschaetzt_gekennzeichnet()
    {
        var folge = MeterTimelineService.BereinigeTimeline(
            [(0.0, 10.0), (1.0, null), (2.0, 10.6)]);

        var (meter, geschaetzt) = MeterTimelineService.InterpolateMeter(folge, 1.0);
        Assert.NotNull(meter);
        Assert.Equal(10.3, meter!.Value, 3);
        Assert.True(geschaetzt);
    }

    [Fact]
    public void Ein_Intervall_aus_zwei_gelesenen_Werten_gilt_nicht_als_geschaetzt()
    {
        var folge = MeterTimelineService.BereinigeTimeline(
            [(0.0, 10.0), (1.0, 10.5)]);

        var (meter, geschaetzt) = MeterTimelineService.InterpolateMeter(folge, 0.5);
        Assert.Equal(10.25, meter!.Value, 3);
        Assert.False(geschaetzt);
    }

    [Fact]
    public void Ausserhalb_der_Lesungen_wird_nichts_extrapoliert()
    {
        var folge = MeterTimelineService.BereinigeTimeline(
            [(10.0, 3.0), (11.0, 3.2)]);

        Assert.Null(MeterTimelineService.InterpolateMeter(folge, 5.0).Meter);
        Assert.Null(MeterTimelineService.InterpolateMeter(folge, 99.0).Meter);
    }

    [Fact]
    public void Dispose_gibt_den_besessenen_Netzwerkdienst_genau_einmal_frei()
    {
        var resource = new DisposeProbe();
        var service = new MeterTimelineService(RuntimeSettings(), ownedResource: resource);

        service.Dispose();
        service.Dispose();

        Assert.Equal(1, resource.Calls);
    }

    private static AiRuntimeSettings RuntimeSettings()
        => new(
            Enabled: true,
            OllamaBaseUri: new Uri("http://localhost:11434"),
            VisionModel: "vision",
            TextModel: "text",
            EmbedModel: "embed",
            FfmpegPath: "ffmpeg",
            OllamaRequestTimeout: TimeSpan.FromMinutes(1),
            OllamaKeepAlive: "5m",
            OllamaNumCtx: 2048);

    private sealed class DisposeProbe : IDisposable
    {
        public int Calls { get; private set; }

        public void Dispose() => Calls++;
    }
}

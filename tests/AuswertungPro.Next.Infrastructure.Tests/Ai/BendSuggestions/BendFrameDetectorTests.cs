using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.UseCases.BendSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.BendSuggestions;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.BendSuggestions;

/// <summary>
/// Uebersetzt die Sidecar-Antwort in genau drei Ausgaenge: Bogen gefunden, kein
/// Bogen, nicht ausgewertet. Alles andere ist ein technischer Fehler und muss
/// geworfen werden — er darf nie als "kein Bogen" erscheinen.
/// </summary>
public sealed class BendFrameDetectorTests
{
    private const string Id = "bcc_nc15_seed46_20260808";
    private const string Sha = "8ad82c1b0186ec02126a18f095d551d7a083faa90855350b22a6e893ac860114";

    [Fact]
    public async Task Ein_Treffer_liefert_die_hoechste_Konfidenz()
    {
        var ergebnis = await Frage(Antwort(detections:
        [
            Box("BCC_bogen", 0.62),
            Box("BCC_bogen", 0.86)
        ]));

        Assert.Equal(BendFrameOutcome.Detected, ergebnis.Outcome);
        Assert.Equal(0.86, ergebnis.Confidence, 3);
    }

    [Fact]
    public async Task Ohne_Box_ist_das_Bild_ausgewertet_und_ohne_Bogen()
    {
        var ergebnis = await Frage(Antwort(detections: []));

        Assert.Equal(BendFrameOutcome.NoBend, ergebnis.Outcome);
    }

    [Fact]
    public async Task Ein_qualitaetsbedingt_nicht_bewertetes_Bild_ist_kein_kein_Bogen()
    {
        var ergebnis = await Frage(Antwort(
            detections: [], frameUsable: false, qualityReason: "zu dunkel"));

        Assert.Equal(BendFrameOutcome.NotAssessed, ergebnis.Outcome);
        Assert.Equal("zu dunkel", ergebnis.Reason);
    }

    [Fact]
    public async Task Eine_fremde_Klasse_zaehlt_nicht_als_Bogen()
    {
        // Der Sidecar filtert bereits auf ID 14; das hier ist die zweite Grenze.
        var ergebnis = await Frage(Antwort(detections: [Box("BAJ_verbindung", 0.9)]));

        Assert.Equal(BendFrameOutcome.NoBend, ergebnis.Outcome);
    }

    [Fact]
    public async Task Ein_anderer_Kandidat_in_der_Antwort_wird_zum_Fehler()
    {
        // Stillschweigend das Modell zu wechseln waere schlimmer als ein Abbruch:
        // Der Arbeitspunkt gilt nur fuer genau dieses Gewicht.
        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Frage(Antwort(detections: [], candidateId: "bcc_nc15_seed44_20260808")));

        Assert.Contains("Kandidat", fehler.Message);
    }

    [Fact]
    public async Task Ein_abweichender_Gewicht_Hash_wird_zum_Fehler()
    {
        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Frage(Antwort(detections: [], sha: new string('b', 64))));

        Assert.Contains("Gewicht", fehler.Message);
    }

    [Fact]
    public async Task Ein_nicht_verfuegbares_Modell_wird_zum_Fehler()
    {
        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => Frage(Antwort(detections: [], available: false, error: "Kandidat gesperrt")));

        Assert.Contains("Kandidat gesperrt", fehler.Message);
    }

    [Fact]
    public async Task Die_Anfrage_traegt_immer_ID_und_Hash()
    {
        // Ohne beide waehlt der Sidecar selbst — und zwar nach hoechster interner
        // mAP50, also derzeit den Kandidaten mit den meisten Fehlalarmen.
        BccTestYoloRequest? gesehen = null;
        var detektor = new BendFrameDetector(Id, Sha, 0.10, (anfrage, _) =>
        {
            gesehen = anfrage;
            return Task.FromResult(Antwort(detections: []));
        });

        await detektor.DetectAsync([1, 2, 3], CancellationToken.None);

        Assert.NotNull(gesehen);
        Assert.Equal(Id, gesehen!.CandidateId);
        Assert.Equal(Sha, gesehen.CandidateSha256);
        Assert.Equal(0.10, gesehen.ConfidenceThreshold, 3);
        Assert.False(string.IsNullOrWhiteSpace(gesehen.ImageBase64));
    }

    private static Task<BendFrameResult> Frage(BccTestYoloResponse antwort)
        => new BendFrameDetector(Id, Sha, 0.10, (_, _) => Task.FromResult(antwort))
            .DetectAsync([1, 2, 3], CancellationToken.None);

    private static YoloDetectionDto Box(string klasse, double konfidenz)
        => new(0, 0, 10, 10, klasse, konfidenz);

    private static BccTestYoloResponse Antwort(
        IReadOnlyList<YoloDetectionDto> detections,
        bool available = true,
        string? error = null,
        bool frameUsable = true,
        string? qualityReason = null,
        string? candidateId = null,
        string? sha = null)
        => new(
            available, error, IsRelevant: detections.Count > 0, detections,
            FrameClass: "relevant", InferenceTimeMs: 12.0,
            CandidateId: candidateId ?? Id, CandidateSha256: sha ?? Sha,
            ModelName: "bcc", Device: "cuda:0",
            FrameUsable: frameUsable, QualityReason: qualityReason);
}

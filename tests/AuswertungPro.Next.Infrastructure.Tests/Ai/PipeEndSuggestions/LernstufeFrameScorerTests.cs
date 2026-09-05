using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.UseCases.PipeEndSuggestions;
using AuswertungPro.Next.Infrastructure.Ai.PipeEndSuggestions;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.PipeEndSuggestions;

/// <summary>
/// Fragt die gepinnte Lernstufe zu einem Bild. Klasse und Gewicht-Hash gehen
/// mit jeder Anfrage mit und werden an der Antwort erneut geprueft; alles
/// andere als eine Konfidenz zwischen 0 und 1 ist ein technischer Fehler und
/// darf nie als "kein Treffer" erscheinen.
/// </summary>
public sealed class LernstufeFrameScorerTests
{
    private static readonly byte[] Bild = [1, 2, 3, 4, 5];

    [Fact]
    public async Task Die_Anfrage_traegt_Klasse_Hash_Bild_und_die_Bildgroesse_der_Abnahme()
    {
        LernstufeRequest? gesendet = null;
        var scorer = new LernstufeFrameScorer((anfrage, _) =>
        {
            gesendet = anfrage;
            return Task.FromResult(Antwort());
        });

        await scorer.ScoreAsync(Bild, PipeEndLernstufePins.Rohranfang, CancellationToken.None);

        Assert.NotNull(gesendet);
        Assert.Equal("rohranfang", gesendet.Klasse);
        Assert.Equal(PipeEndLernstufePins.Rohranfang.WeightSha256, gesendet.GewichtSha256);
        Assert.Equal(Convert.ToBase64String(Bild), gesendet.ImageBase64);
        // Die Freigabe wurde mit imgsz 640 gemessen (cls_runs/*_640).
        Assert.Equal(640, gesendet.Imgsz);
    }

    [Fact]
    public async Task Die_Konfidenz_der_Antwort_wird_zurueckgegeben()
    {
        var scorer = new LernstufeFrameScorer((_, _) => Task.FromResult(Antwort(konfidenz: 0.87)));

        var wert = await scorer.ScoreAsync(Bild, PipeEndLernstufePins.Rohranfang, CancellationToken.None);

        Assert.Equal(0.87, wert, 3);
    }

    [Fact]
    public async Task Das_Rohrende_wird_mit_seinem_eigenen_Pin_gefragt()
    {
        LernstufeRequest? gesendet = null;
        var scorer = new LernstufeFrameScorer((anfrage, _) =>
        {
            gesendet = anfrage;
            return Task.FromResult(Antwort(
                klasse: "rohrende", sha: PipeEndLernstufePins.Rohrende.WeightSha256));
        });

        await scorer.ScoreAsync(Bild, PipeEndLernstufePins.Rohrende, CancellationToken.None);

        Assert.NotNull(gesendet);
        Assert.Equal("rohrende", gesendet.Klasse);
        Assert.Equal(PipeEndLernstufePins.Rohrende.WeightSha256, gesendet.GewichtSha256);
    }

    [Fact]
    public async Task Eine_andere_Klasse_in_der_Antwort_wird_zum_Fehler()
    {
        // Beide Lernstufen teilen sich einen Modellplatz. Antwortet die falsche,
        // ist ein Abbruch besser als eine stille Verwechslung.
        var scorer = new LernstufeFrameScorer((_, _) => Task.FromResult(Antwort(klasse: "rohrende")));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scorer.ScoreAsync(Bild, PipeEndLernstufePins.Rohranfang, CancellationToken.None));

        Assert.Contains("Klasse", fehler.Message);
    }

    [Fact]
    public async Task Ein_abweichender_Gewicht_Hash_wird_zum_Fehler()
    {
        var scorer = new LernstufeFrameScorer((_, _) => Task.FromResult(Antwort(sha: new string('b', 64))));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scorer.ScoreAsync(Bild, PipeEndLernstufePins.Rohranfang, CancellationToken.None));

        Assert.Contains("Gewicht", fehler.Message);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    public async Task Eine_Konfidenz_ausserhalb_von_0_bis_1_wird_zum_Fehler(double wert)
    {
        var scorer = new LernstufeFrameScorer((_, _) => Task.FromResult(Antwort(konfidenz: wert)));

        var fehler = await Assert.ThrowsAsync<InvalidOperationException>(
            () => scorer.ScoreAsync(Bild, PipeEndLernstufePins.Rohranfang, CancellationToken.None));

        Assert.Contains("Konfidenz", fehler.Message);
    }

    [Fact]
    public async Task Keine_Antwort_wird_zum_Fehler()
    {
        var scorer = new LernstufeFrameScorer((_, _) => Task.FromResult<LernstufeResponse>(null!));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => scorer.ScoreAsync(Bild, PipeEndLernstufePins.Rohranfang, CancellationToken.None));
    }

    private static LernstufeResponse Antwort(
        string klasse = "rohranfang",
        string? sha = null,
        double konfidenz = 0.5)
        => new(
            Klasse: klasse,
            Konfidenz: konfidenz,
            GewichtSha256: sha ?? PipeEndLernstufePins.Rohranfang.WeightSha256,
            FreigabeSha256: new string('c', 64),
            Precision: 0.85,
            Recall: 0.98,
            Device: "cuda:0",
            InferenceTimeMs: 12.0);
}

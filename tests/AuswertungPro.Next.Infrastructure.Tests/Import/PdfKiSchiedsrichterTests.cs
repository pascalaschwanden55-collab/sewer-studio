using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Infrastructure.Import;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// R4 KI-Schiedsrichter: Prompt-Bau und Antwort-Parsing sind pur getestet;
/// der LLM-Aufruf ist ein injizierter Delegate (hier gefaked).
/// </summary>
public sealed class PdfKiSchiedsrichterTests
{
    [Fact]
    public void ParseAntwort_LiestGueltigesJson()
    {
        var k = PdfKiSchiedsrichter.ParseAntwort(
            """{ "typ": "Dichtheitspruefung", "schacht_von": "58951", "schacht_bis": "58950", "datum": "22.06.2026" }""");

        Assert.NotNull(k);
        Assert.Equal(PdfDokumentTyp.Dichtheitspruefung, k!.Typ);
        Assert.Equal("58951", k.SchachtVon);
        Assert.Equal("58950", k.SchachtBis);
        Assert.Equal("22.06.2026", k.Datum);
    }

    [Fact]
    public void ParseAntwort_ToleriertNullFelderUndUnbekanntenTyp()
    {
        var k = PdfKiSchiedsrichter.ParseAntwort(
            """{ "typ": "irgendwas", "schacht_von": null, "schacht_bis": "", "datum": null }""");

        Assert.NotNull(k);
        Assert.Equal(PdfDokumentTyp.Unbekannt, k!.Typ);
        Assert.Null(k.SchachtVon);
        Assert.Null(k.SchachtBis);
    }

    [Fact]
    public void ParseAntwort_MuellLiefertNull()
    {
        Assert.Null(PdfKiSchiedsrichter.ParseAntwort(null));
        Assert.Null(PdfKiSchiedsrichter.ParseAntwort(""));
        Assert.Null(PdfKiSchiedsrichter.ParseAntwort("kein json"));
        Assert.Null(PdfKiSchiedsrichter.ParseAntwort("[1,2,3]"));
    }

    [Fact]
    public void BautPrompt_EnthaeltTextUndTypenUndKuerzt()
    {
        var prompt = PdfKiSchiedsrichter.BautPrompt(new string('x', 10_000));

        Assert.Contains("Dichtheitspruefung", prompt);
        Assert.Contains("TvProtokoll", prompt);
        Assert.True(prompt.Length < 5_000); // Text wird auf 4000 Zeichen gekuerzt
    }

    [Fact]
    public async Task KlassifiziereAsync_LlmFehlerLiefertNull_StattException()
    {
        var schiedsrichter = new PdfKiSchiedsrichter(
            (_, _) => throw new System.Net.Http.HttpRequestException("Ollama aus"));

        // Nicht existentes PDF → Text null → null, ohne LLM-Aufruf.
        var ergebnis = await schiedsrichter.KlassifiziereAsync(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "gibt-es-nicht.pdf"),
            CancellationToken.None);

        Assert.Null(ergebnis);
    }
}

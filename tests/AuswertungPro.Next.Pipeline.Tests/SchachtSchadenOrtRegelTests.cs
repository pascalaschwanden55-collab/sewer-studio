using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 5): Ordnet einen Schadenseintrag des Schachts einer Zeichenzone
/// zu. WPF-frei.
/// </summary>
public sealed class SchachtSchadenOrtRegelTests
{
    [Theory]
    [InlineData("Konus", SchachtZone.Konus)]
    [InlineData("Schachthals", SchachtZone.Konus)]
    [InlineData("Bankett", SchachtZone.Sohle)]
    [InlineData("Durchlaufrinne", SchachtZone.Sohle)]
    [InlineData("Tauchbogen", SchachtZone.Sohle)]
    [InlineData("Anschluss", SchachtZone.Anschluss)]
    [InlineData("Schachtrohr", SchachtZone.Schachtwand)]
    [InlineData("Leiter/Steigeisen", SchachtZone.Schachtwand)]
    public void Der_Bauteilname_aus_dem_PDF_Import_bestimmt_die_Zone(string bauteil, SchachtZone erwartet)
    {
        var eintrag = new ProtocolEntry { Code = bauteil, Beschreibung = "gerissen" };
        Assert.Equal(erwartet, SchachtSchadenOrtRegel.Bestimme(eintrag));
    }

    [Theory]
    [InlineData("Schacht")]
    [InlineData("Schachtdeckel")]
    [InlineData("Deckelrahmen")]
    [InlineData("")]
    [InlineData(null)]
    public void Ein_unbekannter_oder_leerer_Ort_faellt_auf_die_Schachtwand(string? bauteil)
    {
        var eintrag = new ProtocolEntry { Code = bauteil ?? "", Beschreibung = "" };
        Assert.Equal(SchachtZone.Schachtwand, SchachtSchadenOrtRegel.Bestimme(eintrag));
    }

    /// <summary>
    /// Ein einzelnes VSA-Kuerzel (A/B/D/F/H/I/J) aus dem VSA-KEK-Import hat in dieser Codebasis
    /// keinen belegten Klartext. Es wird deshalb wie jeder unbekannte Wert behandelt statt
    /// geraten zu werden.
    /// </summary>
    [Fact]
    public void Ein_rohes_VSA_Kuerzel_wird_nicht_gedeutet()
    {
        var eintrag = new ProtocolEntry
        {
            Code = "BAB",
            Beschreibung = "Riss",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = { ["Schachtbereich"] = "A" }
            }
        };

        Assert.Equal(SchachtZone.Schachtwand, SchachtSchadenOrtRegel.Bestimme(eintrag));
    }

    /// <summary>Ein strukturierter Ort in den Parametern sticht den Code.</summary>
    [Fact]
    public void Ein_strukturierter_Ort_in_den_Parametern_hat_Vorrang_vor_dem_Code()
    {
        var eintrag = new ProtocolEntry
        {
            Code = "BAB",
            Beschreibung = "Riss",
            CodeMeta = new ProtocolEntryCodeMeta
            {
                Parameters = { ["Ort"] = "Sohle" }
            }
        };

        Assert.Equal(SchachtZone.Sohle, SchachtSchadenOrtRegel.Bestimme(eintrag));
    }

    /// <summary>Ohne Code-Treffer greift die Beschreibung als letzter Versuch.</summary>
    [Fact]
    public void Ohne_Code_Treffer_greift_die_Beschreibung()
    {
        var eintrag = new ProtocolEntry { Code = "", Beschreibung = "Anschluss mangelhaft eingebunden" };
        Assert.Equal(SchachtZone.Anschluss, SchachtSchadenOrtRegel.Bestimme(eintrag));
    }
}

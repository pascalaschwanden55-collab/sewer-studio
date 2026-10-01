using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Der Zusammenbau aus Modell und Zeichnung fuer die Oberflaeche. WPF-frei; die Seite reicht
/// Datensatz, Haltungen, Katalog und optional Lage/Koten herein.
/// </summary>
public sealed class SchachtgrafikAnsichtBuilderStammkarteTests
{
    [Fact]
    public void Ohne_Schacht_gibt_es_keine_Grafik()
    {
        Assert.Null(SchachtgrafikAnsichtBuilder.Baue(null, haltungen: null, catalog: null));
        Assert.Null(SchachtgrafikAnsichtBuilder.Baue(null, null, null, SchachtgrafikBeispiel.Zusatz80409()));
    }

    [Fact]
    public void Mit_Schacht_entsteht_eine_Ansicht_mit_Legende()
    {
        var ansicht = SchachtgrafikAnsichtBuilder.Baue(SchachtgrafikBeispiel.Schacht80409(), SchachtgrafikBeispiel.Haltungen80409(), null);

        Assert.NotNull(ansicht);
        Assert.Equal(SchachtgrafikSvgBuilder.Width, ansicht!.Breite);
        Assert.True(ansicht.Hoehe > SchachtgrafikSvgBuilder.GrundrissHoehe);
        Assert.StartsWith("<svg", ansicht.Svg, StringComparison.Ordinal);
        Assert.NotEmpty(ansicht.Legende);
        Assert.NotEmpty(ansicht.Marken);
    }

    [Fact]
    public void Der_Zusatz_bringt_Nordpfeil_und_Koten_in_die_Ansicht()
    {
        var ohne = SchachtgrafikAnsichtBuilder.Baue(SchachtgrafikBeispiel.Schacht80409(), SchachtgrafikBeispiel.Haltungen80409(), null)!;
        var mit = SchachtgrafikAnsichtBuilder.Baue(SchachtgrafikBeispiel.Schacht80409(), SchachtgrafikBeispiel.Haltungen80409(), null, SchachtgrafikBeispiel.Zusatz80409())!;

        Assert.DoesNotContain(">N</text>", ohne.Svg, StringComparison.Ordinal);
        Assert.Contains(">N</text>", mit.Svg, StringComparison.Ordinal);
        Assert.DoesNotContain(ohne.Legende, l => l.Marke == "m");
        Assert.Contains(mit.Legende, l => l.Marke == "m");
    }

    [Fact]
    public void Das_Zeichnen_veraendert_das_Protokoll_und_den_Datensatz_nicht()
    {
        var record = SchachtgrafikBeispiel.Schacht80409();
        var felder = new Dictionary<string, string>(record.Fields);
        var geaendert = record.ModifiedAtUtc;
        var eintraege = record.Protocol!.Current.Entries.Select(e => (e.Code, e.Beschreibung, e.IsDeleted)).ToList();

        SchachtgrafikAnsichtBuilder.Baue(record, SchachtgrafikBeispiel.Haltungen80409(), null, SchachtgrafikBeispiel.Zusatz80409());

        Assert.Equal(felder, record.Fields);
        Assert.Equal(geaendert, record.ModifiedAtUtc);
        Assert.Equal(eintraege, record.Protocol.Current.Entries.Select(e => (e.Code, e.Beschreibung, e.IsDeleted)).ToList());
        Assert.Equal(4, record.Anschluesse!.Count);
    }

    [Fact]
    public void Ein_ungueltiger_Tiefetext_ergibt_den_Hinweis_statt_einer_erfundenen_Zahl()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);
        record.SetFieldValue("Schachttiefe", "ca. tief", FieldSource.Manual, false);

        var ansicht = SchachtgrafikAnsichtBuilder.Baue(record, null, null)!;

        Assert.Contains("Tiefe nicht erfasst", ansicht.Svg, StringComparison.Ordinal);
        Assert.DoesNotContain("m ab Deckel-OK", ansicht.Svg, StringComparison.Ordinal);
    }
}

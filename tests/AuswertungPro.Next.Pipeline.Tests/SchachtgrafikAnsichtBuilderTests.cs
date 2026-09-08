using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 5, Review-Nacharbeit): Loest einen <see cref="SchachtRecord"/>
/// samt Haltungen zur fertigen <see cref="SchachtgrafikAnsicht"/> auf. WPF-frei.
/// </summary>
public sealed class SchachtgrafikAnsichtBuilderTests
{
    [Fact]
    public void Ohne_Schacht_gibt_es_keine_Grafik()
    {
        Assert.Null(SchachtgrafikAnsichtBuilder.Baue(null, haltungen: null, catalog: null));
    }

    [Fact]
    public void Mit_Schacht_entsteht_eine_Ansicht_in_der_erwarteten_Groesse()
    {
        var ansicht = SchachtgrafikAnsichtBuilder.Baue(Schacht(), haltungen: null, catalog: null);

        Assert.NotNull(ansicht);
        Assert.Equal(SchachtgrafikSvgBuilder.Width, ansicht!.Breite);
        Assert.Equal(SchachtgrafikSvgBuilder.Height, ansicht.Hoehe);
        Assert.StartsWith("<svg", ansicht.Svg, StringComparison.Ordinal);
    }

    /// <summary>
    /// Rein lesend: Das Anzeigen darf weder das Protokoll noch den Datensatz veraendern —
    /// analog <c>HaltungsgrafikAnsichtBuilderTests.Das_Zeichnen_veraendert_das_Protokoll_nicht</c>.
    /// </summary>
    [Fact]
    public void Das_Zeichnen_veraendert_das_Protokoll_und_den_Datensatz_nicht()
    {
        var record = Schacht();
        var eintrag = record.Protocol!.Current.Entries[0];
        eintrag.FotoPaths.Add(@"C:\irgendwo\foto.jpg");
        eintrag.CodeMeta = new ProtocolEntryCodeMeta { Parameters = { ["Ort"] = "Konus" } };

        var vorherAnzahl = record.Protocol.Current.Entries.Count;
        var vorherFotos = eintrag.FotoPaths.ToList();
        var vorherParameterAnzahl = eintrag.CodeMeta.Parameters.Count;
        var vorherTiefe = record.GetFieldValue("Schachttiefe");

        Assert.NotNull(SchachtgrafikAnsichtBuilder.Baue(record, haltungen: null, catalog: null));

        Assert.Equal(vorherAnzahl, record.Protocol.Current.Entries.Count);
        Assert.Equal(vorherFotos, eintrag.FotoPaths);
        Assert.Equal(vorherParameterAnzahl, eintrag.CodeMeta.Parameters.Count);
        Assert.Equal(vorherTiefe, record.GetFieldValue("Schachttiefe"));
    }

    /// <summary>
    /// Zulauf ist eine Haltung mit <c>Schacht_unten</c> = Schachtnummer, Ablauf eine mit
    /// <c>Schacht_oben</c> = Schachtnummer. Eine Haltung an einem anderen Schacht bleibt aussen vor.
    /// </summary>
    [Fact]
    public void Zulauf_und_Ablauf_werden_aus_echten_Haltungen_ueber_Schacht_oben_unten_zugeordnet()
    {
        var record = Schacht();
        var zulauf = Haltung("10000-10001", schachtOben: "10000", schachtUnten: "10001", dn: "200");
        var ablauf = Haltung("10001-10002", schachtOben: "10001", schachtUnten: "10002", dn: "250");
        var fremd = Haltung("20001-20002", schachtOben: "20001", schachtUnten: "20002", dn: "300");

        var ansicht = SchachtgrafikAnsichtBuilder.Baue(record, [zulauf, ablauf, fremd], catalog: null);

        // Lange Namen werden in der Grafik gekuerzt (siehe SchachtgrafikSvgBuilder); der volle
        // Name bleibt im Hinweistext der Marke erhalten — dort wird geprueft.
        Assert.NotNull(ansicht);
        Assert.Contains(ansicht!.Marken, m => m.Tooltip == "10000-10001 DN200");
        Assert.Contains(ansicht.Marken, m => m.Tooltip == "10001-10002 DN250");
        Assert.DoesNotContain(ansicht.Marken, m => m.Tooltip.Contains("20001-20002", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("2.40 m")]
    [InlineData("2.40")]
    [InlineData("2,4")]
    public void Die_Tiefe_wird_mit_und_ohne_Einheit_gelesen(string tiefeText)
    {
        var record = Schacht();
        record.SetFieldValue("Schachttiefe", tiefeText, FieldSource.Manual, false);

        var ansicht = SchachtgrafikAnsichtBuilder.Baue(record, haltungen: null, catalog: null);

        Assert.NotNull(ansicht);
        Assert.Contains("2.40 m", ansicht!.Svg);
        Assert.DoesNotContain("Tiefe nicht erfasst", ansicht.Svg);
    }

    [Fact]
    public void Ein_ungueltiger_Tiefetext_ergibt_den_Hinweis_statt_einer_erfundenen_Zahl()
    {
        var record = Schacht();
        record.SetFieldValue("Schachttiefe", "unbekannt", FieldSource.Manual, false);

        var ansicht = SchachtgrafikAnsichtBuilder.Baue(record, haltungen: null, catalog: null);

        Assert.NotNull(ansicht);
        Assert.Contains("Tiefe nicht erfasst", ansicht!.Svg);
    }

    private static SchachtRecord Schacht()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "10001", FieldSource.Manual, false);
        record.SetFieldValue("Schachttiefe", "2.40", FieldSource.Manual, false);
        record.SetFieldValue(FieldKeys.ShaftDimension1Mm, "1100", FieldSource.Manual, false);
        record.SetFieldValue(FieldKeys.ShaftDimension2Mm, "900", FieldSource.Manual, false);
        record.Protocol = new ProtocolDocument
        {
            Current = new ProtocolRevision
            {
                Entries = { new ProtocolEntry { Code = "Konus", Beschreibung = "gerissen" } }
            }
        };
        return record;
    }

    private static HaltungRecord Haltung(string name, string schachtOben, string schachtUnten, string dn)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Manual, false);
        record.SetFieldValue("Schacht_oben", schachtOben, FieldSource.Manual, false);
        record.SetFieldValue("Schacht_unten", schachtUnten, FieldSource.Manual, false);
        record.SetFieldValue(FieldKeys.NominalDiameterMm, dn, FieldSource.Manual, false);
        return record;
    }
}

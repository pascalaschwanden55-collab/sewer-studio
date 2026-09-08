using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Nova, Aufklapp-Liste (Task 4): Die Uebersicht zeigt dieselbe Haltungsgrafik wie das PDF.
/// Diese Regel ist WPF-frei; die Oberflaeche zeichnet danach nur noch.
/// </summary>
public sealed class HaltungsgrafikAnsichtBuilderTests
{
    [Fact]
    public void Ohne_Datensatz_gibt_es_keine_Grafik()
    {
        Assert.Null(HaltungsgrafikAnsichtBuilder.Baue(null, catalog: null, hoehe: 700));
    }

    /// <summary>
    /// Ohne belastbare Laenge gibt es keinen Massstab. Eine geratene Laenge waere eine erfundene
    /// Angabe — deshalb lieber keine Grafik.
    /// </summary>
    [Fact]
    public void Ohne_Laenge_gibt_es_keine_Grafik()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "10001-10002", FieldSource.Manual, false);

        Assert.Null(HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700));
    }

    [Fact]
    public void Mit_Laenge_entsteht_ein_SVG_in_der_gewuenschten_Groesse()
    {
        var ansicht = HaltungsgrafikAnsichtBuilder.Baue(Haltung(), catalog: null, hoehe: 640);

        Assert.NotNull(ansicht);
        Assert.Equal(HaltungsgrafikSvgBuilder.Width, ansicht!.Breite);
        Assert.Equal(640, ansicht.Hoehe);
        Assert.StartsWith("<svg", ansicht.Svg, StringComparison.Ordinal);
        Assert.Contains("height='640'", ansicht.Svg, StringComparison.Ordinal);
    }

    /// <summary>Je Beobachtung mit Meterangabe genau eine Hinweisflaeche — nicht mehr, nicht weniger.</summary>
    [Fact]
    public void Jede_Beobachtung_mit_Meter_bekommt_eine_Hinweisflaeche()
    {
        var ansicht = HaltungsgrafikAnsichtBuilder.Baue(Haltung(), catalog: null, hoehe: 700);

        Assert.NotNull(ansicht);
        Assert.Equal(3, ansicht!.Marken.Count);
        Assert.All(ansicht.Marken, marke => Assert.False(string.IsNullOrWhiteSpace(marke.Tooltip)));
        Assert.Contains(ansicht.Marken, marke => marke.Tooltip.StartsWith("BAB", StringComparison.Ordinal));
        Assert.Contains(ansicht.Marken, marke => marke.Tooltip.Contains("12.50", StringComparison.Ordinal));
    }

    /// <summary>
    /// Die Schachtknoten kommen aus den erfassten Feldern; nur ein leeres Feld faellt auf die
    /// Zerlegung des Haltungsnamens zurueck (gleiche Regel wie im PDF-Weg).
    /// </summary>
    [Fact]
    public void Schachtknoten_kommen_aus_den_Feldern_und_sonst_aus_dem_Namen()
    {
        var record = Haltung();
        var ausName = HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700);
        Assert.NotNull(ausName);
        Assert.Contains(">10001<", ausName!.Svg, StringComparison.Ordinal);
        Assert.Contains(">10002<", ausName.Svg, StringComparison.Ordinal);

        record.SetFieldValue("Schacht_oben", "S-A", FieldSource.Manual, true);
        record.SetFieldValue("Schacht_unten", "S-B", FieldSource.Manual, true);
        var ausFeldern = HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700);
        Assert.NotNull(ausFeldern);
        Assert.Contains(">S-A<", ausFeldern!.Svg, StringComparison.Ordinal);
        Assert.Contains(">S-B<", ausFeldern.Svg, StringComparison.Ordinal);
    }

    /// <summary>Die Vorgabe der Fliessrichtung sticht das Feld; ohne Vorgabe gilt das Feld.</summary>
    [Fact]
    public void Fliessrichtung_kommt_aus_dem_Feld_oder_aus_der_Vorgabe()
    {
        var record = Haltung();
        record.SetFieldValue("Inspektionsrichtung", "In Fliessrichtung", FieldSource.Manual, true);

        var ausFeld = HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700);
        Assert.NotNull(ausFeld);
        Assert.Contains("↓ Fliessrichtung", ausFeld!.Svg, StringComparison.Ordinal);

        var mitVorgabe = HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700, flowDownVorgabe: false);
        Assert.NotNull(mitVorgabe);
        Assert.Contains("↑ Fliessrichtung", mitVorgabe!.Svg, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Uebersicht ist rein lesend. Der Exportweg repariert beim Aufloesen Fotopfade und
    /// Codemetadaten im Datensatz und spiegelt bei einem Abbruch die Gegenfahrt-Meter — beides
    /// darf beim blossen Anzeigen nicht passieren.
    /// </summary>
    [Fact]
    public void Das_Zeichnen_veraendert_das_Protokoll_nicht()
    {
        var record = Haltung();
        record.Protocol!.Current.Entries.Add(Eintrag("BDC", 20.0));
        record.Protocol.Current.Entries.Add(Eintrag("BAC", 2.0));
        var vorher = record.Protocol.Current.Entries
            .Select(e => (e.Code, e.MeterStart, e.MeterEnd))
            .ToList();

        Assert.NotNull(HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700));

        var nachher = record.Protocol.Current.Entries
            .Select(e => (e.Code, e.MeterStart, e.MeterEnd))
            .ToList();
        Assert.Equal(vorher, nachher);
    }

    /// <summary>Ein geloeschter Eintrag gehoert nicht in die Grafik.</summary>
    [Fact]
    public void Geloeschte_Eintraege_werden_nicht_gezeichnet()
    {
        var record = Haltung();
        record.Protocol!.Current.Entries[0].IsDeleted = true;

        var ansicht = HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700);

        Assert.NotNull(ansicht);
        Assert.Equal(2, ansicht!.Marken.Count);
        Assert.DoesNotContain(ansicht.Marken, marke => marke.Tooltip.StartsWith("BAB", StringComparison.Ordinal));
    }

    private static HaltungRecord Haltung()
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "10001-10002", FieldSource.Manual, false);
        record.SetFieldValue("Haltungslaenge_m", "42.5", FieldSource.Manual, false);
        record.Protocol = new ProtocolDocument
        {
            HaltungId = "10001-10002",
            Current = new ProtocolRevision
            {
                Entries =
                {
                    Eintrag("BAB", 3.2),
                    Eintrag("BBC", 12.5),
                    Eintrag("BCA", 30.0)
                }
            }
        };
        return record;
    }

    private static ProtocolEntry Eintrag(string code, double meter)
        => new()
        {
            Code = code,
            Beschreibung = code,
            MeterStart = meter,
            CodeMeta = new ProtocolEntryCodeMeta { Code = code }
        };
}

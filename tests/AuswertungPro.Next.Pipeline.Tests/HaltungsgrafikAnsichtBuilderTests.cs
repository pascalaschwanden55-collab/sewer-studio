using System.Xml.Linq;
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

    /// <summary>
    /// Fix-Runde 1 (Review B.2): Ein passendes <see cref="VsaFinding"/> wuerde ueber
    /// <c>ProtocolPdfEntryResolver.ResolveEntriesForExport</c> Fotopfade und Codemetadaten
    /// NACHTRAEGLICH in den bestehenden Eintrag schreiben (Reparatur fuer den Export) und koennte
    /// sogar einen zusaetzlichen Eintrag anlegen. Die vorherige Pruefung (nur Code/Meter) haette
    /// genau das nicht bemerkt, weil die Reparatur diese drei Felder unveraendert laesst.
    /// </summary>
    [Fact]
    public void Ein_passendes_VsaFinding_repariert_den_Eintrag_nicht()
    {
        var record = Haltung();
        var eintrag = record.Protocol!.Current.Entries[0]; // BAB @ 3.2, ohne Foto/Parameter
        var vorherFotos = eintrag.FotoPaths.ToList();
        var vorherParameterAnzahl = eintrag.CodeMeta!.Parameters.Count;
        var vorherAnzahl = record.Protocol.Current.Entries.Count;

        record.VsaFindings.Add(new VsaFinding
        {
            KanalSchadencode = eintrag.Code,
            MeterStart = eintrag.MeterStart,
            FotoPath = @"C:\irgendwo\foto.jpg",
            Quantifizierung1 = "50"
        });

        Assert.NotNull(HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700));

        Assert.Equal(vorherAnzahl, record.Protocol.Current.Entries.Count);
        Assert.Equal(vorherFotos, eintrag.FotoPaths);
        Assert.Equal(vorherParameterAnzahl, eintrag.CodeMeta.Parameters.Count);
    }

    /// <summary>
    /// Fix-Runde 1: Ohne Vorgabe gilt dieselbe gestaffelte Standardhoehe wie im PDF-Weg
    /// (<see cref="HaltungsgrafikExportSizing.ChooseSvgHeight"/>) statt einer festen 700 —
    /// eine Haltung mit vielen Eintraegen bekommt mehr Platz. Eine ausdrueckliche Vorgabe
    /// (z.B. die auf ihre feste Anzeigehoehe abgestimmte Rohrsaeule der Uebersicht) sticht
    /// weiterhin.
    /// </summary>
    [Fact]
    public void Ohne_Hoehenvorgabe_waechst_die_Standardhoehe_mit_der_Eintragszahl()
    {
        var wenig = Haltung();
        var ansichtWenig = HaltungsgrafikAnsichtBuilder.Baue(wenig, catalog: null);
        Assert.NotNull(ansichtWenig);
        Assert.Equal(700, ansichtWenig!.Hoehe);

        var viele = Haltung();
        viele.Protocol!.Current.Entries.Clear();
        for (var i = 0; i < 30; i++)
            viele.Protocol.Current.Entries.Add(Eintrag("BAB", 1.0 + i));

        var ansichtViele = HaltungsgrafikAnsichtBuilder.Baue(viele, catalog: null);
        Assert.NotNull(ansichtViele);
        Assert.Equal(HaltungsgrafikExportSizing.ChooseSvgHeight(30), ansichtViele!.Hoehe);
        Assert.True(ansichtViele.Hoehe > 700, "Mehr Eintraege sollen mehr Platz auf dem Rohr bekommen.");

        var mitVorgabe = HaltungsgrafikAnsichtBuilder.Baue(viele, catalog: null, hoehe: 310);
        Assert.NotNull(mitVorgabe);
        Assert.Equal(310, mitVorgabe!.Hoehe);
    }

    /// <summary>
    /// Fix-Runde 1: Die volle Grafik ist A4-proportioniert und wird in der schmalen Uebersicht
    /// unlesbar klein. <c>nurRohr</c> laesst die Beschriftungstabelle weg und liefert die schmale
    /// Rohrsaeule — Rohr, Skala, Symbole, Schachtknoten und Fliesspfeil bleiben.
    /// </summary>
    [Fact]
    public void Nur_Rohr_laesst_die_Beschriftungstabelle_weg()
    {
        var record = Haltung();
        record.SetFieldValue("Inspektionsrichtung", "In Fliessrichtung", FieldSource.Manual, true);

        var saeule = HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 340, nurRohr: true);
        Assert.NotNull(saeule);
        Assert.Equal(HaltungsgrafikSvgBuilder.RohrBreite, saeule!.Breite);

        // Weg: Spaltenkopf, Spalten-Zuschnitte und die Label-Zeilen.
        foreach (var weg in new[] { "OP Kürzel", "Zustand", "MPEG", "clipPath", "clip-path" })
            Assert.DoesNotContain(weg, saeule.Svg, StringComparison.Ordinal);

        // Da: Rohr, Skala, Schachtknoten, Fliesspfeil auf dem Rohr, Symbole.
        foreach (var da in new[] { "url(#pipeGrad)", ">10001<", ">10002<", "url(#flowGrad)", "<circle" })
            Assert.Contains(da, saeule.Svg, StringComparison.Ordinal);

        // Weg: die Wellen und die gedrehte Beschriftung am linken Rand — sie lagen genau auf
        // den Meterzahlen (Sichtprobe im Pruefhost).
        Assert.DoesNotContain("Fliessrichtung", saeule.Svg, StringComparison.Ordinal);

        // Die Saeule steht als Ganzes gerueckt, damit die Meterbeschriftung nicht abgeschnitten wird.
        Assert.Contains($"<g transform='translate({HaltungsgrafikSvgBuilder.RohrVersatz},0)'>", saeule.Svg, StringComparison.Ordinal);
        Assert.EndsWith("</g></svg>", saeule.Svg, StringComparison.Ordinal);

        // Die Hinweisflaechen wandern mit.
        Assert.All(saeule.Marken, marke => Assert.True(marke.X > 0 && marke.X + marke.Breite < HaltungsgrafikSvgBuilder.RohrBreite));
    }

    /// <summary>Der PDF-Weg bleibt der Standardfall und behaelt seine Tabelle.</summary>
    [Fact]
    public void Der_volle_Weg_bleibt_unveraendert()
    {
        var voll = HaltungsgrafikAnsichtBuilder.Baue(Haltung(), catalog: null, hoehe: 700);

        Assert.NotNull(voll);
        Assert.Equal(HaltungsgrafikSvgBuilder.Width, voll!.Breite);
        Assert.Contains("OP Kürzel", voll.Svg, StringComparison.Ordinal);
        Assert.Contains("clipPath", voll.Svg, StringComparison.Ordinal);
        Assert.DoesNotContain("<g transform=", voll.Svg, StringComparison.Ordinal);
    }

    /// <summary>
    /// Fix-Runde 1 (Review B.1): Ein Steuerzeichen im Befundtext (etwa aus fehlerhaftem
    /// OCR-Import) liess den WPF-XmlReader beim blossen Anzeigen mit einer <c>XmlException</c>
    /// abstuerzen, weil <c>EscapeSvgText</c> es nicht bereinigte. Das erzeugte SVG ist jetzt
    /// immer gueltiges XML.
    /// </summary>
    [Fact]
    public void Ein_Steuerzeichen_im_Befundtext_bleibt_gueltiges_Xml()
    {
        var record = Haltung();
        record.Protocol!.Current.Entries[0].Beschreibung = "Riss \u0002 quer";

        var ansicht = HaltungsgrafikAnsichtBuilder.Baue(record, catalog: null, hoehe: 700);

        Assert.NotNull(ansicht);
        Assert.DoesNotContain('\u0002', ansicht!.Svg);
        // Wirft eine XmlException, wenn ein Steuerzeichen uebrig geblieben waere.
        XDocument.Parse(ansicht.Svg);
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

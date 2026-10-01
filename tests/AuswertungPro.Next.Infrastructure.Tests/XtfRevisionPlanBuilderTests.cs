using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Plan vergleicht die Originaldatei mit dem aktuellen Projektstand. Er schreibt nichts
/// und veraendert nichts — er sagt nur, was geschehen soll.
/// </summary>
public sealed class XtfRevisionPlanBuilderTests
{
    [Fact]
    public void Ohne_Aenderung_bleibt_alles_unveraendert()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out var id));
        var plan = Baue(record, Element("A1", "BAB", 5.00));

        Assert.Equal(1, plan.AnzahlUnveraendert);
        Assert.True(plan.OhneAenderung);
        Assert.False(plan.BrauchtEntscheidung);
        _ = id;
    }

    // Der wichtigste Fall: Eine Codekorrektur darf nicht als "geloescht plus neu" erscheinen.
    [Fact]
    public void Eine_Codekorrektur_wird_als_Aenderung_erkannt()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _));
        Arbeitsstand(record)[0].Code = "BAC";

        var plan = Baue(record, Element("A1", "BAB", 5.00));

        var position = Assert.Single(plan.Positionen);
        Assert.Equal(XtfRevisionAenderung.Geaendert, position.Art);
        Assert.Equal("A1", position.KanalschadenTid);
        var feld = Assert.Single(position.Felder);
        Assert.Equal("KanalSchadencode", feld.Name);
        Assert.Equal("BAB", feld.Alt);
        Assert.Equal("BAC", feld.Neu);
    }

    [Fact]
    public void Ein_geaenderter_Meterwert_erscheint_mit_Alt_und_Neuwert()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _));
        Arbeitsstand(record)[0].MeterStart = 7.25;

        var plan = Baue(record, Element("A1", "BAB", 5.00));

        var feld = Assert.Single(Assert.Single(plan.Positionen).Felder);
        Assert.Equal("Distanz", feld.Name);
        Assert.Equal("5.00", feld.Alt);
        Assert.Equal("7.25", feld.Neu);
    }

    [Fact]
    public void Ein_von_Hand_ergaenzter_Befund_kommt_neu_dazu()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _));
        Arbeitsstand(record).Add(new ProtocolEntry
        {
            Code = "BBC",
            MeterStart = 12.50,
            Source = ProtocolEntrySource.Manual
        });

        var plan = Baue(record, Element("A1", "BAB", 5.00));

        var neu = Assert.Single(plan.Positionen, p => p.Art == XtfRevisionAenderung.Neu);
        Assert.Null(neu.KanalschadenTid);
        Assert.Equal("BBC", neu.Code);
        Assert.Contains(neu.Felder, f => f.Name == "Distanz" && f.Neu == "12.50");
        Assert.Equal(1, plan.AnzahlNeu);
    }

    [Fact]
    public void Ein_geloeschter_Befund_wird_entfernt()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _));
        Arbeitsstand(record)[0].IsDeleted = true;

        var plan = Baue(record, Element("A1", "BAB", 5.00));

        var position = Assert.Single(plan.Positionen);
        Assert.Equal(XtfRevisionAenderung.Entfernt, position.Art);
        Assert.Equal("A1", position.KanalschadenTid);
        Assert.Equal(1, plan.AnzahlEntfernt);
    }

    [Fact]
    public void Eine_geaenderte_Quantifizierung_erscheint_im_Plan()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _));
        Arbeitsstand(record)[0].CodeMeta = new ProtocolEntryCodeMeta
        {
            Code = "BAB",
            Parameters = { ["Quantifizierung1"] = "25" }
        };

        var plan = Baue(record, Element("A1", "BAB", 5.00, q1: "10"));

        var feld = Assert.Single(Assert.Single(plan.Positionen).Felder);
        Assert.Equal("Quantifizierung1", feld.Name);
        Assert.Equal("10", feld.Alt);
        Assert.Equal("25", feld.Neu);
    }

    [Fact]
    public void Eine_leere_Quantifizierung_loescht_den_Originalwert_nicht()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _));
        Arbeitsstand(record)[0].CodeMeta = new ProtocolEntryCodeMeta
        {
            Code = "BAB",
            Parameters = { ["Quantifizierung1"] = "" }
        };

        var plan = Baue(record, Element("A1", "BAB", 5.00, q1: "10"));

        Assert.True(plan.OhneAenderung);
    }

    // Ein Element, das nicht sicher zugeordnet werden kann, darf nie verschwinden.
    [Fact]
    public void Ein_nicht_zuordenbares_Element_bleibt_unveraendert_stehen()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _));

        var plan = Baue(
            record,
            Element("A1", "BAB", 5.00),
            Element("A2", "BBC", 99.00));

        Assert.Equal(2, plan.AnzahlUnveraendert);
        Assert.Equal(0, plan.AnzahlEntfernt);
        Assert.True(plan.OhneAenderung);
    }

    [Fact]
    public void Mehrdeutiges_wird_gemeldet_statt_geraten()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _), Eintrag("BAB", 5.00, out _));

        var plan = Baue(record, Element("A1", "BAB", 5.00), Element("A2", "BAB", 5.00));

        Assert.True(plan.BrauchtEntscheidung);
        Assert.Equal(2, plan.Warnungen.Count);
        Assert.Equal(0, plan.AnzahlEntfernt);
        Assert.Equal(0, plan.AnzahlGeaendert);
    }

    [Fact]
    public void Ohne_Protokoll_bleibt_die_Haltung_unangetastet()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "06-001", FieldSource.Xtf, userEdited: false);

        var plan = Baue(record, Element("A1", "BAB", 5.00));

        Assert.Equal(1, plan.AnzahlUnveraendert);
        Assert.True(plan.OhneAenderung);
    }

    // Ein Projekt enthaelt XTF aus mehreren Gebieten. Eine Haltung, die in DIESER Datei
    // nicht vorkommt, darf keine einzige Position erzeugen — sonst meldet der Bericht
    // Aenderungen an einer Datei, zu der die Haltung gar nicht gehoert.
    [Fact]
    public void Eine_Haltung_aus_einem_fremden_Gebiet_erzeugt_keine_Position()
    {
        var fremd = Haltung("99-999", Eintrag("BAB", 5.00, out _));
        fremd.XtfHerkunft = null;
        Arbeitsstand(fremd).Add(new ProtocolEntry
        {
            Code = "BBC",
            MeterStart = 12.50,
            Source = ProtocolEntrySource.Manual
        });

        var plan = XtfRevisionPlanBuilder.Build(
            new[] { fremd },
            new[] { Element("A1", "BAB", 5.00) },   // gehoert zu 06-001, nicht zu 99-999
            "test.xtf");

        Assert.Empty(plan.Positionen);
        Assert.Equal(0, plan.AnzahlNeu);
        Assert.True(plan.OhneAenderung);
        Assert.False(plan.BrauchtEntscheidung);
    }

    [Fact]
    public void Nur_die_Haltungen_dieser_Datei_kommen_in_den_Plan()
    {
        var eigen = Haltung("06-001", Eintrag("BAB", 5.00, out _));
        var fremd = Haltung("99-999", Eintrag("BAF", 1.00, out _));
        fremd.XtfHerkunft = null;

        var plan = XtfRevisionPlanBuilder.Build(
            new[] { eigen, fremd },
            new[] { Element("A1", "BAB", 5.00) },
            "test.xtf");

        var position = Assert.Single(plan.Positionen);
        Assert.Equal("06-001", position.HaltungName);
    }

    [Fact]
    public void Der_Plan_veraendert_das_Projekt_nicht()
    {
        var record = Haltung("06-001", Eintrag("BAB", 5.00, out _));
        Arbeitsstand(record)[0].Code = "BAC";
        var vorher = record.ModifiedAtUtc;
        var ausgangCode = record.Protocol!.Original!.Entries[0].Code;

        Baue(record, Element("A1", "BAB", 5.00));

        Assert.Equal(vorher, record.ModifiedAtUtc);
        Assert.Equal(ausgangCode, record.Protocol.Original.Entries[0].Code);
        Assert.Equal("BAC", record.Protocol.Current!.Entries[0].Code);
    }

    [Fact]
    public void Die_Quelldatei_steht_im_Plan()
    {
        var plan = XtfRevisionPlanBuilder.Build(
            Array.Empty<HaltungRecord>(),
            Array.Empty<XtfKanalschadenElement>(),
            "Buerglen_1225.xtf");

        Assert.Equal("Buerglen_1225.xtf", plan.Quelldatei);
        Assert.True(plan.OhneAenderung);
    }

    // ── Hilfen ──────────────────────────────────────────────────────────

    private static XtfRevisionPlan Baue(HaltungRecord record, params XtfKanalschadenElement[] elemente)
        => XtfRevisionPlanBuilder.Build(new[] { record }, elemente, "test.xtf");

    private static List<ProtocolEntry> Arbeitsstand(HaltungRecord record)
        => record.Protocol!.Current!.Entries;

    private static ProtocolEntry Eintrag(string code, double meter, out Guid id)
    {
        id = Guid.NewGuid();
        return new ProtocolEntry
        {
            EntryId = id,
            Code = code,
            MeterStart = meter,
            Source = ProtocolEntrySource.Imported
        };
    }

    private static HaltungRecord Haltung(string name, params ProtocolEntry[] eintraege)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Xtf, userEdited: false);
        record.XtfHerkunft = new XtfHerkunft { Datei = "test.xtf", Modell = "VSA_KEK_2020_LV95", UntersuchungTid = "U1" };
        record.Protocol = new ProtocolDocument
        {
            HaltungId = name,
            Original = new ProtocolRevision { Entries = eintraege.Select(ProtocolEntryCloner.CloneLegacyProtocolEntry).ToList() },
            Current = new ProtocolRevision { Entries = eintraege.Select(ProtocolEntryCloner.CloneLegacyProtocolEntry).ToList() }
        };
        return record;
    }

    private static XtfKanalschadenElement Element(
        string tid,
        string code,
        double? distanz,
        string? video = null,
        string? q1 = null)
        => new(tid, "U1", "06-001", code, distanz, video, q1);
}

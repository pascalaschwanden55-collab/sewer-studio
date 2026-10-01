using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Der Neu-Export mit GEONIS-Kennungen: Traegt ein Bauteil die Kennungen des
/// Katasters, schreibt die Datei genau diese TIDs — sonst legte der Import in GEONIS
/// Duplikate an.
/// </summary>
public sealed class XtfNeuPlanBuilderGeonisTests
{
    [Fact]
    public void Eine_Haltung_mit_GEONIS_Kennungen_traegt_diese_als_TIDs()
    {
        var record = Seilergasse();
        record.Geonis = SeilergasseKennungen();

        var plan = XtfNeuPlanBuilder.Build([record], [SchachtMitKennung()]);

        var haltung = Assert.Single(plan.Objekte, o => o.Klasse == "Haltung");
        Assert.Equal("ch23h1a4uL3A2Sjp", haltung.Tid);
        var kanal = Assert.Single(plan.Objekte, o => o.Klasse == "Kanal");
        Assert.Equal("ch23h1a46oVbkGmT", kanal.Tid);
        Assert.Equal(kanal.Tid, haltung.Verweise.Single(v => v.Name == "AbwasserbauwerkRef").ZielTid);

        var punkte = plan.Objekte.Where(o => o.Klasse == "Haltungspunkt").ToList();
        Assert.Contains(punkte, p => p.Tid == "ch23h1a4CNjzeqBU" && p.Felder.Any(f => f.Value == "A75394"));
        Assert.Contains(punkte, p => p.Tid == "ch23h1a44Op5RVY5" && p.Felder.Any(f => f.Value == "E75394"));
        Assert.Equal("ch23h1a4CNjzeqBU", haltung.Verweise.Single(v => v.Name == "vonHaltungspunktRef").ZielTid);
        Assert.Equal("ch23h1a44Op5RVY5", haltung.Verweise.Single(v => v.Name == "nachHaltungspunktRef").ZielTid);

        Assert.Contains(plan.Hinweise, h => h.Contains("GEONIS-Kennung ch23h1a4uL3A2Sjp", StringComparison.Ordinal));
        Assert.DoesNotContain(plan.Hinweise, h => h.Contains("Objekt-ID", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_Schacht_mit_GEONIS_Kennungen_traegt_Bauwerk_und_Knoten_des_Katasters()
    {
        var plan = XtfNeuPlanBuilder.Build([], [SchachtMitKennung()]);

        var bauwerk = Assert.Single(plan.Objekte, o => o.Klasse == "Normschacht");
        Assert.Equal("ch23h1a4Umcgr2UF", bauwerk.Tid);
        var knoten = Assert.Single(plan.Objekte, o => o.Klasse == "Abwasserknoten");
        Assert.Equal("ch23h1a4ftlGdbHU", knoten.Tid);
        Assert.Equal(bauwerk.Tid, knoten.Verweise.Single(v => v.Name == "AbwasserbauwerkRef").ZielTid);
    }

    // Das GEONIS-Rohrprofil der Seilergasse ist "unbekannt"; das Projekt sagt Kreisprofil.
    // Ein geteiltes Profil darf dann nicht unter der Katasterkennung umgeschrieben werden.
    [Fact]
    public void Ein_abweichendes_Rohrprofil_bekommt_eine_eigene_Kennung_und_einen_Hinweis()
    {
        var record = Seilergasse();
        record.Geonis = SeilergasseKennungen();

        var plan = XtfNeuPlanBuilder.Build([record], []);

        var profil = Assert.Single(plan.Objekte, o => o.Klasse == "Rohrprofil");
        Assert.StartsWith("chSST", profil.Tid, StringComparison.Ordinal);
        Assert.Contains(plan.Hinweise, h =>
            h.Contains("Rohrprofil weicht vom Kataster ab", StringComparison.Ordinal)
            && h.Contains("Kataster: unbekannt", StringComparison.Ordinal)
            && h.Contains("Projekt: Kreisprofil", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_gleiches_Rohrprofil_traegt_die_Katasterkennung()
    {
        var record = Seilergasse();
        record.Geonis = SeilergasseKennungen();
        record.Geonis.RohrprofilTyp = "Kreisprofil";

        var plan = XtfNeuPlanBuilder.Build([record], []);

        var profil = Assert.Single(plan.Objekte, o => o.Klasse == "Rohrprofil");
        Assert.Equal("ch23h1a43obhLa8B", profil.Tid);
        Assert.Equal("Kreisprofil", profil.Felder.Single(f => f.Key == "Profiltyp").Value);
        Assert.Equal("1", profil.Felder.Single(f => f.Key == "HoehenBreitenverhaeltnis").Value);
        var haltung = Assert.Single(plan.Objekte, o => o.Klasse == "Haltung");
        Assert.Equal(profil.Tid, haltung.Verweise.Single(v => v.Name == "RohrprofilRef").ZielTid);
    }

    // Eine Kennung, die nicht die Form einer STANDARDOID hat, darf nie in die Datei —
    // der ilivalidator wiese sie ab, GEONIS erkennte sie nicht.
    [Fact]
    public void Eine_ungueltige_Kennung_wird_ignoriert()
    {
        var record = Seilergasse();
        record.Geonis = new GeonisKennungen { Haltung = "866789" };

        var plan = XtfNeuPlanBuilder.Build([record], []);

        var haltung = Assert.Single(plan.Objekte, o => o.Klasse == "Haltung");
        Assert.StartsWith("chSST", haltung.Tid, StringComparison.Ordinal);
    }

    [Fact]
    public void Ohne_Bauwerkskennung_bekommt_nur_der_Knoten_die_Katasterkennung()
    {
        var schacht = SchachtMitKennung();
        schacht.Geonis!.Bauwerk = null;

        var plan = XtfNeuPlanBuilder.Build([], [schacht]);

        Assert.StartsWith("chSST", Assert.Single(plan.Objekte, o => o.Klasse == "Normschacht").Tid, StringComparison.Ordinal);
        Assert.Equal("ch23h1a4ftlGdbHU", Assert.Single(plan.Objekte, o => o.Klasse == "Abwasserknoten").Tid);
        Assert.Contains(plan.Hinweise, h => h.Contains("hat dort keine Kennung", StringComparison.Ordinal));
    }

    private static HaltungRecord Seilergasse()
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, "78998-79002", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.ProfileType, "Kreisprofil", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.UsageType, "Schmutzabwasser", FieldSource.Manual, true);
        record.SetFieldValue("Schacht_oben", "78998", FieldSource.Manual, true);
        record.SetFieldValue("Schacht_unten", "79002", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.CadastreObjectId, "866789", FieldSource.Kataster, false);
        return record;
    }

    private static GeonisKennungen SeilergasseKennungen() => new()
    {
        Haltung = "ch23h1a4uL3A2Sjp",
        Kanal = "ch23h1a46oVbkGmT",
        VonPunkt = "ch23h1a4CNjzeqBU",
        VonPunktBezeichnung = "A75394",
        NachPunkt = "ch23h1a44Op5RVY5",
        NachPunktBezeichnung = "E75394",
        Rohrprofil = "ch23h1a43obhLa8B",
        RohrprofilTyp = "unbekannt",
        Quelle = "GEONIS-Kopie 2024-12"
    };

    private static SchachtRecord SchachtMitKennung()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "78998", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Manual, true);
        record.Geonis = new GeonisKennungen { Knoten = "ch23h1a4ftlGdbHU", Bauwerk = "ch23h1a4Umcgr2UF" };
        return record;
    }
}

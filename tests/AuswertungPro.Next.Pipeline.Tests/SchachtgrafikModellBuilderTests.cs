using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Schachtgrafik Stammkarte: das Modell aus Datensatz, Haltungen, Lage und Koten. Alle Zahlen
/// stammen vom echten Schacht 80409 (siehe <see cref="SchachtgrafikBeispiel"/>).
/// </summary>
public sealed class SchachtgrafikModellBuilderTests
{
    [Fact]
    public void Anschluesse_werden_mit_Haltung_Richtung_und_Kote_verbunden()
    {
        var modell = SchachtgrafikBeispiel.Modell80409();

        Assert.Equal(4, modell.Anschluesse.Count);

        var a1 = modell.Anschluesse.Single(a => a.Nr == 1);
        Assert.True(a1.IstAuslauf);
        Assert.Equal("A1", a1.Kennung);
        Assert.Equal("80409-80538", a1.Haltungsname);
        Assert.Equal(250, a1.DnMm);
        Assert.Equal(3.45m, a1.TiefeM);
        Assert.Equal("Protokoll", a1.TiefeQuelle);
        Assert.Equal(274.6, a1.AzimutGrad!.Value, 1);
        Assert.Equal(495.140m, a1.KoteM);
        Assert.True(a1.ImProjekt);

        var e2 = modell.Anschluesse.Single(a => a.Nr == 2);
        Assert.Equal("80547-80409", e2.Haltungsname);
        Assert.Equal(28.4, e2.AzimutGrad!.Value, 1);
        Assert.Equal(495.160m, e2.KoteM);

        var e3 = modell.Anschluesse.Single(a => a.Nr == 3);
        Assert.Equal("80467-80409", e3.Haltungsname);
        Assert.Equal(100, e3.DnMm);
        Assert.Equal(0.60m, e3.TiefeM);
        Assert.Null(e3.KoteM);

        var e4 = modell.Anschluesse.Single(a => a.Nr == 4);
        Assert.Null(e4.Haltungsname);
        Assert.False(e4.ImProjekt);
        Assert.Null(e4.AzimutGrad);
        Assert.Contains(modell.Hinweise, h => h.Contains("E4", StringComparison.Ordinal) && h.Contains("nicht im Projekt", StringComparison.Ordinal));
        Assert.Contains("Richtung nicht erfasst: E4", modell.Hinweise);

        Assert.Same(a1, modell.Hauptauslauf);
        Assert.True(modell.HatRichtungen);
        Assert.Equal("Mischabwasser", modell.Nutzungsart);
        Assert.True(modell.SteigeisenVorhanden);
        Assert.Equal(660, modell.DeckelDurchmesserMm);
    }

    [Fact]
    public void Schaeden_werden_von_oben_nach_unten_nummeriert_und_die_Bemerkung_haengt_am_Anschluss()
    {
        var modell = SchachtgrafikBeispiel.Modell80409();

        Assert.Equal(5, modell.Schaeden.Count);
        Assert.Equal(
            new[] { SchachtBauteil.Konus, SchachtBauteil.Anschluss, SchachtBauteil.Bankett, SchachtBauteil.Bankett, SchachtBauteil.Durchlaufrinne },
            modell.Schaeden.Select(s => s.Bauteil).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, modell.Schaeden.Select(s => s.Nr).ToArray());

        var bemerkung = modell.Schaeden.Single(s => s.Bauteil == SchachtBauteil.Anschluss);
        Assert.Equal(3, bemerkung.AnschlussNr);
        Assert.Equal("break", bemerkung.Kategorie);
        Assert.Contains("Bemerkung", bemerkung.Tooltip, StringComparison.Ordinal);

        Assert.Equal("incrustation", modell.Schaeden[0].Kategorie);
        Assert.Equal("deposit", modell.Schaeden[3].Kategorie);
    }

    [Fact]
    public void Eine_Bemerkung_zu_einem_Anschluss_den_es_nicht_gibt_erzeugt_keinen_Schaden()
    {
        var schacht = SchachtgrafikBeispiel.Schacht80409();
        schacht.SetFieldValue("Bemerkungen", "Einlauf 7 Ausgebrochen", FieldSource.Manual, true);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, SchachtgrafikBeispiel.Haltungen80409(), null, null, "#006E9C");

        Assert.Equal(4, modell.Schaeden.Count);
        Assert.DoesNotContain(modell.Schaeden, s => s.Bauteil == SchachtBauteil.Anschluss);
    }

    [Fact]
    public void Ohne_Tabelle_werden_die_Haltungen_zu_Anschluessen_mit_dem_Auslauf_zuerst()
    {
        var modell = SchachtgrafikBeispiel.Modell80409(mitTabelle: false);

        Assert.Equal(3, modell.Anschluesse.Count);
        Assert.Equal(new[] { "A1", "E2", "E3" }, modell.Anschluesse.Select(a => a.Kennung).ToArray());
        Assert.Equal("80409-80538", modell.Anschluesse[0].Haltungsname);
        Assert.Equal("80467-80409", modell.Anschluesse[1].Haltungsname);
        Assert.Equal("80547-80409", modell.Anschluesse[2].Haltungsname);

        // Ohne Tabellentiefe ergibt Deckelkote minus Rohrsohlenkote die Tiefe — als Katasterwert.
        Assert.Equal(3.480m, modell.Anschluesse[0].TiefeM);
        Assert.Equal("Kataster", modell.Anschluesse[0].TiefeQuelle);
        Assert.Null(modell.Anschluesse[1].TiefeM);
        Assert.True(modell.Anschluesse.All(a => a.ImProjekt));
    }

    [Fact]
    public void Die_Tiefe_kommt_aus_dem_Kataster_wenn_das_Feld_leer_ist()
    {
        var modell = SchachtgrafikBeispiel.Modell80409(mitTiefe: false);

        Assert.Equal(3.470m, modell.TiefeM);
        Assert.Equal("Kataster", modell.TiefeQuelle);
        Assert.DoesNotContain(modell.Hinweise, h => h.StartsWith("Tiefe nicht erfasst", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_Widerspruch_zwischen_Protokoll_und_Kataster_steht_in_den_Hinweisen()
    {
        var zusatz = new SchachtgrafikZusatz(
            null,
            new SchachtKoten(498.700m, 495.150m, new Dictionary<string, decimal>()));

        var modell = SchachtgrafikModellBuilder.Baue(SchachtgrafikBeispiel.Schacht80409(), null, null, zusatz, "#006E9C");

        Assert.Equal(3.45m, modell.TiefeM);
        Assert.Equal("Protokoll", modell.TiefeQuelle);
        Assert.Contains(modell.Hinweise, h => h.StartsWith("Kataster: Tiefe 3.55 m", StringComparison.Ordinal));
    }

    [Fact]
    public void Ohne_Tiefe_Masse_und_Zusatz_bleiben_die_Hinweise_ehrlich()
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, null, null, null, "#006E9C");

        Assert.Null(modell.TiefeM);
        Assert.Null(modell.TiefeQuelle);
        Assert.False(modell.HatMasse);
        Assert.Null(modell.SteigeisenVorhanden);
        Assert.Empty(modell.Anschluesse);
        Assert.Empty(modell.Schaeden);
        Assert.Contains(modell.Hinweise, h => h.StartsWith("Tiefe nicht erfasst", StringComparison.Ordinal));
        Assert.Contains(modell.Hinweise, h => h.StartsWith("Innenmasse nicht erfasst", StringComparison.Ordinal));
        Assert.DoesNotContain(modell.Hinweise, h => h.Contains("Richtung", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("vorhanden", true)]
    [InlineData("Steigeisen", true)]
    [InlineData("Leiter", true)]
    [InlineData("fehlt", false)]
    [InlineData("nicht notwendig", false)]
    [InlineData("", null)]
    public void Die_Steighilfe_wird_gelesen(string wert, bool? erwartet)
    {
        var schacht = new SchachtRecord();
        schacht.SetFieldValue("Schachtnummer", "S1", FieldSource.Manual, false);
        if (wert.Length > 0)
            schacht.SetFieldValue("Steighilfe", wert, FieldSource.Manual, false);

        var modell = SchachtgrafikModellBuilder.Baue(schacht, null, null, null, "#006E9C");

        Assert.Equal(erwartet, modell.SteigeisenVorhanden);
    }

    [Fact]
    public void Die_Haltungen_werden_ueber_die_Schachtfelder_zugeordnet_wenn_sie_gefuellt_sind()
    {
        var haltungen = SchachtgrafikBeispiel.Haltungen80409();
        // Gegenbefahrung: Der Name sagt 80547-80409, die Felder sagen, 80409 sei OBEN.
        haltungen[0].SetFieldValue("Schacht_oben", "80409", FieldSource.Manual, true);
        haltungen[0].SetFieldValue("Schacht_unten", "80547", FieldSource.Manual, true);

        var modell = SchachtgrafikModellBuilder.Baue(SchachtgrafikBeispiel.Schacht80409(mitTabelle: false), haltungen, null, null, "#006E9C");

        var gegen = modell.Anschluesse.Single(a => a.Haltungsname == "80547-80409");
        Assert.True(gegen.IstAuslauf);
    }

    [Fact]
    public void Der_Modellbauer_veraendert_den_Datensatz_nicht()
    {
        var schacht = SchachtgrafikBeispiel.Schacht80409();
        var felderVorher = new Dictionary<string, string>(schacht.Fields);
        var eintraegeVorher = schacht.Protocol!.Current.Entries.Count;
        var anschluesseVorher = schacht.Anschluesse!.Count;
        var geaendert = schacht.ModifiedAtUtc;

        SchachtgrafikBeispiel.Modell80409();
        SchachtgrafikModellBuilder.Baue(schacht, SchachtgrafikBeispiel.Haltungen80409(), null, SchachtgrafikBeispiel.Zusatz80409(), "#006E9C");

        Assert.Equal(felderVorher, schacht.Fields);
        Assert.Equal(eintraegeVorher, schacht.Protocol.Current.Entries.Count);
        Assert.Equal(anschluesseVorher, schacht.Anschluesse!.Count);
        Assert.Equal(geaendert, schacht.ModifiedAtUtc);
        Assert.All(schacht.Protocol.Current.Entries, e => Assert.Null(e.CodeMeta));
    }

    [Fact]
    public void Ein_geloeschter_Eintrag_wird_nicht_gezeichnet()
    {
        var schacht = SchachtgrafikBeispiel.Schacht80409();
        schacht.Protocol!.Current.Entries[0].IsDeleted = true;

        var modell = SchachtgrafikModellBuilder.Baue(schacht, null, null, null, "#006E9C");

        Assert.DoesNotContain(modell.Schaeden, s => s.Bauteil == SchachtBauteil.Konus);
    }
}

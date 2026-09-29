using System.Text.Json;
using AuswertungPro.Next.Application.DataPage;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Optik Aufgabe 16, Fix-Runde 1: Objektakten nur ganz entfernen/wieder anfuegen, Zellen-Schritt nur
/// ueber das eigene Feld, halb angewendete Schritte, strengere Vorbedingungen.
/// </summary>
public sealed class DatenaenderungsVerlaufFixRundeTests
{
    private const string Material = "Rohrmaterial";
    private static readonly DatenaenderungsBereich H = DatenaenderungsBereich.Haltungen;

    private static (DatenaenderungsVerlauf Verlauf, Project Projekt, HaltungRecord Haltung) Aufbau()
    {
        var projekt = new Project();
        var haltung = new HaltungRecord();
        haltung.Fields[FieldKeys.HoldingName] = "10001-10002";
        projekt.Data.Add(haltung);
        var verlauf = new DatenaenderungsVerlauf();
        verlauf.Binde(projekt);
        return (verlauf, projekt, haltung);
    }

    private static void Eingabe(IDatenaenderungsVerlauf verlauf, HaltungRecord h, string feld, string wert)
    {
        using var _ = verlauf.Erfasse(h, feld);
        h.SetFieldValue(feld, wert, FieldSource.Manual, userEdited: true);
    }

    /// <summary>Erster Schreibvorgang in der Objektakte legt die Wurzelakte an.</summary>
    private static (ObjektaktenBearbeitung B, ObjektAkte Akte) ErsteAktenEingabe(DatenaenderungsVerlauf verlauf, Project p, HaltungRecord h)
    {
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung") { Verlauf = verlauf };
        var gruppe = FieldCatalog.Objektfelder.Feld("haltung.pipegroup");
        var beton = b.ErlaubteEintraege(b.Wurzel, gruppe).Single(e => e.Label == "Beton");
        b.Schreibe(b.Wurzel, gruppe, "", beton.Label, beton);
        return (b, p.Objektakten.Single());
    }

    [Fact]
    public void Eine_inzwischen_ergaenzte_Akte_wird_beim_Rueckgaengig_nicht_entfernt()
    {
        var (verlauf, p, h) = Aufbau();
        var (_, akte) = ErsteAktenEingabe(verlauf, p, h);
        // Ein nicht erfasster Weg (GeoShop, Zusatzdatei, WebGIS) ergaenzt die Akte danach.
        akte.Quellen.Add(new ObjektQuellbeleg { System = "GeoShop", Kennung = "chTEST00H0000001" });

        var ergebnis = verlauf.Rueckgaengig(H);

        Assert.False(ergebnis.Angewendet);
        Assert.Contains("weitere Angaben", ergebnis.Meldung);
        Assert.Same(akte, Assert.Single(p.Objektakten));
        Assert.Single(akte.Quellen);
        Assert.Equal("Beton", akte.Werte["haltung.pipegroup"].Text);
    }

    [Theory]
    [InlineData("Hauptdeckel")]
    [InlineData("Unterliste")]
    [InlineData("Fremdwert")]
    public void Jede_fremde_Aktenangabe_sperrt_das_Entfernen(string was)
    {
        var (verlauf, p, h) = Aufbau();
        var (_, akte) = ErsteAktenEingabe(verlauf, p, h);
        switch (was)
        {
            case "Hauptdeckel": akte.HauptdeckelId = Guid.NewGuid(); break;
            case "Unterliste": akte.Unterlisten["x"] = [new Dictionary<string, string> { ["a"] = "b" }]; break;
            default: akte.Werte["haltung.betreiber"] = new ObjektFeldWert { Text = "Gemeinde" }; break;
        }

        Assert.False(verlauf.Rueckgaengig(H).Angewendet);
        Assert.Same(akte, Assert.Single(p.Objektakten));
    }

    [Fact]
    public void Wiederholen_fuegt_keine_zweite_Akte_mit_derselben_Kennung_an()
    {
        var (verlauf, p, h) = Aufbau();
        var (_, akte) = ErsteAktenEingabe(verlauf, p, h);
        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.Empty(p.Objektakten);
        // Inzwischen legt ein anderer Weg eine Akte mit derselben Kennung an.
        var neu = new ObjektAkte { Id = akte.Id, Art = "haltung" };
        p.Objektakten.Add(neu);

        var ergebnis = verlauf.Wiederholen(H);

        Assert.False(ergebnis.Angewendet);
        Assert.Contains("neuen Eintrag", ergebnis.Meldung);
        Assert.Same(neu, Assert.Single(p.Objektakten));
    }

    [Fact]
    public void Aktenwerte_mit_verschiedenen_Zusatzdaten_sind_nicht_gleich()
    {
        JsonElement E(string json) => JsonDocument.Parse(json).RootElement.Clone();
        var a = new ObjektFeldWert { Text = "X", Zusatzdaten = new() { ["k"] = E("1") } };
        var b = new ObjektFeldWert { Text = "X", Zusatzdaten = new() { ["k"] = E("2") } };
        var c = new ObjektFeldWert { Text = "X", Zusatzdaten = new() { ["k"] = E("1") } };

        Assert.False(AktenWertKopie.Gleich(a, b));
        Assert.True(AktenWertKopie.Gleich(a, c));
    }

    [Fact]
    public void Ein_Zellenschritt_umfasst_nur_das_bearbeitete_Feld()
    {
        var (verlauf, _, h) = Aufbau();
        using (verlauf.Erfasse(h, Material, DatenaenderungsVerlauf.ZellSchrittFelder(Material)))
        {
            h.SetFieldValue(Material, "PVC", FieldSource.Manual, true);
            // Ein anderer Weg schreibt waehrend der offenen Zelle ein anderes Feld desselben Datensatzes.
            h.SetFieldValue(FieldKeys.Remarks, "vom Nachschlagen", FieldSource.Grundbuch, true);
        }

        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.Equal("", h.GetFieldValue(Material));
        Assert.Equal("vom Nachschlagen", h.GetFieldValue(FieldKeys.Remarks));
    }

    [Fact]
    public void Sanieren_nimmt_die_abgeleiteten_Kostenfelder_mit()
    {
        var felder = DatenaenderungsVerlauf.ZellSchrittFelder(FieldKeys.RenovationDecision);
        Assert.Contains(FieldKeys.RenovationDecision, felder);
        Assert.All(SanierungCostFieldMapper.CostFieldNames, f => Assert.Contains(f, felder));
        Assert.Equal([Material], DatenaenderungsVerlauf.ZellSchrittFelder(Material));

        var (verlauf, _, h) = Aufbau();
        h.Fields[FieldKeys.RenovationDecision] = "Ja";
        h.Fields[FieldKeys.Cost] = "1200.00";
        using (verlauf.Erfasse(h, FieldKeys.RenovationDecision, felder))
        {
            h.SetFieldValue(FieldKeys.RenovationDecision, "Nein", FieldSource.Manual, true);
            SanierungCostFieldMapper.SyncRecord(h, cost: null);
        }
        Assert.Equal("", h.GetFieldValue(FieldKeys.Cost));

        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.Equal("Ja", h.GetFieldValue(FieldKeys.RenovationDecision));
        Assert.Equal("1200.00", h.GetFieldValue(FieldKeys.Cost));
    }

    [Fact]
    public void Ein_halb_angewendeter_Schritt_wird_zurueckgesetzt_und_ist_nicht_wiederholbar()
    {
        var (verlauf, _, h) = Aufbau();
        using (verlauf.Erfasse(h))
        {
            h.SetFieldValue("DN_mm", "300", FieldSource.Manual, true);
            h.SetFieldValue("Lichte_Breite_mm", "300", FieldSource.Manual, true);
        }
        var einmal = true;
        h.PropertyChanged += (_, e) =>
        {
            if (einmal && e.PropertyName == "Fields[Lichte_Breite_mm]")
            {
                einmal = false;
                throw new InvalidOperationException("Anzeige gestört");
            }
        };

        var ergebnis = verlauf.Rueckgaengig(H);

        Assert.False(ergebnis.Angewendet);
        Assert.Contains("Es wurde nichts geändert", ergebnis.Meldung);
        Assert.Equal("300", h.GetFieldValue("DN_mm"));
        Assert.Equal("300", h.GetFieldValue("Lichte_Breite_mm"));
        Assert.False(verlauf.KannRueckgaengig(H));
        Assert.False(verlauf.KannWiederholen(H));
    }

    [Fact]
    public void Scheitert_auch_das_Zuruecksetzen_wird_der_Verlauf_geleert()
    {
        var (verlauf, _, h) = Aufbau();
        Eingabe(verlauf, h, "DN_mm", "250");
        using (verlauf.Erfasse(h))
        {
            h.SetFieldValue("DN_mm", "300", FieldSource.Manual, true);
            h.SetFieldValue("Lichte_Breite_mm", "300", FieldSource.Manual, true);
        }
        string? grund = null;
        verlauf.Geleert += (_, e) => grund = e.Grund;
        h.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Fields[Lichte_Breite_mm]")
                throw new InvalidOperationException("Anzeige gestört");
        };

        var ergebnis = verlauf.Rueckgaengig(H);

        Assert.False(ergebnis.Angewendet);
        Assert.Contains("nicht vollständig", ergebnis.Meldung);
        Assert.Equal(DatenaenderungsVerlauf.GrundFehler, grund);
        Assert.False(verlauf.KannRueckgaengig(H));
    }

    [Fact]
    public void Gleicher_Wert_mit_anderer_Herkunft_sperrt_Rueckgaengig()
    {
        var (verlauf, _, h) = Aufbau();
        Eingabe(verlauf, h, Material, "PVC");
        h.FieldMeta[Material].Source = FieldSource.Kataster; // derselbe Text, jetzt als Katasterwert

        var ergebnis = verlauf.Rueckgaengig(H);

        Assert.False(ergebnis.Angewendet);
        Assert.Equal("PVC", h.GetFieldValue(Material));
    }

    [Fact]
    public void Wiederholen_prueft_den_Stand_nach_dem_Rueckgaengig()
    {
        var (verlauf, _, h) = Aufbau();
        Eingabe(verlauf, h, Material, "PVC");
        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        h.SetFieldValue(Material, "Steinzeug", FieldSource.Grundbuch, userEdited: true); // nicht erfasst

        var ergebnis = verlauf.Wiederholen(H);

        Assert.False(ergebnis.Angewendet);
        Assert.Equal("Steinzeug", h.GetFieldValue(Material));
        Assert.False(verlauf.KannWiederholen(H));
    }

    [Fact]
    public void Echtes_Loeschen_einer_Haltung_leert_den_Verlauf()
    {
        var (verlauf, p, h) = Aufbau();
        Eingabe(verlauf, h, Material, "PVC");
        string? grund = null;
        verlauf.Geleert += (_, e) => grund = e.Grund;

        p.Data.Remove(h);

        Assert.Equal(DatenaenderungsVerlauf.GrundListe, grund);
        Assert.False(verlauf.KannRueckgaengig(H));
        Assert.Equal("PVC", h.GetFieldValue(Material));
    }

    [Fact]
    public void EingabeOffen_zeigt_einen_offenen_Bereich()
    {
        var (verlauf, _, h) = Aufbau();
        Assert.False(verlauf.EingabeOffen);
        using (verlauf.Erfasse(h, Material))
            Assert.True(verlauf.EingabeOffen);
        Assert.False(verlauf.EingabeOffen);
    }
}

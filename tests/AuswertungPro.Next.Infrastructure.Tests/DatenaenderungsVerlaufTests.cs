using System.Collections.ObjectModel;
using AuswertungPro.Next.Application.UseCases.Datenaenderungen;
using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Optik Aufgabe 16: Regeln des Rueckgaengig-Verlaufs fuer Haltungs- und Schachtdaten — echte
/// Datensaetze, echte Schreibwege (<c>SetFieldValue</c>, Objektakte), keine Attrappen.
/// </summary>
public sealed class DatenaenderungsVerlaufTests
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

    /// <summary>Eine Benutzereingabe wie in Tabelle/Formular: Bereich oeffnen, von Hand schreiben.</summary>
    private static void Eingabe(IDatenaenderungsVerlauf verlauf, HaltungRecord h, string feld, string wert)
    {
        using var _ = verlauf.Erfasse(h, feld);
        h.SetFieldValue(feld, wert, FieldSource.Manual, userEdited: true);
    }

    [Fact]
    public void Rueckgaengig_stellt_Wert_und_Herkunftsdaten_exakt_wieder_her_ohne_neue_Handmarke()
    {
        var (verlauf, _, h) = Aufbau();
        var zeit = new DateTime(2026, 9, 1, 8, 30, 0, DateTimeKind.Utc);
        h.Fields[Material] = "Beton";
        h.FieldMeta[Material] = new FieldMetadata { FieldName = Material, Source = FieldSource.Kataster, UserEdited = false, LastUpdatedUtc = zeit };

        Eingabe(verlauf, h, Material, "PVC");
        Assert.True(h.FieldMeta[Material].UserEdited);
        Assert.Equal("Rohrmaterial 10001-10002", verlauf.RueckgaengigBeschreibung(H));

        var ergebnis = verlauf.Rueckgaengig(H);

        Assert.True(ergebnis.Angewendet);
        Assert.Equal("Beton", h.GetFieldValue(Material));
        var meta = h.FieldMeta[Material];
        Assert.Equal(FieldSource.Kataster, meta.Source);
        Assert.False(meta.UserEdited);
        Assert.Equal(zeit, meta.LastUpdatedUtc);
        Assert.Same(h, Assert.Single(ergebnis.Datensaetze));

        // Wiederholen setzt den Stand nach der Eingabe wieder, samt Handmarke.
        Assert.True(verlauf.Wiederholen(H).Angewendet);
        Assert.Equal("PVC", h.GetFieldValue(Material));
        Assert.True(h.FieldMeta[Material].UserEdited);
        Assert.Equal(FieldSource.Manual, h.FieldMeta[Material].Source);
    }

    [Fact]
    public void Ein_Feld_ohne_Metadaten_ist_danach_wieder_ohne_Metadaten()
    {
        var (verlauf, _, h) = Aufbau();
        h.FieldMeta.Remove("Bemerkung_Test");
        Eingabe(verlauf, h, "Bemerkung_Test", "neu");
        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.False(h.Fields.ContainsKey("Bemerkung_Test"));
        Assert.False(h.FieldMeta.ContainsKey("Bemerkung_Test"));
    }

    [Fact]
    public void WebGis_Begriff_wird_zeichengenau_zurueckgesetzt_nicht_umgewandelt()
    {
        var (verlauf, _, h) = Aufbau();
        // Alter Projektwert in Normschreibweise, nie umgewandelt.
        h.Fields[FieldKeys.OperatingStatus] = "tot";
        Eingabe(verlauf, h, FieldKeys.OperatingStatus, "in_Betrieb");
        var gespeichert = h.GetFieldValue(FieldKeys.OperatingStatus);
        Assert.Equal("In Betrieb", gespeichert); // der Schreibweg wandelt in den WebGIS-Begriff um

        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.Equal("tot", h.GetFieldValue(FieldKeys.OperatingStatus));

        Assert.True(verlauf.Wiederholen(H).Angewendet);
        Assert.Equal(gespeichert, h.GetFieldValue(FieldKeys.OperatingStatus));
    }

    [Fact]
    public void Tiefe_ist_hundert_und_eine_neue_Eingabe_leert_Wiederholen()
    {
        var (verlauf, _, h) = Aufbau();
        for (var i = 1; i <= 105; i++)
            Eingabe(verlauf, h, Material, "M" + i);

        var schritte = 0;
        while (verlauf.KannRueckgaengig(H))
        {
            Assert.True(verlauf.Rueckgaengig(H).Angewendet);
            schritte++;
        }
        Assert.Equal(DatenaenderungsVerlauf.Tiefe, schritte);
        Assert.Equal("M5", h.GetFieldValue(Material));

        Assert.True(verlauf.KannWiederholen(H));
        Eingabe(verlauf, h, Material, "Neu");
        Assert.False(verlauf.KannWiederholen(H));
    }

    [Fact]
    public void Innere_Bereiche_und_mehrere_Felder_sind_ein_Schritt()
    {
        var (verlauf, _, h) = Aufbau();
        using (verlauf.Erfasse(h, "DN_mm"))
        {
            h.SetFieldValue("DN_mm", "300", FieldSource.Manual, true);
            using (verlauf.Erfasse(h, "Lichte_Breite_mm"))
                h.SetFieldValue("Lichte_Breite_mm", "300", FieldSource.Manual, true);
        }

        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.Equal("", h.GetFieldValue("DN_mm"));
        Assert.Equal("", h.GetFieldValue("Lichte_Breite_mm"));
        Assert.False(verlauf.KannRueckgaengig(H));
    }

    [Fact]
    public void Spalte_leeren_ueber_viele_Haltungen_ist_ein_Schritt()
    {
        var (verlauf, projekt, h1) = Aufbau();
        var h2 = new HaltungRecord();
        h1.Fields[Material] = "Beton";
        h2.Fields[Material] = "PVC";
        verlauf.Binde(null);
        projekt.Data.Add(h2);
        verlauf.Binde(projekt);

        using (verlauf.ErfasseMehrere(projekt.Data, "Spalte leeren: Material"))
            foreach (var h in projekt.Data)
                h.SetFieldValue(Material, "", FieldSource.Manual, true);

        Assert.Equal("Spalte leeren: Material", verlauf.RueckgaengigBeschreibung(H));
        var ergebnis = verlauf.Rueckgaengig(H);
        Assert.Equal(2, ergebnis.Datensaetze.Count);
        Assert.Equal("Beton", h1.GetFieldValue(Material));
        Assert.Equal("PVC", h2.GetFieldValue(Material));
    }

    [Fact]
    public void Blosses_Nachstempeln_ohne_Wertaenderung_ist_kein_Schritt()
    {
        var (verlauf, _, h) = Aufbau();
        h.Fields[Material] = "Beton";
        Eingabe(verlauf, h, Material, "Beton");
        Assert.False(verlauf.KannRueckgaengig(H));
    }

    [Fact]
    public void Ein_inzwischen_anders_geaendertes_Feld_wird_nicht_ueberschrieben()
    {
        var (verlauf, _, h) = Aufbau();
        Eingabe(verlauf, h, Material, "PVC");
        // Ein nicht erfasster Schreiber (z. B. Nachschlagen) aendert dasselbe Feld danach.
        h.SetFieldValue(Material, "Steinzeug", FieldSource.Grundbuch, userEdited: true);

        var ergebnis = verlauf.Rueckgaengig(H);

        Assert.False(ergebnis.Angewendet);
        Assert.Contains("inzwischen anders geändert", ergebnis.Meldung);
        Assert.Equal("Steinzeug", h.GetFieldValue(Material));
        Assert.False(verlauf.KannRueckgaengig(H));
    }

    [Fact]
    public void Ein_nicht_mehr_vorhandener_Datensatz_wird_uebersprungen()
    {
        var (verlauf, projekt, h) = Aufbau();
        Eingabe(verlauf, h, Material, "PVC");
        // Liste ohne Meldung ausgetauscht (z. B. alter Ladepfad): der Eintrag darf nichts mehr schreiben.
        projekt.Data = new ObservableCollection<HaltungRecord>();

        var ergebnis = verlauf.Rueckgaengig(H);

        Assert.False(ergebnis.Angewendet);
        Assert.Contains("nicht mehr im Projekt", ergebnis.Meldung);
        Assert.Equal("PVC", h.GetFieldValue(Material));
    }

    [Theory]
    [InlineData(FieldKeys.HoldingName)]
    [InlineData("Schacht_oben")]
    [InlineData("Schacht_unten")]
    public void Ein_Umbenennungsfeld_ist_nicht_rueckgaengig_machbar_und_leert_den_Verlauf(string feld)
    {
        var (verlauf, _, h) = Aufbau();
        Eingabe(verlauf, h, Material, "PVC");
        string? grund = null;
        verlauf.Geleert += (_, e) => grund = e.Grund;

        Eingabe(verlauf, h, feld, "20001");

        Assert.False(verlauf.KannRueckgaengig(H));
        Assert.NotNull(grund);
        Assert.Contains("umbenannt", grund);
    }

    [Fact]
    public void Schachtnummer_ist_nicht_rueckgaengig_machbar()
    {
        var projekt = new Project();
        var s = new SchachtRecord();
        s.Fields["Schachtnummer"] = "80409";
        projekt.SchaechteData.Add(s);
        var verlauf = new DatenaenderungsVerlauf();
        verlauf.Binde(projekt);

        using (verlauf.Erfasse(s, "Material"))
            s.SetFieldValue("Material", "Beton", FieldSource.Manual, true);
        Assert.Equal("Material 80409", verlauf.RueckgaengigBeschreibung(DatenaenderungsBereich.Schaechte));

        using (verlauf.Erfasse(s, "Schachtnummer"))
            s.SetFieldValue("Schachtnummer", "80410", FieldSource.Manual, true);

        Assert.False(verlauf.KannRueckgaengig(DatenaenderungsBereich.Schaechte));
    }

    [Fact]
    public void Haltungen_und_Schaechte_haben_getrennte_Verlaeufe()
    {
        var (verlauf, projekt, h) = Aufbau();
        var s = new SchachtRecord();
        verlauf.Binde(null);
        projekt.SchaechteData.Add(s);
        verlauf.Binde(projekt);

        Eingabe(verlauf, h, Material, "PVC");
        using (verlauf.Erfasse(s, "Material"))
            s.SetFieldValue("Material", "Beton", FieldSource.Manual, true);

        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.Equal("Beton", s.GetFieldValue("Material"));
        Assert.True(verlauf.KannRueckgaengig(DatenaenderungsBereich.Schaechte));
    }

    [Fact]
    public void Projektwechsel_und_Listenaenderungen_leeren_den_Verlauf()
    {
        var (verlauf, projekt, h) = Aufbau();
        var gruende = new List<string>();
        verlauf.Geleert += (_, e) => gruende.Add(e.Grund);

        Eingabe(verlauf, h, Material, "PVC");
        projekt.Data.Add(new HaltungRecord());
        Assert.False(verlauf.KannRueckgaengig(H));

        Eingabe(verlauf, h, Material, "Beton");
        projekt.Data.Move(0, 1);
        Assert.False(verlauf.KannRueckgaengig(H));

        Eingabe(verlauf, h, Material, "GFK");
        verlauf.Binde(new Project());
        Assert.False(verlauf.KannRueckgaengig(H));

        Assert.Equal(
            [DatenaenderungsVerlauf.GrundListe, DatenaenderungsVerlauf.GrundListe, DatenaenderungsVerlauf.GrundProjekt],
            gruende);

        // Das alte Projekt ist abgemeldet: seine Listen leeren nichts mehr, Eingaben zaehlen nicht.
        Eingabe(verlauf, h, Material, "Zement");
        Assert.False(verlauf.KannRueckgaengig(H));
    }

    [Fact]
    public void Ein_offener_Bereich_wird_bei_einer_Sperre_verworfen()
    {
        var (verlauf, _, h) = Aufbau();
        using (verlauf.Erfasse(h, Material))
        {
            h.SetFieldValue(Material, "PVC", FieldSource.Manual, true);
            verlauf.Leere(DatenaenderungsVerlauf.GrundUebernahme);
        }
        Assert.False(verlauf.KannRueckgaengig(H));
    }

    [Fact]
    public void Waehrend_einer_offenen_Eingabe_wird_nichts_zurueckgenommen()
    {
        var (verlauf, _, h) = Aufbau();
        Eingabe(verlauf, h, Material, "PVC");
        using (verlauf.Erfasse(h, Material))
        {
            var ergebnis = verlauf.Rueckgaengig(H);
            Assert.False(ergebnis.Angewendet);
            Assert.Equal("PVC", h.GetFieldValue(Material));
        }
        Assert.True(verlauf.KannRueckgaengig(H));
    }

    [Fact]
    public void Objektakte_Gruppenwechsel_samt_Materialdetail_ist_ein_Schritt_und_neue_Akte_verschwindet_wieder()
    {
        var (verlauf, p, h) = Aufbau();
        var b = new ObjektaktenBearbeitung(p, h.Id, "haltung") { Verlauf = verlauf };
        var gruppe = FieldCatalog.Objektfelder.Feld("haltung.pipegroup");
        var material = FieldCatalog.Objektfelder.Feld("haltung.material");

        var beton = b.ErlaubteEintraege(b.Wurzel, gruppe).Single(e => e.Label == "Beton");
        b.Schreibe(b.Wurzel, gruppe, "", beton.Label, beton);
        var armiert = b.ErlaubteEintraege(b.Wurzel, material).Single(e => e.Label == "Beton, armiert (BA)");
        b.Schreibe(b.Wurzel, material, b.Lies(b.Wurzel, material), armiert.Label, armiert);
        var materialNachArmiert = h.GetFieldValue(Material);
        var andere = b.ErlaubteEintraege(b.Wurzel, gruppe).Single(e => e.Label == "Andere");
        b.Schreibe(b.Wurzel, gruppe, beton.Label, andere.Label, andere);
        Assert.Equal("131", b.Wurzel.Werte["haltung.material"].Originalcode);

        // Ein Schritt nimmt Gruppe UND nachgezogenes Detail zurueck.
        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.Equal("102", b.Wurzel.Werte["haltung.material"].Originalcode);
        Assert.Equal("Beton", b.Wurzel.Werte["haltung.pipegroup"].Text);
        Assert.Equal(materialNachArmiert, h.GetFieldValue(Material));

        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.True(verlauf.Rueckgaengig(H).Angewendet);
        Assert.False(verlauf.KannRueckgaengig(H));
        Assert.Empty(p.Objektakten); // die erst beim Schreiben angelegte Wurzelakte

        for (var i = 0; i < 3; i++)
            Assert.True(verlauf.Wiederholen(H).Angewendet);
        Assert.Equal("131", p.Objektakten.Single().Werte["haltung.material"].Originalcode);
    }
}

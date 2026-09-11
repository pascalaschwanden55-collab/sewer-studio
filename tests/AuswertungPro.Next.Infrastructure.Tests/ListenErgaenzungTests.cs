using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Objektakten;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>Eigene Listeneintraege und Korrekturen: programmweit, ueber dem Katalog, nie im Katalog.</summary>
public sealed class ListenErgaenzungTests
{
    private static ObjektAuswahl[] Basis() =>
    [
        new(0, "0", "Unbekannt"), new(1, "1", "Beton"), new(2, "2", "Stahl"),
    ];

    private sealed class Speicher : IObjektaktenListenErgaenzungen
    {
        public List<ListenErgaenzung> Inhalt { get; } = new();
        public int Gespeichert { get; private set; }
        public IReadOnlyList<ListenErgaenzung> Lade() => Inhalt.ToArray();
        public void Speichere(IEnumerable<ListenErgaenzung> ergaenzungen) { Inhalt.Clear(); Inhalt.AddRange(ergaenzungen); Gespeichert++; }
    }

    [Fact]
    public void Regel_blendet_aus_benennt_um_und_haengt_eigene_an()
    {
        var ergaenzungen = new List<ListenErgaenzung>
        {
            new("k", null, ListenErgaenzungArt.Ausgeblendet, "2", null),
            new("k", null, ListenErgaenzungArt.Umbenannt, "1", "Beton (alt)"),
            new("k", null, ListenErgaenzungArt.Hinzugefuegt, "9", "Holz"),
            new("k", null, ListenErgaenzungArt.Hinzugefuegt, null, "Lehm"),
            new("andere", null, ListenErgaenzungArt.Ausgeblendet, "0", null), // fremde Liste, ohne Wirkung
            new("k", "3", ListenErgaenzungArt.Ausgeblendet, "1", null),       // fremde Elterngruppe, ohne Wirkung
        };
        var ergebnis = ListenErgaenzungRegel.Anwenden(Basis(), "k", null, ergaenzungen);

        Assert.Equal(["Unbekannt", "Beton (alt)", "Holz", "Lehm"], ergebnis.Select(e => e.Label));
        Assert.Equal("1", ergebnis[1].OriginalCode); // Umbenennen laesst den Code stehen
        Assert.All(ergebnis.Where(e => e.Eigen), e => Assert.True(e.Index >= ListenErgaenzungRegel.EigeneAbPosition));
        Assert.Equal("9", ergebnis[2].OriginalCode);
        Assert.Null(ergebnis[3].OriginalCode);
        Assert.Contains("eigener Eintrag", ergebnis[2].Anzeige);
        Assert.DoesNotContain("eigener", ergebnis[0].Anzeige);
    }

    [Fact]
    public void Ohne_passende_Ergaenzung_bleibt_die_Basis_unveraendert()
    {
        var ergebnis = ListenErgaenzungRegel.Anwenden(Basis(), "k", "1", [new("k", null, ListenErgaenzungArt.Ausgeblendet, "1", null)]);
        Assert.Equal(Basis(), ergebnis);
    }

    [Fact]
    public void Unsinnige_Ergaenzungen_werden_abgewiesen()
    {
        Assert.Throws<ArgumentException>(() => new ListenErgaenzung("k", null, ListenErgaenzungArt.Hinzugefuegt, null, " ").Pruefe());
        Assert.Throws<ArgumentException>(() => new ListenErgaenzung("k", null, ListenErgaenzungArt.Umbenannt, null, "x").Pruefe());
        Assert.Throws<ArgumentException>(() => new ListenErgaenzung("k", null, ListenErgaenzungArt.Ausgeblendet, "", null).Pruefe());
        Assert.Throws<ArgumentException>(() => new ListenErgaenzung("", null, ListenErgaenzungArt.Hinzugefuegt, null, "x").Pruefe());
    }

    [Fact]
    public void Bearbeitung_schreibt_nur_ihre_Liste_und_laesst_andere_stehen()
    {
        var speicher = new Speicher();
        speicher.Inhalt.Add(new("fremd", null, ListenErgaenzungArt.Hinzugefuegt, null, "Bleibt"));
        var b = new ListenErgaenzungBearbeitung(speicher, "Material", "k", null, Basis());
        Assert.Equal(3, b.Zeilen.Count);
        Assert.False(b.Geaendert);

        b.Umbenennen("1", "Beton (alt)");
        b.Ausblenden("2", true);
        var eigen = b.Hinzufuegen("Holz", "9");
        Assert.True(b.Geaendert);
        Assert.Throws<ArgumentException>(() => b.Hinzufuegen("holz", null));   // gleicher Text, andere Schreibung
        Assert.Throws<ArgumentException>(() => b.Hinzufuegen("Kork", "9"));   // Code schon vergeben
        Assert.Throws<InvalidOperationException>(() => b.Entfernen(b.Zeilen.First(z => !z.Eigen)));

        b.Speichere();
        Assert.Equal(1, speicher.Gespeichert);
        Assert.False(b.Geaendert);
        Assert.Contains(speicher.Inhalt, e => e.KatalogId == "fremd" && e.Text == "Bleibt");
        Assert.Contains(speicher.Inhalt, e => e.KatalogId == "k" && e.Art == ListenErgaenzungArt.Umbenannt && e.Code == "1" && e.Text == "Beton (alt)");
        Assert.Contains(speicher.Inhalt, e => e.KatalogId == "k" && e.Art == ListenErgaenzungArt.Ausgeblendet && e.Code == "2");
        Assert.Contains(speicher.Inhalt, e => e.KatalogId == "k" && e.Art == ListenErgaenzungArt.Hinzugefuegt && e.Text == "Holz" && e.Code == "9");

        // Zurueck auf den Originaltext heisst: keine Umbenennung mehr gespeichert.
        var erneut = new ListenErgaenzungBearbeitung(speicher, "Material", "k", null, Basis());
        Assert.Equal("Beton (alt)", erneut.Zeilen.Single(z => z.Code == "1").Text);
        erneut.Umbenennen("1", "Beton");
        erneut.Entfernen(erneut.Zeilen.Single(z => z.Eigen));
        erneut.Speichere();
        Assert.DoesNotContain(speicher.Inhalt, e => e.KatalogId == "k" && e.Art == ListenErgaenzungArt.Umbenannt);
        Assert.DoesNotContain(speicher.Inhalt, e => e.KatalogId == "k" && e.Art == ListenErgaenzungArt.Hinzugefuegt);
        Assert.Contains(speicher.Inhalt, e => e.KatalogId == "k" && e.Art == ListenErgaenzungArt.Ausgeblendet);
        _ = eigen;
    }

    [Fact]
    public void Datei_ueberlebt_die_Runde_und_eine_kaputte_Datei_bricht_ab()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "sewerstudio-listen-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(ordner);
        try
        {
            var store = new ObjektaktenListenErgaenzungenStore(ordner);
            Assert.Empty(store.Lade());
            store.Speichere([
                new("haltung.material-je-eltern", "3", ListenErgaenzungArt.Hinzugefuegt, null, "Polyäthylen, alt"),
                new("haltung-C05", null, ListenErgaenzungArt.Umbenannt, "5", "Übrige"),
            ]);
            var gelesen = new ObjektaktenListenErgaenzungenStore(ordner).Lade();
            Assert.Equal(2, gelesen.Count);
            Assert.Equal("Polyäthylen, alt", gelesen[0].Text);
            Assert.Equal("3", gelesen[0].Eltern);
            Assert.Equal(ListenErgaenzungArt.Umbenannt, gelesen[1].Art);

            File.WriteAllText(Path.Combine(ordner, ObjektaktenListenErgaenzungenStore.Dateiname), "{ kaputt");
            Assert.Throws<InvalidDataException>(() => new ObjektaktenListenErgaenzungenStore(ordner).Lade());
        }
        finally { Directory.Delete(ordner, recursive: true); }
    }

    [Fact]
    public void Eigener_Eintrag_erscheint_in_der_Maske_und_laesst_sich_speichern_ein_erfundener_nicht()
    {
        var speicher = new Speicher();
        speicher.Inhalt.Add(new("haltung.material-je-eltern", "1", ListenErgaenzungArt.Hinzugefuegt, null, "Sichtbeton, alt"));
        speicher.Inhalt.Add(new("haltung.material-je-eltern", "1", ListenErgaenzungArt.Ausgeblendet, "1003", null)); // Polymerbeton (PMB)

        var projekt = new Project(); var haltung = new HaltungRecord(); projekt.Data.Add(haltung);
        var b = new ObjektaktenBearbeitung(projekt, haltung.Id, "haltung", speicher);
        var gruppe = FieldCatalog.Objektfelder.Feld("haltung.pipegroup");
        var material = FieldCatalog.Objektfelder.Feld("haltung.material");
        b.Schreibe(b.Wurzel, gruppe, "", "Beton");

        var eintraege = b.ErlaubteEintraege(b.Wurzel, material);
        Assert.Equal(14, eintraege.Count); // 14 Betonsorten - 1 ausgeblendet + 1 eigener
        Assert.DoesNotContain(eintraege, e => e.OriginalCode == "1003");
        var eigen = Assert.Single(eintraege, e => e.Eigen);
        Assert.Equal("Sichtbeton, alt", eigen.Label);
        Assert.Equal("1", eigen.Eltern);

        b.Schreibe(b.Wurzel, material, "", eigen.Label, eigen);
        var wert = b.Wurzel.Werte["haltung.material"];
        Assert.Equal("haltung.material-je-eltern", wert.KatalogId);
        Assert.Null(wert.Originalcode);
        Assert.Equal(ListenErgaenzungRegel.EigeneAbPosition, wert.LokalerEintrag);

        // Eine behauptete Markierung reicht nicht: der Eintrag muss aus der Ergaenzungsdatei kommen.
        var erfunden = new ObjektAuswahl(ListenErgaenzungRegel.EigeneAbPosition, null, "Gold") { Eltern = "1", Eigen = true };
        Assert.Throws<InvalidOperationException>(() => b.Schreibe(b.Wurzel, material, eigen.Label, "Gold", erfunden));

        // Ohne Speicher gibt es keine Ergaenzungen - und keinen Fehler.
        var ohne = new ObjektaktenBearbeitung(projekt, haltung.Id, "haltung");
        Assert.Equal(14, ohne.ErlaubteEintraege(ohne.Wurzel, material).Count);
        Assert.DoesNotContain(ohne.ErlaubteEintraege(ohne.Wurzel, material), e => e.Eigen);
    }
}

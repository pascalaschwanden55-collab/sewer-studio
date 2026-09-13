using AuswertungPro.Next.Application.UseCases.Objektakten;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.ViewModels;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>Abhaengige Auswahllisten: die Unterliste folgt der gewaehlten Elterngruppe,
/// fuer alle Gruppen - nicht nur fuer die, die der Plan vom 10.09. zufaellig belegt sah.</summary>
public sealed class ObjektaktenUnterlistenTests
{
    private static (ObjektaktenBearbeitung Bearbeitung, ObjektakteViewModel Ansicht, HaltungRecord Haltung) Haltung()
    {
        var projekt = new Project();
        var haltung = new HaltungRecord();
        projekt.Data.Add(haltung);
        var bearbeitung = new ObjektaktenBearbeitung(projekt, haltung.Id, "haltung");
        return (bearbeitung, new ObjektakteViewModel(bearbeitung, new(), () => { }, () => true, () => { }), haltung);
    }

    private static ObjektFeldViewModel Feld(ObjektakteViewModel ansicht, string id)
        => ansicht.Gruppen.SelectMany(g => g.Felder).Single(f => f.Feld.Id == id);

    [Fact]
    public void Beton_zeigt_dieselben_vierzehn_Sorten_wie_bisher()
    {
        var (b, ansicht, _) = Haltung();
        Feld(ansicht, "haltung.pipegroup").Text = "Beton";

        var bisher = FieldCatalog.Objektfelder.Auswahl("haltung-C06")!.Eintraege.Select(e => e.Label).ToArray();
        var jetzt = Feld(ansicht, "haltung.material").Optionen.Select(e => e.Label).ToArray();
        Assert.Equal(14, bisher.Length);
        Assert.Equal(bisher, jetzt);
    }

    [Fact]
    public void Kunststoff_zeigt_seine_Unterliste_und_Polyethylen_ist_dabei()
    {
        var (_, ansicht, _) = Haltung();
        Feld(ansicht, "haltung.pipegroup").Text = "Kunststoff";

        var material = Feld(ansicht, "haltung.material");
        var optionen = material.Optionen.ToArray();
        Assert.Equal(11, optionen.Length);
        Assert.Contains(optionen, e => e.Label == "Polyethylen (PE)");
        Assert.All(optionen, e => Assert.Equal("3", e.Eltern));
        Assert.Equal("", material.Hinweis);
    }

    [Fact]
    public void Ohne_Elterngruppe_ist_die_Unterliste_leer_und_sagt_warum()
    {
        var (_, ansicht, _) = Haltung();
        var material = Feld(ansicht, "haltung.material");
        Assert.Empty(material.Optionen);
        Assert.StartsWith("Zuerst", material.Hinweis);
        Assert.DoesNotContain("noch nicht belegt", material.Hinweis);
    }

    [Fact]
    public void Ein_Kunststoff_wird_gespeichert_und_beim_Neuoeffnen_wiedererkannt()
    {
        var (b, ansicht, haltung) = Haltung();
        Feld(ansicht, "haltung.pipegroup").Text = "Kunststoff";
        var material = Feld(ansicht, "haltung.material");
        var polyethylen = material.Optionen.Single(e => e.Label == "Polyethylen (PE)");

        material.Auswahl = polyethylen;

        var wert = b.Wurzel.Werte["haltung.material"];
        Assert.Equal("haltung.material-je-eltern", wert.KatalogId);
        Assert.Equal("133", wert.Originalcode);
        Assert.Equal("Polyethylen (PE)", wert.Text);
        // Der Bestandswert der Haltung ist ueber das Vokabular normalisiert.
        Assert.Equal(MaterialVokabular.Normalisieren("Polyethylen (PE)"), haltung.GetFieldValue(FieldKeys.PipeMaterial));

        var erneut = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { });
        Assert.Equal(polyethylen, Feld(erneut, "haltung.material").Auswahl);
        // Kein Warnhinweis mehr - nur der gespeicherte Originalcode, den die Maske immer zeigt.
        Assert.StartsWith("Originalcode: 133", Feld(erneut, "haltung.material").Hinweis);
        Assert.Contains("DSS-Material: Kunststoff_Polyethylen", Feld(erneut, "haltung.material").Hinweis);
    }

    [Fact]
    public void Wechsel_der_Elterngruppe_zieht_den_Wert_auf_den_ersten_Eintrag_der_neuen_Liste()
    {
        var (b, ansicht, haltung) = Haltung();
        Feld(ansicht, "haltung.pipegroup").Text = "Kunststoff";
        Feld(ansicht, "haltung.material").Auswahl = Feld(ansicht, "haltung.material").Optionen.Single(e => e.Label == "Polyethylen (PE)");

        Feld(ansicht, "haltung.pipegroup").Text = "Guss";

        var material = Feld(ansicht, "haltung.material");
        Assert.Equal(4, material.Optionen.Count());
        Assert.Contains(material.Optionen, e => e.Label == "Grauguss (GG)");
        // Wie im WebGIS (Entscheid Pascal 12.09.2026): ein Detail aus der alten Gruppe bleibt nicht
        // sichtbar falsch stehen, sondern springt auf den ersten Eintrag der neuen Liste - im
        // Bestandsfeld der Haltung ebenso wie in der Akte.
        Assert.Equal("Guss, unbekannt (GU)", material.Text);
        Assert.Equal("Guss, unbekannt (GU)", material.Auswahl?.Label);
        Assert.Equal("113", b.Wurzel.Werte["haltung.material"].Originalcode);
        Assert.Equal(MaterialVokabular.Normalisieren("Guss, unbekannt (GU)"), haltung.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.DoesNotContain("bleibt erhalten", material.Hinweis);
    }

    [Fact]
    public void Ein_falscher_Eintrag_wird_beim_Schreiben_abgewiesen()
    {
        var (b, _, _) = Haltung();
        var feld = FieldCatalog.Objektfelder.Feld("haltung.material");
        var fremd = FieldCatalog.Objektfelder.Auswahl("haltung-C05")!.Eintraege[1]; // Eintrag des Eltern-Katalogs
        Assert.Throws<InvalidOperationException>(() => b.Schreibe(b.Wurzel, feld, "", fremd.Label, fremd));
        Assert.Null(ObjektaktenBearbeitung.KatalogDesEintrags(feld, fremd));
    }

    [Fact]
    public void Bauwerksteil_Subart_folgt_der_Art()
    {
        var projekt = new Project();
        var schacht = new SchachtRecord();
        projekt.SchaechteData.Add(schacht);
        var b = new ObjektaktenBearbeitung(projekt, schacht.Id, "schacht");
        var teil = b.Neu("bauwerksteil");
        var ansicht = new ObjektakteViewModel(b, new(), () => { }, () => true, () => { }) { Auswahl = teil };

        var subart = Feld(ansicht, "bauwerksteil.subart");
        Assert.Empty(subart.Optionen);
        Assert.StartsWith("Zuerst", subart.Hinweis);

        Feld(ansicht, "bauwerksteil.art").Text = "Einstiegshilfe";
        Assert.Contains(Feld(ansicht, "bauwerksteil.subart").Optionen, e => e.Label == "Leiter");
        Assert.Contains(Feld(ansicht, "bauwerksteil.subart").Optionen, e => e.Label == "Steigeisen");

        // Eine Art ohne Unterliste sagt das, statt eine fremde zu zeigen.
        Feld(ansicht, "bauwerksteil.art").Text = "Trockenwetterrinne";
        Assert.Empty(Feld(ansicht, "bauwerksteil.subart").Optionen);
        Assert.Contains("keine Unterliste", Feld(ansicht, "bauwerksteil.subart").Hinweis);
    }
}

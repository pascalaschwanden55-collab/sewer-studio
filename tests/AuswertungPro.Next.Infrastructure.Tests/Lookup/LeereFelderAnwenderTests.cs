using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Tests.Lookup;

/// <summary>
/// Der Ausfuehrer schreibt nur den Plan — und prueft dabei noch einmal, dass das
/// Zielfeld wirklich leer ist.
/// </summary>
public sealed class LeereFelderAnwenderTests
{
    [Fact]
    public void Ein_geplantes_Feld_wird_geschrieben()
    {
        var record = Haltung("80638-80631");

        var geschrieben = LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        Assert.Equal(1, geschrieben);
        Assert.Equal("Steinzeug", record.GetFieldValue(FieldKeys.PipeMaterial));
    }

    // Der aus dem Kataster geholte Wert ist KEINE Handeingabe. Waere er als solche
    // markiert, ginge er beim naechsten Mal als "vom Operateur gesetzt" in die
    // revidierte XTF zurueck — in dieselbe Quelle, aus der er stammt.
    [Fact]
    public void Der_nachgefuellte_Wert_gilt_nicht_als_Handeingabe()
    {
        var record = Haltung("80638-80631");

        LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        var meta = record.FieldMeta[FieldKeys.PipeMaterial];

        Assert.False(meta.UserEdited);
        Assert.Equal(FieldSource.Kataster, meta.Source);
    }

    // Zwischen Planung und Bestaetigung kann der Bearbeiter etwas eingetippt haben.
    // Seine Arbeit gewinnt auch dann.
    [Fact]
    public void Ein_inzwischen_gefuelltes_Feld_bleibt_unberuehrt()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Manual, userEdited: true);

        var geschrieben = LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        Assert.Equal(0, geschrieben);
        Assert.Equal("Beton", record.GetFieldValue(FieldKeys.PipeMaterial));
    }

    // Der Fall aus dem echten Projekt: Der Bearbeiter loescht den Inhalt einer Zelle
    // im Raster. Die Bindung schreibt dabei direkt in Fields und laesst FieldMeta
    // unberuehrt — das Feld ist danach LEER, traegt aber weiter UserEdited=true.
    //
    // Der Schutz in SetFieldValue weist einen automatischen Schreibvorgang auf ein
    // handmarkiertes Feld ab. An einem leeren Feld ist dieser Schutz sinnlos: Es gibt
    // dort keine Arbeit zu schuetzen, und der Nachfuelllauf hat sichtbar gemeldet,
    // dass er es fuellen wuerde. In Jagdmatt trifft das Schacht 33461 (Dimension
    // leer, UserEdited=true).
    [Fact]
    public void Ein_leeres_Feld_mit_alter_Handmarkierung_wird_trotzdem_gefuellt()
    {
        var record = Haltung("80638-80631");
        record.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Manual, userEdited: true);
        record.Fields[FieldKeys.PipeMaterial] = "";   // wie das Leeren im Raster

        var geschrieben = LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        Assert.Equal(1, geschrieben);
        Assert.Equal("Steinzeug", record.GetFieldValue(FieldKeys.PipeMaterial));
        Assert.False(record.FieldMeta[FieldKeys.PipeMaterial].UserEdited);
    }

    [Fact]
    public void Auch_am_Schacht_wird_ein_leeres_handmarkiertes_Feld_gefuellt()
    {
        var record = new SchachtRecord();
        record.SetFieldValue("Schachtnummer", "33461", FieldSource.Xtf, userEdited: false);
        record.SetFieldValue("Dimension", "120 x 120 mm", FieldSource.Manual, userEdited: true);
        record.Fields["Dimension"] = "";

        var plan = new LeereFelderPlan(
            BauteilArt.Schacht,
            new[] { new LeereFeldPosition("33461", "Dimension", "600 mm") },
            Array.Empty<LeerfeldHinweis>(),
            GepruefteBauteile: 1);

        Assert.Equal(1, LeereFelderAnwender.WendeAnAufSchaechte(new[] { record }, plan));
        Assert.Equal("600 mm", record.GetFieldValue("Dimension"));
    }

    // Die gezaehlte Zahl muss stimmen. Vorher zaehlte der Ausfuehrer jeden Versuch,
    // auch einen abgewiesenen — die Meldung "12 Felder ergaenzt" waere gelogen gewesen.
    [Fact]
    public void Gezaehlt_wird_nur_was_wirklich_geschrieben_wurde()
    {
        var gefuellt = Haltung("80638-80631");
        gefuellt.SetFieldValue(FieldKeys.PipeMaterial, "Beton", FieldSource.Manual, userEdited: true);
        var leer = Haltung("80631-80551");

        var geschrieben = LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { gefuellt, leer },
            Plan(
                new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug"),
                new LeereFeldPosition("80631-80551", FieldKeys.PipeMaterial, "Zement")));

        Assert.Equal(1, geschrieben);
    }

    [Fact]
    public void Ein_Datensatz_ausserhalb_des_Plans_bleibt_unberuehrt()
    {
        var record = Haltung("99-999");

        var geschrieben = LeereFelderAnwender.WendeAnAufHaltungen(
            new[] { record },
            Plan(new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug")));

        Assert.Equal(0, geschrieben);
        Assert.Equal("", record.GetFieldValue(FieldKeys.PipeMaterial));
    }

    [Fact]
    public void Der_Bericht_nennt_Zahl_und_Feld()
    {
        var bericht = LeereFelderBericht.Schreibe(
            Plan(
                new LeereFeldPosition("80638-80631", FieldKeys.PipeMaterial, "Steinzeug"),
                new LeereFeldPosition("80631-80551", FieldKeys.PipeMaterial, "Zement")),
            @"D:\QGIS\Leitungen.gpkg");

        Assert.Contains("2 leere Felder auf 2 Haltungen", bericht, StringComparison.Ordinal);
        Assert.Contains("2x  Rohrmaterial", bericht, StringComparison.Ordinal);
        Assert.Contains(@"D:\QGIS\Leitungen.gpkg", bericht, StringComparison.Ordinal);
        Assert.Contains("nie ueberschrieben", bericht, StringComparison.Ordinal);
    }

    [Fact]
    public void Der_Bericht_nennt_die_mehrdeutigen_Namen()
    {
        var plan = new LeereFelderPlan(
            BauteilArt.Haltung,
            Array.Empty<LeereFeldPosition>(),
            new[] { new LeerfeldHinweis("u-u", LeerfeldGrund.Mehrdeutig) },
            GepruefteBauteile: 1);

        var bericht = LeereFelderBericht.Schreibe(plan, "x.gpkg");

        Assert.Contains("1 mit mehrfach vorkommendem Namen", bericht, StringComparison.Ordinal);
        Assert.Contains("nichts zu ergaenzen", bericht, StringComparison.OrdinalIgnoreCase);
    }

    // Am Schacht heissen die Felder nach der Excel-Kopfzeile: Der Eigentuemer steht
    // dort als "Eigentümer" mit Umlaut, waehrend Import und Nachfuellen "Eigentuemer"
    // meinen. Ohne Aufloesung entstuende ein zweites, unsichtbares Feld daneben.
    [Fact]
    public void Der_Eigentuemer_landet_im_Feld_mit_Umlaut()
    {
        var record = new SchachtRecord();
        record.Fields["Schachtnummer"] = "80089";
        record.Fields["Eigentümer"] = "";

        var plan = new LeereFelderPlan(
            BauteilArt.Schacht,
            new[] { new LeereFeldPosition("80089", FieldKeys.Owner, "Abwasser Uri") },
            Array.Empty<LeerfeldHinweis>(),
            GepruefteBauteile: 1);

        Assert.Equal(1, LeereFelderAnwender.WendeAnAufSchaechte(new[] { record }, plan));
        Assert.Equal("Abwasser Uri", record.GetFieldValue("Eigentümer"));
        Assert.False(record.Fields.ContainsKey("Eigentuemer"));
    }

    // Und umgekehrt: Steht im Feld mit Umlaut bereits ein Wert, gilt es als gefuellt.
    // Sonst wuerde der Lauf danebenschreiben und der sichtbare Wert bliebe der alte.
    [Fact]
    public void Ein_gefuelltes_Umlautfeld_wird_nicht_ueberschrieben()
    {
        var record = new SchachtRecord();
        record.Fields["Schachtnummer"] = "80089";
        record.Fields["Eigentümer"] = "Privat";

        var plan = new LeereFelderPlan(
            BauteilArt.Schacht,
            new[] { new LeereFeldPosition("80089", FieldKeys.Owner, "Abwasser Uri") },
            Array.Empty<LeerfeldHinweis>(),
            GepruefteBauteile: 1);

        Assert.Equal(0, LeereFelderAnwender.WendeAnAufSchaechte(new[] { record }, plan));
        Assert.Equal("Privat", record.GetFieldValue("Eigentümer"));
    }

    private static LeereFelderPlan Plan(params LeereFeldPosition[] positionen)
        => new(BauteilArt.Haltung, positionen, Array.Empty<LeerfeldHinweis>(), positionen.Length);

    private static HaltungRecord Haltung(string name)
    {
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.HoldingName, name, FieldSource.Xtf, userEdited: false);
        return record;
    }
}

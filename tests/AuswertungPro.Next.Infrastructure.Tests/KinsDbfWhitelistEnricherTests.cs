using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Kins;
using AuswertungPro.Next.Infrastructure.Tests.Import;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// KINS-DBF-Anreicherung: schacht.DBF liefert die Schachtliste, haltung.DBF
/// fuellt NUR leere Haltungsfelder (Whitelist). Der Zeilen-Match laeuft ueber
/// das Schachtpaar S_O/S_U → schacht.DBF-Nummern → "{oben}-{unten}".
/// </summary>
public sealed class KinsDbfWhitelistEnricherTests : IDisposable
{
    private readonly string _dir;

    public KinsDbfWhitelistEnricherTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "KinsDbfEnricherTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "DBA"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private void SchreibeSchachtDbf()
    {
        new DbfTestFileBuilder()
            .Feld("NR", 'I', 4)
            .Feld("BEZ", 'C', 25)
            .Feld("STRASSE", 'C', 25)
            .Feld("MATERIAL", 'C', 25)
            .Feld("TIEFE", 'N', 7, 2)
            .Feld("UNTDATUM", 'D', 8)
            .Record(r => r.Int32(1).Text("58951").Text("Grünweg").Text("Beton").Text("   2.50").Text("20260624"))
            .Record(r => r.Int32(2).Text("58950").Text("").Text("").Text("   0.00").Text(""))
            .Schreiben(Path.Combine(_dir, "DBA", "schacht.DBF"));
    }

    private void SchreibeHaltungDbf()
    {
        new DbfTestFileBuilder()
            .Feld("BEZ", 'C', 51)
            .Feld("S_O", 'I', 4)
            .Feld("S_U", 'I', 4)
            .Feld("STRASSE", 'C', 25)
            .Feld("EIGENT", 'C', 25)
            .Feld("MATERIAL", 'C', 25)
            .Feld("HALTLAENGE", 'N', 7, 2)
            .Feld("BREITE", 'N', 4)
            .Feld("BAUJAHR", 'N', 4)
            .Record(r => r.Text("10").Int32(1).Int32(2).Text("Grünweg").Text("Gemeinde Altdorf")
                .Text("Beton").Text("  30.40").Text(" 600").Text("1988"))
            .Schreiben(Path.Combine(_dir, "DBA", "haltung.DBF"));
    }

    private static HaltungRecord ErzeugeHaltung(string name, string oben = "", string unten = "")
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", name, FieldSource.Xtf, userEdited: false);
        if (oben.Length > 0) record.SetFieldValue("Schacht_oben", oben, FieldSource.Xtf, userEdited: false);
        if (unten.Length > 0) record.SetFieldValue("Schacht_unten", unten, FieldSource.Xtf, userEdited: false);
        return record;
    }

    [Fact]
    public void Apply_LegtSchachtlisteAusSchachtDbfAn()
    {
        SchreibeSchachtDbf();
        var project = new Project();

        var result = KinsDbfWhitelistEnricher.Apply(project, _dir);

        Assert.Equal(2, project.SchaechteData.Count);
        Assert.Equal(2, result.SchaechteNeu);
        var s1 = project.SchaechteData.Single(s => s.GetFieldValue("Schachtnummer") == "58951");
        Assert.Equal("Grünweg", s1.GetFieldValue("Strasse"));
        Assert.Equal("2.50", s1.GetFieldValue("Schachttiefe"));
    }

    [Fact]
    public void InstanceService_LegtSchachtlisteWieDieFassadeAn()
    {
        SchreibeSchachtDbf();
        var project = new Project();
        var service = new KinsDbfWhitelistEnrichmentService();

        var result = service.Apply(project, _dir);

        Assert.Equal(2, result.SchaechteNeu);
        Assert.Equal(2, project.SchaechteData.Count);
        Assert.Contains(
            project.SchaechteData,
            s => s.GetFieldValue("Schachtnummer") == "58951"
                 && s.GetFieldValue("Schachttiefe") == "2.50");
    }

    [Fact]
    public void Apply_IstIdempotent_LegtKeineDoppeltenSchaechteAn()
    {
        SchreibeSchachtDbf();
        var project = new Project();

        KinsDbfWhitelistEnricher.Apply(project, _dir);
        var zweiter = KinsDbfWhitelistEnricher.Apply(project, _dir);

        Assert.Equal(2, project.SchaechteData.Count);
        Assert.Equal(0, zweiter.SchaechteNeu);
    }

    [Fact]
    public void Apply_FuelltNurLeereHaltungsfelder_UeberSchachtpaarMatch()
    {
        SchreibeSchachtDbf();
        SchreibeHaltungDbf();
        var project = new Project();
        var record = ErzeugeHaltung("58951-58950", "58951", "58950");
        record.SetFieldValue("Haltungslaenge_m", "30.4", FieldSource.Legacy, userEdited: false); // schon aus TXT
        record.SetFieldValue("DN_mm", "600", FieldSource.Xtf, userEdited: false);                 // schon aus XTF
        project.Data.Add(record);

        var result = KinsDbfWhitelistEnricher.Apply(project, _dir);

        Assert.Equal("Grünweg", record.GetFieldValue("Strasse"));
        Assert.Equal("Gemeinde Altdorf", record.GetFieldValue("Eigentuemer"));
        Assert.Equal("30.4", record.GetFieldValue("Haltungslaenge_m")); // nicht ueberschrieben
        Assert.Equal("600", record.GetFieldValue("DN_mm"));             // nicht ueberschrieben
        Assert.True(result.HaltungsfelderGesetzt >= 2);
    }

    [Fact]
    public void Apply_SetztHaltungslaenge_WennLeerUndDbfWertVorhanden()
    {
        SchreibeSchachtDbf();
        SchreibeHaltungDbf();
        var project = new Project();
        var record = ErzeugeHaltung("58951-58950", "58951", "58950");
        project.Data.Add(record);

        KinsDbfWhitelistEnricher.Apply(project, _dir);

        Assert.Equal("30.4", record.GetFieldValue("Haltungslaenge_m"));
    }

    [Fact]
    public void Apply_UserEditedFelderBleibenUnveraendert()
    {
        SchreibeSchachtDbf();
        SchreibeHaltungDbf();
        var project = new Project();
        var record = ErzeugeHaltung("58951-58950", "58951", "58950");
        record.SetFieldValue("Strasse", "Vom Benutzer", FieldSource.Manual, userEdited: true);
        project.Data.Add(record);

        KinsDbfWhitelistEnricher.Apply(project, _dir);

        Assert.Equal("Vom Benutzer", record.GetFieldValue("Strasse"));
    }

    [Fact]
    public void Apply_MatchtUeberBezeichnung_WennSchachtfelderFehlen()
    {
        SchreibeHaltungDbf(); // ohne schacht.DBF: Schachtpaar nicht aufloesbar
        var project = new Project();
        var record = ErzeugeHaltung("10"); // XTF-Bezeichnung als Name
        project.Data.Add(record);

        KinsDbfWhitelistEnricher.Apply(project, _dir);

        Assert.Equal("Grünweg", record.GetFieldValue("Strasse"));
    }

    [Fact]
    public void Apply_OhneDbfDateien_LiefertNurMeldung()
    {
        var project = new Project();

        var result = KinsDbfWhitelistEnricher.Apply(project, _dir);

        Assert.Equal(0, result.HaltungsfelderGesetzt);
        Assert.Equal(0, result.SchaechteNeu);
        Assert.NotEmpty(result.Messages);
    }

    [Fact]
    public void Apply_FehlenderQuellordner_LiefertKlareMeldungOhneAenderung()
    {
        var project = new Project();
        var fehlenderOrdner = Path.Combine(_dir, "nicht-vorhanden");

        var result = KinsDbfWhitelistEnricher.Apply(project, fehlenderOrdner);

        Assert.Equal(0, result.HaltungsfelderGesetzt);
        Assert.Equal(0, result.SchaechteNeu);
        Assert.Equal(0, result.SchaechteAktualisiert);
        Assert.Equal(
            ["KINS-DBF: Quellordner nicht gefunden \u2014 Anreicherung uebersprungen."],
            result.Messages);
        Assert.Empty(project.Data);
        Assert.Empty(project.SchaechteData);
        Assert.False(Directory.Exists(fehlenderOrdner));
    }
}

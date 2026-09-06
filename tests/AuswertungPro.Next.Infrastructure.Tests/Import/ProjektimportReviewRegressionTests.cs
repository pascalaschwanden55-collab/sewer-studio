using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Lookup;
using AuswertungPro.Next.Application.UseCases;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>Gegenproben des Reviews: echte Leser und Verteiler, nur künstliche Quellen.</summary>
public sealed class ProjektimportReviewRegressionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "import-review-" + Guid.NewGuid().ToString("N"));

    public ProjektimportReviewRegressionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void GleicheAnzahlAberAndereSchaeden_DarfKeineXtfVerwerfen()
    {
        var a = Quelle("a.xtf", "BAB");
        var b = Quelle("b.xtf", "BBC");
        Assert.Equal(2, Auswahl(a, b).Uebernommen.Count);
    }

    [Fact]
    public void DirektBenachbarterZeitpunkt_TrenntZweiAufnahmetage()
    {
        var a = Quelle("a.xtf", "BAB", "20260905");
        var b = Quelle("b.xtf", "BAB", "20250905");
        var reader = new XtfQuellenPruefer();
        Assert.NotEqual(reader.Pruefe(a).Untersuchungsfingerabdruck, reader.Pruefe(b).Untersuchungsfingerabdruck);
        Assert.Equal(2, Auswahl(a, b).Uebernommen.Count);
    }

    [Fact]
    public void IdentischerInhalt_DarfEinmalGelesenWerden()
    {
        var a = Quelle("a.xtf", "BAB");
        var b = Quelle("b.xtf", "BAB");
        Assert.Single(Auswahl(a, b).Uebernommen);
    }

    [Fact]
    public void GeonisTidAusXtf405_WiderspruchBleibtGeschuetzt()
    {
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "100-200", FieldSource.Xtf405, false);
        h.SetFieldValue(FieldKeys.CadastreObjectId, "ch23h1a400000002", FieldSource.Xtf405, false);
        var k = KatasterKennung.FuerHaltung("100-200", null, "ch23h1a400000001", null, null, null, null, null, null, null);
        var bestand = new KatasterKennungBestand(BauteilArt.Haltung,
            new Dictionary<string, KatasterKennung> { ["100-200"] = k }, new HashSet<string>(), 1, "2024-12");

        var plan = KatasterKennungPlanBuilder.BaueFuerHaltungen([h], bestand);
        Assert.Empty(plan.Positionen);
        Assert.NotEmpty(plan.Hinweise);
        Assert.Null(h.Geonis);
    }

    [Fact]
    public void EinzelnesUpstreamVideo_IstKeineGegenbefahrung()
    {
        var rollen = Befahrungsrollen.Ordne("100-200", [new BefahrungsBeleg("clip.mpg", "gegen_Fliessrichtung")]);
        Assert.DoesNotContain(rollen, r => r.Rolle == Befahrungsrolle.Gegenbefahrung);
    }

    [Fact]
    public void FehlendesQuellvideo_WirdAlsFehlerGezaehlt()
    {
        var p = new Project();
        var h = new HaltungRecord();
        h.SetFieldValue(FieldKeys.HoldingName, "100-200", FieldSource.Xtf, false);
        h.SetFieldValue(FieldKeys.Link, Path.Combine(_root, "fehlt.mpg"), FieldSource.Xtf, false);
        p.Data.Add(h);
        var ziel = Path.Combine(_root, "projekt");
        Directory.CreateDirectory(ziel);
        var result = new KanalImportDistributionService().Distribute(p, ziel, Path.Combine(ziel, "PDF"), _root, false);
        Assert.Equal(1, result.Errors);
    }

    [Fact]
    public void GleicheSchachtXtfZweimal_ErzeugtKeineWeitereBegehung()
    {
        var path = Quelle("schacht.xtf", "DACB", schacht: true);
        var p = new Project();
        var reader = new LegacyXtfImportService();
        reader.ImportXtfFiles([path], p);
        var s = Assert.Single(p.SchaechteData);
        Assert.Empty(s.Protocol!.History);
        reader.ImportXtfFiles([path], p);
        Assert.Empty(s.Protocol.History);
    }

    private static XtfExportWahl Auswahl(params string[] paths)
    {
        var reader = new XtfQuellenPruefer();
        return XtfExportAuswahl.Waehle(paths.Select(p => new XtfExportKandidat(p, reader.Pruefe(p))).ToList());
    }

    [Fact]
    public void Dateibilanz_ZaehltNurLesbareDateienImProjekt()
    {
        var p = new Project();
        var h = new HaltungRecord();
        p.Data.Add(h);
        h.SetFieldValue(FieldKeys.Link, "fehlt.mpg", FieldSource.Legacy, false);
        var bestand = ImportProjektdateiPruefer.Pruefe(p, _root, null);
        Assert.Equal(0, bestand.Bestand.HaltungenMitVideo);
        Assert.True(bestand.Bestand.DateienGeprueft);
        Assert.Single(bestand.Fehler);
        File.WriteAllText(Path.Combine(_root, "fehlt.mpg"), "Videoinhalt");
        bestand = ImportProjektdateiPruefer.Pruefe(p, _root, null);
        Assert.Equal(1, bestand.Bestand.HaltungenMitVideo);
        Assert.Empty(bestand.Fehler);
        Assert.False(ImportProjektdateiPruefer.IstLesbar("../ausserhalb.mpg", _root, null, out _));
    }

    [Fact]
    public void Dateibilanz_LiestVorbereiteteDateiVorVeroeffentlichung()
    {
        var projektpfad = Path.Combine(_root, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(Path.GetDirectoryName(projektpfad)!);
        File.WriteAllText(projektpfad, "{}");
        var quelle = Path.Combine(_root, "quelle.mpg");
        File.WriteAllText(quelle, "Videoinhalt");
        using var staging = new ImportFileStagingService().Begin(projektpfad)!;
        var ziel = staging.StageCopy(quelle, Path.Combine(_root, "Videos"));
        Assert.False(File.Exists(ziel));
        Assert.True(ImportProjektdateiPruefer.IstLesbar(Path.GetRelativePath(_root, ziel), _root, staging, out var grund), grund);
    }

    [Fact]
    public void GeaenderteSchachtuntersuchung_BleibtAlsWeitereBegehungErhalten()
    {
        var p = new Project();
        var reader = new LegacyXtfImportService();
        reader.ImportXtfFiles([Quelle("a.xtf", "DACB", schacht: true)], p);
        var schacht = Assert.Single(p.SchaechteData);
        schacht.Protocol!.Current.Entries[0].Beschreibung = "Handkorrektur";
        reader.ImportXtfFiles([Quelle("a.xtf", "DACB", schacht: true)], p);
        Assert.Equal("Handkorrektur", schacht.Protocol.Current.Entries[0].Beschreibung);
        reader.ImportXtfFiles([Quelle("b.xtf", "DACB", "20270905", schacht: true)], p);
        Assert.Single(schacht.Protocol.History);
    }

    private string Quelle(string name, string code, string date = "20260905", bool schacht = false)
    {
        var klasse = schacht ? "Normschachtschaden" : "Kanalschaden";
        var feld = schacht ? "SchachtSchadencode" : "KanalSchadencode";
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, $"""
            <TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3"><HEADERSECTION><MODELS>
            <MODEL NAME="VSA_KEK_2020_LV95"/></MODELS></HEADERSECTION><DATASECTION><VSA_KEK_2020_LV95.KEK BID="B1">
            <VSA_KEK_2020_LV95.KEK.Untersuchung TID="U1"><Bezeichnung>{(schacht ? "3133" : "100-200")}</Bezeichnung><Zeitpunkt>{date}</Zeitpunkt><Erfassungsart>{(schacht ? "Begehung" : "Kanalfernsehen")}</Erfassungsart><Operateur>Test</Operateur></VSA_KEK_2020_LV95.KEK.Untersuchung>
            <VSA_KEK_2020_LV95.KEK.{klasse} TID="D1"><{feld}>{code}</{feld}><UntersuchungRef REF="U1"/></VSA_KEK_2020_LV95.KEK.{klasse}>
            </VSA_KEK_2020_LV95.KEK></DATASECTION></TRANSFER>
            """);
        return path;
    }

    public void Dispose() => Directory.Delete(_root, true);
}

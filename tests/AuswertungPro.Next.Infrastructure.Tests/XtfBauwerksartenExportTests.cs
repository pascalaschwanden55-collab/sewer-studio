using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfBauwerksartenExportTests
{
    [Theory]
    [InlineData("Normschacht", "Kontrollschacht", "Normschacht")]
    [InlineData("Spezialbauwerk", "Pumpwerk", "Spezialbauwerk")]
    [InlineData("Versickerungsanlage", "Sickerschacht", "Versickerungsanlage")]
    [InlineData("Einleitstelle", "", "Einleitstelle")]
    public void Die_erfasste_Bauwerksart_bestimmt_die_Xtf_Klasse(string art, string funktion, string klasse)
    {
        var record = Schacht(art, funktion);
        var plan = XtfNeuPlanBuilder.Build([], [record]);

        var bauwerk = Assert.Single(plan.Objekte, o => o.Klasse == klasse);
        var knoten = Assert.Single(plan.Objekte, o => o.Klasse == "Abwasserknoten");
        Assert.Equal(bauwerk.Tid, knoten.Verweise.Single(v => v.Name == "AbwasserbauwerkRef").ZielTid);
        Assert.Equal("Z2", bauwerk.Felder.Single(f => f.Key == "BaulicherZustand").Value);
        Assert.Equal("Seilergasse", bauwerk.Felder.Single(f => f.Key == "Standortname").Value);
        if (klasse != "Normschacht")
            Assert.DoesNotContain(bauwerk.Felder, f => f.Key == "Material");
        if (klasse is "Einleitstelle" or "Spezialbauwerk")
            Assert.DoesNotContain(bauwerk.Felder, f => f.Key is "Dimension1" or "Dimension2");
    }

    [Fact]
    public void Ein_Sickerschacht_wird_auch_ohne_neues_Typfeld_als_Versickerungsanlage_erkannt()
    {
        var record = Schacht("", "Sickerschacht");
        var plan = XtfNeuPlanBuilder.Build([], [record]);
        var bauwerk = Assert.Single(plan.Objekte, o => o.Klasse == "Versickerungsanlage");
        Assert.Equal("Versickerungsschacht", bauwerk.Felder.Single(f => f.Key == "Art").Value);
        Assert.DoesNotContain(plan.Objekte, o => o.Klasse == "Normschacht");
    }

    [Fact]
    public void Eine_unbekannte_Bauwerksart_wird_nicht_als_Normschacht_ausgegeben()
    {
        var plan = XtfNeuPlanBuilder.Build([], [Schacht("Fantasiebauwerk", "")]);
        Assert.True(plan.Leer);
        Assert.Empty(plan.Objekte);
        Assert.Contains(plan.Hinweise, h => h.Contains("Fantasiebauwerk", StringComparison.Ordinal));
    }

    [Fact]
    public void Vollstaendiger_Export_behaelt_Form_Bemerkung_und_Kennungen_beim_Rueckimport()
    {
        using var temp = new ExportOrdner();
        var record = Schacht("Normschacht", "Kontrollschacht");
        record.SetFieldValue(FieldKeys.ShaftShape, "Oval", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.Remarks, "Tiefe 1.58m", FieldSource.Manual, true);
        record.SetFieldValue("Tiefe", "1.58", FieldSource.Manual, true);
        record.SetFieldValue(FieldKeys.PdfPath, @"C:\Kunden\intern.pdf", FieldSource.Manual, true);
        record.Geonis = new GeonisKennungen { Knoten = "ch24gwkdftlGdbHU", Bauwerk = "ch24gwkdUmcgr2UF" };
        var project = new Project { Name = "Bauwerksarten Test" };
        project.SchaechteData.Add(record);

        var result = new XtfNeuExportService().Erzeuge(new(project, temp.Path));

        Assert.True(result.Ok, result.Fehler);
        var xml = XDocument.Load(result.Datei!);
        var bauwerk = Assert.Single(xml.Descendants().Where(e => e.Name.LocalName.EndsWith(".Normschacht", StringComparison.Ordinal)));
        Assert.Equal("ch24gwkdUmcgr2UF", (string?)bauwerk.Attribute("TID"));
        Assert.Equal("Tiefe 1.58m", bauwerk.Elements().Single(e => e.Name.LocalName == "Bemerkung").Value);
        Assert.Contains(xml.Descendants(), e => e.Name.LocalName == "Wert" && e.Value == "Oval");
        Assert.DoesNotContain("intern.pdf", xml.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(System.IO.Path.Combine(temp.Path, "SewerStudio_Zusatz_2026.ili")));

        var reimport = new Project();
        new LegacyXtfImportService().ImportXtfFiles([result.Datei!], reimport);
        var gelesen = Assert.Single(reimport.SchaechteData);
        Assert.Equal("Oval", gelesen.GetFieldValue(FieldKeys.ShaftShape));
        Assert.Equal("Tiefe 1.58m", gelesen.GetFieldValue(FieldKeys.Remarks));
        Assert.Equal("Normschacht", gelesen.GetFieldValue("Bauwerksart"));
        Assert.Equal("Seilergasse", gelesen.GetFieldValue(FieldKeys.Street));
    }

    internal static SchachtRecord Schacht(string art, string funktion)
    {
        var record = new SchachtRecord();
        foreach (var (key, value) in new Dictionary<string, string>
        {
            ["Schachtnummer"] = "12345", ["Bauwerksart"] = art, ["Funktion"] = funktion,
            ["Material"] = "Beton", [FieldKeys.Owner] = "Privat", [FieldKeys.ConditionClass] = "2",
            [FieldKeys.Street] = "Seilergasse", [FieldKeys.ShaftDimension1Mm] = "1100",
            [FieldKeys.ShaftDimension2Mm] = "900"
        })
            record.SetFieldValue(key, value, FieldSource.Manual, true);
        return record;
    }

    [Theory]
    [InlineData("Normschacht", "Kontrollschacht")]
    [InlineData("Spezialbauwerk", "Regenbecken_Fangbecken")]
    [InlineData("Versickerungsanlage", "Sickerschacht")]
    [InlineData("Einleitstelle", "")]
    public void Neue_Bauwerksarten_und_Zusatzmasse_bleiben_beim_Import_erhalten(string art, string funktion)
    {
        using var temp = new ExportOrdner();
        var p = new Project();
        p.SchaechteData.Add(Schacht(art, funktion));
        var export = new XtfNeuExportService().Erzeuge(new(p, temp.Path));
        Assert.True(export.Ok, export.Fehler);
        var gelesen = new Project();
        var stats = new LegacyXtfImportService().ImportXtfFiles([export.Datei!], gelesen);
        Assert.Equal(0, stats.Errors);
        var s = Assert.Single(gelesen.SchaechteData);
        Assert.Equal(art, s.GetFieldValue(FieldKeys.ShaftStructureType));
        Assert.Equal("1100", s.GetFieldValue(FieldKeys.ShaftDimension1Mm));
        Assert.Equal("900", s.GetFieldValue(FieldKeys.ShaftDimension2Mm));
        Assert.Equal("Beton", s.GetFieldValue("Material"));
        Assert.Equal(funktion, s.GetFieldValue("Funktion"));
    }

    [Theory]
    [InlineData("doppelt")]
    [InlineData("fremdes_ziel")]
    [InlineData("pfad")]
    [InlineData("leer")]
    [InlineData("standard_hat_vorrang")]
    public void Zweifelhafte_Zusatzangaben_ueberschreiben_keine_Daten(string fall)
    {
        using var temp = new ExportOrdner();
        var p = new Project();
        var s = Schacht("Normschacht", "Kontrollschacht");
        s.SetFieldValue(FieldKeys.ShaftShape, "Oval", FieldSource.Manual, true);
        s.SetFieldValue(FieldKeys.Remarks, "Original", FieldSource.Manual, true);
        p.SchaechteData.Add(s);
        var export = new XtfNeuExportService().Erzeuge(new(p, temp.Path));
        Assert.True(export.Ok, export.Fehler);
        var xml = XDocument.Load(export.Datei!);
        XNamespace ns = "http://www.interlis.ch/INTERLIS2.3";
        var extra = xml.Descendants(ns + XtfZusatzangaben.Klasse).Single();
        switch (fall)
        {
            case "doppelt":
                extra.AddAfterSelf(new XElement(extra));
                break;
            case "fremdes_ziel": extra.Element(ns + "ObjektTid")!.Value = "chSST00000000000"; break;
            case "pfad": extra.Element(ns + "Feld")!.Value = FieldKeys.PdfPath; break;
            case "leer": extra.Element(ns + "Wert")!.Value = ""; break;
            case "standard_hat_vorrang": extra.Element(ns + "Feld")!.Value = FieldKeys.Remarks; break;
        }
        var quelle = System.IO.Path.Combine(temp.Path, "manipuliert.xtf");
        xml.Save(quelle);
        var gelesen = new Project();
        var stats = new LegacyXtfImportService().ImportXtfFiles([quelle], gelesen);
        Assert.Equal(0, stats.Errors);
        var ziel = Assert.Single(gelesen.SchaechteData);
        Assert.True(string.IsNullOrEmpty(ziel.GetFieldValue(FieldKeys.ShaftShape)));
        Assert.True(string.IsNullOrEmpty(ziel.GetFieldValue(FieldKeys.PdfPath)));
        Assert.Equal("Original", ziel.GetFieldValue(FieldKeys.Remarks));
    }

    [Fact]
    public void Handwerte_und_Vorlagennamen_bleiben_beim_Zusatzimport_geschuetzt()
    {
        using var temp = new ExportOrdner();
        var p = new Project();
        var s = Schacht("Normschacht", "Kontrollschacht");
        s.SetFieldValue("Massnahmen", "Deckel ersetzen", FieldSource.Manual, true);
        p.SchaechteData.Add(s);
        var export = new XtfNeuExportService().Erzeuge(new(p, temp.Path));
        Assert.True(export.Ok, export.Fehler);
        var gelesen = new Project();
        var ziel = Schacht("Normschacht", "Kontrollschacht");
        ziel.SetFieldValue("Massnahmen", "Vorhandener Entscheid", FieldSource.Manual, true);
        gelesen.SchaechteData.Add(ziel);
        new LegacyXtfImportService().ImportXtfFiles([export.Datei!], gelesen);
        Assert.Equal("Vorhandener Entscheid", ziel.GetFieldValue("Massnahmen"));
        Assert.False(ziel.Fields.ContainsKey(FieldKeys.RecommendedRehabilitationMeasures));
    }

    [Fact]
    public void Vorhandenes_anderes_Modell_wird_nicht_ueberschrieben()
    {
        using var temp = new ExportOrdner();
        var datei = System.IO.Path.Combine(temp.Path, XtfZusatzangaben.Modell + ".ili");
        File.WriteAllText(datei, "bestehendes Modell");
        var p = new Project();
        var s = Schacht("Normschacht", "Kontrollschacht");
        s.SetFieldValue(FieldKeys.ShaftShape, "Oval", FieldSource.Manual, true);
        p.SchaechteData.Add(s);
        var export = new XtfNeuExportService().Erzeuge(new(p, temp.Path));
        Assert.False(export.Ok);
        Assert.Equal("bestehendes Modell", File.ReadAllText(datei));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.xtf"));
    }

    private sealed class ExportOrdner : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "XtfBauwerksarten_" + Guid.NewGuid().ToString("N"));
        public ExportOrdner() => Directory.CreateDirectory(Path);
        public void Dispose() => Directory.Delete(Path, true);
    }
}

using System.Xml.Linq;
using AuswertungPro.Next.Application.Xtf;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfAenderungsExportTests
{
    [Fact]
    public void Nur_bearbeitete_Objekte_und_Felder_erhalten_einen_Aenderungsauftrag()
    {
        var projekt = new Project();
        var s = Schacht("12345");
        var zeit = new DateTime(2026, 9, 3, 12, 15, 0, DateTimeKind.Utc);
        s.SetFieldValue(FieldKeys.ShaftShape, "Oval", FieldSource.Manual, true);
        s.FieldMeta[FieldKeys.ShaftShape].LastUpdatedUtc = zeit;
        projekt.SchaechteData.Add(s);
        projekt.SchaechteData.Add(Schacht("67890"));
        var voll = XtfZusatzangaben.Ergaenze(XtfNeuPlanBuilder.Build([], projekt.SchaechteData), projekt);
        var delta = XtfAenderungsPlanBuilder.Build(voll, projekt);
        Assert.False(delta.Leer);
        Assert.True(delta.NurAenderungen);
        Assert.Equal(1, delta.Schaechte);
        var bauwerk = Assert.Single(delta.Objekte, o => o.Klasse == "Normschacht");
        Assert.DoesNotContain(bauwerk.Felder, f => f.Key is "Material" or "BaulicherZustand");
        var auftrag = Assert.Single(delta.Objekte, o => o.Klasse == "Aenderung");
        Assert.Equal(bauwerk.Tid, auftrag.Felder.Single(f => f.Key == "ObjektTid").Value);
        Assert.Equal("Zusatz:Schachtform", auftrag.Felder.Single(f => f.Key == "Feld").Value);
        Assert.Equal(zeit.ToString("O"), auftrag.Felder.Single(f => f.Key == "GeaendertAm").Value);
        Assert.DoesNotContain(delta.Objekte, o => o.Felder.Any(f => f.Value == "67890"));
    }

    [Fact]
    public void Importwerte_und_reine_Dateipfadaenderungen_erzeugen_keine_Lieferung()
    {
        var p = new Project();
        var s = Schacht("12345");
        s.SetFieldValue(FieldKeys.PdfPath, "datei.pdf", FieldSource.Manual, true);
        p.SchaechteData.Add(s);
        var delta = XtfAenderungsPlanBuilder.Build(XtfNeuPlanBuilder.Build([], p.SchaechteData), p);
        Assert.True(delta.Leer);
        Assert.Empty(delta.Objekte);
    }

    [Fact]
    public void Erneuter_Export_verliert_keine_unbestaetigte_Aenderung()
    {
        var p = new Project();
        var s = Schacht("12345");
        s.SetFieldValue(FieldKeys.ConditionClass, "2", FieldSource.Manual, true);
        p.SchaechteData.Add(s);
        var voll = XtfNeuPlanBuilder.Build([], p.SchaechteData);
        var a = XtfAenderungsPlanBuilder.Build(voll, p);
        var b = XtfAenderungsPlanBuilder.Build(voll, p);
        Assert.Equal(a.Objekte.Select(o => o.Tid), b.Objekte.Select(o => o.Tid));
        Assert.True(s.IsUserEdited(FieldKeys.ConditionClass));
        var bauwerk = Assert.Single(a.Objekte, o => o.Klasse == "Normschacht");
        Assert.Equal("Z2", bauwerk.Felder.Single(f => f.Key == "BaulicherZustand").Value);
        Assert.DoesNotContain(bauwerk.Felder, f => f.Key == "Material");
    }

    private static SchachtRecord Schacht(string nummer)
    {
        var s = new SchachtRecord();
        s.SetFieldValue("Schachtnummer", nummer, FieldSource.Xtf405, false);
        s.SetFieldValue(FieldKeys.Owner, "Privat", FieldSource.Xtf405, false);
        s.SetFieldValue("Material", "Beton", FieldSource.Xtf405, false);
        s.SetFieldValue(FieldKeys.ConditionClass, "4", FieldSource.Xtf405, false);
        return s;
    }

    [Fact]
    public void Aenderungsdatei_enthaelt_Feldauftrag_und_laesst_sich_zuruecklesen()
    {
        var ordner = Path.Combine(Path.GetTempPath(), "XtfDelta_" + Guid.NewGuid().ToString("N"));
        try
        {
            var p = new Project();
            var s = Schacht("12345");
            s.SetFieldValue(FieldKeys.ShaftShape, "Oval", FieldSource.Manual, true);
            p.SchaechteData.Add(s);
            var export = new XtfNeuExportService().Erzeuge(new(p, ordner, NurAenderungen: true));
            Assert.True(export.Ok, export.Fehler);
            var xml = XDocument.Load(export.Datei!);
            XNamespace ns = "http://www.interlis.ch/INTERLIS2.3";
            var marker = Assert.Single(xml.Descendants(ns + XtfZusatzangaben.Topic + ".Aenderung"));
            Assert.Equal("Zusatz:Schachtform", (string?)marker.Element(ns + "Feld"));
            var r = new Project();
            Assert.Equal(0, new LegacyXtfImportService().ImportXtfFiles([export.Datei!], r).Errors);
            Assert.Equal("Oval", Assert.Single(r.SchaechteData).GetFieldValue(FieldKeys.ShaftShape));
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "Material");
        }
        finally { if (Directory.Exists(ordner)) Directory.Delete(ordner, true); }
    }
}

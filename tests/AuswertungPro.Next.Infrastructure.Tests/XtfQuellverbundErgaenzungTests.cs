using System.Text.Json;
using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfQuellverbundErgaenzungTests
{
    [Fact]
    public void Fehlender_Bezug_wird_aus_ausgewaehlter_Quelle_mit_Originalkennung_ergaenzt()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SewerStudio-Xtf-Bezug-" + Guid.NewGuid());
        try
        {
            var p = XtfDssVerbundTests.Verbund();
            var quelle = new XtfNeuExportService().Erzeuge(new(p, Path.Combine(dir, "quelle")));
            Assert.True(quelle.Ok, quelle.Fehler);
            var original = File.ReadAllBytes(quelle.Datei!);
            var kanal = p.Objektakten[0].Quellen.Single(q => q.Klasse == "Kanal");
            p.Objektakten[0].Quellen.Remove(kanal);
            var vorher = JsonSerializer.Serialize(p);
            var dienst = new XtfNeuExportService();
            var fehlt = dienst.Erzeuge(new(p, Path.Combine(dir, "ohne"), NurAenderungen: true));
            Assert.False(fehlt.Ok); Assert.True(fehlt.QuelleFehlt); Assert.False(Directory.Exists(Path.Combine(dir, "ohne")));
            var export = dienst.Erzeuge(new(p, Path.Combine(dir, "ausgabe"), NurAenderungen: true, Quelldateien: [quelle.Datei!]));
            Assert.True(export.Ok, export.Fehler);
            Assert.Contains(XDocument.Load(export.Datei!).Descendants(), e => e.Name.LocalName.EndsWith(".Kanal") && (string?)e.Attribute("TID") == kanal.Kennung);
            Assert.Equal(vorher, JsonSerializer.Serialize(p)); Assert.Equal(original, File.ReadAllBytes(quelle.Datei!));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Doppelte_Quellkennung_sperrt_die_Ergaenzung_auch_bei_gleichem_Inhalt()
    {
        var dir = Path.Combine(Path.GetTempPath(), "SewerStudio-Xtf-Doppelt-" + Guid.NewGuid());
        try
        {
            var p = XtfDssVerbundTests.Verbund();
            var quelle = new XtfNeuExportService().Erzeuge(new(p, dir));
            Assert.True(quelle.Ok, quelle.Fehler);
            var xml = XDocument.Load(quelle.Datei!);
            var kanal = xml.Descendants().Single(e => e.Name.LocalName.EndsWith(".Kanal"));
            kanal.Parent!.Add(new XElement(kanal));
            var fehlerhafteQuelle = Path.Combine(dir, "doppelt.xtf"); xml.Save(fehlerhafteQuelle);
            p.Objektakten[0].Quellen.RemoveAll(q => q.Klasse == "Kanal");
            var result = new XtfNeuExportService().Erzeuge(new(p, Path.Combine(dir, "ausgabe"), Quelldateien: [fehlerhafteQuelle]));
            Assert.False(result.Ok); Assert.Contains("mehrfach", result.Fehler); Assert.Null(result.Datei);
            Assert.False(Directory.Exists(Path.Combine(dir, "ausgabe")));
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

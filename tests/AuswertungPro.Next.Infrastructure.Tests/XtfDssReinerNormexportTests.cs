using System.Xml.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;
using static AuswertungPro.Next.Infrastructure.Tests.XtfDssExportTests;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Buerglen 14.09.2026: Der Empfaenger kann eine XTF mit unserem Zusatzmodell nur lesen, wenn
/// er dessen .ili in sein Modellverzeichnis legt — sonst scheitert der ganze Transfer
/// («model(s) not found»). Die reine Normlieferung ist der Weg ohne diese Bedingung. Sie war
/// an drei Stellen an das Zusatzmodell gekoppelt und liess sich aus einem echten Projekt
/// ueberhaupt nicht erzeugen.
/// </summary>
public sealed class XtfDssReinerNormexportTests
{
    private static Project MitWertenOhneNormziel()
    {
        var p = XtfDssVerbundTests.Verbund();
        var s = p.SchaechteData[0];
        // «Saniert» ist eine erlaubte Programmwahl, aber kein DSS-Wert.
        s.SetFieldValue(FieldKeys.RehabilitationNeed, "Saniert", FieldSource.Manual, true);
        // Nur ein Jahr ist kein INTERLIS-Datum.
        var sanierung = p.Objektakten.Single(a => a.Art == "sanierung");
        sanierung.Werte.Remove("sanierung.beginn");
        sanierung.Werte["sanierung.s_year"] = new() { Text = "2026", VonHand = true };
        // Fehler in den Katasterdaten: die Einstiegshilfe traegt die Art «1».
        p.Objektakten.Single(a => a.Id == s.Id).Quellen.Single(q => q.Klasse == "Einstiegshilfe").Werte["Art"] = "1";
        return p;
    }

    [Fact]
    public void Reine_Normlieferung_entsteht_trotz_Werten_ohne_Normziel_und_ohne_Zusatzmodell()
    {
        var p = MitWertenOhneNormziel();
        var dir = Path.Combine(Path.GetTempPath(), "SewerStudio-Dss-Norm-" + Guid.NewGuid().ToString("N"));
        try
        {
            var r = new XtfNeuExportService().Erzeuge(new(p, dir, MitZusatzangaben: false));
            Assert.True(r.Ok, r.Fehler + "\n" + r.Bericht);

            var xml = XDocument.Load(r.Datei!);
            Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName.StartsWith("SewerStudio_Zusatz", StringComparison.Ordinal));
            Assert.DoesNotContain(xml.Descendants().Where(e => e.Name.LocalName == "MODEL"),
                e => e.Attribute("NAME")?.Value?.StartsWith("SewerStudio_Zusatz", StringComparison.Ordinal) == true);
            Assert.False(File.Exists(Path.Combine(dir, "SewerStudio_Zusatz_2026.ili")));

            // Ueberholter Bedarf und blosses Jahr werden entfernt, nicht geraten.
            var bauwerk = xml.Descendants().Single(e => e.Name.LocalName.EndsWith(".Normschacht", StringComparison.Ordinal));
            Assert.DoesNotContain(bauwerk.Elements(), e => e.Name.LocalName == "Sanierungsbedarf");
            // Das Original behaelt seinen Zeitpunkt; die neue Sanierung bekommt aus «2026» keinen.
            var unterhalte = xml.Descendants().Where(e => e.Name.LocalName.EndsWith(".Unterhalt", StringComparison.Ordinal)).ToArray();
            Assert.Contains(unterhalte, u => !u.Elements().Any(e => e.Name.LocalName == "Zeitpunkt"));
            Assert.DoesNotContain(unterhalte.SelectMany(u => u.Elements()),
                e => e.Name.LocalName == "Zeitpunkt" && e.Value.StartsWith("2026", StringComparison.Ordinal));
            Assert.DoesNotContain(xml.Descendants().Single(e => e.Name.LocalName.EndsWith(".Einstiegshilfe", StringComparison.Ordinal)).Elements(),
                e => e.Name.LocalName == "Art");
            foreach (var wort in new[] { "Saniert", "2026", "«1»" })
                Assert.Contains(wort, r.Bericht, StringComparison.Ordinal);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }

    [Fact]
    public void Dieselben_Werte_gehen_mit_Zusatzmodell_unveraendert_mit()
    {
        var p = MitWertenOhneNormziel();
        var dir = Path.Combine(Path.GetTempPath(), "SewerStudio-Dss-Zusatz-" + Guid.NewGuid().ToString("N"));
        try
        {
            var r = new XtfNeuExportService().Erzeuge(new(p, dir, MitZusatzangaben: true));
            Assert.True(r.Ok, r.Fehler + "\n" + r.Bericht);
            var xml = XDocument.Load(r.Datei!);
            Assert.Contains(xml.Descendants(), e => e.Name.LocalName.StartsWith("SewerStudio_Zusatz", StringComparison.Ordinal));
            Assert.True(File.Exists(Path.Combine(dir, "SewerStudio_Zusatz_2026.ili")));
            Assert.Contains("Erfasste_Angaben", xml.ToString(), StringComparison.Ordinal);
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}

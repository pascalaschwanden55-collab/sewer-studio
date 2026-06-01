using System;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfImportTests
{
    [Fact]
    public void Sia405Import_ParsesHoldingMaterialAndDn_FromSyntheticBasket()
    {
        // Minimales SYNTHETISCHES SIA405-XTF (kein echter Kundendatensatz):
        // ein SIA405_Abwasser-Basket mit einer Haltung inkl. Material und lichter Hoehe (DN).
        // Deckt den realen Parse-Pfad LegacyXtfImportService.ParseSia405 ab, ohne Uri/Altdorf-Daten ins Repo zu legen.
        var tempPath = Path.Combine(Path.GetTempPath(), $"sia405-{Guid.NewGuid():N}.xtf");
        File.WriteAllText(tempPath, """
<?xml version="1.0" encoding="UTF-8"?>
<TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
  <HEADERSECTION SENDER="SewerStudioTest" VERSION="2.3">
    <MODELS>
      <MODEL NAME="SIA405_Abwasser_2015_LV95" />
    </MODELS>
  </HEADERSECTION>
  <DATASECTION>
    <SIA405_Abwasser.SIA405_Abwasser BID="B1">
      <Haltung TID="H1">
        <Bezeichnung>80638-80631</Bezeichnung>
        <LaengeEffektiv>22.5</LaengeEffektiv>
        <Lichte_Hoehe>300</Lichte_Hoehe>
        <Material>Steinzeug</Material>
      </Haltung>
    </SIA405_Abwasser.SIA405_Abwasser>
  </DATASECTION>
</TRANSFER>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);

            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));
            Assert.True(stats.Errors == 0, debug);
            Assert.True(project.Data.Count > 0, $"No records imported.\n{debug}");
            Assert.Equal(stats.Found, project.Data.Count);

            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "80638-80631", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("Steinzeug", rec!.GetFieldValue("Rohrmaterial"));
            Assert.Equal("300", rec.GetFieldValue("DN_mm"));
            Assert.Equal("22.5", rec.GetFieldValue("Haltungslaenge_m"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void M150Import_MergesIntoExistingHolding_WhenNameFormattingDiffers()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"xtf-import-{Guid.NewGuid():N}.m150");
        File.WriteAllText(tempPath, """
<root>
  <row>
    <Haltung>80638 - 80631</Haltung>
    <Inspektionsdatum>2025-01-03</Inspektionsdatum>
    <Laenge>22.5</Laenge>
  </row>
</root>
""");

        try
        {
            var project = new Project();
            var existing = new HaltungRecord();
            existing.SetFieldValue("Haltungsname", "80638-80631", FieldSource.Manual, userEdited: true);
            project.AddRecord(existing);

            var svc = new LegacyXtfImportService();
            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);

            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));
            Assert.True(stats.Errors == 0, debug);
            Assert.Single(project.Data);
            Assert.Equal("03.01.2025", existing.GetFieldValue("Datum_Jahr"));
            Assert.Equal("22.5", existing.GetFieldValue("Haltungslaenge_m"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void M150Import_BuildsHoldingFromHg011Hg012_WhenCombinedHoldingIsMissing()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"xtf-import-{Guid.NewGuid():N}.m150");
        File.WriteAllText(tempPath, """
<root>
  <HG>
    <HG011>80638</HG011>
    <HG012>80631</HG012>
    <HG008>22.5</HG008>
    <HI>
      <HI104>2025-01-03</HI104>
    </HI>
  </HG>
</root>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            Assert.True(stats.Errors == 0, debug);
            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "80638-80631", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("03.01.2025", rec!.GetFieldValue("Datum_Jahr"));
            Assert.Equal("22.5", rec.GetFieldValue("Haltungslaenge_m"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void M150Import_ParsesIsybauHgHiStructure_IntoHoldingDateLengthAndVideoLink()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"xtf-import-{Guid.NewGuid():N}.xml");
        File.WriteAllText(tempPath, """
<?xml version="1.0" encoding="iso-8859-1"?>
<DATA>
  <HG>
    <HG003>23021</HG003>
    <HG004>22369</HG004>
    <HG304>BETON</HG304>
    <HG306>300</HG306>
    <HG310>35.120</HG310>
    <HI>
      <HI104>2014-04-22</HI104>
      <HI116>1_1_1_22042014_112151.mp2</HI116>
      <HZ>
        <HZ001>9.8</HZ001>
        <HZ002>BBA</HZ002>
        <HZ010>Komplexes Wurzelwerk</HZ010>
      </HZ>
    </HI>
  </HG>
</DATA>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            Assert.True(stats.Errors == 0, debug);
            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "23021-22369", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("22.04.2014", rec!.GetFieldValue("Datum_Jahr"));
            Assert.Equal("35.120", rec.GetFieldValue("Haltungslaenge_m"));
            Assert.Equal("300", rec.GetFieldValue("DN_mm"));
            Assert.Equal("BETON", rec.GetFieldValue("Rohrmaterial"));
            Assert.Equal("1_1_1_22042014_112151.mp2", rec.GetFieldValue("Link"));
            Assert.Contains("BBA", rec.GetFieldValue("Primaere_Schaeden"));
            Assert.True(rec.VsaFindings.Count > 0, "Expected VsaFindings from HZ nodes");
            Assert.Contains(rec.VsaFindings, f => string.Equals(f.KanalSchadencode, "BBA", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(stats.Messages, m => m.Context == "M150" && m.Message.Contains("HG erkannt=1", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }

    [Fact]
    public void M150Import_PrefersHi116VideoFile_OverHi006Code()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"xtf-import-{Guid.NewGuid():N}.xml");
        File.WriteAllText(tempPath, """
<?xml version="1.0" encoding="iso-8859-1"?>
<DATA>
  <HG>
    <HG003>23021</HG003>
    <HG004>22369</HG004>
    <HI>
      <HI006>L100</HI006>
      <HI104>2014-04-22</HI104>
      <HI116>1_1_1_22042014_112151.mp2</HI116>
    </HI>
  </HG>
</DATA>
""");

        try
        {
            var project = new Project();
            var svc = new LegacyXtfImportService();

            var stats = svc.ImportXtfFiles(new[] { tempPath }, project);
            var debug = string.Join("\n", stats.Messages.Select(m => $"{m.Level}: {m.Message} ({m.Context})"));

            Assert.True(stats.Errors == 0, debug);
            var rec = project.Data.FirstOrDefault(r =>
                string.Equals(r.GetFieldValue("Haltungsname"), "23021-22369", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(rec);
            Assert.Equal("1_1_1_22042014_112151.mp2", rec!.GetFieldValue("Link"));
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }
}

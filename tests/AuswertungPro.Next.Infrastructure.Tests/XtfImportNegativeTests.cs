using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class XtfImportNegativeTests
{
    [Fact]
    public void ImportXtfFiles_AbgeschnitteneDatei_StopptFolgendeGueltigeDateiNicht()
    {
        var root = Path.Combine(Path.GetTempPath(), $"xtf-negative-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var broken = Path.Combine(root, "01_abgeschnitten.xtf");
        var valid = Path.Combine(root, "02_gueltig.xtf");
        File.WriteAllText(broken, "<TRANSFER><DATASECTION>");
        File.WriteAllText(valid, """
            <?xml version="1.0" encoding="UTF-8"?>
            <TRANSFER xmlns="http://www.interlis.ch/INTERLIS2.3">
              <HEADERSECTION SENDER="SewerStudioTest" VERSION="2.3">
                <MODELS><MODEL NAME="SIA405_Abwasser_2015_LV95" /></MODELS>
              </HEADERSECTION>
              <DATASECTION>
                <SIA405_Abwasser.SIA405_Abwasser BID="B1">
                  <Haltung TID="H1">
                    <Bezeichnung>100-200</Bezeichnung>
                    <LaengeEffektiv>12.5</LaengeEffektiv>
                    <Lichte_Hoehe>300</Lichte_Hoehe>
                    <Material>Steinzeug</Material>
                  </Haltung>
                </SIA405_Abwasser.SIA405_Abwasser>
              </DATASECTION>
            </TRANSFER>
            """);
        var project = new Project();

        try
        {
            var service = new XtfImportServiceAdapter(new LegacyXtfImportService());

            var result = service.ImportXtfFiles([broken, valid], project);

            Assert.True(result.Ok, result.ErrorMessage);
            var stats = Assert.IsType<AuswertungPro.Next.Application.Import.ImportStats>(result.Value);
            Assert.Equal(1, stats.Errors);
            Assert.Equal(1, stats.Found);
            Assert.Contains(stats.Messages, message =>
                message.Contains("01_abgeschnitten.xtf", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(stats.Messages, message =>
                message.Contains("Importiert 1 Haltungen", StringComparison.OrdinalIgnoreCase));

            var record = Assert.Single(project.Data);
            Assert.Equal("100-200", record.GetFieldValue("Haltungsname"));
            Assert.Equal("Steinzeug", record.GetFieldValue("Rohrmaterial"));
            Assert.Equal("300", record.GetFieldValue("DN_mm"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

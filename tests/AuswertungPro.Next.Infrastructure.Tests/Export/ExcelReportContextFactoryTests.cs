using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Export.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

public sealed class ExcelReportContextFactoryTests
{
    [Fact]
    public void Berichtstitel_uebernimmt_Projekt_Zone_und_Aufnahmejahre()
    {
        var project = new Project { Name = "GEP Altdorf" };
        project.Metadata["Zone"] = "Zone 1.15";

        var first = new HaltungRecord();
        first.SetFieldValue(FieldKeys.InspectionYear, "24.09.2024", FieldSource.Manual, userEdited: false);
        var second = new HaltungRecord();
        second.SetFieldValue(FieldKeys.InspectionYear, "2026", FieldSource.Manual, userEdited: false);
        project.Data.Add(first);
        project.Data.Add(second);

        var title = ExcelReportContextFactory.AusProjekt(project).TitelFuer("Haltungen");

        Assert.Equal("GEP Altdorf Zone 1.15 / Aufnahmen 2024-2026 Haltungen", title);
    }

    [Fact]
    public void Leere_Metadaten_erzeugen_keine_Platzhalter_oder_Trennzeichen()
    {
        var project = new Project { Name = "Neues Projekt" };
        project.Metadata["Zone"] = "  ";

        var title = ExcelReportContextFactory.AusProjekt(project).TitelFuer("Schächte");

        Assert.Equal("Schächte", title);
    }

    [Fact]
    public void Aufnahmezeitraum_in_einem_Feld_verliert_das_Endjahr_nicht()
    {
        var project = new Project { Name = "GEP" };
        var record = new HaltungRecord();
        record.SetFieldValue(FieldKeys.InspectionYear, "2024/2025", FieldSource.Manual, userEdited: false);
        project.Data.Add(record);

        var title = ExcelReportContextFactory.AusProjekt(project).TitelFuer("Haltungen");

        Assert.Equal("GEP / Aufnahmen 2024-2025 Haltungen", title);
    }

    [Fact]
    public void Bereits_im_Projektnamen_enthaltene_Zone_wird_nicht_verdoppelt()
    {
        var project = new Project { Name = "Altdorf Zone 1.15" };
        project.Metadata["Zone"] = "Zone 1.15";

        var title = ExcelReportContextFactory.AusProjekt(project).TitelFuer("Haltungen");

        Assert.Equal("Altdorf Zone 1.15 Haltungen", title);
    }

    [Fact]
    public void Reiner_Schachtbericht_leitet_Jahr_aus_dem_Schachtdatensatz_ab()
    {
        var project = new Project { Name = "Schachtprojekt" };
        var record = new SchachtRecord();
        record.SetFieldValue("Ausfuehrung Datum/Jahr", "18.08.2026");
        project.SchaechteData.Add(record);

        var title = ExcelReportContextFactory
            .AusProjekt(project, schaechte: true)
            .TitelFuer("Schächte");

        Assert.Equal("Schachtprojekt / Aufnahmen 2026 Schächte", title);
    }
}

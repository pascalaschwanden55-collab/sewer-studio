using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfCleanupTests
{
    [Fact]
    public void CleanupCorruptHoldingNames_RemovesHeaderPlaceholderRows()
    {
        var project = new Project();
        var valid = NewRecord(
            haltung: "23021-22369",
            damages: "BCD @0.00m (Rohranfang)",
            direction: "Gegen Fliessrichtung",
            usage: "Schmutzwasser",
            dn: "300",
            length: "35.12",
            material: "Beton");
        var placeholder = NewRecord(
            haltung: "Datum :                Wetter :               Operator :            Auftrag Nr. :                   Bericht:",
            damages: "BCD @0.00m (Rohranfang)",
            direction: "Gegen Fliessrichtung",
            usage: "Schmutzwasser",
            dn: "300",
            length: "35.12",
            material: "Beton");

        project.Data.Add(valid);
        project.Data.Add(placeholder);

        var svc = new LegacyPdfImportService();
        var stats = svc.CleanupCorruptHoldingNames(project);

        Assert.Equal(1, stats.UpdatedRecords);
        Assert.Single(project.Data);
        Assert.Equal("23021-22369", project.Data[0].GetFieldValue("Haltungsname"));
    }

    [Fact]
    public void CleanupCorruptHoldingNames_RemovesEmptyUnknownRows()
    {
        var project = new Project();
        var valid = NewRecord(
            haltung: "23021-22369",
            damages: "BCD @0.00m (Rohranfang)",
            direction: "Gegen Fliessrichtung",
            usage: "Schmutzwasser",
            dn: "300",
            length: "35.12",
            material: "Beton");
        var unknown = NewRecord(
            haltung: "UNBEKANNT_20260211_091357_1",
            damages: "",
            direction: "",
            usage: "",
            dn: "",
            length: "",
            material: "");

        project.Data.Add(valid);
        project.Data.Add(unknown);

        var svc = new LegacyPdfImportService();
        var stats = svc.CleanupCorruptHoldingNames(project);

        Assert.Equal(1, stats.UpdatedRecords);
        Assert.Single(project.Data);
        Assert.Equal("23021-22369", project.Data[0].GetFieldValue("Haltungsname"));
    }

    private static HaltungRecord NewRecord(
        string haltung,
        string damages,
        string direction,
        string usage,
        string dn,
        string length,
        string material)
    {
        var r = new HaltungRecord();
        r.SetFieldValue("Haltungsname", haltung, FieldSource.Pdf, userEdited: false);
        r.SetFieldValue("Primaere_Schaeden", damages, FieldSource.Pdf, userEdited: false);
        r.SetFieldValue("Inspektionsrichtung", direction, FieldSource.Pdf, userEdited: false);
        r.SetFieldValue("Nutzungsart", usage, FieldSource.Pdf, userEdited: false);
        r.SetFieldValue("DN_mm", dn, FieldSource.Pdf, userEdited: false);
        r.SetFieldValue("Haltungslaenge_m", length, FieldSource.Pdf, userEdited: false);
        r.SetFieldValue("Rohrmaterial", material, FieldSource.Pdf, userEdited: false);
        return r;
    }
}


using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class SchachtProtocolApplierTests
{
    [Fact]
    public void Apply_SetztFelderPdfPfadUndProtokoll()
    {
        var parsed = new LegacyPdfImportService.ParsedSchachtFields(
            "74467", "02.10.2025", "Kontrollschacht", "BAC", null, "offen", null);
        var damages = new[] { ("Schachtdeckel", "gerissen"), ("Konus", "Riss") };
        var record = new SchachtRecord();

        var imported = SchachtProtocolApplier.Apply(record, "74467", parsed, damages, "C:/x/quelle.pdf");

        Assert.Equal("74467", record.GetFieldValue("Schachtnummer"));
        Assert.Equal("Kontrollschacht", record.GetFieldValue("Funktion"));
        Assert.Equal("C:/x/quelle.pdf", record.GetFieldValue("PDF_Path"));
        Assert.NotNull(record.Protocol);
        Assert.Equal(2, record.Protocol!.Original.Entries.Count);
        Assert.Equal("Schachtdeckel", record.Protocol!.Original.Entries[0].Code);
        Assert.Contains("Schachtnummer", imported);
        Assert.Contains("Protokoll (2 Beobachtungen)", imported);
    }
}

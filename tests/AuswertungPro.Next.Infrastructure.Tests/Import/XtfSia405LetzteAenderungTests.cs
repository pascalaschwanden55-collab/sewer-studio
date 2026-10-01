using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// <c>Letzte_Aenderung</c> kommt in SIA405 als INTERLIS-Datum (<c>2025-10-06</c>) oder als
/// <c>yyyymmdd</c>. Bis 01.10.2026 wurde nur <c>yyyymmdd</c> zu <c>dd.MM.yyyy</c>; das
/// ISO-Datum blieb roh im Feld stehen (Befund 3 aus AP08).
/// </summary>
public sealed class XtfSia405LetzteAenderungTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_letzte_aenderung_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Theory]
    [InlineData("100-101", "03.09.2026")] // yyyymmdd wie bisher
    [InlineData("101-102", "06.10.2025")] // ISO-Datum, neu
    public void Letzte_Aenderung_wird_unabhaengig_vom_Format_zu_dd_MM_yyyy(string haltung, string erwartet)
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.Combine(_dir, "sia405-referenz.xtf");
        File.Copy(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", "sia405-referenz.xtf"), pfad);

        var projekt = new Project { Name = "Test" };
        new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);

        var record = Assert.Single(projekt.Data, r => r.GetFieldValue(FieldKeys.HoldingName) == haltung);
        Assert.Equal(erwartet, record.GetFieldValue(FieldKeys.CadastreLastChange));
    }
}

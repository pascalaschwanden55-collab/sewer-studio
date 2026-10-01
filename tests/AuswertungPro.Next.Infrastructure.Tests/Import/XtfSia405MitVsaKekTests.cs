using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// SIA405-Haltungen und VSA-KEK-Untersuchungen in derselben Datei: SIA405 hat Vorrang, die
/// Untersuchungen werden nicht gelesen (unveraendert). Bis 01.10.2026 geschah das ohne jede
/// Meldung (Befund 7 aus AP08); jetzt sagt der Importbericht, wie viele liegen bleiben.
/// </summary>
public sealed class XtfSia405MitVsaKekTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtf_gemischt_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch (IOException) { /* Aufraeumen darf den Test nicht faellen. */ }
    }

    [Fact]
    public void Nicht_uebernommene_VSA_KEK_Untersuchungen_stehen_im_Importbericht()
    {
        var (projekt, stats) = Importiere("sia405-mit-vsakek.xtf");

        var meldung = Assert.Single(stats.Messages, m => m.Message.Contains("VSA-KEK-Untersuchung", StringComparison.Ordinal));
        Assert.Equal("Warn", meldung.Level);
        Assert.Contains("sia405-mit-vsakek.xtf", meldung.Message, StringComparison.Ordinal);
        Assert.Contains("enthält zusätzlich 1 VSA-KEK-Untersuchung(en), die nicht übernommen wurden", meldung.Message, StringComparison.Ordinal);

        // Kein Verhaltenswechsel: nur die SIA405-Haltung, ohne Befunde der Untersuchung.
        var haltung = Assert.Single(projekt.Data);
        Assert.Equal("refHALTUNG9", haltung.GetFieldValue(FieldKeys.CadastreObjectId));
        Assert.True(haltung.VsaFindings is null || haltung.VsaFindings.Count == 0);
    }

    [Fact]
    public void Eine_reine_SIA405_Datei_meldet_keine_VSA_KEK_Untersuchungen()
    {
        var (_, stats) = Importiere("sia405-referenz.xtf");

        Assert.DoesNotContain(stats.Messages, m => m.Message.Contains("VSA-KEK-Untersuchung", StringComparison.Ordinal));
    }

    private (Project Projekt, ImportStats Stats) Importiere(string datei)
    {
        Directory.CreateDirectory(_dir);
        var pfad = Path.Combine(_dir, datei);
        File.Copy(TestRepoPaths.RepoFile("tests", "Fixtures", "XtfReferenz", datei), pfad);

        var projekt = new Project { Name = "Test" };
        var stats = new LegacyXtfImportService().ImportXtfFiles(new[] { pfad }, projekt);
        return (projekt, stats);
    }
}
